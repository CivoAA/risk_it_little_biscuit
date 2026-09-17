using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Kleine Bausteine, die sich die Hub-Fenster teilen. Die bauen ihre Oberflaeche
/// zur Laufzeit auf, statt Prefabs zu pflegen - das hier ist der immer gleiche
/// Teil davon.
/// </summary>
public static class HubUiKit
{
    /// <summary>
    /// Die Hub-UIs rechnen in Pixeln der 320x180-Vorlage, mit Nullpunkt links
    /// OBEN - so wie man es in einem Bildbearbeitungsprogramm ausmisst.
    /// </summary>
    public static void Place(RectTransform rect, Rect pixels)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot     = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(pixels.width, pixels.height);
        rect.anchoredPosition = new Vector2(pixels.x, -pixels.y);
    }

    public static GameObject NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0) go.layer = uiLayer;
        go.transform.SetParent(parent, false);
        return go;
    }

    public static TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset font,
                                          float size, Color color,
                                          TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        var go = NewRect(name, parent);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        // Pixelschrift: ein Umbruch mitten im Wort sieht schlimmer aus als eine
        // Zeile, die einen Tick zu lang ist.
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    public static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = NewRect(name, parent);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        // Pixelart darf nicht in ein anderes Seitenverhaeltnis gequetscht werden
        if (sprite != null) img.preserveAspect = true;
        return img;
    }

    public static void Stretch(RectTransform r, float inset = 0f)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(inset, inset);
        r.offsetMax = new Vector2(-inset, -inset);
    }

    /// <summary>
    /// Ohne EventSystem kommt kein Klick und kein Mausrad an. Die Hub-Szene
    /// bringt keins mit, also legen wir bei Bedarf eins an.
    /// </summary>
    public static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        EventSystem existing = Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);
        if (existing != null) { existing.gameObject.SetActive(true); return; }

        var go = new GameObject("EventSystem (Hub)");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }
}
