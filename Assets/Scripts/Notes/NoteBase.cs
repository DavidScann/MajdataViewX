#nullable enable

#region

using System;
using UnityEngine;

#endregion

public class NoteBase : MonoBehaviour
{
    public float time;
    public int startPosition;
    public SensorType sensor;
    public float speed = 7;
    public int noteSortOrder;

    public bool isEach;
    public bool isEx;
    public bool isBreak;
    public bool isMine;
    public bool usingSV;


    protected NoteStatus State { get; set; } = NoteStatus.Start;

    protected Guid guid = Guid.NewGuid();
    protected JudgeType judgeResult;
    protected bool isJudged = false;

    protected Vector3 getPositionFromDistance(float distance) => getPositionFromDistance(distance, startPosition);
    protected Vector3 getPositionFromDistance(float distance, int position)
    {
        return new Vector3(
            distance * Mathf.Cos((position * -2f + 5f) * 0.125f * Mathf.PI),
            distance * Mathf.Sin((position * -2f + 5f) * 0.125f * Mathf.PI));
    }
}

public class NoteLongBase : NoteBase
{
    public float LastFor = 1f;

    protected float playerIdleTime = 0;
    protected float judgeDiff = -1;

    [SerializeField]
    public GameObject holdEffect;
    protected Material material;

    protected float GetRemainingTime() => MathF.Max(LastFor - (MajCtx._timeProvider.NoteTime - time), 0);

    protected virtual void PlayHoldEffect()
    {
        switch (judgeResult)
        {
            case JudgeType.LatePerfect2:
            case JudgeType.FastPerfect2:
            case JudgeType.LatePerfect1:
            case JudgeType.FastPerfect1:
            case JudgeType.Perfect:
                material.SetColor("_Color", new Color(1f, 0.93f, 0.61f)); // Yellow
                break;
            case JudgeType.LateGreat:
            case JudgeType.LateGreat1:
            case JudgeType.LateGreat2:
            case JudgeType.FastGreat2:
            case JudgeType.FastGreat1:
            case JudgeType.FastGreat:
                material.SetColor("_Color", new Color(1f, 0.70f, 0.94f)); // Pink
                break;
            case JudgeType.LateGood:
            case JudgeType.FastGood:
                material.SetColor("_Color", new Color(0.56f, 1f, 0.59f)); // Green
                break;
            case JudgeType.Miss:
                material.SetColor("_Color", new Color(1f, 1f, 1f)); // White
                break;
            default:
                break;
        }
        holdEffect.SetActive(true);
    }
    protected virtual void StopHoldEffect()
    {
        holdEffect.SetActive(false);
    }
}