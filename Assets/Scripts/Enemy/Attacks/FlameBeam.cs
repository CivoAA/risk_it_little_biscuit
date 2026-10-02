using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Flammenwurf des Baum-Bosses aus drei Teilen, die nie gestreckt werden:
/// Ansatz (am Maul) | Mittelstueck (so oft wie noetig aneinandergereiht) | Spitze.
/// Die Bilder zeichnet Tools/baumboss_feuer.py (je 8 Frames, 32x32).
///
/// Das Objekt sitzt am Maul und zeigt mit +x in Feuerrichtung - zum Zielen
/// einfach den Transform drehen. Laenge ueber <see cref="Length"/> in Welt-
/// Einheiten (inklusive Ansatz und Spitze, Skalierung eingerechnet); der Strahl
/// waechst beim Einschalten in <c>growTime</c> darauf hin und mit
/// <see cref="Stop"/> wieder zurueck.
///
/// Die Laenge rastet auf 4 px ein: so weit stroemt das Feuer pro Frame. Jedes
/// Teil, das bei x Pixeln sitzt, zeigt Frame - x/4 - damit passt das Muster an
/// jeder 4-px-Stelle pixelgenau, und das letzte Mittelstueck darf das
/// vorletzte einfach ueberlappen. Gezeichnet wird Mitte, darueber Ansatz,
/// darueber Spitze.
/// </summary>
public class FlameBeam : MonoBehaviour
{
    [SerializeField] private Sprite[] startFrames;
    [SerializeField] private Sprite[] midFrames;
    [SerializeField] private Sprite[] endFrames;
    [SerializeField] private float fps = 14f;

    [Tooltip("Gesamtlaenge in Welt-Einheiten.")]
    [SerializeField] private float length = 8f;

    [Tooltip("Sekunden, bis der Strahl nach dem Einschalten voll ausgefahren ist.")]
    [SerializeField] private float growTime = 0.1f;

    [SerializeField] private string sortingLayerName = "Objects";
    [SerializeField] private int sortingOrder = 2;

    private const int TilePx = 32;
    private const int FlowPx = 4;

    /// <summary>
    /// Halbe Dicke des Feuerkerns in Bildpixeln (der Strahl ist ~19 px dick,
    /// die Zungen am Rand zaehlen nicht). Trifft lieber etwas zu knapp als zu
    /// grosszuegig: wer sichtbar daneben steht, darf nicht brennen.
    /// </summary>
    private const float CorePx = 8f;

    private SpriteRenderer start, end;
    private readonly List<SpriteRenderer> mids = new List<SpriteRenderer>();
    private float shown;
    private float time;
    private bool stopping;

    /// <summary>Ziellaenge in Welt-Einheiten.</summary>
    public float Length
    {
        get => length;
        set => length = Mathf.Max(0f, value);
    }

    /// <summary>Aktuell sichtbare Laenge in Welt-Einheiten.</summary>
    public float CurrentLength => shown;

    /// <summary>Sekunden bis zur vollen Laenge (und zurueck).</summary>
    public float GrowTime
    {
        get => growTime;
        set => growTime = Mathf.Max(0.01f, value);
    }

    /// <summary>Halbe Dicke des brennenden Kerns in Welt-Einheiten.</summary>
    public float HalfWidth => CorePx / Ppu * Scale;

    /// <summary>Faehrt gerade zurueck (nach <see cref="Stop"/>).</summary>
    public bool Stopping => stopping;

    /// <summary>Faehrt den Strahl zurueck und schaltet das Objekt danach ab.</summary>
    public void Stop() => stopping = true;

    /// <summary>Vor oder hinter den Boss legen (schiesst er nach oben, liegt der Strahl dahinter).</summary>
    public void SetSorting(string layer, int order)
    {
        sortingLayerName = layer;
        sortingOrder = order;
        if (start == null) return;
        foreach (SpriteRenderer sr in mids) Sort(sr, 0);
        Sort(start, 1);
        Sort(end, 2);
    }

    /// <summary>
    /// Trifft der Strahl einen Kreis um <paramref name="point"/>? Geprueft
    /// wird nur der Teil, der schon sichtbar ist.
    /// </summary>
    public bool Hits(Vector2 point, float radius)
    {
        if (!isActiveAndEnabled || shown <= 0f) return false;

        Vector2 from = transform.position;
        Vector2 dir = transform.right;
        float along = Mathf.Clamp(Vector2.Dot(point - from, dir), 0f, shown);
        return Vector2.Distance(point, from + dir * along) <= HalfWidth + radius;
    }

    void Awake()
    {
        start = MakePart("Start", 1);
        end = MakePart("End", 2);
    }

    void OnEnable()
    {
        shown = 0f;
        time = 0f;
        stopping = false;
        Apply();
    }

    void Update()
    {
        time += Time.deltaTime;
        float target = stopping ? 0f : length;
        float speed = Mathf.Max(length, 1f) / Mathf.Max(0.01f, growTime);
        shown = Mathf.MoveTowards(shown, target, speed * Time.deltaTime);
        Apply();
        if (stopping && shown <= 0f)
            gameObject.SetActive(false);
    }

    private float Ppu
    {
        get
        {
            Sprite s = startFrames != null && startFrames.Length > 0 ? startFrames[0] : null;
            return s != null ? s.pixelsPerUnit : 32f;
        }
    }

    private float Scale => Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));

    private SpriteRenderer MakePart(string partName, int layerOffset)
    {
        var go = new GameObject(partName);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        Sort(sr, layerOffset);
        return sr;
    }

    private void Sort(SpriteRenderer sr, int offset)
    {
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = sortingOrder + offset;
    }

    private void Apply()
    {
        int frame = Mathf.FloorToInt(time * fps);
        float ppu = Ppu;

        // Laenge in Bildpixeln, auf 4 px gerastert
        int px = Mathf.FloorToInt(shown / Scale * ppu / FlowPx) * FlowPx;
        bool visible = px > 0;
        start.enabled = visible;
        end.enabled = visible && px >= TilePx;
        int endPos = Mathf.Max(0, px - TilePx);

        // Mittelstuecke ab x = 32 bis vor die Spitze, das letzte buendig davor
        int count = visible && endPos > TilePx ? (endPos - 1) / TilePx : 0;
        while (mids.Count < count)
            mids.Add(MakePart("Mid" + mids.Count, 0));
        for (int i = 0; i < mids.Count; i++)
        {
            SpriteRenderer sr = mids[i];
            sr.enabled = i < count;
            if (!sr.enabled) continue;
            int pos = Mathf.Max(TilePx, Mathf.Min(TilePx * (i + 1), endPos - TilePx));
            Place(sr, midFrames, frame, pos, ppu);
        }
        if (!visible) return;
        Place(start, startFrames, frame, 0, ppu);
        if (end.enabled) Place(end, endFrames, frame, endPos, ppu);
    }

    private static void Place(SpriteRenderer sr, Sprite[] frames, int frame, int posPx, float ppu)
    {
        sr.sprite = Pick(frames, frame - posPx / FlowPx);
        sr.transform.localPosition = new Vector3(posPx / ppu, 0f, 0f);
    }

    private static Sprite Pick(Sprite[] frames, int frame)
    {
        if (frames == null || frames.Length == 0) return null;
        int n = frames.Length;
        return frames[((frame % n) + n) % n];
    }

#if UNITY_EDITOR
    /// <summary>Nur fuer das Bau-Skript: Bilder eintragen.</summary>
    public void EditorBind(Sprite[] startSprites, Sprite[] midSprites, Sprite[] endSprites)
    {
        startFrames = startSprites;
        midFrames = midSprites;
        endFrames = endSprites;
    }
#endif
}
