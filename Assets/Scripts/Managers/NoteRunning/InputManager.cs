#nullable enable

using System.Collections.Generic;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Jobs;
using static MajBurst;
using static MajCtx;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

public class InputManager : MonoBehaviour
{
    public AutoPlayMode Mode { get; set; }

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
            InputData.SensorWorldPositions[i] = (Vector2)sensors.GetChild(i).transform.position;
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
    }






    // ==========button/sensor management==========
    public readonly SensorState GetButtonState(SensorType type) => _buttonStates[(int)type];
    public readonly SensorState GetSensorState(SensorType type) => _sensorStates[(int)type];


    public void DJAutoSetButtonState(SensorType type, bool status)
    {
        ref var button = ref _buttonStates.ElementRef((int)type);
        if (status)
        {
            button.TaskCount++;
            button.HoldFrames = 2;
        }
        else
        {
            button.TaskCount = math.max(button.TaskCount - 1, 0);
        }
    }
    public void DJAutoSetSensorState(SensorType type, bool status)
    {
        ref var sensor = ref _sensorStates.ElementRef((int)type);
        if (status)
        {
            sensor.TaskCount++;
            sensor.HoldFrames = 2;
        }
        else
        {
            sensor.TaskCount = math.max(sensor.TaskCount - 1, 0);
        }
    }


    public void BeginHandler()
    {
        for (int i = 0; i < BUTTON_COUNT; i++)
        {
            ref var button = ref _buttonStates.ElementRef(i);
            if (button.TaskCount > 0) button.HoldFrames = 2;
            else if (button.HoldFrames > 0) button.HoldFrames--;
            button.PressCount = 0;
        }
        for (int i = 0; i < SENSOR_COUNT; i++)
        {
            ref var sensor = ref _sensorStates.ElementRef(i);
            if (sensor.TaskCount > 0) sensor.HoldFrames = 2;
            else if (sensor.HoldFrames > 0) sensor.HoldFrames--;
            sensor.PressCount = 0;
        }
    }
    public void HandleButtonInput(SensorType type, bool status)
    {
        if (status)
        {
            ref var button = ref _buttonStates.ElementRef((int)type);
            Interlocked.Increment(ref button.PressCount);
        }
    }
    public void HandleWorldPosition(in float2 pos)
    {
        for (int i = 0; i < SensorWorldPositions.Length; i++)
        {
            var combinedR = DJAUTO_HAND_RADIUS + MajPos.GetSensorRadius((SensorType)i);
            var combinedSq = combinedR * combinedR;
            ref readonly var sp = ref SensorWorldPositions.ElementRef(i);
            var dx = pos.x - sp.x;
            var dy = pos.y - sp.y;
            var distSq = dx * dx + dy * dy;

            if (distSq <= combinedSq)
            {
                ref var sensor = ref _sensorStates.ElementRef(i);
                Interlocked.Increment(ref sensor.PressCount);
            }
        }

        var idx = Interlocked.Increment(ref *HitWriteCountPtr) - 1;
        hitRender[idx] = new HitRenderData
        {
            pos = pos,
            radius = DJAUTO_HAND_RADIUS,
            color = new float4(1, 0, 0, 1) // Exact hand position color
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
    }
}

public struct SensorState
{
    public readonly bool Status => TaskCount > 0 || PressCount > 0 || HoldFrames > 0;
    public int TaskCount;
    public int PressCount;
    public int HoldFrames;
}
// for coming MuriDX
public readonly struct HitInfo
{
    public readonly bool IsButton;
    public readonly SensorType Type;
    public readonly float Radius;
    public readonly float Time;
    public readonly float Duration;
    public HitInfo(bool isButton, SensorType type, float radius, float duration)
    {
        IsButton = isButton;
        Type = type;
        Radius = radius;
        Time = TimeData.NoteTime;
        Duration = duration;
    }
}