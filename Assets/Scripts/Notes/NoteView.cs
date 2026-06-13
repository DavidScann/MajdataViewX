using UnityEngine;

public class NoteView
{
    public static readonly int BrightnessHash = Shader.PropertyToID("_Brightness");
    public static readonly int ColorHash = Shader.PropertyToID("_Color");

    public GameObject GameObject { get; set; }
    public Transform Transform { get; set; }
    public SpriteRenderer SpriteRenderer { get; set; }
    public Material Material { get; set; }

    public void SetProperty(int propertyHash, float value) =>
        Material.SetFloat(propertyHash, value);
    public void SetProperty(int propertyHash, Color value) =>
        Material.SetColor(propertyHash, value);
    public void SetProperty(int propertyHash, Vector4 value) =>
        Material.SetVector(propertyHash, value);


    /// <summary>
    /// 重置状态（归还池子时调用）
    /// </summary>
    public void Reset()
    {
        //SpriteRenderer.sprite = null;
        SpriteRenderer.enabled = false;
    }

    /// <summary>
    /// 初始化状态（从池子取出时调用）
    /// </summary>
    public void Init(Vector3 position, Quaternion rotation, int sortOrder)
    {
        SpriteRenderer.enabled = true;
        Transform.SetPositionAndRotation(position, rotation);
        SpriteRenderer.sortingOrder = sortOrder;
    }
}