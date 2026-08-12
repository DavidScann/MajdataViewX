# DJAuto Independent Clock Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Run DJAuto/Autoplay input and judgment on a fixed 240Hz simulation clock derived from the wall-clock-accurate `NoteTime`, independent of the View's render FPS.

**Architecture:** A new `DJAutoSim` fixed-timestep loop in `NoteManager.Update` (after `InputManager.BeginHandler`, before the render jobs) steps the autoplay logic at 240Hz and writes DJAuto input directly to current-frame state. Render jobs keep visuals and user-input judging, but judge DJAuto presses using the exact recorded press time. The old "next-frame buffer + trigger-one-frame-early" mechanism is deleted.

**Tech Stack:** Unity 6000.3 (IL2CPP/Burst), C# with `Unity.Collections` NativeArrays/NativeLists, `Unity.Jobs` (Burst-compiled jobs touched only minimally).

**Repo:** `~/gitclones/MajdataViewX` (branch `master`). **No GUI-launch testing allowed** (`AGENTS.md`). **No test framework exists** (no asmdefs) — verification is compile + build via `scripts/build-linux.sh` plus code review.

**Spec:** `docs/superpowers/specs/2026-08-12-djauto-independent-clock-design.md`

---

## Reference: Current behavior (before this plan)

- `TimeProvider.Update()` sets `TimeData.NoteTime` from a Stopwatch (accurate song clock).
- `NoteManager.Update()`: `_prevChain.Complete()` → set `DJAUTO_AUTOPLAY_START_SEC_SS.Data = -Time.unscaledDeltaTime` → `_inputManager.BeginHandler()` → (early return if no notes) → schedule render jobs → `LateUpdate()` completes jobs, renders, `EndHandler()`.
- `InputDataB.BeginHandler(bool)`: consumes `_buttonActiveDownNextFrame`/`_sensorActiveDownNextFrame` into `_buttonStates`/`_sensorStates` (advancing `LastActiveDown`), copies `_activeCoveragesNextFrame`→`ActiveCoverages`, `_worldPosHitsNextFrame`→`hitRender`.
- DJAuto input functions (`DJAutoSetButtonOn` etc.) write to the next-frame buffers; the "start one frame early" `DJAUTO_AUTOPLAY_START_SEC` compensates.
- The five `*UpdateJob`s run `TransformUpdate` → `AutoplayUpdate` → `CheckUpdate` per note per render frame.
- `CheckUpdate` (simulated modes only, via `NoteHelper.IsSimulated`) judges user input with `diffSec = TimeData.NoteTime - note.time` at render-frame granularity.

---

### Task 1: Note structs — record exact DJAuto press times

**Files:**
- Modify: `Assets/Scripts/Notes/NoteDatas/TapData.cs`
- Modify: `Assets/Scripts/Notes/NoteDatas/HoldData.cs`
- Modify: `Assets/Scripts/Notes/NoteDatas/TouchData.cs`
- Modify: `Assets/Scripts/Notes/NoteDatas/TouchHoldData.cs`
- (SlideData intentionally gets NO new field — slide input is continuous; its judgment stays render-side.)

- [ ] **Step 1: Add the press-time fields**

In each of the four structs, add next to the existing `// state` block (e.g. after `IsJudged`/`Diff`/`JudgeGrade` in `TapData.cs`):

```csharp
        // DJAuto simulation records the exact tick time the press was issued;
        // the render jobs judge with this instead of the render-frame time.
        public bool DjAutoPressed { get; set; }
        public float DjAutoPressTime { get; set; }
```

For `HoldData.cs` the state block is near the top (`isHeadJudged`, `headDiff`, …); for `TouchData.cs` (`isJudged`, `diff`, …) and `TouchHoldData.cs` (`isHeadJudged`, `headDiff`, …) place it with those state fields. Fields are plain auto-properties like their neighbors; Burst structs allow these.

- [ ] **Step 2: Verify no compile breakage**

Run: `grep -rn "DjAutoPressed\|DjAutoPressTime" Assets/Scripts/Notes/NoteDatas/`
Expected: 4 matches (one per file). No other file references the fields yet — that's fine; nothing else touches these structs' layout (they are `struct` with auto-props, layout not explicit).

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Notes/NoteDatas/TapData.cs Assets/Scripts/Notes/NoteDatas/HoldData.cs Assets/Scripts/Notes/NoteDatas/TouchData.cs Assets/Scripts/Notes/NoteDatas/TouchHoldData.cs
git commit -m "feat: record exact DJAuto press time on note data"
```

---

### Task 2: InputManager — current-frame DJAuto writes, drop next-frame buffers

**Files:**
- Modify: `Assets/Scripts/Managers/InputManager.cs`

Goal: DJAuto inputs become current-frame level writes (like user input), and the sim owns the per-tick hand/coverage reset. Behavior-neutral on its own (DJAuto functions still called from jobs until Task 4, but the jobs call them before the render jobs read state — with the same timing as before: pressed in frame N, judged in frame N).

- [ ] **Step 1: Remove next-frame button/sensor buffers**

In `InputDataB` (starts line ~170 in `InputManager.cs`):

Delete these fields (lines ~179-184):
```csharp
        NativeArray<int> _buttonActiveDownNextFrame;
        NativeArray<int> _sensorActiveDownNextFrame;
```

Delete these methods (lines ~679-686):
```csharp
        private void SetNextFrameButtonOn(SensorType type)
        {
            Interlocked.Increment(ref _buttonActiveDownNextFrame.ElementRef((int)type));
        }
        private void SetNextFrameSensorOn(SensorType type)
        {
            Interlocked.Increment(ref _sensorActiveDownNextFrame.ElementRef((int)type));
        }
```

- [ ] **Step 2: Repoint DJAuto button/sensor writes to current frame**

In `DJAutoSetButtonOn` (line ~260) and `DJAutoSetSensorOn` (line ~274): change `SetNextFrameButtonOn(type)` → `SetThisFrameButtonOn(type)` and `SetNextFrameSensorOn(type)` → `SetThisFrameSensorOn(type)`.

In `SetSensorsFromMask` (line ~523): change `SetNextFrameSensorOn` → `SetThisFrameSensorOn`.

- [ ] **Step 3: Simplify `BeginHandler`**

Replace the body of `BeginHandler(bool showHandThisFrame)` (lines ~566-607) with:

```csharp
        public void BeginHandler(bool showHandThisFrame)
        {
            _showHandThisFrame = showHandThisFrame;

            // DJAuto 输入不再走 next-frame 缓冲：DJAutoSim 在 BeginHandler 之后以固定
            // 步长直接写入当前状态。这里只做每帧的边沿推进，供 IsPadDown 使用。
            for (int i = 0; i < BUTTON_COUNT; i++)
            {
                ref var button = ref _buttonStates.ElementRef(i);
                button.LastActiveDown = button.ActiveDown;
                button.ActiveDown = 0;
            }
            for (int i = 0; i < SENSOR_COUNT; i++)
            {
                ref var sensor = ref _sensorStates.ElementRef(i);
                sensor.LastActiveDown = sensor.ActiveDown;
                sensor.ActiveDown = 0;
            }
        }
```

(Remove the coverage-copy and hit-copy blocks and the `_djAutoInputCount = 0` line — the sim resets those per tick now.)

- [ ] **Step 4: Add per-tick reset API**

Add to `InputDataB`, next to `BeginHandler`:

```csharp
        /// <summary>
        /// DJAutoSim 每 tick 调用：重置手部/覆盖数据，让本 tick 从干净状态重新申请。
        /// </summary>
        public void BeginSimTick()
        {
            _djAutoInputCount = 0;
            *ActiveCoveragesCountPtr = 0;
            _worldPosHitsNextFrameCount = 0;
        }
```

- [ ] **Step 5: Repoint hand/coverage visual writes to current-frame buffers**

In `TryRequestDJAutoHand` (line ~362-390): change the `Coverage` branch to write `ActiveCoverages` instead of `_activeCoveragesNextFrame`:

```csharp
                if (visualKind == DJAutoHandVisualKind.Coverage)
                {
                    visualIndex = *ActiveCoveragesCountPtr;
                    visualAvailable = visualIndex < ActiveCoverages.Length;
                    if (visualAvailable)
                    {
                        (*ActiveCoveragesCountPtr)++;
                        ActiveCoverages[visualIndex] = new CoverResult
                        {
                            Mode = CoverMode.SingleCircleDirect,
                            Circle1 = requestedCircle
                        };
                    }
                }
```

In the `WorldHit` branch (line ~376-389): change to write directly into `hitRender` (the render buffer is locked — `BeginHandler` ran first):

```csharp
                else if (visualKind == DJAutoHandVisualKind.WorldHit && _showHandThisFrame)
                {
                    visualIndex = _worldPosHitsNextFrameCount;
                    visualAvailable = visualIndex < _worldPosHitsNextFrame.Length;
                    if (visualAvailable)
                    {
                        var idx = Interlocked.Increment(ref *HitWriteCountPtr) - 1;
                        _worldPosHitsNextFrameCount++;
                        hitRender[idx] = new HitRenderData
                        {
                            pos = requestedCircle.Center,
                            radius = requestedCircle.Radius,
                            color = new float4(1, 0, 0, 0.75f)
                        };
                    }
                }
```

In `TryExpandDJAutoHand` (lines ~483-494), the visual-sync blocks must also repoint: `_activeCoveragesNextFrame[hand.VisualIndex]` → `ActiveCoverages[hand.VisualIndex]` and the world-hit block becomes:

```csharp
            else if (hand.VisualIndex >= 0 && hand.VisualKind == DJAutoHandVisualKind.WorldHit)
            {
                hitRender[hand.VisualIndex] = new HitRenderData
                {
                    pos = hand.Circle.Center,
                    radius = hand.Circle.Radius,
                    color = new float4(1, 0, 0, 0.75f)
                };
            }
```

Then delete the now-unused fields `_activeCoveragesNextFrame`, `_worldPosHitsNextFrame` and keep `_worldPosHitsNextFrameCount` (rename it to `_worldPosHitsCount` in the 3 places it appears: field, `BeginSimTick`, the `WorldHit` branch above).

- [ ] **Step 6: Drop the dead `nextFrame` parameter paths**

In `HandleButtonInput` (line ~613) and `HandleWorldPosInput` (line ~626): remove the `bool nextFrame = false` parameter and the `if (nextFrame) ... else ...` branches, keeping only the current-frame writes (`SetThisFrameButtonOn`/`SetThisFrameSensorOn`). Update the two call sites in `InputManager.BeginHandler` (`CheckButton`/`CheckScreenPos` — they don't pass `nextFrame`, so no change needed there).

- [ ] **Step 7: Update `ResetState` and `Dispose`**

In `InputDataB.ResetState` (line ~779): remove the `_buttonActiveDownNextFrame`/`_sensorActiveDownNextFrame` loops lines:
```csharp
                _buttonActiveDownNextFrame[i] = 0;
                _sensorActiveDownNextFrame[i] = 0;
```
And the fields now removed (`_activeCoveragesNextFrame`, `_worldPosHitsNextFrame`), adjust:
```csharp
            _djAutoInputCount = 0;
            _djAutoHandsWriteLock = 0;
            *ActiveCoveragesCountPtr = 0;
            _worldPosHitsCount = 0;
```

In `InputDataB.Dispose` (line ~803): remove the `Dispose()` lines for `_sensorActiveDownNextFrame`, `_buttonActiveDownNextFrame`, `_activeCoveragesNextFrame`, `_worldPosHitsNextFrame`; keep `_djAutoHandsNextFrame`, `ActiveCoverages`, `ActiveCoveragesCountPtr` (still used — the hand list is the sim's per-tick working set, `ActiveCoverages` is the current-frame visual list).

In `InputDataB.Init` (line ~206): remove the `new(...)` lines for the four removed arrays; keep `_djAutoHandsNextFrame = new(DJAUTO_MAX_CONCURRENT_INPUTS, Allocator.Persistent);` and `ActiveCoverages = new(32, Allocator.Persistent);`.

- [ ] **Step 8: Remove the `DJAUTO_AUTOPLAY_START_SEC` hack**

In `InputManager` class:
- Delete lines ~33-36 (the `DJAutoAutoplayStartSecKey` struct, `DJAUTO_AUTOPLAY_START_SEC_SS`, and the `DJAUTO_AUTOPLAY_START_SEC` property).
- In the `InputManager()` constructor (line ~55): delete `DJAUTO_AUTOPLAY_START_SEC_SS.Data = -0.013f;`.

Keep `DJAUTO_TOUCH_DOUBLE_CIRCLE_SLIDE_START_SEC`, `DJAUTO_SLIDE_TAP_GUIDE_DELAY_SEC`, `DJAUTO_SLIDE_RELEASE_DELAY_SEC` (the sim uses them).

- [ ] **Step 9: Remove the per-frame assignments**

In `NoteManager.Update` (lines ~156-162): delete the whole block:
```csharp
            if (!_timeProvider.IsRecord)
            {
                // 防止帧率对“下一帧应用”的机制产生过大影响
                // Record模式下在TimeProvider中设定
                InputManager.DJAUTO_AUTOPLAY_START_SEC_SS.Data = -Time.unscaledDeltaTime;
            }
```

In `TimeProvider.SetStartTime` (line ~190): delete `InputManager.DJAUTO_AUTOPLAY_START_SEC_SS.Data = -1f / fps;` (and the comment line above it).

- [ ] **Step 10: Verify**

Run: `grep -rn "DJAUTO_AUTOPLAY_START_SEC\|_buttonActiveDownNextFrame\|_sensorActiveDownNextFrame\|_activeCoveragesNextFrame" Assets/Scripts/`
Expected: no matches (all removed). The jobs still reference `InputManager.DJAUTO_AUTOPLAY_START_SEC` in `AutoplayUpdate` bodies (Tap/Slide/Touch/TouchHold/Hold) — those will compile-fail; that's expected and fixed in Task 4. (Do NOT build now.)

- [ ] **Step 11: Commit**

```bash
git add Assets/Scripts/Managers/InputManager.cs Assets/Scripts/Managers/NoteManager.cs Assets/Scripts/Managers/TimeProvider.cs
git commit -m "refactor: DJAuto writes current-frame state, drop next-frame buffers"
```

---

### Task 3: New `DJAutoSim` — the fixed-rate autoplay simulation

**Files:**
- Create: `Assets/Scripts/Managers/DJAutoSim.cs`
- Create: `Assets/Scripts/Managers/DJAutoSim.cs.meta`

The sim holds no native allocations (it reads the arrays NoteManager passes per frame) and is a plain main-thread class. Its per-note logic is a faithful move of the `AutoplayUpdate` bodies from the five jobs, parameterized on `simTime` instead of `TimeData.NoteTime`.

- [ ] **Step 1: Write the sim skeleton + context**

```csharp
using MajdataViewX.Notes;
using MajdataViewX.Notes.NoteDatas;
using MajdataViewX.Types.Enums;
using MajdataViewX.Types.Notes;
using MajdataViewX.Types.Notes.RenderData;
using MajdataViewX.Utils;
using MajdataViewX.Utils.Extensions;
using MajSimai;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using static MajdataViewX.Base.MajBurst;
using static MajdataViewX.Base.MajCtx;

namespace MajdataViewX.Managers
{
    /// <summary>
    /// DJAuto/Autoplay fixed-rate simulation. Steps at SIM_DT (240Hz) on the
    /// wall-clock-accurate NoteTime, independent of render FPS, so lag cannot
    /// degrade autoplay input timing or its judgments.
    /// </summary>
    public unsafe class DJAutoSim
    {
        public const float SIM_DT = 1f / 240;
        // 暂停/卡顿后最多补多少 tick，避免一次性追大量时间（暂停本身会重锚）。
        public const float MAX_CATCH_UP_SEC = 0.1f;

        /// <summary>每帧构建一次：DJAutoSim 需要的全部原生数据。</summary>
        public struct SimContext
        {
            public NativeList<TapData> Taps;
            public NativeList<HoldData> Holds;
            public NativeList<SlideData> Slides;
            public NativeList<TouchData> Touches;
            public NativeList<TouchHoldData> TouchHolds;
            public NativeList<CoverResult> TouchGroupCoverResults;
            public NativeList<CoverResult> TouchHoldGroupCoverResults;

            [NativeDisableUnsafePtrRestriction] public bool* SfxRequests;
            [NativeDisableUnsafePtrRestriction] public EffectData* JudgeEffectRequests;
            public NativeList<ReportResultEntry>.ParallelWriter ReportResults;
        }

        float _acc;
        float _lastSimNoteTime;

        public void Reset()
        {
            _acc = 0;
            _lastSimNoteTime = 0;
        }

        /// <summary>推进固定步长模拟。noteTime 为 TimeProvider 的 NoteTime。</summary>
        public void Step(float noteTime, in SimContext ctx)
        {
            var delta = noteTime - _lastSimNoteTime;
            _lastSimNoteTime = noteTime;
            if (delta <= 0 || delta > 4f) { _acc = 0; return; } // 重置/跳变直接重锚

            _acc = math.min(_acc + delta, MAX_CATCH_UP_SEC);
            while (_acc >= SIM_DT)
            {
                Tick(noteTime - _acc, ctx);
                _acc -= SIM_DT;
            }
        }

        void Tick(float simTime, in SimContext ctx)
        {
            var mode = NoteHelper.AutoPlayMode;
            if (mode == AutoPlayMode.Disable)
            {
                UpdateSlidesMine(simTime, ctx); // Disable 只处理 mine slide
                return;
            }

            InputData.BeginSimTick();
            if (ctx.Taps.IsCreated) UpdateTaps(simTime, mode, ctx);
            if (ctx.Holds.IsCreated) UpdateHolds(simTime, mode, ctx);
            if (ctx.Slides.IsCreated) UpdateSlides(simTime, mode, ctx);
            if (ctx.Touches.IsCreated) UpdateTouches(simTime, mode, ctx);
            if (ctx.TouchHolds.IsCreated) UpdateTouchHolds(simTime, mode, ctx);
        }
        // ... per-note methods follow (next steps)
    }
}
```

- [ ] **Step 2: Taps**

```csharp
        void UpdateTaps(float simTime, AutoPlayMode mode, in SimContext ctx)
        {
            for (int i = 0; i < ctx.Taps.Length; i++)
            {
                ref var tap = ref ctx.Taps.ElementRef(i);
                if (tap.IsEnd) return;
                var timing = simTime - tap.Time;
                if (timing < 0) return; // notes are time-ordered

                switch (mode)
                {
                    case AutoPlayMode.Enable:
                        tap.JudgeGrade = JudgeGrade.LateCritical;
                        tap.IsJudged = true;
                        tap.Diff = 0;
                        EndTap(ref tap, ctx);
                        break;
                    case AutoPlayMode.Random:
                        var grade = (JudgeGrade)GlobalRandom.NextInt((int)JudgeGrade.FastGood, (int)JudgeGrade.Miss);
                        tap.JudgeGrade = tap.IsMine
                            ? (grade < JudgeGrade.FastPerfect3rd ? JudgeGrade.Miss : JudgeGrade.LateCritical)
                            : grade;
                        tap.IsJudged = true;
                        tap.Diff = grade >= JudgeGrade.LateCritical ? 11.4514f : -11.4514f;
                        EndTap(ref tap, ctx);
                        break;
                    case AutoPlayMode.DJAutoButton:
                        if (tap.IsMine) break;
                        if (!tap.IsJudged)
                        {
                            InputData.DJAutoSetButtonOn(tap.Key);
                            tap.DjAutoPressed = true;
                            tap.DjAutoPressTime = simTime;
                        }
                        break;
                    case AutoPlayMode.DJAutoSensor:
                        if (tap.IsMine) break;
                        if (!tap.IsJudged && !tap.IsSlideGuide)
                        {
                            InputData.DJAutoSetSensorOn(tap.Key);
                            tap.DjAutoPressed = true;
                            tap.DjAutoPressTime = simTime;
                        }
                        break;
                }
            }
        }

        void EndTap(ref TapData tap, in SimContext ctx)
        {
            NoteHelper.PlayTapSound(ctx.SfxRequests,
                tap.JudgeGrade, tap.IsBreak, tap.IsEx, tap.IsMine, tap.Diff);
            NoteHelper.PlayTapEffect(ctx.JudgeEffectRequests,
                (int)tap.Key, tap.JudgeGrade, tap.IsBreak, tap.IsMine);
            NoteHelper.ReportResult(ctx.ReportResults,
                tap.JudgeGrade, tap.IsBreak, SimaiNoteType.Tap);
            InputData.NextTapHold(tap.Key);
            tap.IsEnd = true;
        }
```

> Note: the per-note loops use `return` inside `for` after the trigger check — notes are appended in time order (see `NoteManager.Loader.cs`), so once `timing < 0` the rest of the list is also in the future. This is the "single compare early-exit".

- [ ] **Step 3: Holds**

```csharp
        void UpdateHolds(float simTime, AutoPlayMode mode, in SimContext ctx)
        {
            for (int i = 0; i < ctx.Holds.Length; i++)
            {
                ref var hold = ref ctx.Holds.ElementRef(i);
                if (hold.isEnd) return;
                var timing = simTime - hold.time;
                if (timing < 0) return;

                switch (mode)
                {
                    case AutoPlayMode.Enable:
                        if (!hold.isHeadJudged)
                        {
                            hold.judgeGrade = JudgeGrade.LateCritical;
                            hold.isHeadJudged = true;
                            hold.isHolding = true;
                            hold.headDiff = 0;
                            NoteHelper.PlayHoldSound(ctx.SfxRequests,
                                hold.judgeGrade, hold.isBreak, hold.isEx, hold.isMine, hold.headDiff);
                        }
                        if (hold.isHeadJudged && math.max(hold.LastFor - timing, 0) <= 0)
                        {
                            hold.holdPercent = 1f;
                            EndHold(ref hold, ctx);
                        }
                        break;
                    case AutoPlayMode.Random:
                        if (!hold.isHeadJudged)
                        {
                            var grade = (JudgeGrade)GlobalRandom.NextInt((int)JudgeGrade.FastGood, (int)JudgeGrade.Miss);
                            hold.judgeGrade = hold.isMine
                                ? (grade < JudgeGrade.FastPerfect3rd ? JudgeGrade.Miss : JudgeGrade.LateCritical)
                                : grade;
                            hold.isHeadJudged = true;
                            hold.isHolding = true;
                            hold.headDiff = grade >= JudgeGrade.LateCritical ? 11.4514f : -11.4514f;
                            NoteHelper.PlayHoldSound(ctx.SfxRequests,
                                hold.judgeGrade, hold.isBreak, hold.isEx, hold.isMine, hold.headDiff);
                        }
                        if (hold.isHeadJudged && hold.LastFor - timing <= 0)
                        {
                            hold.holdPercent = 1f;
                            EndHold(ref hold, ctx);
                        }
                        break;
                    case AutoPlayMode.DJAutoButton:
                        if (hold.isMine) break;
                        if (!hold.isHeadJudged || math.max(hold.LastFor - timing, 0) > 0)
                        {
                            InputData.DJAutoSetButtonOn(hold.Key);
                            if (!hold.DjAutoPressed)
                            {
                                hold.DjAutoPressed = true;
                                hold.DjAutoPressTime = simTime;
                            }
                        }
                        break;
                    case AutoPlayMode.DJAutoSensor:
                        if (hold.isMine) break;
                        if (!hold.isHeadJudged || math.max(hold.LastFor - timing, 0) > 0)
                        {
                            InputData.DJAutoSetSensorOn(hold.Key);
                            if (!hold.DjAutoPressed)
                            {
                                hold.DjAutoPressed = true;
                                hold.DjAutoPressTime = simTime;
                            }
                        }
                        break;
                }
            }
        }

        void EndHold(ref HoldData hold, in SimContext ctx)
        {
            NoteHelper.ReportResult(ctx.ReportResults,
                hold.judgeGrade, hold.isBreak, SimaiNoteType.Hold);
            InputData.NextTapHold(hold.Key);
            hold.isEnd = true;
        }
```

(Verify `EndNote`'s exact body in `HoldUpdateJob.cs` — it also may play a tail sound / effect; copy the job's `EndNote` faithfully, replacing the job's field accesses with the `ctx` pointers.)

- [ ] **Step 4: Touches**

```csharp
        void UpdateTouches(float simTime, AutoPlayMode mode, in SimContext ctx)
        {
            for (int i = 0; i < ctx.Touches.Length; i++)
            {
                ref var touch = ref ctx.Touches.ElementRef(i);
                if (touch.isEnd) return;
                var timing = simTime - touch.time;
                if (touch.coverageId < 0 && NoteHelper.IsSimulated) continue;
                if (timing < 0) return;

                var autoplayStart = mode is AutoPlayMode.DJAutoButton or AutoPlayMode.DJAutoSensor &&
                                    ctx.TouchGroupCoverResults[touch.coverageId].Mode == CoverMode.DoubleCircleSlide
                    ? InputManager.DJAUTO_TOUCH_DOUBLE_CIRCLE_SLIDE_START_SEC
                    : 0f;
                if (timing < autoplayStart) continue;

                switch (mode)
                {
                    case AutoPlayMode.Enable:
                        touch.judgeGrade = JudgeGrade.LateCritical;
                        touch.isJudged = true;
                        touch.diff = 0;
                        EndTouch(ref touch, ctx);
                        break;
                    case AutoPlayMode.Random:
                        var grade = (JudgeGrade)GlobalRandom.NextInt((int)JudgeGrade.FastGood, (int)JudgeGrade.Miss);
                        touch.judgeGrade = touch.isMine
                            ? (grade < JudgeGrade.FastPerfect3rd ? JudgeGrade.Miss : JudgeGrade.LateCritical)
                            : grade;
                        touch.isJudged = true;
                        touch.diff = grade >= JudgeGrade.LateCritical ? 11.4514f : -11.4514f;
                        EndTouch(ref touch, ctx);
                        break;
                    case AutoPlayMode.DJAutoButton:
                    case AutoPlayMode.DJAutoSensor:
                        if (touch.isMine) break;
                        if (!touch.isJudged && !touch.isSlideGuide)
                        {
                            InputData.DJAutoAddGroupCoverage(ctx.TouchGroupCoverResults[touch.coverageId], timing);
                            if (!touch.DjAutoPressed)
                            {
                                touch.DjAutoPressed = true;
                                touch.DjAutoPressTime = simTime;
                            }
                        }
                        break;
                }
            }
        }

        void EndTouch(ref TouchData touch, in SimContext ctx)
        {
            NoteHelper.PlayTouchSound(ctx.SfxRequests, touch.judgeGrade, touch.isBreak, touch.isEx, touch.isMine, touch.diff);
            NoteHelper.PlayTouchEffect(ctx.JudgeEffectRequests, (int)touch.sensor, touch.judgeGrade, touch.isBreak, touch.isMine);
            NoteHelper.ReportResult(ctx.ReportResults, touch.judgeGrade, touch.isBreak, SimaiNoteType.Touch);
            InputData.NextTouch(touch.sensor);
            touch.isEnd = true;
        }
```

(Verify the exact helper names/arity in `TouchUpdateJob.cs` — copy its `EndNote`/`PlayTouchSound` calls faithfully.)

- [ ] **Step 5: TouchHolds**

```csharp
        void UpdateTouchHolds(float simTime, AutoPlayMode mode, in SimContext ctx)
        {
            for (int i = 0; i < ctx.TouchHolds.Length; i++)
            {
                ref var th = ref ctx.TouchHolds.ElementRef(i);
                if (th.isEnd) return;
                var timing = simTime - th.time;
                if (timing < 0) return;

                switch (mode)
                {
                    case AutoPlayMode.Enable:
                        if (!th.isHeadJudged)
                        {
                            th.judgeGrade = JudgeGrade.LateCritical;
                            th.isHeadJudged = true;
                            th.isHolding = true;
                            th.headDiff = 0;
                        }
                        if (th.isHeadJudged)
                        {
                            var remaining = math.max(th.LastFor - timing, 0);
                            if (remaining <= 0)
                            {
                                th.holdPercent = 1f;
                                EndTouchHold(ref th, ctx);
                            }
                        }
                        break;
                    case AutoPlayMode.Random:
                        if (!th.isHeadJudged)
                        {
                            var grade = (JudgeGrade)GlobalRandom.NextInt((int)JudgeGrade.FastGood, (int)JudgeGrade.Miss);
                            th.judgeGrade = th.isMine
                                ? (grade < JudgeGrade.FastPerfect3rd ? JudgeGrade.Miss : JudgeGrade.LateCritical)
                                : grade;
                            th.isHeadJudged = true;
                            th.isHolding = true;
                            th.headDiff = grade >= JudgeGrade.LateCritical ? 11.4514f : -11.4514f;
                        }
                        if (th.isHeadJudged)
                        {
                            var remaining = math.max(th.LastFor - timing, 0);
                            if (remaining <= 0)
                            {
                                th.holdPercent = 1f;
                                EndTouchHold(ref th, ctx);
                            }
                        }
                        break;
                    case AutoPlayMode.DJAutoButton:
                    case AutoPlayMode.DJAutoSensor:
                        if (th.isMine) break;
                        if (!th.isHeadJudged)
                        {
                            if (th.headCoverageId >= 0)
                                InputData.DJAutoAddGroupCoverage(ctx.TouchGroupCoverResults[th.headCoverageId], timing);
                            if (!th.DjAutoPressed)
                            {
                                th.DjAutoPressed = true;
                                th.DjAutoPressTime = simTime;
                            }
                        }
                        else if (math.max(th.LastFor - timing, 0) > 0)
                        {
                            if (th.coverageId >= 0)
                                InputData.DJAutoAddGroupCoverage(ctx.TouchHoldGroupCoverResults[th.coverageId]);
                        }
                        break;
                }
            }
        }

        void EndTouchHold(ref TouchHoldData th, in SimContext ctx)
        {
            // copy the job's EndNote faithfully (report + any sound/effect)
            NoteHelper.ReportResult(ctx.ReportResults, th.judgeGrade, th.isBreak, SimaiNoteType.TouchHold);
            InputData.NextTouch(th.sensor);
            th.isEnd = true;
        }
```

- [ ] **Step 6: Slides — non-mine DJAuto input + Enable/Random judging**

```csharp
        void UpdateSlides(float simTime, AutoPlayMode mode, in SimContext ctx)
        {
            for (int i = 0; i < ctx.Slides.Length; i++)
            {
                ref var slide = ref ctx.Slides.ElementRef(i);
                // 与 SlideUpdateJob.AutoplayUpdate 相同的入口条件
                if (slide.isEnd || slide.isSlideEnd ||
                   (slide.isJudged && simTime > slide.judgeTime + InputManager.DJAUTO_SLIDE_RELEASE_DELAY_SEC)
                ) continue;

                var timing = simTime - slide.shootTime;
                var autoplayStart = mode == AutoPlayMode.DJAutoButton && slide.hasTapGuide
                    ? InputManager.DJAUTO_SLIDE_TAP_GUIDE_DELAY_SEC
                    : 0f;
                if (timing < autoplayStart) continue;

                switch (mode)
                {
                    case AutoPlayMode.Enable:
                    case AutoPlayMode.Random:
                        {
                            if (slide.smoothSlideAnime)
                            {
                                slide.eaten = slide.processIdx - 1;
                            }
                            else
                            {
                                if (slide.processIdx > slide.judgeQueue[slide.judgeCurrent].ArrowProgressFinish)
                                {
                                    slide.eaten = slide.judgeQueue[slide.judgeCurrent].ArrowProgressFinish;
                                    slide.judgeCurrent++;
                                }
                                else if (slide.processIdx > slide.judgeQueue[slide.judgeCurrent].ArrowProgressPush)
                                {
                                    slide.eaten = slide.judgeQueue[slide.judgeCurrent].ArrowProgressPush;
                                }
                            }

                            if (!slide.isSoundPlayed)
                            {
                                NoteHelper.PlaySlideSound(ctx.SfxRequests, slide.isBreak);
                                slide.isSoundPlayed = true;
                            }

                            if (slide.process >= 1)
                            {
                                if (mode is AutoPlayMode.Enable)
                                {
                                    slide.judgeGrade = JudgeGrade.LateCritical;
                                }
                                else
                                {
                                    var grade = (JudgeGrade)GlobalRandom.NextInt((int)JudgeGrade.FastGood, (int)JudgeGrade.Miss);
                                    slide.judgeGrade = slide.isMine
                                        ? (grade < JudgeGrade.FastPerfect3rd ? JudgeGrade.TooFast : JudgeGrade.LateCritical)
                                        : grade;
                                }
                                slide.finishJudgeTiming = simTime;
                                FinishJudgeSlide(ref slide, simTime);
                                EndSlide(ref slide);
                                if (slide.isFolded) EndSlideFolded(ref slide);
                            }
                            break;
                        }
                    case AutoPlayMode.DJAutoButton:
                    case AutoPlayMode.DJAutoSensor:
                        {
                            if (slide.isMine) break;
                            if (slide.isFolded) break;

                            var inputProcess = autoplayStart > 0
                                ? math.saturate((timing - autoplayStart) / math.max(slide.LastFor, 0.001f))
                                : math.saturate(timing / math.max(slide.LastFor, 0.001f)); // == slide.process 于 simTime

                            if (!slide.isWifi)
                            {
                                if (autoplayStart <= 0)
                                {
                                    InputData.DJAutoHandleWorldPosition(slide.starPos);
                                    continue;
                                }

                                var lastIndex = slide.slideArrowsCount - 1;
                                var distance = inputProcess * slide.slideArrows[lastIndex].L;
                                var nextIndex = 1;
                                while (nextIndex < lastIndex && slide.slideArrows[nextIndex].L < distance)
                                    nextIndex++;

                                var previous = slide.slideArrows[nextIndex - 1];
                                var next = slide.slideArrows[nextIndex];
                                var progress = math.unlerp(previous.L, next.L, distance);
                                InputData.DJAutoHandleWorldPosition(
                                    new float2(
                                        math.lerp(previous.X, next.X, progress),
                                        math.lerp(previous.Y, next.Y, progress)
                                    )
                                );
                            }
                            else
                            {
                                var center = slide.starPosConstC * inputProcess + slide.starPosStart;
                                var left = slide.starPosConstL * inputProcess + slide.starPosStart;
                                var right = slide.starPosConstR * inputProcess + slide.starPosStart;
                                InputData.DJAutoHandleWifiWorldPosition(
                                    (left + center) / 2,
                                    (right + center) / 2
                                );
                            }
                            break;
                        }
                }
            }
        }
```

> Note: `slide.process` in the job is `saturate((TimeData.NoteTime - shootTime) / LastFor)` — the sim computes the same from `simTime`. `starPos`/`starPosL/R` are written by the render job each frame; for the `autoplayStart <= 0` fast path the sim uses the *last rendered* `starPos`, same as the job does today (it runs after TransformUpdate in the same job).

- [ ] **Step 7: Slides — mine auto-advance (DJAuto + Disable modes)**

```csharp
        void UpdateSlidesMine(float simTime, in SimContext ctx)
        {
            // 与 SlideUpdateJob.AutoplayUpdate 的 Disable 分支相同：mine slide 程序自动推进
            for (int i = 0; i < ctx.Slides.Length; i++)
            {
                ref var slide = ref ctx.Slides.ElementRef(i);
                if (slide.isEnd || slide.isSlideEnd ||
                   (slide.isJudged && simTime > slide.judgeTime + InputManager.DJAUTO_SLIDE_RELEASE_DELAY_SEC)
                ) continue;
                if (!slide.isMine || !slide.mineAutoSlide) continue;
                var timing = simTime - slide.shootTime;
                if (timing < 0) continue;

                var idx = slide.judgeCurrent;
                if (slide.isWifi)
                    idx = math.min(slide.judgeCurrent, math.min(slide.judgeL_Current, slide.judgeR_Current));
                if (idx >= slide.judgeQueueCount - 1) continue;

                if (slide.processIdx > slide.judgeQueue[idx].ArrowProgressFinish)
                {
                    slide.eaten = slide.judgeQueue[idx].ArrowProgressFinish;
                    if (slide.isWifi)
                    {
                        if (slide.judgeCurrent <= idx) { slide.judgeCurrent++; }
                        if (slide.judgeL_Current <= idx) { slide.judgeL_Current++; }
                        if (slide.judgeR_Current <= idx) { slide.judgeR_Current++; }
                    }
                    else
                    {
                        slide.judgeCurrent++;
                    }
                }
            }
        }
```

> Important: the mine-slide body in `SlideUpdateJob.AutoplayUpdate` (lines 374-441) is ~70 lines with wifi-specific cursor advancement and the "leave last area to CheckUpdate" rule. **Copy it faithfully** — the snippet above is the skeleton; read the job's lines 374-441 and port the exact cursor/eaten logic (including the `slide.isWifi` three-cursor advancement and the `starPos`-based `newEaten` guards).

- [ ] **Step 8: Slide end helpers**

Copy from `SlideUpdateJob.cs` the private helpers `FinishJudgeSlide` and `EndSlide` (and the folded variant), parameterized to take `simTime` where they read `TimeData.NoteTime` (e.g. `slide.judgeTime = simTime;`). Verify their exact bodies at lines ~660-794 and port them unchanged otherwise.

- [ ] **Step 9: Create the .meta**

Create `Assets/Scripts/Managers/DJAutoSim.cs.meta` by copying `Assets/Scripts/Managers/NoteManager.cs.meta` and giving it a fresh GUID (any unique 32-hex string, e.g. `a1b2c3d4e5f60718293a4b5c6d7e8f90` — ensure it does not collide with an existing GUID via `grep`).

- [ ] **Step 10: Verify**

The sim file is not referenced by anything yet. Run:
`grep -n "simTime" Assets/Scripts/Managers/DJAutoSim.cs | head` (expect matches) and eyeball-compile by checking all referenced members exist:
- `NoteHelper.PlayTapSound/PlayTapEffect/PlaySlideSound/PlayHoldSound/PlayTouchSound/PlayTouchEffect/ReportResult` (verify names/arity against `NoteHelper.cs` and `ObjectCounter.Counter.cs` — adjust if any differ)
- `InputData.NextTapHold/NextTouch`, `InputData.DJAutoSetButtonOn/DJAutoSetSensorOn/DJAutoAddGroupCoverage/DJAutoHandleWorldPosition/DJAutoHandleWifiWorldPosition`, `InputData.BeginSimTick`
- `SlideData` fields used: `processIdx`, `judgeQueue`, `judgeCurrent`, `judgeL_Current`, `judgeR_Current`, `starPosConstC/L/R`, `starPosStart`, `slideArrows`, `slideArrowsCount`, `eaten`, `isSoundPlayed`, `judgeTime`, `finishJudgeTiming`, `mineAutoSlide`, `smoothSlideAnime`, `isWifi`, `isFolded`, `hasTapGuide`, `LastFor`, `shootTime`
- `TouchData.coverageId`, `TouchHoldData.headCoverageId/coverageId`
- `EffectData`, `ReportResultEntry`, `SimaiNoteType` namespaces are the ones the jobs use (the jobs' `using` lines are the guide)

- [ ] **Step 11: Commit**

```bash
git add Assets/Scripts/Managers/DJAutoSim.cs Assets/Scripts/Managers/DJAutoSim.cs.meta
git commit -m "feat: DJAutoSim fixed-rate autoplay simulation (240Hz)"
```

---

### Task 4: Wire the sim in NoteManager + strip autoplay from the jobs (the functional switch)

**Files:**
- Modify: `Assets/Scripts/Managers/NoteManager.cs`
- Modify: `Assets/Scripts/Notes/Updaters/TapUpdateJob.cs`
- Modify: `Assets/Scripts/Notes/Updaters/HoldUpdateJob.cs`
- Modify: `Assets/Scripts/Notes/Updaters/SlideUpdateJob.cs`
- Modify: `Assets/Scripts/Notes/Updaters/TouchUpdateJob.cs`
- Modify: `Assets/Scripts/Notes/Updaters/TouchHoldUpdateJob.cs`

One atomic commit: from here on, autoplay comes only from the sim.

- [ ] **Step 1: NoteManager field + wiring**

Add field next to the other private fields (near line ~62):
```csharp
        readonly DJAutoSim _djAutoSim = new();
```

In `Update()`, right after `_inputManager.BeginHandler();` (line ~163) and after the empty-notes early return, add:

```csharp
            _djAutoSim.Step(_timeProvider.NoteTime, new DJAutoSim.SimContext
            {
                Taps = taps,
                Holds = holds,
                Slides = slides,
                Touches = touches,
                TouchHolds = touchHolds,
                TouchGroupCoverResults = touchGroupCoverResults,
                TouchHoldGroupCoverResults = touchHoldGroupCoverResults,
                SfxRequests = _audioManager.SfxRequestsPtr,
                JudgeEffectRequests = _effectManager.JudgeEffectRequestsPtr,
                ReportResults = _objectCounter.ReportRequestsWriter,
            });
```

Wait — the empty-notes early return is `if (taps.Length + ... == 0) return;` at line ~165. Place the `Step` call BEFORE that early return (the sim is a no-op with zero notes and must still run when notes exist). Final placement:

```csharp
            _inputManager.BeginHandler(); // 这里牵扯到用户输入，需要一直调用

            _djAutoSim.Step(_timeProvider.NoteTime, new DJAutoSim.SimContext
            {
                ...
            });

            if (taps.Length + eachLines.Length + holds.Length + slides.Length + touches.Length + touchHolds.Length == 0) return;
```

In `ResetState()` (line ~381), at the top:
```csharp
            _prevChain.Complete();
            _djAutoSim.Reset();
```

- [ ] **Step 2: TapUpdateJob — remove autoplay, add press-time judgment**

In `Execute` (line ~39): delete `AutoplayUpdate(ref tap);` and the whole `AutoplayUpdate` method (lines 116-158).

In `CheckUpdate` (line ~160), after the two guards (`if (tap.IsEnd) return; if (!NoteHelper.IsSimulated) return;`), insert:

```csharp
            // DJAuto 的输入由 DJAutoSim 在精确时刻发出；用记录的按下时间判定，不受渲染帧率影响。
            if (tap.DjAutoPressed && !tap.IsJudged)
            {
                tap.JudgeGrade = NoteHelper.GetTapJudge(tap.DjAutoPressTime - tap.Time, tap.IsEx);
                tap.IsJudged = true;
                tap.Diff = tap.DjAutoPressTime - tap.Time;
                EndNote(ref tap);
                return;
            }
```

- [ ] **Step 3: HoldUpdateJob — remove autoplay, add press-time head judgment**

Delete `AutoplayUpdate(ref hold);` (line ~44) and the method (lines 220-290).

In `CheckUpdate`, locate the head-judgment block (starts ~line 300, the `if (hold.isMine)` branch then the normal branch that reads `buttonClicked`/`sensorClicked`). Before the normal `clicked` judgment, insert:

```csharp
            if (hold.DjAutoPressed && !hold.isHeadJudged && !hold.isMine)
            {
                hold.judgeGrade = NoteHelper.GetTapJudge(hold.DjAutoPressTime - hold.time, hold.isEx);
                hold.isHeadJudged = true;
                hold.isHolding = true;
                hold.headDiff = hold.DjAutoPressTime - hold.time;
                NoteHelper.PlayHoldSound(SfxRequests,
                    hold.judgeGrade, hold.isBreak, hold.isEx, hold.isMine, hold.headDiff);
            }
```

(The mine branch above stays untouched. The hold-continue/end logic later in `CheckUpdate` — driven by `Status` levels the sim holds — stays.)

- [ ] **Step 4: TouchUpdateJob — remove autoplay, add press-time judgment**

Delete `AutoplayUpdate(ref touch);` (line ~45) and the method (lines 170-213).

In `CheckUpdate` (line ~215), after its guards, insert:

```csharp
            if (touch.DjAutoPressed && !touch.isJudged)
            {
                touch.judgeGrade = NoteHelper.GetTouchJudge(touch.DjAutoPressTime - touch.time, touch.isEx);
                touch.isJudged = true;
                touch.diff = touch.DjAutoPressTime - touch.time;
                EndNote(ref touch);
                return;
            }
```

(Verify the actual touch judge method name/arity in `NoteHelper.cs` — `GetTouchJudge` exists at line ~110; adjust `EndNote` call to the job's actual signature if needed.)

- [ ] **Step 5: TouchHoldUpdateJob — remove autoplay, add press-time head judgment**

Delete `AutoplayUpdate(ref th);` (line ~54) and the method (lines 176-243).

In `CheckUpdate` (line ~245), after its guards, insert (head):

```csharp
            if (th.DjAutoPressed && !th.isHeadJudged && !th.isMine)
            {
                th.judgeGrade = NoteHelper.GetTouchJudge(th.DjAutoPressTime - th.time, th.isEx);
                th.isHeadJudged = true;
                th.isHolding = true;
                th.headDiff = th.DjAutoPressTime - th.time;
            }
```

(Verify `GetTouchJudge` usage in the existing head-judge block of the job and mirror it.)

- [ ] **Step 6: SlideUpdateJob — remove autoplay only**

Delete `AutoplayUpdate(ref slide);` (line ~54) and the method (lines 245-441). `CheckUpdate` and `TransformUpdate` stay untouched (slide judgment remains render-side; it is level-driven by the sim's held sensors and can advance multiple sections per frame).

- [ ] **Step 7: Verify references are gone**

Run: `grep -rn "AutoplayUpdate\|DJAUTO_AUTOPLAY_START_SEC" Assets/Scripts/`
Expected: no matches (the sim file contains neither token).

- [ ] **Step 8: Commit**

```bash
git add Assets/Scripts/Managers/NoteManager.cs Assets/Scripts/Notes/Updaters/
git commit -m "feat: run autoplay on 240Hz sim clock, judge DJAuto presses by exact time"
```

---

### Task 5: Full build verification

**Files:** none (verification only)

- [ ] **Step 1: Run the Linux build (Unity IL2CPP compile of all scripts)**

```bash
scripts/build-linux.sh
```

Expected: Unity batch build succeeds (this compiles all C# + Burst), player produced at `build/Linux/MajdataViewX`, editor dotnet publish succeeds, zip created. Takes ~6-10 minutes. If compile errors appear, fix them (they will be missing member names in the sim's ported helpers — compare against the job sources), then re-run. Do NOT attempt to launch the player (`AGENTS.md` forbids GUI launch testing).

- [ ] **Step 2: Fix and amend if needed**

If the build failed, fix, re-run, then commit the fixes as a separate commit (do not amend Task 4's commit):
```bash
git add -A Assets/Scripts
git commit -m "fix: compile fixes for DJAuto sim integration"
```

- [ ] **Step 3: Final review sweep**

Run `git log --oneline -6` and confirm the four commits are present. Grep for leftover dead code:
```bash
grep -rn "nextFrame" Assets/Scripts/Managers/InputManager.cs
```
Expected: only the `SimContext`-unrelated leftovers, if any — ideally none (the `HandleButtonInput`/`HandleWorldPosInput` `nextFrame` params were removed in Task 2 Step 6).

---

## Self-review notes (done by plan author)

- **Spec coverage:** accumulator + clamp ✓ (Task 3 Step 1); per-tick hand reset ✓ (Task 2 Step 4); exact press times ✓ (Task 1, Task 4 Steps 2-5); next-frame buffer + start-sec hack removal ✓ (Task 2 Steps 1-3, 8-9); mine slides in Disable ✓ (Task 3 Step 7); record mode: no special-casing, accumulator handles it ✓ (Task 3 Step 1); user input coexistence ✓ (jobs' CheckUpdate user paths untouched); hand visuals per render frame ✓ (Task 2 Step 5 — sim writes the locked render buffer).
- **Known residual (documented in spec):** slide *section advancement* (`ProcessAreas`) stays render-side; it is level-driven so it remains correct, just render-quantized. `FinishJudgeSlide` for DJAuto slides happens at render frames. Acceptable per spec; a follow-up could move `ProcessAreas` into the sim.
- **Type consistency:** `DjAutoPressed`/`DjAutoPressTime` defined in Task 1, used in Tasks 3-4; `BeginSimTick` defined in Task 2 Step 4, used Task 3; `SimContext` fields match NoteManager's NativeList fields exactly.
- **Placeholder scan:** the two "copy faithfully from the job" spots (Task 3 Steps 3/5/7/8) are flagged as reads-first; the plan names the exact line ranges to copy so no guessing is needed.
