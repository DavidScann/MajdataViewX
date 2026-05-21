#nullable enable

#region

using System.Collections.Generic;
using UnityEngine;

#endregion

/// <summary>
/// 通用 Note 池化器：按 prefab 分组，使用 <see cref="Stack{T}"/> 复用 GameObject。
/// <para>API：<see cref="Get"/> / <see cref="Release"/> / <see cref="Prewarm"/>。</para>
/// <para>life-cycle：从池中取出 → 调用 <c>Init(info)</c> → 播放 → 调用 <c>End()</c> → 归还。</para>
/// <para>EDITOR 下用 <c>_inPoolSet</c> 防重复回收。</para>
/// </summary>
public class NotePool : MonoBehaviour
{
    private static NotePool? _instance;

    public static NotePool Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[NotePool]");
                _instance = go.AddComponent<NotePool>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private readonly Dictionary<int, Stack<GameObject>> _pools = new();
    private readonly Dictionary<int, Transform> _roots = new();

#if UNITY_EDITOR
    private readonly HashSet<GameObject> _inPoolSet = new();
#endif

    /// <summary>
    /// 从池子取出一个实例（如池为空则 Instantiate）。返回时 GameObject 处于 inactive 状态。
    /// </summary>
    public GameObject Get(GameObject prefab, Transform parent)
    {
        if (prefab == null) return null!;
        var key = prefab.GetInstanceID();

        if (!_pools.TryGetValue(key, out var stack))
        {
            stack = new Stack<GameObject>();
            _pools[key] = stack;
        }

        GameObject instance;
        if (stack.Count > 0)
        {
            instance = stack.Pop();
#if UNITY_EDITOR
            _inPoolSet.Remove(instance);
#endif
            instance.transform.SetParent(parent, false);
        }
        else
        {
            instance = Instantiate(prefab, parent);
        }
        instance.SetActive(false);
        return instance;
    }

    /// <summary>
    /// 把实例归还到池中。会立即 SetActive(false) 并停在 _roots 中（不依附场景层级）。
    /// </summary>
    public void Release(GameObject prefab, GameObject instance)
    {
        if (prefab == null || instance == null) return;
        var key = prefab.GetInstanceID();

        if (!_pools.TryGetValue(key, out var stack))
        {
            stack = new Stack<GameObject>();
            _pools[key] = stack;
        }

#if UNITY_EDITOR
        if (_inPoolSet.Contains(instance))
        {
            Debug.LogError($"[NotePool] Double-release detected: {instance.name}");
            return;
        }
        _inPoolSet.Add(instance);
#endif

        if (!_roots.TryGetValue(key, out var root))
        {
            var rootGo = new GameObject($"Pool_{prefab.name}");
            rootGo.transform.SetParent(transform);
            rootGo.SetActive(false); // 整个池根 inactive，回池实例不参与场景遍历
            root = rootGo.transform;
            _roots[key] = root;
        }

        instance.SetActive(false);
        instance.transform.SetParent(root, false);
        stack.Push(instance);
    }

    /// <summary>
    /// 预热：提前实例化指定数量并放入池中，避免运行时首批 Instantiate 抖动。
    /// </summary>
    public void Prewarm(GameObject prefab, Transform parent, int count)
    {
        if (prefab == null || count <= 0) return;
        for (var i = 0; i < count; i++)
        {
            var instance = Instantiate(prefab, parent);
            Release(prefab, instance);
        }
    }

    /// <summary>
    /// 清空所有池子（场景切换/重载使用）。会销毁所有缓存的 GameObject。
    /// </summary>
    public void ClearAll()
    {
        foreach (var stack in _pools.Values)
        {
            while (stack.Count > 0)
            {
                var obj = stack.Pop();
                if (obj != null) Destroy(obj);
            }
        }
        _pools.Clear();
        foreach (var root in _roots.Values)
        {
            if (root != null) Destroy(root.gameObject);
        }
        _roots.Clear();
#if UNITY_EDITOR
        _inPoolSet.Clear();
#endif
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
}
