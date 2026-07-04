#nullable enable

#region

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

using static MajCtx;

#endregion

public class EffectManager : MonoBehaviour
{
    public const int EFFECT_COUNT = 8;

    private static readonly int PerfectHash = Animator.StringToHash("perfect");
    private static readonly int GreatHash = Animator.StringToHash("great");
    private static readonly int BreakHash = Animator.StringToHash("break");
    private static readonly int BGreatHash = Animator.StringToHash("bGreat");
    private static readonly int BGoodHash = Animator.StringToHash("bGood");

    public static bool showFL;
    public static bool showLevel;

    public NativeArray<EffectData> judgeEffectRequests = new(EFFECT_COUNT, Allocator.Persistent);
    public unsafe EffectData* JudgeEffectRequestsPtr => (EffectData*)judgeEffectRequests.GetUnsafePtr();

    private readonly Animator[] judgeAnimators = new Animator[8];
    private readonly GameObject[] judgeEffects = new GameObject[8];
    private readonly Animator[] tapAnimators = new Animator[8];
    private readonly GameObject[] tapEffects = new GameObject[8];
    private readonly GameObject[] greatEffects = new GameObject[8];
    private readonly GameObject[] goodEffects = new GameObject[8];
    private readonly Animator[] fastLateAnims = new Animator[8];
    private readonly GameObject[] fastLateEffects = new GameObject[8];
    private readonly GameObject[] holdEffects = new GameObject[8];
    private readonly Material[] holdMaterials = new Material[8];
    Sprite[] judgeText;

    private void Awake()
    {
        _effectManager = this;
    }

    private void Start()
    {
        var tapEffectParent = transform.GetChild(0).gameObject;
        var greatEffectParent = transform.GetChild(1).gameObject;
        var goodEffectParent = transform.GetChild(2).gameObject;
        var judgeEffectParent = transform.GetChild(3).gameObject;
        var flParent = transform.GetChild(4).gameObject;
        var holdEffectParent = transform.GetChild(5).gameObject;

        for (var i = 0; i < 8; i++)
        {
            judgeEffects[i] = judgeEffectParent.transform.GetChild(i).gameObject;
            judgeAnimators[i] = judgeEffects[i].GetComponent<Animator>();

            fastLateEffects[i] = flParent.transform.GetChild(i).gameObject;
            fastLateAnims[i] = fastLateEffects[i].GetComponent<Animator>();

            goodEffects[i] = goodEffectParent.transform.GetChild(i).gameObject;
            greatEffects[i] = greatEffectParent.transform.GetChild(i).gameObject;
            tapEffects[i] = tapEffectParent.transform.GetChild(i).gameObject;
            tapAnimators[i] = tapEffects[i].GetComponent<Animator>();

            holdEffects[i] = holdEffectParent.transform.GetChild(i).gameObject;
            holdMaterials[i] = holdEffects[i].GetComponent<ParticleSystemRenderer>().material;

            goodEffects[i].SetActive(false);
            greatEffects[i].SetActive(false);
            tapEffects[i].SetActive(false);
            holdEffects[i].SetActive(false);
        }

        judgeText = _noteSkinManager.JudgeText;

        foreach (var judgeEffect in judgeEffects)
        {
            judgeEffect.transform.GetChild(0).GetChild(0).gameObject.GetComponent<SpriteRenderer>().sprite =
                _noteSkinManager.JudgeText[0];
            judgeEffect.transform.GetChild(0).GetChild(1).gameObject.GetComponent<SpriteRenderer>().sprite =
                _noteSkinManager.JudgeText_Break;
        }
    }

    private void Update()
    {
        ProcessEffectRequests();
    }

    private void OnDestroy()
    {
        if (judgeEffectRequests.IsCreated) judgeEffectRequests.Dispose();
    }

    public void SetDisplayMode(JudgeDisplayMode mode)
    {
        switch (mode)
        {
            case JudgeDisplayMode.None:
                showFL = showLevel = false;
                break;
            case JudgeDisplayMode.FastLate:
                showFL = true;
                showLevel = false;
                break;
            case JudgeDisplayMode.Level:
                showFL = false;
                showLevel = true;
                break;
            case JudgeDisplayMode.Both:
            default:
                showFL = showLevel = true;
                break;
        }
    }

    public void ProcessEffectRequests()
    {
        for (var i = 0; i < judgeEffectRequests.Length; i++)
        {
            var req = judgeEffectRequests[i];
            if (req.HasEffect)
            {
                ResetEffect(i);
                PlayJudgeEffect(i, req.JudgeGrade, req.IsBreak);
                PlayFastLateEffect(i, req.JudgeGrade);
            }
            holdEffects[i].SetActive(req.HasHolding);
            if (req.HasHolding)
            {
                holdMaterials[i].SetColor("_Color", req.HoldingColor);
            }
        }

        for (var i = 0; i < judgeEffectRequests.Length; i++)
            judgeEffectRequests[i] = default;
    }

    public void ResetEffect(int pos)
    {
        tapEffects[pos].SetActive(false);
        greatEffects[pos].SetActive(false);
        goodEffects[pos].SetActive(false);
    }

    private void PlayJudgeEffect(int pos, JudgeGrade judge, bool isBreak)
    {
        switch (judge)
        {
            case JudgeGrade.LateGood:
            case JudgeGrade.FastGood:
                SetJudgeEffect(pos, judgeText[1]);
                if (isBreak)
                {
                    tapEffects[pos].SetActive(true);
                    tapAnimators[pos].speed = 0.9f;
                    tapAnimators[pos].SetTrigger(BGoodHash);
                }
                else
                {
                    goodEffects[pos].SetActive(true);
                }
                break;
            case JudgeGrade.LateGreat3rd:
            case JudgeGrade.LateGreat2nd:
            case JudgeGrade.LateGreat:
            case JudgeGrade.FastGreat3rd:
            case JudgeGrade.FastGreat2nd:
            case JudgeGrade.FastGreat:
                SetJudgeEffect(pos, judgeText[2]);
                if (isBreak)
                {
                    tapEffects[pos].SetActive(true);
                    tapAnimators[pos].speed = 0.9f;
                    tapAnimators[pos].SetTrigger(BGreatHash);
                }
                else
                {
                    greatEffects[pos].SetActive(true);
                    greatEffects[pos].gameObject.GetComponent<Animator>().SetTrigger(GreatHash);
                }
                break;
            case JudgeGrade.LatePerfect3rd:
            case JudgeGrade.LatePerfect2nd:
            case JudgeGrade.FastPerfect3rd:
            case JudgeGrade.FastPerfect2nd:
                SetJudgeEffect(pos, judgeText[3]);
                tapEffects[pos].SetActive(true);
                if (isBreak)
                {
                    tapAnimators[pos].speed = 0.9f;
                    tapAnimators[pos].SetTrigger(BreakHash);
                }
                break;
            case JudgeGrade.Perfect:
                SetJudgeEffect(pos, judgeText[4]);
                tapEffects[pos].SetActive(true);
                if (isBreak)
                {
                    tapAnimators[pos].speed = 0.9f;
                    tapAnimators[pos].SetTrigger(BreakHash);
                }
                break;
            default:
                SetJudgeEffect(pos, judgeText[0]);
                break;
        }

        if (showLevel)
        {
            if (isBreak && judge == JudgeGrade.Perfect)
                judgeAnimators[pos].SetTrigger(BreakHash);
            else
                judgeAnimators[pos].SetTrigger(PerfectHash);
        }
    }

    private void SetJudgeEffect(int pos, Sprite sprite)
    {
        if (!showLevel) return;
        judgeEffects[pos].transform.GetChild(0).GetChild(0).gameObject.GetComponent<SpriteRenderer>().sprite = sprite;
    }

    private void PlayFastLateEffect(int pos, JudgeGrade judge)
    {
        if (!showFL) return;

        if (judge is JudgeGrade.Miss or JudgeGrade.Perfect)
        {
            fastLateEffects[pos].SetActive(false);
            return;
        }

        fastLateEffects[pos].SetActive(true);
        var isFast = (int)judge > 7;
        if (isFast)
            fastLateEffects[pos].transform.GetChild(0).GetChild(0).gameObject.GetComponent<SpriteRenderer>().sprite = _noteSkinManager.FastText;
        else
            fastLateEffects[pos].transform.GetChild(0).GetChild(0).gameObject.GetComponent<SpriteRenderer>().sprite = _noteSkinManager.LateText;
        fastLateAnims[pos].SetTrigger(PerfectHash);
    }

    public void ResetState()
    {
        for (var i = 0; i < judgeEffectRequests.Length; i++)
            judgeEffectRequests[i] = default;
    }
}

public struct EffectData
{
    public bool HasEffect;
    public JudgeGrade JudgeGrade;
    public bool IsBreak;
    public bool HasHolding;
    public Color HoldingColor;
}