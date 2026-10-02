using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Windschneise einer Hauptwelle des <see cref="TyphoonFan"/>. Reicht vom
/// Abschusspunkt bis zur Welle und waechst mit ihr mit. Ist die Welle weg,
/// bleibt die Schneise noch <see cref="TyphoonFan.TrailLinger"/> Sekunden
/// liegen und blendet zum Schluss aus.
///
/// Solange sie liegt, bremst sie jeden Gegner darin (<see cref="TyphoonFan.TrailSlow"/>,
/// kein Schaden) und gibt dem Spieler Rueckenwind, wenn er darin steht
/// (<see cref="TyphoonFan.HasTailwind"/>, ausgewertet im PlayerController).
///
/// Abspaltungen der Spaltwelle bekommen keine Schneise - die legt nur
/// <see cref="TyphoonFan"/> fuer seine Hauptwellen an.
/// </summary>
public class TyphoonTrail : MonoBehaviour
{
    /// <summary>So oft (Sekunden) wird gebremst und der Spieler geprueft.</summary>
    private const float TickInterval = 0.2f;

    /// <summary>Auf dieser letzten Strecke der Liegezeit blendet die Schneise aus.</summary>
    private const float FadeTime = 0.5f;

    /// <summary>Koerpermitte des Spielers ueber seinem Pivot (der liegt unter den Fuessen).</summary>
    private const float PlayerCenterOffset = 0.4f;

    private Transform wave;
    private Vector2 start;
    private Vector2 end;
    private float width;
    private float lingerLeft = TyphoonFan.TrailLinger;
    private float tick;

    private SpriteRenderer sr;
    private readonly List<Enemy> buffer = new List<Enemy>();

    /// <param name="wave">Die Welle, der die Schneise folgt.</param>
    /// <param name="width">Breite in Welteinheiten.</param>
    public static TyphoonTrail Create(Transform wave, Vector2 start, float width)
    {
        GameObject go = new GameObject("TyphoonTrail");
        go.transform.position = start;

        Scene gameScene = RunScene.Current;
        if (gameScene.IsValid() && gameScene.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(go, gameScene);
        }

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = TrailSprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        SaladFan.CopySorting(sr, -1);   // unter Spieler und Welle

        TyphoonTrail trail = go.AddComponent<TyphoonTrail>();
        trail.sr = sr;
        trail.wave = wave;
        trail.start = start;
        trail.end = start;
        trail.width = width;
        trail.UpdateShape();
        return trail;
    }

    void Update()
    {
        if (wave != null)
        {
            end = wave.position;
        }
        else
        {
            lingerLeft -= Time.deltaTime;
            if (lingerLeft <= 0f)
            {
                Destroy(gameObject);
                return;
            }
        }

        UpdateShape();

        tick -= Time.deltaTime;
        if (tick <= 0f)
        {
            tick = TickInterval;
            Blow();
        }
    }

    /// <summary>Gegner in der Schneise bremsen, Spieler darin bekommt Rueckenwind.</summary>
    private void Blow()
    {
        if ((end - start).sqrMagnitude < 0.0025f) return;

        float radius = width * 0.5f;
        float hold = TickInterval + 0.1f;   // reicht bis zum naechsten Tick

        OverlapDamage.SweepEnemies(start, end, radius, buffer);
        for (int i = 0; i < buffer.Count; i++)
        {
            if (buffer[i] != null) buffer[i].ApplySlow(TyphoonFan.TrailSlow, hold);
        }

        if (PlayerController.Instance == null) return;

        Vector2 player = (Vector2)PlayerController.Instance.transform.position + Vector2.up * PlayerCenterOffset;
        if (DistanceToSegment(player, start, end) <= radius)
        {
            TyphoonFan.TailwindUntil = Mathf.Max(TyphoonFan.TailwindUntil, Time.time + hold);
        }
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    private void UpdateShape()
    {
        Vector2 delta = end - start;
        float length = delta.magnitude;

        transform.position = (start + end) * 0.5f;
        if (length > 0.0001f)
        {
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        // Laenge ueber die Kachelung (ein Muster pro Tile), Breite ueber die Skalierung.
        transform.localScale = new Vector3(1f, width, 1f);
        sr.size = new Vector2(Mathf.Max(length, 0.01f), 1f);

        Color c = sr.color;
        c.a = wave != null ? 1f : Mathf.Clamp01(lingerLeft / FadeTime);
        sr.color = c;
    }

    private static Sprite trailSprite;

    /// <summary>
    /// Platzhalter, bis es ein gezeichnetes Sprite gibt: 32x32 bei 32 PPU,
    /// also ein Tile, das sich in Laengsrichtung nahtlos kachelt. Blasses Blau
    /// mit hellen, gestrichelten Windlinien, zu den Raendern durchsichtiger.
    /// </summary>
    private static Sprite TrailSprite
    {
        get
        {
            if (trailSprite != null) return trailSprite;

            const int n = 32;
            Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            Color32 haze = new Color32(150, 205, 250, 0);
            Color32 streak = new Color32(225, 242, 255, 0);
            int[] streakRows = { 7, 12, 16, 20, 25 };
            int[] streakShift = { 0, 9, 4, 13, 6 };

            Color32[] px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                // 0 in der Mitte, 1 am Rand.
                float edge = Mathf.Abs(y + 0.5f - n * 0.5f) / (n * 0.5f);
                float falloff = 1f - edge * edge;

                int row = System.Array.IndexOf(streakRows, y);

                for (int x = 0; x < n; x++)
                {
                    Color32 col = haze;
                    col.a = (byte)Mathf.RoundToInt(45f * falloff);

                    // Striche mit Periode 16 - passt zweimal in die Kachel, also nahtlos.
                    if (row >= 0 && (x + streakShift[row]) % 16 < 9)
                    {
                        col = streak;
                        col.a = (byte)Mathf.RoundToInt(150f * falloff);
                    }

                    px[y * n + x] = col;
                }
            }

            tex.SetPixels32(px);
            tex.Apply();

            trailSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n,
                                        0, SpriteMeshType.FullRect);
            return trailSprite;
        }
    }
}
