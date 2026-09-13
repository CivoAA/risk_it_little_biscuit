using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kleiner Baukasten für Pixel-UI: flache Farbflächen, 1 px Rahmen, keine Sprites.
/// Bewusst ohne Grafik-Assets - alles was hier entsteht, sind eingefärbte Rechtecke
/// und Text. Dadurch hängt das Options-Panel an keiner einzigen Bilddatei.
///
/// Alle Maße sind ganze Zahlen und beziehen sich auf das 320x180-Raster des Menüs:
/// eine Canvas-Einheit ist genau ein Pixel. Sobald etwas auf einer halben Einheit
/// landet, verschmiert es beim Hochskalieren.
/// </summary>
public static class PixelUI
{
    // ---------- Palette ----------
    // Gezogen aus MenuButtonVisual, damit das Panel zum gebauten Menü passt.
    public static readonly Color Backdrop   = new Color(0.098f, 0.062f, 0.047f, 0.96f);
    public static readonly Color PanelFill  = new Color(0.157f, 0.078f, 0.059f);
    public static readonly Color Outline    = new Color(0.239f, 0.118f, 0.086f);
    public static readonly Color RowFill    = new Color(0.278f, 0.133f, 0.090f);
    public static readonly Color RowHover   = new Color(0.360f, 0.180f, 0.121f);
    public static readonly Color RowPressed = new Color(0.203f, 0.082f, 0.050f);
    public static readonly Color TextNormal = new Color(1f, 0.972f, 0.803f);
    public static readonly Color TextAccent = new Color(0.980f, 0.584f, 0.129f);
    public static readonly Color TextDim    = new Color(1f, 0.972f, 0.803f, 0.45f);
    public static readonly Color BarEmpty   = new Color(0.203f, 0.082f, 0.050f);

    /// <summary>
    /// Sucht die Pixel-Font des Projekts (ThaleahFat). Die liegt nicht in einem
    /// Resources-Ordner, ist aber geladen, sobald irgendein TMP-Text im Spiel sie
    /// benutzt - Game.unity und World Map.unity tun das. Wird sie nicht gefunden,
    /// fällt TMP auf seine Standardschrift zurück.
    /// </summary>
    public static TMP_FontAsset FindPixelFont()
    {
        foreach (TMP_FontAsset f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (f == null) continue;
            if (f.name == "PixelArtFont" || f.name.StartsWith("ThaleahFat")) return f;
        }
        return null;
    }

    public static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 anchoredPos,
                                     Vector2? anchor = null)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        Vector2 a = anchor ?? new Vector2(0.5f, 0.5f);
        rt.anchorMin = rt.anchorMax = rt.pivot = a;
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
        return rt;
    }

    public static Image Panel(string name, Transform parent, Vector2 size, Vector2 pos, Color color,
                              Vector2? anchor = null)
    {
        RectTransform rt = Rect(name, parent, size, pos, anchor);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>Fläche mit 1 px Rahmen: außen Rahmenfarbe, innen die Füllung.</summary>
    public static RectTransform Framed(string name, Transform parent, Vector2 size, Vector2 pos,
                                       Color fill, Color border, Vector2? anchor = null)
    {
        Image outer = Panel(name, parent, size, pos, border, anchor);
        Image inner = Panel("Fill", outer.transform, size - new Vector2(2f, 2f), Vector2.zero, fill);
        inner.rectTransform.anchorMin = inner.rectTransform.anchorMax = inner.rectTransform.pivot
            = new Vector2(0.5f, 0.5f);
        return outer.rectTransform;
    }

    public static TMP_Text Label(string name, Transform parent, Vector2 size, Vector2 pos, string text,
                                 float fontSize, Color color, TextAlignmentOptions align,
                                 TMP_FontAsset font, Vector2? anchor = null)
    {
        RectTransform rt = Rect(name, parent, size, pos, anchor);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = align;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        // Kein Auto-Sizing: das würde die Schrift auf krumme Größen ziehen und
        // bei einer Pixel-Font sofort unscharf aussehen.
        t.enableAutoSizing = false;
        return t;
    }

    /// <summary>Anklickbare Fläche mit Rahmen, Beschriftung und Hover-Feedback.</summary>
    public static Button TextButton(string name, Transform parent, Vector2 size, Vector2 pos, string text,
                                    float fontSize, TMP_FontAsset font, Vector2? anchor = null)
    {
        Image outer = Panel(name, parent, size, pos, Outline, anchor);
        outer.raycastTarget = true;

        Image fill = Panel("Fill", outer.transform, size - new Vector2(2f, 2f), Vector2.zero, RowFill);
        TMP_Text label = Label("Label", outer.transform, size, Vector2.zero, text, fontSize,
                               TextNormal, TextAlignmentOptions.Center, font);

        Button btn = outer.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = outer;

        PixelButtonFeedback fb = outer.gameObject.AddComponent<PixelButtonFeedback>();
        fb.Setup(fill, label);
        return btn;
    }
}
