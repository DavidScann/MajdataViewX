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

public class InputManager : MonoBehaviour
{
    RenderGroup<HitRenderData> _hitGroup;

    private void Awake()
    {
        _inputManager = this;
    }

    private void Start()
    {
        //get sensor positions
        var sensors = GameObject.Find("SensorRects").transform;
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
        _hitGroup = new(matHit, hitMesh, 6); // larger than notes
    }

    private unsafe void Update()
    {
        // UPDATE MUST BE EARLIER THAN NoteManager's UPDATE!!
        // (set in Script Execution Order)
        _hitGroup.AdvanceWrite();
        var hitRender = _hitGroup.LockForWrite();
        _hitGroup.ResetCount();

        InputData.hitRender = (HitRenderData*)hitRender.GetUnsafePtr();
        InputData.HitWriteCountPtr = _hitGroup.WriteCountPtr;
        InputData.BeginHandler();

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
                WriteWorldPosition(mouse.position.ReadValue());
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
                    WriteWorldPosition(touch.position.ReadValue());
            }
        }
    }

    public void RenderHit() // in NoteManager, wait for slide
    {
        InputData.EndHandler();
        _hitGroup.UnlockWrite();
        _hitGroup.Render();
        _hitGroup.Swap();
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

    private void WriteWorldPosition(Vector2 screenPos)
    {
        var mainCamera = Camera.main;
        var pos = (Vector2)mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f));

        InputData.HandleWorldPosition(pos);
    }

    public static SensorType GetSensor(char areaPos, int startPos)
    {
        return areaPos switch
        {
            'A' => (SensorType)(startPos - 1),
            'B' => (SensorType)(startPos + 7),
            'C' => SensorType.C,
            'D' => (SensorType)(startPos + 16),
            'E' => (SensorType)(startPos + 24),
            _ => SensorType.A1,
        };
    }

    public void ResetState()
    {
    }

    private void OnDestroy()
    {
        _hitGroup?.Dispose();
    }
}

[BurstCompile]
public unsafe struct InputDataB
{
    public bool ShowHand;

    public NativeArray<float2> SensorWorldPositions;

    NativeArray<SensorState> _buttonStates;
    NativeArray<SensorState> _sensorStates;
    NativeArray<int> _nextButtonIndex;
    NativeArray<int> _nextSensorIndex;
    NativeArray<int> _nextButtonIndexNextFrame;
    NativeArray<int> _nextSensorIndexNextFrame;

    const int DJAUTO_MAX_CONCURRENT_INPUTS = 2;
    int _djAutoInputCount;

    public NativeArray<CoverResult> ActiveCoverages;
    [NativeDisableUnsafePtrRestriction]
    public int* ActiveCoveragesCountPtr;

    [NativeDisableUnsafePtrRestriction]
    public HitRenderData* hitRender;
    [NativeDisableUnsafePtrRestriction]
    public int* HitWriteCountPtr;
    public const float BUTTON_HIT_RENDER_RADIUS = 0.4f;

    public void Init()
    {
        SensorWorldPositions = new(SENSOR_COUNT, Allocator.Persistent);

        _buttonStates = new(BUTTON_COUNT, Allocator.Persistent);
        _sensorStates = new(SENSOR_COUNT, Allocator.Persistent);
        _nextButtonIndex = new(BUTTON_COUNT, Allocator.Persistent);
        _nextSensorIndex = new(SENSOR_COUNT, Allocator.Persistent);
        _nextButtonIndexNextFrame = new(BUTTON_COUNT, Allocator.Persistent);
        _nextSensorIndexNextFrame = new(SENSOR_COUNT, Allocator.Persistent);

        for (var i = 0; i < BUTTON_COUNT; i++)
            _buttonStates[i] = new();
        for (var i = 0; i < SENSOR_COUNT; i++)
            _sensorStates[i] = new();

        ActiveCoverages = new(32, Allocator.Persistent);
        ActiveCoveragesCountPtr = (int*)UnsafeUtility.Malloc(sizeof(int), 4, Allocator.Persistent);
    }






    // ==========button/sensor management==========

    public readonly SensorState GetButtonState(SensorType type) => _buttonStates[(int)type];
    public readonly SensorState GetSensorState(SensorType type) => _sensorStates[(int)type];

    /// <summary>
    /// DJAuto按键处理Tap/Hold
    /// </summary>
    public void DJAutoSetButtonOn(SensorType type)
    {
        if (!TryAcquireDJAutoInputs(1)) return;

        SetButtonOn(type);
    }
    /// <summary>
    /// DJAuto判定区处理Tap/Hold
    /// </summary>
    public void DJAutoSetSensorOn(SensorType type)
    {
        if (!TryAcquireDJAutoInputs(1)) return;

        SetSensorOn(type);
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

        for (int i = 0; i < *ActiveCoveragesCountPtr; i++)
        {
            var existing = ActiveCoverages[i];
            if (existing.Mode == cover.Mode && math.all(existing.Circle1.Center == cover.Circle1.Center))
                return;
        }

        var requiredInputs = cover.Mode is CoverMode.DoubleCircleDirect or CoverMode.DoubleCircleGroup or CoverMode.DoubleCircleSlide ? 2 : 1;
        if (!TryAcquireDJAutoInputs(requiredInputs)) return;

        var idx = Interlocked.Increment(ref *ActiveCoveragesCountPtr) - 1;
        if (idx < ActiveCoverages.Length)
        {
            ActiveCoverages[idx] = cover;

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
                    SetSensorOn((SensorType)s);
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

        HandleWorldPosition(pos, radius);
    }

    public void DJAutoHandleWifiWorldPosition(in float2 leftPos, in float2 rightPos)
    {
        if (!TryAcquireDJAutoInputs(2)) return;

        HandleWorldPosition(leftPos, DJAUTO_WIFI_RADIUS);
        HandleWorldPosition(rightPos, DJAUTO_WIFI_RADIUS);
    }

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

    // 每帧重新收集所有输入引用；上一帧引用仅用于生成 Down/Up 边沿。
    public void BeginHandler()
    {
        _djAutoInputCount = 0;
        *ActiveCoveragesCountPtr = 0;

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

    public void HandleButtonInput(SensorType type, bool status)
    {
        if (!status) return;

        ref var button = ref _buttonStates.ElementRef((int)type);
        Interlocked.Increment(ref button.ActiveDown);
    }

    public void HandleWorldPosition(in float2 pos, float radius = DJAUTO_HAND_RADIUS)
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
                ref var sensor = ref _sensorStates.ElementRef(i);
                Interlocked.Increment(ref sensor.ActiveDown);
            }
        }

        var idx = Interlocked.Increment(ref *HitWriteCountPtr) - 1;
        hitRender[idx] = new HitRenderData
        {
            pos = pos,
            radius = radius,
            color = new float4(1, 0, 0, 0.75f)
        };
    }

    public void EndHandler()
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

    private void SetButtonOn(SensorType type)
    {
        ref var button = ref _buttonStates.ElementRef((int)type);
        Interlocked.Increment(ref button.ActiveDown);
    }

    private void SetSensorOn(SensorType type)
    {
        ref var sensor = ref _sensorStates.ElementRef((int)type);
        Interlocked.Increment(ref sensor.ActiveDown);
    }


    // ==========judge management==========
    public void NextTapHold(SensorType pos)
    {
        Interlocked.Increment(ref _nextButtonIndexNextFrame.ElementRef((int)pos));
        Interlocked.Increment(ref _nextSensorIndexNextFrame.ElementRef((int)pos));
    }
    public void NextTouch(SensorType pos)
    {
        Interlocked.Increment(ref _nextSensorIndexNextFrame.ElementRef((int)pos));
    }
    public void ApplyNextIndices()
    {
        for (int i = 0; i < BUTTON_COUNT; i++)
        {
            _nextButtonIndex[i] = _nextButtonIndexNextFrame[i];
        }
        for (int i = 0; i < SENSOR_COUNT; i++)
        {
            _nextSensorIndex[i] = _nextSensorIndexNextFrame[i];
        }
    }
    public readonly bool CanJudgeButton(SensorType pos, int order)
    {
        return order == _nextButtonIndex[(int)pos];
    }
    public readonly bool CanJudgeSensor(SensorType pos, int order)
    {
        return order == _nextSensorIndex[(int)pos];
    }




    public void ResetState()
    {
        _djAutoInputCount = 0;

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
