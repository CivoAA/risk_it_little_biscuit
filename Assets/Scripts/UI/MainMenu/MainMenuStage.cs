using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Leben in der Hauptmenue-Kueche, alles per Code und im Pixelraster des
/// Hintergrunds (320x180, ein Bildpunkt = eine Canvas-Einheit):
///
///   - Vignette: Raender per Bayer-Dithering abgedunkelt, der Blick geht zur Mitte.
///   - Staubkoernchen schweben langsam durchs Licht und funkeln.
///   - Der Topf auf dem Herd dampft.
///   - Das Logo-Schild faellt beim Auftritt an seinen Ketten herunter und
///     pendelt danach ganz leicht.
///
/// Gehoert auf das Menue-Canvas (legt <see cref="JuicyMainMenu"/> selbst an);
/// erwartet dort die Kinder "Background" und "Title".
/// </summary>
[DisallowMultipleComponent]
public class MainMenuStage : MonoBehaviour
{
    private const int BgW = 320, BgH = 180;

    // Deckelrand des Topfs im Hintergrundbild (Pixel von unten links).
    private static readonly Vector2[] SteamVents = { new Vector2(10f, 83f), new Vector2(26f, 83f), new Vector2(18f, 86f) };

    private const int DustCount = 22;
    private const int SteamPool = 14;
    private const float SteamEvery = 0.3f, SteamLife = 2.4f;

    private struct Mote
    {
        public Image image;
        public float x, y, speed, phase, drift, alpha, twinkle;
    }

    private struct Puff
    {
        public Image image;
        public float age, x0, phase, wobble;
        public bool alive;
    }

    private RectTransform canvasRect, stage, title;
    private CanvasGroup stageGroup;
    private Texture2D vignetteTex;
    private readonly List<Mote> motes = new List<Mote>();
    private readonly Puff[] puffs = new Puff[SteamPool];
    private float steamTimer, elapsed;
    private Vector2 titleHome;
    private bool started;

    private static readonly int[,] Bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };

    private void Start()
    {
        canvasRect = (RectTransform)transform;

        stage = new GameObject("Stage", typeof(RectTransform)).GetComponent<RectTransform>();
        stage.SetParent(transform, false);
        stage.anchorMin = Vector2.zero;
        stage.anchorMax = Vector2.one;
        stage.offsetMin = stage.offsetMax = Vector2.zero;
        Transform bg = transform.Find("Background");
        stage.SetSiblingIndex(bg != null ? bg.GetSiblingIndex() + 1 : 0);
        stageGroup = stage.gameObject.AddComponent<CanvasGroup>();
        stageGroup.blocksRaycasts = false;
        stageGroup.interactable = false;

        BuildVignette();
        BuildMotes();
        BuildSteam();

        // Logo: Drehpunkt an die Oberkante (dort haengen die Ketten), Platz bleibt gleich.
        title = transform.Find("Title") as RectTransform;
        if (title != null)
        {
            Vector2 size = title.sizeDelta;
            Vector2 shift = new Vector2((0.5f - title.pivot.x) * size.x, (1f - title.pivot.y) * size.y);
            title.pivot = new Vector2(0.5f, 1f);
            title.anchoredPosition += shift;
            titleHome = title.anchoredPosition;
            title.anchoredPosition = titleHome + new Vector2(0f, 90f);
        }
    }

    private void OnDestroy()
    {
        if (vignetteTex != null) Destroy(vignetteTex);
    }

    // ==================================================================
    //  Aufbau
    // ==================================================================

    private void BuildVignette()
    {
        vignetteTex = new Texture2D(BgW, BgH, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "MenuVignette"
        };

        Color32 ink = GameHudSkin.Night;
        var px = new Color32[BgW * BgH];
        for (int y = 0; y < BgH; y++)
        {
            for (int x = 0; x < BgW; x++)
            {
                float dx = (x + 0.5f) / BgW - 0.5f, dy = ((y + 0.5f) / BgH - 0.55f) * 1.1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                float v = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 1.35f, r)) * 0.62f;
                // In Stufen von 0.12, dazwischen gerastert - kein weicher Verlauf auf Pixelart.
                const float step = 0.12f;
                float q = Mathf.Floor(v / step + Bayer[y & 3, x & 3] / 16f) * step;
                px[y * BgW + x] = new Color32(ink.r, ink.g, ink.b, (byte)(Mathf.Clamp01(q) * 255f));
            }
        }
        vignetteTex.SetPixels32(px);
        vignetteTex.Apply();

        RectTransform rt = new GameObject("Vignette", typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(stage, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        RawImage raw = rt.gameObject.AddComponent<RawImage>();
        raw.texture = vignetteTex;
        raw.raycastTarget = false;
    }

    private void BuildMotes()
    {
        for (int i = 0; i < DustCount; i++)
        {
            motes.Add(new Mote
            {
                image = Pixel("Mote", GameHudSkin.Cream),
                x = Random.Range(56f, 264f),
                y = Random.Range(24f, 170f),
                speed = Random.Range(1.2f, 3.4f),
                phase = Random.Range(0f, Mathf.PI * 2f),
                drift = Random.Range(2f, 7f),
                alpha = Random.Range(0.18f, 0.55f),
                twinkle = Random.Range(0.6f, 1.8f),
            });
        }
    }

    private void BuildSteam()
    {
        for (int i = 0; i < SteamPool; i++)
        {
            puffs[i].image = Pixel("Steam", GameHudSkin.Cream);
            puffs[i].image.enabled = false;
        }
    }

    private Image Pixel(string name, Color32 color)
    {
        RectTransform rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(stage, false);
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.sizeDelta = Vector2.one;
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = GameHudSkin.White;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // ==================================================================
    //  Ablauf
    // ==================================================================

    private void Update()
    {
        if (stage == null) return;
        if (StudioSplash.IsShowing)
        {
            stageGroup.alpha = 0f;
            return;
        }

        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
        elapsed += dt;
        float time = Time.unscaledTime;

        // Die Kueche kommt mit dem Menue - die Vignette ist ab dem ersten Bild da.
        stageGroup.alpha = 1f;

        Vector2 scale = new Vector2(canvasRect.rect.width / BgW, canvasRect.rect.height / BgH);

        AnimateMotes(time, dt, scale);
        AnimateSteam(dt, scale);
        AnimateTitle(time);
    }

    private void AnimateMotes(float time, float dt, Vector2 scale)
    {
        float appear = Mathf.Clamp01((elapsed - 0.3f) / 1.2f);
        for (int i = 0; i < motes.Count; i++)
        {
            Mote m = motes[i];
            m.y += m.speed * dt;
            if (m.y > 172f)
            {
                m.y = 20f;
                m.x = Random.Range(56f, 264f);
            }

            float x = m.x + Mathf.Sin(time * 0.35f + m.phase) * m.drift;
            float fade = Mathf.Clamp01((172f - m.y) / 20f) * Mathf.Clamp01((m.y - 20f) / 14f);
            float glint = 0.55f + 0.45f * Mathf.Sin(time * m.twinkle + m.phase * 3f);

            m.image.rectTransform.anchoredPosition = new Vector2(Mathf.Round(x * scale.x), Mathf.Round(m.y * scale.y));
            Color c = m.image.color;
            c.a = m.alpha * fade * glint * appear;
            m.image.color = c;
            motes[i] = m;
        }
    }

    private void AnimateSteam(float dt, Vector2 scale)
    {
        steamTimer -= dt;
        if (steamTimer <= 0f)
        {
            steamTimer = SteamEvery * Random.Range(0.7f, 1.3f);
            for (int i = 0; i < puffs.Length; i++)
            {
                if (puffs[i].alive) continue;
                Vector2 vent = SteamVents[Random.Range(0, SteamVents.Length)];
                puffs[i].alive = true;
                puffs[i].age = 0f;
                puffs[i].x0 = vent.x + Random.Range(-1f, 1f);
                puffs[i].phase = Random.Range(0f, Mathf.PI * 2f);
                puffs[i].wobble = Random.Range(1.2f, 2.6f);
                puffs[i].image.enabled = true;
                break;
            }
        }

        for (int i = 0; i < puffs.Length; i++)
        {
            if (!puffs[i].alive) continue;
            Puff p = puffs[i];
            p.age += dt;
            float life = p.age / SteamLife;
            if (life >= 1f)
            {
                p.alive = false;
                p.image.enabled = false;
                puffs[i] = p;
                continue;
            }

            // Steigt, wird breiter und leiser; ganze Pixelgroessen 1 -> 2 -> 3.
            int size = life < 0.25f ? 1 : life < 0.6f ? 2 : 3;
            float x = p.x0 + Mathf.Sin(p.age * p.wobble + p.phase) * (1f + life * 2.5f) + life * 3f;
            float y = 83f + p.age * 9f;
            RectTransform rt = p.image.rectTransform;
            rt.sizeDelta = new Vector2(size * Mathf.Round(scale.x), size * Mathf.Round(scale.y));
            rt.anchoredPosition = new Vector2(Mathf.Round(x * scale.x), Mathf.Round(y * scale.y));
            Color c = p.image.color;
            c.a = 0.42f * Mathf.Clamp01(life * 6f) * (1f - life) * (1f - life);
            p.image.color = c;
            puffs[i] = p;
        }
    }

    private void AnimateTitle(float time)
    {
        if (title == null) return;

        // Faellt in 0.9 s an den Ketten herunter und federt aus.
        float t = Mathf.Clamp01(elapsed / 0.9f);
        float drop = (1f - BounceOut(t)) * 90f;
        title.anchoredPosition = titleHome + new Vector2(0f, drop);

        // Danach ein ruhiges Pendeln; beim Aufschlagen etwas staerker.
        float settle = Mathf.Clamp01((elapsed - 0.6f) / 1.5f);
        float kick = Mathf.Exp(-Mathf.Max(0f, elapsed - 0.6f) * 2.2f) * 2.2f * settle;
        float angle = Mathf.Sin(time * 1.15f) * 0.8f * settle + Mathf.Sin((elapsed - 0.6f) * 7f) * kick;
        title.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private static float BounceOut(float t)
    {
        const float n = 7.5625f, d = 2.75f;
        if (t < 1f / d) return n * t * t;
        if (t < 2f / d) { t -= 1.5f / d; return n * t * t + 0.75f; }
        if (t < 2.5f / d) { t -= 2.25f / d; return n * t * t + 0.9375f; }
        t -= 2.625f / d;
        return n * t * t + 0.984375f;
    }
}
