using UnityEngine;
#nullable enable
public class EachLineDrop : MonoBehaviour
{
    //managers
    private TimeProvider timeProvider;
    
    //init args
    public float time;
    public int startPosition;
    public float speed;
    
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
    private void Update()
    {
        var timing = timeProvider.AudioTime - time;
        var distance = timing * speed + 4.8f;
        var destScale = distance * 0.4f + 0.51f;
        if (timing > 0) Destroy(gameObject);
        if (distance < 1.225f)
        {
            distance = 1.225f;
            if (destScale > 0.3f) spriteRenderer.forceRenderingOff = false;
        }

        var lineScale = Mathf.Abs(distance / 4.8f);
        transform.localScale = new Vector3(lineScale, lineScale, 1f);
        transform.rotation = Quaternion.Euler(0, 0, -45f * (startPosition - 1));
    }
}