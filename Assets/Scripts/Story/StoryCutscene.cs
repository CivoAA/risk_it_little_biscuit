using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Die beiden Cutscenes der Story - als bebildertes Bilderbuch: ein Fenster
/// im Holzrahmen (wie das Panorama der Levelauswahl), darin Bilder, die sich
/// bewegen, leuchten und zerfallen, darunter die getippte Bildunterschrift.
///
///   INTRO (erster Besuch im Hub)
///     1  die vier Welten ziehen vorbei          (Level-Panoramen)
///     2  der Vulkan glueht                      (Panorama, Glut, Beben)
///     3  im Ofen faellt ein Keks hinters Blech  (ofen_*.png)
///     4  aus ihm wird DER VERKOHLTE             (Silhouette -> Glut, Titelband)
///     5  sein Herz zerspringt in vier Splitter, die zu vier Bossen fliegen
///     6  eine alte Kuechenuhr tickt             (Pomo)
///
///   FINALE (im Lauf, nachdem das Herz des Verkohlten zerfallen ist)
///     1  der Verkohlte kuehlt ab                + Gespraech finale.a
///     2  Pomo klingelt - DRRRRRING!             + Gespraech finale.b
///     3  er zerfaellt zu Sternen
///     4  Endkarte ENDE / Danke fuers Spielen
///
/// Klick/Leertaste/Enter = naechstes Bild (bzw. erst fertig tippen), Esc =
/// alles ueberspringen. Laeuft auf unskalierter Zeit; im Lauf haelt die
/// Cutscene die Zeit an, im Hub sperrt sie den Hub.
/// </summary>
public class StoryCutscene : MonoBehaviour
{
    public enum Kind { Intro, Finale }

    // ==================================================================
    //  Masse (Seitenpixel)
    // ==================================================================

    private const int WinX = 108, WinY = 30, WinW = 264, WinH = 156;
    private const int CapY = 198, CapW = 380, CapH = 44;
    private const int Order = 280;
    private static readonly RectInt Content = new RectInt(0, 0, OptionsKit.RefW, OptionsKit.RefH);

    private static readonly Color Ember = new Color32(0xff, 0x8a, 0x2a, 0xff);
    private static readonly Color EmberHot = new Color32(0xff, 0xd3, 0x6a, 0xff);

    private static StoryCutscene instance;
    public static bool IsPlaying => instance != null;

    private Kind kind;
    private Action onDone;
    private bool finished, skipAll;
    private float oldTimeScale = 1f;
    private bool froze;
    private HubUI hub;

    private TMP_FontAsset textFont, pixelFont;
    private RectTransform page, window, windowFrame, overlayRoot;
    private CanvasScaler scaler;
    private Vector2Int lastScreen;
    private Image fade, flash, dimWindow;
    private TextMeshProUGUI caption;
    private DialogueText captionTyper;
    private Vector2 shake;
    private float shakeUntil, shakeAmp;
    private float inputBlockedUntil;

    private readonly List<Image> layers = new List<Image>();
    private readonly List<Particle> particles = new List<Particle>();
    private static Sprite glowSprite;

    private class Particle
    {
        public Image Img;
        public Vector2 Pos, Vel;
        public float Life, Age;
        public Color Color;
        public bool Fade = true;
        public Sprite[] Frames;
        public float Fps;
    }

    public static void Play(Kind kind, Action onDone)
    {
        if (instance != null) return;
        var go = new GameObject("StoryCutscene");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<StoryCutscene>();
        instance.kind = kind;
        instance.onDone = onDone;
        instance.Begin();
    }

    // ==================================================================
    //  Rahmen
    // ==================================================================

    private void Begin()
    {
        textFont = PixelUI.FindTextFont();
        pixelFont = PixelUI.FindPixelFont();

        if (GameManager.Instance != null)
        {
            oldTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            froze = true;
        }
        hub = FindAnyObjectByType<HubUI>();
        if (hub != null)
        {
            HubUI.PushModal();
            hub.SetPlayerFrozen(true);
        }

        Build();
        StartCoroutine(Run());
    }

    private void Build()
    {
        page = OptionsKit.CreatePage(gameObject, Order, 1f, out scaler);
        // CreatePage dunkelt nur ab - hier soll es ganz schwarz sein.
        Image dim = transform.Find("Dim").GetComponent<Image>();
        dim.color = new Color(0.035f, 0.025f, 0.03f, 1f);

        // Rahmen + Fenster
        windowFrame = OptionsKit.Img("Frame", page, WinX - 7, WinY - 7, WinW + 14, WinH + 14, GameHudSkin.Card, true).rectTransform;
        RectTransform clip = OptionsKit.Rect("Clip", page, WinX, WinY, WinW, WinH);
        clip.gameObject.AddComponent<RectMask2D>();
        window = OptionsKit.Rect("Window", clip, 0, 0, WinW, WinH);
        OptionsKit.Img("WindowBack", window, 0, 0, WinW, WinH, GameHudSkin.White, new Color(0.06f, 0.04f, 0.05f, 1f));
        overlayRoot = OptionsKit.Rect("Overlay", clip, 0, 0, WinW, WinH);
        dimWindow = OptionsKit.Img("WindowDim", overlayRoot, 0, 0, WinW, WinH, GameHudSkin.White, new Color(0f, 0f, 0f, 0f));
        flash = OptionsKit.Img("Flash", overlayRoot, 0, 0, WinW, WinH, GameHudSkin.White, new Color(1f, 1f, 1f, 0f));

        caption = OptionsKit.Label("Caption", page, (OptionsKit.RefW - CapW) / 2, CapY, CapW, CapH, "", textFont,
                                   OptionsKit.SizeText, GameHudSkin.Parchment, TextAlignmentOptions.Top);
        caption.textWrappingMode = TextWrappingModes.Normal;
        caption.lineSpacing = 6f;
        captionTyper = caption.gameObject.AddComponent<DialogueText>();
        captionTyper.CharsPerSecond = 40f;

        OptionsKit.Label("Skip", page, OptionsKit.RefW - 168, OptionsKit.RefH - 14, 160, 10, StoryTexts.T("story.ui.skip"),
                         textFont, 8f, OptionsKit.WithAlpha(GameHudSkin.Stone, 0.9f), TextAlignmentOptions.Right);

        // Ueberblendung ueber allem
        fade = OptionsKit.Img("Fade", page, -400, -400, OptionsKit.RefW + 800, OptionsKit.RefH + 800, GameHudSkin.White,
                              new Color(0f, 0f, 0f, 1f));

        OptionsKit.Layout(page, scaler, Content);
        lastScreen = new Vector2Int(Screen.width, Screen.height);
    }

    private void Update()
    {
        var size = new Vector2Int(Screen.width, Screen.height);
        if (size != lastScreen)
        {
            lastScreen = size;
            OptionsKit.Layout(page, scaler, Content);
        }

        if (!StoryDialog.IsOpen && Input.GetKeyDown(KeyCode.Escape) && !skipAll)
        {
            skipAll = true;
            OptionsKit.PlayClick();
        }

        // Wackeln: das ganze Fenster ruckt um ganze Pixel
        float now = Time.unscaledTime;
        if (now < shakeUntil)
        {
            float a = shakeAmp * (shakeUntil - now) / 0.4f;
            shake = new Vector2(Mathf.Round(UnityEngine.Random.Range(-a, a)), Mathf.Round(UnityEngine.Random.Range(-a, a)));
        }
        else shake = Vector2.zero;
        window.anchoredPosition = new Vector2(shake.x, -shake.y);

        UpdateParticles(Time.unscaledDeltaTime);
    }

    private void Finish()
    {
        if (finished) return;
        finished = true;
        if (froze) Time.timeScale = oldTimeScale;
        if (hub != null)
        {
            HubUI.PopModal();
            hub.SetPlayerFrozen(false);
        }
        if (instance == this) instance = null;
        Destroy(gameObject);
        onDone?.Invoke();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (!finished)
        {
            if (froze) Time.timeScale = oldTimeScale;
            if (hub != null)
            {
                HubUI.PopModal();
                hub.SetPlayerFrozen(false);
            }
        }
    }

    // ==================================================================
    //  Ablauf
    // ==================================================================

    private IEnumerator Run()
    {
        yield return null;
        IEnumerator body = kind == Kind.Intro ? Intro() : Finale();
        while (!skipAll && body.MoveNext()) yield return body.Current;

        if (StoryDialog.IsOpen) yield break;   // sollte nicht passieren
        yield return Fade(1f, 0.4f, true);
        Finish();
    }

    // ------------------------------------------------------------ Intro

    private IEnumerator Intro()
    {
        // 1 - die vier Welten
        Clear();
        string[] panos = { "pano_kueche", "pano_wald", "pano_eis", "pano_geist" };
        var pano = new Image[panos.Length];
        for (int i = 0; i < panos.Length; i++)
        {
            pano[i] = Layer("Pano" + i, StoryArt.Frame(panos[i], 0), 3);
            pano[i].color = new Color(1f, 1f, 1f, i == 0 ? 1f : 0f);
        }
        AddDust(18, GameHudSkin.Cream, 0.35f);
        yield return Fade(0f, 0.6f);
        Caption("story.cut.intro.1");
        float start = Time.unscaledTime;
        const float each = 1.8f;
        const float slide = 0.45f;
        float baseX = (WinW - 246) / 2f, baseY = (WinH - 138) / 2f;
        int shown = 0;
        while (!CaptionDoneAnd(start, panos.Length * each))
        {
            float t = Time.unscaledTime - start;
            int cur = Mathf.Min(panos.Length - 1, Mathf.FloorToInt(t / each));
            if (cur != shown) { shown = cur; StorySounds.Whoosh(); }
            float q = cur == 0 ? 1f : Mathf.Clamp01((t - cur * each) / slide);
            float e = 1f - (1f - q) * (1f - q) * (1f - q);
            for (int i = 0; i < panos.Length; i++)
            {
                // die aktuelle Postkarte gleitet von rechts herein, die alte nach links hinaus
                float x;
                if (i == cur) x = baseX + (1f - e) * (WinW + 8);
                else if (i == cur - 1) x = baseX - e * (WinW + 8);
                else { pano[i].color = Color.clear; continue; }
                pano[i].color = Color.white;
                Place(pano[i], Mathf.Round(x), baseY);
            }
            yield return null;
        }
        yield return Fade(1f, 0.35f);

        // 2 - der Vulkan
        Clear();
        Image vulkan = Layer("Vulkan", StoryArt.Frame("pano_vulkan", 0), 3);
        Image glow = Glow("Glow", new Vector2(WinW * 0.62f, WinH * 0.3f), 220, new Color(1f, 0.35f, 0.1f, 0f));
        Place(vulkan, (WinW - 246) / 2f, (WinH - 138) / 2f + 3f);
        yield return Fade(0f, 0.6f);
        Caption("story.cut.intro.2");
        start = Time.unscaledTime;
        float nextEmber = 0f;
        bool boomed = false;
        while (!CaptionDoneAnd(start, 4.2f))
        {
            float t = Time.unscaledTime - start;
            glow.color = new Color(1f, 0.35f, 0.1f, 0.18f + 0.12f * Mathf.Sin(t * 3f));
            Place(vulkan, (WinW - 246) / 2f, (WinH - 138) / 2f + Mathf.Round(Mathf.Max(0f, 3f - t * 2f)));
            if (t > nextEmber)
            {
                nextEmber = t + 0.05f;
                Spark(new Vector2(UnityEngine.Random.Range(20f, WinW - 20f), WinH + 2),
                      new Vector2(UnityEngine.Random.Range(-6f, 6f), -UnityEngine.Random.Range(18f, 40f)),
                      UnityEngine.Random.value < 0.3f ? EmberHot : Ember, 3.5f, UnityEngine.Random.value < 0.2f ? 2 : 1);
            }
            if (!boomed && t > 1.6f)
            {
                boomed = true;
                Shake(2f, 0.6f);
                StorySounds.Boom();
            }
            yield return null;
        }
        yield return Fade(1f, 0.35f);

        // 3 - der Ofen, der Keks faellt hinters Blech
        Clear();
        yield return OvenShot();

        // 4 - der Verkohlte
        Clear();
        yield return VillainShot();

        // 5 - das Herz zerspringt
        Clear();
        yield return HeartShot();

        // 6 - Pomo tickt
        Clear();
        yield return PomoShot();
    }

    private IEnumerator OvenShot()
    {
        const int S = 3;
        float ox = (WinW - 82 * S) / 2f, oy = (WinH - 46 * S) / 2f;
        Image back = Layer("Ofen", StoryArt.Frame("ofen_hinten", 0), S);
        Place(back, ox, oy);
        Image glut = Layer("Glut", StoryArt.Frame("ofen_glut", 0), S);
        Place(glut, ox, oy);
        Image keks = Layer("Keks", StoryArt.Frame("ofen_keks", 0), S);
        Image blech = Layer("Blech", StoryArt.Frame("ofen_blech", 0), S);
        Vector2 trayAt = new Vector2(19, 19);
        Place(blech, ox + trayAt.x * S, oy + trayAt.y * S);
        Vector2 keksStart = new Vector2(19 + 42, 18), keksFloor = new Vector2(60, 36);
        Place(keks, ox + keksStart.x * S, oy + keksStart.y * S);

        yield return Fade(0f, 0.5f);
        Caption("story.cut.intro.3");
        float start = Time.unscaledTime;
        bool landed = false, whooshed = false;
        while (!CaptionDoneAnd(start, 6.4f))
        {
            float t = Time.unscaledTime - start;
            glut.sprite = StoryArt.Frame("ofen_glut", Mathf.FloorToInt(t * 6f));

            // 0.0-1.2 wackelt der Keks am Rand, 1.2-1.9 kippt er und faellt
            Vector2 k = keksStart;
            if (t < 1.2f) k.x += Mathf.Floor(t * 4f) % 2 == 0 ? 0f : 1f;
            else if (t < 1.9f)
            {
                float q = (t - 1.2f) / 0.7f;
                k = Vector2.Lerp(keksStart, keksFloor, q);
                k.y = Mathf.Lerp(keksStart.y, keksFloor.y, q * q);
            }
            else
            {
                k = keksFloor;
                if (!landed)
                {
                    landed = true;
                    Shake(1f, 0.25f);
                    StorySounds.Boom();
                }
                float b = t - 1.9f;
                if (b < 0.3f) k.y -= Mathf.Round(Mathf.Sin(b / 0.3f * Mathf.PI) * 2f);
            }
            Place(keks, ox + Mathf.Round(k.x) * S, oy + Mathf.Round(k.y) * S);

            // 2.6-3.6 zieht jemand das Blech heraus (nach links)
            if (t > 2.6f)
            {
                if (!whooshed) { whooshed = true; StorySounds.Whoosh(); }
                float q = Mathf.Clamp01((t - 2.6f) / 1f);
                Place(blech, ox + Mathf.Round(trayAt.x - q * q * 80f) * S, oy + trayAt.y * S);
            }
            // danach dunkelt der Keks nach
            if (t > 3.8f) keks.sprite = StoryArt.Frame("ofen_keks", Mathf.Min(3, 1 + Mathf.FloorToInt((t - 3.8f) / 0.8f)));
            yield return null;
        }
        yield return Fade(1f, 0.35f);
    }

    private IEnumerator VillainShot()
    {
        Sprite[] idle = StoryArt.Strip("verkohlter_idle");
        Image glow = Glow("Glow", new Vector2(WinW / 2f, WinH - 40f), 200, new Color(1f, 0.3f, 0.08f, 0f));
        Image v = Layer("Verkohlter", idle.Length > 0 ? idle[0] : null, 1);
        Place(v, (WinW - 128) / 2f, WinH - 128 - 4);
        v.color = Color.black;
        yield return Fade(0f, 0.5f);
        Caption("story.cut.intro.4");
        float start = Time.unscaledTime;
        float nextEmber = 0f;
        bool titled = false;
        while (!CaptionDoneAnd(start, 5.2f))
        {
            float t = Time.unscaledTime - start;
            if (idle.Length > 0) v.sprite = idle[Mathf.FloorToInt(t * 12f) % idle.Length];
            float q = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.6f) / 2.2f));
            v.color = Color.Lerp(Color.black, Color.white, q);
            glow.color = new Color(1f, 0.3f, 0.08f, 0.35f * q + 0.05f * Mathf.Sin(t * 5f));
            if (t > nextEmber && q > 0.2f)
            {
                nextEmber = t + 0.07f;
                Spark(new Vector2(WinW / 2f + UnityEngine.Random.Range(-40f, 40f), WinH - UnityEngine.Random.Range(20f, 70f)),
                      new Vector2(UnityEngine.Random.Range(-8f, 8f), -UnityEngine.Random.Range(15f, 35f)),
                      UnityEngine.Random.value < 0.3f ? EmberHot : Ember, 2.5f, 1);
            }
            if (!titled && t > 3.0f)
            {
                titled = true;
                RectTransform band = OptionsKit.Rect("Title", overlayRoot, 0, 0, WinW, 40);
                OptionsKit.Ribbon(band, WinW / 2f, 8, StoryTexts.T("story.cut.villain"), pixelFont, textFont);
                Flash(0.5f);
                Shake(3f, 0.5f);
                StorySounds.Boom();
            }
            yield return null;
        }
        yield return Fade(1f, 0.35f);
    }

    private IEnumerator HeartShot()
    {
        Sprite[] heart = VerkohlterArt.Strip("verkohlter_herz");
        Image glow = Glow("Glow", new Vector2(WinW / 2f, WinH / 2f), 230, new Color(1f, 0.32f, 0.1f, 0.25f));
        Image h = Layer("Herz", heart.Length > 0 ? heart[0] : null, 1);
        float hw = heart.Length > 0 ? heart[0].rect.width : 144, hh = heart.Length > 0 ? heart[0].rect.height : 152;
        Place(h, Mathf.Round((WinW - hw) / 2f), Mathf.Round((WinH - hh) / 2f));

        EnemyId[] bosses = { EnemyId.KeksKoenig, EnemyId.Glutwurz, EnemyId.Eiskaiser, EnemyId.Squiddy };
        Vector2[] spots = { new Vector2(10, 8), new Vector2(WinW - 58, 8), new Vector2(10, WinH - 56), new Vector2(WinW - 58, WinH - 56) };
        var portraits = new Image[4];
        var shards = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            portraits[i] = Layer("Boss" + i, StoryArt.BossPortrait(bosses[i]), 1);
            Place(portraits[i], spots[i].x, spots[i].y);
            portraits[i].color = new Color(1f, 1f, 1f, 0f);
            shards[i] = Layer("Splitter" + i, StoryArt.Frame("glutsplitter_gross", 0), 1);
            shards[i].color = Color.clear;
        }

        yield return Fade(0f, 0.5f);
        Caption("story.cut.intro.5");
        float start = Time.unscaledTime;
        int lastBeat = -1;
        bool broke = false;
        const float BreakAt = 2.2f;
        while (!CaptionDoneAnd(start, 6.6f))
        {
            float t = Time.unscaledTime - start;
            if (heart.Length > 0)
            {
                int f = Mathf.FloorToInt(t * 12f) % heart.Length;
                h.sprite = heart[f];
                if (f == 2 && lastBeat != Mathf.FloorToInt(t * 12f / heart.Length) && !broke)
                {
                    lastBeat = Mathf.FloorToInt(t * 12f / heart.Length);
                    Shake(1f, 0.15f);
                    StorySounds.Boom();
                }
            }

            if (!broke && t > BreakAt)
            {
                broke = true;
                Flash(0.8f);
                Shake(4f, 0.6f);
                StorySounds.Shard();
                h.color = new Color(0.45f, 0.4f, 0.42f, 1f);
                glow.color = new Color(1f, 0.32f, 0.1f, 0.08f);
            }

            if (broke)
            {
                float q = Mathf.Clamp01((t - BreakAt) / 0.9f);
                float e = 1f - (1f - q) * (1f - q);
                Vector2 from = new Vector2(WinW / 2f - 16, WinH / 2f - 16);
                for (int i = 0; i < 4; i++)
                {
                    Vector2 to = spots[i] + new Vector2(8, 8);
                    Vector2 p = Vector2.Lerp(from, to, e);
                    p.y -= Mathf.Sin(e * Mathf.PI) * 20f;
                    Place(shards[i], Mathf.Round(p.x), Mathf.Round(p.y));
                    shards[i].sprite = StoryArt.Frame("glutsplitter_gross", Mathf.FloorToInt(t * 10f) + i);
                    shards[i].color = q < 1f ? Color.white : new Color(1f, 1f, 1f, Mathf.Clamp01(1f - (t - BreakAt - 0.9f) * 3f));

                    float pa = Mathf.Clamp01((t - BreakAt - 0.8f) / 0.4f);
                    float pulse = 0.5f + 0.5f * Mathf.Sin((t - BreakAt) * 6f + i);
                    portraits[i].color = new Color(1f, Mathf.Lerp(1f, 0.55f, pulse * pa), Mathf.Lerp(1f, 0.5f, pulse * pa), pa);
                    if (q < 1f && UnityEngine.Random.value < 0.5f)
                        Spark(p + new Vector2(16, 16), UnityEngine.Random.insideUnitCircle * 10f, Ember, 0.6f, 1);
                }
            }
            yield return null;
        }
        yield return Fade(1f, 0.35f);
    }

    private IEnumerator PomoShot()
    {
        Image glow = Glow("Glow", new Vector2(WinW / 2f, WinH / 2f + 6), 200, new Color(1f, 0.75f, 0.45f, 0.18f));
        Image pomo = Layer("Pomo", StoryArt.Frame("pomo_portrait", 0), 2);
        Place(pomo, (WinW - 96) / 2f - 2f - 40, (WinH - 96) / 2f + 4);
        // Die Spielfigur selbst (ohne Keksdosen-Hintergrund), doppelt gross
        Image me = OptionsKit.Img("Ich", window, 0, 0, 1, 1, null, Color.clear);
        layers.Add(me);
        AddDust(14, GameHudSkin.GoldLight, 0.4f);

        yield return Fade(0f, 0.6f);
        Caption("story.cut.intro.6");
        float start = Time.unscaledTime;
        int lastTick = -1;
        while (!CaptionDoneAnd(start, 5.6f))
        {
            float t = Time.unscaledTime - start;
            int tick = Mathf.FloorToInt(t * 2f);
            if (tick != lastTick)
            {
                lastTick = tick;
                StorySounds.Voice(StoryTexts.Who.Pomo, 'x');
                StorySounds.Voice(StoryTexts.Who.Pomo, 'x');
            }
            // Ticken: im Sekundentakt nach links und rechts wippen
            float lean = tick % 2 == 0 ? -1f : 1f;
            bool blink = (t % 3.1f) < 0.12f;
            pomo.sprite = StoryArt.Portrait(StoryTexts.Who.Pomo, StoryTexts.Mood.Normal, false, blink);
            Place(pomo, (WinW - 96) / 2f + lean * 2f - 40, (WinH - 96) / 2f + 4);
            glow.color = new Color(1f, 0.75f, 0.45f, 0.16f + 0.04f * Mathf.Sin(t * 2f));

            // ab 2.4 s wacht der Krümel auf (rechts, huepft einmal)
            Sprite ps = StoryArt.PlayerFrame();
            if (ps != null && t > 2.4f)
            {
                float q = Mathf.Clamp01((t - 2.4f) / 0.3f);
                me.color = new Color(1f, 1f, 1f, q);
                float hop = t > 3.2f && t < 3.5f ? Mathf.Round(Mathf.Sin((t - 3.2f) / 0.3f * Mathf.PI) * 6f) : 0f;
                me.sprite = ps;
                Rect r = StoryArt.PlayerRect(ps, WinW / 2f + 72, (WinH - 96) / 2f + 4 + 92 - hop, 2);
                OptionsKit.Move(me.rectTransform, r.x, r.y, r.width, r.height);
            }
            yield return null;
        }
    }

    // ----------------------------------------------------------- Finale

    private IEnumerator Finale()
    {
        Sprite[] idle = StoryArt.Strip("verkohlter_idle");
        Sprite[] tod = StoryArt.Strip("verkohlter_tod");
        Clear();
        Image glow = Glow("Glow", new Vector2(WinW / 2f, WinH - 40f), 200, new Color(1f, 0.3f, 0.08f, 0.3f));
        Image v = Layer("Verkohlter", idle.Length > 0 ? idle[0] : null, 1);
        Place(v, (WinW - 128) / 2f, WinH - 128 - 4);

        // 1 - er kuehlt ab
        yield return Fade(0f, 0.8f);
        Caption("story.cut.finale.1");
        float start = Time.unscaledTime;
        Color cold = new Color(0.42f, 0.46f, 0.6f, 1f);
        while (!CaptionDoneAnd(start, 3.6f))
        {
            float t = Time.unscaledTime - start;
            float q = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 3f));
            if (idle.Length > 0) v.sprite = idle[Mathf.FloorToInt(t * Mathf.Lerp(12f, 3f, q)) % idle.Length];
            v.color = Color.Lerp(Color.white, cold, q);
            glow.color = new Color(1f, 0.3f, 0.08f, 0.3f * (1f - q));
            if (UnityEngine.Random.value < 0.25f * (1f - q))
                Spark(new Vector2(WinW / 2f + UnityEngine.Random.Range(-30f, 30f), WinH - 60f),
                      new Vector2(0f, -UnityEngine.Random.Range(10f, 25f)), Ember, 1.5f, 1);
            yield return null;
        }
        captionTyper.Play("");
        yield return Talk("finale.a");

        // 2 - DRRRRRING!
        Image pomo = Layer("Pomo", StoryArt.PomoRing(0), 2);
        pomo.color = Color.clear;
        TextMeshProUGUI ring = OptionsKit.ShadowLabel("Ring", overlayRoot, 0, 14, WinW, 24, StoryTexts.T("story.cut.ring"),
                                                      OptionsKit.PickFont("DRRRRRING!", pixelFont, textFont),
                                                      OptionsKit.SizeTitle, GameHudSkin.GoldLight, TextAlignmentOptions.Center);
        RectTransform ringRt = (RectTransform)ring.transform;
        RectTransform ringShadow = (RectTransform)overlayRoot.Find("RingShadow");
        ring.gameObject.SetActive(false);
        if (ringShadow != null) ringShadow.gameObject.SetActive(false);

        Place(v, 30, WinH - 128 - 4);
        start = Time.unscaledTime;
        StorySounds.Ring();
        Flash(1f);
        bool rung2 = false;
        while (Time.unscaledTime - start < 2.6f && !skipAll)
        {
            float t = Time.unscaledTime - start;
            if (!rung2 && t > 1.3f) { rung2 = true; StorySounds.Ring(); }
            bool ringing = t < 2.4f;
            pomo.color = Color.white;
            pomo.sprite = ringing ? StoryArt.PomoRing(Mathf.FloorToInt(t * 18f))
                                  : StoryArt.Portrait(StoryTexts.Who.Pomo, StoryTexts.Mood.Happy, false, false);
            float jx = ringing ? (Mathf.FloorToInt(t * 18f) % 2 == 0 ? -2f : 2f) : 0f;
            Place(pomo, WinW - 96 - 34 + jx, (WinH - 96) / 2f + 10);
            ring.gameObject.SetActive(ringing);
            if (ringShadow != null) ringShadow.gameObject.SetActive(ringing);
            float rx = Mathf.Round(UnityEngine.Random.Range(-2f, 2f)), ry = Mathf.Round(UnityEngine.Random.Range(-1f, 1f));
            ringRt.anchoredPosition = new Vector2(rx, -(14 + ry));
            if (ringShadow != null) ringShadow.anchoredPosition = new Vector2(rx, -(15 + ry));
            if (ringing) Shake(2f, 0.1f);
            if (idle.Length > 0) v.sprite = idle[0];
            yield return null;
        }
        ring.gameObject.SetActive(false);
        if (ringShadow != null) ringShadow.gameObject.SetActive(false);
        captionTyper.Play("");
        yield return Talk("finale.b");

        // 3 - er zerfaellt zu Sternen
        Caption("story.cut.finale.2");
        start = Time.unscaledTime;
        Sprite[] stars = StoryArt.Strip("sterne");
        bool cracked = false;
        while (!CaptionDoneAnd(start, 5f))
        {
            float t = Time.unscaledTime - start;
            int f = Mathf.FloorToInt(t * 12f);
            if (tod.Length > 0)
            {
                v.sprite = tod[Mathf.Min(f, tod.Length - 1)];
                v.color = Color.Lerp(cold, Color.white, Mathf.Clamp01(t * 2f));
                if (f >= tod.Length) v.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - (t - tod.Length / 12f) * 2f));
            }
            if (!cracked && t > 0.4f) { cracked = true; StorySounds.Shard(); }
            if (t > 0.6f && t < 3.5f && UnityEngine.Random.value < 0.35f && stars.Length > 0)
            {
                Particle p = Spark(new Vector2(30 + 64 + UnityEngine.Random.Range(-34f, 34f), WinH - 40 - UnityEngine.Random.Range(0f, 50f)),
                                   new Vector2(UnityEngine.Random.Range(-6f, 10f), -UnityEngine.Random.Range(12f, 28f)),
                                   Color.white, 2.4f, 7);
                p.Frames = stars;
                p.Fps = 8f;
            }
            pomo.sprite = StoryArt.Portrait(StoryTexts.Who.Pomo, StoryTexts.Mood.Happy, false, (t % 2.7f) < 0.12f);
            yield return null;
        }
        yield return Fade(1f, 0.6f);

        // 4 - Endkarte
        yield return EndCard();
    }

    private IEnumerator EndCard()
    {
        Clear();
        windowFrame.gameObject.SetActive(false);
        window.parent.gameObject.SetActive(false);
        captionTyper.Play("");

        RectTransform card = OptionsKit.Rect("EndCard", page, 0, 0, OptionsKit.RefW, OptionsKit.RefH);
        OptionsKit.Ribbon(card, OptionsKit.RefW / 2f, 92, StoryTexts.T("story.cut.end"), pixelFont, textFont);
        OptionsKit.Label("Thanks", card, 40, 128, 400, 14, StoryTexts.T("story.cut.thanks"), textFont, OptionsKit.SizeText,
                         GameHudSkin.Cream, TextAlignmentOptions.Center);
        OptionsKit.Label("Endless", card, 40, 144, 400, 14, StoryTexts.T("story.cut.endless"), textFont, OptionsKit.SizeText,
                         GameHudSkin.Stone, TextAlignmentOptions.Center);
        Image pomo = OptionsKit.Img("Pomo", card, (OptionsKit.RefW - 48) / 2f, 168, 48, 48, StoryArt.Frame("pomo_portrait", 6));

        Sprite[] stars = StoryArt.Strip("sterne");
        var twinkles = new List<(Image img, float phase)>();
        var rnd = new System.Random(4);
        for (int i = 0; i < 14; i++)
        {
            float x = 60 + (float)rnd.NextDouble() * 360, y = 40 + (float)rnd.NextDouble() * 190;
            if (y > 80 && y < 220 && x > 150 && x < 330) continue;
            Image s = OptionsKit.Img("Stern" + i, card, Mathf.Round(x), Mathf.Round(y), 7, 7, stars.Length > 0 ? stars[0] : null);
            twinkles.Add((s, (float)rnd.NextDouble() * 4f));
        }

        yield return Fade(0f, 0.8f);
        StorySounds.Shard();
        float start = Time.unscaledTime;
        while (!skipAll)
        {
            float t = Time.unscaledTime - start;
            foreach (var (img, ph) in twinkles)
                if (stars.Length > 0) img.sprite = stars[Mathf.FloorToInt((t + ph) * 5f) % stars.Length];
            bool blink = (t % 3f) < 0.12f;
            pomo.sprite = StoryArt.Frame("pomo_portrait", blink ? 1 : 6);
            if (t > 1.5f && Clicked()) break;
            if (t > 12f) break;
            yield return null;
        }
    }

    // ==================================================================
    //  Bausteine
    // ==================================================================

    private IEnumerator Talk(string convo)
    {
        bool done = false;
        StoryDialog.Play(convo, () => done = true, new StoryDialog.Options
        {
            Dim = 0f, FreezeTime = false, BlockHub = false, SortingOrder = Order + 10,
        });
        while (!done) yield return null;
        yield return null;                 // der Klick, der den Dialog schloss, zaehlt hier nicht
        inputBlockedUntil = Time.unscaledTime + 0.2f;
    }

    private void Caption(string key)
    {
        captionTyper.Play(StoryTexts.T(key));
        inputBlockedUntil = Time.unscaledTime + 0.25f;
    }

    /// <summary>Fertig, wenn die Unterschrift steht und die Mindestzeit um ist - oder geklickt wurde.</summary>
    private bool CaptionDoneAnd(float start, float minSeconds)
    {
        if (skipAll) return true;
        bool click = Clicked();
        if (click && !captionTyper.IsDone)
        {
            captionTyper.Complete();
            return false;
        }
        if (click) return true;
        return captionTyper.IsDone && Time.unscaledTime - start >= minSeconds + 1.2f;
    }

    private bool Clicked()
    {
        if (StoryDialog.IsOpen || Time.unscaledTime < inputBlockedUntil) return false;
        return Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
               Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.E);
    }

    private IEnumerator Fade(float to, float seconds, bool ignoreSkip = false)
    {
        float from = fade.color.a;
        float start = Time.unscaledTime;
        while (Time.unscaledTime - start < seconds && (ignoreSkip || !skipAll))
        {
            float q = (Time.unscaledTime - start) / seconds;
            fade.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, q));
            yield return null;
        }
        fade.color = new Color(0f, 0f, 0f, to);
    }

    private void Flash(float strength)
    {
        StartCoroutine(FlashRoutine(strength));
    }

    private IEnumerator FlashRoutine(float strength)
    {
        float start = Time.unscaledTime;
        while (Time.unscaledTime - start < 0.45f)
        {
            float q = (Time.unscaledTime - start) / 0.45f;
            flash.color = new Color(1f, 0.97f, 0.9f, strength * (1f - q) * (1f - q));
            yield return null;
        }
        flash.color = Color.clear;
    }

    private void Shake(float amp, float seconds)
    {
        shakeAmp = Mathf.Max(amp, Time.unscaledTime < shakeUntil ? shakeAmp : 0f);
        shakeUntil = Mathf.Max(shakeUntil, Time.unscaledTime + seconds);
    }

    /// <summary>Bild im Fenster, ganzzahlig vergroessert.</summary>
    private Image Layer(string name, Sprite sprite, int scale)
    {
        float w = sprite != null ? sprite.rect.width * scale : 1, h = sprite != null ? sprite.rect.height * scale : 1;
        // Gleich mittig anlegen: die Schritte setzen ihre Bilder zwar selbst,
        // aber erst im naechsten Bild - so springt nichts waehrend der Einblendung.
        Image img = OptionsKit.Img(name, window, Mathf.Round((WinW - w) / 2f), Mathf.Round((WinH - h) / 2f), w, h, sprite);
        if (sprite == null) img.color = Color.clear;
        layers.Add(img);
        return img;
    }

    private static void Place(Image img, float x, float y)
    {
        RectTransform rt = img.rectTransform;
        rt.anchoredPosition = new Vector2(Mathf.Round(x), -Mathf.Round(y));
    }

    /// <summary>Weiches Licht (kein Pixelbild - Licht darf weich sein).</summary>
    private Image Glow(string name, Vector2 center, float size, Color color)
    {
        if (glowSprite == null)
        {
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(N / 2f, N / 2f)) / (N / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            glowSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        }
        Image img = OptionsKit.Img(name, window, center.x - size / 2f, center.y - size / 2f, size, size, glowSprite);
        img.color = color;
        layers.Add(img);
        return img;
    }

    private void Clear()
    {
        foreach (Image l in layers) if (l != null) Destroy(l.gameObject);
        layers.Clear();
        foreach (Particle p in particles) if (p.Img != null) Destroy(p.Img.gameObject);
        particles.Clear();
        captionTyper.Play("");
        for (int i = overlayRoot.childCount - 1; i >= 0; i--)
        {
            Transform c = overlayRoot.GetChild(i);
            if (c != dimWindow.transform && c != flash.transform) Destroy(c.gameObject);
        }
    }

    // ------------------------------------------------------------ Partikel

    private Particle Spark(Vector2 pos, Vector2 vel, Color color, float life, int size)
    {
        Image img = OptionsKit.Img("Funke", window, pos.x, pos.y, size, size, GameHudSkin.White, color);
        var p = new Particle { Img = img, Pos = pos, Vel = vel, Life = life, Color = color };
        particles.Add(p);
        return p;
    }

    private void AddDust(int count, Color color, float alpha)
    {
        for (int i = 0; i < count; i++)
        {
            Particle p = Spark(new Vector2(UnityEngine.Random.Range(0f, WinW), UnityEngine.Random.Range(0f, WinH)),
                               new Vector2(UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.Range(-4f, -1f)),
                               new Color(color.r, color.g, color.b, alpha), 999f, 1);
            p.Fade = false;
        }
    }

    private void UpdateParticles(float dt)
    {
        for (int i = particles.Count - 1; i >= 0; i--)
        {
            Particle p = particles[i];
            if (p.Img == null) { particles.RemoveAt(i); continue; }
            p.Age += dt;
            p.Pos += p.Vel * dt;
            if (!p.Fade)
            {
                // Staub: am Rand umklappen
                if (p.Pos.y < -2) p.Pos.y += WinH + 4;
                if (p.Pos.x < -2) p.Pos.x += WinW + 4;
                if (p.Pos.x > WinW + 2) p.Pos.x -= WinW + 4;
            }
            if (p.Age >= p.Life)
            {
                Destroy(p.Img.gameObject);
                particles.RemoveAt(i);
                continue;
            }
            if (p.Frames != null && p.Frames.Length > 0)
            {
                p.Img.sprite = p.Frames[Mathf.FloorToInt(p.Age * p.Fps) % p.Frames.Length];
                p.Img.color = new Color(1f, 1f, 1f, Mathf.Clamp01((p.Life - p.Age) / 0.6f));
            }
            else if (p.Fade)
            {
                float a = Mathf.Clamp01((p.Life - p.Age) / (p.Life * 0.5f));
                p.Img.color = new Color(p.Color.r, p.Color.g, p.Color.b, p.Color.a * a);
            }
            p.Img.rectTransform.anchoredPosition = new Vector2(Mathf.Round(p.Pos.x), -Mathf.Round(p.Pos.y));
        }
    }
}
