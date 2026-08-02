#Requires -Version 5.1

<#
.SYNOPSIS
Builds the minimal win-x64 FFmpeg shared runtime required by RenderingOut.

.DESCRIPTION
The build intentionally keeps only the FFmpeg features used by RenderingOut:

  Video encoders : h264_nvenc, h264_mf, h264_amf, h264_qsv, libx264
  Audio encoder  : aac
  Container      : MOV/MP4 muxer
  I/O            : file output protocol
  Conversion     : libswscale and libswresample
  Hardware       : D3D11VA device/frame contexts

All decoders, demuxers, filters, devices, network protocols, documentation and
FFmpeg command-line programs are disabled. x264 and the oneVPL dispatcher are
linked statically into the FFmpeg DLLs so the runtime package stays small.

Every normal invocation installs/updates the required MSYS2 packages (including
Git, GCC, CMake, Ninja, NASM and pkg-config), then updates the tracked stable
FFmpeg release branch and refreshes x264, nv-codec-headers, AMD AMF and Intel
oneVPL from their official Git repositories. No prebuilt codec SDK or manually
configured dependency path is required. Use -SkipToolchainUpdate only in a
controlled CI image whose packages were already updated immediately before this
script ran. Source updates cannot be skipped.

The finished runtime is written to .\dist\win-x64. No EXE is placed there.

.EXAMPLE
powershell -ExecutionPolicy Bypass -File .\build.ps1

.EXAMPLE
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Msys2Root C:\msys64 -Jobs 16
#>

[CmdletBinding()]
param(
    [ValidateRange(1, 256)]
    [int] $Jobs = [Environment]::ProcessorCount,

    [string] $Msys2Root = $(if ($env:MSYS2_ROOT) { $env:MSYS2_ROOT } else { "C:\msys64" }),

    [switch] $SkipToolchainUpdate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$BuilderRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$WorkRoot = Join-Path $BuilderRoot ".work"
$SourceRoot = Join-Path $WorkRoot "src"
$BuildRoot = Join-Path $WorkRoot "build"
$InstallRoot = Join-Path $WorkRoot "install"
$StagingRoot = Join-Path $WorkRoot "package"
$OutputRoot = Join-Path $BuilderRoot "dist\win-x64"
$FFmpegReleaseBranch = "release/8.1"

$Sources = [ordered]@{
    FFmpeg = @{
        Url = "https://github.com/FFmpeg/FFmpeg.git"
        Ref = $FFmpegReleaseBranch
        Path = Join-Path $SourceRoot "ffmpeg"
    }
    x264 = @{
        Url = "https://code.videolan.org/videolan/x264.git"
        Ref = "master"
        Path = Join-Path $SourceRoot "x264"
    }
    NvCodecHeaders = @{
        Url = "https://github.com/FFmpeg/nv-codec-headers.git"
        Ref = "master"
        Path = Join-Path $SourceRoot "nv-codec-headers"
    }
    AMF = @{
        Url = "https://github.com/GPUOpen-LibrariesAndSDKs/AMF.git"
        Ref = "master"
        Path = Join-Path $SourceRoot "AMF"
    }
    oneVPL = @{
        Url = "https://github.com/intel/libvpl.git"
        Ref = "main"
        Path = Join-Path $SourceRoot "libvpl"
    }
}

function Write-Step {
    param([Parameter(Mandatory)][string] $Message)
    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory)][string] $FilePath,
        [string[]] $ArgumentList = @(),
        [string] $WorkingDirectory = $BuilderRoot
    )

    Push-Location -LiteralPath $WorkingDirectory
    try {
        & $FilePath @ArgumentList
        if ($LASTEXITCODE -ne 0) {
            throw "Command failed with exit code $LASTEXITCODE`: $FilePath $($ArgumentList -join ' ')"
        }
    }
    finally {
        Pop-Location
    }
}

function Assert-PathInside {
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][string] $AllowedRoot
    )

    $fullPath = [IO.Path]::GetFullPath($Path).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar)
    $fullRoot = [IO.Path]::GetFullPath($AllowedRoot).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar)
    $rootPrefix = $fullRoot + [IO.Path]::DirectorySeparatorChar

    if ($fullPath.Equals($fullRoot, [StringComparison]::OrdinalIgnoreCase) -or
        -not $fullPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify path outside the intended root '$fullRoot': '$fullPath'"
    }
}

function Reset-OwnedDirectory {
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][string] $AllowedRoot
    )

    Assert-PathInside -Path $Path -AllowedRoot $AllowedRoot
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

function Sync-Repository {
    param(
        [Parameter(Mandatory)][string] $Name,
        [Parameter(Mandatory)][string] $Url,
        [Parameter(Mandatory)][string] $Ref,
        [Parameter(Mandatory)][string] $Path
    )

    Assert-PathInside -Path $Path -AllowedRoot $SourceRoot

    if (Test-Path -LiteralPath (Join-Path $Path ".git")) {
        $actualRemote = (& $script:Git -C $Path remote get-url origin).Trim()
        if ($LASTEXITCODE -ne 0) {
            throw "Unable to read the origin URL for $Name at '$Path'."
        }
        if (-not $actualRemote.Equals($Url, [StringComparison]::OrdinalIgnoreCase)) {
            throw "$Name source directory has an unexpected origin. Expected '$Url', found '$actualRemote'."
        }

        Write-Step "Updating $Name ($Ref)"
        Invoke-Checked $script:Git @("-C", $Path, "fetch", "--prune", "--no-tags", "--depth=1", "origin", $Ref)
        Invoke-Checked $script:Git @("-C", $Path, "checkout", "--detach", "--force", "FETCH_HEAD")
        Invoke-Checked $script:Git @("-C", $Path, "clean", "-ffd")
    }
    elseif (Test-Path -LiteralPath $Path) {
        throw "Source path exists but is not the expected Git checkout: '$Path'"
    }
    else {
        Write-Step "Cloning $Name ($Ref)"
        Invoke-Checked $script:Git @(
            "clone",
            "--depth=1",
            "--no-tags",
            "--single-branch",
            "--branch", $Ref,
            $Url,
            $Path)
    }
}

if (-not [Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [Runtime.InteropServices.OSPlatform]::Windows)) {
    throw "This script builds only win-x64 and must run on Windows."
}

if (-not [Environment]::Is64BitOperatingSystem) {
    throw "A 64-bit Windows host is required."
}

$Msys2Root = [IO.Path]::GetFullPath($Msys2Root)
$Bash = Join-Path $Msys2Root "usr\bin\bash.exe"
if (-not (Test-Path -LiteralPath $Bash -PathType Leaf)) {
    throw @"
MSYS2 was not found at '$Msys2Root'.
Install the current x86_64 MSYS2 distribution from https://www.msys2.org/,
or pass its location with -Msys2Root.
"@
}

# MSYS2's login profile selects the UCRT64 toolchain from this value. Each
# invocation starts a fresh process, which is important if msys2-runtime itself
# was upgraded by the preceding pacman call.
$env:MSYSTEM = "UCRT64"
$env:CHERE_INVOKING = "1"
$env:MSYS2_PATH_TYPE = "inherit"

if (-not $SkipToolchainUpdate) {
    Write-Step "Updating the MSYS2 installation"
    # MSYS2's documented full-update procedure requires a second invocation
    # from a new process when a core package (especially msys2-runtime) closes
    # the first shell. Running the second pass unconditionally is harmless when
    # the first pass already completed the full upgrade.
    Invoke-Checked $Bash @("--login", "-c", "pacman -Syu --noconfirm")
    Invoke-Checked $Bash @("--login", "-c", "pacman -Syu --noconfirm")

    Write-Step "Installing/updating the UCRT64 build toolchain"
    $toolchainPackages = @(
        "git"
        "make"
        "diffutils"
        "mingw-w64-ucrt-x86_64-gcc"
        "mingw-w64-ucrt-x86_64-cmake"
        "mingw-w64-ucrt-x86_64-ninja"
        "mingw-w64-ucrt-x86_64-nasm"
        "mingw-w64-ucrt-x86_64-pkgconf"
    )
    $installToolchain = "pacman -S --needed --noconfirm " + ($toolchainPackages -join " ")
    Invoke-Checked $Bash @("--login", "-c", $installToolchain)
}
else {
    Write-Warning "Skipping the MSYS2 package update; source repositories are still updated."
}

$gitCommand = Get-Command git -ErrorAction SilentlyContinue
if ($gitCommand) {
    $script:Git = $gitCommand.Source
}
else {
    $script:Git = Join-Path $Msys2Root "usr\bin\git.exe"
}
if (-not (Test-Path -LiteralPath $script:Git -PathType Leaf)) {
    throw "Git is unavailable. Run without -SkipToolchainUpdate so the script can install it."
}

New-Item -ItemType Directory -Path $SourceRoot -Force | Out-Null
foreach ($entry in $Sources.GetEnumerator()) {
    Sync-Repository `
        -Name $entry.Key `
        -Url $entry.Value.Url `
        -Ref $entry.Value.Ref `
        -Path $entry.Value.Path
}

Reset-OwnedDirectory -Path $BuildRoot -AllowedRoot $WorkRoot
Reset-OwnedDirectory -Path $InstallRoot -AllowedRoot $WorkRoot
Reset-OwnedDirectory -Path $StagingRoot -AllowedRoot $WorkRoot

$env:FFBUILD_FFMPEG_SOURCE = $Sources.FFmpeg.Path
$env:FFBUILD_X264_SOURCE = $Sources.x264.Path
$env:FFBUILD_NV_SOURCE = $Sources.NvCodecHeaders.Path
$env:FFBUILD_AMF_SOURCE = $Sources.AMF.Path
$env:FFBUILD_VPL_SOURCE = $Sources.oneVPL.Path
$env:FFBUILD_BUILD_ROOT = $BuildRoot
$env:FFBUILD_INSTALL_ROOT = $InstallRoot
$env:FFBUILD_STAGING_ROOT = $StagingRoot
$env:FFBUILD_JOBS = $Jobs.ToString([Globalization.CultureInfo]::InvariantCulture)

$buildDriver = @'
set -euo pipefail

export PATH="/ucrt64/bin:/usr/bin:$PATH"
export PKG_CONFIG_SYSTEM_INCLUDE_PATH=
export PKG_CONFIG_SYSTEM_LIBRARY_PATH=

to_posix() {
    cygpath -u "$1"
}

FFMPEG_SOURCE="$(to_posix "$FFBUILD_FFMPEG_SOURCE")"
X264_SOURCE="$(to_posix "$FFBUILD_X264_SOURCE")"
NV_SOURCE="$(to_posix "$FFBUILD_NV_SOURCE")"
AMF_SOURCE="$(to_posix "$FFBUILD_AMF_SOURCE")"
VPL_SOURCE="$(to_posix "$FFBUILD_VPL_SOURCE")"
BUILD_ROOT="$(to_posix "$FFBUILD_BUILD_ROOT")"
INSTALL_ROOT="$(to_posix "$FFBUILD_INSTALL_ROOT")"
STAGING_ROOT="$(to_posix "$FFBUILD_STAGING_ROOT")"
JOBS="$FFBUILD_JOBS"

DEPS_PREFIX="$INSTALL_ROOT/deps"
FFMPEG_PREFIX="$INSTALL_ROOT/ffmpeg"
VPL_BUILD="$BUILD_ROOT/libvpl"
FFMPEG_BUILD="$BUILD_ROOT/ffmpeg"
PROBE_BUILD="$BUILD_ROOT/probe"

for tool in make cmake ninja gcc g++ ar nasm pkg-config objdump strip; do
    if ! command -v "$tool" >/dev/null 2>&1; then
        echo "Required UCRT64 build tool is missing: $tool" >&2
        exit 1
    fi
done

mkdir -p "$DEPS_PREFIX" "$FFMPEG_PREFIX" "$VPL_BUILD" "$FFMPEG_BUILD" "$PROBE_BUILD"

echo
echo "==> Installing current NVIDIA codec headers"
make -C "$NV_SOURCE" PREFIX="$DEPS_PREFIX" install

echo
echo "==> Installing current AMD AMF headers"
if [[ ! -d "$AMF_SOURCE/amf/public/include" ]]; then
    echo "AMD AMF public headers were not found in the expected official repository layout." >&2
    exit 1
fi
mkdir -p "$DEPS_PREFIX/include/AMF"
cp -a "$AMF_SOURCE/amf/public/include/." "$DEPS_PREFIX/include/AMF/"

echo
echo "==> Building minimal 8-bit 4:2:0 x264 static library"
cd "$X264_SOURCE"
make distclean >/dev/null 2>&1 || true
./configure \
    --host=x86_64-w64-mingw32 \
    --prefix="$DEPS_PREFIX" \
    --enable-static \
    --disable-cli \
    --enable-pic \
    --bit-depth=8 \
    --chroma-format=420 \
    --disable-opencl \
    --extra-cflags="-Os -ffunction-sections -fdata-sections"
make -j"$JOBS"
make install

echo
echo "==> Building the Intel oneVPL dispatcher as a static library"
cmake \
    -S "$VPL_SOURCE" \
    -B "$VPL_BUILD" \
    -G Ninja \
    -DCMAKE_BUILD_TYPE=MinSizeRel \
    -DCMAKE_CXX_FLAGS=-D_GLIBCXX_USE_CXX11_ABI=0 \
    -DCMAKE_INSTALL_PREFIX="$DEPS_PREFIX" \
    -DCMAKE_INSTALL_LIBDIR=lib \
    -DBUILD_SHARED_LIBS=OFF \
    -DBUILD_TESTS=OFF \
    -DBUILD_EXAMPLES=OFF \
    -DBUILD_EXPERIMENTAL=OFF \
    -DINSTALL_DEV=ON \
    -DINSTALL_LIB=ON \
    -DINSTALL_EXAMPLES=OFF \
    -DCMAKE_C_FLAGS_MINSIZEREL="-Os -DNDEBUG -ffunction-sections -fdata-sections" \
    -DCMAKE_CXX_FLAGS_MINSIZEREL="-Os -DNDEBUG -ffunction-sections -fdata-sections"
cmake --build "$VPL_BUILD" --parallel "$JOBS"
cmake --install "$VPL_BUILD"

# oneVPL exposes a C ABI but its dispatcher is implemented in C++. Record the
# static C++ runtime as a private dependency so libavcodec remains self-contained.
VPL_PC="$DEPS_PREFIX/lib/pkgconfig/vpl.pc"
if ! grep -Fq -- '-l:libstdc++.a' "$VPL_PC"; then
    sed -i 's#^Libs\.private:.*#Libs.private: -l:libstdc++.a#' "$VPL_PC"
fi

export PKG_CONFIG_PATH="$DEPS_PREFIX/lib/pkgconfig"
export PKG_CONFIG_LIBDIR="$DEPS_PREFIX/lib/pkgconfig"

for package in x264 ffnvcodec vpl; do
    if ! pkg-config --exists "$package"; then
        echo "Dependency '$package' was built but is not visible to pkg-config." >&2
        exit 1
    fi
done

echo
echo "==> Configuring minimal shared FFmpeg libraries"
cd "$FFMPEG_BUILD"
"$FFMPEG_SOURCE/configure" \
    --prefix="$FFMPEG_PREFIX" \
    --target-os=mingw32 \
    --arch=x86_64 \
    --enable-shared \
    --disable-static \
    --enable-small \
    --enable-gpl \
    --disable-programs \
    --disable-doc \
    --disable-debug \
    --disable-network \
    --disable-autodetect \
    --disable-avdevice \
    --disable-avfilter \
    --disable-pthreads \
    --enable-w32threads \
    --disable-everything \
    --enable-avcodec \
    --enable-avformat \
    --enable-avutil \
    --enable-swscale \
    --enable-swresample \
    --enable-encoder=aac \
    --enable-encoder=h264_nvenc \
    --enable-encoder=h264_mf \
    --enable-encoder=h264_amf \
    --enable-encoder=h264_qsv \
    --enable-encoder=libx264 \
    --enable-muxer=mov,mp4 \
    --enable-protocol=file \
    --enable-libx264 \
    --enable-ffnvcodec \
    --enable-nvenc \
    --disable-nvdec \
    --disable-cuvid \
    --enable-amf \
    --enable-mediafoundation \
    --enable-libvpl \
    --enable-d3d11va \
    --disable-d3d12va \
    --disable-dxva2 \
    --disable-vulkan \
    --disable-iconv \
    --disable-zlib \
    --disable-bzlib \
    --disable-lzma \
    --disable-schannel \
    --disable-sdl2 \
    --disable-swscale-alpha \
    --pkg-config-flags=--static \
    --extra-cflags="-I$DEPS_PREFIX/include -ffunction-sections -fdata-sections" \
    --extra-ldflags="-L$DEPS_PREFIX/lib -Wl,--gc-sections -static-libgcc -static-libstdc++"

required_config=(
    CONFIG_AAC_ENCODER
    CONFIG_H264_NVENC_ENCODER
    CONFIG_H264_MF_ENCODER
    CONFIG_H264_AMF_ENCODER
    CONFIG_H264_QSV_ENCODER
    CONFIG_LIBX264_ENCODER
    CONFIG_MOV_MUXER
    CONFIG_MP4_MUXER
    CONFIG_FILE_PROTOCOL
)
for item in "${required_config[@]}"; do
    if ! grep -Eq "^#define ${item} 1$" config_components.h; then
        echo "FFmpeg configure did not enable required component: $item" >&2
        exit 1
    fi
done

for forbidden in CONFIG_AVFILTER CONFIG_AVDEVICE; do
    if grep -Eq "^#define ${forbidden} 1$" config.h; then
        echo "FFmpeg configure unexpectedly enabled forbidden library: $forbidden" >&2
        exit 1
    fi
done

echo
echo "==> Building and installing FFmpeg DLLs"
make -j"$JOBS"
make install

cat > "$PROBE_BUILD/probe.c" <<'PROBE_EOF'
#include <stdio.h>
#include <string.h>
#include <libavcodec/avcodec.h>
#include <libavformat/avformat.h>
#include <libavutil/hwcontext.h>
#include <libavutil/pixfmt.h>
#include <libswresample/swresample.h>
#include <libswscale/swscale.h>

static int require_encoder(const char *name)
{
    if (avcodec_find_encoder_by_name(name))
        return 0;
    fprintf(stderr, "missing encoder: %s\n", name);
    return 1;
}

int main(void)
{
    const char *encoders[] = {
        "h264_nvenc", "h264_mf", "h264_amf", "h264_qsv", "libx264", "aac"
    };
    void *protocol_state = NULL;
    const char *protocol;
    int found_file_protocol = 0;
    int failed = 0;
    unsigned int i;

    for (i = 0; i < sizeof(encoders) / sizeof(encoders[0]); ++i)
        failed |= require_encoder(encoders[i]);

    if (!av_guess_format("mp4", NULL, NULL)) {
        fprintf(stderr, "missing MP4/MOV muxer\n");
        failed = 1;
    }

    while ((protocol = avio_enum_protocols(&protocol_state, 1)) != NULL) {
        if (strcmp(protocol, "file") == 0)
            found_file_protocol = 1;
    }
    if (!found_file_protocol) {
        fprintf(stderr, "missing file output protocol\n");
        failed = 1;
    }

    if (av_hwdevice_find_type_by_name("d3d11va") == AV_HWDEVICE_TYPE_NONE) {
        fprintf(stderr, "missing D3D11VA hardware device support\n");
        failed = 1;
    }

    if (!sws_isSupportedInput(AV_PIX_FMT_BGRA) ||
        !sws_isSupportedInput(AV_PIX_FMT_RGBA) ||
        !sws_isSupportedInput(AV_PIX_FMT_NV12) ||
        !sws_isSupportedOutput(AV_PIX_FMT_NV12)) {
        fprintf(stderr, "missing required libswscale pixel-format support\n");
        failed = 1;
    }

    {
        SwrContext *swr = swr_alloc();
        if (!swr) {
            fprintf(stderr, "libswresample allocation failed\n");
            failed = 1;
        }
        swr_free(&swr);
    }

    if (!failed)
        puts("RenderingOut FFmpeg capability probe passed.");
    return failed;
}
PROBE_EOF

echo
echo "==> Verifying the installed DLLs through the public FFmpeg APIs"
gcc \
    -Os \
    -I"$FFMPEG_PREFIX/include" \
    "$PROBE_BUILD/probe.c" \
    -L"$FFMPEG_PREFIX/lib" \
    -lavformat -lavcodec -lswscale -lswresample -lavutil \
    -o "$PROBE_BUILD/probe.exe"
PATH="$FFMPEG_PREFIX/bin:$PATH" "$PROBE_BUILD/probe.exe"

echo
echo "==> Packaging DLLs and their non-system runtime dependencies"
expected_core_dlls=(
    "avcodec-62.dll"
    "avformat-62.dll"
    "avutil-60.dll"
    "swscale-9.dll"
    "swresample-6.dll"
)

for filename in "${expected_core_dlls[@]}"; do
    if [[ ! -f "$FFMPEG_PREFIX/bin/$filename" ]]; then
        echo "FFmpeg release ABI mismatch: expected '$filename'." >&2
        find "$FFMPEG_PREFIX/bin" -maxdepth 1 -type f -iname '*.dll' -printf '  %f\n' | sort >&2
        exit 1
    fi
    cp -a "$FFMPEG_PREFIX/bin/$filename" "$STAGING_ROOT/"
done

# FFmpeg and oneVPL are linked with static GCC/C++ runtimes where possible.
# If a current toolchain introduces a remaining UCRT64 DLL dependency, discover
# it recursively and ship it instead of leaving a machine-specific dependency.
declare -a scan_queue=("$STAGING_ROOT"/*.dll)
scan_index=0
while [[ "$scan_index" -lt "${#scan_queue[@]}" ]]; do
    current="${scan_queue[$scan_index]}"
    scan_index=$((scan_index + 1))

    while IFS= read -r dependency; do
        [[ -n "$dependency" ]] || continue

        already_present=
        for packaged in "$STAGING_ROOT"/*.dll; do
            if [[ "${packaged##*/}" == "$dependency" ]]; then
                already_present=1
                break
            fi
        done
        [[ -z "$already_present" ]] || continue

        candidate=
        for directory in "$FFMPEG_PREFIX/bin" "$DEPS_PREFIX/bin" /ucrt64/bin; do
            if [[ -f "$directory/$dependency" ]]; then
                candidate="$directory/$dependency"
                break
            fi
        done
        [[ -n "$candidate" ]] || continue

        cp -a "$candidate" "$STAGING_ROOT/"
        scan_queue+=("$STAGING_ROOT/$dependency")
    done < <(objdump -p "$current" | sed -n 's/^[[:space:]]*DLL Name: //p')
done

if compgen -G "$STAGING_ROOT/*.exe" >/dev/null; then
    echo "An EXE unexpectedly entered the runtime package." >&2
    exit 1
fi

cp "$FFMPEG_BUILD/config.h" "$STAGING_ROOT/FFMPEG_CONFIG.h"
cp "$FFMPEG_BUILD/config_components.h" "$STAGING_ROOT/FFMPEG_COMPONENTS.h"
cp "$FFMPEG_SOURCE/COPYING.GPLv2" "$STAGING_ROOT/LICENSE.FFmpeg-GPLv2.txt"
cp "$X264_SOURCE/COPYING" "$STAGING_ROOT/LICENSE.x264.txt"
cp "$VPL_SOURCE/LICENSE" "$STAGING_ROOT/LICENSE.oneVPL.txt"

if [[ -f "$AMF_SOURCE/LICENSE.txt" ]]; then
    cp "$AMF_SOURCE/LICENSE.txt" "$STAGING_ROOT/LICENSE.AMF.txt"
elif [[ -f "$AMF_SOURCE/LICENSE" ]]; then
    cp "$AMF_SOURCE/LICENSE" "$STAGING_ROOT/LICENSE.AMF.txt"
fi

echo
echo "Packaged runtime DLLs:"
find "$STAGING_ROOT" -maxdepth 1 -type f -iname '*.dll' -printf '  %f\n' | sort
'@

$driverPath = Join-Path $WorkRoot "build-driver.sh"
[IO.File]::WriteAllText(
    $driverPath,
    $buildDriver.Replace("`r`n", "`n") + "`n",
    [Text.UTF8Encoding]::new($false))
Write-Step "Building FFmpeg and validating the RenderingOut feature set"
$driverArgument = $driverPath.Replace("\", "/")
Invoke-Checked $Bash @("--login", $driverArgument)

$buildInfo = [Collections.Generic.List[string]]::new()
$buildInfo.Add("Target: win-x64")
$buildInfo.Add("Built at (UTC): $([DateTime]::UtcNow.ToString("O", [Globalization.CultureInfo]::InvariantCulture))")
$buildInfo.Add("FFmpeg release branch: $FFmpegReleaseBranch")
$buildInfo.Add("License: GPL v2 or later (FFmpeg is built with GPL-licensed x264)")
$buildInfo.Add("")
$buildInfo.Add("Enabled encoders: h264_nvenc, h264_mf, h264_amf, h264_qsv, libx264, aac")
$buildInfo.Add("Enabled muxers: mov, mp4")
$buildInfo.Add("Enabled protocol: file")
$buildInfo.Add("Enabled libraries: avcodec, avformat, avutil, swscale, swresample")
$buildInfo.Add("Enabled hardware context: d3d11va")
$buildInfo.Add("")
$buildInfo.Add("Deployment: copy every packaged DLL beside RenderingOut.dll; do not put them in a nested directory.")
$buildInfo.Add("")
$buildInfo.Add("Source revisions:")

foreach ($entry in $Sources.GetEnumerator()) {
    $commit = (& $script:Git -C $entry.Value.Path rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to record the source revision for $($entry.Key)."
    }
    $buildInfo.Add("  $($entry.Key): $commit ($($entry.Value.Ref))")
}

[IO.File]::WriteAllLines(
    (Join-Path $StagingRoot "BUILD_INFO.txt"),
    $buildInfo,
    [Text.UTF8Encoding]::new($false))

$runtimeDlls = @(Get-ChildItem -LiteralPath $StagingRoot -Filter "*.dll" -File)
if ($runtimeDlls.Count -lt 5) {
    throw "The build completed but fewer than the five required FFmpeg DLLs were packaged."
}

$unexpectedExecutables = @(Get-ChildItem -LiteralPath $StagingRoot -Filter "*.exe" -File)
if ($unexpectedExecutables.Count -ne 0) {
    throw "The staging package unexpectedly contains executable files."
}

Reset-OwnedDirectory -Path $OutputRoot -AllowedRoot $BuilderRoot
Copy-Item -Path (Join-Path $StagingRoot "*") -Destination $OutputRoot -Recurse -Force

Write-Host ""
Write-Host "Build complete: $OutputRoot" -ForegroundColor Green
Write-Host "Deploy every DLL below beside RenderingOut.dll (not in a nested directory)." -ForegroundColor Yellow
Write-Host "Runtime DLLs:"
Get-ChildItem -LiteralPath $OutputRoot -Filter "*.dll" -File |
    Sort-Object Name |
    ForEach-Object {
        Write-Host ("  {0,-28} {1,10:N0} bytes" -f $_.Name, $_.Length)
    }
