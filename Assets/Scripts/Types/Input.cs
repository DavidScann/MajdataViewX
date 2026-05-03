#nullable enable

#region

using System;
using System.Collections.Generic;
using UnityEngine;

#endregion

public struct InputEventArgs
{
    public SensorType Type { get; set; }
    public SensorStatus OldStatus { get; set; }
    public SensorStatus Status { get; set; }
    public bool IsButton { get; set; }
    public bool IsClick => OldStatus == SensorStatus.Off && Status == SensorStatus.On;

}

public class Button
{
    public KeyCode BindingKey { get; }
    public SensorType Type { get; }
    public SensorStatus Status { get; private set; } = SensorStatus.Off;
    public bool IsJudging { get; set; } = false;
    public event EventHandler<InputEventArgs>? OnStatusChanged;

    public Button(KeyCode bindingKey, SensorType type)
    {
        BindingKey = bindingKey;
        Type = type;
    }

    private List<Guid> tasks = new();
    
    public void SetOn(Guid id)
    {
        if (tasks.Contains(id))
            return;
        
        var oStatus = Status;
        var nStatus = SensorStatus.On;
        Status = nStatus;
        
        if(!tasks.Contains(id))
            tasks.Add(id);
        if (oStatus != nStatus)
        {
            if (OnStatusChanged != null)
            {
                OnStatusChanged(this, new InputEventArgs()
                {
                    IsButton = true,
                    Type = Type,
                    OldStatus = oStatus,
                    Status = nStatus
                });
                IsJudging = false;
            }
            Debug.Log($"Button:{Type} On");
        }
    }
    public void SetOff(Guid id) 
    {
        if (!tasks.Contains(id))
            return;
        var nStatus = SensorStatus.Off;

        tasks.Remove(id);
        if(tasks.Count == 0)
        {
            var oStatus = Status;
            if (OnStatusChanged != null)
            {
                OnStatusChanged(this, new InputEventArgs()
                {
                    IsButton = true,
                    Type = Type,
                    OldStatus = oStatus,
                    Status = nStatus
                });
            }
            Status = nStatus;
            Debug.Log($"Button:{Type} Off");
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

