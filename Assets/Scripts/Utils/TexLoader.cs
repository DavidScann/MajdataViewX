#nullable enable

#region

using System.IO;
using UnityEngine;

#endregion

public static class TexLoader
{
    public static Texture2D LoadTexture(string path)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!File.Exists(path))
            return tex;
        var bytes = File.ReadAllBytes(path);
        tex.LoadImage(bytes);
        return tex;
    }

    public static Sprite LoadSprite(string path)
    {
        var tex = LoadTexture(path);
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
    }
}