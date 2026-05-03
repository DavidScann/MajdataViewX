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
    
    public List<Sensor> Sensors = new();
    public List<Button> Buttons = new();
    
    public Dictionary<int,List<Sensor>> triggerSensors = new();
    
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
            new(KeyCode.W, Sensors[0]), //A1~8
            new(KeyCode.E, Sensors[1]),
            new(KeyCode.D, Sensors[2]),
            new(KeyCode.C, Sensors[3]),
            new(KeyCode.X, Sensors[4]),
            new(KeyCode.Z, Sensors[5]),
            new(KeyCode.A, Sensors[6]),
            new(KeyCode.Q, Sensors[7]),
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
            foreach(var touch in Input.touches)
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

    public Button GetButton(Sensor sensor) => Buttons[(int)sensor.Type];
    public Sensor GetSensor(SensorArea sensorArea) => Sensors[(int)sensorArea];
    
    public void BindSensor(EventHandler<InputEventArgs> checker, Sensor sensor)
    {
        sensor.OnStatusChanged += checker;
    }
    public void UnbindSensor(EventHandler<InputEventArgs> checker, Sensor sensor)
    {
        sensor.OnStatusChanged -= checker;
    }
    public void BindArea(EventHandler<InputEventArgs> checker, Sensor sensor)
    {
        var button = GetButton(sensor);

        sensor.OnStatusChanged += checker;
        button.OnStatusChanged += checker;
    }
    public void UnbindArea(EventHandler<InputEventArgs> checker, Sensor sensor)
    {
        var button = GetButton(sensor);

        sensor.OnStatusChanged -= checker;
        button.OnStatusChanged -= checker;
    }
    
    
    public bool CheckAreaStatus(Sensor sensor, SensorStatus targetStatus)
    {
        var button = GetButton(sensor);
        return sensor.Status == targetStatus || button.Status == targetStatus; 
    }
    public bool CheckSensorStatus(Sensor sensor, SensorStatus targetStatus)
    {
        return sensor.Status == targetStatus;
    }

    public void SetBusy(InputEventArgs args)
    {
        if(args.IsButton)
        {
            GetButton(args.Sensor).IsJudging = true;
        }
        else
        {
            args.Sensor.IsJudging = true;
        }
    }
    public bool IsIdle(InputEventArgs args)
    {
        bool isIdle;
        if (args.IsButton)
        {
            isIdle = GetButton(args.Sensor).IsJudging;
        }
        else
        {
            isIdle = !args.Sensor.IsJudging;
        }
        return isIdle;
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
    
        const float HAND_RADIUS = 0.28f;
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
    
    void CheckButton()
    {
        foreach (var button in Buttons)
        {
            var nStatus = Input.GetKey(button.BindingKey) ? SensorStatus.On : SensorStatus.Off;
            var oStatus = button.Status;
            if (oStatus == nStatus) return;

            print($"Key \"{button.BindingKey}\": {nStatus}");
            button.PushEvent(new InputEventArgs()
            {
                Sensor = button.Sensor,
                OldStatus = oStatus,
                Status = nStatus,
                IsButton = true
            });
            button.Status = nStatus;
            button.IsJudging = false;
        }
    }

    public void ResetState()
    {
        triggerSensors.Clear();

        foreach (var sensor in Sensors)
        {
            sensor.ForceReset();
        }

        foreach (var button in Buttons)
        {
            button.Status = SensorStatus.Off;
            button.IsJudging = false;
        }
    }
}
