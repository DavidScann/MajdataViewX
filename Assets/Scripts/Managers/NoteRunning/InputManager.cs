#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

using static MajCtx;
using static MajBurst;
using Unity.Collections;
using Unity.Burst;
using UnityEngine.UI;

public class InputManager : MonoBehaviour
{
    public AutoPlayMode Mode { get; set; }
    public bool ButtonFirst { get; set; }

    private Vector2[] _sensorWorldPositions;

    private void Awake()
    {
        _inputManager = this;

        //get sensor positions
        var sensors = GameObject.Find("SensorRects").transform;
        var childCount = sensors.childCount;
        _sensorWorldPositions = new Vector2[sensors.childCount];
        for (var i = 0; i < childCount; i++)
        {
            _sensorWorldPositions[i] = sensors.GetChild(i).transform.position;
        }
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            CheckButton(keyboard);
        }

        var mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.leftButton.isPressed)
                WriteWorldPosition(mouse.position.ReadValue());
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

    private void CheckButton(Keyboard keyboard)
    {
        MajBurst.InputData.SetButtonState(SensorType.A1, keyboard[Key.W].isPressed);
        MajBurst.InputData.SetButtonState(SensorType.A2, keyboard[Key.E].isPressed);
        MajBurst.InputData.SetButtonState(SensorType.A3, keyboard[Key.D].isPressed);
        MajBurst.InputData.SetButtonState(SensorType.A4, keyboard[Key.C].isPressed);
        MajBurst.InputData.SetButtonState(SensorType.A5, keyboard[Key.X].isPressed);
        MajBurst.InputData.SetButtonState(SensorType.A6, keyboard[Key.Z].isPressed);
        MajBurst.InputData.SetButtonState(SensorType.A7, keyboard[Key.A].isPressed);
        MajBurst.InputData.SetButtonState(SensorType.A8, keyboard[Key.Q].isPressed);
    }

    private void WriteWorldPosition(Vector2 screenPos)
    {
        var mainCamera = Camera.main;
        if (mainCamera == null) return;
        var worldPos = mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f));
        var pos = (Vector2)worldPos;

        const float HAND_RADIUS = 0.39f;
        const float SENSOR_RADIUS = 0.39f;
        var combinedR = HAND_RADIUS + SENSOR_RADIUS;
        var combinedSq = combinedR * combinedR;

        for (int i = 0; i < _sensorWorldPositions.Length; i++)
        {
            var sp = _sensorWorldPositions[i];
            var dx = pos.x - sp.x;
            var dy = pos.y - sp.y;
            var distSq = dx * dx + dy * dy;

            var s = MajBurst.InputData.GetSensorState((SensorType)i);
            if (distSq <= combinedSq)
            {
                if (!s.Status)
                    s.Status = true;
            }
            else
            {
                if (s.Status)
                    s.Status = false;
            }
            MajBurst.InputData.SetSensorState((SensorType)i, s.Status);
        }
    }

    public static SensorType GetSensor(char areaPos, int startPos)
    {
        switch (areaPos)
        {
            case 'A': return (SensorType)(startPos - 1);
            case 'B': return (SensorType)(startPos + 7);
            case 'C': return SensorType.C;
            case 'D': return (SensorType)(startPos + 16);
            case 'E': return (SensorType)(startPos + 24);
            default: return SensorType.A1;
        }
    }

    public void ResetState()
    {
    }
}

[BurstCompile]
public struct InputDataB
{
    NativeArray<SensorState> ButtonStates;
    NativeArray<SensorState> SensorStates;
    NativeArray<int> NextButtonIndex;
    NativeArray<int> NextSensorIndex;

    public void Init()
    {
        ButtonStates = new(BUTTON_COUNT, Allocator.Persistent);
        SensorStates = new(SENSOR_COUNT, Allocator.Persistent);
        NextButtonIndex = new(BUTTON_COUNT, Allocator.Persistent);
        NextSensorIndex = new(SENSOR_COUNT, Allocator.Persistent);

        for (var i = 0; i < BUTTON_COUNT; i++)
            ButtonStates[i] = new();
        for (var i = 0; i < SENSOR_COUNT; i++)
            SensorStates[i] = new();
    }

    public void SetButtonState(SensorType type, bool status)
    {
        ButtonStates[(int)type] = new SensorState
        {
            Status = status
        };
    }

    public readonly SensorState GetButtonState(SensorType type)
    {
        return ButtonStates[(int)type];
    }

    public void SetSensorState(SensorType type, bool status)
    {
        SensorStates[(int)type] = new SensorState
        {
            Status = status
        };
    }

    public readonly SensorState GetSensorState(SensorType type)
    {
        return SensorStates[(int)type];
    }

    public void NextTapHold(SensorType pos)
    {
        NextButtonIndex[(int)pos]++;
        NextSensorIndex[(int)pos]++;
    }

    public void NextTouch(SensorType pos)
    {
        NextSensorIndex[(int)pos]++;
    }

    public readonly bool CanJudgeButton(SensorType pos, int order)
    {
        return order == NextButtonIndex[(int)pos];
    }

    public readonly bool CanJudgeSensor(SensorType pos, int order)
    {
        return order == NextSensorIndex[(int)pos];
    }

    public void ResetIndex()
    {
        for (var i = 0; i < BUTTON_COUNT; i++)
            NextButtonIndex[i] = 0;
        for (var i = 0; i < SENSOR_COUNT; i++)
            NextSensorIndex[i] = 0;
    }

    public void ResetState()
    {
        ResetIndex();

        for (var i = 0; i < BUTTON_COUNT; i++)
            ButtonStates[i] = default;
        for (var i = 0; i < SENSOR_COUNT; i++)
            SensorStates[i] = default;
    }

    public void Dispose()
    {
        if (SensorStates.IsCreated) SensorStates.Dispose();
        if (NextSensorIndex.IsCreated) NextSensorIndex.Dispose();
        if (ButtonStates.IsCreated) ButtonStates.Dispose();
        if (NextButtonIndex.IsCreated) NextButtonIndex.Dispose();
    }
}

public struct SensorState
{
    public bool Status;
}
