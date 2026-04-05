using UnityEngine;
#nullable enable
public class TapDrop : TapBase
{
    private void Start()
    {
        PreLoad();

        LoadSkin();
        spriteRenderer.forceRenderingOff = true;
        exSpriteRender.forceRenderingOff = true;
        
        sensor = GameObject.Find("Sensors")
                                   .transform.GetChild(startPosition - 1)
                                   .GetComponent<Sensor>();
        inputManager.BindArea(Check, sensor);
        State = NoteStatus.Initialized;
    }

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