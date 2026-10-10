using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Eine Lache in Pixelart, die mit Level und AOE waechst, OHNE skaliert zu
/// werden: die Lauf-Kamera ist pixelgenau (32 PPU), jede localScale != 1
/// macht ungleich grosse Pixel. Stattdessen liegt jede Groesse als eigenes
/// Bild in Resources (&lt;prefix&gt;&lt;Pixel&gt;), und <see cref="Begin"/> nimmt die
/// naechstpassende Stufe. Gezeichnet von Tools/kaffeepool.py und
/// Tools/marmelade.py.
///
/// Jeder Streifen hat drei Abschnitte: Auftauchen (einmal), Schleife,
/// Verschwinden (laeuft zum Lebensende, siehe <see cref="SetLifeLeft"/>).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PixelPool : MonoBehaviour
{
    public const float Ppu = 32f;

    [Tooltip("Resources-Pfad ohne Groesse, z. B. Weapons/coffee_pool_")]
    [SerializeField] private string resourcePrefix = "Weapons/coffee_pool_";

    [Tooltip("Vorhandene Groessenstufen in Pixeln (Breite bzw. Durchmesser).")]
    [SerializeField] private int[] sizes = { 160, 192, 224, 256, 288, 320, 352, 384, 416 };

    [SerializeField] private int appearFrames = 3;
    [SerializeField] private int loopFrames = 6;
    [SerializeField] private int vanishFrames = 3;
    [SerializeField] private float appearFps = 18f;
    [SerializeField] private float loopFps = 8f;
    [SerializeField] private float vanishFps = 14f;

    [Tooltip("Sekunden, in denen am Ende zusaetzlich ausgeblendet wird.")]
    [SerializeField] private float fadeOut = 0.12f;

    private static readonly Dictionary<string, Sprite[]> Cache = new Dictionary<string, Sprite[]>();

    private SpriteRenderer sr;
    private Sprite[] frames;
    private Color baseColor;
    private float age;
    private float loopStart;
    private float lifeLeft = float.PositiveInfinity;

    /// <summary>Gewaehlte Stufe in Pixeln (0 = noch nicht gestartet).</summary>
    public int SizePx { get; private set; }

    /// <summary>Gewaehlte Stufe in Welteinheiten.</summary>
    public float SizeUnits => SizePx / Ppu;

    /// <summary>Dauer des Verschwindens - so lange vor Lebensende beginnt es.</summary>
    public float VanishTime => vanishFrames / Mathf.Max(0.01f, vanishFps);

    /// <summary>
    /// Waehlt die Stufe, die <paramref name="wantedPx"/> am naechsten kommt,
    /// und setzt Massstab 1. Gibt false zurueck, wenn keine Bilder da sind -
    /// dann bleibt das Prefab-Sprite stehen.
    /// </summary>
    public bool Begin(float wantedPx)
    {
        sr = GetComponent<SpriteRenderer>();
        baseColor = sr.color;

        int best = sizes[0];
        for (int i = 1; i < sizes.Length; i++)
        {
            if (Mathf.Abs(sizes[i] - wantedPx) < Mathf.Abs(best - wantedPx)) best = sizes[i];
        }

        frames = Load(resourcePrefix + best);
        if (frames.Length < appearFrames + loopFrames + vanishFrames)
        {
            frames = null;
            return false;
        }

        SizePx = best;
        // Auch unter einem skalierten Elternteil genau 1 Texel = 1 Bildpunkt.
        Vector3 parent = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(1f / Safe(parent.x), 1f / Safe(parent.y), 1f);
        loopStart = Random.Range(0, loopFrames) / Mathf.Max(0.01f, loopFps);
        Apply();
        return true;
    }

    /// <summary>Restlebenszeit in Sekunden - darauf laeuft das Verschwinden hin.</summary>
    public void SetLifeLeft(float seconds) => lifeLeft = seconds;

    /// <summary>Haelt die Mitte auf dem Pixelraster (fuer Lachen, die liegen bleiben).</summary>
    public static Vector3 Snap(Vector3 p)
    {
        return new Vector3(Mathf.Round(p.x * Ppu) / Ppu, Mathf.Round(p.y * Ppu) / Ppu, p.z);
    }

    void Update()
    {
        if (frames == null) return;
        age += Time.deltaTime;
        Apply();
    }

    private void Apply()
    {
        int index;
        float appearTime = appearFrames / Mathf.Max(0.01f, appearFps);

        if (lifeLeft < VanishTime)
        {
            int k = Mathf.FloorToInt((VanishTime - lifeLeft) * vanishFps);
            index = appearFrames + loopFrames + Mathf.Clamp(k, 0, vanishFrames - 1);
        }
        else if (age < appearTime)
        {
            index = Mathf.Clamp(Mathf.FloorToInt(age * appearFps), 0, appearFrames - 1);
        }
        else
        {
            int k = Mathf.FloorToInt((age - appearTime + loopStart) * loopFps);
            index = appearFrames + k % loopFrames;
        }

        sr.sprite = frames[index];

        Color c = baseColor;
        if (fadeOut > 0f) c.a *= Mathf.Clamp01(lifeLeft / fadeOut);
        sr.color = c;
    }

    private static float Safe(float v) => Mathf.Abs(v) < 0.0001f ? 1f : v;

    private static Sprite[] Load(string path)
    {
        if (Cache.TryGetValue(path, out Sprite[] cached) && cached.Length > 0 && cached[0] != null) return cached;
        Sprite[] loaded = SpriteStrip.Load(path);
        Cache[path] = loaded;
        return loaded;
    }
}
