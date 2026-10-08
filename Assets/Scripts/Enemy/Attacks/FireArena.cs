using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Der Feuerkreis der Glutwurz: ein Oval aus stehenden Flammen um Spieler und
/// Boss. Drinnen ist man sicher, im Feuer und dahinter brennt es - und zwar
/// immer staerker: 1, 2, 3, ... Schaden im halben Sekundentakt. Der Zaehler
/// gilt fuer die ganze Arena, wer rauslaeuft und wieder rein, macht bei 7, 8,
/// 9 weiter. Steht bis <see cref="Extinguish"/> (der Boss stirbt).
///
/// Die Flammen sind vorerst die Spitze des Flammenwurfs (<see cref="FlameBeam"/>),
/// hochkant gestellt und in mehreren Reihen hintereinander. Der Ring entzuendet
/// sich von dem Punkt aus, an dem der Boss gelandet ist, nach beiden Seiten
/// und schliesst sich gegenueber.
///
/// Alles entsteht zur Laufzeit, aus demselben Grund wie bei
/// <see cref="BossTelegraph"/>: kein Prefab, das parallel nachgezogen werden
/// muss.
/// </summary>
public class FireArena : MonoBehaviour
{
    // ------------------------------------------------------------- Aussehen

    /// <summary>So gross wie der Flammenwurf (der steht auch doppelt).</summary>
    private const float FlameScale = 2f;

    /// <summary>Abstand zweier Flammen entlang einer Reihe (Welt-Einheiten).</summary>
    private const float Spacing = 0.7f;

    private const int Rows = 3;

    /// <summary>So weit liegt jede Reihe hinter der vorigen.</summary>
    private const float RowGap = 0.75f;

    private const float Fps = 14f;

    /// <summary>So schnell waechst eine Flamme aus dem Boden.</summary>
    private const float RiseTime = 0.3f;

    /// <summary>So lange laeuft das Feuer um das Oval, bis es sich schliesst.</summary>
    private const float SpreadTime = 0.9f;

    private const float FadeTime = 0.6f;

    // -------------------------------------------------------------- Schaden

    private const float Tick = 0.5f;

    /// <summary>Wer das Feuer mit dem Rand beruehrt, brennt schon.</summary>
    private const float PlayerRadius = 0.3f;

    // ---------------------------------------------------------------- Zustand

    private struct Flame
    {
        public SpriteRenderer Renderer;
        public Vector2 Base;
        public float Delay;
        public int FrameOffset;
    }

    private readonly List<Flame> flames = new List<Flame>();
    private Sprite[] frames;
    private Vector2 center;
    private Vector2 halfAxes;
    private float time;
    private float fadeStart = -1f;
    private float flameHeight;

    /// <summary>Abstand Fuss -> Pivot der Flamme in Bild-Einheiten (vor der Skalierung).</summary>
    private float pivotFromFoot;

    private int stacks;
    private float tickCooldown;

    /// <summary>Wie oft es schon gebrannt hat - der naechste Tick macht Stacks + 1 Schaden.</summary>
    public int Stacks => stacks;

    // ------------------------------------------------------------------- Bau

    /// <param name="center">Mitte des Ovals.</param>
    /// <param name="halfAxes">Halbachsen des freien Innenraums.</param>
    /// <param name="origin">Von hier aus entzuendet sich der Ring (Landepunkt des Bosses).</param>
    /// <param name="flameFrames">Bildstreifen einer Flamme, Spitze zeigt nach +x.</param>
    public static FireArena Build(Vector2 center, Vector2 halfAxes, Vector2 origin,
                                  Sprite[] flameFrames, string sortingLayer, int sortingOrder)
    {
        var root = new GameObject("Feuerkreis");

        // Wie bei den Warnflaechen: sonst landet der Kreis im Hub und
        // ueberlebt das Entladen der Karte.
        Scene run = RunScene.Current;
        if (run.IsValid() && run.isLoaded) SceneManager.MoveGameObjectToScene(root, run);

        root.transform.position = center;
        FireArena arena = root.AddComponent<FireArena>();
        arena.Setup(center, halfAxes, origin, flameFrames, sortingLayer, sortingOrder);
        return arena;
    }

    /// <summary>Feuer aus: die Flammen sinken zusammen, danach verschwindet der Kreis.</summary>
    public void Extinguish()
    {
        if (fadeStart < 0f) fadeStart = time;
    }

    private void Setup(Vector2 c, Vector2 axes, Vector2 origin, Sprite[] flameFrames, string layer, int order)
    {
        center = c;
        halfAxes = new Vector2(Mathf.Max(1f, axes.x), Mathf.Max(1f, axes.y));
        frames = flameFrames;

        Sprite first = frames != null && frames.Length > 0 ? frames[0] : null;
        float ppu = first != null ? first.pixelsPerUnit : 32f;
        float px = first != null ? first.rect.width : 32f;
        flameHeight = px / ppu * FlameScale;
        pivotFromFoot = first != null ? first.pivot.x / ppu : 0.5f;
        float flameThickness = flameHeight * 0.6f;

        // Wo der Ring anfaengt zu brennen: der Winkel des Landepunkts
        Vector2 o = origin - center;
        float originAngle = Mathf.Atan2(o.y / halfAxes.y, o.x / halfAxes.x);

        for (int row = 0; row < Rows; row++)
        {
            Vector2 axesRow = halfAxes + Vector2.one * (row * RowGap);
            List<float> angles = EvenAngles(axesRow, Spacing, row % 2 == 1 ? 0.5f : 0f);

            foreach (float angle in angles)
            {
                float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                Vector2 onRing = new Vector2(axesRow.x * cos, axesRow.y * sin);
                Vector2 normal = new Vector2(cos / axesRow.x, sin / axesRow.y).normalized;

                // Die Flammen stehen hochkant. Damit ihre Innenkante ueberall
                // auf dem Oval liegt: an den Seiten um die halbe Dicke nach
                // aussen, unten um die ganze Hoehe nach unten (dort zeigt die
                // Spitze nach innen), oben steht der Fuss direkt auf der Linie.
                Vector2 basePos = onRing
                                  + normal * (flameThickness * 0.5f * Mathf.Abs(cos))
                                  + Vector2.down * (flameHeight * Mathf.Max(0f, -sin));

                float around = Mathf.Abs(Mathf.DeltaAngle(originAngle * Mathf.Rad2Deg, angle * Mathf.Rad2Deg)) / 180f;

                flames.Add(new Flame
                {
                    Renderer = NewFlame(row, layer, order),
                    Base = center + basePos,
                    Delay = around * SpreadTime + row * 0.06f + Random.Range(0f, 0.05f),
                    FrameOffset = Random.Range(0, 64),
                });
            }
        }

        Apply();
    }

    private SpriteRenderer NewFlame(int row, string layer, int order)
    {
        var go = new GameObject("Flamme");
        go.transform.SetParent(transform, false);
        // Spitze zeigt im Bild nach +x - hochkant gedreht zeigt sie nach oben
        go.transform.rotation = Quaternion.Euler(0f, 0f, 90f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = layer;
        sr.sortingOrder = order;
        // Nach dem Fuss sortieren: wer davor steht, steht davor
        sr.spriteSortPoint = SpriteSortPoint.Pivot;
        // Gespiegelt (nach der Drehung links/rechts), damit die Reihe nicht wie gestempelt aussieht
        sr.flipY = Random.value < 0.5f;
        // Hintere Reihen etwas dunkler - so liest sich die Tiefe
        float shade = 1f - row * 0.12f;
        sr.color = new Color(shade, shade, shade, 1f);
        sr.enabled = false;
        return sr;
    }

    /// <summary>
    /// Winkel, die auf dem Oval gleich weit auseinander liegen (nicht gleiche
    /// Winkelschritte - die wuerden die Flammen an den Seiten zusammendraengen).
    /// </summary>
    private static List<float> EvenAngles(Vector2 axes, float spacing, float shift)
    {
        const int Samples = 720;
        var cumulative = new float[Samples + 1];
        Vector2 prev = new Vector2(axes.x, 0f);
        for (int i = 1; i <= Samples; i++)
        {
            float a = i * Mathf.PI * 2f / Samples;
            Vector2 p = new Vector2(axes.x * Mathf.Cos(a), axes.y * Mathf.Sin(a));
            cumulative[i] = cumulative[i - 1] + Vector2.Distance(prev, p);
            prev = p;
        }

        float perimeter = cumulative[Samples];
        int count = Mathf.Max(8, Mathf.CeilToInt(perimeter / spacing));
        float step = perimeter / count;

        var angles = new List<float>(count);
        int j = 0;
        for (int k = 0; k < count; k++)
        {
            float target = (k + shift) * step;
            while (j < Samples && cumulative[j + 1] < target) j++;
            float span = cumulative[j + 1] - cumulative[j];
            float f = span > 0f ? (target - cumulative[j]) / span : 0f;
            angles.Add((j + f) * Mathf.PI * 2f / Samples);
        }
        return angles;
    }

    // ---------------------------------------------------------------- Laufzeit

    private void Update()
    {
        time += Time.deltaTime;
        Apply();

        if (fadeStart >= 0f)
        {
            if (time - fadeStart >= FadeTime) Destroy(gameObject);
            return;
        }

        Burn();
    }

    private void Burn()
    {
        tickCooldown -= Time.deltaTime;

        PlayerController player = PlayerController.Instance;
        if (player == null || !player.gameObject.activeSelf) return;
        if (!IsOutside(player.transform.position)) return;

        // Der Takt laeuft drinnen weiter: wer kurz rein und wieder raus geht,
        // wartet nicht, wer laenger drin war, brennt beim Rauslaufen sofort.
        if (tickCooldown > 0f) return;

        stacks++;
        tickCooldown = Tick;
        player.TakeDamage(stacks * RunDifficulty.DamageFactor);
    }

    /// <summary>Steht der Punkt im Feuer oder dahinter?</summary>
    public bool IsOutside(Vector2 point)
    {
        Vector2 d = point - center;
        float ax = Mathf.Max(0.5f, halfAxes.x - PlayerRadius);
        float ay = Mathf.Max(0.5f, halfAxes.y - PlayerRadius);
        float x = d.x / ax, y = d.y / ay;
        return x * x + y * y > 1f;
    }

    private void Apply()
    {
        int frame = Mathf.FloorToInt(time * Fps);
        float fade = fadeStart >= 0f ? 1f - Mathf.Clamp01((time - fadeStart) / FadeTime) : 1f;

        foreach (Flame f in flames)
        {
            float grow = Mathf.Clamp01((time - f.Delay) / RiseTime) * fade;
            SpriteRenderer sr = f.Renderer;
            sr.enabled = grow > 0f && frames != null && frames.Length > 0;
            if (!sr.enabled) continue;

            // Waechst vom Fuss aus nach oben: die lokale x-Achse zeigt nach der
            // Drehung nach oben, also wird nur sie gestaucht.
            sr.sprite = frames[(frame + f.FrameOffset) % frames.Length];
            sr.transform.localScale = new Vector3(FlameScale * grow, FlameScale, 1f);
            sr.transform.position = f.Base + Vector2.up * (pivotFromFoot * FlameScale * grow);
        }
    }
}
