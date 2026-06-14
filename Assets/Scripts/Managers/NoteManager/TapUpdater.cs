using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using static MajCtx;
using static NoteSkinManager;

public partial class NoteManager
{
    public void SyncTap()
    {
        _noteRenderData.Clear();
        for (int i = 0; i < taps.Length; i++)
        {
            var tap = taps[i];

            if (tap.show) //sort by render order
            {
                LoadTapSkin(tap,
                    out var tapSprite,
                    out var lineSprite,
                    out var exSprite,
                    out var exColor);
                _noteRenderData.Add(new()     //tapLine
                {
                    pos = tap.tapLine.pos,
                    angRad = Mathf.Deg2Rad * tap.tapLine.ang,
                    scale = tap.tapLine.scale,
                    spriteId = lineSprite,
                    color = new float4(1, 1, 1, 1),
                    brightness = 1f
                });
                if (tap.isEx) _noteRenderData.Add(new()     //tapEx
                {
                    pos = tap.tapEx.pos,
                    angRad = 0,
                    scale = tap.tapEx.scale,
                    spriteId = exSprite,
                    color = exColor,
                    brightness = 1f
                });
                _noteRenderData.Add(new()     //tap
                {
                    pos = tap.pos,
                    angRad = Mathf.Deg2Rad * tap.ang,
                    scale = tap.scale,
                    spriteId = tapSprite,
                    color = new float4(1, 1, 1, 1),
                    brightness = tap.brightness
                });
            }
        }
    }

    private void LoadTapSkin(TapData tap,
        out uint tapSpriteID, out uint lineSpriteID, out uint exSpriteID, out float4 exColor)
    {
        tapSpriteID = TAP;
        lineSpriteID = LINE;
        exSpriteID = TAP_EX;
        exColor = Ex;
        if (tap.isEach)
        {
            tapSpriteID = TAP_EACH;
            lineSpriteID = LINE_EACH;
            if (tap.isEx) exColor = Ex_Each;
        }
        if (tap.isBreak)
        {
            tapSpriteID = TAP_BREAK;
            // view.SpriteRenderer.material = _skinManager.BreakMaterial;
            lineSpriteID = LINE_BREAK;
            if (tap.isEx) exColor = Ex_Break;
        }
        if (tap.isMine)
        {
            if (tap.isBreak)
                tapSpriteID = TAP_BREAK_MINE;
            else
                tapSpriteID = TAP_MINE;
            lineSpriteID = LINE_MINE;
        }
    }
}