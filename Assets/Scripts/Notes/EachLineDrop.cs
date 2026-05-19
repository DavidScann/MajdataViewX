#nullable enable

#region

using UnityEngine;

#endregion

public class EachLineDrop : MonoBehaviour
{
    //managers
    private TimeProvider timeProvider;

    //init args
    public float time;
    public int startPosition;
    public float speed;
    public bool UsingSV;

    public int curvLength;

    [SerializeField]
    Sprite[] curvSprites;

    //own
    private SpriteRenderer spriteRenderer;

    // Start is called before the first frame update
    private void Start()
    {
        timeProvider = Majdata<TimeProvider>.Instance!;

        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = curvSprites[curvLength - 1];
        spriteRenderer.forceRenderingOff = true;
    }

    // Update is called once per frame
    private void FixedUpdate()
    {
        var timing = timeProvider.NoteTime - time;
        if (timing > 0) Destroy(gameObject);
        var distance = timing * speed + 4.8f;
        var destScale = distance * 0.4f + 0.51f;
        var lineScale = Mathf.Abs(distance / 4.8f);

        var fakeTiming = timeProvider.FakeNoteTime - timeProvider.GetPositionAtTime(time);
        var fakeDistance = fakeTiming * speed + 4.8f;
        var fakeDestScale = fakeDistance * 0.4f + 0.51f;
        var fakeLineScale = Mathf.Abs(fakeDistance / 4.8f);

        if (!UsingSV)
        {
            //fakeTiming = timing;
            fakeDistance = distance;
            fakeDestScale = destScale;
            fakeLineScale = lineScale;
        }


        if (fakeDistance < 1.225f)
        {
            if (fakeDestScale > 0.3f) spriteRenderer.forceRenderingOff = false;
        }

        transform.localScale = new Vector3(fakeLineScale, fakeLineScale, 1f);
        transform.rotation = Quaternion.Euler(0, 0, -45f * (startPosition - 1));
    }
}