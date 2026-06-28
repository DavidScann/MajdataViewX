#nullable enable

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

using static MajCtx;

public class JudgeManager : MonoBehaviour
{
    public const int SENSOR_COUNT = 34;

    public NativeArray<SensorState> states = new(SENSOR_COUNT, Allocator.Persistent);
    public NativeArray<int> nextIndex = new(SENSOR_COUNT, Allocator.Persistent);

    public unsafe SensorState* StatesPtr => (SensorState*)states.GetUnsafePtr();
    public unsafe int* NextIndexPtr => (int*)nextIndex.GetUnsafePtr();

    private Dictionary<GameObject, int> noteOrder = new();
    private Dictionary<GameObject, int> touchOrder = new();
    private Dictionary<int, int> noteIndex = new();
    private Dictionary<SensorType, int> touchIndex = new();

    private void Awake()
    {
        _judgeManager = this;
        unsafe
        {
            NoteHelper.SensorStates = StatesPtr;
            NoteHelper.NextSensorIndex = NextIndexPtr;
        }
        InitSensorWorldPositions();
    }

    private void InitSensorWorldPositions()
    {
        var sensorsObj = GameObject.Find("Sensors");
    }

    private void OnDestroy()
    {
        if (states.IsCreated) states.Dispose();
        if (nextIndex.IsCreated) nextIndex.Dispose();
    }

    public void SetState(SensorType type, SensorStatus status)
    {
        states[(int)type] = new SensorState
        {
            Status = status,
            IsJudging = states[(int)type].IsJudging
        };
    }

    public void SetBusy(SensorType type, bool isButton)
    {
        var idx = (int)type;
        var s = states[idx];
        s.IsJudging = true;
        states[idx] = s;
    }

    public bool IsIdle(SensorType type)
    {
        return !states[(int)type].IsJudging;
    }

    public void AddNote(GameObject obj, int index) => noteOrder[obj] = index;
    public void AddTouch(GameObject obj, int index) => touchOrder[obj] = index;

    public void NextNote(int pos)
    {
        if (!noteIndex.ContainsKey(pos))
            noteIndex[pos] = 0;
        noteIndex[pos]++;
    }

    public void NextTouch(SensorType pos)
    {
        if (!touchIndex.ContainsKey(pos))
            touchIndex[pos] = 0;
        touchIndex[pos]++;
    }

    public bool CanJudge(GameObject obj, int pos)
    {
        if (!noteOrder.ContainsKey(obj))
            return false;
        if (!noteIndex.ContainsKey(pos))
            noteIndex[pos] = 0;
        return noteOrder[obj] <= noteIndex[pos];
    }

    public bool CanJudge(GameObject obj, SensorType t)
    {
        if (!touchOrder.ContainsKey(obj))
            return false;
        if (!touchIndex.ContainsKey(t))
            touchIndex[t] = 0;
        return touchOrder[obj] <= touchIndex[t];
    }

    public void ResetIndex()
    {
        for (var i = 1; i < 9; i++)
            noteIndex[i] = 0;
        for (var i = 0; i < 33; i++)
            touchIndex[(SensorType)i] = 0;

        for (var i = 0; i < SENSOR_COUNT; i++)
            nextIndex[i] = 0;
    }

    public async UniTask ResetState()
    {
        noteOrder.Clear();
        touchOrder.Clear();
        ResetIndex();

        for (var i = 0; i < SENSOR_COUNT; i++)
            states[i] = default;

        for (var i = 0; i < transform.childCount; i++)
            Destroy(transform.GetChild(i).gameObject);
        await UniTask.WaitUntil(() => transform.childCount == 0);

        PlayManager.IsReloading = false;
    }
}

public struct SensorState
{
    public SensorStatus Status;
    public bool IsJudging;
}
