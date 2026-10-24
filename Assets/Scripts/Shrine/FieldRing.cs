using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ring am Boden um einen Schrein (<see cref="SacrificeShrine"/>,
/// <see cref="ReviveAltar"/>): Stuecke, die sich im Uhrzeigersinn fuellen,
/// innen ein hauchduenner Schimmer. Pixel fuer Pixel per Code, PPU 32.
///
/// Welche Pixel zu welchem Stueck gehoeren, wird einmal beim Bauen
/// ausgerechnet - beim Fortschritt werden nur die Ringpixel neu gesetzt. So
/// bleibt auch der grosse Kreis am Kreuz (zweieinhalb Mixer breit) billig.
/// </summary>
public sealed class FieldRing
{
    public const int Segments = 36;
    private const float PPU = 32f;

    public Color32 Empty = new Color32(0x3a, 0x20, 0x30, 150);
    public Color32 Full = new Color32(0xd4, 0x56, 0x6c, 235);    // GameHudSkin.Jam
    public Color32 Hot = new Color32(0xf0, 0x8a, 0x9a, 255);     // GameHudSkin.JamLight
    public Color32 Inner = new Color32(0x7d, 0x23, 0x38, 34);    // GameHudSkin.JamDeep, hauchduenn

    public SpriteRenderer Renderer { get; }

    private readonly Texture2D tex;
    private readonly Color32[] px;
    private readonly List<int> ringIndex = new List<int>();
    private readonly List<sbyte> ringSegment = new List<sbyte>();   // -1 = Fuge zwischen zwei Stuecken
    private readonly List<int> innerIndex = new List<int>();
    private int paintedStep = -1;
    private bool paintedHot;

    /// <param name="width">Dicke des Rings in Pixeln.</param>
    public FieldRing(SpriteRenderer renderer, float radius, int width = 4)
    {
        Renderer = renderer;
        int r = Mathf.CeilToInt(radius * PPU);
        int size = r * 2 + 2;
        tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        px = new Color32[size * size];

        float c = (size - 1) * 0.5f;
        float outer = c - 1f, inner = outer - width;
        float segAngle = 360f / Segments;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - c, dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                int i = y * size + x;
                if (d <= outer && d >= inner)
                {
                    // 0 oben, im Uhrzeigersinn
                    float ang = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;
                    if (ang < 0f) ang += 360f;
                    // Fugen gleich breit in Pixeln, egal wie gross der Ring ist.
                    bool gap = Mathf.Repeat(ang, segAngle) * Mathf.Deg2Rad * d < 1.2f;
                    ringIndex.Add(i);
                    ringSegment.Add(gap ? (sbyte)-1 : (sbyte)Mathf.Min(Segments - 1, Mathf.FloorToInt(ang / segAngle)));
                }
                else if (d < inner)
                {
                    innerIndex.Add(i);
                }
            }
        }

        renderer.sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PPU);
        Paint(0f, true);
    }

    /// <summary>Fortschritt 0..1. Malt nur neu, wenn sich ein Stueck aendert.</summary>
    public void Paint(float progress, bool force = false)
    {
        int step = Mathf.Clamp(Mathf.FloorToInt(progress * Segments), 0, Segments);
        bool hot = step >= Segments - 3;
        if (!force && step == paintedStep && hot == paintedHot) return;
        paintedStep = step;
        paintedHot = hot;

        Color32 clear = new Color32(0, 0, 0, 0);
        for (int k = 0; k < ringIndex.Count; k++)
        {
            int seg = ringSegment[k];
            px[ringIndex[k]] = seg < 0 ? clear : seg < step ? (hot ? Hot : Full) : Empty;
        }
        for (int k = 0; k < innerIndex.Count; k++) px[innerIndex[k]] = Inner;

        tex.SetPixels32(px);
        tex.Apply(false);
    }

    public void Destroy()
    {
        if (tex != null) Object.Destroy(tex);
    }
}
