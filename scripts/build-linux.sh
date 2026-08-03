#!/usr/bin/env bash
# build-linux.sh — Build the MajdataX Linux x86_64 bundle and (optionally)
# tag, push, and publish it as a GitHub release.
#
# Usage:
#   scripts/build-linux.sh                          # build only, no tag/push
#   scripts/build-linux.sh --release                # build + tag + push + release
#   scripts/build-linux.sh --release --version v6.0.0-linux.2
#   scripts/build-linux.sh --release --dry-run      # show what would happen
#
# Environment overrides:
#   UNITY_PATH       Path to the Unity editor binary (default: /home/davidscann/Unity/Hub/Editor/6000.3.19f1/Editor/Unity)
#   DOTNET_PATH      Path to dotnet (default: dotnet on $PATH)
#   EDITOR_REPO      Path to MajdataEdit-Neo checkout (default: ../MajdataEdit-Neo-Linux)
#   BUNDLE_NAME      Output bundle dir name (default: MajdataX-Linux-<VERSION>)
#   IL2CPP_CONFIG    release (default, fast) or master (full LTO, slow)
#   SKIP_UNITY       If set, skip Unity build and reuse $BUNDLE_NAME
#   SKIP_DOTNET      If set, skip dotnet build and reuse $BUNDLE_NAME
#   GH               Path to gh CLI (default: gh on $PATH)
#   VERSION_BASE     Version base for auto-increment (default: v6.0.0-linux)
#
# Required tools: bash, git, zip, dotnet, awk, sed. For --release: gh (authenticated).

set -euo pipefail

# ---------- Defaults ----------
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
UNITY_PATH="${UNITY_PATH:-/home/davidscann/Unity/Hub/Editor/6000.3.19f1/Editor/Unity}"
DOTNET_PATH="${DOTNET_PATH:-dotnet}"
EDITOR_REPO="${EDITOR_REPO:-$(cd "$REPO_ROOT/.." && pwd)/MajdataEdit-Neo-Linux}"
VERSION_BASE="${VERSION_BASE:-v6.0.0-linux}"
IL2CPP_CONFIG="${IL2CPP_CONFIG:-release}"
GH_BIN="${GH:-gh}"
DIST_DIR="${DIST_DIR:-$REPO_ROOT/dist}"

DO_RELEASE=0
DO_DRY_RUN=0
EXPLICIT_VERSION=""

# ---------- Parse args ----------
while [[ $# -gt 0 ]]; do
  case "$1" in
    --release) DO_RELEASE=1; shift ;;
    --dry-run) DO_DRY_RUN=1; shift ;;
    --version) EXPLICIT_VERSION="$2"; shift 2 ;;
    --version=*) EXPLICIT_VERSION="${1#*=}"; shift ;;
    --help|-h)
      sed -n '2,24p' "$0" | sed 's/^# \?//'
      exit 0 ;;
    *) echo "Unknown arg: $1" >&2; exit 2 ;;
  esac
done

# ---------- Helpers ----------
log()  { printf '\033[1;34m[build]\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m[warn]\033[0m %s\n' "$*" >&2; }
die()  { printf '\033[1;31m[err ]\033[0m %s\n' "$*" >&2; exit 1; }
run()  {
  if [[ $DO_DRY_RUN -eq 1 ]]; then
    printf '\033[1;36m[dry]\033[0m %s\n' "$*"
  else
    eval "$@"
  fi
}

# Run only if not in dry-run mode (for [[ -f ... ]] checks)
check() {
  if [[ $DO_DRY_RUN -eq 1 ]]; then
    printf '\033[1;36m[chk]\033[0m (skipped in dry-run) %s\n' "$*"
  else
    eval "$@"
  fi
}

# ---------- Preflight ----------
[[ -d "$REPO_ROOT" ]] || die "Repo not found: $REPO_ROOT"
[[ -d "$EDITOR_REPO" ]] || die "Editor repo not found: $EDITOR_REPO"
command -v git >/dev/null || die "git is required"
command -v zip >/dev/null || die "zip is required"
command -v "$DOTNET_PATH" >/dev/null 2>&1 || [[ $DO_DRY_RUN -eq 1 ]] || die "dotnet not found (or DOTNET_PATH is wrong)"
[[ $DO_RELEASE -eq 1 ]] && command -v "$GH_BIN" >/dev/null || true

cd "$REPO_ROOT"

# Determine version
if [[ -n "$EXPLICIT_VERSION" ]]; then
  VERSION="$EXPLICIT_VERSION"
else
  if [[ $DO_RELEASE -eq 1 ]]; then
    # Auto-increment: find latest v6.0.0-linux.* tag, increment
    LATEST="$(git tag --list "${VERSION_BASE}.*" --sort=-version:refname | head -n 1)"
    if [[ -z "$LATEST" ]]; then
      VERSION="${VERSION_BASE}.1"
    else
      NUM="${LATEST#${VERSION_BASE}.}"
      if [[ "$NUM" =~ ^[0-9]+$ ]]; then
        VERSION="${VERSION_BASE}.$((NUM + 1))"
      else
        VERSION="${VERSION_BASE}.1"
      fi
    fi
  else
    # Just a build, no release; use a timestamp suffix to avoid clashes
    VERSION="${VERSION_BASE}-$(date +%Y%m%d-%H%M%S)"
  fi
fi

BUNDLE_NAME="${BUNDLE_NAME:-MajdataX-Linux-${VERSION}}"
BUNDLE_DIR="$DIST_DIR/$BUNDLE_NAME"
ZIP_PATH="$DIST_DIR/${BUNDLE_NAME}.zip"

log "Version:    $VERSION"
log "Bundle:     $BUNDLE_DIR"
log "Zip:        $ZIP_PATH"
log "Repo:       $REPO_ROOT"
log "Editor:     $EDITOR_REPO"
log "Unity:      $UNITY_PATH"
log "IL2CPP:     $IL2CPP_CONFIG"
log "Mode:       $([[ $DO_RELEASE -eq 1 ]] && echo 'release' || echo 'build only')$([[ $DO_DRY_RUN -eq 1 ]] && echo ' (dry-run)')"

# ---------- 1. Clean dist + bundle ----------
run "rm -rf '$BUNDLE_DIR' '$ZIP_PATH'"
run "mkdir -p '$BUNDLE_DIR' '$DIST_DIR'"

# ---------- 2. Build Unity Linux player ----------
if [[ -z "${SKIP_UNITY:-}" ]]; then
  log "Building Unity player (this takes ~6 min with IL2CPP=$IL2CPP_CONFIG)..."
  [[ -x "$UNITY_PATH" ]] || die "Unity not found or not executable: $UNITY_PATH"

  UNITY_BUILD_DIR="$REPO_ROOT/build/Linux"
  run "rm -rf '$UNITY_BUILD_DIR' '$REPO_ROOT/build'"

  BUILD_LOG="$DIST_DIR/unity-build-$(date +%Y%m%d-%H%M%S).log"
  log "Unity log: $BUILD_LOG"
  run "'$UNITY_PATH' \
    -batchmode -nographics -quit \
    -projectPath '$REPO_ROOT' \
    -executeMethod BuildScript.Build \
    -logFile '$BUILD_LOG' \
    -buildTarget StandaloneLinux64"

  # The player binary is "MajdataViewX" (matches what the editor launches).
  # Unity 6.3 (6000.3) omits the ".x86_64" suffix; older versions used it.
  PLAYER_BIN=""
  for cand in "$UNITY_BUILD_DIR/MajdataViewX" "$UNITY_BUILD_DIR/MajdataViewX.x86_64"; do
    if [[ -x "$cand" ]]; then PLAYER_BIN="$cand"; break; fi
  done
  [[ -n "$PLAYER_BIN" ]] \
    || die "Unity build did not produce a MajdataViewX binary in $UNITY_BUILD_DIR — see $BUILD_LOG"

  log "Unity build OK: $UNITY_BUILD_DIR"
else
  log "SKIP_UNITY set; reusing existing Unity build at $REPO_ROOT/build/Linux/"
  UNITY_BUILD_DIR="$REPO_ROOT/build/Linux"
  PLAYER_BIN=""
  for cand in "$UNITY_BUILD_DIR/MajdataViewX" "$UNITY_BUILD_DIR/MajdataViewX.x86_64"; do
    if [[ -x "$cand" ]]; then PLAYER_BIN="$cand"; break; fi
  done
  [[ -n "$PLAYER_BIN" ]] \
    || die "SKIP_UNITY set but no existing build at $UNITY_BUILD_DIR"
fi

# ---------- 3. Build .NET editor ----------
if [[ -z "${SKIP_DOTNET:-}" ]]; then
  log "Building MajdataEdit-Neo (.NET 10)..."
  EDITOR_CSPROJ="$EDITOR_REPO/MajdataEdit-Neo.csproj"
  [[ -f "$EDITOR_CSPROJ" ]] || die "Editor csproj not found: $EDITOR_CSPROJ"

  EDITOR_PUBLISH_DIR="$DIST_DIR/editor-publish"
  run "rm -rf '$EDITOR_PUBLISH_DIR'"
  run "'$DOTNET_PATH' publish '$EDITOR_CSPROJ' \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    -o '$EDITOR_PUBLISH_DIR'"

  check "[[ -x '$EDITOR_PUBLISH_DIR/MajdataEdit-Neo' ]]" \
    || die "dotnet publish did not produce $EDITOR_PUBLISH_DIR/MajdataEdit-Neo"
  log "Editor build OK: $EDITOR_PUBLISH_DIR"
else
  log "SKIP_DOTNET set; reusing existing publish at $DIST_DIR/editor-publish/"
  EDITOR_PUBLISH_DIR="$DIST_DIR/editor-publish"
fi

# ---------- 4. Assemble the bundle ----------
log "Assembling bundle at $BUNDLE_DIR ..."

# 4a. Copy Unity Linux player output
run "cp -a '$UNITY_BUILD_DIR/.' '$BUNDLE_DIR/'"

# 4b. Copy dotnet-published editor
run "rsync -a --exclude='*.pdb' \
      '$EDITOR_PUBLISH_DIR/' '$BUNDLE_DIR/'"

# 4c. Ensure libbassopus.so is where BASS needs it in BOTH places:
#   - bundle root: the editor (MajdataEdit-Neo) loads libbass.so + libbassopus.so
#     from its own directory (TrackReader.cs: Bass.PluginLoad("libbassopus"))
#   - MajdataViewX_Data/Plugins/AnyCPU/: the Unity player's BASS
if check "[[ -f '$BUNDLE_DIR/MajdataViewX_Data/Plugins/AnyCPU/libbassopus.so' ]]"; then
  log "libbassopus.so in Plugins/AnyCPU/ ✓"
  run "cp -a '$BUNDLE_DIR/MajdataViewX_Data/Plugins/AnyCPU/libbassopus.so' '$BUNDLE_DIR/libbassopus.so'"
  log "libbassopus.so copied to bundle root for the editor ✓"
elif check "[[ -f '$BUNDLE_DIR/libbassopus.so' ]]"; then
  log "libbassopus.so already at bundle root ✓"
else
  warn "libbassopus.so not found — BASS will not decode Opus"
  warn "Place it at: $BUNDLE_DIR/MajdataViewX_Data/Plugins/AnyCPU/libbassopus.so"
fi

# 4d. Copy bundled assets (Skin + SFX) from the editor's BinaryAssets submodule
if check "[[ -d '$EDITOR_REPO/BinaryAssets/Skin' && -d '$EDITOR_REPO/BinaryAssets/SFX' ]]"; then
  log "Copying BinaryAssets (Skin/SFX) from $EDITOR_REPO"
  run "cp -a '$EDITOR_REPO/BinaryAssets/Skin' '$EDITOR_REPO/BinaryAssets/SFX' '$BUNDLE_DIR/'"
else
  warn "BinaryAssets/Skin or BinaryAssets/SFX missing in $EDITOR_REPO — bundle will lack skin/SFX"
  warn "Run: git -C '$EDITOR_REPO' submodule update --init --recursive"
fi

# 4d2. Remove Unity build-only artifacts
run "rm -rf '$BUNDLE_DIR/MajdataViewX_BackUpThisFolder_ButDontShipItWithYourGame'"
run "rm -rf '$BUNDLE_DIR/MajdataViewX_BurstDebugInformation_DoNotShip'"

# 4e. The player binary is already named "MajdataViewX" (what the editor
# launches). Nothing to alias.
if ! check "[[ -x '$BUNDLE_DIR/MajdataViewX' ]]"; then
  die "No player launcher (MajdataViewX) in bundle"
fi

# 4f. Write a manifest
if [[ $DO_DRY_RUN -eq 1 ]]; then
  printf '\033[1;36m[dry]\033[0m write manifest to %s/VERSION\n' "$BUNDLE_DIR"
else
  cat > "$BUNDLE_DIR/VERSION" <<EOF
$VERSION
Built:    $(date -u +%Y-%m-%dT%H:%M:%SZ)
Repo:     $(git -C "$REPO_ROOT" config --get remote.origin.url)
Commit:   $(git -C "$REPO_ROOT" rev-parse HEAD)
Unity:    $(basename "$(dirname "$UNITY_PATH")")
IL2CPP:   $IL2CPP_CONFIG
EOF
fi

# ---------- 5. Create zip ----------
log "Creating zip: $ZIP_PATH"
run "cd '$DIST_DIR' && zip -r9 -q '$ZIP_PATH' '$BUNDLE_NAME' \
  -x '${BUNDLE_NAME}/.autosave/*'"

if [[ $DO_DRY_RUN -eq 1 ]]; then
  log "Zip would be at: $ZIP_PATH (size unknown in dry-run)"
else
  ZIP_SIZE=$(du -h "$ZIP_PATH" | awk '{print $1}')
  log "Zip created: $ZIP_PATH ($ZIP_SIZE)"
fi

# ---------- 6. Tag + push + release ----------
if [[ $DO_RELEASE -eq 1 ]]; then
  log "Creating tag $VERSION..."
  run "git tag -a '$VERSION' -m 'Linux port $VERSION

Built by scripts/build-linux.sh
Commit: $(git rev-parse HEAD)' HEAD"
  run "git push origin '$VERSION'"

  log "Creating GitHub release..."
  RELEASE_NOTES="## MajdataEdit-Neo + MajdataView — Linux x86_64 build

Tag: \`$VERSION\`
Commit: \`$(git rev-parse --short HEAD)\`
IL2CPP config: \`$IL2CPP_CONFIG\`

### Run
\`\`\`bash
unzip ${BUNDLE_NAME}.zip
cd $BUNDLE_NAME
./MajdataEdit-Neo
\`\`\`

### Requirements
- \`ffmpeg\` in \`\$PATH\` (for video preview)
- \`libopus\` (system, for Opus decoding via bassopus — bundled)
- X11 or Wayland session (or Xvfb for headless)

Built automatically by \`scripts/build-linux.sh\`."
  run "'$GH_BIN' release create '$VERSION' \
    --repo '$(git -C "$REPO_ROOT" config --get remote.origin.url | sed 's|.*github.com[:/]||;s|\.git$||')' \
    --title 'MajdataX Linux $VERSION' \
    --notes \"$RELEASE_NOTES\" \
    '$ZIP_PATH#${BUNDLE_NAME}.zip'"

  log "Release published: $VERSION"
else
  log "Build complete. To publish as a release, re-run with --release."
fi

log "Done."
