#nullable enable

using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using static MajBurst;
using static MajCtx;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

public class InputManager
{
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
            // 这里是抄的 muridx 的参数，其中除了 D 区以外，其他判定区恰好和 touch 坐标重合
            if (i >= 17 && i <= 24)
                // D 区用于计算触摸圆的坐标与其 touch 坐标不同
                InputData.SensorWorldPositions[i] = MajPos.RingPos(4.4f, i - 16, true);
            else
                InputData.SensorWorldPositions[i] = MajPos.GetAreaPos((SensorType)i);
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
        if (!TryAcquireDJAutoInputs(1)) return;

        SetNextFrameButtonOn(type);
    }
    /// <summary>
    /// DJAuto判定区处理Tap/Hold
    /// </summary>
    public void DJAutoSetSensorOn(SensorType type)
    {
        if (!TryAcquireDJAutoInputs(1)) return;

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
            float slideStart = NoteHelper.DJAUTO_TOUCH_DOUBLE_CIRCLE_SLIDE_START_SEC;
            float slideDuration = NoteHelper.TOUCH_JUDGE_SEG_3RD_PERFECT_MSEC / 1000f;
            float progress = math.saturate((timing - slideStart) / slideDuration);
            cover.Circle1.Center = math.lerp(cover.Circle1.Center, cover.Circle1End, progress);
            cover.Circle2.Center = math.lerp(cover.Circle2.Center, cover.Circle2End, progress);
        }

        for (int i = 0; i < _activeCoveragesNextFrameCount; i++)
        {
            var existing = _activeCoveragesNextFrame[i];
            if (existing.Mode == cover.Mode && math.all(existing.Circle1.Center == cover.Circle1.Center))
                return;
        }

        var requiredInputs = cover.Mode is CoverMode.DoubleCircleDirect or CoverMode.DoubleCircleGroup or CoverMode.DoubleCircleSlide ? 2 : 1;
        if (!TryAcquireDJAutoInputs(requiredInputs)) return;

        var idx = Interlocked.Increment(ref _activeCoveragesNextFrameCount) - 1;
        if (idx < _activeCoveragesNextFrame.Length)
        {
            _activeCoveragesNextFrame[idx] = cover;

            // Intersect virtual hand circles with physical sensors to trigger them
            for (int s = 0; s < SENSOR_COUNT; s++)
            {
                ref readonly var sp = ref SensorWorldPositions.ElementRef(s);
                var sr = MajPos.GetSensorRadius((SensorType)s);

                bool hits = false;

                var r1 = cover.Circle1.Radius + sr;
                if (math.distancesq(sp, cover.Circle1.Center) <= r1 * r1 + 1e-4f)
                {
                    hits = true;
                }
                else if (cover.Mode == CoverMode.DoubleCircleDirect || cover.Mode == CoverMode.DoubleCircleGroup || cover.Mode == CoverMode.DoubleCircleSlide)
                {
                    var r2 = cover.Circle2.Radius + sr;
                    if (math.distancesq(sp, cover.Circle2.Center) <= r2 * r2 + 1e-4f)
                    {
                        hits = true;
                    }
                }

                if (hits)
                {
                    SetNextFrameSensorOn((SensorType)s);
                }
            }
        }
    }
    /// <summary>
    /// DJAuto处理星星
    /// </summary>
    public void DJAutoHandleWorldPosition(in float2 pos, float radius = DJAUTO_HAND_RADIUS)
    {
        if (!TryAcquireDJAutoInputs(1)) return;

        HandleWorldPosInput(pos, radius, true);
    }
    /// <summary>
    /// DJAuto处理wifi星星
    /// </summary>
    public void DJAutoHandleWifiWorldPosition(in float2 leftPos, in float2 rightPos)
    {
        if (!TryAcquireDJAutoInputs(2)) return;

        HandleWorldPosInput(leftPos, DJAUTO_WIFI_RADIUS, true);
        HandleWorldPosInput(rightPos, DJAUTO_WIFI_RADIUS, true);
    }
    /// <summary>
    /// 申请手
    /// </summary>
    /// <param name="requiredInputs">申请几只手</param>
    private bool TryAcquireDJAutoInputs(int requiredInputs)
    {
        while (true)
        {
            var currentCount = Interlocked.CompareExchange(ref _djAutoInputCount, 0, 0);
            if (currentCount + requiredInputs > DJAUTO_MAX_CONCURRENT_INPUTS)
                return false;

            if (Interlocked.CompareExchange(ref _djAutoInputCount, currentCount + requiredInputs, currentCount) == currentCount)
                return true;
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
    public void HandleWorldPosInput(in float2 pos, float radius = DJAUTO_HAND_RADIUS, bool nextFrame = false)
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
                        radius = BUTTON_HIT_RENDER_RADIUS,
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
