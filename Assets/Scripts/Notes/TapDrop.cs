using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using UnityEngine;

public class TapDrop : NoteDrop
{
    // Start is called before the first frame update
    // public float time;
    public int startPosition = 1;
    public float speed = 1;

    public bool isEach;
    public bool isBreak;
    public bool isEX;
    public bool isFakeStarRotate;

    public Sprite normalSpr;
    public Sprite eachSpr;
    public Sprite breakSpr;
    public Sprite exSpr;

    public Sprite eachLine;
    public Sprite breakLine;

    public RuntimeAnimatorController BreakShine;

    public GameObject tapLine;

    public Color exEffectTap;
    public Color exEffectEach;
    public Color exEffectBreak;

    public Material breakMaterial;

    private bool breakAnimStart;
    private SpriteRenderer exSpriteRender;
    public SpriteRenderer lineSpriteRender;

    private ObjectCounter ObjectCounter;

    private SpriteRenderer spriteRenderer;

    private AudioTimeProvider timeProvider;

    private void Awake()
    {
        var notes = GameObject.Find("Notes").transform;
        tapLine = Instantiate(tapLine, notes);
        tapLine.SetActive(false);
        lineSpriteRender = tapLine.GetComponent<SpriteRenderer>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        exSpriteRender = transform.GetChild(0).GetComponent<SpriteRenderer>();
        timeProvider = GameObject.Find("AudioTimeProvider").GetComponent<AudioTimeProvider>();
        ObjectCounter = GameObject.Find("ObjectCounter").GetComponent<ObjectCounter>();

        spriteRenderer.sortingOrder += noteSortOrder;
        exSpriteRender.sortingOrder += noteSortOrder;

        spriteRenderer.sprite = normalSpr;
        exSpriteRender.sprite = exSpr;

        if (isEX) exSpriteRender.color = exEffectTap;
        if (isEach)
        {
            spriteRenderer.sprite = eachSpr;
            lineSpriteRender.sprite = eachLine;
            if (isEX) exSpriteRender.color = exEffectEach;
        }
        if (isBreak)
        {
            spriteRenderer.sprite = breakSpr;
            lineSpriteRender.sprite = breakLine;
            if (isEX) exSpriteRender.color = exEffectBreak;
            spriteRenderer.material = breakMaterial;
            //var anim = gameObject.AddComponent<Animator>(); // break tap闪烁
            //anim.runtimeAnimatorController = BreakShine;
            //anim.enabled = false;
            //animator = anim;
        }

        spriteRenderer.forceRenderingOff = true;
        exSpriteRender.forceRenderingOff = true;
    }

    // Update is called once per frame
    private void Update()
    {
        var timing = timeProvider.ScrollDist - timeProvider.GetPositionAtTime(time);
        var realtime = timeProvider.AudioTime - time;
        var distance = timing * speed + 4.8f;
        var realDistance = realtime * speed + 4.8f;
        if (canSVAffect == 0)
        {
            timing = realtime;
            distance = realDistance;
        }
        else if (canSVAffect != 1)
        {
            var svProvider = timeProvider.SubSVList[canSVAffect];
            timing = svProvider.ScrollDist - svProvider.GetPositionAtTime(time);
            distance = timing * speed + 4.8f;
        }
        var destScale = distance * 0.4f + 0.51f;
        if (destScale < 0f)
        {
            destScale = 0f;
            tapLine.SetActive(false);
            return;
        }

        spriteRenderer.forceRenderingOff = false;
        if (isEX) exSpriteRender.forceRenderingOff = false;

        if (isBreak)
        {
            var extra = Math.Max(Mathf.Sin(timeProvider.GetFrame() * 0.17f) * 0.5f, 0);
            spriteRenderer.material.SetFloat("_Brightness", 0.95f + extra);
        }

        if (realtime > 0)
        {
            GameObject.Find("NoteEffects").GetComponent<NoteEffectManager>().PlayEffect(startPosition, isBreak, isUnplayable);
            if (isBreak) ObjectCounter.breakCount++;
            else ObjectCounter.tapCount++;
            if (isUnplayable) ObjectCounter.missSum++;
            else ObjectCounter.cpSum++;

            Destroy(tapLine);
            Destroy(gameObject);
        }

        if (isFakeStarRotate)
            transform.Rotate(0f, 0f, 400f * Time.deltaTime);
        else
            transform.rotation = Quaternion.Euler(0, 0, -22.5f + -45f * (startPosition - 1));
        tapLine.transform.rotation = Quaternion.Euler(0, 0, -22.5f + -45f * (startPosition - 1));


        if (destScale > 0.3f) tapLine.SetActive(true);

        if (distance < 1.225f)
        {
            transform.localScale = new Vector3(destScale, destScale);

            distance = 1.225f;
            var pos = getPositionFromDistance(distance);
            transform.position = pos;            
        }
        else
        {
            var pos = getPositionFromDistance(distance);
            transform.position = pos;
            transform.localScale = new Vector3(1f, 1f);
        }

        var lineScale = Mathf.Abs(distance / 4.8f);
        tapLine.transform.localScale = new Vector3(lineScale, lineScale, 1f);
        //lineSpriteRender.color = new Color(1f, 1f, 1f, lineScale);
    }

    private Vector3 getPositionFromDistance(float distance)
    {
        return new Vector3(
            distance * Mathf.Cos((startPosition * -2f + 5f) * 0.125f * Mathf.PI),
            distance * Mathf.Sin((startPosition * -2f + 5f) * 0.125f * Mathf.PI));
    }
}