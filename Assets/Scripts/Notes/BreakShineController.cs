using System;
using UnityEngine;
#nullable enable
public class BreakShineController : MonoBehaviour
{
    TimeProvider timeProvider;
    
    public SlideBase? parent;

    private SpriteRenderer spriteRenderer;
    private MaterialPropertyBlock _mpb;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
    }
    
    private void Start()
    {
        timeProvider = Majdata<TimeProvider>.Instance!;
        
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (parent == null || !parent.CanShine()) return;
        
        var extra = Math.Max(Mathf.Sin(timeProvider.GetFrame() * 0.17f) * 0.5f, 0);
            
        spriteRenderer.GetPropertyBlock(_mpb);
        _mpb.SetFloat("_Brightness", 0.95f + extra);
        spriteRenderer.SetPropertyBlock(_mpb);
    }
}
