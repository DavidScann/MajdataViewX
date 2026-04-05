using System.Collections.Generic;
using System.Linq;

public class Area
{
    public bool On = false;
    public bool Off = false;
    public SensorArea Type;
    public bool IsLast = false;
    public bool IsFinished
    {
        get
        {
            if (IsLast)
                return On;
            
            return On && Off;
        }
    }
    public void Judge(SensorStatus status)
    {
        if (status == SensorStatus.Off)
        {
            if (On)
                Off = true;
        }
        else
            On = true;
    }
    public void Reset()
    {
        On = false;
        Off = false;
    }
}

public class JudgeArea
{
    public bool On 
    {
        get
        {
            return areas.Any(a => a.On);
        }
    }
    public bool CanSkip = true;
    public bool IsFinished 
    {
        get
        {
            if (areas.Count == 0)
                return false;
            
            return areas.Any(x => x.IsFinished);
        }
    }
    public int SlideIndex { get; set; }
    
    List<Area> areas = new();
    public Area[] GetAreas() => areas.ToArray();
    public SensorArea[] GetSensorTypes() => areas.Select(x => x.Type).ToArray();
    public JudgeArea(List<SensorArea> types, int slideIndex, bool isLast)
    {
        SlideIndex = slideIndex;
        foreach (var type in types)
        {
            if (areas.Any(x => x.Type == type))
                continue;

            areas.Add(new Area()
            {
                Type = type,
                IsLast = isLast
            });
        }
    }
    public void SetIsLast()
    {
        areas.ForEach(x => x.IsLast = true);
    }
    public void SetNonLast()
    {
        areas.ForEach(x => x.IsLast = false);
    }
    public void Judge(SensorArea type, SensorStatus status)
    {
        var areaList = areas.Where(x => x.Type == type);

        if (areaList.Count() == 0)
            return;

        var area = areaList.First();
        area.Judge(status);
    }
    public void AddArea(SensorArea type, bool isLast = false)
    {
        if (areas.Any(x => x.Type == type))
            return;
        areas.Add(new Area()
        {
            Type = type,
            IsLast = isLast
        });
    }
    public void Reset()
    {
        foreach(var area in areas)
            area.Reset();
    }
}
