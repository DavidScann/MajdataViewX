#nullable enable

using MajdataViewX.Base;
using MajdataViewX.Types.Enums;
using MajdataViewX.Types.Input;
using MajdataViewX.Types.Notes;
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

using static MajdataViewX.Base.MajCtx;

namespace MajdataViewX.Managers
{
    public class EffectManager : MonoBehaviour
    {
        private const int EFFECT_COUNT = BUTTON_COUNT + SENSOR_COUNT;

        private static readonly int PerfectHash = Animator.StringToHash("perfect");
        private static readonly int GreatHash = Animator.StringToHash("great");
        private static readonly int GoodHash = Animator.StringToHash("good");
        private static readonly int BPerfectHash = Animator.StringToHash("bPerfect");
        private static readonly int BGreatHash = Animator.StringToHash("bGreat");
        private static readonly int BGoodHash = Animator.StringToHash("bGood");
        private static readonly int FireHash = Animator.StringToHash("fire");

        [SerializeField] 
        private GameObject effectPrefab = null!;

        public NativeArray<EffectData> judgeEffectRequests = new(EFFECT_COUNT, Allocator.Persistent);
        public unsafe EffectData* JudgeEffectRequestsPtr => (EffectData*)judgeEffectRequests.GetUnsafePtr();

        private readonly Animator[] _tapAnimators = new Animator[EFFECT_COUNT];

        private readonly GameObject[] _holdEffects = new GameObject[EFFECT_COUNT];
        private readonly Material[] _holdMaterials = new Material[EFFECT_COUNT];

        private readonly GameObject[] _touchEffects = new GameObject[EFFECT_COUNT];
        private readonly Animator[] _touchAnimators = new Animator[EFFECT_COUNT];

        private readonly Animator[] _judgeAnimators = new Animator[EFFECT_COUNT];
        private readonly SpriteRenderer[] _judgeRenderers = new SpriteRenderer[EFFECT_COUNT];
        private readonly SpriteRenderer[] _judgeBreakCovers = new SpriteRenderer[EFFECT_COUNT];

        private readonly SpriteRenderer[] _fastLateRenderers = new SpriteRenderer[EFFECT_COUNT];

        private bool showFL;
        private bool showLevel;
        private JudgeDisplayMode displayMode = JudgeDisplayMode.Both;

        /// <summary>
        /// In Fast/Late mode the slide-end SlideOK is hidden for CRITICAL
        /// PERFECT judgments (only the off-perfect grades keep their OK).
        /// </summary>
        public bool HideCriticalSlideOk => displayMode == JudgeDisplayMode.FastLate;

        private GameObject _fireworkEffect = null!;
        private Animator _fireworkAnimator = null!;

        private void Awake()
        {
            _effectManager = this;
        }

        private void Start()
        {
            var parent = GameObject.Find("NoteEffects");

            for (var i = 0; i < EFFECT_COUNT; i++)
            {
                float2 pos;
                if (i < BUTTON_COUNT)
                {
                    pos = MajPos.GetBtnPos(i);
                }
                else
                {
                    pos = MajPos.GetAreaPos((SensorType)i - 8);
                }
                float ang = 0f;
                if (i - 8 < 16)       // 1~8, A1~B8
                    ang = -45f * (i % 8) - 22.5f;
                else if (i - 8 > 16)  // D1~E8
                    ang = -45f * (i % 8 - 1) - 22.5f;

                var effect = Instantiate(
                    effectPrefab,
                    new Vector3(pos.x, pos.y, 0),
                    Quaternion.Euler(new Vector3(0, 0, ang)),
                    parent.transform);

                var tapEffect = effect.transform.GetChild(0).gameObject;
                _tapAnimators[i] = tapEffect.GetComponent<Animator>();
                if (i > 7) tapEffect.SetActive(false); // touch 部分的不要了 

                _holdEffects[i] = effect.transform.GetChild(1).gameObject;
                _holdMaterials[i] = _holdEffects[i].GetComponent<ParticleSystemRenderer>().material;
                _holdEffects[i].SetActive(false);

                _touchEffects[i] = effect.transform.GetChild(2).gameObject;
                _touchEffects[i].transform.localEulerAngles = new Vector3(0, 0, -ang); // 回正
                _touchAnimators[i] = _touchEffects[i].GetComponent<Animator>();
                if (i <= 7) _touchEffects[i].SetActive(false); // tap 部分的不要了 

                var judgeEffect = effect.transform.GetChild(3).gameObject;
                _judgeAnimators[i] = judgeEffect.GetComponent<Animator>();
                _judgeRenderers[i] = judgeEffect.transform.GetChild(0).GetChild(0).gameObject.GetComponent<SpriteRenderer>();
                _judgeBreakCovers[i] = judgeEffect.transform.GetChild(0).GetChild(1).gameObject.GetComponent<SpriteRenderer>();
                _judgeBreakCovers[i].sprite = _noteSkinManager.JudgeText_BPerfect;
                _fastLateRenderers[i] = judgeEffect.transform.GetChild(1).GetChild(0).GetComponent<SpriteRenderer>();

                _fireworkEffect = GameObject.Find("FireworkEffect");
                _fireworkAnimator = _fireworkEffect.GetComponent<Animator>();
            }
        }

        private void OnDestroy()
        {
            if (judgeEffectRequests.IsCreated) judgeEffectRequests.Dispose();
        }

        public void ProcessEffectRequests()
        {
            for (var i = 0; i < judgeEffectRequests.Length; i++)
            {
                var req = judgeEffectRequests[i];

                if (!req.IsMine ||
                    (req.IsMine && req.JudgeGrade is (JudgeGrade.Miss or JudgeGrade.TooFast)))
                {
                    if (req.Effect.HasFlag(EffectType.Tap))
                    {
                        PlayTapEffect(i, req.JudgeGrade, req.IsBreak);
                    }
                    if (req.Effect.HasFlag(EffectType.Touch))
                    {
                        PlayTouchEffect(i, req.JudgeGrade, req.IsBreak);
                    }
                    if (req.Effect.HasFlag(EffectType.Firework))
                    {
                        PlayFireworkEffect(i);
                    }
                }

                _holdEffects[i].SetActive(req.HasHolding);
                if (req.HasHolding)
                {
                    _holdMaterials[i].SetColor("_Color", req.HoldingColor);
                }
            }

            for (var i = 0; i < judgeEffectRequests.Length; i++)
                judgeEffectRequests[i] = default;
        }

        public void SetDisplayMode(JudgeDisplayMode mode)
        {
            displayMode = mode;
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
                    showFL = showLevel = true;
                    break;
                default:
                    showFL = showLevel = true;
                    break;
            }

            // The judge clip animates the active states of the level text and
            // the fast/late text, so visibility is controlled by the sprites:
            // keep the level text and its break cover blank in Fast/Late-only
            // (and None) modes, where the shared clip still plays.
            for (var i = 0; i < EFFECT_COUNT; i++)
            {
                if (_judgeRenderers[i] != null && !showLevel)
                    _judgeRenderers[i].sprite = null;
                if (_judgeBreakCovers[i] != null)
                    _judgeBreakCovers[i].sprite = showLevel ? _noteSkinManager.JudgeText_BPerfect : null;
            }
        }

        private void PlayTapEffect(int pos, JudgeGrade judge, bool isBreak)
        {
            // Effect & Judge Text
            switch (judge)
            {
                case JudgeGrade.LateGood:
                case JudgeGrade.FastGood:
                    if (showLevel)
                        _judgeRenderers[pos].sprite = _noteSkinManager.JudgeText[1];
                    if (isBreak)
                    {
                        _tapAnimators[pos].speed = 0.9f;
                        _tapAnimators[pos].SetTrigger(BGoodHash);
                    }
                    else
                    {
                        _tapAnimators[pos].speed = 1f;
                        _tapAnimators[pos].SetTrigger(GoodHash);
                    }
                    break;
                case JudgeGrade.LateGreat3rd:
                case JudgeGrade.LateGreat2nd:
                case JudgeGrade.LateGreat1st:
                case JudgeGrade.FastGreat3rd:
                case JudgeGrade.FastGreat2nd:
                case JudgeGrade.FastGreat1st:
                    if (showLevel)
                        _judgeRenderers[pos].sprite = _noteSkinManager.JudgeText[2];
                    if (isBreak)
                    {
                        _tapAnimators[pos].speed = 0.9f;
                        _tapAnimators[pos].SetTrigger(BGreatHash);
                    }
                    else
                    {
                        _tapAnimators[pos].speed = 1f;
                        _tapAnimators[pos].SetTrigger(GreatHash);
                    }
                    break;
                case JudgeGrade.LatePerfect3rd:
                case JudgeGrade.LatePerfect2nd:
                case JudgeGrade.FastPerfect3rd:
                case JudgeGrade.FastPerfect2nd:
                    if (showLevel)
                        _judgeRenderers[pos].sprite = _noteSkinManager.JudgeText[3];
                    if (isBreak)
                    {
                        _tapAnimators[pos].speed = 0.9f;
                        _tapAnimators[pos].SetTrigger(BPerfectHash);
                    }
                    else
                    {
                        _tapAnimators[pos].speed = 1f;
                        _tapAnimators[pos].SetTrigger(PerfectHash);
                    }
                    break;
                case JudgeGrade.LateCritical:
                case JudgeGrade.FastCritical:
                    if (showLevel)
                        _judgeRenderers[pos].sprite = _noteSkinManager.JudgeText[4];
                    if (isBreak)
                    {
                        _tapAnimators[pos].speed = 0.9f;
                        _tapAnimators[pos].SetTrigger(BPerfectHash);
                    }
                    else
                    {
                        _tapAnimators[pos].speed = 1f;
                        _tapAnimators[pos].SetTrigger(PerfectHash);
                    }
                    break;
                default:
                    if (showLevel)
                        _judgeRenderers[pos].sprite = _noteSkinManager.JudgeText[0];
                    break;
            }

            if (!showLevel)
                _judgeRenderers[pos].sprite = null;

            // Judge Anim: the clip reveals the level text and the fast/late
            // text together, so it must run whenever either is shown.
            if (showLevel || showFL)
            {
                if (isBreak && (judge is JudgeGrade.LateCritical or JudgeGrade.FastCritical))
                    _judgeAnimators[pos].SetTrigger(BPerfectHash);
                else
                    _judgeAnimators[pos].SetTrigger(PerfectHash);
            }

            // Fast / Late
            if (showFL)
            {
                if (judge is JudgeGrade.Miss or JudgeGrade.LateCritical or JudgeGrade.FastCritical)
                {
                    _fastLateRenderers[pos].sprite = null;
                }
                else
                {
                    var isFast = judge <= JudgeGrade.FastCritical;
                    if (isFast)
                        _fastLateRenderers[pos].sprite = _noteSkinManager.FastText;
                    else
                        _fastLateRenderers[pos].sprite = _noteSkinManager.LateText;
                }
            }
            else
            {
                _fastLateRenderers[pos].sprite = null;
            }
        }

        private void PlayTouchEffect(int pos, JudgeGrade judge, bool isBreak)
        {
            // Effect & Judge Text
            switch (judge)
            {
                case JudgeGrade.LateGood:
                case JudgeGrade.FastGood:
                    if (showLevel)
                        _judgeRenderers[pos].sprite = _noteSkinManager.JudgeText[1];
                    _touchAnimators[pos].SetTrigger(GoodHash);
                    break;
                case JudgeGrade.LateGreat3rd:
                case JudgeGrade.LateGreat2nd:
                case JudgeGrade.LateGreat1st:
                case JudgeGrade.FastGreat3rd:
                case JudgeGrade.FastGreat2nd:
                case JudgeGrade.FastGreat1st:
                    if (showLevel)
                        _judgeRenderers[pos].sprite = _noteSkinManager.JudgeText[2];
                    _touchAnimators[pos].SetTrigger(GreatHash);
                    break;
                case JudgeGrade.LatePerfect3rd:
                case JudgeGrade.LatePerfect2nd:
                case JudgeGrade.FastPerfect3rd:
                case JudgeGrade.FastPerfect2nd:
                    if (showLevel)
                        _judgeRenderers[pos].sprite = _noteSkinManager.JudgeText[3];
                    _touchAnimators[pos].SetTrigger(PerfectHash);
                    break;
                case JudgeGrade.LateCritical:
                case JudgeGrade.FastCritical:
                    if (showLevel)
                        _judgeRenderers[pos].sprite = _noteSkinManager.JudgeText[4];
                    _touchAnimators[pos].SetTrigger(PerfectHash);
                    break;
                default:
                    if (showLevel)
                        _judgeRenderers[pos].sprite = _noteSkinManager.JudgeText[0];
                    break;
            }

            if (!showLevel)
                _judgeRenderers[pos].sprite = null;

            // Judge Anim: the clip reveals the level text and the fast/late
            // text together, so it must run whenever either is shown.
            if (showLevel || showFL)
            {
                if (isBreak && (judge is JudgeGrade.LateCritical or JudgeGrade.FastCritical))
                    _judgeAnimators[pos].SetTrigger(BPerfectHash);
                else
                    _judgeAnimators[pos].SetTrigger(PerfectHash);
            }

            // Fast / Late
            if (showFL)
            {
                if (judge is JudgeGrade.Miss or JudgeGrade.LateCritical or JudgeGrade.FastCritical)
                {
                    _fastLateRenderers[pos].sprite = null;
                }
                else
                {
                    var isFast = judge <= JudgeGrade.FastCritical;
                    if (isFast)
                        _fastLateRenderers[pos].sprite = _noteSkinManager.FastText;
                    else
                        _fastLateRenderers[pos].sprite = _noteSkinManager.LateText;
                }
            }
            else
            {
                _fastLateRenderers[pos].sprite = null;
            }
        }

        public void PlayFireworkEffect(int pos)
        {
            float2 worldPos;
            if (pos is < 0 or > EFFECT_COUNT) return;
            else if (pos < BUTTON_COUNT) worldPos = MajPos.GetBtnPos(pos);
            else worldPos = MajPos.GetAreaPos((SensorType)(pos - 8));
            _fireworkEffect.transform.position = new float3(worldPos, 0);
            _fireworkAnimator.SetTrigger(FireHash);
        }

        public void ResetState()
        {
            for (var i = 0; i < judgeEffectRequests.Length; i++)
                judgeEffectRequests[i] = default;
        }
    }

    public struct EffectData
    {
        public EffectType Effect;
        public JudgeGrade JudgeGrade;
        public bool IsBreak;
        public bool IsMine;
        public bool HasHolding;
        public Color HoldingColor;
    }

    [Flags]
    public enum EffectType
    {
        None = 0,
        Tap = 1 << 0,
        Touch = 1 << 1,
        Firework = 1 << 2
    }
}