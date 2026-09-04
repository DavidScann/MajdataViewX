# MajdataX for macOS

Two apps that work together, just like on Linux/Windows:

* **MajdataEdit-Neo.app** - the chart editor (type your simai, see the visualizer)
* **MajdataViewX.app** - the 3D preview/player (shows the notes falling)

They talk over `127.0.0.1:8083`. Opening the editor auto-opens the viewer.

---

## Quick Start

1. **Unzip** `MajdataX-macOS-Native-v6.1.1-fix2.zip`
2. **Fix the security block** (one time):
   ```bash
   cd MajdataX-macOS-Native-v6.1.1-fix2
   chmod +x sign_applications.sh
   ./sign_applications.sh
   ```
   This does `xattr -cr` (remove quarantine) + `codesign --sign -` (ad-hoc sign) for both `.app`s. Enter your password if asked. You only do this once after unzipping.
   
   *Or in Finder: Right-click each `.app` -> Open -> Open Anyway -> System Settings -> Privacy & Security -> Open Anyway.*
3. **Run**:
   ```bash
   open MajdataEdit-Neo.app
   # Viewer opens automatically. Or:
   open MajdataViewX.app
   ```
   Or double-click `MajdataEdit-Neo.app` in Finder. Drag both `.app`s to `/Applications` if you want them in Launchpad.

4. **Video export needs ffmpeg** (like on Linux):
   ```bash
   brew install ffmpeg
   ffmpeg -version | head -1
   # Needs libx264 / libx265 / libsvtav1 - brew's ffmpeg 8.1.2 already has them
   ffmpeg -encoders | grep -E "libx264|libx265|libsvtav1"
   ```

No `dotnet` needed - this `MajdataEdit-Neo.app` is self-contained (arm64). Intel Macs need the `osx-x64` build, tell us if you need it.

---

## Why do I have to run `sign_applications.sh`?

### ELI5

Think of macOS Gatekeeper as a **bouncer at a club**. Every app needs an ID to get in.

* Apps from the App Store have a **government ID** - the bouncer lets them right in.
* Apps from big companies pay Apple `$99/year` for a **VIP badge** (Apple Developer Program, `Developer ID` + `notarization`). The bouncer checks the badge online and lets them in.
* Our `MajdataX` is open-source, built on a Linux computer. We can't afford the VIP badge for every test build, so we give the apps a **hand-written name tag** (`ad-hoc` signature, `codesign --sign -`). The bouncer says "I don't trust a hand-written tag" and blocks the door. It also puts a **"quarantine sticker"** (`com.apple.quarantine` xattr) on anything you download from the internet.

`sign_applications.sh` just does two things the bouncer will accept for local friends:

1. `xattr -cr` - **peels off the quarantine sticker** ("I trust this, it came from my friend, not a stranger")
2. `codesign --force --deep --sign -` - **writes a fresh hand-written name tag** so macOS knows the app hasn't been tampered with after you peeled the sticker

If the apps were from a paid `Developer ID` + `notarized` build, you wouldn't need this - the bouncer would check Apple's online list and let them in. For nightly/test builds, the script is the free, one-time workaround. After you run it, macOS remembers you trust these two apps.

If Finder still says "damaged", also do: `System Settings -> Privacy & Security -> Open Anyway` or `sudo spctl --add /path/to/MajdataEdit-Neo.app`.

---

## What's inside?

```
MajdataX-macOS-Native-v6.1.1-fix2/
  MajdataEdit-Neo.app      # editor, Mach-O arm64 143M self-contained (Avalonia)
  MajdataViewX.app         # viewer, Mach-O universal x86_64+arm64 Mono (Unity 6000.3.19f1, Metal)
  Skin/  SFX/              # shared skins/sounds (also duplicated inside each .app for path probing)
  completions.json  VERSION
  sign_applications.sh     # the helper above
  README-macOS.md          # this file
```

* Editor: `MajdataEdit-Neo.app/Contents/MacOS/MajdataEdit-Neo` + `Avalonia` + `libSkiaSharp` + `libbass.dylib` (universal)
* Viewer: `MajdataViewX.app/Contents/MacOS/MajdataViewX` + `UnityPlayer.dylib` + `PlugIns/libbass.dylib` + `lib_burst_generated.bundle`

Both built from `macos-compatibility` branch (private hidden CI verified on `macos-14` 14.8.7 arm64: viewer `8083 LISTEN` + editor GUI loop).

---

## Troubleshooting

**App bounces and closes:**
```bash
./MajdataEdit-Neo.app/Contents/MacOS/MajdataEdit-Neo 2>&1 | tee /tmp/editor.log
cat ~/Library/Application\ Support/MajdataEdit-Neo/crash.log 2>&1 | tail -50
cat ~/Library/Logs/DiagnosticReports/MajdataEdit-Neo*.ips 2>&1 | tail -100
```

**Viewer not auto-opening:** Editor looks for sibling `../MajdataViewX.app` via `Base/MajEnv.cs:151 GetMajdataViewExecutablePath()` - keep both `.app`s in the same folder. Check `lsof -i :8083` - should show `MajdataViewX LISTEN` 3s after launching viewer.

**Video export says "no ffmpeg/codec":**
```bash
which ffmpeg
ffmpeg -encoders | grep -E "libx264|libx265|libsvtav1|videotoolbox"
# Should list libx264 etc. If not, `brew reinstall ffmpeg`
# Viewer log: ~/Library/Logs/Unity/Player.log | grep FfmpegEncoder
```

**"Bad CPU type":** You are on Intel (`uname -m` = `x86_64`) but this editor is `arm64` only. Ask for `osx-x64` build.

**Still blocked:** `xattr -l MajdataEdit-Neo.app` should show no `com.apple.quarantine` after `sign_applications.sh`. If it does, `sudo xattr -cr` again.

---

## For developers

* Branch `macos-compatibility` in `DavidScann/MajdataEdit-Neo-Linux-macos-private` + `MajdataViewX-macos-private` (hidden CI, `main` untouched)
* `dotnet publish -r osx-arm64 --self-contained true` + `BUILD_TARGET=StandaloneOSX BUILD_OUTPUT_PATH=... Unity -batchmode BuildScript.Build` (Mono fallback when built on Linux, `MacStandaloneSupport` required)
* To make a fully trusted build: set `CODE_SIGN_IDENTITY="Developer ID Application: Name (TEAMID)"` in `sign_applications.sh`, replace `codesign --sign -` with that, then `xcrun notarytool submit ... --wait && xcrun stapler staple`.
