using System.Collections.Generic;
using UnityEngine;

using static MajCtx;

public class NoteViewPoolManager : MonoBehaviour
{
    [SerializeField] private GameObject noteViewPrefab;
    [SerializeField] private int initialSize;
    [SerializeField] private Transform poolRoot;

    private readonly List<NoteView> _views = new();
    private readonly Queue<int> _freeIds = new();

    private void Awake()
    {
        _noteViewPoolManager = this;
        PreWarm();
    }

    private void PreWarm()
    {
        for (int i = 0; i < initialSize; i++)
        {
            int id = Create();
            _freeIds.Enqueue(id);
        }
    }

    private int Create()
    {
        var go = Instantiate(noteViewPrefab, poolRoot);
        var sr = go.GetComponent<SpriteRenderer>();

        var mat = Instantiate(sr.sharedMaterial);
        sr.material = mat;

        var note = new NoteView
        {
            GameObject = go,
            Transform = go.transform,
            SpriteRenderer = sr,
            Material = mat
        };

        int id = _views.Count;
        _views.Add(note);

        return id;
    }

    /// <summary>
    /// 获取一个 viewId（绑定 NoteData 用）并初始化层级
    /// </summary>
    public int Get(string sortingLayer, int sortingOrder)
    {
        int id;

        if (_freeIds.Count > 0)
        {
            id = _freeIds.Dequeue();
        }
        else
        {
            id = Create();
        }

        var view = _views[id];

        view.SpriteRenderer.sortingOrder = sortingOrder;
        view.SpriteRenderer.sortingLayerName = sortingLayer;
        view.SpriteRenderer.enabled = true;

        return id;
    }

    /// <summary>
    /// 获取 View 实例（用于更新）
    /// </summary>
    public NoteView GetView(int id)
    {
        if (id < 0) return null;

        return _views[id];
    }

    /// <summary>
    /// 释放 viewId
    /// </summary>
    public void Release(int id)
    {
        if (id < 0 || id >= _views.Count) return;

        var view = _views[id];

        view.Reset();

        _freeIds.Enqueue(id);
    }
}