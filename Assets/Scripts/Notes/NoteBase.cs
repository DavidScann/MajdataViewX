#nullable enable

#region

using System;
using UnityEngine;

#endregion

/// <summary>
/// 所有 Note 的共同基类。
/// <para>池化生命周期：<c>Start (一次性注入依赖) → Init(info) (每次刷新数据/重置状态) → Update/FixedUpdate (运行) → End (归还到池)</c></para>
/// <para>代码组织规范：<c>Update</c> = running(autoplay) + check(timing 检测)，<c>FixedUpdate</c> = Render（位置/缩放/材质）。</para>
/// </summary>
public class NoteBase : MonoBehaviour
{
    protected GameObject notes;
    protected TimeProvider timeProvider;
    protected ObjectCounter objectCounter;
    protected NoteManager noteManager;
    protected InputManager inputManager;
    protected SkinManager skinManager;
    protected AudioManager audioManager;

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

    /// <summary>池化时回归用的 prefab 引用（由 DataLoader 在 Get 后立即设置）。</summary>
    [System.NonSerialized]
    public GameObject prefabRef;

    /// <summary>
    /// 池化生命周期的统一结束钩子。子类（如 TapDrop / HoldDrop）override 后负责
    /// 上报判定 / 解绑事件 / 把自身与子对象归还到 <see cref="NotePool"/>。
    /// 默认实现走旧的 Destroy 路径（兼容 SlideDrop / WifiDrop 暂未池化的情况）。
    /// </summary>
    public virtual void End()
    {
        if (gameObject != null)
            Destroy(gameObject);
    }

    protected NoteStatus State { get; set; } = NoteStatus.Start;

    protected Guid guid = Guid.NewGuid();
    protected JudgeType judgeResult;
    protected bool isJudged = false;
    private JudgeType _judgeResult;

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

    protected float GetRemainingTime() => MathF.Max(LastFor - (timeProvider.NoteTime - time), 0);

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
