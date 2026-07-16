#!/usr/bin/env pwsh
<#
Build the minimal FFmpeg 8.1 + x264 runtime used by MajdataViewX.
Run on the target OS:
  pwsh ./ffmpeg-builder/build.ps1
  pwsh ./ffmpeg-builder/build.ps1 -Target win-x64|osx|linux-x64
Sources and intermediates stay in ffmpeg-builder/.work. macOS output is universal.
Windows requires MSYS2 MinGW64; pass -Msys2Root if it is not C:\msys64.
#>
[CmdletBinding()]
param(
    [ValidateSet("auto", "win-x64", "osx", "linux-x64")]
    [string] $Target = "auto",
    [string] $FFmpegBranch = "release/8.1",
    [string] $X264Branch = "master",
    [ValidateRange(1, 256)]
    [int] $Jobs = [Environment]::ProcessorCount,
    [string] $Msys2Root = $env:MSYS2_ROOT
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$BuilderRoot = $PSScriptRoot
$RepositoryRoot = [IO.Path]::GetFullPath((Join-Path $BuilderRoot ".."))
$WorkRoot = Join-Path $BuilderRoot ".work"
$FFmpegSource = Join-Path $WorkRoot "src/ffmpeg"
$X264Source = Join-Path $WorkRoot "src/x264"
$OutputRoot = Join-Path $RepositoryRoot "Assets/StreamingAssets/FFmpeg"

function Invoke-Checked {
    param([string] $File, [string[]] $Arguments = @(), [string] $Cwd = $RepositoryRoot)
    Push-Location $Cwd
    try {
        & $File @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "Command failed ($LASTEXITCODE): $File $($Arguments -join ' ')"
        }
    }
    finally {
        Pop-Location
    }
}

function Sync-Repository {
    param([string] $Url, [string] $Branch, [string] $Path)
    if (Test-Path (Join-Path $Path ".git")) {
        Invoke-Checked git @("-C", $Path, "fetch", "--prune", "origin", $Branch)
        Invoke-Checked git @("-C", $Path, "checkout", $Branch)
        Invoke-Checked git @("-C", $Path, "pull", "--ff-only", "origin", $Branch)
    }
    elseif (Test-Path $Path) {
        throw "Path exists but is not a Git checkout: $Path"
    }
    else {
        Invoke-Checked git @("clone", "--branch", $Branch, "--single-branch", $Url, $Path)
    }
}

function Reset-SafeDirectory {
    param([string] $Path, [string] $AllowedRoot)
    $fullPath = [IO.Path]::GetFullPath($Path)
    $fullRoot = [IO.Path]::GetFullPath($AllowedRoot).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not ($fullPath + [IO.Path]::DirectorySeparatorChar).StartsWith(
        $fullRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to reset path outside ${AllowedRoot}: $fullPath"
    }
    if (Test-Path $fullPath) {
        Remove-Item $fullPath -Recurse -Force
    }
    New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw "Git is required."
}
if ($Target -eq "auto") {
    if ($IsWindows) { $Target = "win-x64" }
    elseif ($IsMacOS) { $Target = "osx" }
    elseif ($IsLinux) { $Target = "linux-x64" }
    else { throw "Unsupported host OS." }
}
if (($Target -eq "win-x64") -and -not $IsWindows) {
    throw "win-x64 must be built on Windows."
}
if (($Target -eq "osx") -and -not $IsMacOS) {
    throw "osx must be built on macOS with Xcode."
}
if (($Target -eq "linux-x64") -and -not $IsLinux) {
    throw "linux-x64 must be built on Linux."
}

New-Item -ItemType Directory -Path (Join-Path $WorkRoot "src") -Force | Out-Null
Sync-Repository "https://github.com/FFmpeg/FFmpeg.git" $FFmpegBranch $FFmpegSource
Sync-Repository "https://code.videolan.org/videolan/x264.git" $X264Branch $X264Source

if ($IsWindows) {
    if ([string]::IsNullOrWhiteSpace($Msys2Root)) { $Msys2Root = "C:\msys64" }
    $Bash = Join-Path $Msys2Root "usr/bin/bash.exe"
    if (-not (Test-Path $Bash)) {
        throw "MSYS2 bash not found: $Bash"
    }
}
else {
    $bashCommand = Get-Command bash -ErrorAction SilentlyContinue
    if (-not $bashCommand) { throw "bash is required." }
    $Bash = $bashCommand.Source
}

$BuildRoot = Join-Path $WorkRoot "build/$Target"
$InstallRoot = Join-Path $WorkRoot "install/$Target"
$StagingOutput = Join-Path $WorkRoot "output/$Target"
$TargetOutput = Join-Path $OutputRoot $Target
Reset-SafeDirectory $BuildRoot $WorkRoot
Reset-SafeDirectory $InstallRoot $WorkRoot
Reset-SafeDirectory $StagingOutput $WorkRoot

$env:FFMPEG_SOURCE = $FFmpegSource
$env:X264_SOURCE = $X264Source
$env:BUILD_ROOT = $BuildRoot
$env:INSTALL_ROOT = $InstallRoot
$env:OUTPUT_ROOT = $StagingOutput
$env:BUILD_TARGET = $Target
$env:BUILD_JOBS = $Jobs.ToString([Globalization.CultureInfo]::InvariantCulture)

$buildScript = @'
set -euo pipefail
path_u() {
    if command -v cygpath >/dev/null 2>&1; then cygpath -u "$1"; else printf '%s\n' "$1"; fi
}
FFMPEG_SOURCE="$(path_u "$FFMPEG_SOURCE")"
X264_SOURCE="$(path_u "$X264_SOURCE")"
BUILD_ROOT="$(path_u "$BUILD_ROOT")"
INSTALL_ROOT="$(path_u "$INSTALL_ROOT")"
OUTPUT_ROOT="$(path_u "$OUTPUT_ROOT")"
TARGET="$BUILD_TARGET"
JOBS="$BUILD_JOBS"

if [[ "$TARGET" == "win-x64" ]]; then
    export PATH="/mingw64/bin:/usr/bin:$PATH"
    export MSYSTEM=MINGW64
fi
for tool in make pkg-config nasm; do
    command -v "$tool" >/dev/null || { echo "Missing build tool: $tool" >&2; exit 1; }
done

COMMON=(
    --disable-everything
    --disable-programs
    --disable-doc
    --disable-debug
    --disable-network
    --disable-autodetect
    --disable-avdevice
    --disable-avfilter
    --disable-swresample
    --enable-avcodec
    --enable-avformat
    --enable-avutil
    --enable-swscale
    --enable-encoder=libx264
    --enable-encoder=aac
    --enable-encoder=mpeg4
    --enable-muxer=mov,mp4
    --enable-protocol=file
    --enable-gpl
    --enable-libx264
    --enable-shared
    --disable-static
    --enable-small
)

build_x264_native() {
    local host="$1" prefix="$2"
    cd "$X264_SOURCE"
    make distclean >/dev/null 2>&1 || true
    ./configure --host="$host" --prefix="$prefix" --enable-static --enable-pic --disable-cli
    make -j"$JOBS"
    make install
}

build_x264_macos() {
    local arch="$1" host="$2" prefix="$3"
    local flags="-arch $arch -mmacosx-version-min=11.0"
    cd "$X264_SOURCE"
    make distclean >/dev/null 2>&1 || true
    CC=clang CFLAGS="$flags" LDFLAGS="$flags" ./configure         --host="$host" --prefix="$prefix" --enable-static --enable-pic --disable-cli         --extra-cflags="$flags" --extra-ldflags="$flags"
    make -j"$JOBS"
    make install
}

build_ffmpeg() {
    local x264_prefix="$1" prefix="$2" build_dir="$3"
    shift 3
    rm -rf "$build_dir"
    mkdir -p "$build_dir"
    cd "$build_dir"
    export PKG_CONFIG_PATH="$x264_prefix/lib/pkgconfig"
    "$FFMPEG_SOURCE/configure"         --prefix="$prefix"         --pkg-config-flags=--static         "--extra-cflags=-I$x264_prefix/include"         "--extra-ldflags=-L$x264_prefix/lib"         "${COMMON[@]}" "$@"
    make -j"$JOBS"
    make install
}

case "$TARGET" in
win-x64)
    command -v gcc >/dev/null || { echo "MinGW64 gcc is required." >&2; exit 1; }
    X264="$INSTALL_ROOT/x264"
    FFMPEG="$INSTALL_ROOT/ffmpeg"
    build_x264_native x86_64-w64-mingw32 "$X264"
    build_ffmpeg "$X264" "$FFMPEG" "$BUILD_ROOT/ffmpeg"         --target-os=mingw32 --arch=x86_64 --enable-w32threads
    cp -L "$FFMPEG/bin/avcodec-62.dll" "$OUTPUT_ROOT/"
    cp -L "$FFMPEG/bin/avformat-62.dll" "$OUTPUT_ROOT/"
    cp -L "$FFMPEG/bin/avutil-60.dll" "$OUTPUT_ROOT/"
    cp -L "$FFMPEG/bin/swscale-9.dll" "$OUTPUT_ROOT/"
    cp -L /mingw64/bin/libwinpthread-1.dll "$OUTPUT_ROOT/"
    cp -L /mingw64/share/licenses/winpthreads/COPYING "$OUTPUT_ROOT/LICENSE.winpthread.txt"
    ;;
linux-x64)
    command -v gcc >/dev/null || { echo "gcc is required." >&2; exit 1; }
    X264="$INSTALL_ROOT/x264"
    FFMPEG="$INSTALL_ROOT/ffmpeg"
    build_x264_native x86_64-linux "$X264"
    ORIGIN_RPATH='-Wl,-rpath,$ORIGIN'
    build_ffmpeg "$X264" "$FFMPEG" "$BUILD_ROOT/ffmpeg"         --arch=x86_64 --enable-pthreads "--extra-ldflags=-L$X264/lib $ORIGIN_RPATH"
    cp -L "$FFMPEG/lib/libavcodec.so.62" "$OUTPUT_ROOT/libavcodec.so.62"
    cp -L "$FFMPEG/lib/libavformat.so.62" "$OUTPUT_ROOT/libavformat.so.62"
    cp -L "$FFMPEG/lib/libavutil.so.60" "$OUTPUT_ROOT/libavutil.so.60"
    cp -L "$FFMPEG/lib/libswscale.so.9" "$OUTPUT_ROOT/libswscale.so.9"
    ;;
osx)
    command -v clang >/dev/null || { echo "Xcode clang is required." >&2; exit 1; }
    command -v lipo >/dev/null || { echo "Xcode lipo is required." >&2; exit 1; }
    for arch in x86_64 arm64; do
        [[ "$arch" == x86_64 ]] && host=x86_64-apple-darwin || host=aarch64-apple-darwin
        X264="$INSTALL_ROOT/x264-$arch"
        FFMPEG="$INSTALL_ROOT/ffmpeg-$arch"
        FLAGS="-arch $arch -mmacosx-version-min=11.0"
        build_x264_macos "$arch" "$host" "$X264"
        build_ffmpeg "$X264" "$FFMPEG" "$BUILD_ROOT/ffmpeg-$arch"             --target-os=darwin "--arch=$arch" --cc=clang --enable-pthreads             --install-name-dir=@loader_path             "--extra-cflags=$FLAGS -I$X264/include"             "--extra-ldflags=$FLAGS -L$X264/lib"
    done
    for lib in libavcodec.62.dylib libavformat.62.dylib libavutil.60.dylib libswscale.9.dylib; do
        lipo -create             "$INSTALL_ROOT/ffmpeg-x86_64/lib/$lib"             "$INSTALL_ROOT/ffmpeg-arm64/lib/$lib"             -output "$OUTPUT_ROOT/$lib"
    done
    ;;
esac
'@

$scriptPath = Join-Path $WorkRoot "build-native.sh"
$buildScript = $buildScript.Replace("`r`n", "`n")
Set-Content $scriptPath $buildScript -Encoding utf8NoBOM
$scriptArgument = $scriptPath
if ($IsWindows) { $scriptArgument = $scriptArgument.Replace("\", "/") }
Invoke-Checked $Bash @($scriptArgument) $BuilderRoot

$ffmpegCommit = (& git -C $FFmpegSource rev-parse HEAD).Trim()
$x264Commit = (& git -C $X264Source rev-parse HEAD).Trim()
Copy-Item (Join-Path $FFmpegSource "COPYING.GPLv2") (Join-Path $StagingOutput "LICENSE.ffmpeg.txt")
Copy-Item (Join-Path $X264Source "COPYING") (Join-Path $StagingOutput "LICENSE.x264.txt")
@(
    "Target: $Target"
    "FFmpeg branch: $FFmpegBranch"
    "FFmpeg commit: $ffmpegCommit"
    "x264 branch: $X264Branch"
    "x264 commit: $x264Commit"
    "License: GPL v2 or later (FFmpeg built with libx264)"
) | Set-Content (Join-Path $StagingOutput "BUILD_INFO.txt") -Encoding utf8NoBOM

New-Item -ItemType Directory -Path $TargetOutput -Force | Out-Null
try {
    Get-ChildItem -LiteralPath $StagingOutput -File | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $TargetOutput $_.Name) -Force
    }
}
catch {
    throw "FFmpeg was built successfully in '$StagingOutput', but deployment to " +
        "'$TargetOutput' failed. Close Unity or any process using the DLLs, then run the script again. $($_.Exception.Message)"
}
Write-Host "Built FFmpeg runtime: $TargetOutput"

