using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Sicherheitsnetz fuer die laengeren Sprachen (ES, FR, RU): Die Oberflaechen
/// sind auf Deutsch und Englisch zugeschnitten - meist ein Label genau so breit
/// wie sein Knopf, ohne Umbruch. Waere ein einzeiliger Text breiter als sein
/// Feld, wird er hier verkleinert (hoechstens auf <see cref="MinScale"/>),
/// statt ueber den Rand zu laufen. Wird der Text wieder kuerzer, waechst er
/// zurueck auf seine eigentliche Groesse.
///
/// Deutsch und Englisch bleiben unberuehrt. Passt ein Text auch verkleinert
/// nicht, meldet die Konsole ihn (Editor und Development-Build) - dann sollte
/// die Uebersetzung kuerzer werden.
///
/// Angepasst wird nicht im Moment des Neuaufbaus (TMP meldet ihn mitten in der
/// Canvas-Aktualisierung, dort darf nichts neu als "dirty" markiert werden),
/// sondern direkt danach - schlimmstenfalls steht ein zu langer Text einen
/// einzigen Frame lang in voller Groesse da.
/// </summary>
public static class TextFit
{
    /// <summary>Kleiner wird ein Text nicht - darunter wird die Pixelschrift unleserlich.</summary>
    private const float MinScale = 0.7f;

    /// <summary>Luft links und rechts, damit nichts am Knopfrand klebt.</summary>
    private const float Padding = 1f;

    /// <summary>Schmalere Felder sind Platzhalter, die bewusst ueberlaufen.</summary>
    private const float MinBoxWidth = 12f;

    private sealed class Entry
    {
        public float BaseSize;
        public float Applied;
    }

    private static readonly Dictionary<TextMeshProUGUI, Entry> entries = new Dictionary<TextMeshProUGUI, Entry>();
    private static readonly HashSet<TextMeshProUGUI> pending = new HashSet<TextMeshProUGUI>();
    private static readonly List<TextMeshProUGUI> work = new List<TextMeshProUGUI>();
    private static readonly HashSet<string> warned = new HashSet<string>();
    private static bool active;
    private static bool installed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (installed) return;
        installed = true;

        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        Canvas.willRenderCanvases += Process;
        Loc.LanguageChanged += UpdateActive;
        UpdateActive();
    }

    private static void UpdateActive()
    {
        string lang = Loc.Language;
        active = lang == "es" || lang == "fr" || lang == "ru";
    }

    private static void OnTextChanged(Object obj)
    {
        if (obj is TextMeshProUGUI t && (active || entries.ContainsKey(t))) pending.Add(t);
    }

    private static void Process()
    {
        if (pending.Count == 0) return;

        work.Clear();
        work.AddRange(pending);
        pending.Clear();

        // Zerstoerte Texte, die nie wieder gemeldet werden, nicht ewig festhalten.
        if (entries.Count > 512)
        {
            var dead = new List<TextMeshProUGUI>();
            foreach (TextMeshProUGUI k in entries.Keys)
            {
                if (k == null) dead.Add(k);
            }
            foreach (TextMeshProUGUI k in dead) entries.Remove(k);
        }

        foreach (TextMeshProUGUI t in work)
        {
            if (t == null)
            {
                entries.Remove(t);
                continue;
            }
            if (!t.isActiveAndEnabled) continue;
            Fit(t);
        }
    }

    private static void Fit(TextMeshProUGUI t)
    {
        entries.TryGetValue(t, out Entry e);

        // Hat jemand anderes die Groesse inzwischen gesetzt, gilt seine als neue Basis.
        if (e != null && !Mathf.Approximately(t.fontSize, e.Applied))
        {
            entries.Remove(t);
            e = null;
        }

        // Eingabefelder scrollen beim Tippen - die duerfen nicht schrumpfen.
        bool candidate = active
                         && !t.enableAutoSizing
                         && t.textWrappingMode == TextWrappingModes.NoWrap
                         && (t.overflowMode == TextOverflowModes.Overflow ||
                             t.overflowMode == TextOverflowModes.Ellipsis)
                         && t.GetComponentInParent<TMP_InputField>() == null;

        if (!candidate)
        {
            if (e != null)
            {
                entries.Remove(t);
                t.fontSize = e.BaseSize;
            }
            return;
        }

        // preferredWidth statt der Mesh-Grenzen: bei "..."-Kuerzung zeigen die nur
        // den abgeschnittenen Teil. Die Raender stecken in beiden Werten.
        float box = t.rectTransform.rect.width - 2f * Padding;
        float current = t.fontSize;
        if (box < MinBoxWidth || current <= 0f) return;

        float width = t.preferredWidth;
        if (width <= 0f || float.IsNaN(width) || float.IsInfinity(width)) return;

        float baseSize = e != null ? e.BaseSize : current;
        float widthAtBase = width * baseSize / current;

        float target = widthAtBase > box ? baseSize * box / widthAtBase : baseSize;
        if (target < baseSize * MinScale)
        {
            target = baseSize * MinScale;
            Warn(t, widthAtBase, box);
        }
        target = Mathf.Floor(target * 10f) / 10f;

        if (target >= baseSize - 0.05f)
        {
            if (e != null)
            {
                entries.Remove(t);
                t.fontSize = baseSize;
            }
            return;
        }

        if (Mathf.Abs(target - current) < 0.05f) return;

        if (e == null)
        {
            e = new Entry { BaseSize = baseSize };
            entries[t] = e;
        }
        e.Applied = target;
        t.fontSize = target;
    }

    private static void Warn(TextMeshProUGUI t, float width, float box)
    {
        if (!Debug.isDebugBuild) return;

        string text = t.text;
        if (!warned.Add(Loc.Language + "|" + text)) return;

        Debug.LogWarning($"[TextFit] Text passt auch verkleinert nicht ({Loc.Language}): \"{text}\" " +
                         $"braucht {width:0} px, Feld {box:0} px - {PathOf(t.transform)}", t);
    }

    private static string PathOf(Transform tr)
    {
        string path = tr.name;
        for (Transform p = tr.parent; p != null; p = p.parent) path = p.name + "/" + path;
        return path;
    }
}
