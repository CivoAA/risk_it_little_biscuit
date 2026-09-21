using UnityEngine;

/// <summary>
/// Eine einzelne Warnflaeche. Gebaut wird sie ueber <see cref="BossTelegraph"/>,
/// von Hand haengt man sie nirgends dran.
///
/// Der Aufrufer treibt sie selbst: er ruft jeden Frame <see cref="SetProgress"/>
/// mit seinem eigenen Fortschritt auf und am Ende <see cref="Impact"/> oder
/// <see cref="Cancel"/>. Die Markierung zaehlt bewusst KEINE eigene Zeit mit -
/// sonst haetten Attacke und Anzeige zwei Uhren, die auseinanderlaufen, und
/// genau daran erkennt man unfaire Bosse.
/// </summary>
[DisallowMultipleComponent]
public class BossTelegraphMarker : MonoBehaviour
{
    private SpriteRenderer area;
    private SpriteRenderer fill;
    private Color areaColor;
    private Color fillColor;

    private Vector3 fillFrom = Vector3.zero;
    private Vector3 fillTo = Vector3.one;
    private float progress;

    private bool fading;
    private float fadeLeft;
    private float fadeTime;
    private Color fadeAreaFrom;
    private Color fadeFillFrom;

    internal void Bind(SpriteRenderer area, SpriteRenderer fill, Color areaColor, Color fillColor)
    {
        this.area = area;
        this.fill = fill;
        this.areaColor = areaColor;
        this.fillColor = fillColor;
    }

    // ----------------------------------------------------------------- Form

    /// <summary>
    /// Legt die Bahn neu. Darf waehrend der Vorwarnung wiederholt werden,
    /// solange der Boss noch nachzielt - danach nicht mehr, sonst waere die
    /// rote Bahn eine Luege.
    /// </summary>
    public void Aim(Vector2 from, Vector2 to, float width)
    {
        Vector2 delta = to - from;
        float length = delta.magnitude;
        if (length < 0.01f)
        {
            delta = Vector2.right;
            length = 0.01f;
        }

        transform.position = from;
        transform.rotation = Quaternion.FromToRotation(Vector3.right, (Vector3)delta.normalized);

        if (area != null) area.transform.localScale = new Vector3(length, width, 1f);

        // Die Fuellung ist von Anfang an so hoch wie die Bahn und waechst nur
        // in die Laenge: sie ist die Uhr, nicht der Bereich.
        fillFrom = new Vector3(0f, width * 0.88f, 1f);
        fillTo = new Vector3(length, width * 0.88f, 1f);
        SetProgress(progress);
    }

    /// <summary>Legt die Zone neu (Mittelpunkt und Weltradius).</summary>
    public void Spread(Vector2 center, float radius)
    {
        transform.position = center;
        transform.rotation = Quaternion.identity;

        if (area != null) area.transform.localScale = new Vector3(radius, radius, 1f);

        fillFrom = Vector3.zero;
        fillTo = new Vector3(radius, radius, 1f);
        SetProgress(progress);
    }

    public void MoveTo(Vector2 center)
    {
        transform.position = center;
    }

    // ------------------------------------------------------------ Ablaufende

    /// <summary>0 = gerade erschienen, 1 = schlaegt jetzt ein.</summary>
    public void SetProgress(float t)
    {
        if (fading) return;

        progress = Mathf.Clamp01(t);
        if (fill == null) return;

        fill.transform.localScale = Vector3.Lerp(fillFrom, fillTo, progress);

        // Je naeher der Einschlag, desto kraeftiger. Die Groesse allein liest
        // man im Gewuehl zu spaet - die Farbe zieht den Blick.
        Color color = fillColor;
        color.a = Mathf.Lerp(fillColor.a, 0.9f, progress * progress);
        fill.color = color;
    }

    /// <summary>Es schlaegt ein: kurz aufblitzen und dann weg.</summary>
    public void Impact(float fade = 0.22f)
    {
        SetProgress(1f);

        if (area != null) area.color = BossTelegraph.ImpactColor;
        if (fill != null) fill.color = BossTelegraph.ImpactColor;

        StartFade(fade);
    }

    /// <summary>
    /// Doch nichts: blass ausblenden. Braucht der Boss, wenn er mitten in der
    /// Vorwarnung stirbt - eine liegengebliebene rote Flaeche, in der nie
    /// etwas passiert, macht alle folgenden Warnungen unglaubwuerdig.
    /// </summary>
    public void Cancel(float fade = 0.12f)
    {
        StartFade(fade);
    }

    private void StartFade(float seconds)
    {
        if (fading) return;

        fading = true;
        fadeTime = Mathf.Max(0.01f, seconds);
        fadeLeft = fadeTime;
        fadeAreaFrom = area != null ? area.color : areaColor;
        fadeFillFrom = fill != null ? fill.color : fillColor;
    }

    private void Update()
    {
        if (!fading) return;

        fadeLeft -= Time.deltaTime;
        float k = Mathf.Clamp01(fadeLeft / fadeTime);

        if (area != null) area.color = WithAlpha(fadeAreaFrom, fadeAreaFrom.a * k);
        if (fill != null) fill.color = WithAlpha(fadeFillFrom, fadeFillFrom.a * k);

        if (fadeLeft <= 0f) Destroy(gameObject);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
