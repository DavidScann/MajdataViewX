#nullable enable

#region

using UnityEngine;

#endregion

/// <summary>
/// 普通 Tap note。继承 <see cref="TapBase"/>。
/// <para>生命周期：<c>Start (一次性) → Init(info) → Update(running) + Check(事件) → FixedUpdate(Render) → End(归还池)</c></para>
/// </summary>
public class TapDrop : TapBase
{
    /// <summary>
    /// Awake：池化场景下 Init 在 SetActive(true) 之前调用，因此依赖注入必须在 Awake 完成
    /// （Awake 在 Instantiate 时同步触发，Start 仅在 SetActive(true) 后才运行）。
    /// </summary>
    private void Awake()
    {
        PreLoad();
        // 池化时即使尚未 Init，先把渲染关掉，等 Init 注入 startPosition 等数据后再启用
        spriteRenderer.forceRenderingOff = true;
        exSpriteRender.forceRenderingOff = true;
    }

    /// <summary>
    /// 池化复用入口：每次从池中取出后调用。
    /// </summary>
    public void Init(TapPoolingInfo info)
    {
        ApplyTapInfo(info);
        GetTapLine();
        ResetSortingOrder(info.NoteSortOrder);
        LoadSkin();
        ResetTapState();
        sensor = (SensorType)startPosition - 1;
        inputManager.BindArea(Check, sensor);
        inputBound = true;
        gameObject.SetActive(false); // 等 streaming 激活
    }

    /// <summary>把 PoolingInfo 内的字段写入实例。</summary>
    private void ApplyTapInfo(TapPoolingInfo info)
    {
        time = info.Time;
        startPosition = info.StartPosition;
        speed = info.Speed;
        isEach = info.IsEach;
        isEx = info.IsEx;
        isBreak = info.IsBreak;
        isMine = info.IsMine;
        usingSV = info.UsingSV;
    }

    /// <summary>根据 isEach / isBreak / isMine / isEx 切换皮肤资源。</summary>
    private void LoadSkin()
    {
        lineSpriteRenderer.sprite = skinManager.Line;
        spriteRenderer.sprite = skinManager.Tap;
        exSpriteRender.sprite = skinManager.Tap_Ex;
        if (isEx)
        {
            exSpriteRender.color = skinManager.Ex;
        }
        if (isEach)
        {
            spriteRenderer.sprite = skinManager.Tap_Each;
            if (isEx) exSpriteRender.color = skinManager.Ex_Each;
            lineSpriteRenderer.sprite = skinManager.Line_Each;
        }
        if (isBreak)
        {
            spriteRenderer.sprite = skinManager.Tap_Break;
            lineSpriteRenderer.sprite = skinManager.Line_Break;
            if (isEx) exSpriteRender.color = skinManager.Ex_Break;
            spriteRenderer.material = skinManager.BreakMaterial;
        }
        if (isMine)
        {
            spriteRenderer.sprite = skinManager.Tap_Mine;
            lineSpriteRenderer.sprite = skinManager.Line_Mine;
        }
    }
}
