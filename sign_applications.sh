#!/usr/bin/env bash
# sign_applications.sh - Ad-hoc sign and de-quarantine MajdataX macOS apps
# Usage: ./sign_applications.sh  (run from the unzipped MajdataX-macOS-* folder)
#  or: ./sign_applications.sh /path/to/MajdataX-macOS-Native
# No Apple Developer Program required - uses ad-hoc signature (codesign -s -).
# For fully trusted distribution, replace `-` with "Developer ID Application: Name (TEAMID)"
# and run xcrun notarytool submit after.

set -e
BUNDLE_DIR="${1:-$(cd "$(dirname "$0")" && pwd)}"
# If script is inside the bundle's top-level, BUNDLE_DIR is that top-level
# Handle being called from inside MajdataEdit-Neo.app or from bundle root
if [[ "$BUNDLE_DIR" == *".app"* ]]; then
  BUNDLE_DIR="$(cd "$BUNDLE_DIR/../.." && pwd)"
fi

echo "==> MajdataX sign_applications.sh"
echo "    Bundle: $BUNDLE_DIR"

for app in "$BUNDLE_DIR/MajdataEdit-Neo.app" "$BUNDLE_DIR/MajdataViewX.app"; do
  if [[ ! -d "$app" ]]; then
    echo "SKIP: $app not found"
    continue
  fi
  echo "==> Processing $app"
  echo "    xattr -cr ..."
  xattr -cr "$app" 2>&1 || echo "    (xattr -cr failed or not needed)"
  if command -v codesign >/dev/null 2>&1; then
    echo "    codesign --force --deep --sign - ..."
    codesign --force --deep --sign - "$app" 2>&1 | sed 's/^/    /' || echo "    (codesign warning, continuing)"
    echo "    verify:"
    codesign -dv "$app" 2>&1 | head -5 | sed 's/^/    /' || true
    # spctl will still fail for ad-hoc, but show status
    spctl --assess --verbose "$app" 2>&1 | head -5 | sed 's/^/    /' || true
  else
    echo "    codesign not found (Linux), only xattr done - will be signed on Mac via this script"
  fi
done

echo "==> Done. Now safe to:"
echo "    open \"$BUNDLE_DIR/MajdataEdit-Neo.app\""
echo "    open \"$BUNDLE_DIR/MajdataViewX.app\""
echo "If Finder still says damaged, also run:"
echo "    sudo spctl --add \"$BUNDLE_DIR/MajdataEdit-Neo.app\"  # or System Settings -> Privacy & Security -> Open Anyway"
