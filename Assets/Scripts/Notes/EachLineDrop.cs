#nullable enable

#region

using UnityEngine;

#endregion

/// <summary>
/// each-line：连接同时刻多个 note 的弧线，无判定逻辑，仅渲染。
/// <para>生命周期：<c>Start(注入+取 SpriteRenderer) → Init(刷新数据/状态) → FixedUpdate(Render) → End(归还池)</c></para>
/// </summary>
public class EachLineDrop : MonoBehaviour
{
    #region Injected Dependencies (Start 一次性)
    private TimeProvider timeProvider;
    private SpriteRenderer spriteRenderer;
    #endregion

    #region Pool Reference
    [System.NonSerialized] public GameObject? prefabRef;
    #endregion

    #region Note Data (Init 刷新)
    public float time;
    public int startPosition;
    public float speed;
    public bool UsingSV;
    public int curvLength;

    [SerializeField] Sprite[] curvSprites;
    #endregion

    #region Runtime State (Init 重置)
    private bool isFinished;
    #endregion

    /// <summary>
    /// Awake：池化场景下 Init 在 SetActive(true) 之前调用，依赖注入必须在 Awake 完成。
    /// </summary>
    private void Awake()
    {
        timeProvider = Majdata<TimeProvider>.Instance!;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    /// <summary>
    /// 池化每次复用时调用：刷新数据并重置渲染状态。
    /// </summary>
    public void Init(EachLinePoolingInfo info)
    {
        time = info.Time;
        startPosition = info.StartPosition;
        speed = info.Speed;
        UsingSV = info.UsingSV;
        curvLength = info.CurvLength;

        spriteRenderer.sprite = curvSprites[curvLength - 1];
        spriteRenderer.forceRenderingOff = true;
        isFinished = false;
        gameObject.SetActive(false);
    }

    // —— Render: each-line 没有判定逻辑，只在 FixedUpdate 中绘制
    private void FixedUpdate()
    {
        if (isFinished) return;
        Render();
    }

    private void Render()
    {
        var timing = timeProvider.NoteTime - time;
        if (timing > 0)
        {
            isFinished = true;
            End();
            return;
        }
        var distance = timing * speed + 4.8f;
        var destScale = distance * 0.4f + 0.51f;

        var fakeTiming = timeProvider.FakeNoteTime - timeProvider.GetPositionAtTime(time);
        var fakeDistance = fakeTiming * speed + 4.8f;
        var fakeDestScale = fakeDistance * 0.4f + 0.51f;

        if (!UsingSV)
        {
            //fakeTiming = timing;
            fakeDistance = distance;
            fakeDestScale = destScale;
        }

        transform.rotation = Quaternion.Euler(0, 0, -45f * (startPosition - 1));

        if (fakeDestScale > 0.3f)
            spriteRenderer.forceRenderingOff = false;
        if (fakeDistance < 1.225f)
        {
            transform.localScale = new Vector3(1.225f / 4.8f, 1.225f / 4.8f, 1f);
            return;
        }

        var lineScale = Mathf.Abs(fakeDistance / 4.8f);
        transform.localScale = new Vector3(lineScale, lineScale, 1f);
    }

    /// <summary>
    /// 池化结束：禁用渲染并归还到池。
    /// </summary>
    public void End()
    {
        spriteRenderer.forceRenderingOff = true;
        if (prefabRef != null)
            NotePool.Instance.Release(prefabRef, gameObject);
        else
            Destroy(gameObject); // fallback：未走池化创建
    }
}
