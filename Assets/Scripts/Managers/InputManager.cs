#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

using static MajCtx;

public class InputManager : MonoBehaviour
{
    public AutoPlayMode Mode { get; set; }
    public bool ButtonFirst { get; set; }

    private void Awake()
    {
        _inputManager = this;
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
        _judgeManager.states[(int)SensorType.A1]
            = new SensorState { Status = keyboard[Key.W].isPressed ? SensorStatus.On : SensorStatus.Off, IsJudging = false };
        _judgeManager.states[(int)SensorType.A2]
            = new SensorState { Status = keyboard[Key.E].isPressed ? SensorStatus.On : SensorStatus.Off, IsJudging = false };
        _judgeManager.states[(int)SensorType.A3]
            = new SensorState { Status = keyboard[Key.D].isPressed ? SensorStatus.On : SensorStatus.Off, IsJudging = false };
        _judgeManager.states[(int)SensorType.A4]
            = new SensorState { Status = keyboard[Key.C].isPressed ? SensorStatus.On : SensorStatus.Off, IsJudging = false };
        _judgeManager.states[(int)SensorType.A5]
            = new SensorState { Status = keyboard[Key.X].isPressed ? SensorStatus.On : SensorStatus.Off, IsJudging = false };
        _judgeManager.states[(int)SensorType.A6]
            = new SensorState { Status = keyboard[Key.Z].isPressed ? SensorStatus.On : SensorStatus.Off, IsJudging = false };
        _judgeManager.states[(int)SensorType.A7]
            = new SensorState { Status = keyboard[Key.A].isPressed ? SensorStatus.On : SensorStatus.Off, IsJudging = false };
        _judgeManager.states[(int)SensorType.A8]
            = new SensorState { Status = keyboard[Key.Q].isPressed ? SensorStatus.On : SensorStatus.Off, IsJudging = false };
    }

    private void WriteWorldPosition(Vector2 screenPos)
    {
        var mainCamera = Camera.main;
        if (mainCamera == null) return;

        var worldPos = mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f));
        var pos = new Vector2(worldPos.x, worldPos.y);

        const float HAND_RADIUS = 0.39f;
        const float SENSOR_RADIUS = 0.39f;
        var combinedR = HAND_RADIUS + SENSOR_RADIUS;
        var combinedSq = combinedR * combinedR;

        // for (int i = 0; i < JudgeManager.SENSOR_COUNT; i++)
        // {
        //     //TODO
        //     var sp = _judgeManager.sensorWorldPositions[i];
        //     var dx = pos.x - sp.x;
        //     var dy = pos.y - sp.y;
        //     var distSq = dx * dx + dy * dy;

        //     var s = _judgeManager.states[i];
        //     s.IsJudging = false;
        //     if (distSq <= combinedSq)
        //     {
        //         if (s.Status != SensorStatus.On)
        //             s.Status = SensorStatus.On;
        //     }
        //     else
        //     {
        //         if (s.Status != SensorStatus.Off)
        //             s.Status = SensorStatus.Off;
        //     }
        //     _judgeManager.states[i] = s;
        // }
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
        for (int i = 0; i < JudgeManager.SENSOR_COUNT; i++)
            _judgeManager.states[i] = default;
    }
}
