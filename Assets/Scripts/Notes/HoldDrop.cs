#nullable enable

#region

using System;
using MajSimai;
using UnityEngine;
using Random = UnityEngine.Random;

using static MajCtx;

#endregion

public class HoldDrop : NoteLongBase
{

    public GameObject tapLine;

    private Animator animator;
    private bool holdAnimStart;
    private SpriteRenderer lineSpriteRender;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer holdEndRender;
    private SpriteRenderer exSpriteRender;

    private bool isTouched = false; //for mine judge
    private bool isPlayedSFX = false; //for Enable / Random mode


    private void Start()
    {
        var notes = GameObject.Find("Notes").transform;
        holdEffect = Instantiate(holdEffect, notes);
        holdEffect.SetActive(false);
        material = holdEffect.GetComponent<ParticleSystemRenderer>().material;

        tapLine = Instantiate(tapLine, notes);
        tapLine.SetActive(false);

        animator = GetComponent<Animator>();
        animator.enabled = false;

        lineSpriteRender = tapLine.GetComponent<SpriteRenderer>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        holdEndRender = transform.GetChild(1).GetComponent<SpriteRenderer>();
        exSpriteRender = transform.GetChild(0).GetComponent<SpriteRenderer>();

        spriteRenderer.sortingOrder += noteSortOrder;
        holdEndRender.sortingOrder += noteSortOrder;
        exSpriteRender.sortingOrder += noteSortOrder;

        LoadSkin();
        spriteRenderer.forceRenderingOff = true;
        exSpriteRender.forceRenderingOff = true;
        holdEndRender.enabled = false;

        sensor = (SensorType)startPosition - 1;
        _inputManager.BindArea(Check, sensor);
    }

    private void LoadSkin()
    {
        lineSpriteRender.sprite = _skinManager.Line;
        spriteRenderer.sprite = _skinManager.Hold;
        exSpriteRender.sprite = _skinManager.Hold_Ex;
        holdEndRender.sprite = _skinManager.HoldEnd;
        if (isEx)
        {
            exSpriteRender.color = _skinManager.Ex;
        }
        if (isEach)
        {
            spriteRenderer.sprite = _skinManager.Hold_Each;
            lineSpriteRender.sprite = _skinManager.Line_Each;
            holdEndRender.sprite = _skinManager.HoldEnd_Each;
            if (isEx) exSpriteRender.color = _skinManager.Ex_Each;
        }
        if (isBreak)
        {
            spriteRenderer.sprite = _skinManager.Hold_Break;
            lineSpriteRender.sprite = _skinManager.Line_Break;
            holdEndRender.sprite = _skinManager.HoldEnd_Break;
            if (isEx) exSpriteRender.color = _skinManager.Ex_Break;
            spriteRenderer.material = _skinManager.BreakMaterial;
        }
        if (isMine)
        {
            if (isBreak)
                spriteRenderer.sprite = _skinManager.Hold_Break_Mine;
            else
                spriteRenderer.sprite = _skinManager.Hold_Mine;
            lineSpriteRender.sprite = _skinManager.Line_Mine;
        }
    }

    private void FixedUpdate()
    {
        var timing = _timeProvider.NoteTime - time;
        var remainingTime = GetRemainingTime();

        if (isMine && !isJudged && timing >= 0.016667f)
        {
            judgeResult = JudgeType.Perfect;
            isJudged = true;
            _noteJudgeManager.NextNote(startPosition);
        }
        else if (remainingTime == 0 && isJudged) // Hold完成后Destroy
        {
            DestroySelf();
        }
        else if (timing >= -0.01f)
        {
            // AutoPlay相关
            switch (_inputManager.Mode)
            {
                case AutoPlayMode.Enable:
                    if (!isJudged)
                        _noteJudgeManager.NextNote(startPosition);

                    if (isMine)
                        judgeResult = JudgeType.Miss;
                    else
                        judgeResult = JudgeType.Perfect;

                    isJudged = true;
                    isTouched = true; //算是点到了
                    PlayHoldEffect();
                    PlaySFX();
                    return;
                case AutoPlayMode.DJAuto:
                    if (!isMine) //mine buda
                        _inputManager.SetAreaOn(sensor, guid);
                    break;
                case AutoPlayMode.Random:
                    if (!isJudged)
                    {
                        _noteJudgeManager.NextNote(startPosition);
                        judgeResult = (JudgeType)Random.Range(1, 14);
                        if (isMine)
                        {
                            if (judgeResult > JudgeType.Perfect) //Fast
                            {
                                judgeResult = JudgeType.Miss;
                            }
                            else
                            {
                                judgeResult = JudgeType.Perfect;
                            }

                            if (judgeResult != JudgeType.Miss) isTouched = true; //必有摸
                        }
                        isJudged = true;
                    }
                    PlayHoldEffect();
                    PlaySFX();
                    return;
            }
        }

        if (isJudged) // 头部判定完成后开始累计按压时长
        {
            if (!_timeProvider.IsStart) // 忽略暂停
                return;

            var on = _inputManager.CheckArea(sensor);

            if (on)
            {
                isTouched = true;
                PlayHoldEffect();
            }
            else
            {
                StopHoldEffect();
            }

            if (timing <= 0.25f) // 忽略头部15帧
                return;
            if (remainingTime <= 0.2f) // 忽略尾部12帧
                return;

            if (!on)
            {
                playerIdleTime += Time.fixedDeltaTime;
            }
        }
        else if (timing > 0.15f && !isJudged) // 头部Miss
        {
            judgeDiff = 150;
            judgeResult = JudgeType.Miss;
            isJudged = true;
            _noteJudgeManager.NextNote(startPosition);
        }
    }
    void Check(object sender, InputEventArgs arg)
    {
        if (arg.Type != sensor)
            return;
        if (isJudged || !_noteJudgeManager.CanJudge(gameObject, startPosition))
            return;
        if (_inputManager.Mode is AutoPlayMode.Enable or AutoPlayMode.Random)
            return;

        if (arg.IsClick)
        {
            if (!_inputManager.IsIdle(arg))
                return;

            _inputManager.SetBusy(arg);
            Judge();
            if (isJudged)
            {
                _inputManager.UnbindArea(Check, sensor);
                _noteJudgeManager.NextNote(startPosition);
            }
        }
    }
    private void Judge() //hold类头判正常检查，在destroy统一处理
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

        var timing = _timeProvider.NoteTime - time;
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
        if (isFast)
            judgeDiff = 0;
        else
            judgeDiff = diff;

        judgeResult = result;
        isJudged = true;
        PlayHoldEffect();
        PlaySFX();
    }

    private void Update()
    {
        var timing = _timeProvider.NoteTime - time;
        var distance = timing * speed + 4.8f;
        var destScale = distance * 0.4f + 0.51f;
        var holdTime = timing - LastFor;
        var holdDistance = holdTime * speed + 4.8f;

        var fakeTiming = _timeProvider.FakeNoteTime - _timeProvider.GetPositionAtTime(time);
        var fakeDistance = fakeTiming * speed + 4.8f;
        var fakeDestScale = fakeDistance * 0.4f + 0.51f;
        var fakeLastfor = _timeProvider.GetPositionAtTime(time + LastFor) - _timeProvider.GetPositionAtTime(time);
        var fakeHoldTime = fakeTiming - fakeLastfor;
        var fakeHoldDistance = fakeHoldTime * speed + 4.8f;

        if (!usingSV)
        {
            //fakeTiming = timing;
            fakeDistance = distance;
            fakeDestScale = destScale;
            fakeHoldTime = holdTime;
            fakeHoldDistance = holdDistance;
        }

        if (fakeDestScale < 0f)
        {
            return;
        }

        spriteRenderer.forceRenderingOff = false;
        if (isEx) exSpriteRender.forceRenderingOff = false;
        spriteRenderer.size = new Vector2(1.22f, 1.4f);

        if (fakeHoldTime >= 0 ||
            fakeHoldTime >= 0 && LastFor <= 0.15f)
        {
            tapLine.transform.localScale = new Vector3(1f, 1f, 1f);
            transform.position = getPositionFromDistance(4.8f);
            return;
        }


        transform.rotation = Quaternion.Euler(0, 0, -22.5f + -45f * (startPosition - 1));
        tapLine.transform.rotation = transform.rotation;
        holdEffect.transform.position = getPositionFromDistance(4.8f);

        if (isBreak &&
            !holdAnimStart &&
            !isJudged)
        {
            var extra = Math.Max(Mathf.Sin(_timeProvider.GetFrame() * 0.17f) * 0.5f, 0);
            spriteRenderer.material.SetFloat("_Brightness", 0.95f + extra);
        }


        if (fakeDestScale > 0.3f) tapLine.SetActive(true);

        if (fakeDistance < 1.225f)
        {
            transform.localScale = new Vector3(fakeDestScale, fakeDestScale);
            spriteRenderer.size = new Vector2(1.22f, 1.42f);
            fakeDistance = 1.225f;
            var pos = getPositionFromDistance(fakeDistance);
            transform.position = pos;
        }
        else
        {
            if (fakeHoldDistance < 1.225f && fakeDistance >= 4.8f) // 头到达 尾未出现
            {
                fakeHoldDistance = 1.225f;
                fakeDistance = 4.8f;
            }
            else if (fakeHoldDistance < 1.225f && fakeDistance < 4.8f) // 头未到达 尾未出现
            {
                fakeHoldDistance = 1.225f;
            }
            else if (fakeHoldDistance >= 1.225f && fakeDistance >= 4.8f) // 头到达 尾出现
            {
                fakeDistance = 4.8f;

                holdEndRender.enabled = true;
            }
            else if (fakeHoldDistance >= 1.225f && fakeDistance < 4.8f) // 头未到达 尾出现
            {
                holdEndRender.enabled = true;
            }

            var dis = (fakeDistance - fakeHoldDistance) / 2 + fakeHoldDistance;
            transform.position = getPositionFromDistance(dis); //0.325
            var size = fakeDistance - fakeHoldDistance + 1.4f;
            spriteRenderer.size = new Vector2(1.22f, size);
            holdEndRender.transform.localPosition = new Vector3(0f, 0.6825f - size / 2);
            transform.localScale = new Vector3(1f, 1f);
        }

        var lineScale = Mathf.Abs(fakeDistance / 4.8f);
        lineScale = lineScale >= 1f ? 1f : lineScale;
        tapLine.transform.localScale = new Vector3(lineScale, lineScale, 1f);
        exSpriteRender.size = spriteRenderer.size;
    }

    private void DestroySelf()
    {
        PlayJudgeSFX();
        Destroy(tapLine);
        Destroy(holdEffect);
        Destroy(gameObject);
    }
    private void OnDestroy()
    {
        if (PlayManager.IsReloading) return;
        var realityHT = LastFor - 0.3f - (judgeDiff / 1000f);
        var percent = Math.Clamp((realityHT - playerIdleTime) / realityHT, 0, 1);
        var result = judgeResult; //头判
        if (realityHT > 0)
        {
            if (percent >= 1f)
            {
                if (judgeResult == JudgeType.Miss)
                    result = JudgeType.LateGood;
                else if (Math.Abs((int)judgeResult - 7) == 6)
                    result = (int)judgeResult < 7 ? JudgeType.LateGreat : JudgeType.FastGreat;
                else
                    result = judgeResult;
            }
            else if (percent >= 0.67f)
            {
                if (judgeResult == JudgeType.Miss)
                    result = JudgeType.LateGood;
                else if (Math.Abs((int)judgeResult - 7) == 6)
                    result = (int)judgeResult < 7 ? JudgeType.LateGreat : JudgeType.FastGreat;
                else if (judgeResult == JudgeType.Perfect)
                    result = (int)judgeResult < 7 ? JudgeType.LatePerfect1 : JudgeType.FastPerfect1;
            }
            else if (percent >= 0.33f)
            {
                if (Math.Abs((int)judgeResult - 7) >= 6)
                    result = (int)judgeResult < 7 ? JudgeType.LateGood : JudgeType.FastGood;
                else
                    result = (int)judgeResult < 7 ? JudgeType.LateGreat : JudgeType.FastGreat;
            }
            else if (percent >= 0.05f)
                result = (int)judgeResult < 7 ? JudgeType.LateGood : JudgeType.FastGood;
            else if (percent >= 0)
            {
                if (judgeResult == JudgeType.Miss)
                    result = JudgeType.Miss;
                else
                    result = (int)judgeResult < 7 ? JudgeType.LateGood : JudgeType.FastGood;
            }
        }

        switch (_inputManager.Mode)
        {
            case AutoPlayMode.Enable:
                result = JudgeType.Perfect;
                break;
            case AutoPlayMode.Random:
                result = (JudgeType)Random.Range(1, 14);
                break;
            case AutoPlayMode.DJAuto:
            case AutoPlayMode.Disable:
                break;
        }

        if (isMine) //覆盖掉前面的判定
        {
            if (isTouched)
                result = JudgeType.Miss;
            else
                result = JudgeType.Perfect;
        }

        _effectManager.PlayEffect(startPosition, isBreak, (JudgeGrade)result);
        _effectManager.PlayFastLate(startPosition, (JudgeGrade)result, transform.position);
        print($"Hold: {MathF.Round(percent * 100, 2)}%\nTotal Len : {MathF.Round(realityHT * 1000, 2)}ms");

        _objectCounter.ReportResult(SimaiNoteType.Hold, (JudgeGrade)result, isBreak);
        if (!isJudged)
            _noteJudgeManager.NextNote(startPosition);

        _inputManager.SetAreaOff(sensor, guid);
        _inputManager.UnbindArea(Check, sensor);
    }
    protected override void PlayHoldEffect()
    {
        base.PlayHoldEffect();
        _effectManager.ResetEffect(startPosition - 1);
        if (LastFor <= 0.3)
            return;
        if (!holdAnimStart && _timeProvider.NoteTime - time >= 0.1f && !isMine)//忽略开头6帧与结尾12帧和mine
        {
            holdAnimStart = true;

            if (isBreak)
            {
                spriteRenderer.sprite = _skinManager.Hold_Break_On;
                animator.runtimeAnimatorController = _skinManager.Shine_Break;
            }
            else if (isEach)
            {
                spriteRenderer.sprite = _skinManager.Hold_Each_On;
                animator.runtimeAnimatorController = _skinManager.Shine;
            }
            else if (isMine)
            {
                if (isBreak)
                    spriteRenderer.sprite = _skinManager.Hold_Break_Mine_On;
                else
                    spriteRenderer.sprite = _skinManager.Hold_Mine_On;
                animator.runtimeAnimatorController = _skinManager.Shine;
            }
            else
            {
                spriteRenderer.sprite = _skinManager.Hold_On;
                animator.runtimeAnimatorController = _skinManager.Shine;
            }
            animator.enabled = true;
        }
    }
    protected override void StopHoldEffect()
    {
        base.StopHoldEffect();
        holdAnimStart = false;
        animator.enabled = false;
        if (!isMine)
            spriteRenderer.sprite = _skinManager.Hold_Off;
    }


    private void PlayJudgeSFX()
    {
        if (isMine) return;
        _audioManager.PlayTapSound(judgeResult, false, false);
    }

    private void PlaySFX()
    {
        if (isPlayedSFX || isMine) return;
        _audioManager.PlayTapSound(judgeResult, isEx, isBreak);
        isPlayedSFX = true;
    }
}