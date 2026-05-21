#nullable enable

#region

using System.Collections.Generic;
using UnityEngine;

#endregion

/// <summary>
/// 专门为 Slide 箭头(arrow)设计的子对象池。
/// <para>所有 slide shape 的 arrow 都使用同一个 <c>Slide_Arrow</c> prefab，箭头的位姿(<see cref="SlideArrowTable.ArrowPose"/>)
/// 由 <see cref="SlideArrowTable"/> 提供，运行时由 <see cref="SlideDrop"/> 摆放。</para>
/// <para>使用 <see cref="Stack{T}"/> 复用，单池子大量复用，避免每个 slide 实例化几十个 arrow GameObject。</para>
/// </summary>
public class ArrowPool : MonoBehaviour
{
    private static ArrowPool? _instance;
    public static ArrowPool Instance => _instance ??= EnsureInstance();

    private static ArrowPool EnsureInstance()
    {
        var go = new GameObject("[ArrowPool]");
        var pool = go.AddComponent<ArrowPool>();
        DontDestroyOnLoad(go);
        return pool;
    }

    private GameObject? _arrowPrefab;
    private readonly Stack<GameObject> _stack = new();
    private Transform? _poolRoot;

#if UNITY_EDITOR
    private readonly HashSet<GameObject> _inPoolSet = new();
#endif

    /// <summary>
    /// 注入 arrow prefab。一般由 <see cref="DataLoader"/> 在场景启动时调用一次。
    /// </summary>
    public void RegisterPrefab(GameObject prefab)
    {
        _arrowPrefab = prefab;
        if (_poolRoot == null)
        {
            var rootGo = new GameObject("Pool_Arrow");
            rootGo.transform.SetParent(transform);
            rootGo.SetActive(false);
            _poolRoot = rootGo.transform;
        }
    }

    /// <summary>
    /// 取出一个 arrow。调用方负责 SetParent / 设置位姿 / SetActive。
    /// </summary>
    public GameObject Get(Transform parent)
    {
        if (_arrowPrefab == null)
        {
            Debug.LogError("[ArrowPool] Prefab not registered. Call RegisterPrefab first.");
            return null!;
        }

        GameObject arrow;
        if (_stack.Count > 0)
        {
            arrow = _stack.Pop();
#if UNITY_EDITOR
            _inPoolSet.Remove(arrow);
#endif
        }
        else
        {
            arrow = Instantiate(_arrowPrefab);
        }
        arrow.transform.SetParent(parent, false);
        arrow.SetActive(false);
        return arrow;
    }

    /// <summary>
    /// 一次性获取多个 arrow，用于一个 slide 一次性摆放完所有 child。
    /// </summary>
    public void GetMany(Transform parent, int count, List<GameObject> output)
    {
        for (var i = 0; i < count; i++)
            output.Add(Get(parent));
    }

    /// <summary>
    /// 归还单个 arrow。会重置 transform 父级并 SetActive(false)。
    /// </summary>
    public void Release(GameObject arrow)
    {
        if (arrow == null || _poolRoot == null) return;

#if UNITY_EDITOR
        if (_inPoolSet.Contains(arrow))
        {
            Debug.LogError($"[ArrowPool] Double-release detected: {arrow.name}");
            return;
        }
        _inPoolSet.Add(arrow);
#endif

        arrow.SetActive(false);
        arrow.transform.SetParent(_poolRoot, false);

        // 重置 SpriteRenderer 状态，防止脏数据
        if (arrow.TryGetComponent<SpriteRenderer>(out var sr))
        {
            sr.color = Color.white;
            sr.enabled = true;
        }
        _stack.Push(arrow);
    }

    /// <summary>
    /// 批量归还。
    /// </summary>
    public void ReleaseMany(List<GameObject> arrows)
    {
        for (var i = 0; i < arrows.Count; i++)
            Release(arrows[i]);
        arrows.Clear();
    }

    public void Prewarm(int count)
    {
        if (_arrowPrefab == null || _poolRoot == null) return;
        for (var i = 0; i < count; i++)
        {
            var arrow = Instantiate(_arrowPrefab, _poolRoot);
            Release(arrow);
        }
    }

    public void ClearAll()
    {
        while (_stack.Count > 0)
        {
            var obj = _stack.Pop();
            if (obj != null) Destroy(obj);
        }
#if UNITY_EDITOR
        _inPoolSet.Clear();
#endif
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
}
