using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Glatteis-Pfuetze der Eiswelt (Map_World5): wer drauf laeuft, rutscht.
/// Kein Hindernis und kein Physik-Collider - die Eisflaeche ist ein Polygon
/// (lokale Koordinaten, Bild von Tools/eis_props.py), gegen das der
/// <see cref="PlayerController"/> seine Fuesse prueft.
///
/// Auf dem Eis folgt die Geschwindigkeit der Eingabe nur langsam
/// (<see cref="Grip"/>), ohne Eingabe gleitet der Keks weiter und bremst kaum
/// (<see cref="Friction"/>) - dafuer ist er etwas schneller
/// (<see cref="SlideBoost"/>). Beim Rutschen stieben kleine Eissplitter.
///
/// Die Pfuetzen liegen als Props im Chunk und werden vom
/// <see cref="ChunkPropRandomizer"/> verschoben und ausgeblendet; ausgeblendete
/// melden sich ueber OnDisable ab.
/// </summary>
[DisallowMultipleComponent]
public class Glatteis : MonoBehaviour
{
    /// <summary>So schnell folgt die Geschwindigkeit der Eingabe (je Sekunde).</summary>
    public const float Grip = 1.8f;

    /// <summary>So schnell bremst der Keks ohne Eingabe ab (je Sekunde).</summary>
    public const float Friction = 0.55f;

    /// <summary>Auf dem Eis ist man etwas flotter unterwegs.</summary>
    public const float SlideBoost = 1.2f;

    [Tooltip("Umriss der Eisflaeche in lokalen Einheiten (setzt der EisBuilder).")]
    [SerializeField] private Vector2[] outline = new Vector2[0];

    private static readonly List<Glatteis> all = new List<Glatteis>();
    private Rect box;

    /// <summary>Steht der Spieler gerade auf Glatteis? (Stand des letzten FixedUpdate)</summary>
    public static bool PlayerOnIce { get; private set; }

    public void SetOutline(Vector2[] points)
    {
        outline = points ?? new Vector2[0];
        UpdateBox();
    }

    private void Awake() => UpdateBox();

    private void OnEnable()
    {
        if (!all.Contains(this)) all.Add(this);
    }

    private void OnDisable() => all.Remove(this);

    private void UpdateBox()
    {
        if (outline == null || outline.Length < 3) { box = new Rect(); return; }
        Vector2 min = outline[0], max = outline[0];
        foreach (Vector2 p in outline)
        {
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }
        box = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    /// <summary>Liegt der Weltpunkt auf dieser Eisflaeche?</summary>
    public bool Contains(Vector2 world)
    {
        if (outline == null || outline.Length < 3) return false;
        Vector2 p = transform.InverseTransformPoint(world);
        if (!box.Contains(p)) return false;

        // Strahl nach rechts, Kanten zaehlen
        bool inside = false;
        for (int i = 0, j = outline.Length - 1; i < outline.Length; j = i++)
        {
            Vector2 a = outline[i], b = outline[j];
            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }
        return inside;
    }

    public static bool IsOnIce(Vector2 world)
    {
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] != null && all[i].Contains(world)) return true;
        }
        return false;
    }

    /// <summary>
    /// Geschwindigkeit fuer diesen Physikschritt. Abseits vom Eis kommt
    /// <paramref name="wanted"/> unveraendert zurueck - ausserhalb der Eiswelt
    /// kostet das nur einen Blick in eine leere Liste.
    /// </summary>
    public static Vector2 Steer(Vector2 feet, Vector2 current, Vector2 wanted, float dt)
    {
        PlayerOnIce = all.Count > 0 && IsOnIce(feet);
        if (!PlayerOnIce) return wanted;

        bool steering = wanted.sqrMagnitude > 0.0001f;
        float rate = steering ? Grip : Friction;
        Vector2 v = Vector2.Lerp(current, wanted * SlideBoost, 1f - Mathf.Exp(-rate * dt));

        GlatteisSplitter.Emit(feet, v, dt);
        return v;
    }
}
