using UnityEngine;
using static MajCtx;

public partial class NoteManager
{
    public void SyncTap()
    {
        for (int i = 0; i < taps.Length; i++)
        {
            var tap = taps[i];

            if (tap.show)
            {
                if (tap.ViewIndex < 0)
                {
                    tap.ViewIndex = _pool.Get(Notes_LAYER, tap.sort);
                    tap.tapLine.ViewIndex = _pool.Get(HanteiLine_LAYER, 0);
                    if (tap.isEx) tap.tapEx.ViewIndex = _pool.Get(Notes_LAYER, tap.sort);
                    var view = _pool.GetView(tap.ViewIndex);
                    var lineView = _pool.GetView(tap.tapLine.ViewIndex);
                    var exView = _pool.GetView(tap.tapEx.ViewIndex);
                    //no matter if exView is null, check later
                    LoadTapSkin(tap, view, lineView, exView);
                }

                UpdateTapView(tap);
            }
            else
            {
                _pool.Release(tap.ViewIndex);
                _pool.Release(tap.tapLine.ViewIndex);
                _pool.Release(tap.tapEx.ViewIndex);
                tap.ViewIndex = -1;
                tap.tapLine.ViewIndex = -1;
                tap.tapEx.ViewIndex = -1;
            }

            taps[i] = tap;
        }
    }

    private void UpdateTapView(TapData tap)
    {
        var view = _pool.GetView(tap.ViewIndex);
        var lineView = _pool.GetView(tap.tapLine.ViewIndex);
        var exView = _pool.GetView(tap.tapEx.ViewIndex);

        view.Transform.SetPositionAndRotation(tap.pos, tap.ang);
        view.Transform.localScale = tap.scale;
        if (tap.isBreak)
        {
            view.SetProperty(NoteView.BrightnessHash, tap.brightness);
        }

        lineView.Transform.SetPositionAndRotation(tap.tapLine.pos, tap.tapLine.ang);
        lineView.Transform.localScale = tap.tapLine.scale;

        if (tap.isEx)
        {
            exView.Transform.SetPositionAndRotation(tap.tapEx.pos, tap.tapEx.ang);
            exView.Transform.localScale = tap.tapEx.scale;
        }
    }

    private void LoadTapSkin(TapData tap, NoteView view, NoteView lineView, NoteView exView)
    {
        lineView.SpriteRenderer.sprite = _skinManager.Line;
        view.SpriteRenderer.sprite = _skinManager.Tap;
        if (tap.isEx)
        {
            exView.SpriteRenderer.sprite = _skinManager.Tap_Ex;
            exView.SpriteRenderer.color = _skinManager.Ex;
        }
        if (tap.isEach)
        {
            view.SpriteRenderer.sprite = _skinManager.Tap_Each;
            lineView.SpriteRenderer.sprite = _skinManager.Line_Each;
            if (tap.isEx) exView.SpriteRenderer.color = _skinManager.Ex_Each;
        }
        if (tap.isBreak)
        {
            view.SpriteRenderer.sprite = _skinManager.Tap_Break;
            view.SpriteRenderer.material = _skinManager.BreakMaterial;
            lineView.SpriteRenderer.sprite = _skinManager.Line_Break;
            if (tap.isEx) exView.SpriteRenderer.color = _skinManager.Ex_Break;
        }
        if (tap.isMine)
        {
            if (tap.isBreak)
                view.SpriteRenderer.sprite = _skinManager.Tap_Break_Mine;
            else
                view.SpriteRenderer.sprite = _skinManager.Tap_Mine;
            lineView.SpriteRenderer.sprite = _skinManager.Line_Mine;
        }
    }
}