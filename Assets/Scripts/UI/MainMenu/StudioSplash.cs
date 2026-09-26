using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Studio-Logo direkt nach dem "Made with Unity": dunkler Pixel-Farbverlauf,
/// Logo blendet ein, steht kurz, blendet aus - dann gibt die Blende das
/// Hauptmenü frei und die Menümusik kommt hoch.
///
/// Die Komponente gehört auf das Menü-Canvas. Läuft nur einmal pro Spielstart;
/// wer später aus Hub oder Spiel ins Menü zurückkehrt, sieht es nicht noch mal.
/// Beliebige Taste oder Klick überspringt.
///
/// Der Verlauf wird zur Laufzeit als 320x180-Textur mit Bayer-Dithering
/// gebaut - gleiche Auflösung wie das UI-Raster, gleicher Pixel-Look. Die
/// Farben kommen aus dem Nachthimmel im Logo; hinter dem Logo am hellsten,
/// damit auch das lila "REVOKO" noch trägt.
/// </summary>
[DisallowMultipleComponent]
public class StudioSplash : MonoBehaviour
{
    [SerializeField] private Sprite logo;
    [SerializeField, Tooltip("Höhe des Logos im 320x180-Raster.")]
    private float logoHeight = 118f;

    [Header("Zeiten (Sekunden)")]
    [SerializeField] private float logoFadeIn = 0.8f;
    [SerializeField] private float hold = 1.8f;
    [SerializeField] private float logoFadeOut = 0.5f;
    [SerializeField, Tooltip("Hintergrund blendet weg, das Menü kommt darunter hervor.")]
    private float reveal = 0.7f;
    [SerializeField] private float musicIn = 1.5f;

    [Header("Verlauf (Mitte -> Rand)")]
    [SerializeField] private Color[] palette =
    {
        new Color32(0x35, 0x2d, 0x52, 0xff),
        new Color32(0x2b, 0x24, 0x44, 0xff),
        new Color32(0x22, 0x1c, 0x36, 0xff),
        new Color32(0x1a, 0x15, 0x29, 0xff),
        new Color32(0x12, 0x0e, 0x1c, 0xff),
        new Color32(0x0c, 0x09, 0x13, 0xff),
    };
    [SerializeField, Tooltip("Hellste Stelle des Verlaufs, 0..1 von links unten.")]
    private Vector2 glowCenter = new Vector2(0.5f, 0.53f);

    private const int TexW = 320, TexH = 180;

    private static readonly int[,] Bayer =
    {
        { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 }
    };

    private static bool shownThisSession;

    /// <summary>Solange das stimmt, wartet das Hauptmenü mit seinem Auftritt.</summary>
    public static bool IsShowing { get; private set; }

    private GameObject overlay;
    private RawImage background;
    private CanvasGroup logoGroup;
    private RectTransform logoRect;
    private Texture2D gradient;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        shownThisSession = false;
        IsShowing = false;
    }

    private void Awake()
    {
        // In Awake, damit schon das allererste Bild abgedeckt ist und das
        // JuicyMainMenu in seinem Start bereits sieht, dass es warten muss.
        if (shownThisSession || logo == null)
        {
            enabled = false;
            return;
        }

        shownThisSession = true;
        IsShowing = true;
        Build();
    }

    private void Start()
    {
        if (overlay != null) StartCoroutine(Run());
    }

    private void Build()
    {
        overlay = new GameObject("StudioSplash");
        Canvas canvas = overlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900; // unter dem SceneFader (1000)
        CanvasScaler scaler = overlay.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(TexW, TexH);
        scaler.matchWidthOrHeight = 1f; // Logo richtet sich nach der Höhe
        overlay.AddComponent<GraphicRaycaster>();

        gradient = BuildGradient();
        GameObject bg = new GameObject("Gradient", typeof(RectTransform));
        bg.transform.SetParent(overlay.transform, false);
        RectTransform bgRect = (RectTransform)bg.transform;
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;
        background = bg.AddComponent<RawImage>();
        background.texture = gradient;
        background.raycastTarget = true; // Klicks gehen nicht ans Menü darunter

        GameObject lg = new GameObject("Logo", typeof(RectTransform));
        lg.transform.SetParent(overlay.transform, false);
        logoRect = (RectTransform)lg.transform;
        logoRect.anchorMin = logoRect.anchorMax = logoRect.pivot = new Vector2(0.5f, 0.5f);
        float aspect = logo.rect.width / logo.rect.height;
        logoRect.sizeDelta = new Vector2(Mathf.Round(logoHeight * aspect), logoHeight);
        logoRect.anchoredPosition = Vector2.zero;
        Image img = lg.AddComponent<Image>();
        img.sprite = logo;
        img.preserveAspect = true;
        img.raycastTarget = false;
        logoGroup = lg.AddComponent<CanvasGroup>();
        logoGroup.alpha = 0f;
    }

    private Texture2D BuildGradient()
    {
        var tex = new Texture2D(TexW, TexH, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color[TexW * TexH];
        int last = palette.Length - 1;
        float cx = glowCenter.x * TexW, cy = glowCenter.y * TexH;

        for (int y = 0; y < TexH; y++)
        {
            for (int x = 0; x < TexW; x++)
            {
                // Elliptisch, breiter als hoch - wie das Bild selbst.
                float dx = (x - cx) / (TexW * 0.62f);
                float dy = (y - cy) / (TexH * 0.78f);
                float t = Mathf.Min(1f, Mathf.Sqrt(dx * dx + dy * dy));

                // Geordnetes Dithering zwischen zwei Nachbarfarben statt weichem
                // Übergang: sieht nach Pixelart aus und bandet nicht.
                float f = t * last;
                int i = (int)f;
                if (i < last && f - i > (Bayer[y & 3, x & 3] + 0.5f) / 16f) i++;
                pixels[y * TexW + x] = palette[i];
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private IEnumerator Run()
    {
        // Menümusik läuft schon (PlayOnAwake) - erst mit dem Menü hochziehen.
        List<(AudioSource source, float volume)> music = FindMusic();
        foreach (var m in music) m.source.volume = 0f;

        // ---------- Logo rein + stehen (überspringbar) ----------
        float shown = logoFadeIn + hold;
        for (float t = 0f; t < shown; t += Step())
        {
            float k = Smooth(Mathf.Clamp01(t / logoFadeIn));
            logoGroup.alpha = k;
            logoRect.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, k);
            if (t > 0.1f && Input.anyKeyDown) break;
            yield return null;
        }

        // ---------- Logo raus ----------
        float startAlpha = logoGroup.alpha;
        for (float t = 0f; t < logoFadeOut; t += Step())
        {
            logoGroup.alpha = startAlpha * (1f - Smooth(t / logoFadeOut));
            yield return null;
        }
        logoGroup.alpha = 0f;

        // ---------- Menü freigeben: Verlauf weg, Musik hoch ----------
        IsShowing = false;
        background.raycastTarget = false;
        // Die Musik lief während des Logos stumm mit - jetzt von vorn, damit sie
        // mit dem Menü anfängt und nicht mittendrin.
        foreach (var m in music)
        {
            if (m.source == null) continue;
            m.source.Stop();
            m.source.Play();
        }
        float total = Mathf.Max(reveal, musicIn);
        for (float t = 0f; t < total; t += Step())
        {
            background.color = new Color(1f, 1f, 1f, 1f - Smooth(Mathf.Clamp01(t / reveal)));
            float k = Smooth(Mathf.Clamp01(t / musicIn));
            foreach (var m in music)
                if (m.source != null) m.source.volume = m.volume * k;
            yield return null;
        }
        foreach (var m in music)
            if (m.source != null) m.source.volume = m.volume;

        Destroy(overlay);
    }

    private static List<(AudioSource, float)> FindMusic()
    {
        var list = new List<(AudioSource, float)>();
        foreach (AudioSource source in FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude))
            if (source.isPlaying && source.loop && !source.mute)
                list.Add((source, source.volume));
        return list;
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    /// <summary>
    /// Gekappter Zeitschritt: der erste Frame nach dem Laden dauert oft Hunderte
    /// Millisekunden und würde das Einblenden sonst halb verschlucken.
    /// </summary>
    private static float Step() => Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);

    private void OnDestroy()
    {
        if (gradient != null) Destroy(gradient);
        // Overlay noch da = Szene wurde mitten im Splash verlassen.
        if (overlay != null)
        {
            Destroy(overlay);
            IsShowing = false;
        }
    }
}
