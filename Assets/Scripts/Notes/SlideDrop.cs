#nullable enable

#region

using System;
using System.Collections.Generic;
using System.Linq;
using MajSimai;
using UnityEngine;
using Random = UnityEngine.Random;

#endregion

/// <summary>
/// Slide note：复杂的轨迹型 note，包含多段判定区(<see cref="SlideArea"/>)、引导星(<see cref="star_slide"/>)、
/// 箭头组(<see cref="slideBars"/>)、判定显示(<see cref="slideOK"/>)、连接星星(ConnSlide)。
/// <para>生命周期：<c>Awake (注入依赖) → Init(info) (刷新数据/重置状态/调用 Initialize) →
/// Update (running+check) → FixedUpdate (Render) → End (归还池)</c>。</para>
/// <para>箭头摆放数据：<see cref="SlideArrowTable"/> 提供静态 ArrowPose；当前实现仍使用 prefab 上的
/// arrow 子对象（保留旧渲染），后续可改用 <see cref="ArrowPool"/> 完成"单 prefab + 字典定位"的目标。</para>
/// </summary>
public class SlideDrop : NoteLongBase, ICanShine
{
    public float SlideBarAlpha; //FOR ANIMATOER
    #region Note Data (Init 刷新)
    public int endPosition;
    public bool isMirror;
    public bool isJustR;
    public bool isSpecialFlip; // fixes known star problem
    public float startTime;
    public int sortIndex;
    public ConnSlideInfo ConnectInfo = new();
    public List<int> areaStep = new();
    public bool smoothSlideAnime = false;
    public string slideType;
    #endregion

    #region Pooled Children (DataLoader 注入 prefab refs)
    public GameObject star_slide;
    [NonSerialized] public GameObject starSlidePrefab;
    private SpriteRenderer starRenderer;
    private GameObject slideOK;
    #endregion

    #region Runtime State (Init 重置)
    private float arriveTime = -1;
    private List<SensorType> boundSensors = new();
    private List<SensorType> triggerSensors = new(); // AutoPlay; 标记已触发的Sensor
    private List<SlideArea> judgeQueue = new();      // 判定队列(目前剩余的)
    private List<SlideArea> _judgeQueue = new();     // 判定队列原始拷贝

    public bool IsFinished => judgeQueue.Count == 0;
    public bool IsPendingFinish => judgeQueue.Count == 1;

    private readonly List<GameObject> slideBars = new();

    private readonly List<SpriteRenderer> slideBarsRenderer = new();

    private readonly List<Vector3> slidePositions = new();
    private readonly List<Quaternion> slideRotations = new();
    private Animator fadeInAnimator;

    bool canShine = false;
    bool canCheck = false;
    bool isChecking = false;
    float fadeInTime;
    float judgeTiming; // 正解帧
    float forceJudgeTime;
    bool isInitialized = false; //防止重复初始化
    bool isDestroying = false;  // 防止 OnDestroy 重复执行
    bool isSoundPlayed = false;
    bool isDestroyed = false;
    bool _initApplied = false;  // Init(info) 是否被调用过（区分新池化路径 vs 旧直接赋值路径）
    bool _ondestroyReported = false; // 防止 End 与 OnDestroy 重复上报
    bool _isEnded = false;       // 防止 End() 被重复调用（链式 End 时尤其重要）
    /// <summary>动态 AddComponent 的 BreakShineController 列表，End 时清除避免池复用累积。</summary>
    private readonly List<BreakShineController> _dynamicShineControllers = new();
    /// <summary>从 ArrowPool 动态获取的 arrow，End 时需归还。</summary>
    private readonly List<GameObject> _pooledArrows = new();
    /// <summary>缓存的 slideOK 原始父级（slide 自身）；Initialize 会把它移到 slide.parent，End 时需还原。</summary>
    private bool _slideOKDetached = false;
    #endregion

    // ============================== 池化入口 ==============================
    /// <summary>
    /// 池化复用入口：刷新数据、重置状态、再调用 <see cref="Initialize"/>。
    /// </summary>
    public void Init(SlidePoolingInfo info)
    {
        notes = GameObject.Find("Notes");
        noteManager = Majdata<NoteManager>.Instance!;
        objectCounter = Majdata<ObjectCounter>.Instance!;
        skinManager = Majdata<SkinManager>.Instance!;
        timeProvider = Majdata<TimeProvider>.Instance!;
        inputManager = Majdata<InputManager>.Instance!;
        audioManager = Majdata<AudioManager>.Instance!;
        _initApplied = true;
        ApplyInfo(info);
        ResetRuntimeState();
        // 注：Initialize 由 DataLoader.InstantiateStarGroup 在所有 subSlide 创建完后统一调用
        // （以便正确建立 ConnectInfo 链与 totalSlideLen）。这里不主动 Initialize。
    }

    private void ApplyInfo(SlidePoolingInfo info)
    {
        time = info.Time;
        startTime = info.StartTime;
        LastFor = info.LastFor;
        startPosition = info.StartPosition;
        endPosition = info.EndPosition;
        speed = info.Speed;
        sortIndex = info.SortIndex;

        slideType = info.SlideShape;
        isMirror = info.IsMirror;
        isSpecialFlip = info.IsSpecialFlip;
        isJustR = info.IsJustR;
        isEach = info.IsEach;
        isBreak = info.IsBreak;
        isMine = info.IsMine;
        usingSV = info.UsingSV;
        smoothSlideAnime = info.SmoothSlideAnime;
        ConnectInfo = info.ConnectInfo;
    }

    /// <summary>
    /// 重置所有运行时状态（list/bool/Animator/sortingOrder/transform），让 prefab 实例
    /// 可以被 <see cref="Initialize"/> 重新初始化。
    /// </summary>
    private void ResetRuntimeState()
    {
        arriveTime = -1;
        judgeResult = JudgeType.Miss;
        isJudged = false;
        isInitialized = false;
        isDestroying = false;
        isDestroyed = false;
        isSoundPlayed = false;
        canShine = false;
        canCheck = false;
        isChecking = false;
        _ondestroyReported = false;
        _isEnded = false;

        boundSensors.Clear();
        triggerSensors.Clear();
        judgeQueue.Clear();
        _judgeQueue.Clear();

        // 归还上次 Initialize 从 ArrowPool 获取的 arrow（如果有）
        if (_pooledArrows.Count > 0)
        {
            ArrowPool.Instance.ReleaseMany(_pooledArrows);
            _pooledArrows.Clear();
        }
        slideBars.Clear();
        slidePositions.Clear();
        slideRotations.Clear();
        slideBarsRenderer.Clear();

        // 移除上次 Init 在 slideBars 与 star_slide 上 AddComponent 的 BreakShineController（避免累积）
        for (var i = 0; i < _dynamicShineControllers.Count; i++)
            if (_dynamicShineControllers[i] != null)
                Destroy(_dynamicShineControllers[i]);
        _dynamicShineControllers.Clear();

        // 把上次 Initialize 移到 slide.parent 的 slideOK 还原回 slide 子对象（如果存在）
        if (_slideOKDetached && slideOK != null)
        {
            slideOK.transform.SetParent(transform, false);
            _slideOKDetached = false;
        }

        // 重置 transform（Initialize 后续会覆盖）
        transform.localScale = Vector3.one;
        transform.rotation = Quaternion.identity;
    }

    // ============================== 原 Initialize（公开供 DataLoader.InstantiateStarGroup 调用） ==============================
    /// <summary>
    /// Slide初始化
    /// <para>由 DataLoader.InstantiateStarGroup 在所有 subSlide 都创建完成后统一调用。
    /// 池化场景下：先由 <see cref="Init"/> 重置状态，再调此方法重新铺 arrow / 计算 judgeQueue。</para>
    /// </summary>
    public void Initialize()
    {
        if (isInitialized)
            return;
        isInitialized = true;

        //star
        if (star_slide == null)
            star_slide = NotePool.Instance.Get(starSlidePrefab, notes.transform);

        starRenderer = star_slide.GetComponent<SpriteRenderer>();
        starRenderer.sprite = skinManager.Star;
        if (isEach) starRenderer.sprite = skinManager.Star_Each;
        if (isMine) starRenderer.sprite = skinManager.Star_Mine;
        if (isBreak)
        {
            starRenderer.sprite = skinManager.Star_Break;
            starRenderer.material = skinManager.BreakMaterial;
            starRenderer.material.SetFloat("_Brightness", 0.95f);
            var controller = star_slide.AddComponent<BreakShineController>();
            controller.parent = this;
            controller.enabled = true;
            _dynamicShineControllers.Add(controller);
        }
        // star 的 transform 重置（池化复用时旧的位置/缩放/颜色会脏）
        star_slide.transform.localScale = Vector3.one;
        star_slide.SetActive(false);
        starRenderer.color = Color.white;

        //slideok（空壳 prefab 中 slideOK 是第一个也是唯一一个子对象）
        slideOK = transform.GetChild(0).gameObject;

        // 先设置 slide 的 rotation/mirror，这样后续动态摆放的 arrow 才能正确变换
        if (isMirror)
        {
            transform.localScale = new Vector3(-1f, 1f, 1f);
            transform.rotation = Quaternion.Euler(0f, 0f, -45f * startPosition);
            slideOK.transform.localScale = new Vector3(-1f, 1f, 1f);
        }
        else
        {
            transform.rotation = Quaternion.Euler(0f, 0f, -45f * (startPosition - 1));
        }

        // slideOK 位置：从 SlideOKTable 获取该 slideType 对应的位姿并设置 localPosition/localRotation
        var okPose = SlideOKTable.Get(slideType);
        if (okPose.HasValue)
        {
            var pose = okPose.Value;
            slideOK.transform.localPosition = new Vector3(pose.X, pose.Y, 0);
            slideOK.transform.localRotation = Quaternion.Euler(0, 0, pose.RotZ);
        }

        //bars——从 ArrowPool 动态获取并按 SlideArrowTable 摆放
        var poses = SlideArrowTable.Get(slideType);
        if (poses != null)
        {
            ArrowPool.Instance.GetMany(transform, poses.Length, _pooledArrows);
            for (var i = 0; i < poses.Length; i++)
            {
                var arrow = _pooledArrows[i];
                var pose = poses[i];

                // 设置 localPosition/localRotation（父对象已旋转，镜像由 localScale.x=-1 处理）
                arrow.transform.localPosition = new Vector3(pose.X, pose.Y, 0);
                arrow.transform.localRotation = Quaternion.Euler(0, 0, pose.RotZ);
                arrow.SetActive(true);

                slideBars.Add(arrow);
            }
        }

        // slideOK 的 Just 设置
        if (isJustR)
        {
            if (slideOK.GetComponent<LoadJustSprite>().setR() == 1 && isMirror)
            {
                slideOK.transform.Rotate(new Vector3(0f, 0f, 180f));
                var angel = slideOK.transform.rotation.eulerAngles.z * Mathf.Deg2Rad;
                slideOK.transform.position += new Vector3(Mathf.Sin(angel) * 0.27f, Mathf.Cos(angel) * -0.27f);
            }
        }
        else
        {
            if (slideOK.GetComponent<LoadJustSprite>().setL() == 1 && !isMirror)
            {
                slideOK.transform.Rotate(new Vector3(0f, 0f, 180f));
                var angel = slideOK.transform.rotation.eulerAngles.z * Mathf.Deg2Rad;
                slideOK.transform.position += new Vector3(Mathf.Sin(angel) * 0.27f, Mathf.Cos(angel) * -0.27f);
            }
        }
        slideOK.SetActive(false);
        slideOK.transform.SetParent(transform.parent);
        _slideOKDetached = true; // 标记：End 时需要把它再 SetParent(transform) 回来以便整体回池

        //bars positions/rotations（动态摆放后读取世界坐标，保留原 18° 偏移）
        slidePositions.Add(getPositionFromDistance(4.8f));
        foreach (var bars in slideBars)
        {
            slidePositions.Add(bars.transform.position);
            slideRotations.Add(Quaternion.Euler(bars.transform.rotation.eulerAngles + new Vector3(0f, 0f, 18f)));
        }

        //bars pos
        var endPos = getPositionFromDistance(4.8f, endPosition);
        var x = slidePositions.LastOrDefault() - Vector3.zero;
        var y = endPos - Vector3.zero;
        var angle = Mathf.Acos(Vector3.Dot(x, y) / (x.magnitude * y.magnitude)) * Mathf.Rad2Deg;
        var offset = slideRotations.TakeLast(1).First().eulerAngles - slideRotations.TakeLast(2).First().eulerAngles;
        if (offset.z < 0)
            angle = -angle;

        var q = slideRotations.LastOrDefault() * Quaternion.Euler(0, 0, angle);
        slidePositions.Add(endPos);
        slideRotations.Add(q);

        //bars skin
        foreach (var gm in slideBars)
        {
            var sr = gm.GetComponent<SpriteRenderer>();
            slideBarsRenderer.Add(sr);
            sr.sortingOrder = sortIndex--;
            sr.sortingLayerName = "Slide";

            sr.sprite = skinManager.Slide; //注意赋值顺序
            if (isEach)
            {
                sr.sprite = skinManager.Slide_Each;
            }
            if (isBreak)
            {
                sr.sprite = skinManager.Slide_Break;
                sr.material = skinManager.BreakMaterial;
                //sr.material.SetFloat("_Brightness", 0.95f);
                var controller = gm.AddComponent<BreakShineController>();
                controller.parent = this;
                controller.enabled = true;
                _dynamicShineControllers.Add(controller);
            }
            if (isMine)
            {
                sr.sprite = skinManager.Slide_Mine;
            }
        }
        SetSlideBarAlpha(0f);

        //bars fadein
        // 计算Slide淡入时机
        // 在8.0速时应当提前300ms显示Slide
        fadeInTime = -3.926913f / speed;
        // Slide完全淡入时机
        // 正常情况下应为负值；速度过高将忽略淡入
        var fullFadeInTime = Math.Min(fadeInTime + 0.2f, 0);
        var interval = fullFadeInTime - fadeInTime;
        fadeInAnimator = GetComponent<Animator>();
        // 池化复用：Animator 状态需要 Rebind，否则 SetTrigger 不生效
        fadeInAnimator.Rebind();
        //淡入时机与正解帧间隔小于200ms时，加快淡入动画的播放速度; interval永不为0
        fadeInAnimator.speed = 0.2f / interval;
        fadeInAnimator.SetTrigger("slide");

        //judgeQueue
        var table = SlideTables.FindTableByName(slideType);
        if (isMirror)
        {
            table!.Mirror(SensorType.A1);
        }
        var diff = Math.Abs(1 - startPosition);
        if (diff != 0)
        {
            table!.Diff(diff);
        }
        judgeQueue = table!.JudgeQueue.ToList();

        if (ConnectInfo.IsConnSlide)
        {
            if (ConnectInfo.IsGroupPartEnd)
            {
                judgeQueue.LastOrDefault()!.SetIsLast();
            }
            else
            {
                judgeQueue.LastOrDefault()!.SetNonLast();
            }

            if (ConnectInfo.TotalJudgeQueueLen < 4)
            {
                if (ConnectInfo.IsGroupPartHead)
                {
                    judgeQueue[0].IsSkippable = true;
                    judgeQueue[1].IsSkippable = false;
                }
                else if (ConnectInfo.IsGroupPartEnd)
                {
                    judgeQueue[0].IsSkippable = false;
                    judgeQueue[1].IsSkippable = true;
                }
            }
            else
            {
                foreach (var judgeArea in judgeQueue)
                {
                    judgeArea.IsSkippable = true;
                }
            }
        }

        _judgeQueue = new(judgeQueue);

        foreach (var area in judgeQueue.SelectMany(x => x.Areas))
        {
            boundSensors.Add(area);
            inputManager.BindSensor(Check, area);
        }

        //judge timing
        if (ConnectInfo.IsGroupPartEnd || !ConnectInfo.IsConnSlide)
        {
            var percent = table.Const;
            judgeTiming = time + LastFor * (1 - percent);
            forceJudgeTime = LastFor * percent;
        }
    }

    /// <summary>
    /// Connection Slide
    /// <para>强制完成该Slide</para>
    /// </summary>
    public void ForceFinish()
    {
        if (!ConnectInfo.IsConnSlide || ConnectInfo.IsGroupPartEnd)
            return;
        judgeQueue.Clear();
    }

    // ============================== 逻辑：Update（running + check） ==============================
    /// <summary>
    /// Update：状态机推进 + autoplay (Running) + 玩家判定轮询 (Check)。
    /// 由原 Update 与 FixedUpdate 末尾的 Check 合并而来。
    /// </summary>
    private void Update()
    {
        if (isDestroyed) return;

        if (Majdata<InputManager>.Instance!.Mode is AutoPlayMode.Enable or AutoPlayMode.Random)
        {
            // autoplay 路径下 Update 不做检查（FixedUpdate.Render 会处理 hide/destroy）
            return;
        }

        // time        是Slide启动的时间点
        // startTiming 是Slide完全显示但未启动
        var start = timeProvider.NoteTime - startTime;
        var timing = timeProvider.NoteTime - time;
        var forceJudge = timing - LastFor - forceJudgeTime;

        if (ConnectInfo.IsConnSlide)
        {
            if (ConnectInfo.IsGroupPartHead && start >= -0.05f)
                canCheck = true;
            else if (!ConnectInfo.IsGroupPartHead)
                canCheck = ConnectInfo.ParentFinished || ConnectInfo.ParentPendingFinish;
        }
        else if (start >= -0.05f)
            canCheck = true;

        //此处对mine音符的处理：一进judge就判定为miss并销毁，能进too late就判为perfect
        if (ConnectInfo.IsGroupPartEnd || !ConnectInfo.IsConnSlide)
        {
            if (IsFinished)
            {
                HideBar(areaStep.LastOrDefault());
                Judge();
                DestroySelf();
            }
            else if (forceJudge >= 0)
            {
                TooLateJudge();
                DestroySelf();
            }
        }
        else if (IsFinished)
        {
            HideBar(areaStep.LastOrDefault());
            DestroySelf(true);
        }

        Running();
        Check();
    }

    // ============================== 渲染：FixedUpdate ==============================
    /// <summary>
    /// FixedUpdate：渲染（淡入、引导星位置插值、HideBar 进度）。
    /// 注：autoplay 模式下的 HideBar/DestroySelf 仍在 Render 中触发（与原版一致）。
    /// </summary>
    private void FixedUpdate()
    {
        Render();
    }

    private void Render()
    {
        if (isDestroyed) return;

        var timing = timeProvider.NoteTime - startTime; // Slide完全显示但未启动的时间点
        var stiming = timeProvider.NoteTime - time;
        var remaining = Math.Max(LastFor - timing, 0);

        var fakeTiming = timeProvider.FakeNoteTime - timeProvider.GetPositionAtTime(startTime);
        var fakesTiming = timeProvider.FakeNoteTime - timeProvider.GetPositionAtTime(time);
        var fakeLastfor = timeProvider.GetPositionAtTime(time + LastFor) - timeProvider.GetPositionAtTime(time);
        var fakeRemaining = Math.Max(fakeLastfor - fakeTiming, 0);

        if (!usingSV)
        {
            fakeTiming = timing;
            fakesTiming = stiming;
            fakeLastfor = LastFor;
            fakeRemaining = remaining;
        }

        // Slide淡入期间，不透明度从0到0.55耗时200ms
        if (fakeTiming <= 0f)
        {
            if (fakeTiming >= -0.05f)
            {
                fadeInAnimator.enabled = false;
                SetSlideBarAlpha(1f);
            }
            else if (fakeTiming >= fadeInTime)
            {
                if (fadeInAnimator != null && !fadeInAnimator.enabled)
                {
                    fadeInAnimator.enabled = true;
                }
                SetSlideBarAlpha(SlideBarAlpha);
            }
            return;
        }

        star_slide.SetActive(true);
        if (fakesTiming <= 0f)
        {
            canShine = true;
            float alpha;
            if (ConnectInfo.IsConnSlide && !ConnectInfo.IsGroupPartHead)
                alpha = 0;
            else
            {
                // 只有当它是一个起点Slide（而非Slide Group中的子部分）的时候，才会有开始的星星渐入动画
                alpha = 1f - -fakesTiming / (time - startTime);
                alpha = alpha > 1f ? 1f : alpha;
                alpha = alpha < 0f ? 0f : alpha;
            }

            starRenderer.color = new Color(1, 1, 1, alpha);
            star_slide.transform.localScale = new Vector3(alpha + 0.5f, alpha + 0.5f, alpha + 0.5f);
            star_slide.transform.position = slidePositions[0];
            applyStarRotation(slideRotations[0]);
        }
        else
        {
            starRenderer.color = Color.white;
            star_slide.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);

            var process = (fakeLastfor - fakesTiming) / fakeLastfor;
            process = Math.Max(1f - process, 0);
            var indexProcess = (slidePositions.Count - 1) * process;
            var index = (int)indexProcess;
            var pos = indexProcess - index;

            if (process >= 1f)
            {
                switch (Majdata<InputManager>.Instance!.Mode)
                {
                    case AutoPlayMode.Enable:
                        if (smoothSlideAnime) HideBar(index + 1);
                        else HideBar(areaStep[(int)(process * (areaStep.Count - 1))]);
                        DestroySelf();
                        judgeQueue.Clear();
                        return;
                    case AutoPlayMode.Random:
                        var barIndex = areaStep[(int)(process * (areaStep.Count - 1))];
                        HideBar(barIndex);
                        DestroySelf();
                        judgeQueue.Clear();
                        return;
                }
                star_slide.transform.position = slidePositions.LastOrDefault();
                applyStarRotation(slideRotations.LastOrDefault());
                if (ConnectInfo.IsConnSlide && !ConnectInfo.IsGroupPartEnd)
                    DestroySelf(true);
                else if (IsFinished && isJudged)
                    DestroySelf();
            }
            else
            {
                var a = slidePositions[index + 1];
                var b = slidePositions[index];
                var ba = a - b;
                var newPos = ba * pos + b;

                star_slide.transform.position = newPos;
                if (index < slideRotations.Count - 1)
                {
                    var _a = slideRotations[index + 1].eulerAngles.z;
                    var _b = slideRotations[index].eulerAngles.z;
                    var dAngle = Mathf.DeltaAngle(_b, _a) * pos;
                    dAngle = Mathf.Abs(dAngle);
                    var newRotation = Quaternion.Euler(0f, 0f,
                        Mathf.MoveTowardsAngle(_b, _a, dAngle));
                    applyStarRotation(newRotation);
                }
            }
            switch (Majdata<InputManager>.Instance!.Mode)
            {
                case AutoPlayMode.Enable:
                    judgeQueue = judgeQueue.Skip((int)(process * (judgeQueue.Count - 1))).ToList();
                    if (smoothSlideAnime) HideBar(index + 1);
                    else HideBar(areaStep[(int)(process * (areaStep.Count - 1))]);
                    PlaySFX();
                    break;
                case AutoPlayMode.Random:
                    judgeQueue = judgeQueue.Skip((int)(process * (judgeQueue.Count - 1))).ToList();
                    var barIndex = areaStep[(int)(process * (areaStep.Count - 1))];
                    HideBar(barIndex);
                    PlaySFX();
                    break;
            }
        }
    }

    public float GetSlideLength()
    {
        if (areaStep.Count > 0)
            return areaStep.Last();

        return Math.Max(slideBars.Count, 1);
    }

    // ============================== 判定 ==============================
    public void Check(object sender, InputEventArgs arg) => Check();
    /// <summary>
    /// 判定队列检查
    /// </summary>
    public void Check()
    {
        if (!canCheck || isChecking || IsFinished)
            return;
        if (Majdata<InputManager>.Instance!.Mode is AutoPlayMode.Enable or AutoPlayMode.Random)
            return;

        isChecking = true;

        //parent conn slide
        if (ConnectInfo.Parent != null && judgeQueue.Count < _judgeQueue.Count)
        {
            if (!ConnectInfo.ParentFinished)
                ConnectInfo.Parent.GetComponent<SlideDrop>().ForceFinish();
        }

        //slide
        var first = judgeQueue.First();
        SlideArea? second = null;

        if (judgeQueue.Count >= 2)
            second = judgeQueue[1];
        foreach (var t in first.Areas)
        {
            first.Judge(inputManager.CheckSensor(t));
        }

        if (first.On)
        {
            PlaySFX();
        }

        if (second is not null && (first.IsSkippable || first.On))
        {
            var sType = second.Areas;
            foreach (var t in sType)
            {
                second.Judge(inputManager.CheckSensor(t));
            }

            if (second.IsFinished)
            {
                HideBar(second.ArrowProgressWhenFinished);
                judgeQueue = judgeQueue.Skip(2).ToList();
                isChecking = false;
                return;
            }
            else if (second.On)
            {
                HideBar(second.ArrowProgressWhenOn);
                judgeQueue = judgeQueue.Skip(1).ToList();
                isChecking = false;
                return;
            }
        }
        if (first.IsFinished)
        {
            HideBar(first.ArrowProgressWhenFinished);
            judgeQueue = judgeQueue.Skip(1).ToList();
            isChecking = false;
            return;
        }

        isChecking = false;
    }

    void HideBar(int endIndex)
    {
        endIndex = Math.Min(endIndex - 1, slideBars.Count - 1);
        for (int i = 0; i <= endIndex; i++)
            slideBars[i].SetActive(false);
    }

    /// <summary>
    /// AutoPlay
    /// <para>用于触发Sensor</para>
    /// </summary>
    void Running()
    {
        if (timeProvider.NoteTime - time < 0f || isMine)
            return;
        if (Majdata<InputManager>.Instance!.Mode is AutoPlayMode.Enable or AutoPlayMode.Random or AutoPlayMode.Disable)
            return;
        if (star_slide)
        {
            var starPos = star_slide.transform.position;
            inputManager.WorldPositionHandle(guid.GetHashCode(), starPos);
        }
    }

    /// <summary>
    /// Slide判定
    /// </summary>
    void Judge()
    {
        if (isMine)
        {
            judgeResult = JudgeType.Miss;
            SetJust();
            isJudged = true;
            return;
        }
        if (!ConnectInfo.IsGroupPartEnd && ConnectInfo.IsConnSlide)
            return;
        var stayTime = time + LastFor - judgeTiming; // 停留时间
        if (usingSV)
        {
            judgeTiming = timeProvider.GetPositionAtTime(judgeTiming);
            stayTime = timeProvider.GetPositionAtTime(stayTime);
        }
        if (!isJudged)
        {
            arriveTime = timeProvider.NoteTime;
            var triggerTime = timeProvider.NoteTime;

            const float totalInterval = 1.2f; // 秒
            const float nPInterval = 0.4666667f; // Perfect基础区间

            float extInterval = MathF.Min(stayTime / 4, 0.733333f);           // Perfect额外区间
            float pInterval = MathF.Min(nPInterval + extInterval, totalInterval);// Perfect总区间
            var ext = MathF.Max(extInterval - 0.4f, 0);
            float grInterval = MathF.Max(0.4f - extInterval, 0);        // Great总区间
            float gdInterval = MathF.Max(0.3333334f - ext, 0); // Good总区间

            var diff = judgeTiming - triggerTime; // 大于0为Fast，小于为Late
            bool isFast = false;
            JudgeType? judge = null;

            if (diff > 0)
                isFast = true;

            var p = pInterval / 2;
            var gr = grInterval / 2;
            var gd = gdInterval / 2;
            diff = MathF.Abs(diff);

            if (gr == 0)
            {
                if (diff >= p)
                    judge = isFast ? JudgeType.FastGood : JudgeType.LateGood;
                else
                    judge = JudgeType.Perfect;
            }
            else
            {
                if (diff >= gr + p || diff >= totalInterval / 2)
                    judge = isFast ? JudgeType.FastGood : JudgeType.LateGood;
                else if (diff >= p)
                    judge = isFast ? JudgeType.FastGreat : JudgeType.LateGreat;
                else
                    judge = JudgeType.Perfect;
            }
            print($"Slide diff : {MathF.Round(diff * 1000, 2)} ms");
            judgeResult = judge ?? JudgeType.Miss;
            isJudged = true;
            SetJust();
        }
    }

    void SetJust()
    {
        switch (judgeResult)
        {
            case JudgeType.FastGreat2:
            case JudgeType.FastGreat1:
            case JudgeType.FastGreat:
                slideOK.GetComponent<LoadJustSprite>().setFastGr();
                break;
            case JudgeType.FastGood:
                slideOK.GetComponent<LoadJustSprite>().setFastGd();
                break;
            case JudgeType.LateGood:
                slideOK.GetComponent<LoadJustSprite>().setLateGd();
                break;
            case JudgeType.LateGreat1:
            case JudgeType.LateGreat2:
            case JudgeType.LateGreat:
                slideOK.GetComponent<LoadJustSprite>().setLateGr();
                break;
            case JudgeType.Miss:
                slideOK.GetComponent<LoadJustSprite>().setMiss();
                break;

        }
    }

    /// <summary>
    /// 强制将Slide判定为TooLate并销毁
    /// </summary>
    void TooLateJudge()
    {
        if (isMine)
        {
            judgeResult = JudgeType.Perfect;
            SetJust();
            isJudged = true;
            return;
        }
        if (judgeQueue.Count == 1)
            slideOK.GetComponent<LoadJustSprite>().setLateGd();
        else
            slideOK.GetComponent<LoadJustSprite>().setMiss();
        SetJust();
        isJudged = true;
    }

    // ============================== 销毁 / End ==============================
    /// <summary>
    /// 销毁当前Slide
    /// <para>当 <paramref name="onlyStar"/> 为true时，仅销毁/归还引导Star</para>
    /// </summary>
    /// <param name="onlyStar"></param>
    void DestroySelf(bool onlyStar = false)
    {
        if (isDestroyed)
            return;
        isDestroyed = true;
        PlayJudgeSFX();

        if (onlyStar)
        {
            // conn slide 中段：只释放引导星，slide GameObject 保留，等链尾通过 End() 链式清理
            ReleaseStar();
        }
        else
        {
            foreach (var obj in slideBars)
                obj.SetActive(false);

            ReleaseStar();
            // End() 内部会负责链式 End 上游 conn slide，避免孤儿
            End();
        }
    }

    private void ReleaseStar()
    {
        if (star_slide == null) return;

        NotePool.Instance.Release(starSlidePrefab, star_slide);
        star_slide = null!;
    }

    void OnDestroy()
    {
        if (PlayManager.IsReloading) return;
        if (isDestroying) return;
        ReportAndUnbind();
    }

    /// <summary>
    /// 上报判定 + 解绑 sensors。 OnDestroy 与 End 都会调用此方法（用 _ondestroyReported 防重复）。
    /// </summary>
    private void ReportAndUnbind()
    {
        if (PlayManager.IsReloading) return;
        if (_ondestroyReported) return;
        _ondestroyReported = true;
        isDestroying = true;

        // 兜底销毁未释放的 child slide / star_slide（Reload 流程会走这里）
        if (ConnectInfo.Parent != null && ConnectInfo.Parent)
        {
            // ConnectInfo.Parent 在重玩流程中可能仍存在，但其 SlideDrop 可能已被销毁
            // 不再触碰它，避免循环
        }

        if (inputManager != null)
            inputManager.ClearTriggeredSensor(guid.GetHashCode());

        if (ConnectInfo.IsGroupPartEnd || !ConnectInfo.IsConnSlide)
        {
            switch (Majdata<InputManager>.Instance!.Mode)
            {
                case AutoPlayMode.Enable:
                    if (isMine)
                        judgeResult = JudgeType.Miss;
                    else
                        judgeResult = JudgeType.Perfect;
                    SetJust();
                    break;
                case AutoPlayMode.Random:
                    judgeResult = (JudgeType)Random.Range(1, 14);
                    if (isMine)
                    {
                        if (judgeResult != JudgeType.Miss)
                        { //Too Late Only, 不考虑留一个判定区的那种LateGd，都随机了，能支持就是随机的荣幸
                            judgeResult = JudgeType.Miss;
                        }
                        else
                        {
                            judgeResult = JudgeType.Perfect;
                        }
                    }
                    SetJust();
                    break;
            }

            // 只有组内最后一个Slide完成 才会显示判定条并增加总数
            if (objectCounter != null)
                objectCounter.ReportResult(SimaiNoteType.Slide, judgeResult, isBreak);
            if (slideOK != null)
            {
                if (isBreak && judgeResult == JudgeType.Perfect)
                    slideOK.GetComponent<Animator>().runtimeAnimatorController = skinManager.Shine_JudgeBreak;
                if (EffectManager.showLevel)
                {
                    slideOK.SetActive(true);
                }
            }
        }
        else
        {
            // 如果不是组内最后一个 那么也要把判定条隐藏
            if (slideOK != null) slideOK.SetActive(false);
        }
        if (inputManager != null)
        {
            foreach (var t in boundSensors)
                inputManager.UnbindSensor(Check, t);
        }
        boundSensors.Clear();
    }

    /// <summary>
    /// 池化结束：上报、解绑、链式 End 上游 conn slide、把 slideOK 还原回 child、释放 star_slide 与自身回池。
    /// 通过 <see cref="_isEnded"/> 保证幂等（链式 End 可能被重复触发）。
    /// </summary>
    public override void End()
    {
        if (_isEnded) return;
        _isEnded = true;

        noteManager.RemoveLoadedNote(this);
        ReportAndUnbind();

        // 链式 End 上游 conn slide，避免孤儿（原版在 OnDestroy 中通过 Destroy(Parent) 实现）
        if (ConnectInfo.Parent != null)
        {
            var parentSlide = ConnectInfo.Parent.GetComponent<SlideDrop>();
            ConnectInfo.Parent = null; // 先断引用，防止循环
            parentSlide.End();
        }

        // 把 slideOK 还原回 slide 子对象，这样整体 Release 后下次 Initialize 还能正确找到它
        if (_slideOKDetached && slideOK != null)
        {
            slideOK.transform.SetParent(transform, false);
            _slideOKDetached = false;
        }

        // 释放 star_slide（如果还在）
        ReleaseStar();

        // 移除动态 BreakShineController（避免下次 Init 累积）
        for (var i = 0; i < _dynamicShineControllers.Count; i++)
            if (_dynamicShineControllers[i] != null)
                Destroy(_dynamicShineControllers[i]);
        _dynamicShineControllers.Clear();

        // 归还 arrow 到 ArrowPool
        if (_pooledArrows.Count > 0)
        {
            ArrowPool.Instance.ReleaseMany(_pooledArrows);
            _pooledArrows.Clear();
        }
        slideBars.Clear();
        slideBarsRenderer.Clear();

        NotePool.Instance.Release(prefabRef, gameObject);
    }

    private void SetSlideBarAlpha(float alpha)
    {
        foreach (var r in slideBarsRenderer)
        {
            var c = r.color;
            c.a = alpha;
            r.color = c;
        }
    }
    private void applyStarRotation(Quaternion newRotation)
    {
        var halfFlip = newRotation.eulerAngles;
        halfFlip.z += 180f;
        if (isSpecialFlip)
            star_slide.transform.rotation = Quaternion.Euler(halfFlip);
        else
            star_slide.transform.rotation = newRotation;
    }

    private void PlayJudgeSFX()
    {
        if ((ConnectInfo.IsGroupPartHead || !ConnectInfo.IsConnSlide) &&
            isBreak &&
            judgeResult == JudgeType.Perfect)
        {
            audioManager.PlayBreakSlideEndSound();
        }
    }
    private void PlaySFX()
    {
        if (isSoundPlayed) return;

        if (ConnectInfo.IsGroupPartHead || !ConnectInfo.IsConnSlide)
        {
            isSoundPlayed = true;
            audioManager.PlaySlideSound(isBreak);
        }
    }

    public bool CanShine() => canShine;
}
