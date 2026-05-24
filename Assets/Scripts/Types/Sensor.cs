#region

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#endregion

public class Sensor : MonoBehaviour
{
    [SerializeField]
    public SensorType Type;
    public SensorStatus Status { get; private set; } = SensorStatus.Off;
    public bool IsJudging { get; set; } = false;
    public event EventHandler<InputEventArgs> OnStatusChanged;

    private List<Guid> tasks = new();
    public void SetOn(Guid id)
    {
        if (tasks.Contains(id))
            return;

        var oStatus = Status;
        var nStatus = SensorStatus.On;
        Status = nStatus;

        if (!tasks.Contains(id))
            tasks.Add(id);
        if (oStatus != nStatus)
        {
            if (OnStatusChanged != null)
            {
                OnStatusChanged(this, new InputEventArgs()
                {
                    IsButton = false,
                    Type = Type,
                    OldStatus = oStatus,
                    Status = nStatus
                });
                IsJudging = false;
            }
            print($"Sensor:{Type} On");
        }
    }
    public void SetOff(Guid id)
    {
        if (!tasks.Contains(id))
            return;
        var nStatus = SensorStatus.Off;

        tasks.Remove(id);
        if (tasks.Count == 0)
        {
            var oStatus = Status;
            if (OnStatusChanged != null)
            {
                OnStatusChanged(this, new InputEventArgs()
                {
                    IsButton = false,
                    Type = Type,
                    OldStatus = oStatus,
                    Status = nStatus
                });
            }
            Status = nStatus;
            print($"Sensor:{Type} Off");
        }
    }
    public void Click()
    {
        var guid = Guid.NewGuid();
        SetOn(guid);
        SetOff(guid);
    }

    public void ForceReset()
    {
        tasks.Clear();
        Status = SensorStatus.Off;
        IsJudging = false;
        OnStatusChanged = null;
    }
}
