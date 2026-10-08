using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Laedt einen Sprite-Streifen aus Resources in der richtigen Reihenfolge.
/// Resources.LoadAll garantiert keine Reihenfolge - die Bilder heissen aber
/// &lt;name&gt;_0, &lt;name&gt;_1, ... (Tools/unity_meta.py), danach wird sortiert.
/// </summary>
public static class SpriteStrip
{
    public static Sprite[] Load(string resourcePath)
    {
        Sprite[] all = Resources.LoadAll<Sprite>(resourcePath);
        if (all == null || all.Length == 0) return new Sprite[0];

        var list = new List<Sprite>(all);
        list.Sort((a, b) => Index(a.name).CompareTo(Index(b.name)));
        return list.ToArray();
    }

    private static int Index(string name)
    {
        int underscore = name.LastIndexOf('_');
        int value;
        if (underscore >= 0 && int.TryParse(name.Substring(underscore + 1), out value)) return value;
        return 0;
    }

    /// <summary>Notfall-Bild, falls ein Streifen fehlt: runder Klecks, 32 PPU.</summary>
    public static Sprite Blob(int size, Color32 color)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };

        float c = (size - 1) * 0.5f;
        float r = size * 0.5f;
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32[] px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = (x - c) * (x - c) + (y - c) * (y - c) <= r * r ? color : clear;

        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32);
    }
}
