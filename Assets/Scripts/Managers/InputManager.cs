#nullable enable

#region

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#endregion

public class InputManager : MonoBehaviour
{
    public AutoPlayMode Mode { get; set; }

    private Guid guid = Guid.NewGuid();

    private List<Sensor> Sensors = new();
    private List<Button> Buttons = new();

    public Dictionary<int, List<Sensor>> triggerSensors = new();

    private void Awake()
    {
        Majdata<InputManager>.Instance = this;
    }

    private void Start()
    {
        //init sensors and buttons
        var sensorsObj = GameObject.Find("Sensors");
        for (var i = 0; i < sensorsObj.transform.childCount; i++)
        {
            var obj = sensorsObj.transform.GetChild(i).gameObject;
            Sensors.Add(obj.GetComponent<Sensor>());
        }

        Buttons = new(new Button[]
        {
            new(KeyCode.W, SensorType.A1), //A1~8
            new(KeyCode.E, SensorType.A2),
            new(KeyCode.D, SensorType.A3),
            new(KeyCode.C, SensorType.A4),
            new(KeyCode.X, SensorType.A5),
            new(KeyCode.Z, SensorType.A6),
            new(KeyCode.A, SensorType.A7),
            new(KeyCode.Q, SensorType.A8),
        });
    }

    private void Update()
    {
        //check keyboard and mouse input
        CheckButton();
        if (Input.GetMouseButton(0))
            ScreenPositionHandle(-1, Input.mousePosition);
        else
            Untrigger(-1);

        if (Input.touchCount > 0)
        {
            foreach (var touch in Input.touches)
            {
                switch (touch.phase)
                {
                    case TouchPhase.Began:
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        ScreenPositionHandle(touch.fingerId, touch.position);
                        break;
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        Untrigger(touch.fingerId);
                        break;
                }
            }
        }
    }

    public static SensorType GetSensor(char areaPos, int startPos)
    {
        switch (areaPos)
        {
            case 'A':
                return (SensorType)(startPos - 1);
            case 'B':
                return (SensorType)(startPos + 7);
            case 'C':
                return SensorType.C;
            case 'D':
                return (SensorType)(startPos + 16);
            case 'E':
                return (SensorType)(startPos + 24);
            default:
                return SensorType.A1;
        }
    }
    private Sensor GetSensor(SensorType type) => Sensors[(int)type];
    private Button GetButton(SensorType type)
    {
        var index = (int)type;
        switch (type)
        {
            case >= SensorType.A1 and <= SensorType.A8:
                return Buttons[index];
            case >= SensorType.B1 and <= SensorType.B8:
                return Buttons[index - 7];
            case SensorType.C:
            default:
                return Buttons[0];
            case >= SensorType.D1 and <= SensorType.D8:
                return Buttons[index - 16];
            case >= SensorType.E1 and <= SensorType.E8:
                return Buttons[index - 24];
        }
    }

    public void BindSensor(EventHandler<InputEventArgs> checker, SensorType type)
    {
        GetSensor(type).OnStatusChanged += checker;
    }
    public void UnbindSensor(EventHandler<InputEventArgs> checker, SensorType type)
    {
        GetSensor(type).OnStatusChanged -= checker;
    }
    public void BindArea(EventHandler<InputEventArgs> checker, SensorType type)
    {
        GetSensor(type).OnStatusChanged += checker;
        GetButton(type).OnStatusChanged += checker;
    }
    public void UnbindArea(EventHandler<InputEventArgs> checker, SensorType type)
    {
        GetSensor(type).OnStatusChanged -= checker;
        GetButton(type).OnStatusChanged -= checker;
    }


    public bool CheckArea(SensorType type) =>
        GetSensor(type).Status == SensorStatus.On ||
        GetButton(type).Status == SensorStatus.On;

    public bool CheckSensor(SensorType type) =>
        GetSensor(type).Status == SensorStatus.On;

    public void SetSensorOn(SensorType type, Guid guid) =>
        GetSensor(type).SetOn(guid);
    public void SetSensorOff(SensorType type, Guid guid) =>
        GetSensor(type).SetOff(guid);
    public void ClickSensor(SensorType type) =>
        GetSensor(type).Click();

    public void SetBusy(InputEventArgs args)
    {
        if (args.IsButton)
        {
            GetButton(args.Type).IsJudging = true;
        }
        else
        {
            GetSensor(args.Type).IsJudging = true;
        }
    }
    public bool IsIdle(InputEventArgs args)
    {
        if (args.IsButton)
        {
            return !GetButton(args.Type).IsJudging;
        }
        else
        {
            return !GetSensor(args.Type).IsJudging;
        }
    }



    void Untrigger(int id)
    {
        if (!triggerSensors.TryGetValue(id, out var triggerSensor))
            return;

        foreach (var s in triggerSensor)
            s.SetOff(guid);
        triggerSensor.Clear();
    }

    public void ScreenPositionHandle(int id, Vector3 pos)
    {
        var mainCamera = Camera.main!;
        var sPosition = pos;
        sPosition.z = 10f; //for parse
        var wPos3 = mainCamera.ScreenToWorldPoint(sPosition);
        var worldPos = new Vector2(wPos3.x, wPos3.y);
        WorldPositionHandle(id, worldPos);
    }

    public void WorldPositionHandle(int id, Vector2 pos)
    {
        if (!triggerSensors.ContainsKey(id))
            triggerSensors.Add(id, new());

        const float HAND_RADIUS = 0.39f;
        var oldList = new List<Sensor>(triggerSensors[id]);
        triggerSensors[id].Clear();

        foreach (var sensor in Sensors)
        {
            var s = (RectTransform)sensor.gameObject.transform;

            Vector2 rCenter = s.position;
            var rWidth = s.rect.width * s.lossyScale.x;
            var rHeight = s.rect.height * s.lossyScale.y;

            var radius = Math.Max(rWidth, rHeight) / 2f;

            var combinedRadius = radius + HAND_RADIUS;
            if ((pos - rCenter).sqrMagnitude <= (combinedRadius * combinedRadius))
            {
                triggerSensors[id].Add(sensor);
            }
        }

        var untriggerSensors = oldList.Where(x => !triggerSensors[id].Contains(x));
        foreach (var s in untriggerSensors)
            s.SetOff(guid);
        foreach (var s in triggerSensors[id])
            s.SetOn(guid);
    }

    public void ClearTriggeredSensor(int id)
    {
        if (!triggerSensors.ContainsKey(id)) return;
        var oldList = new List<Sensor>(triggerSensors[id]);
        triggerSensors[id].Clear();
        var untriggerSensors = oldList.Where(x => !triggerSensors[id].Contains(x));
        foreach (var s in untriggerSensors)
            s.SetOff(guid);
        foreach (var s in triggerSensors[id])
            s.SetOn(guid);
    }

    void CheckButton()
    {
        foreach (var button in Buttons)
        {
            if (Input.GetKey(button.BindingKey))
                button.SetOn(guid);
            else
                button.SetOff(guid);
        }
    }

    public void ResetState()
    {
        triggerSensors.Clear();

        foreach (var sensor in Sensors)
            sensor.ForceReset();
        foreach (var button in Buttons)
            button.ForceReset();
    }
}
