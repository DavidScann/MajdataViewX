# AGENTS.md — MajdataViewX / MajdataEdit-Neo Linux fork

Rules for working on this repo. Read before doing anything.

## Never launch the GUI apps during builds or tests

`MajdataEdit-Neo` auto-launches `MajdataViewX` on startup, and neither
process terminates reliably:

- The editor's .NET runtime swallows SIGTERM (graceful shutdown runs the
  window-closing logic, which cancels and can wait forever on dialogs),
  so a plain `timeout N ./MajdataEdit-Neo` does NOT kill it.
- The View it spawns stays alive until the editor's close dialog is
  answered, or the processes are killed manually.

Consequences: a stray editor+View pair can sit on the user's desktop for
hours and make builds feel stuck.

Rules:

1. Do NOT run `MajdataEdit-Neo` or `MajdataViewX` to "test" them during
   build/debug sessions. The user tests the builds themselves.
2. If you must run one (e.g. verifying a binary launches), use
   `timeout -k 3 N ./MajdataEdit-Neo` (SIGKILL after N seconds) and
   afterwards verify nothing lingers:
   `pgrep -a -f 'Majdata(Edit|View)' || echo clean`
3. Kill leftovers with `pkill -9 -f MajdataViewX` / `pkill -9 -f MajdataEdit-Neo`
   before finishing any session that touched them.
4. The build script (`scripts/build-linux.sh`) never launches the apps;
   if an app window appears during a build, it came from somewhere else
   (usually a stray instance from a previous run).
5. The build script kills stray `MajdataEdit-Neo`/`MajdataViewX` instances
   at startup (exact-name `pgrep -x`/`pkill -9 -x`, so the script itself is
   never matched), because a running pair can block the build.

## Release conventions

- Version follows upstream: do NOT bump the version number unless
  upstream (re-poem) bumps theirs.
- Patched fork releases use the `v<upstream-version>-linux.N` pattern
  (e.g. `v6.1.1-linux.5`), matching the existing release history.
- Release with: `scripts/build-linux.sh --release --version vX.Y.Z-linux.N`
- After releasing, update `~/Games/MajdataX` via rsync, preserving the
  user's custom files (`salt_dx.png`, `xxlb.png`).

## Hardware notes

- Encoder/decoder detection must VERIFY functionality, not just check
  `ffmpeg -encoders`/`-hwaccels` (compiled-in != usable): h264_nvenc is
  listed on machines without NVIDIA GPUs. See FfmpegEncoder
  (HwEncoderPresent / VerifyEncoder) and BgVideoPipe (VerifyHwDecode).
