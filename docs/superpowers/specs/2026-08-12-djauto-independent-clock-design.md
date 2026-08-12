# DJAuto on an Independent Clock

**Date:** 2026-08-12
**Status:** Approved design
**Scope:** MajdataViewX (Unity view client)

## Problem

DJAuto (and the simpler Autoplay modes) is tied to the framerate the View renders at:

1. Autoplay input checks (`AutoplayUpdate` in the five note update jobs) run once per
   rendered frame, so every DJAuto press/slide/touch decision is quantized to render
   frames. At low FPS the Critical Perfect window (±16.7ms) cannot be hit reliably,
   and below ~5 FPS even the ±150ms Good window can be straddled by a single frame.
2. DJAuto inputs are written to a "next frame" buffer and applied at the start of the
   *next* rendered frame, with a "trigger one frame early" hack
   (`DJAUTO_AUTOPLAY_START_SEC = -Time.unscaledDeltaTime`, set per frame in
   `NoteManager.Update`). The compensation scales with the actual frame time, so under
   lag the effective press time jitters by up to ±1 frame and the hand visuals lag.
3. Judgment of DJAuto presses happens in the render jobs using the render-frame
   `NoteTime`, so even a perfectly-timed press is graded from a frame-quantized diff,
   and a frame that straddles the window can turn a correct press into a Miss.

Root cause: the song clock is already wall-clock accurate (`TimeProvider` drives
`NoteTime` from a `Stopwatch`), but DJAuto's *processing* is driven by render frames.

## Goals

- DJAuto (DJAutoButton / DJAutoSensor) and Autoplay (Enable / Random) run on their
  own fixed-rate clock derived from `NoteTime`, independent of render FPS.
- DJAuto presses, slide hand trajectories, touch coverage, and their judgments are
  exact at any render FPS (grades, visuals, and effects follow the sim clock).
- Mixed play stays intact: the real player's input is still accepted and judged as
  today (render-frame granularity is inherent for physical input).
- Works identically in real-time playback and record/export mode.
- CPU cost stays negligible (linear in notes, tiny constant per note per tick).

## Non-goals

- Changing user-input judgment granularity (inherently render-frame).
- Changing record-mode timeline semantics (one export frame per rendered frame).
- Rendering the hands more than once per render frame (visuals stay render-rate).

## Architecture

New component: `DJAutoSim` — a fixed-timestep simulation on the main thread, owned by
`NoteManager`, stepped inside `NoteManager.Update`.

### Tick schedule

- Fixed tick `SIM_DT = 1/240` (~4.17ms).
- Accumulator driven by `TimeData.NoteTime`:

  ```
  acc += NoteTime - lastSimNoteTime
  lastSimNoteTime = NoteTime
  while (acc >= SIM_DT) { Tick(SIM_DT); acc -= SIM_DT; }
  ```

- The accumulator is clamped to avoid a tick storm after a long hitch or pause
  (`acc = min(acc, MAX_CATCH_UP * SIM_DT)`). `MAX_CATCH_UP` must cover one full
  record-mode frame (`ceil(recordFrameStep / SIM_DT)`, i.e. 4 ticks at 60fps export,
  10 at 24fps) plus headroom; `MAX_CATCH_UP = max(8, ceil(recordFrameStep / SIM_DT) + 1)`
  satisfies both — the sim never falls behind the export timeline, and a paused clock
  can never emit a burst of stale presses (pauses also re-anchor `lastSimNoteTime`).
- Placement inside `NoteManager.Update`, **after** `InputManager.BeginHandler` and
  **before** the render jobs are scheduled. This ordering makes the sim's input state
  visible to the jobs in the same frame without any next-frame buffering.
- Reset rules:
  - `SetStartTime` / `Pause` / `Resume` / note reload: recompute
    `lastSimNoteTime = NoteTime` and zero the accumulator (the clamp covers any
    leftover, e.g. a pause).
  - Only runs in autoplay-relevant modes (Enable / Random / DJAutoButton /
    DJAutoSensor / Disable-for-mines). In `AutoPlayMode.Disable` the sim only does the
    mine auto-slide advance (same as today's `AutoplayUpdate` Disable branch).

### What moves into `Tick()`

The `AutoplayUpdate()` bodies from the five update jobs
(`TapUpdateJob`, `HoldUpdateJob`, `SlideUpdateJob`, `TouchUpdateJob`,
`TouchHoldUpdateJob`) plus the mine auto-slide advance. The sim iterates the same
note `NativeArray`s (`NoteManager` fields) on the main thread; per note per tick the
cost is a single float compare early-exit
(`if (timing < triggerTime) return;`).

Per note the sim records an exact press time:

- Tap / Hold / Touch / TouchHold: new `djAutoPressTime` field on the note data
  (float, set when the sim issues the press).
- Slide: trajectories and section progress are pure functions of time already
  (`inputProcess`, wifi lerp, `judgeQueue` progress); the sim computes them from
  `simTime` instead of the render frame.

### Input flow changes

- The sim calls the existing `InputData.DJAuto*` APIs
  (`DJAutoSetButtonOn`, `DJAutoSetSensorOn`, `DJAutoAddGroupCoverage`,
  `DJAutoHandleWorldPosition`, `DJAutoHandleWifiWorldPosition`) but with
  **current-frame level semantics** instead of the next-frame buffers:
  - New `SetThisFrameButtonOn` / `SetThisFrameSensorOn` level writes into
    `_buttonStates` / `_sensorStates` (mirroring the existing user-input path).
  - Hand/coverage visuals: the sim writes directly into the render buffers the
    `BeginHandler` already locked (`hitRender` / `HitWriteCountPtr`, `ActiveCoverages`
    / `ActiveCoveragesCountPtr`), so hands render per render frame from the sim's
    latest state. The `_djAutoHandsNextFrame`-style double buffering collapses into a
    single sim-owned hand set.
- The `_buttonActiveDownNextFrame` / `_sensorActiveDownNextFrame` buffers and the
  `SetNextFrame*` DJAuto path are removed; the per-frame hack
  `DJAUTO_AUTOPLAY_START_SEC = -Time.unscaledDeltaTime` (and the record-mode
  `-1/fps` variant in `TimeProvider`) reverts to a constant 0 — the press is issued
  exactly at note time.
- Edge semantics stay correct because `BeginHandler` still advances
  `LastActiveDown = ActiveDown` per render frame, so the jobs' `IsPadDown` rising
  edge fires exactly once per render frame for a sim-held press.

### Judgment changes

The render jobs keep `CheckUpdate` for **user** input unchanged (render-frame, as
today). For DJAuto presses each job checks the note's `djAutoPressTime` first:

- If set and within the window → judge with `diff = djAutoPressTime - note.Time`
  (exact grade), then end the note (sound, effect, report as today).
- If no valid DJAuto press → existing frame-based logic (late-Miss guard, user-input
  branch) unchanged.

This removes the frame-straddle: a note pressed exactly on time is graded exactly on
time no matter when the next render frame runs. The `AutoplayUpdate` calls are removed
from the jobs.

### Hand quota / coexistence

Unchanged: `TryRequestDJAutoHand` (2-hand limit, reuse, expand-to-max-radius,
coverage visuals) keeps working; the sim calls it on the main thread at tick rate.
User input shares the same `_buttonStates` / `_sensorStates` levels; `NoteManager`
updates `touchHoldGroupPressedCounts` etc. per render frame from current state, so
grouped touch logic is unaffected.

## Record / export mode

`NoteTime` advances one export frame per rendered frame; the accumulator ticks the sim
`recordFrameStep / SIM_DT` times per frame. Export accuracy is unchanged (export
frames are the timeline's truth), but DJAuto input decisions are computed at sim
precision within each export frame instead of at frame boundaries.

## Error handling & edge cases

- **Pause/seek/speed change:** `TimeProvider` freezes or rewinds `NoteTime`; the
  clamp + `lastSimNoteTime` reset on `SetStartTime`/`Pause`/`Resume` prevent bursts of
  catch-up ticks.
- **Hitch (long frame):** clamp caps catch-up; a hitch of N ms yields at most 8 ticks
  after the frame, never a tick storm.
- **Note unload/reload:** sim state is re-bound to the fresh `NativeArray`s and
  `lastSimNoteTime` re-anchored.
- **DJAuto in Disable mode:** only the mine auto-slide branch runs (as today).
- **Hand visuals with `ShowHand` off:** hit rendering stays gated by
  `_showHandThisFrame` as today; coverage arrays always update (judgment-relevant).

## Testing

- No GUI launch (per `AGENTS.md`): no in-editor gameplay verification by the agent.
- Build the standalone Linux player (`scripts/build-linux.sh` in the fork release
  flow) to verify Burst compilation of the changed jobs/structs.
- A small non-GUI verification of the accumulator arithmetic (time sequence →
  expected tick count) is acceptable if it fits existing test tooling; otherwise the
  accumulator logic is simple enough to verify by review.
- Manual verification is on the user: run the built player in DJAuto modes at normal
  and artificially low FPS (vsync off / heavy scenes) and confirm Critical grades,
  exact press timing, and hand visuals.

## Files touched (expected)

- `Assets/Scripts/Managers/DJAutoSim.cs` (new): the fixed-rate simulation.
- `Assets/Scripts/Managers/NoteManager.cs`: sim lifecycle, Update-order wiring,
  per-frame DJAuto start-sec hack removal.
- `Assets/Scripts/Managers/InputManager.cs`: `SetThisFrame*` writes, removal of the
  DJAuto next-frame buffers, `DJAUTO_AUTOPLAY_START_SEC` constant.
- `Assets/Scripts/Managers/TimeProvider.cs`: record-mode `-1/fps` hack removal.
- Note data structs (`TapData`, `HoldData`, `TouchData`, `TouchHoldData`):
  `djAutoPressTime` field.
- The five `*UpdateJob.cs`: autoplay branches removed, DJAuto judgment reads
  `djAutoPressTime`.
