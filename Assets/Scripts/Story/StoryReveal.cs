using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Zwei kurze Einblendungen der Story, beide auf der 480x270-Seite:
///
///   Shard   Der Glutsplitter steigt dort auf, wo der Boss gefallen ist,
///           fliegt in die Bildmitte und waechst, Strahlen drehen sich
///           dahinter, das Titelband GLUTSPLITTER faellt ein, darunter
///           "2 von 4" und vier Plaetze, von denen sich einer fuellt.
///   Banner  Titelband + Untertitel, z.B. DER VULKAN IST OFFEN.
///
/// Laeuft auf unskalierter Zeit und fasst Time.timeScale nicht an. Klick
/// verkuerzt das Halten.
/// </summary>
public class StoryReveal : MonoBehaviour
{
    private static readonly RectInt Content = new RectInt(0, 0, OptionsKit.RefW, OptionsKit.RefH);

    private RectTransform page;
    private CanvasScaler scaler;
    private TMP_FontAsset textFont, pixelFont;
    private Action onDone;

    public static void Shard(Vector3 worldFrom, int count, Action onDone)
    {
        StoryReveal r = Create(onDone);
        r.StartCoroutine(r.ShardRoutine(worldFrom, count));
    }

    public static void Banner(string title, string sub, Action onDone)
    {
        StoryReveal r = Create(onDone);
        r.StartCoroutine(r.BannerRoutine(title, sub));
    }

    private static StoryReveal Create(Action onDone)
    {
        var go = new GameObject("StoryReveal");
        DontDestroyOnLoad(go);
        StoryReveal r = go.AddComponent<StoryReveal>();
        r.onDone = onDone;
        r.textFont = PixelUI.FindTextFont();
        r.pixelFont = PixelUI.FindPixelFont();
        r.page = OptionsKit.CreatePage(go, 290, 0f, out r.scaler);
        OptionsKit.Layout(r.page, r.scaler, Content);
        return r;
    }

    private void Done()
    {
        Destroy(gameObject);
        onDone?.Invoke();
    }

    private Image Dim() => transform.Find("Dim").GetComponent<Image>();

    /// <summary>Bildschirmpunkt -> Seitenpixel (Ursprung oben links).</summary>
    private Vector2 ToPage(Vector3 world)
    {
        Camera cam = Camera.main;
        if (cam == null) return new Vector2(OptionsKit.RefW / 2f, OptionsKit.RefH / 2f);
        Vector3 sp = cam.WorldToScreenPoint(world);
        float s = scaler.scaleFactor;
        Vector2 off = page.anchoredPosition;
        return new Vector2(sp.x / s - off.x, (Screen.height - sp.y) / s + off.y);
    }

    private static bool Clicked() =>
        Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
        Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape);

    // ------------------------------------------------------------------

    private IEnumerator ShardRoutine(Vector3 worldFrom, int count)
    {
        Image dim = Dim();
        Sprite[] big = StoryArt.Strip("glutsplitter_gross");
        Sprite[] small = StoryArt.Strip("glutsplitter");

        // Strahlen: 12 duenne Balken, die sich langsam drehen
        RectTransform rays = OptionsKit.Rect("Rays", page, OptionsKit.RefW / 2f, 104, 0, 0);
        var rayImgs = new List<Image>();
        for (int i = 0; i < 12; i++)
        {
            RectTransform arm = OptionsKit.Rect("Ray" + i, rays, 0, 0, 0, 0);
            arm.localEulerAngles = new Vector3(0, 0, i * 30f);
            int bw = i % 2 == 0 ? 4 : 2;
            Image bar = OptionsKit.Img("Bar", arm, -bw / 2, -150, bw, 150, GameHudSkin.White, new Color(1f, 0.6f, 0.2f, 0f));
            rayImgs.Add(bar);
        }

        Vector2 from = ToPage(worldFrom);
        Vector2 to = new Vector2(OptionsKit.RefW / 2f, 104);
        Image shard = OptionsKit.Img("Shard", page, from.x - 16, from.y - 16, 32, 32, big.Length > 0 ? big[0] : null);
        RectTransform srt = shard.rectTransform;

        StorySounds.Whoosh();
        float start = Time.unscaledTime;
        const float Fly = 0.9f;
        bool landed = false;
        GameObject ribbon = null, label = null;
        var slots = new List<Image>();

        while (true)
        {
            float t = Time.unscaledTime - start;
            float q = Mathf.Clamp01(t / Fly);
            float e = 1f - (1f - q) * (1f - q) * (1f - q);
            Vector2 p = Vector2.Lerp(from, to, e);
            p.y -= Mathf.Sin(q * Mathf.PI) * 30f;
            int scale = q < 0.5f ? 1 : q < 0.85f ? 2 : 3;
            if (landed) scale = 3;
            float size = 32 * scale;
            float bob = landed ? Mathf.Round(Mathf.Sin(t * 3f) * 2f) : 0f;
            OptionsKit.Move(srt, p.x - size / 2f, p.y - size / 2f + bob, size, size);
            if (big.Length > 0) shard.sprite = big[Mathf.FloorToInt(t * 8f) % big.Length];

            dim.color = new Color(0.06f, 0.04f, 0.05f, Mathf.Clamp01(t / 0.5f) * 0.6f);
            rays.localEulerAngles = new Vector3(0, 0, t * 12f);
            float ra = landed ? 0.16f + 0.05f * Mathf.Sin(t * 4f) : 0.16f * q;
            foreach (Image r in rayImgs) r.color = new Color(1f, 0.6f, 0.2f, ra);

            if (!landed && q >= 1f)
            {
                landed = true;
                StorySounds.Shard();
                ScreenShake.Kick(2f, 0.2f);

                RectTransform rib = OptionsKit.Rect("Ribbon", page, 0, 0, OptionsKit.RefW, 40);
                OptionsKit.Ribbon(rib, OptionsKit.RefW / 2f, 160, StoryTexts.T("story.ui.shard"), pixelFont, textFont);
                ribbon = rib.gameObject;

                string countText = string.Format(StoryTexts.T("story.ui.shardcount"), count);
                label = OptionsKit.ShadowLabel("Count", page, 0, 186, OptionsKit.RefW, 12, countText, textFont,
                                               OptionsKit.SizeText, GameHudSkin.GoldLight, TextAlignmentOptions.Center).gameObject;

                // vier Plaetze, die bisherigen voll, der neue blinkt auf
                const int gap = 22;
                float sx = OptionsKit.RefW / 2f - (4 * gap - 6) / 2f;
                for (int i = 0; i < 4; i++)
                {
                    Image slot = OptionsKit.Img("Slot" + i, page, sx + i * gap, 202, 16, 16, small.Length > 0 ? small[0] : null);
                    slot.color = i < count ? Color.white : new Color(0.2f, 0.16f, 0.18f, 0.9f);
                    slots.Add(slot);
                }
            }

            if (landed)
            {
                for (int i = 0; i < slots.Count && i < count; i++)
                {
                    if (small.Length > 0) slots[i].sprite = small[(Mathf.FloorToInt(t * 8f) + i) % small.Length];
                    if (i == count - 1)
                    {
                        float pop = Mathf.Clamp01((t - Fly) / 0.3f);
                        slots[i].color = new Color(1f, 1f, 1f, pop);
                    }
                }
                if (t > Fly + 0.5f && Clicked()) break;
                if (t > Fly + 2.6f) break;
            }
            yield return null;
        }

        // ausblenden
        float endAt = Time.unscaledTime;
        Graphic[] all = GetComponentsInChildren<Graphic>();
        var baseA = new Dictionary<Graphic, float>();
        foreach (Graphic g in all) baseA[g] = g.color.a;
        while (Time.unscaledTime - endAt < 0.3f)
        {
            float k = 1f - (Time.unscaledTime - endAt) / 0.3f;
            foreach (Graphic g in all)
                if (g != null) g.color = new Color(g.color.r, g.color.g, g.color.b, baseA[g] * k);
            yield return null;
        }
        Done();
    }

    private IEnumerator BannerRoutine(string title, string sub)
    {
        Image dim = Dim();
        RectTransform root = OptionsKit.Rect("Banner", page, 0, 0, OptionsKit.RefW, OptionsKit.RefH);
        OptionsKit.Ribbon(root, OptionsKit.RefW / 2f, 112, title, pixelFont, textFont);
        OptionsKit.ShadowLabel("Sub", root, 0, 138, OptionsKit.RefW, 12, sub, textFont, OptionsKit.SizeText,
                               GameHudSkin.GoldLight, TextAlignmentOptions.Center);
        Graphic[] all = root.GetComponentsInChildren<Graphic>();
        StorySounds.Shard();

        float start = Time.unscaledTime;
        while (true)
        {
            float t = Time.unscaledTime - start;
            float a = Mathf.Clamp01(t / 0.25f);
            dim.color = new Color(0.06f, 0.04f, 0.05f, 0.5f * a);
            root.anchoredPosition = new Vector2(0f, -Mathf.Round((1f - a) * -12f));
            foreach (Graphic g in all) g.color = new Color(g.color.r, g.color.g, g.color.b, a);
            if (t > 0.6f && Clicked()) break;
            if (t > 3f) break;
            yield return null;
        }
        Done();
    }
}
