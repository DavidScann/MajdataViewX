#nullable enable

#region

using System;
using MajSimai;
using UnityEngine;
using Random = UnityEngine.Random;

#endregion

/// <summary>
/// TouchHold note：长按型 touch note。继承 <see cref="NoteLongBase"/>。
/// <para>生命周期：<c>Start (一次性) → Init(info) → Update(running+autoplay+hold判定累计) + Check(事件) → FixedUpdate(Render) → End(归还池)</c></para>
/// </summary>
public class TouchHoldDrop : NoteLongBase
{
    #region Note Data (Init 刷新)
    public char areaPosition;
    public bool isFirework;
    #endregion

    #region Injected Dependencies (Start 一次性)
    [SerializeField] GameObject touchEffect;
    [SerializeField] GameObject gr_TouchEffect;
    [SerializeField] GameObject gd_TouchEffect;
    [SerializeField] GameObject judgeEffect;

    [SerializeField] GameObject[] fans = new GameObject[6]; //01,02,03,04,point,border
    [SerializeField] SpriteMask mask;
    [SerializeField] private GameObject holdEffectPrefab;

    private SpriteRenderer[] fansRenderers = new SpriteRenderer[5];
    private SpriteRenderer border;
    private GameObject firework;
    private Animator fireworkEffect;

    private float wholeDuration;
    private float moveDuration;
    private float displayDuration;

    private Sprite _borderSprite;
    private bool isSfxPlaying;

    // sortingOrder 基线
    private int[] _baseFanOrder = new int[5];
    private int _baseBorderOrder;
    private bool _baseOrderCached = false;
    #endregion

    #region Runtime State (Init 重置)
    private bool _isTouched = false;
    private bool inputBound = false;
    #endregion

    /// <summary>
    /// Awake：池化场景下 Init 在 SetActive(true) 之前调用，依赖注入必须在 Awake 完成。
    /// </summary>
    private void Awake()
    {
        notes = GameObject.Find("Notes");
        objectCounter = Majdata<ObjectCounter>.Instance!;
        noteManager = Majdata<NoteManager>.Instance!;
        timeProvider = Majdata<TimeProvider>.Instance!;
        inputManager = Majdata<InputManager>.Instance!;
        skinManager = Majdata<SkinManager>.Instance!;
        audioManager = Majdata<AudioManager>.Instance!;

        holdEffectPrefab = holdEffect;

        firework = GameObject.Find("FireworkEffect");
        fireworkEffect = firework.GetComponent<Animator>();

        for (var i = 0; i < 5; i++)
            fansRenderers[i] = fans[i].GetComponent<SpriteRenderer>();
        border = fans[5].GetComponent<SpriteRenderer>();
    }

    /// <summary>池化复用入口。</summary>
    public void Init(TouchHoldPoolingInfo info)
    {
        ApplyInfo(info);
        wholeDuration = 3.209385682f * Mathf.Pow(speed, -0.9549621752f);
        moveDuration = 0.8f * wholeDuration;
        displayDuration = 0.2f * wholeDuration;

        GetHoldEffect();
        ResetSortingOrder(info.NoteSortOrder);
        LoadSkin();
        ResetState();

        transform.position = GetAreaPos(startPosition, areaPosition);
        SetFanColor(new Color(1f, 1f, 1f, 0f));

        mask.backSortingOrder = border.sortingOrder - 1;
        mask.frontSortingOrder = border.sortingOrder;
        mask.enabled = false;

        sensor = InputManager.GetSensor(areaPosition, startPosition);
        inputManager.BindSensor(Check, sensor);
        inputBound = true;
        gameObject.SetActive(false);
    }

    private void ApplyInfo(TouchHoldPoolingInfo info)
    {
        time = info.Time;
        LastFor = info.LastFor;
        startPosition = info.StartPosition;
        areaPosition = info.AreaPosition;
        speed = info.Speed;
        isEach = info.IsEach;
        isBreak = info.IsBreak;
        isMine = info.IsMine;
        isFirework = info.IsFirework;
        usingSV = info.UsingSV;
    }

    private void GetHoldEffect()
    {
        holdEffect = NotePool.Instance.Get(holdEffectPrefab, notes.transform);
        holdEffect.SetActive(false);
        material = holdEffect.GetComponent<ParticleSystemRenderer>().material;
    }

    private void ResetSortingOrder(int order)
    {
        if (!_baseOrderCached)
        {
            for (var i = 0; i < 5; i++)
                _baseFanOrder[i] = fansRenderers[i].sortingOrder;
            _baseBorderOrder = border.sortingOrder;
            _baseOrderCached = true;
        }
        for (var i = 0; i < 5; i++)
            fansRenderers[i].sortingOrder = _baseFanOrder[i] + order;
        border.sortingOrder = _baseBorderOrder + order;
        noteSortOrder = order;
    }

    private void ResetState()
    {
        isJudged = false;
        judgeResult = JudgeType.Miss;
        judgeDiff = -1;
        playerIdleTime = 0;
        _isTouched = false;
        inputBound = false;
        holdEffect.SetActive(false);
        isSfxPlaying = false;
    }

    private void LoadSkin()
    {
        for (var i = 0; i < 4; i++)
            fansRenderers[i].sprite = skinManager.TouchHold[i];
        fansRenderers[4].sprite = skinManager.TouchPoint;
        border.sprite = _borderSprite = skinManager.TouchHold_Border;
        if (isEach) fansRenderers[4].sprite = skinManager.TouchPoint_Each;
        if (isBreak)
        {
            for (var i = 0; i < 4; i++)
                fansRenderers[i].sprite = skinManager.TouchHold_Break[i];
            fansRenderers[4].sprite = skinManager.TouchPoint_Break;
            border.sprite = _borderSprite = skinManager.TouchHold_Border_Break;
        }
        if (isMine)
        {
            for (var i = 0; i < 4; i++)
                fansRenderers[i].sprite = skinManager.TouchHold_Mine[i];
            fansRenderers[4].sprite = skinManager.TouchPoint_Mine;
            border.sprite = _borderSprite = skinManager.TouchHold_Border_Mine;
        }
    }

    // ============================== 输入派发 + 判定 ==============================
    void Check(object sender, InputEventArgs arg)
    {
        if (isJudged || !noteManager.CanJudge(gameObject, sensor)) return;
        if (Majdata<InputManager>.Instance!.Mode is AutoPlayMode.Enable or AutoPlayMode.Random) return;
        if (arg.IsClick)
        {
            if (!inputManager.IsIdle(arg)) return;
            inputManager.SetBusy(arg);
            Judge();
            if (isJudged)
            {
                inputManager.UnbindArea(Check, sensor);
                inputBound = false;
                noteManager.NextTouch(sensor);
            }
        }
    }

    void Judge()
    {
        const float JUDGE_GOOD_AREA = 316.667f;
        const int JUDGE_GREAT_AREA = 250;
        const int JUDGE_PERFECT_AREA = 200;
        const float JUDGE_SEG_PERFECT = 150f;

        if (isJudged) return;

        var timing = timeProvider.NoteTime - time;
        var isFast = timing < 0;
        var diff = MathF.Abs(timing * 1000);
        JudgeType result;
        if (diff > JUDGE_SEG_PERFECT && isFast) return;
        else if (diff < JUDGE_SEG_PERFECT) result = JudgeType.Perfect;
        else if (diff < JUDGE_PERFECT_AREA) result = JudgeType.LatePerfect2;
        else if (diff < JUDGE_GREAT_AREA) result = JudgeType.LateGreat;
        else if (diff < JUDGE_GOOD_AREA) result = JudgeType.LateGood;
        else result = JudgeType.Miss;
        judgeDiff = isFast ? 0 : diff;
        judgeResult = result;
        isJudged = true;
        PlayHoldEffect();
        audioManager.PlayTouchSound();
    }

    // ============================== 逻辑：Update（running + hold判定累计） ==============================
    /// <summary>
    /// Update：running(autoplay) + hold 判定累计。由原 FixedUpdate 迁移到 Update。
    /// </summary>
    private void Update()
    {
        UpdateRunning();
    }

    private void UpdateRunning()
    {
        var remainingTime = GetRemainingTime();
        var timing = timeProvider.NoteTime - time;

        if (remainingTime == 0 && isJudged)
        {
            inputManager.SetSensorOff(sensor, guid);
            DestroySelf();
            return;
        }
        else if (timing >= -0.01f)
        {
            // AutoPlay相关
            switch (Majdata<InputManager>.Instance!.Mode)
            {
                case AutoPlayMode.Enable:
                    if (!isJudged) noteManager.NextTouch(GetSensor());
                    judgeResult = isMine ? JudgeType.Miss : JudgeType.Perfect;
                    isJudged = true;
                    _isTouched = true;
                    PlayHoldEffect();
                    audioManager.PlayTouchHoldSound();
                    return;
                case AutoPlayMode.DJAuto:
                    if (!isJudged && !isMine) inputManager.SetSensorOn(sensor, guid);
                    break;
                case AutoPlayMode.Random:
                    if (!isJudged)
                    {
                        noteManager.NextTouch(GetSensor());
                        if (isMine)
                        {
                            judgeResult = judgeResult > JudgeType.Perfect ? JudgeType.Miss : JudgeType.Perfect;
                            if (judgeResult != JudgeType.Miss) _isTouched = true;
                        }
                        isJudged = true;
                    }
                    PlayHoldEffect();
                    audioManager.PlayTouchHoldSound();
                    return;
                case AutoPlayMode.Disable:
                default: break;
            }
        }

        if (isJudged)
        {
            if (!timeProvider.IsStart) return;
            var on = inputManager.CheckSensor(sensor);

            if (on)
            {
                _isTouched = true;
                audioManager.PlayTouchHoldSound();
            }
            else
            {
                audioManager.StopTouchHoldSound();
            }

            if (timing <= 0.25f) return;
            if (remainingTime <= 0.2f) return;

            if (on) PlayHoldEffect();
            else
            {
                playerIdleTime += Time.deltaTime;
                StopHoldEffect();
            }
        }
        else if (timing > 0.316667f)
        {
            judgeDiff = 316.667f;
            judgeResult = JudgeType.Miss;
            if (inputBound)
            {
                inputManager.UnbindSensor(Check, sensor);
                inputBound = false;
            }
            isJudged = true;
            noteManager.NextTouch(GetSensor());
        }
    }

    // ============================== 渲染：FixedUpdate ==============================
    /// <summary>
    /// FixedUpdate：Render（fans 移动/颜色/mask alphaCutoff）。由原 Update 迁移到 FixedUpdate。
    /// </summary>
    private void FixedUpdate()
    {
        Render();
    }

    private void Render()
    {
        var timing = timeProvider.NoteTime - time;
        var pow = -Mathf.Exp(8 * (timing * 0.43f / moveDuration) - 0.85f) + 0.42f;
        var distance = Mathf.Clamp(pow, 0f, 0.4f);

        var fakeTiming = timeProvider.FakeNoteTime - timeProvider.GetPositionAtTime(time);
        var fakePow = -Mathf.Exp(8 * (fakeTiming * 0.43f / moveDuration) - 0.85f) + 0.42f;
        var fakeDistance = Mathf.Clamp(fakePow, 0f, 0.4f);
        var fakeLastFor = timeProvider.GetPositionAtTime(time + LastFor) - timeProvider.GetPositionAtTime(time);

        if (!usingSV)
        {
            fakeTiming = timing;
            fakePow = pow;
            fakeDistance = distance;
            fakeLastFor = LastFor;
        }

        if (-fakeTiming <= wholeDuration && -fakeTiming > moveDuration)
        {
            SetFanColor(new Color(1f, 1f, 1f, Mathf.Clamp((wholeDuration + fakeTiming) / displayDuration, 0f, 1f)));
            fans[5].SetActive(false);
            mask.enabled = false;
        }
        else if (-fakeTiming < moveDuration)
        {
            fans[5].SetActive(true);
            mask.enabled = true;
            SetFanColor(Color.white);
            mask.alphaCutoff = Mathf.Clamp(0.91f * (1 - (fakeLastFor - fakeTiming) / fakeLastFor), 0f, 1f);
        }

        if (float.IsNaN(distance)) distance = 0f;
        if (fakeTiming >= 0f)
            holdEffect.transform.position = transform.position;

        for (var i = 0; i < 4; i++)
            fans[i].transform.localPosition = (0.226f + distance) * GetAngle(i);
    }

    // ============================== 销毁 / End ==============================
    private void DestroySelf()
    {
        if (judgeResult != JudgeType.Miss)
        {
            if (isBreak) audioManager.PlayTapSound(judgeResult, false, isBreak);
            else if (isFirework) audioManager.PlayHanabiSound();
            else audioManager.PlayTouchSound();
        }
        noteManager.RemoveLoadedNote(this);
        End();
    }

    public override void End()
    {
        ReportResult();
        if (inputBound)
        {
            inputManager.UnbindSensor(Check, sensor);
            inputBound = false;
        }


        NotePool.Instance.Release(holdEffectPrefab, holdEffect);
        holdEffect = null!;
        NotePool.Instance.Release(prefabRef, gameObject);
    }

    private void ReportResult()
    {
        if (PlayManager.IsReloading) return;
        audioManager.StopTouchHoldSound();
        var realityHT = LastFor - 0.45f - (judgeDiff / 1000f);
        var percent = Math.Clamp((realityHT - playerIdleTime) / realityHT, 0, 1);
        JudgeType result = judgeResult;
        if (realityHT > 0)
        {
            if (percent >= 1f)
            {
                if (judgeResult == JudgeType.Miss) result = JudgeType.LateGood;
                else if (MathF.Abs((int)judgeResult - 7) == 6)
                    result = (int)judgeResult < 7 ? JudgeType.LateGreat : JudgeType.FastGreat;
                else result = judgeResult;
            }
            else if (percent >= 0.67f)
            {
                if (judgeResult == JudgeType.Miss) result = JudgeType.LateGood;
                else if (MathF.Abs((int)judgeResult - 7) == 6)
                    result = (int)judgeResult < 7 ? JudgeType.LateGreat : JudgeType.FastGreat;
                else if (judgeResult == JudgeType.Perfect)
                    result = (int)judgeResult < 7 ? JudgeType.LatePerfect1 : JudgeType.FastPerfect1;
            }
            else if (percent >= 0.33f)
            {
                if (MathF.Abs((int)judgeResult - 7) >= 6)
                    result = (int)judgeResult < 7 ? JudgeType.LateGood : JudgeType.FastGood;
                else result = (int)judgeResult < 7 ? JudgeType.LateGreat : JudgeType.FastGreat;
            }
            else if (percent >= 0.05f)
                result = (int)judgeResult < 7 ? JudgeType.LateGood : JudgeType.FastGood;
            else if (percent >= 0)
            {
                if (judgeResult == JudgeType.Miss) result = JudgeType.Miss;
                else result = (int)judgeResult < 7 ? JudgeType.LateGood : JudgeType.FastGood;
            }
        }

        switch (Majdata<InputManager>.Instance!.Mode)
        {
            case AutoPlayMode.Enable: result = JudgeType.Perfect; break;
            case AutoPlayMode.Random: result = (JudgeType)Random.Range(1, 14); break;
            case AutoPlayMode.DJAuto:
            case AutoPlayMode.Disable: break;
        }

        if (isMine)
            result = _isTouched ? JudgeType.Miss : JudgeType.Perfect;

        print($"TouchHold: {MathF.Round(percent * 100, 2)}%\nTotal Len : {MathF.Round(realityHT * 1000, 2)}ms");
        objectCounter.ReportResult(SimaiNoteType.TouchHold, result, isBreak);
        if (isFirework && result != JudgeType.Miss)
        {
            fireworkEffect.SetTrigger("Fire");
            firework.transform.position = transform.position;
        }
        if (!isJudged) noteManager.NextTouch(GetSensor());
        PlayJudgeEffect(result);
    }

    private void OnDestroy()
    {
        if (PlayManager.IsReloading) return;
        audioManager.StopTouchHoldSound();
        if (inputBound)
        {
            inputManager.UnbindSensor(Check, sensor);
            inputBound = false;
        }
    }

    protected override void PlayHoldEffect()
    {
        base.PlayHoldEffect();
        border.sprite = _borderSprite;
    }
    protected override void StopHoldEffect()
    {
        base.StopHoldEffect();
        border.sprite = skinManager.TouchHold_Border_Miss;
    }

    private void PlayJudgeEffect(JudgeType judgeResult)
    {
        //show effect
        if (judgeResult != JudgeType.Miss)
        {
            switch (judgeResult)
            {
                case JudgeType.LateGood:
                case JudgeType.FastGood:
                    Instantiate(gd_TouchEffect, transform.position, transform.rotation); break;
                case JudgeType.LateGreat:
                case JudgeType.LateGreat1:
                case JudgeType.LateGreat2:
                case JudgeType.FastGreat2:
                case JudgeType.FastGreat1:
                case JudgeType.FastGreat:
                    Instantiate(gr_TouchEffect, transform.position, transform.rotation); break;
                case JudgeType.LatePerfect2:
                case JudgeType.FastPerfect2:
                case JudgeType.LatePerfect1:
                case JudgeType.FastPerfect1:
                case JudgeType.Perfect:
                    Instantiate(touchEffect, transform.position, transform.rotation); break;
                default: break;
            }
        }

        if (EffectManager.showLevel)
        {
            var obj = Instantiate(judgeEffect, Vector3.zero, transform.rotation);
            var judgeObj = obj.transform.GetChild(0);
            if (sensor != SensorType.C) judgeObj.transform.position = GetPosition(-0.46f);
            else judgeObj.transform.position = new Vector3(0, -0.6f, 0);
            judgeObj.GetChild(0).transform.rotation = GetRotation();
            var anim = obj.GetComponent<Animator>();

            switch (judgeResult)
            {
                case JudgeType.LateGood:
                case JudgeType.FastGood:
                    judgeObj.GetChild(0).gameObject.GetComponent<SpriteRenderer>().sprite = skinManager.JudgeText[1]; break;
                case JudgeType.LateGreat:
                case JudgeType.LateGreat1:
                case JudgeType.LateGreat2:
                case JudgeType.FastGreat2:
                case JudgeType.FastGreat1:
                case JudgeType.FastGreat:
                    judgeObj.GetChild(0).gameObject.GetComponent<SpriteRenderer>().sprite = skinManager.JudgeText[2]; break;
                case JudgeType.LatePerfect2:
                case JudgeType.FastPerfect2:
                case JudgeType.LatePerfect1:
                case JudgeType.FastPerfect1:
                    judgeObj.GetChild(0).gameObject.GetComponent<SpriteRenderer>().sprite = skinManager.JudgeText[3]; break;
                case JudgeType.Perfect:
                    judgeObj.GetChild(0).gameObject.GetComponent<SpriteRenderer>().sprite = skinManager.JudgeText[4]; break;
                case JudgeType.Miss:
                    judgeObj.GetChild(0).gameObject.GetComponent<SpriteRenderer>().sprite = skinManager.JudgeText[0]; break;
                default: break;
            }
            anim.SetTrigger("touch");
        }

        if (EffectManager.showFL)
        {
            if (judgeResult == JudgeType.Miss || judgeResult == JudgeType.Perfect) return;
            var customSkin = GameObject.Find("Outline").GetComponent<SkinManager>();
            var obj = Instantiate(judgeEffect, Vector3.zero, transform.rotation);
            var flObj = obj.transform.GetChild(0);
            if (sensor != SensorType.C) flObj.transform.position = GetPosition(-0.92f);
            else flObj.transform.position = new Vector3(0, -1.08f, 0);
            flObj.GetChild(0).transform.rotation = GetRotation();
            var flAnim = obj.GetComponent<Animator>();
            obj.SetActive(true);
            if (judgeResult > JudgeType.Perfect)
                obj.transform.GetChild(0).GetChild(0).gameObject.GetComponent<SpriteRenderer>().sprite = customSkin.FastText;
            else
                obj.transform.GetChild(0).GetChild(0).gameObject.GetComponent<SpriteRenderer>().sprite = customSkin.LateText;
            flAnim.SetTrigger("touch");
        }
    }

    /// <summary>
    /// 获取当前坐标指定距离的坐标
    /// <para>方向：原点</para>
    /// </summary>
    Vector3 GetPosition(float distance)
    {
        var d = transform.position.magnitude;
        var ratio = MathF.Max(0, d + distance) / d;
        return transform.position * ratio;
    }
    private Quaternion GetRotation()
    {
        if (sensor == SensorType.C) return Quaternion.Euler(Vector3.zero);
        var d = Vector3.zero - transform.position;
        var deg = 180 + Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
        return Quaternion.Euler(new Vector3(0, 0, -deg));
    }
    private Vector3 GetAngle(int index)
    {
        var angle = Mathf.PI / 4 + index * (Mathf.PI / 2);
        return new Vector3(Mathf.Sin(angle), Mathf.Cos(angle));
    }

    public SensorType GetSensor() => GetSensor(areaPosition, startPosition);
    public static SensorType GetSensor(char areaPos, int startPos)
    {
        switch (areaPos)
        {
            case 'A': return (SensorType)(startPos - 1);
            case 'B': return (SensorType)(startPos + 7);
            case 'C': return SensorType.C;
            case 'D': return (SensorType)(startPos + 16);
            case 'E': return (SensorType)(startPos + 24);
            default: return SensorType.A1;
        }
    }
    Vector3 GetAreaPos(int index, char area)
    {
        // AreaDistance:  C: 0  E: 3.1  B: 2.21  A,D: 4.8
        if (area == 'C') return Vector3.zero;
        if (area == 'B')
        {
            var angle = (-index * (Mathf.PI / 4)) + ((Mathf.PI * 5) / 8);
            return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * 2.3f;
        }
        if (area == 'A')
        {
            var angle = (-index * (Mathf.PI / 4)) + ((Mathf.PI * 5) / 8);
            return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * 4.1f;
        }
        if (area == 'E')
        {
            var angle = (-index * (Mathf.PI / 4)) + ((Mathf.PI * 6) / 8);
            return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * 3.0f;
        }
        if (area == 'D')
        {
            var angle = (-index * (Mathf.PI / 4)) + ((Mathf.PI * 6) / 8);
            return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * 4.1f;
        }
        return Vector3.zero;
    }
    private void SetFanColor(Color color)
    {
        foreach (var fan in fansRenderers) fan.color = color;
        border.color = color;
    }
}
