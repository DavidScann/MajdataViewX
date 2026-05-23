#nullable enable

#region

using System;
using MajSimai;
using UnityEngine;
using Random = UnityEngine.Random;

#endregion

/// <summary>
/// Tap 系列(TapDrop / StarDrop)的基类。
/// <para>统一生命周期：<c>Start(PreLoad 一次性) → Init(每次刷新) → Update(running+check) → FixedUpdate(Render) → End(归还池)</c>。</para>
/// <para>子类负责：在 Start 中调用 <see cref="PreLoad"/> 并各自处理 LoadSkin；在 Init 时通过 <see cref="ApplyTapInfo"/> + 重置 state 完成复用。</para>
/// </summary>
public class TapBase : NoteBase
{
    public GameObject tapLine;

    protected SpriteRenderer spriteRenderer;
    protected SpriteRenderer exSpriteRender;
    protected SpriteRenderer lineSpriteRenderer;

    /// <summary>原 prefab 引用，子类 Start 时存下用于子对象池化（tapLine）。</summary>
    [SerializeField] protected GameObject tapLinePrefab;

    protected bool isTriggered = false;
    /// <summary>已订阅 InputManager 的标记，防止 End 时漏解绑或重复解绑。</summary>
    protected bool inputBound = false;

    /// <summary>
    /// Start 阶段一次性获取依赖与子对象。
    /// </summary>
    protected void PreLoad()
    {
        notes = GameObject.Find("Notes");
        noteManager = Majdata<NoteManager>.Instance!;
        timeProvider = Majdata<TimeProvider>.Instance!;
        objectCounter = Majdata<ObjectCounter>.Instance!;
        inputManager = Majdata<InputManager>.Instance!;
        skinManager = Majdata<SkinManager>.Instance!;
        audioManager = Majdata<AudioManager>.Instance!;

        tapLinePrefab = Majdata<DataLoader>.Instance!.tapLine;

        spriteRenderer = GetComponent<SpriteRenderer>();
        exSpriteRender = transform.GetChild(0).GetComponent<SpriteRenderer>();
    }

    /// <summary>
    /// 把 NoteBase 公共字段 + tap 渲染层级 一并应用。子类的 Init 在调用此方法后应额外重置自己的状态。
    /// </summary>
    protected void ApplyTapInfoCommon(int sortOrder)
    {
        ResetSortingOrder(sortOrder);
    }

    protected void GetTapLine()
    {
        tapLine = NotePool.Instance.Get(tapLinePrefab, notes.transform);
        tapLine.SetActive(false);
        lineSpriteRenderer = tapLine.GetComponent<SpriteRenderer>();
    }
    /// <summary>
    /// 重置 sortingOrder：原版只在 PreLoad 中 +=，池化时每次 Init 都要重新设置。
    /// 使用绝对值（避免反复 += 累积）。子类 StarDrop 也复用。
    /// </summary>
    private int _baseSpriteOrder = int.MinValue;
    private int _baseExOrder = int.MinValue;
    protected void ResetSortingOrder(int order)
    {
        if (_baseSpriteOrder == int.MinValue)
        {
            _baseSpriteOrder = spriteRenderer.sortingOrder;
            _baseExOrder = exSpriteRender.sortingOrder;
        }
        spriteRenderer.sortingOrder = _baseSpriteOrder + order;
        exSpriteRender.sortingOrder = _baseExOrder + order;
        noteSortOrder = order;
    }

    /// <summary>每次复用前重置 Tap 共有的运行时状态。</summary>
    protected void ResetTapState()
    {
        State = NoteStatus.Initialized;
        isJudged = false;
        judgeResult = JudgeType.Miss;
        isTriggered = false;
        inputBound = false;

        // 重置渲染态：关掉显示，等到 fakeDestScale 大于阈值再显示
        spriteRenderer.forceRenderingOff = true;
        if (isEx) exSpriteRender.forceRenderingOff = true;
        spriteRenderer.material.SetFloat("_Brightness", 0.95f);

        transform.localScale = new Vector3(0, 0);
        tapLine.SetActive(false);
    }

    // ============================== 逻辑：running + check ==============================
    /// <summary>
    /// Update：处理 autoplay running 与 timing-based check（miss 检测）。
    /// 由原 FixedUpdate 迁移而来——按照 refactor.md，逻辑在 Update、Render 在 FixedUpdate。
    /// </summary>
    protected virtual void Update()
    {
        UpdateRunning();
        // Check 是事件驱动（InputManager.BindArea），不在这里直接调用
    }

    /// <summary>状态机推进 + autoplay + 自动 miss 检测。</summary>
    protected void UpdateRunning()
    {
        var timing = timeProvider.NoteTime - time;
        if (isMine && !isJudged && timing >= 0.016667f)
        {
            judgeResult = JudgeType.Perfect;
            isJudged = true;
        }
        else if (!isJudged && timing > 0.15f)
        {
            judgeResult = JudgeType.Miss;
            isJudged = true;
            DestroySelf();
            return;
        }
        else if (isJudged)
        {
            DestroySelf();
            return;
        }
        else if (timing >= -0.01f)
        {
            switch (Majdata<InputManager>.Instance!.Mode)
            {
                case AutoPlayMode.Enable:
                    if (isMine)
                        judgeResult = JudgeType.Miss;
                    else
                        judgeResult = JudgeType.Perfect;
                    isJudged = true;
                    break;
                case AutoPlayMode.Random:
                    judgeResult = (JudgeType)Random.Range(1, 14);
                    if (isMine)
                    {
                        if (judgeResult > JudgeType.Perfect) //Fast
                            judgeResult = JudgeType.Miss;
                        else
                            judgeResult = JudgeType.Perfect;
                    }
                    isJudged = true;
                    break;
                case AutoPlayMode.DJAuto:
                    if (isTriggered) break;
                    //mine就不打了
                    if (!isMine)
                        inputManager.ClickSensor(sensor);
                    isTriggered = true;
                    break;
            }
        }
    }

    // ============================== 渲染：状态机视觉表现 ==============================
    /// <summary>
    /// FixedUpdate：渲染（位置/缩放/material 高亮）。
    /// 之所以放 FixedUpdate，是按照 refactor.md 的统一规范——所有 note 的 Render 步骤进入 FixedUpdate。
    /// </summary>
    protected virtual void FixedUpdate()
    {
        Render();
    }

    /// <summary>渲染：状态机驱动的位置/缩放/Line。</summary>
    protected virtual void Render()
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
                    tapLine.transform.rotation = Quaternion.Euler(0, 0, -22.5f + -45f * (startPosition - 1));
                    State = NoteStatus.Pending;
                    goto case NoteStatus.Pending;
                }
                transform.localScale = new Vector3(0, 0);
                return;
            case NoteStatus.Pending:
                {
                    if (fakeDestScale > 0.3f)
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

        spriteRenderer.forceRenderingOff = false;
        if (isEx) exSpriteRender.forceRenderingOff = false;
        if (isBreak)
        {
            var extra = Math.Max(Mathf.Sin(timeProvider.GetFrame() * 0.17f) * 0.5f, 0);
            spriteRenderer.material.SetFloat("_Brightness", 0.95f + extra);
        }
    }

    // ============================== 输入派发 ==============================
    /// <summary>
    /// 由 InputManager 触发：玩家点击对应 sensor 时调用。
    /// </summary>
    protected void Check(object sender, InputEventArgs arg)
    {
        if (arg.Type != sensor)
            return;
        if (isJudged || !noteManager.CanJudge(gameObject, startPosition))
            return;
        if (Majdata<InputManager>.Instance!.Mode is AutoPlayMode.Enable or AutoPlayMode.Random)
            return;

        if (arg.IsClick)
        {
            if (!inputManager.IsIdle(arg))
                return;
            inputManager.SetBusy(arg);
            Judge();
        }
    }

    /// <summary>判定计算（玩家点击触发）。</summary>
    protected void Judge()
    {
        const int JUDGE_GOOD_AREA = 150;
        const int JUDGE_GREAT_AREA = 100;
        const int JUDGE_PERFECT_AREA = 50;

        const float JUDGE_SEG_PERFECT1 = 16.66667f;
        const float JUDGE_SEG_PERFECT2 = 33.33334f;
        const float JUDGE_SEG_GREAT1 = 66.66667f;
        const float JUDGE_SEG_GREAT2 = 83.33334f;

        if (isJudged)
            return;

        if (isMine)
        {
            judgeResult = JudgeType.Miss;
            isJudged = true;
            return;
        }

        var timing = timeProvider.NoteTime - time;
        var isFast = timing < 0;
        var diff = MathF.Abs(timing * 1000);
        JudgeType result;
        if (diff > JUDGE_GOOD_AREA && isFast)
            return;
        else if (diff < JUDGE_SEG_PERFECT1)
            result = JudgeType.Perfect;
        else if (diff < JUDGE_SEG_PERFECT2)
            result = JudgeType.LatePerfect1;
        else if (diff < JUDGE_PERFECT_AREA)
            result = JudgeType.LatePerfect2;
        else if (diff < JUDGE_SEG_GREAT1)
            result = JudgeType.LateGreat;
        else if (diff < JUDGE_SEG_GREAT2)
            result = JudgeType.LateGreat1;
        else if (diff < JUDGE_GREAT_AREA)
            result = JudgeType.LateGreat;
        else if (diff < JUDGE_GOOD_AREA)
            result = JudgeType.LateGood;
        else
            result = JudgeType.Miss;

        if (result != JudgeType.Miss && isFast)
            result = 14 - result;
        if (result != JudgeType.Miss && isEx)
            result = JudgeType.Perfect;

        judgeResult = result;
        isJudged = true;
    }

    // ============================== 销毁 / End ==============================
    /// <summary>
    /// 完成判定后销毁/归还。原版直接 Destroy；池化版本走 End()。
    /// </summary>
    protected virtual void DestroySelf()
    {
        audioManager.PlayTapSound(judgeResult, isEx, isBreak);
        noteManager.RemoveLoadedNote(this);
        End();
    }

    /// <summary>
    /// 池化结束：上报判定结果、解绑事件、归还到池。
    /// </summary>
    public override void End()
    {
        ReportResult();
        UnbindInput();

        // tapLine 也回池（独立 prefab 的池子）
        NotePool.Instance.Release(tapLinePrefab, tapLine);
        tapLine = null!;
        NotePool.Instance.Release(prefabRef, gameObject);
    }

    /// <summary>上报判定结果给 effect/object counter/note manager。</summary>
    protected virtual void ReportResult()
    {
        if (PlayManager.IsReloading) return;
        var effectManager = Majdata<EffectManager>.Instance!;
        effectManager.PlayEffect(startPosition, isBreak, judgeResult);
        effectManager.PlayFastLate(startPosition, judgeResult);
        noteManager.NextNote(startPosition);
        objectCounter.ReportResult(SimaiNoteType.Tap, judgeResult, isBreak);
    }

    /// <summary>解绑 inputManager 订阅，确保池化复用时不会泄漏 handler。</summary>
    protected void UnbindInput()
    {
        if (!inputBound) return;
        inputManager.UnbindArea(Check, sensor);
        inputBound = false;
    }

    /// <summary>场景销毁兜底（非池化场景）：保持原 Destroy 时的上报与解绑行为。</summary>
    protected virtual void OnDestroy()
    {
        if (PlayManager.IsReloading) return;
        // 如果走过 End()，inputBound 已为 false，不会重复 Unbind
        UnbindInput();
    }
}
