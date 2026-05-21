#nullable enable

#region

using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

#endregion

public class NoteManager : MonoBehaviour
{
    public List<NoteBase> LoadedNotes { get; private set; } = new();

    private Dictionary<GameObject, int> noteOrder = new();
    private Dictionary<int, int> noteIndex = new();

    private Dictionary<GameObject, int> touchOrder = new();
    private Dictionary<SensorType, int> touchIndex = new();

    private void Awake()
    {
        Majdata<NoteManager>.Instance = this;
    }

    public void AddLoadedNote(NoteBase note)
    {
        LoadedNotes.Add(note);
    }
    public void AddNote(NoteBase note, int index)
    {
        // 池化场景下：同一 GameObject 可能跨多个 timing 复用，因此用 indexer 覆盖（原 Add 会抛重复键异常）
        noteOrder[note.gameObject] = index;
    }
    public void AddTouch(NoteBase note, int index)
    {
        touchOrder[note.gameObject] = index;
    }

    public void NextNote(int pos) => noteIndex[pos]++;
    public void NextTouch(SensorType pos) => touchIndex[pos]++;

    public void RemoveLoadedNote(NoteBase note)
    {
        LoadedNotes.Remove(note);
    }

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
        // 池化路径：先把活跃 note 通过 End() 回收到池，避免 Destroy 浪费实例；
        // 已池化的 NoteBase override 了 End() 把自己 Release 回 NotePool；
        // 未池化的 fallback 到 Destroy（NoteBase.End 默认行为）。
        var snapshot = LoadedNotes.ToArray();
        foreach (var note in snapshot)
        {
            if (note != null)
            {
                try { note.End(); }
                catch (System.Exception e) { Debug.LogWarning($"[NoteManager] End() failed: {e.Message}"); }
            }
        }
        LoadedNotes.Clear();
        noteOrder.Clear();
        touchOrder.Clear();
        ResetIndex();

        //clear notes：兜底——任何未通过 End() 归还的 GameObject 直接销毁
        for (var i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
        await UniTask.WaitUntil(() => transform.childCount == 0);

        PlayManager.IsReloading = false;
    }
}