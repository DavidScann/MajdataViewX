#nullable enable

#region

using UnityEngine;
using static MajCtx;

#endregion

public class TapDrop : TapBase
{
    private void Start()
    {
        PreLoad();

        LoadSkin();
        spriteRenderer.forceRenderingOff = true;
        exSpriteRender.forceRenderingOff = true;

        sensor = (SensorType)startPosition - 1;
        _inputManager.BindArea(Check, sensor);
        State = NoteStatus.Initialized;
    }

    private void LoadSkin()
    {
        lineSpriteRenderer.sprite = _skinManager.Line;
        spriteRenderer.sprite = _skinManager.Tap;
        exSpriteRender.sprite = _skinManager.Tap_Ex;
        if (isEx)
        {
            exSpriteRender.color = _skinManager.Ex;
        }
        if (isEach)
        {
            spriteRenderer.sprite = _skinManager.Tap_Each;
            if (isEx) exSpriteRender.color = _skinManager.Ex_Each;
            lineSpriteRenderer.sprite = _skinManager.Line_Each;
        }
        if (isBreak)
        {
            spriteRenderer.sprite = _skinManager.Tap_Break;
            lineSpriteRenderer.sprite = _skinManager.Line_Break;
            if (isEx) exSpriteRender.color = _skinManager.Ex_Break;
            spriteRenderer.material = _skinManager.BreakMaterial;
        }
        if (isMine)
        {
            if (isBreak)
                spriteRenderer.sprite = _skinManager.Tap_Break_Mine;
            else
                spriteRenderer.sprite = _skinManager.Tap_Mine;
            lineSpriteRenderer.sprite = _skinManager.Line_Mine;
        }
    }
}