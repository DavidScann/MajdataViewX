#nullable enable

#region

using UnityEngine;

#endregion

public class StarDrop : TapBase
{
    public float rotateSpeed = 1f;

    public bool isDouble;
    public bool isFakeStar = false;
    public bool isFakeStarRotate = false;


    private void Start()
    {
        PreLoad();

        LoadSkin();

        spriteRenderer.forceRenderingOff = true;
        exSpriteRender.forceRenderingOff = true;

        sensor = (SensorType)startPosition - 1;
        inputManager.BindArea(Check, sensor);

        State = NoteStatus.Initialized;
    }

    private void LoadSkin()
    {
        if (isDouble)
        {
            exSpriteRender.sprite = skinManager.Star_Ex_Double;
            spriteRenderer.sprite = skinManager.Star_Double;
            lineSpriteRenderer.sprite = skinManager.Line_Star;
            if (isEx)
            {
                exSpriteRender.color = skinManager.Ex_Star;
            }
            if (isEach)
            {
                spriteRenderer.sprite = skinManager.Star_Each_Double;
                lineSpriteRenderer.sprite = skinManager.Line_Each;
                if (isEx) exSpriteRender.color = skinManager.Ex_Each;
            }
            if (isBreak)
            {
                spriteRenderer.sprite = skinManager.Star_Break_Double;
                lineSpriteRenderer.sprite = skinManager.Line_Break;
                if (isEx) exSpriteRender.color = skinManager.Ex_Break;
                spriteRenderer.material = skinManager.BreakMaterial;
            }
            if (isMine)
            {
                spriteRenderer.sprite = skinManager.Star_Mine_Double;
                lineSpriteRenderer.sprite = skinManager.Line_Mine;
            }
        }
        else
        {
            exSpriteRender.sprite = skinManager.Star_Ex;
            spriteRenderer.sprite = skinManager.Star;
            lineSpriteRenderer.sprite = skinManager.Line_Star;
            if (isEx)
            {
                exSpriteRender.color = skinManager.Ex_Star;
            }
            if (isEach)
            {
                spriteRenderer.sprite = skinManager.Star_Each;
                lineSpriteRenderer.sprite = skinManager.Line_Each;
                if (isEx) exSpriteRender.color = skinManager.Ex_Each;
            }
            if (isBreak)
            {
                spriteRenderer.sprite = skinManager.Star_Break;
                lineSpriteRenderer.sprite = skinManager.Line_Break;
                if (isEx) exSpriteRender.color = skinManager.Ex_Break;
                spriteRenderer.material = skinManager.BreakMaterial;
            }
            if (isMine)
            {
                if (isBreak)
                    if (isDouble)
                        spriteRenderer.sprite = skinManager.Star_Break_Double_Mine;
                    else
                        spriteRenderer.sprite = skinManager.Star_Break_Mine;
                else
                    if (isDouble)
                        spriteRenderer.sprite = skinManager.Star_Mine_Double;
                    else
                        spriteRenderer.sprite = skinManager.Star_Mine;
                lineSpriteRenderer.sprite = skinManager.Line_Mine;
            }
        }
    }


    protected override void Update()
    {
        var songSpeed = timeProvider.CurrentSpeed;

        var timing = timeProvider.NoteTime - time;
        var distance = timing * speed + 4.8f;
        var destScale = distance * 0.4f + 0.51f;

        var fakeTiming = timeProvider.FakeNoteTime - timeProvider.GetPositionAtTime(time);
        var fakeDistance = fakeTiming * speed + 4.8f;
        var fakeDestScale = fakeDistance * 0.4f + 0.51f;

        if (!usingSV)
        {
            //fakeTiming = timing;
            fakeDistance = distance;
            fakeDestScale = destScale;
        }

        switch (State)
        {
            case NoteStatus.Initialized:
                if (fakeDestScale >= 0f)
                {
                    tapLine.transform.rotation = Quaternion.Euler(0, 0, -22.5f + -45f * (startPosition - 1));
                    State = NoteStatus.Pending;
                    goto case NoteStatus.Pending;
                }

                transform.localScale = new Vector3(0, 0);
                return;
            case NoteStatus.Pending:
                {
                    if (fakeDestScale > 0.3f)
                        tapLine.SetActive(true);
                    if (fakeDistance < 1.225f)
                    {
                        transform.localScale = new Vector3(fakeDestScale, fakeDestScale);
                        transform.position = getPositionFromDistance(1.225f);
                        var lineScale = Mathf.Abs(1.225f / 4.8f);
                        tapLine.transform.localScale = new Vector3(lineScale, lineScale, 1f);
                    }
                    else
                    {
                        State = NoteStatus.Running;
                        goto case NoteStatus.Running;
                    }
                }
                break;
            case NoteStatus.Running:
                {
                    transform.position = getPositionFromDistance(fakeDistance);
                    transform.localScale = new Vector3(1f, 1f);
                    var lineScale = Mathf.Abs(fakeDistance / 4.8f);
                    tapLine.transform.localScale = new Vector3(lineScale, lineScale, 1f);
                }
                break;
        }

        spriteRenderer.forceRenderingOff = false;
        if (isEx) exSpriteRender.forceRenderingOff = false;

        if (timeProvider.IsStart && !isFakeStar && rotateSpeed != 0)
            transform.Rotate(0f, 0f, -180f * Time.deltaTime * songSpeed / rotateSpeed);
        else if (isFakeStarRotate)
            transform.Rotate(0f, 0f, 400f * Time.deltaTime);
    }
}