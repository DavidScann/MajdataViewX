#nullable enable

#region

using UnityEngine;

#endregion

/// <summary>
/// Star note（slide 的头星 / forced-star tap）。继承 <see cref="TapBase"/>。
/// <para>关键差异：旋转、附带 slide 引用、isNoHead 时 head 仅用于叫醒 slide。</para>
/// <para>生命周期：<c>Start (一次性) → Init(info) → Update(running+rotate) + Check(事件) → FixedUpdate(Render) → End(归还池)</c></para>
/// </summary>
public class StarDrop : TapBase
{
    #region Note Data (Init 刷新)
    public float rotateSpeed = 1f;
    public bool isDouble;
    public bool isNoHead;
    public bool isFakeStar = false;
    public bool isFakeStarRotate = false;

    /// <summary>关联的 slide GameObject（由 DataLoader 填入 PoolingInfo）。</summary>
    public GameObject? slide;
    #endregion

    /// <summary>
    /// Awake：池化场景下 Init 在 SetActive(true) 之前调用，依赖注入必须在 Awake 完成。
    /// </summary>
    private void Awake()
    {
        PreLoad();
        LoadSkin();
        spriteRenderer.forceRenderingOff = true;
        exSpriteRender.forceRenderingOff = true;
    }

    /// <summary>
    /// 兼容旧路径：DataLoader.InstantiateSlide 仍然走 Instantiate 并直接设置字段，
    /// 不会调用 <see cref="Init"/>。此处在 Start (首次激活) 时补做 sensor 绑定与状态初始化。
    /// 已通过 Init 走过的实例（_initApplied=true）跳过此逻辑。
    /// </summary>
    private bool _initApplied = false;
    private void Start()
    {
        if (_initApplied) return; // Init 已经把所有事项处理完了
        if (!isNoHead)
        {
            sensor = (SensorType)startPosition - 1;
            inputManager.BindArea(Check, sensor);
            inputBound = true;
        }
        State = NoteStatus.Initialized;
    }

    /// <summary>池化复用入口。</summary>
    public void Init(StarPoolingInfo info)
    {
        _initApplied = true;
        ApplyStarInfo(info);
        ResetSortingOrder(info.NoteSortOrder);
        LoadSkin();
        ResetTapState();
        // 旋转重置（关键：池化复用时旧的旋转状态会脏）
        transform.rotation = Quaternion.identity;

        if (!isNoHead)
        {
            sensor = (SensorType)startPosition - 1;
            inputManager.BindArea(Check, sensor);
            inputBound = true;
        }
        gameObject.SetActive(false);
    }

    private void ApplyStarInfo(StarPoolingInfo info)
    {
        time = info.Time;
        startPosition = info.StartPosition;
        speed = info.Speed;
        rotateSpeed = info.RotateSpeed;
        isEach = info.IsEach;
        isEx = info.IsEx;
        isBreak = info.IsBreak;
        isMine = info.IsMine;
        usingSV = info.UsingSV;
        isDouble = info.IsDouble;
        isNoHead = info.IsNoHead;
        isFakeStar = info.IsFakeStar;
        isFakeStarRotate = info.IsFakeStarRotate;
        slide = info.Slide;
    }

    private void LoadSkin()
    {
        if (isDouble)
        {
            exSpriteRender.sprite = skinManager.Star_Ex_Double;
            spriteRenderer.sprite = skinManager.Star_Double;
            lineSpriteRenderer.sprite = skinManager.Line_Star;
            if (isEx) exSpriteRender.color = skinManager.Ex_Star;
            if (isEach)
            {
                spriteRenderer.sprite = skinManager.Star_Each_Double;
                lineSpriteRenderer.sprite = skinManager.Line_Each;
                if (isEx) exSpriteRender.color = skinManager.Ex_Each;
            }
            if (isBreak)
            {
                spriteRenderer.sprite = skinManager.Star_Break_Double;
                lineSpriteRenderer.sprite = skinManager.Line_Break;
                if (isEx) exSpriteRender.color = skinManager.Ex_Break;
                spriteRenderer.material = skinManager.BreakMaterial;
            }
            if (isMine)
            {
                spriteRenderer.sprite = skinManager.Star_Mine_Double;
                lineSpriteRenderer.sprite = skinManager.Line_Mine;
            }
        }
        else
        {
            exSpriteRender.sprite = skinManager.Star_Ex;
            spriteRenderer.sprite = skinManager.Star;
            lineSpriteRenderer.sprite = skinManager.Line_Star;
            if (isEx) exSpriteRender.color = skinManager.Ex_Star;
            if (isEach)
            {
                spriteRenderer.sprite = skinManager.Star_Each;
                lineSpriteRenderer.sprite = skinManager.Line_Each;
                if (isEx) exSpriteRender.color = skinManager.Ex_Each;
            }
            if (isBreak)
            {
                spriteRenderer.sprite = skinManager.Star_Break;
                lineSpriteRenderer.sprite = skinManager.Line_Break;
                if (isEx) exSpriteRender.color = skinManager.Ex_Break;
                spriteRenderer.material = skinManager.BreakMaterial;
            }
            if (isMine)
            {
                spriteRenderer.sprite = skinManager.Star_Mine;
                lineSpriteRenderer.sprite = skinManager.Line_Mine;
            }
        }
    }

    // ============================== 逻辑（Update：running + 旋转） ==============================
    /// <summary>
    /// 复用 base.UpdateRunning 的判定逻辑（autoplay + miss），
    /// Star 自己加旋转动画（per-frame 视觉，仍归入 Update 中处理旋转输入帧率）。
    /// </summary>
    protected override void Update()
    {
        base.Update(); // 调用 UpdateRunning

        // Star 特有：自身旋转
        var songSpeed = timeProvider.CurrentSpeed;
        if (timeProvider.IsStart && !isFakeStar && rotateSpeed != 0)
            transform.Rotate(0f, 0f, -180f * Time.deltaTime * songSpeed / rotateSpeed);
        else if (isFakeStarRotate)
            transform.Rotate(0f, 0f, 400f * Time.deltaTime);
    }

    // ============================== 渲染（FixedUpdate：Render） ==============================
    protected override void Render()
    {
        var timing = timeProvider.NoteTime - time;
        var distance = timing * speed + 4.8f;
        var destScale = distance * 0.4f + 0.51f;

        var fakeTiming = timeProvider.FakeNoteTime - timeProvider.GetPositionAtTime(time);
        var fakeDistance = fakeTiming * speed + 4.8f;
        var fakeDestScale = fakeDistance * 0.4f + 0.51f;

        if (!usingSV)
        {
            //fakeTiming = timing;
            fakeDistance = distance;
            fakeDestScale = destScale;
        }

        switch (State)
        {
            case NoteStatus.Initialized:
                if (fakeDestScale >= 0f)
                {
                    if (!isNoHead)
                        tapLine.transform.rotation = Quaternion.Euler(0, 0, -22.5f + -45f * (startPosition - 1));
                    State = NoteStatus.Pending;
                    goto case NoteStatus.Pending;
                }
                transform.localScale = new Vector3(0, 0);
                return;
            case NoteStatus.Pending:
                {
                    if (fakeDestScale > 0.3f && !isNoHead)
                        tapLine.SetActive(true);
                    if (fakeDistance < 1.225f)
                    {
                        transform.localScale = new Vector3(fakeDestScale, fakeDestScale);
                        transform.position = getPositionFromDistance(1.225f);
                        var lineScale = Mathf.Abs(1.225f / 4.8f);
                        tapLine.transform.localScale = new Vector3(lineScale, lineScale, 1f);
                    }
                    else
                    {
                        if (!isFakeStar && slide != null && !slide.activeSelf)
                        {
                            slide.SetActive(true);
                            if (isNoHead)
                            {
                                DestroySelf();
                                return;
                            }
                        }
                        State = NoteStatus.Running;
                        goto case NoteStatus.Running;
                    }
                }
                break;
            case NoteStatus.Running:
                {
                    transform.position = getPositionFromDistance(fakeDistance);
                    transform.localScale = new Vector3(1f, 1f);
                    var lineScale = Mathf.Abs(fakeDistance / 4.8f);
                    tapLine.transform.localScale = new Vector3(lineScale, lineScale, 1f);
                }
                break;
        }

        if (isNoHead)
        {
            spriteRenderer.forceRenderingOff = true;
            if (isEx) exSpriteRender.forceRenderingOff = true;
        }
        else
        {
            spriteRenderer.forceRenderingOff = false;
            if (isEx) exSpriteRender.forceRenderingOff = false;
        }
    }

    // ============================== 销毁 / End ==============================
    protected override void DestroySelf()
    {
        if (!isNoHead || isFakeStar)
        {
            // 走完整的 Tap 销毁流程（含播放 sfx）
            base.DestroySelf();
        }
        else
        {
            // 无头星只是叫醒 slide 用，不上报判定也不播 sfx
            noteManager.RemoveLoadedNote(this);
            End();
        }
    }

    /// <summary>
    /// 上报：StarDrop 走 SimaiNoteType.Slide 通道，且 isNoHead 时跳过统计（无头星无判定）。
    /// </summary>
    protected override void ReportResult()
    {
        if (PlayManager.IsReloading) return;
        if (isNoHead) return; // 无头星不上报
        base.ReportResult();
    }

    protected override void OnDestroy()
    {
        if (isNoHead) return;
        base.OnDestroy();
    }
}
