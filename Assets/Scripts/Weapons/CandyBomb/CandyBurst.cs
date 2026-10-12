using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Bonbon-Explosion der Candy Bomb und der Bomb-Saw-Evo, einmal abgespielt.
///
/// Die Lauf-Kamera ist pixelgenau (32 PPU) - eine skalierte Explosion haette
/// ungleich grosse Pixel. Darum liegt jede Groesse als eigener Streifen in
/// Resources (&lt;prefix&gt;&lt;Durchmesser in Pixeln&gt;), und <see cref="Spawn"/>
/// nimmt die Stufe, die dem Schadensradius am naechsten kommt.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class CandyBurst : MonoBehaviour
{
    public const string CandyPrefix = "Weapons/candy_burst_";
    public const string SawPrefix = "Weapons/saw_burst_";

    public static readonly int[] CandySizes = { 160, 208, 256, 320, 384, 448 };
    public static readonly int[] SawSizes = { 208, 256, 320, 384, 448 };

    private const float Fps = 18f;

    private static readonly Dictionary<string, Sprite[]> Cache = new Dictionary<string, Sprite[]>();

    private SpriteRenderer sr;
    private Sprite[] frames;
    private float age;

    /// <summary>
    /// Erzeugt die Explosion mit <paramref name="radius"/> Welteinheiten Radius.
    /// Gibt null zurueck, wenn die Bilder fehlen - dann bleibt der alte Effekt.
    /// </summary>
    public static GameObject Spawn(string prefix, int[] sizes, Vector3 position, float radius,
                                   int sortingLayerId, int sortingOrder)
    {
        float wantedPx = radius * 2f * PixelPool.Ppu;
        int best = sizes[0];
        for (int i = 1; i < sizes.Length; i++)
        {
            if (Mathf.Abs(sizes[i] - wantedPx) < Mathf.Abs(best - wantedPx)) best = sizes[i];
        }

        Sprite[] loaded = Load(prefix + best);
        if (loaded.Length == 0) return null;

        var go = new GameObject("CandyBurst");
        go.transform.position = PixelPool.Snap(position);
        // Weit ueber der groessten Stufe lieber skalieren als zu klein zeigen.
        float over = wantedPx / best;
        if (over > 1.15f) go.transform.localScale = new Vector3(over, over, 1f);

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = sortingOrder;
        renderer.sprite = loaded[0];

        var burst = go.AddComponent<CandyBurst>();
        burst.sr = renderer;
        burst.frames = loaded;

        Scene gameScene = RunScene.Current;
        if (gameScene.IsValid() && gameScene.isLoaded)
            SceneManager.MoveGameObjectToScene(go, gameScene);
        return go;
    }

    /// <summary>Laedt einen Streifen einmal und merkt ihn sich.</summary>
    public static Sprite[] Load(string path)
    {
        if (Cache.TryGetValue(path, out Sprite[] cached) && cached.Length > 0 && cached[0] != null) return cached;
        Sprite[] loaded = SpriteStrip.Load(path);
        Cache[path] = loaded;
        return loaded;
    }

    void Update()
    {
        age += Time.deltaTime;
        int f = (int)(age * Fps);
        if (f >= frames.Length)
        {
            Destroy(gameObject);
            return;
        }
        sr.sprite = frames[f];
    }
}
