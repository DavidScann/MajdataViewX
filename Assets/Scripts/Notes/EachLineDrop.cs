#nullable enable

#region

using UnityEngine;

using static MajCtx;

#endregion

public class EachLineDrop : MonoBehaviour
{
    //init args
    public float time;
    public int startPosition;
    public float speed;
    public bool UsingSV;

    public int curvLength;

    //own
    private SpriteRenderer spriteRenderer;

    // Start is called before the first frame update
    private void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = _skinManager.EachLine[curvLength - 1];
        spriteRenderer.forceRenderingOff = true;
    }

    // Update is called once per frame
    private void FixedUpdate()
    {
        var timing = _timeProvider.NoteTime - time;
        if (timing > 0) Destroy(gameObject);
        var distance = timing * speed + 4.8f;
        var destScale = distance * 0.4f + 0.51f;

        var fakeTiming = _timeProvider.FakeNoteTime - _timeProvider.GetPositionAtTime(time);
        var fakeDistance = fakeTiming * speed + 4.8f;
        var fakeDestScale = fakeDistance * 0.4f + 0.51f;

        if (!UsingSV)
        {
            //fakeTiming = timing;
            fakeDistance = distance;
            fakeDestScale = destScale;
        }


        transform.rotation = Quaternion.Euler(0, 0, -45f * (startPosition - 1));

        if (fakeDestScale > 0.3f)
        {
            spriteRenderer.forceRenderingOff = false;
        }
        if (fakeDistance < 1.225f)
        {
            transform.localScale = new Vector3(1.225f / 4.8f, 1.225f / 4.8f, 1f);
            return;
        }

        var lineScale = Mathf.Abs(fakeDistance / 4.8f);
        transform.localScale = new Vector3(lineScale, lineScale, 1f);
    }
}