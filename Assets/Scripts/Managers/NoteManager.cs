#nullable enable

#region

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

using static MajCtx;

#endregion

public class NoteManager : MonoBehaviour
{
    private Dictionary<GameObject, int> noteOrder = new();
    private Dictionary<int, int> noteIndex = new();

    private Dictionary<GameObject, int> touchOrder = new();
    private Dictionary<SensorType, int> touchIndex = new();

    private void Awake()
    {
        _noteManager = this;
    }

    public void AddNote(NoteBase note, int index)
    {
        noteOrder.Add(note.gameObject, index);
    }
    public void AddTouch(NoteBase note, int index)
    {
        touchOrder.Add(note.gameObject, index);
    }

    public void NextNote(int pos) => noteIndex[pos]++;
    public void NextTouch(SensorType pos) => touchIndex[pos]++;

    public void ResetIndex()
    {
        for (var i = 1; i < 9; i++)
            noteIndex[i] = 0;
        for (var i = 0; i < 33; i++)
            touchIndex[(SensorType)i] = 0;
    }
    public bool CanJudge(GameObject obj, int pos)
    {
        if (!noteOrder.ContainsKey(obj))
            return false;
        var index = noteOrder[obj];
        var nowIndex = noteIndex[pos];

        return index <= nowIndex;
    }

    public bool CanJudge(GameObject obj, SensorType t)
    {
        if (!touchOrder.ContainsKey(obj))
            return false;
        var index = touchOrder[obj];
        var nowIndex = touchIndex[t];

        return index <= nowIndex;
    }

    public async UniTask ResetState()
    {
        noteOrder.Clear();
        touchOrder.Clear();
        ResetIndex();

        //clear notes
        for (var i = 0; i < transform.childCount; i++)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
        await UniTask.WaitUntil(() => transform.childCount == 0);

        PlayManager.IsReloading = false;
    }
}