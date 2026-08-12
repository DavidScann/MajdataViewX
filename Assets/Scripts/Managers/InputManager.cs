#nullable enable

using MajdataViewX.Base;
using MajdataViewX.Notes;
using MajdataViewX.Types.Input;
using MajdataViewX.Types.Rendering;
using MajdataViewX.Utils;
using MajdataViewX.Utils.Extensions;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using static MajdataViewX.Base.MajBurst;
using static MajdataViewX.Base.MajCtx;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace MajdataViewX.Managers
{
    public class InputManager
    {
        // Slide默认尺寸
        public const float DJAUTO_HAND_RADIUS = 0.45f;
        // Wide-hand mode slide hand size: closer to a real palm, more likely to sweep adjacent sensors (realistic DJAuto).
        public const float DJAUTO_WIDE_HAND_RADIUS = 0.70f;
        // Wifi默认尺寸
        public const float DJAUTO_WIFI_RADIUS = 1.00f;
        // Touch/TouchHold 覆盖圆的最小指尖尺寸；需要更少误触时可单独调小。
        public const float DJAUTO_TOUCH_COVER_MIN_RADIUS = 0.45f;
        // 所有 DJAuto 手势复用时允许扩大的最大半径。
        public const float DJAUTO_HAND_MAX_RADIUS = 1.80f;

        /// <summary>Wide-hand mode toggle (set by PlayManager from ViewSetting).</summary>
        public static bool WideHands;

        public const float DJAUTO_TOUCH_DOUBLE_CIRCLE_SLIDE_START_SEC = -2 * FRAME_LENGTH_SEC;
        public const float DJAUTO_SLIDE_TAP_GUIDE_DELAY_SEC = 3 * FRAME_LENGTH_SEC;

        public const float DJAUTO_SLIDE_RELEASE_DELAY_SEC = 6 * FRAME_LENGTH_SEC;

        public const float BUTTON_HIT_RENDER_RADIUS = 0.4f;

        public bool ShowHand
        {
            get => InputData.ShowHand;
            set => InputData.ShowHand = value;
        }
        RenderGroup<HitRenderData> _hitGroup;
        bool _isHitGroupLocked;

        public InputManager()
        {
            _inputManager = this;
            //get sensor positions
            for (var i = 0; i < SENSOR_COUNT; i++)
            {
                InputData.SensorWorldPositions[i] = MajPos.GetSensorWorldPos((SensorType)i);
            }
            //REMEMBER TO FORCE INCLUDE
            var matHit = new Material(Shader.Find("Custom/Hit"));
            var hitMesh = MeshGenerator.CreateCircleMesh(8, 1f, true);
            _hitGroup = new(matHit, hitMesh, 6); // priority larger than notes
        }

        public unsafe void BeginHandler()
        {
            // UPDATE MUST BE EARLIER THAN NoteManager's UPDATE!!
            // (set in Script Execution Order)
            _isHitGroupLocked = ShowHand;
            if (_isHitGroupLocked)
            {
                _hitGroup.AdvanceWrite();
                var hitRender = _hitGroup.LockForWrite();
                _hitGroup.ResetCount();

                InputData.hitRender = (HitRenderData*)hitRender.GetUnsafePtr();
                InputData.HitWriteCountPtr = _hitGroup.WriteCountPtr;
            }
            InputData.BeginHandler(_isHitGroupLocked);

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                CheckButton(keyboard);
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.isPressed)
                {
                    CheckScreenPos(mouse.position.ReadValue());
                }
            }

            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                foreach (var touch in touchscreen.touches)
                {
                    var phase = touch.phase.ReadValue();
                    if (phase == TouchPhase.None) continue;
                    if (phase is TouchPhase.Began or TouchPhase.Moved or TouchPhase.Stationary)
                        CheckScreenPos(touch.position.ReadValue());
                }
            }
        }

        // wait for slide and other notes finish update
        public void EndHandler()
        {
            InputData.EndHandler();
            if (_isHitGroupLocked)
            {
                _hitGroup.UnlockWrite();
                _hitGroup.Render();
                _hitGroup.Swap();
                _isHitGroupLocked = false;
            }
        }

        private void CheckButton(Keyboard keyboard)
        {
            InputData.HandleButtonInput(SensorType.A1, keyboard[Key.W].isPressed);
            InputData.HandleButtonInput(SensorType.A2, keyboard[Key.E].isPressed);
            InputData.HandleButtonInput(SensorType.A3, keyboard[Key.D].isPressed);
            InputData.HandleButtonInput(SensorType.A4, keyboard[Key.C].isPressed);
            InputData.HandleButtonInput(SensorType.A5, keyboard[Key.X].isPressed);
            InputData.HandleButtonInput(SensorType.A6, keyboard[Key.Z].isPressed);
            InputData.HandleButtonInput(SensorType.A7, keyboard[Key.A].isPressed);
            InputData.HandleButtonInput(SensorType.A8, keyboard[Key.Q].isPressed);
        }
        private void CheckScreenPos(Vector2 screenPos)
        {
            var mainCamera = Camera.main;
            var pos = (Vector2)mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f));

            InputData.HandleWorldPosInput(pos);
        }



        public void ResetState()
        {
        }

        public void OnDestroy()
        {
            _hitGroup?.Dispose();
        }
    }

    internal enum DJAutoHandVisualKind : byte
    {
        None,
        Coverage,
        WorldHit
    }

    /// <summary>
    /// One hand requested per DJAuto tick.
    /// VisualIndex semantics depend on VisualKind: the ActiveCoverages index for Coverage,
    /// the actual hitRender slot for WorldHit.
    /// </summary>
    internal struct DJAutoHandData
    {
        public Circle Circle;
        public DJAutoHandVisualKind VisualKind;
        public int VisualIndex;
    }

    [BurstCompile]
    public unsafe struct InputDataB
    {
        public bool ShowHand;
        bool _showHandThisFrame;

        public NativeArray<float2> SensorWorldPositions;

        NativeArray<SensorState> _buttonStates;
        NativeArray<SensorState> _sensorStates;
        NativeArray<int> _nextButtonIndex;
        NativeArray<int> _nextSensorIndex;
        NativeArray<int> _nextButtonIndexNextFrame;
        NativeArray<int> _nextSensorIndexNextFrame;

        const int DJAUTO_MAX_CONCURRENT_INPUTS = 2;
        // per-tick world-hit budget (hand slots); actual writes are bounded by the render group capacity.
        const int DJAUTO_WORLD_HIT_CAPACITY = 32;
        int _djAutoInputCount;
        NativeArray<DJAutoHandData> _djAutoHandsThisTick;
        int _djAutoHandsWriteLock;

        public NativeArray<CoverResult> ActiveCoverages;
        [NativeDisableUnsafePtrRestriction]
        public int* ActiveCoveragesCountPtr;

        int _djAutoWorldHitCount;

        [NativeDisableUnsafePtrRestriction]
        public HitRenderData* hitRender;
        [NativeDisableUnsafePtrRestriction]
        public int* HitWriteCountPtr;

        public void Init()
        {
            SensorWorldPositions = new(SENSOR_COUNT, Allocator.Persistent);

            _buttonStates = new(BUTTON_COUNT, Allocator.Persistent);
            _sensorStates = new(SENSOR_COUNT, Allocator.Persistent);
            _nextButtonIndex = new(BUTTON_COUNT, Allocator.Persistent);
            _nextSensorIndex = new(SENSOR_COUNT, Allocator.Persistent);
            _nextButtonIndexNextFrame = new(BUTTON_COUNT, Allocator.Persistent);
            _nextSensorIndexNextFrame = new(SENSOR_COUNT, Allocator.Persistent);
            _djAutoHandsThisTick = new(DJAUTO_MAX_CONCURRENT_INPUTS, Allocator.Persistent);

            for (var i = 0; i < BUTTON_COUNT; i++)
                _buttonStates[i] = new();
            for (var i = 0; i < SENSOR_COUNT; i++)
                _sensorStates[i] = new();

            ActiveCoverages = new(32, Allocator.Persistent);
            ActiveCoveragesCountPtr = (int*)UnsafeUtility.Malloc(sizeof(int), 4, Allocator.Persistent);
            *ActiveCoveragesCountPtr = 0;
        }






        // ==========button/sensor management==========
        // DJAutoSim writes DJAuto input directly at a fixed step (sharing the same edge semantics as user input).

        public readonly SensorState GetButtonState(SensorType type) => _buttonStates[(int)type];
        public readonly SensorState GetSensorState(SensorType type) => _sensorStates[(int)type];


        // ======DJAuto Part======
        // DJAuto writes must happen after BeginHandler and before the render jobs (ordered by NoteManager.Update).

        /// <summary>
        /// DJAuto按键处理Tap/Hold
        /// </summary>
        public bool DJAutoSetButtonOn(SensorType type)
        {
            var hand = new Circle
            {
                Center = MajPos.GetBtnPos((int)type),
                Radius = InputManager.DJAUTO_HAND_RADIUS
            };
            if (!TryRequestDJAutoHand(hand, DJAutoHandVisualKind.None, out _)) return false;

            SetThisFrameButtonOn(type);
            return true;
        }
        /// <summary>
        /// DJAuto判定区处理Tap/Hold
        /// </summary>
        public bool DJAutoSetSensorOn(SensorType type)
        {
            var hand = new Circle
            {
                Center = SensorWorldPositions[(int)type],
                Radius = InputManager.DJAUTO_HAND_RADIUS
            };
            if (!TryRequestDJAutoHand(hand, DJAutoHandVisualKind.None, out _)) return false;

            SetThisFrameSensorOn(type);
            return true;
        }
        /// <summary>
        /// Unconditional DJAuto press: perfect-player semantics - press any free sensor without the
        /// two-hand allocation model. Used for momentary tap/hold-head presses (the hand model only
        /// constrains continuous inputs: slides/touches).
        /// </summary>
        public void DJAutoPressButton(SensorType type) => SetThisFrameButtonOn(type);
        public void DJAutoPressSensor(SensorType type) => SetThisFrameSensorOn(type);
        /// <summary>
        /// DJAuto处理Touch/TouchHold（寻找大手圆）
        /// </summary>
        public void DJAutoAddGroupCoverage(CoverResult cover, float timing = 0f)
        {
            if (cover.Mode == CoverMode.None) return;

            if (cover.Mode == CoverMode.DoubleCircleSlide)
            {
                // 从 -2 帧提前起手落下两指，再用后半段 Perfect 窗口（12 帧，即 0.2 秒）完成滑动。
                // 这也是全屏扫动可接受的速度上限。
                float slideStart = InputManager.DJAUTO_TOUCH_DOUBLE_CIRCLE_SLIDE_START_SEC;
                float slideDuration = NoteHelper.TOUCH_JUDGE_SEG_3RD_PERFECT_MSEC / 1000f;
                float progress = math.saturate((timing - slideStart) / slideDuration);
                cover.Circle1.Center = math.lerp(cover.Circle1.Center, cover.Circle1End, progress);
                cover.Circle2.Center = math.lerp(cover.Circle2.Center, cover.Circle2End, progress);
            }

            // Perfect-player semantics: touch coverage bypasses the two-hand model - panel fingers
            // don't conflict with ring hands, so the cover always lands. The two-hand model only
            // constrains the star-following/continuous inputs.
            if (InputManager.WideHands)
            {
                cover.Circle1.Radius = math.max(cover.Circle1.Radius, InputManager.DJAUTO_WIDE_HAND_RADIUS);
                cover.Circle2.Radius = math.max(cover.Circle2.Radius, InputManager.DJAUTO_WIDE_HAND_RADIUS);
            }
            SetSensorsFromMask(GetSensorMask(cover.Circle1));
            if (cover.Mode is CoverMode.DoubleCircleDirect or
                CoverMode.DoubleCircleGroup or
                CoverMode.DoubleCircleSlide)
            {
                SetSensorsFromMask(GetSensorMask(cover.Circle2));
            }
        }

        private bool TryRequestDJAutoHand(
            Circle requestedCircle,
            DJAutoHandVisualKind visualKind,
            out int assignedHandIndex,
            int excludedHandIndex = -1)
        {
            while (Interlocked.CompareExchange(ref _djAutoHandsWriteLock, 1, 0) != 0)
            {
            }

            // try/finally: an exception can never wedge the lock (or the main thread would spin forever on the next request).
            try
            {
                assignedHandIndex = -1;
                ulong requestedSensors = GetSensorMask(requestedCircle);
                bool accepted = false;

                // 已经覆盖目标时直接共用。
                for (int handIndex = 0; handIndex < _djAutoInputCount; handIndex++)
                {
                    if (handIndex == excludedHandIndex) continue;

                    Circle existingCircle = _djAutoHandsThisTick[handIndex].Circle;
                    if (requestedSensors != 0)
                    {
                        ulong existingSensors = GetSensorMask(existingCircle);
                        if ((existingSensors & requestedSensors) == requestedSensors)
                        {
                            accepted = true;
                            assignedHandIndex = handIndex;
                            break;
                        }
                    }
                    else
                    {
                        float containRadius = math.distance(existingCircle.Center, requestedCircle.Center) +
                                              requestedCircle.Radius;
                        if (containRadius <= existingCircle.Radius + 1e-4f)
                        {
                            accepted = true;
                            assignedHandIndex = handIndex;
                            break;
                        }
                    }
                }

                // 没有现成覆盖时优先申请空闲手。
                if (!accepted && _djAutoInputCount < DJAUTO_MAX_CONCURRENT_INPUTS)
                {
                    int visualIndex = -1;
                    bool visualAvailable = true;
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
                    else if (visualKind == DJAutoHandVisualKind.WorldHit && _showHandThisFrame)
                    {
                        var count = _djAutoWorldHitCount;
                        visualAvailable = count < DJAUTO_WORLD_HIT_CAPACITY;
                        if (visualAvailable)
                        {
                            var idx = Interlocked.Increment(ref *HitWriteCountPtr) - 1;
                            _djAutoWorldHitCount = count + 1;
                            visualIndex = idx;
                            hitRender[idx] = new HitRenderData
                            {
                                pos = requestedCircle.Center,
                                radius = requestedCircle.Radius,
                                color = new float4(1, 0, 0, 0.75f)
                            };
                        }
                    }

                    if (visualAvailable)
                    {
                        assignedHandIndex = _djAutoInputCount;
                        _djAutoHandsThisTick[_djAutoInputCount++] = new DJAutoHandData
                        {
                            Circle = requestedCircle,
                            VisualKind = visualKind,
                            VisualIndex = visualIndex
                        };
                        accepted = true;
                    }
                }

                // 两只手都占用后，再尝试扩大已有的手。
                if (!accepted)
                    accepted = TryExpandDJAutoHand(
                        requestedCircle,
                        requestedSensors,
                        excludedHandIndex,
                        out assignedHandIndex);

                return accepted;
            }
            finally
            {
                Interlocked.Exchange(ref _djAutoHandsWriteLock, 0);
            }
        }

        private bool TryExpandDJAutoHand(
            Circle requestedCircle,
            ulong requestedSensors,
            int excludedHandIndex,
            out int assignedHandIndex)
        {
            assignedHandIndex = -1;
            int bestHandIndex = -1;
            int bestAddedSensorCount = int.MaxValue;
            float bestRadiusGrowth = float.MaxValue;
            float bestRadius = 0f;

            for (int handIndex = 0; handIndex < _djAutoInputCount; handIndex++)
            {
                if (handIndex == excludedHandIndex) continue;

                Circle oldCircle = _djAutoHandsThisTick[handIndex].Circle;
                float expandedRadius = 0f;
                if (requestedSensors != 0)
                {
                    for (int sensorIndex = 0; sensorIndex < SENSOR_COUNT; sensorIndex++)
                    {
                        if ((requestedSensors & (1ul << sensorIndex)) == 0) continue;

                        float distance = math.distance(oldCircle.Center, SensorWorldPositions[sensorIndex]);
                        float sensorRadius = MajPos.GetSensorRadius((SensorType)sensorIndex);
                        expandedRadius = math.max(expandedRadius, math.max(0f, distance - sensorRadius));
                    }
                }
                else
                {
                    expandedRadius = math.distance(oldCircle.Center, requestedCircle.Center) +
                                     requestedCircle.Radius;
                }

                expandedRadius = math.max(expandedRadius, oldCircle.Radius);
                if (expandedRadius > InputManager.DJAUTO_HAND_MAX_RADIUS + 1e-4f)
                    continue;

                Circle expandedCircle = oldCircle;
                expandedCircle.Radius = expandedRadius;
                ulong oldSensors = GetSensorMask(oldCircle);
                ulong expandedSensors = GetSensorMask(expandedCircle);
                if (requestedSensors != 0 &&
                    (expandedSensors & requestedSensors) != requestedSensors)
                    continue;

                int addedSensorCount = math.countbits(expandedSensors & ~oldSensors);
                float radiusGrowth = expandedRadius - oldCircle.Radius;
                if (addedSensorCount > bestAddedSensorCount ||
                    (addedSensorCount == bestAddedSensorCount && radiusGrowth >= bestRadiusGrowth))
                    continue;

                bestHandIndex = handIndex;
                bestAddedSensorCount = addedSensorCount;
                bestRadiusGrowth = radiusGrowth;
                bestRadius = expandedRadius;
            }

            if (bestHandIndex < 0) return false;

            DJAutoHandData hand = _djAutoHandsThisTick[bestHandIndex];
            Circle oldBestCircle = hand.Circle;
            hand.Circle.Radius = bestRadius;
            _djAutoHandsThisTick[bestHandIndex] = hand;

            if (hand.VisualIndex >= 0 && hand.VisualKind == DJAutoHandVisualKind.Coverage)
            {
                CoverResult cover = ActiveCoverages[hand.VisualIndex];
                cover.Circle1 = hand.Circle;
                ActiveCoverages[hand.VisualIndex] = cover;
            }
            else if (hand.VisualIndex >= 0 && hand.VisualKind == DJAutoHandVisualKind.WorldHit)
            {
                hitRender[hand.VisualIndex] = new HitRenderData
                {
                    pos = hand.Circle.Center,
                    radius = hand.Circle.Radius,
                    color = new float4(1, 0, 0, 0.75f)
                };
            }

            ulong newlyCoveredSensors = GetSensorMask(hand.Circle) & ~GetSensorMask(oldBestCircle);
            SetSensorsFromMask(newlyCoveredSensors);
            assignedHandIndex = bestHandIndex;
            return true;
        }

        private ulong GetSensorMask(Circle circle)
        {
            ulong mask = 0;
            for (int sensorIndex = 0; sensorIndex < SENSOR_COUNT; sensorIndex++)
            {
                ref readonly var sensorPos = ref SensorWorldPositions.ElementRef(sensorIndex);
                float combinedRadius = circle.Radius + MajPos.GetSensorRadius((SensorType)sensorIndex);
                if (math.distancesq(sensorPos, circle.Center) <=
                    combinedRadius * combinedRadius + 1e-4f)
                {
                    mask |= 1ul << sensorIndex;
                }
            }
            return mask;
        }

        private void SetSensorsFromMask(ulong sensorMask)
        {
            for (int sensorIndex = 0; sensorIndex < SENSOR_COUNT; sensorIndex++)
            {
                if ((sensorMask & (1ul << sensorIndex)) != 0)
                    SetThisFrameSensorOn((SensorType)sensorIndex);
            }
        }

        /// <summary>
        /// DJAuto处理星星
        /// </summary>
        public void DJAutoHandleWorldPosition(in float2 pos, float radius = InputManager.DJAUTO_HAND_RADIUS)
        {
            if (InputManager.WideHands)
                radius = math.max(radius, InputManager.DJAUTO_WIDE_HAND_RADIUS);
            var hand = new Circle { Center = pos, Radius = radius };
            if (TryRequestDJAutoHand(hand, DJAutoHandVisualKind.WorldHit, out _))
                SetSensorsFromMask(GetSensorMask(hand));
        }
        /// <summary>
        /// DJAuto处理wifi星星
        /// </summary>
        public void DJAutoHandleWifiWorldPosition(in float2 leftPos, in float2 rightPos)
        {
            var leftHand = new Circle { Center = leftPos, Radius = InputManager.DJAUTO_WIFI_RADIUS };
            int leftHandIndex = -1;
            if (TryRequestDJAutoHand(
                leftHand,
                DJAutoHandVisualKind.WorldHit,
                out leftHandIndex))
            {
                SetSensorsFromMask(GetSensorMask(leftHand));
            }

            var rightHand = new Circle { Center = rightPos, Radius = InputManager.DJAUTO_WIFI_RADIUS };
            if (TryRequestDJAutoHand(
                rightHand,
                DJAutoHandVisualKind.WorldHit,
                out _,
                leftHandIndex))
            {
                SetSensorsFromMask(GetSensorMask(rightHand));
            }
        }



        // ======User Input Part======

        public void BeginHandler(bool showHandThisFrame)
        {
            _showHandThisFrame = showHandThisFrame;

            // Must run after _prevChain.Complete(), before the DJAutoSim ticks and the render
            // jobs; user input is read below. DJAuto input no longer uses the next-frame buffer:
            // DJAutoSim writes the current state at a fixed step after BeginHandler; only the
            // per-frame edge advance (for IsPadDown) happens here.
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

        /// <summary>
        /// Called by DJAutoSim per tick: reset hand/coverage data so the tick starts clean.
        /// </summary>
        public void BeginSimTick()
        {
            _djAutoInputCount = 0;
            *ActiveCoveragesCountPtr = 0;
            _djAutoWorldHitCount = 0;
        }

        /// <summary>
        /// 处理按键输入
        /// </summary>
        public void HandleButtonInput(SensorType type, bool status)
        {
            if (!status) return;

            SetThisFrameButtonOn(type);
        }
        /// <summary>
        /// 处理世界坐标（手）输入
        /// </summary>
        public void HandleWorldPosInput(in float2 pos, float radius = InputManager.DJAUTO_HAND_RADIUS)
        {
            for (int i = 0; i < SensorWorldPositions.Length; i++)
            {
                var combinedR = radius + MajPos.GetSensorRadius((SensorType)i);
                var combinedSq = combinedR * combinedR;
                ref readonly var sp = ref SensorWorldPositions.ElementRef(i);
                var dx = pos.x - sp.x;
                var dy = pos.y - sp.y;
                var distSq = dx * dx + dy * dy;

                if (distSq <= combinedSq)
                    SetThisFrameSensorOn((SensorType)i);
            }

            if (_showHandThisFrame) // 本帧没有锁定渲染缓冲时不能写入指针
            {
                var hit = new HitRenderData
                {
                    pos = pos,
                    radius = radius,
                    color = new float4(1, 0, 0, 0.75f)
                };

                var idx = Interlocked.Increment(ref *HitWriteCountPtr) - 1;
                hitRender[idx] = hit;
            }
        }

        private void SetThisFrameButtonOn(SensorType type)
        {
            ref var button = ref _buttonStates.ElementRef((int)type);
            Interlocked.Increment(ref button.ActiveDown);
        }
        private void SetThisFrameSensorOn(SensorType type)
        {
            ref var sensor = ref _sensorStates.ElementRef((int)type);
            Interlocked.Increment(ref sensor.ActiveDown);
        }

        public void EndHandler()
        {
            if (_showHandThisFrame)
            {
                for (int i = 0; i < BUTTON_COUNT; i++)
                {
                    if (_buttonStates[i].Status)
                    {
                        var idx = Interlocked.Increment(ref *HitWriteCountPtr) - 1;
                        hitRender[idx] = new HitRenderData
                        {
                            pos = MajPos.GetBtnPos(i),
                            radius = InputManager.BUTTON_HIT_RENDER_RADIUS,
                            color = new float4(0, 1, 1, 0.5f) // Cyan responsive color
                        };
                    }
                }
                for (int i = 0; i < SENSOR_COUNT; i++)
                {
                    if (_sensorStates[i].Status)
                    {
                        var idx = Interlocked.Increment(ref *HitWriteCountPtr) - 1;
                        hitRender[idx] = new HitRenderData
                        {
                            pos = SensorWorldPositions[i],
                            radius = MajPos.GetSensorRadius((SensorType)i),
                            color = new float4(0, 1, 1, 0.5f) // Cyan responsive color
                        };
                    }
                }

                for (int i = 0; i < math.min(*ActiveCoveragesCountPtr, ActiveCoverages.Length); i++)
                {
                    var cover = ActiveCoverages[i];
                    var idx1 = Interlocked.Increment(ref *HitWriteCountPtr) - 1;
                    hitRender[idx1] = new HitRenderData
                    {
                        pos = cover.Circle1.Center,
                        radius = cover.Circle1.Radius,
                        color = new float4(0.5f, 1f, 0.5f, 0.6f) // Light green
                    };

                    if (cover.Mode == CoverMode.DoubleCircleDirect || cover.Mode == CoverMode.DoubleCircleGroup || cover.Mode == CoverMode.DoubleCircleSlide)
                    {
                        var idx2 = Interlocked.Increment(ref *HitWriteCountPtr) - 1;
                        hitRender[idx2] = new HitRenderData
                        {
                            pos = cover.Circle2.Center,
                            radius = cover.Circle2.Radius,
                            color = new float4(0.5f, 1f, 0.5f, 0.6f)
                        };
                    }
                }
            }
        }


        // ==========judge management==========
        public readonly void NextTapHold(SensorType pos)
        {
            Interlocked.Increment(ref _nextButtonIndexNextFrame.ElementRef((int)pos));
            Interlocked.Increment(ref _nextSensorIndexNextFrame.ElementRef((int)pos));
        }
        public readonly void NextTouch(SensorType pos)
        {
            Interlocked.Increment(ref _nextSensorIndexNextFrame.ElementRef((int)pos));
        }
        public readonly bool CanJudgeButton(SensorType pos, int order)
        {
            return order == _nextButtonIndex[(int)pos];
        }
        public readonly bool CanJudgeSensor(SensorType pos, int order)
        {
            return order == _nextSensorIndex[(int)pos];
        }


        public readonly void ApplyNextIndices()
        {
            for (int i = 0; i < BUTTON_COUNT; i++)
            {
                _nextButtonIndex.ElementRef(i) = _nextButtonIndexNextFrame[i];
            }
            for (int i = 0; i < SENSOR_COUNT; i++)
            {
                _nextSensorIndex.ElementRef(i) = _nextSensorIndexNextFrame[i];
            }
        }



        public void ResetState()
        {
            _djAutoInputCount = 0;
            _djAutoHandsWriteLock = 0;
            *ActiveCoveragesCountPtr = 0;
            _djAutoWorldHitCount = 0;

            for (var i = 0; i < BUTTON_COUNT; i++)
            {
                _buttonStates[i] = default;
                _nextButtonIndex[i] = 0;
                _nextButtonIndexNextFrame[i] = 0;
            }
            for (var i = 0; i < SENSOR_COUNT; i++)
            {
                _sensorStates[i] = default;
                _nextSensorIndex[i] = 0;
                _nextSensorIndexNextFrame[i] = 0;
            }
        }

        public void Dispose()
        {
            if (SensorWorldPositions.IsCreated) SensorWorldPositions.Dispose();
            if (_sensorStates.IsCreated) _sensorStates.Dispose();
            if (_nextSensorIndex.IsCreated) _nextSensorIndex.Dispose();
            if (_nextSensorIndexNextFrame.IsCreated) _nextSensorIndexNextFrame.Dispose();
            if (_buttonStates.IsCreated) _buttonStates.Dispose();
            if (_nextButtonIndex.IsCreated) _nextButtonIndex.Dispose();
            if (_nextButtonIndexNextFrame.IsCreated) _nextButtonIndexNextFrame.Dispose();

            if (_djAutoHandsThisTick.IsCreated) _djAutoHandsThisTick.Dispose();
            if (ActiveCoverages.IsCreated) ActiveCoverages.Dispose();
            if (ActiveCoveragesCountPtr != null) UnsafeUtility.Free(ActiveCoveragesCountPtr, Allocator.Persistent);
        }
    }

    public struct SensorState
    {
        public readonly bool Status => ActiveDown > 0;
        public readonly bool IsPadDown => LastActiveDown <= 0 && ActiveDown > 0;
        public readonly bool IsPadUp => LastActiveDown > 0 && ActiveDown <= 0;

        public int ActiveDown;
        public int LastActiveDown;
    }
}
