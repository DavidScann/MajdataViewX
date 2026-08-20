#!/usr/bin/env bash
# build-macos.sh — Build the MajdataX macOS bundle (Unity Mono + .NET editor) locally
# from a Linux host (cross-build Mono) or natively on macOS (Mono or IL2CPP).
# No GitHub release publishing (private testing only).
#
# Usage:
#   scripts/build-macos.sh                          # build both, output to dist/MajdataX-macOS-<VERSION>
#   scripts/build-macos.sh --version v6.0.0-macos.1
#   SKIP_UNITY=1 scripts/build-macos.sh             # reuse existing Unity build at build/macOS
#   SKIP_DOTNET=1 scripts/build-macos.sh            # reuse existing dotnet publish
#
# Parity: mirrors scripts/build-linux.sh but for macOS:
#   - Unity target StandaloneOSX (BuildScript.cs uses Mono when cross-building from Linux,
#     IL2CPP when HOST is macOS or IL2CPP_MAC=1)
#   - dotnet publish -r osx-arm64 (arm64 primary) + osx-x64 fallback
#   - Bundle layout is flat sibling (MajdataEdit-Neo + MajdataViewX.app + Skin/SFX)
#     so MajEnv.GetMajdataViewExecutablePath() and MajdataViewBassDllFile probing work
#     identically to Linux (no bundle-relative resource difference except .app-wrapper).
#
# Environment overrides:
#   UNITY_PATH    Unity editor binary (default: /home/davidscann/Unity/Hub/Editor/6000.3.19f1/Editor/Unity)
#   DOTNET_PATH   dotnet (default: dotnet)
#   EDITOR_REPO   MajdataEdit-Neo checkout (default: ../MajdataEdit-Neo-Linux)
#   IL2CPP_MAC    1 to force IL2CPP on macOS host (default 0 => Mono cross-build)
#   VERSION_BASE  version base (default: v6.0.0-macos)
#   DOTNET_RID    dotnet RID (default: osx-arm64, x64 fallback built only on macOS host collisions)
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
UNITY_PATH="${UNITY_PATH:-/home/davidscann/Unity/Hub/Editor/6000.3.19f1/Editor/Unity}"
DOTNET_PATH="${DOTNET_PATH:-dotnet}"
EDITOR_REPO="${EDITOR_REPO:-$(cd "$REPO_ROOT/.." && pwd)/MajdataEdit-Neo-Linux}"
VERSION_BASE="${VERSION_BASE:-v6.0.0-macos}"
DOTNET_RID="${DOTNET_RID:-osx-arm64}"
DIST_DIR="${DIST_DIR:-$REPO_ROOT/dist}"

EXPLICIT_VERSION=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --version) EXPLICIT_VERSION="$2"; shift 2 ;;
    --version=*) EXPLICIT_VERSION="${1#*=}"; shift ;;
    --help|-h) sed -n '2,40p' "$0" | sed 's/^# \?//'; exit 0 ;;
    *) echo "Unknown arg: $1" >&2; exit 2 ;;
  esac
done

if [[ -n "$EXPLICIT_VERSION" ]]; then
  VERSION="$EXPLICIT_VERSION"
else
  VERSION="${VERSION_BASE}-$(date +%Y%m%d-%H%M%S)"
fi

BUNDLE_NAME="${BUNDLE_NAME:-MajdataX-macOS-${VERSION}}"
BUNDLE_DIR="$DIST_DIR/$BUNDLE_NAME"
ZIP_PATH="$DIST_DIR/${BUNDLE_NAME}.zip"

log() { printf '\033[1;34m[mac]\033[0m %s\n' "$*"; }
die() { printf '\033[1;31m[err]\033[0m %s\n' "$*" >&2; exit 1; }

[[ -d "$REPO_ROOT" ]] || die "Repo not found"
[[ -d "$EDITOR_REPO" ]] || die "Editor repo not found: $EDITOR_REPO"
command -v zip >/dev/null || die "zip required"

log "Version: $VERSION"
log "Bundle:  $BUNDLE_DIR"
log "RID:     $DOTNET_RID"
log "Editor:  $EDITOR_REPO"
log "Unity:   $UNITY_PATH"

rm -rf "$BUNDLE_DIR" "$ZIP_PATH"
mkdir -p "$BUNDLE_DIR" "$DIST_DIR"

# 1. Unity macOS player (Mono when cross-building from Linux)
if [[ -z "${SKIP_UNITY:-}" ]]; then
  log "Building Unity macOS player (Mono cross-build)..."
  [[ -x "$UNITY_PATH" ]] || die "Unity not found: $UNITY_PATH"
  UNITY_BUILD_DIR="$REPO_ROOT/build/macOS"
  rm -rf "$UNITY_BUILD_DIR"
  BUILD_LOG="$DIST_DIR/unity-mac-$(date +%Y%m%d-%H%M%S).log"
  log "Unity log: $BUILD_LOG"
  BUILD_TARGET=StandaloneOSX \
  BUILD_OUTPUT_PATH="$UNITY_BUILD_DIR/MajdataViewX.app" \
  IL2CPP_MAC="${IL2CPP_MAC:-0}" \
    "$UNITY_PATH" -batchmode -nographics -quit -projectPath "$REPO_ROOT" -executeMethod BuildScript.Build -logFile "$BUILD_LOG" -buildTarget StandaloneOSX
  [[ -d "$UNITY_BUILD_DIR/MajdataViewX.app" ]] || die "Unity build did not produce $UNITY_BUILD_DIR/MajdataViewX.app - see $BUILD_LOG"
  log "Unity OK: $UNITY_BUILD_DIR/MajdataViewX.app"
else
  log "SKIP_UNITY set - reusing $REPO_ROOT/build/macOS/MajdataViewX.app"
  UNITY_BUILD_DIR="$REPO_ROOT/build/macOS"
  [[ -d "$UNITY_BUILD_DIR/MajdataViewX.app" ]] || die "SKIP_UNITY set but no $UNITY_BUILD_DIR/MajdataViewX.app"
fi

# 2. dotnet editor for macOS
if [[ -z "${SKIP_DOTNET:-}" ]]; then
  log "Publishing MajdataEdit-Neo ($DOTNET_RID)..."
  EDITOR_CSPROJ="$EDITOR_REPO/MajdataEdit-Neo.csproj"
  [[ -f "$EDITOR_CSPROJ" ]] || die "Editor csproj not found"
  EDITOR_PUBLISH_DIR="$DIST_DIR/editor-publish-mac"
  rm -rf "$EDITOR_PUBLISH_DIR"
  "$DOTNET_PATH" publish "$EDITOR_CSPROJ" -c Release -r "$DOTNET_RID" --self-contained false -o "$EDITOR_PUBLISH_DIR"
  [[ -f "$EDITOR_PUBLISH_DIR/MajdataEdit-Neo" ]] || [[ -f "$EDITOR_PUBLISH_DIR/MajdataEdit-Neo.dll" ]] || die "dotnet publish failed"
  log "Editor OK: $EDITOR_PUBLISH_DIR"
else
  log "SKIP_DOTNET set - reusing $DIST_DIR/editor-publish-mac"
  EDITOR_PUBLISH_DIR="$DIST_DIR/editor-publish-mac"
fi

# 3. Assemble bundle (flat sibling, parity with Linux)
log "Assembling $BUNDLE_DIR ..."
cp -a "$UNITY_BUILD_DIR/MajdataViewX.app" "$BUNDLE_DIR/"
rsync -a --exclude='*.pdb' "$EDITOR_PUBLISH_DIR/" "$BUNDLE_DIR/" 2>/dev/null || cp -a "$EDITOR_PUBLISH_DIR/"* "$BUNDLE_DIR/"

# Ensure editor-required dylibs are at bundle root and in plugin probe locations
for dylib in libbass.dylib libbassopus.dylib; do
  found=0
  for src in "$REPO_ROOT/Assets/Plugins/macOS/$dylib" "$BUNDLE_DIR/MajdataViewX.app/Contents/Plugins/x86_64/$dylib" "$BUNDLE_DIR/MajdataViewX_Data/Plugins/x86_64/$dylib"; do
    if [[ -f "$src" ]]; then
      cp -a "$src" "$BUNDLE_DIR/" 2>/dev/null || true
      mkdir -p "$BUNDLE_DIR/MajdataViewX_Data/Plugins/x86_64"
      cp -a "$src" "$BUNDLE_DIR/MajdataViewX_Data/Plugins/x86_64/" 2>/dev/null || true
      found=1
      break
    fi
  done
  if [[ $found -eq 0 && -f "$EDITOR_REPO/runtimes/osx/native/$dylib" ]]; then
    cp -a "$EDITOR_REPO/runtimes/osx/native/$dylib" "$BUNDLE_DIR/"
    mkdir -p "$BUNDLE_DIR/MajdataViewX_Data/Plugins/x86_64"
    cp -a "$EDITOR_REPO/runtimes/osx/native/$dylib" "$BUNDLE_DIR/MajdataViewX_Data/Plugins/x86_64/"
  fi
done

if [[ -d "$EDITOR_REPO/BinaryAssets/Skin" && -d "$EDITOR_REPO/BinaryAssets/SFX" ]]; then
  log "Copying BinaryAssets Skin/SFX..."
  cp -a "$EDITOR_REPO/BinaryAssets/Skin" "$EDITOR_REPO/BinaryAssets/SFX" "$BUNDLE_DIR/"
else
  log "WARNING: BinaryAssets/Skin or SFX missing - bundle will lack skin/SFX (git -C $EDITOR_REPO submodule update --init)"
fi

cat > "$BUNDLE_DIR/VERSION" <<EOF
$VERSION
Built: $(date -u +%Y-%m-%dT%H:%M:%SZ)
Commit: $(git -C "$REPO_ROOT" rev-parse HEAD)
Editor: $(git -C "$EDITOR_REPO" rev-parse HEAD 2>/dev/null || echo "?")
RID: $DOTNET_RID
Backend: Mono (cross-build, IL2CPP_MAC=${IL2CPP_MAC:-0})
EOF

rm -rf "$BUNDLE_DIR/MajdataViewX_BackUpThisFolder_ButDontShipItWithYourGame" "$BUNDLE_DIR/MajdataViewX_BurstDebugInformation_DoNotShip" 2>/dev/null || true

log "Creating zip $ZIP_PATH ..."
(cd "$DIST_DIR" && zip -r9 -q "$ZIP_PATH" "$BUNDLE_NAME")
ls -lh "$ZIP_PATH"
log "Done. Unzip and run: cd $BUNDLE_NAME && ./MajdataEdit-Neo (or open MajdataViewX.app directly)"
