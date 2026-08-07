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
        // Wifi默认尺寸
        public const float DJAUTO_WIFI_RADIUS = 1.00f;
        // Touch/TouchHold 覆盖圆的最小指尖尺寸；需要更少误触时可单独调小。
        public const float DJAUTO_TOUCH_COVER_MIN_RADIUS = 0.45f;
        // 所有 DJAuto 手势复用时允许扩大的最大半径。
        public const float DJAUTO_HAND_MAX_RADIUS = 1.80f;

        // mutable, depends on fps(djauto changes apply in next frame)
        private struct DJAutoAutoplayStartSecKey { }
        public static readonly SharedStatic<float> DJAUTO_AUTOPLAY_START_SEC_SS = SharedStatic<float>.GetOrCreate<InputManager, DJAutoAutoplayStartSecKey>();
        public static float DJAUTO_AUTOPLAY_START_SEC => DJAUTO_AUTOPLAY_START_SEC_SS.Data;
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
            DJAUTO_AUTOPLAY_START_SEC_SS.Data = -0.013f;
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
        NativeArray<int> _buttonActiveDownNextFrame;
        NativeArray<int> _sensorActiveDownNextFrame;
        NativeArray<int> _nextButtonIndex;
        NativeArray<int> _nextSensorIndex;
        NativeArray<int> _nextButtonIndexNextFrame;
        NativeArray<int> _nextSensorIndexNextFrame;

        const int DJAUTO_MAX_CONCURRENT_INPUTS = 2;
        int _djAutoInputCount;
        NativeArray<DJAutoHandData> _djAutoHandsNextFrame;
        int _djAutoHandsWriteLock;

        public NativeArray<CoverResult> ActiveCoverages;
        [NativeDisableUnsafePtrRestriction]
        public int* ActiveCoveragesCountPtr;

        NativeArray<CoverResult> _activeCoveragesNextFrame;
        int _activeCoveragesNextFrameCount;

        NativeArray<HitRenderData> _worldPosHitsNextFrame;
        int _worldPosHitsNextFrameCount;

        [NativeDisableUnsafePtrRestriction]
        public HitRenderData* hitRender;
        [NativeDisableUnsafePtrRestriction]
        public int* HitWriteCountPtr;

        public void Init()
        {
            SensorWorldPositions = new(SENSOR_COUNT, Allocator.Persistent);

            _buttonStates = new(BUTTON_COUNT, Allocator.Persistent);
            _sensorStates = new(SENSOR_COUNT, Allocator.Persistent);
            _buttonActiveDownNextFrame = new(BUTTON_COUNT, Allocator.Persistent);
            _sensorActiveDownNextFrame = new(SENSOR_COUNT, Allocator.Persistent);
            _nextButtonIndex = new(BUTTON_COUNT, Allocator.Persistent);
            _nextSensorIndex = new(SENSOR_COUNT, Allocator.Persistent);
            _nextButtonIndexNextFrame = new(BUTTON_COUNT, Allocator.Persistent);
            _nextSensorIndexNextFrame = new(SENSOR_COUNT, Allocator.Persistent);
            _djAutoHandsNextFrame = new(DJAUTO_MAX_CONCURRENT_INPUTS, Allocator.Persistent);

            for (var i = 0; i < BUTTON_COUNT; i++)
                _buttonStates[i] = new();
            for (var i = 0; i < SENSOR_COUNT; i++)
                _sensorStates[i] = new();

            ActiveCoverages = new(32, Allocator.Persistent);
            _activeCoveragesNextFrame = new(32, Allocator.Persistent);
            _worldPosHitsNextFrame = new(32, Allocator.Persistent);
            ActiveCoveragesCountPtr = (int*)UnsafeUtility.Malloc(sizeof(int), 4, Allocator.Persistent);
            *ActiveCoveragesCountPtr = 0;
        }






        // ==========button/sensor management==========
        // 上帧 DJAuto 缓冲 -> 本帧状态 -> 叠加用户输入 -> 判定 -> DJAuto 写入下帧缓冲

        public readonly SensorState GetButtonState(SensorType type) => _buttonStates[(int)type];
        public readonly SensorState GetSensorState(SensorType type) => _sensorStates[(int)type];


        // ======DJAuto Part======
        // DJAuto部分的state写入都会被移到下一帧开头
        // 避免因为update顺序导致的读取问题

        /// <summary>
        /// DJAuto按键处理Tap/Hold
        /// </summary>
        public void DJAutoSetButtonOn(SensorType type)
        {
            var hand = new Circle
            {
                Center = MajPos.GetBtnPos((int)type),
                Radius = InputManager.DJAUTO_HAND_RADIUS
            };
            if (!TryRequestDJAutoHand(hand, DJAutoHandVisualKind.None, out _)) return;

            SetNextFrameButtonOn(type);
        }
        /// <summary>
        /// DJAuto判定区处理Tap/Hold
        /// </summary>
        public void DJAutoSetSensorOn(SensorType type)
        {
            var hand = new Circle
            {
                Center = SensorWorldPositions[(int)type],
                Radius = InputManager.DJAUTO_HAND_RADIUS
            };
            if (!TryRequestDJAutoHand(hand, DJAutoHandVisualKind.None, out _)) return;

            SetNextFrameSensorOn(type);
        }
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

            int firstHandIndex = -1;
            if (TryRequestDJAutoHand(
                cover.Circle1,
                DJAutoHandVisualKind.Coverage,
                out firstHandIndex))
                SetSensorsFromMask(GetSensorMask(cover.Circle1));

            if (cover.Mode is CoverMode.DoubleCircleDirect or
                CoverMode.DoubleCircleGroup or
                CoverMode.DoubleCircleSlide)
            {
                if (TryRequestDJAutoHand(
                    cover.Circle2,
                    DJAutoHandVisualKind.Coverage,
                    out _,
                    firstHandIndex))
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

            assignedHandIndex = -1;
            ulong requestedSensors = GetSensorMask(requestedCircle);
            bool accepted = false;

            // 已经覆盖目标时直接共用。
            for (int handIndex = 0; handIndex < _djAutoInputCount; handIndex++)
            {
                if (handIndex == excludedHandIndex) continue;

                Circle existingCircle = _djAutoHandsNextFrame[handIndex].Circle;
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
                    visualIndex = _activeCoveragesNextFrameCount;
                    visualAvailable = visualIndex < _activeCoveragesNextFrame.Length;
                    if (visualAvailable)
                    {
                        _activeCoveragesNextFrameCount++;
                        _activeCoveragesNextFrame[visualIndex] = new CoverResult
                        {
                            Mode = CoverMode.SingleCircleDirect,
                            Circle1 = requestedCircle
                        };
                    }
                }
                else if (visualKind == DJAutoHandVisualKind.WorldHit && _showHandThisFrame)
                {
                    visualIndex = _worldPosHitsNextFrameCount;
                    visualAvailable = visualIndex < _worldPosHitsNextFrame.Length;
                    if (visualAvailable)
                    {
                        _worldPosHitsNextFrameCount++;
                        _worldPosHitsNextFrame[visualIndex] = new HitRenderData
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
                    _djAutoHandsNextFrame[_djAutoInputCount++] = new DJAutoHandData
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

            Interlocked.Exchange(ref _djAutoHandsWriteLock, 0);
            return accepted;
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

                Circle oldCircle = _djAutoHandsNextFrame[handIndex].Circle;
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

            DJAutoHandData hand = _djAutoHandsNextFrame[bestHandIndex];
            Circle oldBestCircle = hand.Circle;
            hand.Circle.Radius = bestRadius;
            _djAutoHandsNextFrame[bestHandIndex] = hand;

            if (hand.VisualIndex >= 0 && hand.VisualKind == DJAutoHandVisualKind.Coverage)
            {
                CoverResult cover = _activeCoveragesNextFrame[hand.VisualIndex];
                cover.Circle1 = hand.Circle;
                _activeCoveragesNextFrame[hand.VisualIndex] = cover;
            }
            else if (hand.VisualIndex >= 0 && hand.VisualKind == DJAutoHandVisualKind.WorldHit)
            {
                HitRenderData hit = _worldPosHitsNextFrame[hand.VisualIndex];
                hit.radius = hand.Circle.Radius;
                _worldPosHitsNextFrame[hand.VisualIndex] = hit;
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
                    SetNextFrameSensorOn((SensorType)sensorIndex);
            }
        }

        /// <summary>
        /// DJAuto处理星星
        /// </summary>
        public void DJAutoHandleWorldPosition(in float2 pos, float radius = InputManager.DJAUTO_HAND_RADIUS)
        {
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
            _djAutoInputCount = 0;

            // DJAuto 的判定状态和手部显示使用同一份 next-frame 数据，避免画面领先一帧。
            var coverageCount = math.min(
                Interlocked.Exchange(ref _activeCoveragesNextFrameCount, 0),
                ActiveCoverages.Length);
            *ActiveCoveragesCountPtr = coverageCount;
            for (int i = 0; i < coverageCount; i++)
                ActiveCoverages[i] = _activeCoveragesNextFrame[i];

            var hitCount = math.min(
                Interlocked.Exchange(ref _worldPosHitsNextFrameCount, 0),
                _worldPosHitsNextFrame.Length);
            if (_showHandThisFrame)
            {
                for (int i = 0; i < hitCount; i++)
                {
                    var hitIndex = Interlocked.Increment(ref *HitWriteCountPtr) - 1;
                    hitRender[hitIndex] = _worldPosHitsNextFrame[i];
                }
            }

            // 先保留上一帧的合计引用数，再消费 DJAuto 在上一帧排入的输入。
            // 随后用户输入会继续加到 ActiveDown 上，两种来源自然遵循同一套边沿判断。
            for (int i = 0; i < BUTTON_COUNT; i++)
            {
                ref var button = ref _buttonStates.ElementRef(i);
                button.LastActiveDown = button.ActiveDown;
                button.ActiveDown = Interlocked.Exchange(
                    ref _buttonActiveDownNextFrame.ElementRef(i), 0);
            }
            for (int i = 0; i < SENSOR_COUNT; i++)
            {
                ref var sensor = ref _sensorStates.ElementRef(i);
                sensor.LastActiveDown = sensor.ActiveDown;
                sensor.ActiveDown = Interlocked.Exchange(
                    ref _sensorActiveDownNextFrame.ElementRef(i), 0);
            }
        }

        /// <summary>
        /// 处理按键输入
        /// </summary>
        /// <param name="nextFrame">是否应用到下一帧（DJAuto）</param>
        public void HandleButtonInput(SensorType type, bool status, bool nextFrame = false)
        {
            if (!status) return;

            if (nextFrame)
                SetNextFrameButtonOn(type);
            else
                SetThisFrameButtonOn(type);
        }
        /// <summary>
        /// 处理世界坐标（手）输入
        /// </summary>
        /// <param name="nextFrame">是否应用到下一帧（DJAuto）</param>
        public void HandleWorldPosInput(in float2 pos, float radius = InputManager.DJAUTO_HAND_RADIUS, bool nextFrame = false)
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
                {
                    if (nextFrame)
                        SetNextFrameSensorOn((SensorType)i);
                    else
                        SetThisFrameSensorOn((SensorType)i);
                }
            }

            if (_showHandThisFrame) // 本帧没有锁定渲染缓冲时不能写入指针
            {
                var hit = new HitRenderData
                {
                    pos = pos,
                    radius = radius,
                    color = new float4(1, 0, 0, 0.75f)
                };

                if (nextFrame)
                {
                    var idx = Interlocked.Increment(ref _worldPosHitsNextFrameCount) - 1;
                    if (idx < _worldPosHitsNextFrame.Length)
                        _worldPosHitsNextFrame[idx] = hit;
                }
                else
                {
                    var idx = Interlocked.Increment(ref *HitWriteCountPtr) - 1;
                    hitRender[idx] = hit;
                }
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
        private void SetNextFrameButtonOn(SensorType type)
        {
            Interlocked.Increment(ref _buttonActiveDownNextFrame.ElementRef((int)type));
        }
        private void SetNextFrameSensorOn(SensorType type)
        {
            Interlocked.Increment(ref _sensorActiveDownNextFrame.ElementRef((int)type));
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
            _activeCoveragesNextFrameCount = 0;
            _worldPosHitsNextFrameCount = 0;

            for (var i = 0; i < BUTTON_COUNT; i++)
            {
                _buttonStates[i] = default;
                _buttonActiveDownNextFrame[i] = 0;
                _nextButtonIndex[i] = 0;
                _nextButtonIndexNextFrame[i] = 0;
            }
            for (var i = 0; i < SENSOR_COUNT; i++)
            {
                _sensorStates[i] = default;
                _sensorActiveDownNextFrame[i] = 0;
                _nextSensorIndex[i] = 0;
                _nextSensorIndexNextFrame[i] = 0;
            }
        }

        public void Dispose()
        {
            if (SensorWorldPositions.IsCreated) SensorWorldPositions.Dispose();
            if (_sensorStates.IsCreated) _sensorStates.Dispose();
            if (_sensorActiveDownNextFrame.IsCreated) _sensorActiveDownNextFrame.Dispose();
            if (_nextSensorIndex.IsCreated) _nextSensorIndex.Dispose();
            if (_nextSensorIndexNextFrame.IsCreated) _nextSensorIndexNextFrame.Dispose();
            if (_buttonStates.IsCreated) _buttonStates.Dispose();
            if (_buttonActiveDownNextFrame.IsCreated) _buttonActiveDownNextFrame.Dispose();
            if (_nextButtonIndex.IsCreated) _nextButtonIndex.Dispose();
            if (_nextButtonIndexNextFrame.IsCreated) _nextButtonIndexNextFrame.Dispose();

            if (_djAutoHandsNextFrame.IsCreated) _djAutoHandsNextFrame.Dispose();
            if (ActiveCoverages.IsCreated) ActiveCoverages.Dispose();
            if (_activeCoveragesNextFrame.IsCreated) _activeCoveragesNextFrame.Dispose();
            if (_worldPosHitsNextFrame.IsCreated) _worldPosHitsNextFrame.Dispose();
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
