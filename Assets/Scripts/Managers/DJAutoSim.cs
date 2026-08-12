using MajdataViewX.Base;
using MajdataViewX.Notes;
using MajdataViewX.Notes.NoteDatas;
using MajdataViewX.Notes.SlideUtils;
using MajdataViewX.Types.Enums;
using MajdataViewX.Types.Input;
using MajdataViewX.Types.Notes;
using MajdataViewX.Types.Notes.RenderData;
using MajdataViewX.Utils;
using MajdataViewX.Utils.Extensions;
using MajSimai;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
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
        // Max catch-up after a hitch: never chase a large time gap in one burst (pauses re-anchor anyway).
        public const float MAX_CATCH_UP_SEC = 0.1f;
        // Gaps larger than this are treated as pause/seek: re-anchor and drop the elapsed sim time.
        public const float MAX_GAP_SEC = 4f;
        // The old -deltaTime compensation died with the next-frame buffer: a 240Hz tick has no frame latency to compensate.
        public const float SIM_AUTOPLAY_START_SEC = 0f;

        /// <summary>Built once per frame: all native data DJAutoSim needs.</summary>
        public struct SimContext
        {
            public NativeList<TapData> Taps;
            public NativeList<HoldData> Holds;
            public NativeList<SlideData> Slides;
            public NativeList<TouchData> Touches;
            public NativeList<TouchHoldData> TouchHolds;
            public NativeList<CoverResult> TouchGroupCoverResults;
            public NativeList<CoverResult> TouchHoldGroupCoverResults;
            public NativeList<int> TouchGroupTotalCounts;
            public NativeList<int> TouchGroupJudgedCounts;

            [NativeDisableUnsafePtrRestriction] public bool* SfxRequests;
            [NativeDisableUnsafePtrRestriction] public EffectData* JudgeEffectRequests;
            public NativeList<ReportResultEntry>.ParallelWriter ReportResults;
        }

        float _acc;
        float _lastSimNoteTime;
        public long TotalTicks { get; private set; }

        public void Reset()
        {
            _acc = 0;
            _lastSimNoteTime = 0;
            TotalTicks = 0;
        }

        /// <summary>Advance the fixed-step simulation. noteTime is TimeProvider's NoteTime.</summary>
        public void Step(float noteTime, in SimContext ctx)
        {
            var delta = noteTime - _lastSimNoteTime;
            _lastSimNoteTime = noteTime;
            if (delta <= 0 || delta > MAX_GAP_SEC) { _acc = 0; return; } // reset/seek: re-anchor
            if (delta > 1.5f)
                Debug.Log($"[dbg][sim] big catch-up delta={delta:F2}s");

            // Cap must cover this frame's advance: below 10 FPS (incl. low-fps export) the window
            // must be no smaller than the frame step, or notes in its tail never tick. Same as
            // MAX_CATCH_UP_SEC at normal FPS.
            var cap = math.max(MAX_CATCH_UP_SEC, delta + SIM_DT);
            _acc = math.min(_acc + delta, cap);
            while (_acc >= SIM_DT)
            {
                Tick(noteTime - _acc, ctx);
                _acc -= SIM_DT;
            }
        }

        void Tick(float simTime, in SimContext ctx)
        {
            TotalTicks++;
            if (TotalTicks == 1)
                Debug.Log($"[dbg][sim] first tick simTime={simTime:F3}");
            if ((TotalTicks & 511) == 0)
                Debug.Log($"[dbg][sim] tick={TotalTicks} simTime={simTime:F3}");
            var mode = NoteHelper.AutoPlayMode;
            // Always reset the per-tick counters (the Disable path self-cleans too, without relying on a mode-switch ResetState).
            InputData.BeginSimTick();
            if (mode == AutoPlayMode.Disable)
            {
                UpdateSlidesMine(simTime, ctx); // Disable mode only advances mine slides
                return;
            }
            if (ctx.Holds.IsCreated) UpdateHolds(simTime, mode, ctx);
            if (ctx.Slides.IsCreated) UpdateSlides(simTime, mode, ctx);
            if (ctx.TouchHolds.IsCreated) UpdateTouchHolds(simTime, mode, ctx);
            if (ctx.Taps.IsCreated) UpdateTaps(simTime, mode, ctx);
            if (ctx.Touches.IsCreated) UpdateTouches(simTime, mode, ctx);
        }

        void UpdateTaps(float simTime, AutoPlayMode mode, in SimContext ctx)
        {
            for (int i = 0; i < ctx.Taps.Length; i++)
            {
                ref var tap = ref ctx.Taps.ElementRef(i);
                if (tap.IsEnd) continue;
                var timing = simTime - tap.Time;
                // Notes are time-ordered, so later notes could early-out; `continue` is used uniformly to match the other types.
                if (timing < SIM_AUTOPLAY_START_SEC) continue;

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
                    case AutoPlayMode.DJAutoSensor:
                        if (tap.IsMine)
                        {
                            // Mine: touched -> Miss, untouched -> LateCritical after MINE_END (mirrors the render job)
                            var mineDown = InputData.GetButtonState(tap.Key).Status ||
                                           InputData.GetSensorState(tap.Key).Status;
                            if (mineDown && timing >= -NoteHelper.TAP_JUDGE_GOOD_AREA_MSEC / 1000f)
                            {
                                tap.JudgeGrade = JudgeGrade.Miss;
                                tap.IsJudged = true;
                                tap.Diff = timing;
                                EndTap(ref tap, ctx);
                            }
                            else if (timing >= NoteHelper.MINE_END_SEC)
                            {
                                tap.JudgeGrade = JudgeGrade.LateCritical;
                                tap.IsJudged = true;
                                EndTap(ref tap, ctx);
                            }
                            break;
                        }
                        if (!tap.IsJudged)
                        {
                            // Perfect player: press when the sensor is free; when occupied, press on the first release
                            if (mode == AutoPlayMode.DJAutoButton)
                            {
                                if (!InputData.GetButtonState(tap.Key).Status)
                                    InputData.DJAutoPressButton(tap.Key);
                            }
                            else
                            {
                                if (!InputData.GetSensorState(tap.Key).Status)
                                    InputData.DJAutoPressSensor(tap.Key);
                            }
                            JudgeTapByStatus(ref tap, timing, ctx);
                        }
                        break;
                }
            }
        }

        // Mirrors TapUpdateJob.EndNote (sim-side judging).
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

        /// <summary>
        /// DJAuto tap judging: observe the sensor level. The perfect player presses at the
        /// first pressable point at/after the note time - a free sensor is pressed by DJAuto
        /// itself; an occupied one (slide/hold) still counts as pressable (a finger can land
        /// on an already-held sensor), so "down" is judged at tick precision and never later
        /// than the first pressable moment.
        /// timing = simTime - tap.Time.
        /// </summary>
        void JudgeTapByStatus(ref TapData tap, float timing, in SimContext ctx)
        {
            var down = InputData.GetButtonState(tap.Key).Status ||
                       InputData.GetSensorState(tap.Key).Status;
            if (!down) return;
            tap.JudgeGrade = NoteHelper.GetTapJudge(timing, tap.IsEx);
            tap.IsJudged = true;
            tap.Diff = timing;
            EndTap(ref tap, ctx);
        }

        void UpdateHolds(float simTime, AutoPlayMode mode, in SimContext ctx)
        {
            for (int i = 0; i < ctx.Holds.Length; i++)
            {
                ref var hold = ref ctx.Holds.ElementRef(i);
                if (hold.isEnd) continue;
                var timing = simTime - hold.time;
                if (timing < SIM_AUTOPLAY_START_SEC) continue;

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
                        if (hold.isMine)
                        {
                            var mineDown = InputData.GetButtonState(hold.Key).Status ||
                                           InputData.GetSensorState(hold.Key).Status;
                            if (mineDown && timing >= -NoteHelper.TAP_JUDGE_GOOD_AREA_MSEC / 1000f)
                            {
                                hold.judgeGrade = JudgeGrade.Miss;
                                hold.isHeadJudged = true;
                                hold.headDiff = timing;
                                EndHold(ref hold, ctx);
                            }
                            else if (timing >= hold.LastFor)
                            {
                                hold.judgeGrade = JudgeGrade.LateCritical;
                                hold.isHeadJudged = true;
                                hold.holdPercent = 1f;
                                EndHold(ref hold, ctx);
                            }
                            break;
                        }
                        if (!hold.isHeadJudged || math.max(hold.LastFor - timing, 0) > 0)
                        {
                            // Head: press when free (creates the edge); hold phase: keep holding
                            if (!InputData.GetButtonState(hold.Key).Status)
                                InputData.DJAutoPressButton(hold.Key);
                        }
                        JudgeHoldHeadByStatus(ref hold, timing, ctx);
                        if (hold.isHeadJudged && math.max(hold.LastFor - timing, 0) <= 0)
                        {
                            hold.holdPercent = 1f;
                            EndHold(ref hold, ctx);
                        }
                        break;
                    case AutoPlayMode.DJAutoSensor:
                        if (hold.isMine)
                        {
                            var mineDown = InputData.GetButtonState(hold.Key).Status ||
                                           InputData.GetSensorState(hold.Key).Status;
                            if (mineDown && timing >= -NoteHelper.TAP_JUDGE_GOOD_AREA_MSEC / 1000f)
                            {
                                hold.judgeGrade = JudgeGrade.Miss;
                                hold.isHeadJudged = true;
                                hold.headDiff = timing;
                                EndHold(ref hold, ctx);
                            }
                            else if (timing >= hold.LastFor)
                            {
                                hold.judgeGrade = JudgeGrade.LateCritical;
                                hold.isHeadJudged = true;
                                hold.holdPercent = 1f;
                                EndHold(ref hold, ctx);
                            }
                            break;
                        }
                        if (!hold.isHeadJudged || math.max(hold.LastFor - timing, 0) > 0)
                        {
                            if (!InputData.GetSensorState(hold.Key).Status)
                                InputData.DJAutoPressSensor(hold.Key);
                        }
                        JudgeHoldHeadByStatus(ref hold, timing, ctx);
                        if (hold.isHeadJudged && math.max(hold.LastFor - timing, 0) <= 0)
                        {
                            hold.holdPercent = 1f;
                            EndHold(ref hold, ctx);
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// DJAuto hold-head judging: observe the level at tick precision (same as JudgeTapByStatus).
        /// </summary>
        void JudgeHoldHeadByStatus(ref HoldData hold, float timing, in SimContext ctx)
        {
            if (hold.isHeadJudged) return;
            var down = InputData.GetButtonState(hold.Key).Status ||
                       InputData.GetSensorState(hold.Key).Status;
            if (!down) return;
            hold.judgeGrade = NoteHelper.GetTapJudge(timing, hold.isEx);
            hold.isHeadJudged = true;
            hold.isHolding = true;
            hold.headDiff = timing;
            NoteHelper.PlayHoldSound(ctx.SfxRequests,
                hold.judgeGrade, hold.isBreak, hold.isEx, hold.isMine, hold.headDiff);
        }

        // Mirrors HoldUpdateJob.EndNote (sim-side judging).
        void EndHold(ref HoldData hold, in SimContext ctx)
        {
            NoteHelper.PlayTapSound(ctx.SfxRequests,
                hold.judgeGrade, false, false, hold.isMine, 0);
            NoteHelper.PlayTapEffect(ctx.JudgeEffectRequests,
                (int)hold.Key, hold.judgeGrade, hold.isBreak, hold.isMine);
            NoteHelper.ReportResult(ctx.ReportResults,
                hold.judgeGrade, hold.isBreak, SimaiNoteType.Hold);
            InputData.NextTapHold(hold.Key);
            hold.isEnd = true;
        }

        void UpdateTouches(float simTime, AutoPlayMode mode, in SimContext ctx)
        {
            for (int i = 0; i < ctx.Touches.Length; i++)
            {
                ref var touch = ref ctx.Touches.ElementRef(i);
                if (touch.isEnd) continue;
                var timing = simTime - touch.time;
                if (touch.coverageId < 0 && NoteHelper.IsSimulated) continue;
                var autoplayStart = mode is AutoPlayMode.DJAutoButton or AutoPlayMode.DJAutoSensor &&
                                    ctx.TouchGroupCoverResults[touch.coverageId].Mode == CoverMode.DoubleCircleSlide
                    ? InputManager.DJAUTO_TOUCH_DOUBLE_CIRCLE_SLIDE_START_SEC
                    : SIM_AUTOPLAY_START_SEC;
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
                        if (touch.isMine)
                        {
                            if (InputData.GetSensorState(touch.sensor).Status &&
                                timing >= -NoteHelper.TOUCH_JUDGE_GOOD_AREA_MSEC / 1000f)
                            {
                                touch.judgeGrade = JudgeGrade.Miss;
                                touch.isJudged = true;
                                touch.diff = timing;
                                EndTouch(ref touch, ctx);
                            }
                            else if (timing >= NoteHelper.MINE_END_SEC)
                            {
                                touch.judgeGrade = JudgeGrade.LateCritical;
                                touch.isJudged = true;
                                EndTouch(ref touch, ctx);
                            }
                            break;
                        }
                        if (!touch.isJudged && !touch.isSlideGuide)
                        {
                            InputData.DJAutoAddGroupCoverage(ctx.TouchGroupCoverResults[touch.coverageId], timing);
                            // Level-based judging at tick precision; group majority auto-judges the rest
                            var down = InputData.GetSensorState(touch.sensor).Status;
                            if (!down && touch.groupId != -1 &&
                                ctx.TouchGroupJudgedCounts[touch.groupId] * 2 >
                                ctx.TouchGroupTotalCounts[touch.groupId])
                                down = true;
                            if (down)
                            {
                                touch.judgeGrade = NoteHelper.GetTouchJudge(timing);
                                touch.isJudged = true;
                                touch.diff = timing;
                                EndTouch(ref touch, ctx);
                            }
                        }
                        break;
                }
            }
        }

        // Mirrors TouchUpdateJob.EndNote (sim-side; the sim maintains touchGroupJudgedCounts).
        void EndTouch(ref TouchData touch, in SimContext ctx)
        {
            if (touch.isBreak)
                NoteHelper.PlayTapSound(ctx.SfxRequests,
                    touch.judgeGrade, true, touch.isEx, false, touch.diff);
            else
                NoteHelper.PlayTouchSound(ctx.SfxRequests,
                    touch.judgeGrade, touch.isMine, touch.isHanabi);
            NoteHelper.PlayTouchEffect(ctx.JudgeEffectRequests,
                (int)touch.sensor + 8,
                touch.judgeGrade, touch.isBreak, touch.isHanabi, touch.isMine);
            NoteHelper.ReportResult(ctx.ReportResults,
                touch.judgeGrade, touch.isBreak, SimaiNoteType.Touch);

            InputData.NextTouch(touch.sensor);
            MajBurst.MultTouchHandler.Unregister(touch.sensor);
            if (touch.isAppeared)
            {
                touch.isAppeared = false;
                MajBurst.MultTouchHandler.UnregisterActive(touch.sensor);
            }
            if (touch.groupId != -1 && touch.judgeGrade != JudgeGrade.Miss)
            {
                var judged = ctx.TouchGroupJudgedCounts;
                judged[touch.groupId]++;
            }

            touch.isEnd = true;
        }

        void UpdateTouchHolds(float simTime, AutoPlayMode mode, in SimContext ctx)
        {
            for (int i = 0; i < ctx.TouchHolds.Length; i++)
            {
                ref var th = ref ctx.TouchHolds.ElementRef(i);
                if (th.isEnd) continue;
                var timing = simTime - th.time;
                if (timing < SIM_AUTOPLAY_START_SEC) continue;

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
                                continue;
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
                                continue;
                            }
                        }
                        break;
                    case AutoPlayMode.DJAutoButton:
                    case AutoPlayMode.DJAutoSensor:
                        if (th.isMine)
                        {
                            if (InputData.GetSensorState(th.sensor).Status &&
                                timing >= -NoteHelper.TOUCH_JUDGE_GOOD_AREA_MSEC / 1000f)
                            {
                                th.judgeGrade = JudgeGrade.Miss;
                                th.isHeadJudged = true;
                                th.headDiff = timing;
                                EndTouchHold(ref th, ctx);
                            }
                            else if (timing >= th.LastFor)
                            {
                                th.judgeGrade = JudgeGrade.LateCritical;
                                th.isHeadJudged = true;
                                th.holdPercent = 1f;
                                EndTouchHold(ref th, ctx);
                            }
                            break;
                        }
                        // Head phase uses the touchGroup cover (shared with touches); hold phase uses the touchHoldGroup cover
                        if (!th.isHeadJudged)
                        {
                            if (th.headCoverageId >= 0)
                                InputData.DJAutoAddGroupCoverage(ctx.TouchGroupCoverResults[th.headCoverageId], timing);
                            // Level-based judging at tick precision; head-group majority auto-judges too
                            var down = InputData.GetSensorState(th.sensor).Status;
                            if (!down && th.headGroupId != -1 &&
                                ctx.TouchGroupJudgedCounts[th.headGroupId] * 2 >
                                ctx.TouchGroupTotalCounts[th.headGroupId])
                                down = true;
                            if (down)
                            {
                                th.judgeGrade = NoteHelper.GetTouchJudge(timing);
                                th.isHeadJudged = true;
                                th.isHolding = true;
                                th.headDiff = timing;
                            }
                        }
                        else if (math.max(th.LastFor - timing, 0) > 0)
                        {
                            if (th.coverageId >= 0)
                                InputData.DJAutoAddGroupCoverage(ctx.TouchHoldGroupCoverResults[th.coverageId]);
                        }
                        else
                        {
                            th.holdPercent = 1f;
                            EndTouchHold(ref th, ctx);
                            continue;
                        }
                        break;
                }
            }
        }

        // Mirrors TouchHoldUpdateJob.EndNote (sim-side judging).
        void EndTouchHold(ref TouchHoldData th, in SimContext ctx)
        {
            th.isHolding = false;

            if (th.isBreak)
                NoteHelper.PlayTapSound(ctx.SfxRequests,
                    th.judgeGrade, true, th.isEx, false, th.headDiff);
            else
                NoteHelper.PlayTouchSound(ctx.SfxRequests,
                    th.judgeGrade, th.isMine, th.isHanabi);
            NoteHelper.PlayTouchEffect(ctx.JudgeEffectRequests,
                (int)th.sensor + 8,
                th.judgeGrade, th.isBreak, th.isHanabi, th.isMine);
            NoteHelper.ReportResult(ctx.ReportResults,
                th.judgeGrade, th.isBreak, SimaiNoteType.TouchHold);

            InputData.NextTouch(th.sensor);
            if (th.headGroupId != -1 && th.judgeGrade != JudgeGrade.Miss)
            {
                var judged = ctx.TouchGroupJudgedCounts;
                judged[th.headGroupId]++;
            }
            th.isEnd = true;
        }

        void UpdateSlides(float simTime, AutoPlayMode mode, in SimContext ctx)
        {
            for (int i = 0; i < ctx.Slides.Length; i++)
            {
                ref var slide = ref ctx.Slides.ElementRef(i);
                if (slide.isEnd || slide.isSlideEnd ||
                   (slide.isJudged && simTime > slide.judgeTime + InputManager.DJAUTO_SLIDE_RELEASE_DELAY_SEC)
                ) continue;
                var timing = simTime - slide.shootTime;
                // Sensor mode: the star fades in on the start sensor from tapTime, so the input
                // follows it - the hand sits on the start (first sensor) from the fade-in.
                // Button mode keeps its tap-guide delay.
                var autoplayStart = mode == AutoPlayMode.DJAutoButton && slide.hasTapGuide
                    ? InputManager.DJAUTO_SLIDE_TAP_GUIDE_DELAY_SEC // button-mode DJAuto tap-guide delay
                    : mode == AutoPlayMode.DJAutoSensor
                        ? math.min(0f, slide.tapTime - slide.shootTime)
                        : SIM_AUTOPLAY_START_SEC;
                if (timing < autoplayStart) continue;
                switch (mode)
                {
                    // Non-simulated modes let the star run to the end before the SlideOK shows
                    case AutoPlayMode.Enable:
                    case AutoPlayMode.Random:
                        {
                            if (slide.smoothSlideAnime)
                            {
                                // processIdx was already computed by RenderStar; reuse it
                                slide.eaten = slide.processIdx - 1;
                            }
                            else
                            {
                                // Section lengths vary a lot (conn slides worse), so a straight lerp looks bad;
                                // reuse judgeCurrent to track the current section
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
                                NoteHelper.PlaySlideSound(ctx.SfxRequests,
                                    slide.isBreak
                                );
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
                                    // start point isn't TooFast, for compat with regular slides
                                    var grade = (JudgeGrade)GlobalRandom.NextInt((int)JudgeGrade.FastGood, (int)JudgeGrade.Miss);
                                    slide.judgeGrade = slide.isMine
                                        ? (grade < JudgeGrade.FastPerfect3rd ? JudgeGrade.TooFast : JudgeGrade.LateCritical)
                                        : grade;
                                }
                                // non-simulated modes must set finishJudgeTiming themselves
                                slide.finishJudgeTiming = simTime;
                                FinishJudgeSlide(ref slide, simTime);
                                EndSlide(ref slide, ctx);
                                if (slide.isFolded) slide.isEnd = true;
                            }
                            break;
                        }
                    // Simulated modes wait for the star to fully end (isSlideEnd); isJudged means the hand isn't stuck here
                    case AutoPlayMode.DJAutoButton:
                    case AutoPlayMode.DJAutoSensor:
                        {
                            // Non-mine slides only need input
                            if (!slide.isMine)
                            {
                                // A folded slide shares the visible copy's trajectory: it only consumes the
                                // visible copy's input and never requests hands/expansion/merging.
                                if (slide.isFolded) break;

                                // The guide tap only delays DJAuto's input by a few frames without eating the
                                // slide's start trajectory; the input walks the path from its start while the
                                // on-screen star keeps its timeline.
                                var inputProcess = autoplayStart > 0
                                    ? math.saturate((timing - autoplayStart) / math.max(slide.LastFor, 0.001f))
                                    : math.saturate(timing / math.max(slide.LastFor, 0.001f));

                                if (!slide.isWifi)
                                {
                                    // The star position must be computed from simTime: the render job's per-frame
                                    // slide.starPos lags a full frame at low FPS, lagging the hand and dropping sections (Miss).
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
                                    // wifi uses the big hand
                                    var center = slide.starPosConstC * inputProcess + slide.starPosStart;
                                    var left = slide.starPosConstL * inputProcess + slide.starPosStart;
                                    var right = slide.starPosConstR * inputProcess + slide.starPosStart;
                                    InputData.DJAutoHandleWifiWorldPosition(
                                        (left + center) / 2,
                                        (right + center) / 2
                                    );
                                }

                                // Section-following is judged on the sim tick (tick precision, render-FPS independent)
                                CheckSlide(ref slide, simTime, ctx);
                                break;
                            }

                            // Mine slides advance programmatically and skip the DJAuto flow (else they'd hold a hand)
                            MineSlideAutoAdvance(ref slide);
                            CheckSlide(ref slide, simTime, ctx);
                            break;
                        }
                }
            }
        }

        /// <summary>
        /// DJAuto slide judging: mirrors SlideUpdateJob.CheckUpdate but runs on the sim tick
        /// with simTime (section advance/timeout/finish are no longer render-frame quantized).
        /// </summary>
        void CheckSlide(ref SlideData slide, float simTime, in SimContext ctx)
        {
            if (slide.isEnd || slide.isSlideEnd || slide.isJudged) return;

            // A slide's correct frame is when the star enters the last section, so judging is SV-affected
            var tapTiming = simTime - slide.tapTime;
            var timing = simTime - slide.shootTime;

            if (tapTiming < -NoteHelper.SLIDE_CHECK_AHEAD_TIME_MSEC / 1000f) return; // accept judging 100ms early
            var remaining = slide.LastFor - timing;

            // the star misses at end + 550ms
            var forceJudge = timing - slide.LastFor - NoteHelper.SLIDE_FORCE_MISS / 1000f;
            // mine stars perfect at the slide end
            // with mineAutoSlide on, sections up to the second-to-last are auto-eaten; touching the last one misses
            bool timeout = slide.isMine ? (simTime >= slide.judgeTiming) : (forceJudge >= 0);

            if (timeout)
            {
                slide.judgeGrade = slide.isMine
                    ? JudgeGrade.LateCritical
                    : (CanLeaveTailAsGood(slide) ? JudgeGrade.LateGood : JudgeGrade.Miss);
                // zero lastStayTime to skip the SlideOK display delay
                slide.lastStayTime = 0;
                FinishJudgeSlide(ref slide, simTime);
                return;
            }

            if (!slide.isWifi)
            {
                ProcessAreas(ref slide, slide.judgeQueue, slide.judgeQueueCount, ref slide.judgeCurrent, ref slide.currentOn, ctx);
            }
            else
            {
                ProcessAreas(ref slide, slide.judgeQueue, slide.judgeQueueCount, ref slide.judgeCurrent, ref slide.currentOn, ctx);
                ProcessAreas(ref slide, slide.judgeQueueL, slide.judgeQueueLCount, ref slide.judgeL_Current, ref slide.currentOnL, ctx);
                ProcessAreas(ref slide, slide.judgeQueueR, slide.judgeQueueRCount, ref slide.judgeR_Current, ref slide.currentOnR, ctx);
            }

            var newEaten = 0;
            if (!slide.isWifi)
            {
                if (slide.judgeCurrent >= slide.judgeQueueCount) // all sections done
                {
                    slide.judgeGrade = CalcSlideJudgeGrade(ref slide, simTime);
                    FinishJudgeSlide(ref slide, simTime);
                    return;
                }

                if (slide.currentOn >= SensorType.A1) // pressed
                {
                    newEaten = slide.judgeQueue[slide.judgeCurrent].ArrowProgressPush;
                }
                else if (slide.judgeCurrent > 0) // completed
                {
                    newEaten = slide.judgeQueue[slide.judgeCurrent - 1].ArrowProgressFinish;
                }
                else
                {
                    newEaten = 0; // nothing
                }
            }
            else
            {
                if (slide.judgeCurrent >= slide.judgeQueueCount &&
                    slide.judgeL_Current >= slide.judgeQueueLCount &&
                    slide.judgeR_Current >= slide.judgeQueueRCount)
                {
                    slide.judgeGrade = CalcSlideJudgeGrade(ref slide, simTime);
                    FinishJudgeSlide(ref slide, simTime);
                    return;
                }

                var eatenC = (slide.currentOn >= SensorType.A1)
                    ? slide.judgeQueue[slide.judgeCurrent].ArrowProgressPush
                    : (slide.judgeCurrent > 0)
                        ? slide.judgeQueue[slide.judgeCurrent - 1].ArrowProgressFinish
                        : 0;
                var eatenL = (slide.currentOnL >= SensorType.A1)
                    ? slide.judgeQueueL[slide.judgeL_Current].ArrowProgressPush
                    : (slide.judgeL_Current > 0)
                        ? slide.judgeQueueL[slide.judgeL_Current - 1].ArrowProgressFinish
                        : 0;
                var eatenR = (slide.currentOnR >= SensorType.A1)
                    ? slide.judgeQueueR[slide.judgeR_Current].ArrowProgressPush
                    : (slide.judgeR_Current > 0)
                        ? slide.judgeQueueR[slide.judgeR_Current - 1].ArrowProgressFinish
                        : 0;
                newEaten = math.min(eatenC, math.min(eatenL, eatenR));
            }

            // For mine slides: if the auto-advance passed ArrowProgressPush but the first section
            // was never pressed, newEaten would be smaller than slide.eaten
            if (newEaten > slide.eaten)
            {
                slide.eaten = newEaten;
            }
        }

        // Walks the area queue, updates sensor On/Off, advances the cursor (mirrors SlideUpdateJob.ProcessAreas)
        void ProcessAreas(ref SlideData slide, SlideArea* queue, int queueCount, ref int cur, ref SensorType currentOn, in SimContext ctx)
        {
            if (cur >= queueCount) return;

            var changed = false;
            do
            {
                changed = false;

                var first = queue[cur];
                var hasSecond = cur + 1 < queueCount;

                // check the current first section
                if (currentOn <= SensorType.Invalid)  // first section not yet pressed
                {
                    if (InputData.GetSensorState(first.SensorA).Status)
                    {
                        currentOn = first.SensorA;
                        changed = true;
                        if (!hasSecond) cur++;  // last section needs no release
                    }
                    else if (first.SensorB >= SensorType.A1 && InputData.GetSensorState(first.SensorB).Status)
                    {
                        currentOn = first.SensorB;
                        changed = true;
                        if (!hasSecond) cur++;  // last section needs no release
                    }
                }
                else // first section is pressed
                {
                    if (!InputData.GetSensorState(currentOn).Status)
                    {
                        currentOn = SensorType.Invalid;
                        changed = true;
                        cur++;
                    }
                }

                // then check the second section; skipping is always allowed once the first is held
                var skippable = (cur != slide.unskippable1 && cur != slide.unskippable2 || currentOn >= SensorType.A1);
                if (!changed && hasSecond && skippable)
                {
                    var second = queue[cur + 1];
                    var isSecondLast = cur + 2 >= queueCount;
                    var sensorState = InputData.GetSensorState(second.SensorA);
                    if (sensorState.Status || sensorState.IsPadUp)  // a sensor released this frame still counts as held when skipping
                    {
                        currentOn = second.SensorA;
                        changed = true;
                        cur++;
                        if (isSecondLast) cur++;  // last section needs no release
                    }
                    else if (second.SensorB >= SensorType.A1)
                    {
                        sensorState = InputData.GetSensorState(second.SensorB);
                        if (sensorState.Status || sensorState.IsPadUp)
                        {
                            currentOn = second.SensorB;
                            changed = true;
                            cur++;
                            if (isSecondLast) cur++;  // last section needs no release
                        }
                    }
                }

                if (changed && !slide.isSoundPlayed)
                {
                    NoteHelper.PlaySlideSound(ctx.SfxRequests,
                        slide.isBreak
                    );
                    slide.isSoundPlayed = true;
                }
            } while (changed && cur < queueCount);

            if (cur >= queueCount)
            {
                currentOn = SensorType.Invalid;
                cur = (byte)queueCount;
            }
        }

        /// <summary>
        /// Tail-green: a slide timeouting to Miss is promoted to LateGood when the remaining sections qualify.
        /// Slide: at most 1 section left.
        /// Wifi: at most 1 per branch. The middle branch's last section is a two-part OR;
        /// the arcade counts it as two (bug), so the middle branch can't go green until fully cleared.
        /// </summary>
        bool CanLeaveTailAsGood(SlideData slide)
        {
            if (!slide.isWifi)
            {
                return slide.judgeQueueCount - slide.judgeCurrent <= 1;
            }
            else
            {
                var cRemaining = slide.judgeQueueCount - slide.judgeCurrent;
                // the middle branch's last section is merged; the arcade counts it as two when checking: +1 if not cleared
                if (slide.judgeCurrent < slide.judgeQueueCount)
                    cRemaining += 1;
                return cRemaining <= 1
                    && slide.judgeQueueLCount - slide.judgeL_Current <= 1
                    && slide.judgeQueueRCount - slide.judgeR_Current <= 1;
            }
        }

        JudgeGrade CalcSlideJudgeGrade(ref SlideData slide, float simTime)
        {
            if (slide.isMine)
            {
                return JudgeGrade.TooFast;
            }

            var triggerTime = simTime;

            const float totalInterval = 36f / 60; // seconds
            const float nPInterval = 14f / 60; // base Perfect interval
            const float gr1Interval = 21f / 60;
            const float gr2Interval = 25f / 60;
            const float gr3Interval = 29f / 60;
            const float gdInterval = 36f / 60;

            var ext = slide.lastStayTime; // extra interval T
            var pInterval = math.min(nPInterval + ext / 4f, totalInterval); // total Perfect interval

            var diff = slide.judgeTiming - triggerTime;
            var isFast = diff > 0;
            diff = math.abs(diff);

            if (diff <= pInterval)
                return isFast ? JudgeGrade.FastCritical : JudgeGrade.LateCritical;
            if (diff <= gr1Interval)
                return isFast ? JudgeGrade.FastGreat1st : JudgeGrade.LateGreat1st;
            if (diff <= gr2Interval)
                return isFast ? JudgeGrade.FastGreat2nd : JudgeGrade.LateGreat2nd;
            if (diff <= gr3Interval)
                return isFast ? JudgeGrade.FastGreat3rd : JudgeGrade.LateGreat3rd;
            if (diff <= gdInterval)
                return isFast ? JudgeGrade.FastGood : JudgeGrade.LateGood;
            if (!isFast)
            {
                // past the natural late-good range: the arcade judges too-late-miss then promotes to late good;
                // zero lastStayTime to skip the SlideOK display delay
                slide.lastStayTime = 0;
                return JudgeGrade.LateGood;
            }
            // too fast good
            return JudgeGrade.FastGood;
        }

        /// <summary>Disable mode only advances mine slides (no hand input at all).</summary>
        void UpdateSlidesMine(float simTime, in SimContext ctx)
        {
            for (int i = 0; i < ctx.Slides.Length; i++)
            {
                ref var slide = ref ctx.Slides.ElementRef(i);
                if (slide.isEnd || slide.isSlideEnd ||
                   (slide.isJudged && simTime > slide.judgeTime + InputManager.DJAUTO_SLIDE_RELEASE_DELAY_SEC)
                ) continue;
                var timing = simTime - slide.shootTime;
                if (timing < 0) continue;
                if (!slide.isMine) continue;

                MineSlideAutoAdvance(ref slide);
            }
        }

        void MineSlideAutoAdvance(ref SlideData slide)
        {
            if (!slide.mineAutoSlide) return;

            // which section the cursor is on
            var idx = slide.judgeCurrent;
            if (slide.isWifi)
            {
                // wifi: use the branch with the most remaining
                idx = math.min(slide.judgeCurrent, math.min(slide.judgeL_Current, slide.judgeR_Current));
            }

            // leave the last section to the check
            if (idx >= slide.judgeQueueCount - 1) return;

            var newEaten = 0;
            // the three wifi queues share the same ArrowProgress
            if (slide.processIdx > slide.judgeQueue[idx].ArrowProgressFinish)
            {
                // advance a section once the guide star passed it
                newEaten = slide.judgeQueue[idx].ArrowProgressFinish;

                if (slide.isWifi)
                {
                    // wifi: check each branch separately
                    if (slide.judgeCurrent <= idx)
                    {
                        slide.judgeCurrent = idx + 1;
                        slide.currentOn = SensorType.Invalid;
                    }

                    if (slide.judgeL_Current <= idx)
                    {
                        slide.judgeL_Current = idx + 1;
                        slide.currentOnL = SensorType.Invalid;
                    }

                    if (slide.judgeR_Current <= idx)
                    {
                        slide.judgeR_Current = idx + 1;
                        slide.currentOnR = SensorType.Invalid;
                    }
                }
                else
                {
                    // regular slides always advance
                    slide.currentOn = SensorType.Invalid;
                    slide.judgeCurrent++;
                }
            }
            else if (slide.processIdx > slide.judgeQueue[idx].ArrowProgressPush)
            {
                newEaten = slide.judgeQueue[idx].ArrowProgressPush;
            }

            if (slide.smoothSlideAnime)
            {
                newEaten = slide.processIdx - 1;
            }

            if (newEaten > slide.eaten)
            {
                slide.eaten = newEaten;
            }
        }

        /// <summary>
        /// the star is judged (truth)
        /// </summary>
        void FinishJudgeSlide(ref SlideData slide, float simTime)
        {
            slide.judgeTime = simTime;
            slide.isJudged = true;
        }
        /// <summary>
        /// the star is presented as judged (visual)
        /// </summary>
        void EndSlide(ref SlideData slide, in SimContext ctx)
        {
            slide.isSlideEnd = true;
            NoteHelper.PlaySlideEndSound(ctx.SfxRequests,
                slide.judgeGrade,
                slide.isMine,
                slide.isBreak
            );
            NoteHelper.ReportResult(ctx.ReportResults,
                slide.judgeGrade,
                slide.isBreak,
                SimaiNoteType.Slide
            );
        }
    }
}
