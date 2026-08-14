#!/usr/bin/env bash
# build-windows.sh — Build the private MajdataX Windows x64 bundle (Unity
# player + .NET editor + Skin/SFX) as a local ZIP. No release publishing.
#
# Usage:
#   scripts/build-windows.sh                          # build only
#   scripts/build-windows.sh --version v6.1.1-win.4  # explicit version
#
# Environment overrides:
#   UNITY_PATH      Path to the Unity editor binary
#                   (default: /home/davidscann/Unity/Hub/Editor/6000.3.19f1/Editor/Unity)
#   DOTNET_PATH     Path to dotnet (default: dotnet on $PATH)
#   EDITOR_REPO     Path to MajdataEdit-Neo checkout (default: ../MajdataEdit-Neo-Linux)
#   BUNDLE_NAME     Output bundle dir name (default: MajdataX-Windows-<VERSION>)
#   SKIP_UNITY      If set, skip the Unity build and reuse build/Windows
#   SKIP_DOTNET     If set, skip the dotnet publish and reuse $DIST_DIR/editor-publish-win
#
# Layout note: the Windows player is a Mono build and probes its exe directory
# for assemblies, so it must NOT share a directory with the editor's .NET
# runtime DLLs. The bundle therefore keeps the player under View/.
#
# Required tools: bash, git, zip, dotnet, and Unity with Windows Build Support
# (Mono). The BuildScript switches the scripting backend to Mono for Windows
# automatically; no IL2CPP Windows module is needed.

set -euo pipefail

# ---------- Defaults ----------
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
UNITY_PATH="${UNITY_PATH:-/home/davidscann/Unity/Hub/Editor/6000.3.19f1/Editor/Unity}"
DOTNET_PATH="${DOTNET_PATH:-dotnet}"
EDITOR_REPO="${EDITOR_REPO:-$(cd "$REPO_ROOT/.." && pwd)/MajdataEdit-Neo-Linux}"
VERSION_BASE="${VERSION_BASE:-v6.1.1-win}"
DIST_DIR="${DIST_DIR:-$REPO_ROOT/dist}"

EXPLICIT_VERSION=""

# ---------- Parse args ----------
while [[ $# -gt 0 ]]; do
  case "$1" in
    --version) EXPLICIT_VERSION="$2"; shift 2 ;;
    --version=*) EXPLICIT_VERSION="${1#*=}"; shift ;;
    --help|-h)
      sed -n '2,25p' "$0" | sed 's/^# \?//'
      exit 0 ;;
    *) echo "Unknown arg: $1" >&2; exit 2 ;;
  esac
done

# ---------- Helpers ----------
log()  { printf '\033[1;34m[build]\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m[warn]\033[0m %s\n' "$*" >&2; }
die()  { printf '\033[1;31m[err ]\033[0m %s\n' "$*" >&2; exit 1; }

# ---------- Stray-instance guard ----------
kill_strays() {
  for proc in MajdataEdit-Neo MajdataViewX; do
    if pgrep -x "$proc" >/dev/null 2>&1; then
      log "Killing running $proc instance(s) - they can block the build"
      pkill -9 -x "$proc" || true
    fi
  done
}
kill_strays

# ---------- Preflight ----------
[[ -d "$REPO_ROOT" ]] || die "Repo not found: $REPO_ROOT"
[[ -d "$EDITOR_REPO" ]] || die "Editor repo not found: $EDITOR_REPO"
command -v git >/dev/null || die "git is required"
command -v zip >/dev/null || die "zip is required"
command -v "$DOTNET_PATH" >/dev/null 2>&1 || die "dotnet not found (or DOTNET_PATH is wrong)"

cd "$REPO_ROOT"

# ---------- Version ----------
if [[ -n "$EXPLICIT_VERSION" ]]; then
  VERSION="$EXPLICIT_VERSION"
else
  VERSION="${VERSION_BASE}-$(date +%Y%m%d-%H%M%S)"
fi

BUNDLE_NAME="${BUNDLE_NAME:-MajdataX-Windows-${VERSION}}"
BUNDLE_DIR="$DIST_DIR/$BUNDLE_NAME"
ZIP_PATH="$DIST_DIR/${BUNDLE_NAME}.zip"
EDITOR_PUBLISH_DIR="$DIST_DIR/editor-publish-win"

log "Version:  $VERSION"
log "Bundle:   $BUNDLE_DIR"
log "Zip:      $ZIP_PATH"
log "Repo:     $REPO_ROOT"
log "Editor:   $EDITOR_REPO"
log "Unity:    $UNITY_PATH"

# ---------- 1. Build the Unity Windows player (Mono) ----------
if [[ -z "${SKIP_UNITY:-}" ]]; then
  log "Building Unity Windows player (Mono backend)..."
  [[ -x "$UNITY_PATH" ]] || die "Unity not found or not executable: $UNITY_PATH"

  UNITY_BUILD_DIR="$REPO_ROOT/build/Windows"
  rm -rf "$UNITY_BUILD_DIR"

  BUILD_LOG="$DIST_DIR/unity-build-win-$(date +%Y%m%d-%H%M%S).log"
  log "Unity log: $BUILD_LOG"
  BUILD_TARGET=StandaloneWindows64 \
  BUILD_OUTPUT_PATH=build/Windows/MajdataViewX.exe \
    "$UNITY_PATH" \
      -batchmode -nographics -quit \
      -projectPath "$REPO_ROOT" \
      -executeMethod BuildScript.Build \
      -logFile "$BUILD_LOG" \
      -buildTarget StandaloneWindows64

  [[ -f "$UNITY_BUILD_DIR/MajdataViewX.exe" ]] \
    || die "Unity build did not produce MajdataViewX.exe in $UNITY_BUILD_DIR — see $BUILD_LOG"
  log "Unity build OK: $UNITY_BUILD_DIR"
else
  log "SKIP_UNITY set; reusing existing build at $REPO_ROOT/build/Windows"
  UNITY_BUILD_DIR="$REPO_ROOT/build/Windows"
  [[ -f "$UNITY_BUILD_DIR/MajdataViewX.exe" ]] \
    || die "SKIP_UNITY set but no existing build at $UNITY_BUILD_DIR"
fi

# ---------- 2. Publish the .NET editor (self-contained) ----------
if [[ -z "${SKIP_DOTNET:-}" ]]; then
  log "Publishing MajdataEdit-Neo (self-contained win-x64)..."
  EDITOR_CSPROJ="$EDITOR_REPO/MajdataEdit-Neo.csproj"
  [[ -f "$EDITOR_CSPROJ" ]] || die "Editor csproj not found: $EDITOR_CSPROJ"

  rm -rf "$EDITOR_PUBLISH_DIR"
  "$DOTNET_PATH" publish "$EDITOR_CSPROJ" \
    -c Release \
    -r win-x64 \
    --self-contained true \
    -o "$EDITOR_PUBLISH_DIR"

  [[ -f "$EDITOR_PUBLISH_DIR/MajdataEdit-Neo.exe" ]] \
    || die "dotnet publish did not produce $EDITOR_PUBLISH_DIR/MajdataEdit-Neo.exe"
  log "Editor build OK: $EDITOR_PUBLISH_DIR"
else
  log "SKIP_DOTNET set; reusing existing publish at $DIST_DIR/editor-publish-win"
  EDITOR_PUBLISH_DIR="$DIST_DIR/editor-publish-win"
  [[ -f "$EDITOR_PUBLISH_DIR/MajdataEdit-Neo.exe" ]] \
    || die "SKIP_DOTNET set but no existing publish at $EDITOR_PUBLISH_DIR"
fi

# ---------- 3. Assemble the bundle ----------
log "Assembling bundle at $BUNDLE_DIR ..."
rm -rf "$BUNDLE_DIR" "$ZIP_PATH"
mkdir -p "$BUNDLE_DIR/View" "$DIST_DIR"

# 3a. Unity player under View/ (keeps its assembly probing away from the
#     editor's .NET runtime DLLs at the bundle root).
cp -a "$UNITY_BUILD_DIR/." "$BUNDLE_DIR/View/"
rm -rf "$BUNDLE_DIR/View/MajdataViewX_BurstDebugInformation_DoNotShip"

# 3b. Editor publish at the bundle root.
cp -a "$EDITOR_PUBLISH_DIR/." "$BUNDLE_DIR/"

# 3c. Skin + SFX next to the player (SkinManager/AudioManager resolve them
#     relative to the player exe directory).
if [[ -d "$EDITOR_REPO/BinaryAssets/Skin" && -d "$EDITOR_REPO/BinaryAssets/SFX" ]]; then
  log "Copying BinaryAssets (Skin/SFX) from $EDITOR_REPO"
  cp -a "$EDITOR_REPO/BinaryAssets/Skin" "$EDITOR_REPO/BinaryAssets/SFX" "$BUNDLE_DIR/View/"
else
  warn "BinaryAssets/Skin or BinaryAssets/SFX missing in $EDITOR_REPO — bundle will lack skin/SFX"
  warn "Run: git -C '$EDITOR_REPO' submodule update --init --recursive"
fi

# 3d. The editor loads bassopus.dll from its own directory (TrackReader).
if [[ -f "$BUNDLE_DIR/View/MajdataViewX_Data/Plugins/x86_64/bassopus.dll" ]]; then
  cp "$BUNDLE_DIR/View/MajdataViewX_Data/Plugins/x86_64/bassopus.dll" "$BUNDLE_DIR/bassopus.dll"
else
  warn "bassopus.dll not found in the Unity build — Opus tracks may not decode"
fi

# 3e. Manifest.
cat > "$BUNDLE_DIR/VERSION" <<EOF
$VERSION
Built:    $(date -u +%Y-%m-%dT%H:%M:%SZ)
Repo:     $(git -C "$REPO_ROOT" config --get remote.origin.url)
Commit:   $(git -C "$REPO_ROOT" rev-parse HEAD)
Unity:    $(basename "$(dirname "$UNITY_PATH")")
Backend:  Mono (Windows)
EOF

# ---------- 4. Create ZIP ----------
log "Creating zip: $ZIP_PATH"
cd "$DIST_DIR" && zip -r9 -q "$ZIP_PATH" "$BUNDLE_NAME"
ZIP_SIZE=$(du -h "$ZIP_PATH" | awk '{print $1}')
log "Zip created: $ZIP_PATH ($ZIP_SIZE)"

log "Done. Send $ZIP_PATH (unzip anywhere and run MajdataEdit-Neo.exe)."
