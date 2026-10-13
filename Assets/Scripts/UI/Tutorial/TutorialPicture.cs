using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Eine Bildtafel im <see cref="TutorialPanel"/>: 136x112 Seitenpixel, Ursprung
/// oben links, gebaut aus echten Spielgrafiken (Portraets, Werkbank-Kacheln,
/// Waffensymbole, Skill-Formen). Was die Tafel vorfuehrt, laeuft als Schleife:
/// jede Seite haengt sich mit <see cref="Tick"/> an die Zeit, ein Zeiger
/// (<see cref="Cursor"/>) zeigt, wohin man klickt.
///
/// Regeln wie ueberall in UI 2.0: Sprites nur in ganzen Vielfachen ihrer Textur,
/// Positionen auf ganzen Pixeln (alles laeuft ueber <see cref="OptionsKit.Move"/>).
/// </summary>
public sealed class TutorialPicture : System.IDisposable
{
    public const int W = 136, H = 112;

    public readonly RectTransform Root;
    public readonly TMP_FontAsset Font;

    /// <summary>Laenge der Schleife in Sekunden. 0 = laeuft einfach weiter.</summary>
    public float Loop = 6f;

    private readonly List<System.Action<float>> ticks = new List<System.Action<float>>();
    private readonly List<System.IDisposable> owned = new List<System.IDisposable>();

    public TutorialPicture(RectTransform root, TMP_FontAsset font)
    {
        Root = root;
        Font = font;
    }

    public void Dispose()
    {
        foreach (System.IDisposable d in owned) d.Dispose();
        owned.Clear();
        ticks.Clear();
    }

    public void Own(System.IDisposable d) => owned.Add(d);

    public void Tick(System.Action<float> action) => ticks.Add(action);

    /// <summary>Spielt die Tafel bei <paramref name="seconds"/> seit dem Aufschlagen der Seite.</summary>
    public void Run(float seconds)
    {
        float t = Loop > 0f ? seconds % Loop : seconds;
        for (int i = 0; i < ticks.Count; i++) ticks[i](t);
    }

    // ==================================================================
    //  Bausteine
    // ==================================================================

    public Image Img(float x, float y, float w, float h, Sprite s, Color c, bool sliced = false)
    {
        Image img = OptionsKit.Img("Pic", Root, x, y, w, h, s, c, sliced);
        if (s == null) img.enabled = false;
        return img;
    }

    /// <summary>Ein Sprite in seiner eigenen Groesse mal <paramref name="scale"/>.</summary>
    public Image Sprite(float x, float y, Sprite s, int scale = 1)
    {
        float w = s != null ? s.rect.width * scale : 0f;
        float h = s != null ? s.rect.height * scale : 0f;
        return Img(x, y, w, h, s, Color.white);
    }

    public TextMeshProUGUI Text(float x, float y, float w, string text, Color c,
                                TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        TextMeshProUGUI t = OptionsKit.Label("PicText", Root, x, y, w, 13, text, Font, OptionsKit.SizeText, c, align);
        t.overflowMode = TextOverflowModes.Ellipsis;
        return t;
    }

    /// <summary>Text mit 1-px-Schatten - fuer Beschriftungen auf bunten Flaechen.</summary>
    public TextMeshProUGUI ShadowText(float x, float y, float w, string text, Color c,
                                      TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        return OptionsKit.ShadowLabel("PicText", Root, x, y, w, 13, text, Font, OptionsKit.SizeText, c, align);
    }

    /// <summary>Ein Knopf, der nur so aussieht - die Tafel ist zum Zuschauen da.</summary>
    public FakeButton Button(float x, float y, float w, float h, string text, GameHudSkin.ButtonLook look,
                             Sprite icon = null)
    {
        var b = new FakeButton();
        b.Bg = Img(x, y, w, h, GameHudSkin.Button(look), Color.white, true);

        float textX = x, textW = w;
        if (icon != null)
        {
            float iw = icon.rect.width, ih = icon.rect.height;
            bool textless = string.IsNullOrEmpty(text);
            float left = textless ? Mathf.Round((w - iw) / 2f) : 5f;
            b.Icon = Img(x + left, y + Mathf.Round((h - 1f - ih) / 2f), iw, ih, icon, Color.white);
            if (!textless) { textX = x + left + iw + 2f; textW = w - left - iw - 6f; }
        }

        b.Label = OptionsKit.Label("PicBtn", Root, textX, y, textW, h - 1, text ?? "", Font, OptionsKit.SizeText,
                                   GameHudSkin.Cream, TextAlignmentOptions.Center);
        b.LabelHome = b.Label.rectTransform.anchoredPosition;
        b.SetLook(look);
        return b;
    }

    /// <summary>Gemaltes Portraet aus der Keksdose (42x42), animiert wie dort.</summary>
    public Image Portrait(float x, float y, int character)
    {
        Sprite[] frames = CharacterLooks.PortraitFor(character);
        if (frames == null || frames.Length == 0) frames = CharacterLooks.PortraitFor(0);

        Sprite first = frames != null && frames.Length > 0 ? frames[0] : null;
        Image img = Sprite(x, y, first);
        if (frames != null && frames.Length > 1)
        {
            Sprite[] f = frames;
            Tick(t => img.sprite = f[Mathf.FloorToInt(Time.unscaledTime * 6f) % f.Length]);
        }
        return img;
    }

    /// <summary>Kachel aus der Werkbank (Resources/Workbench/ui), ganzzahlig vergroessert.</summary>
    public Image Workbench(float x, float y, string sprite, int scale = 2)
    {
        return Sprite(x, y, WorkbenchSprite(sprite), scale);
    }

    public static Sprite WorkbenchSprite(string name) => Resources.Load<Sprite>("Workbench/ui/" + name);

    /// <summary>
    /// Funkenregen bei <paramref name="at"/> Sekunden: vier Funken fliegen aus
    /// der Mitte nach aussen und verloeschen.
    /// </summary>
    public void Burst(float at, float cx, float cy, float length = 0.5f)
    {
        var sparks = new Image[4];
        Vector2[] dirs = { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) };
        for (int i = 0; i < sparks.Length; i++)
        {
            sparks[i] = Img(cx - 3, cy - 3, 6, 6, GameHudSkin.Sparkle, Color.white);
            sparks[i].enabled = false;
        }

        Tick(t =>
        {
            float age = t - at;
            bool on = age >= 0f && age < length;
            for (int i = 0; i < sparks.Length; i++)
            {
                sparks[i].enabled = on;
                if (!on) continue;
                float d = Mathf.Round(2f + age / length * 9f);
                Place(sparks[i], cx - 3 + dirs[i].x * d, cy - 3 + dirs[i].y * d);
            }
        });
    }

    // ==================================================================
    //  Zeiger
    // ==================================================================

    public struct Key
    {
        public float T, X, Y;
        public bool Click;
    }

    /// <summary>Zeiger steht bei (x, y) zur Zeit t; ab dort gleitet er zum naechsten Punkt.</summary>
    public static Key At(float t, float x, float y) => new Key { T = t, X = x, Y = y };

    /// <summary>Wie <see cref="At"/>, mit Klick bei Ankunft.</summary>
    public static Key ClickAt(float t, float x, float y) => new Key { T = t, X = x, Y = y, Click = true };

    /// <summary>
    /// Der Zeiger. Als LETZTES anlegen, sonst liegt er unter den Kacheln.
    /// Die Spitze landet genau auf den angegebenen Punkten.
    /// </summary>
    public void Cursor(params Key[] keys)
    {
        if (keys == null || keys.Length == 0) return;

        Sprite up = GameHudSkin.Pointer, down = GameHudSkin.PointerDown;
        Image ring = Img(0, 0, 9, 9, GameHudSkin.Ring, OptionsKit.WithAlpha(GameHudSkin.GoldLight, 0.9f), true);
        ring.enabled = false;
        Image cursor = Sprite(0, 0, up);

        Tick(t =>
        {
            int k = 0;
            while (k + 1 < keys.Length && keys[k + 1].T <= t) k++;

            Key a = keys[k];
            Vector2 pos = new Vector2(a.X, a.Y);

            // Zwischen zwei Punkten: erst kurz stehen (Klick), dann gleiten.
            if (k + 1 < keys.Length)
            {
                Key b = keys[k + 1];
                float start = a.T + (a.Click ? 0.25f : 0.05f);
                float p = Ease(t, start, b.T);
                pos = Vector2.Lerp(pos, new Vector2(b.X, b.Y), p);
            }

            float since = t - a.T;
            bool pressed = a.Click && since >= 0f && since < 0.18f;
            cursor.sprite = pressed ? down : up;
            Place(cursor, pos.x - 1, pos.y - 1 + (pressed ? 1 : 0));

            bool showRing = a.Click && since >= 0f && since < 0.3f;
            ring.enabled = showRing;
            if (showRing)
            {
                float r = Mathf.Round(9f + since / 0.3f * 8f);
                if (((int)r & 1) == 0) r += 1f;
                OptionsKit.Move(ring.rectTransform, Mathf.Round(pos.x - r / 2f), Mathf.Round(pos.y - r / 2f), r, r);
                ring.color = OptionsKit.WithAlpha(GameHudSkin.GoldLight, 0.9f * (1f - since / 0.3f));
            }
        });
    }

    // ==================================================================
    //  Werkzeug
    // ==================================================================

    /// <summary>0 vor <paramref name="a"/>, 1 nach <paramref name="b"/>, dazwischen weich.</summary>
    public static float Ease(float t, float a, float b)
    {
        if (b <= a) return t >= b ? 1f : 0f;
        float p = Mathf.Clamp01((t - a) / (b - a));
        return p * p * (3f - 2f * p);
    }

    /// <summary>Verschiebt ein Element, Groesse bleibt - immer auf ganze Pixel.</summary>
    public static void Place(Graphic g, float x, float y)
    {
        RectTransform rt = g.rectTransform;
        OptionsKit.Move(rt, x, y, rt.sizeDelta.x, rt.sizeDelta.y);
    }

    /// <summary>Flug von a nach b mit Bogen nach oben (<paramref name="lift"/> Pixel).</summary>
    public static Vector2 Fly(Vector2 a, Vector2 b, float p, float lift)
    {
        Vector2 v = Vector2.Lerp(a, b, p);
        v.y -= Mathf.Sin(p * Mathf.PI) * lift;
        return v;
    }
}

/// <summary>Ein Knopf zum Ansehen - Look umschalten wie bei <see cref="SkinButton"/>.</summary>
public sealed class FakeButton
{
    public Image Bg, Icon;
    public TextMeshProUGUI Label;
    public Vector2 LabelHome;

    public void SetLook(GameHudSkin.ButtonLook look)
    {
        Bg.sprite = GameHudSkin.Button(look);

        Color text;
        switch (look)
        {
            case GameHudSkin.ButtonLook.Gold:
            case GameHudSkin.ButtonLook.GoldHover:
            case GameHudSkin.ButtonLook.DangerHover: text = GameHudSkin.Ink; break;
            case GameHudSkin.ButtonLook.Pressed:     text = GameHudSkin.ParchDark; break;
            case GameHudSkin.ButtonLook.Disabled:    text = GameHudSkin.StoneLight; break;
            default:                                 text = GameHudSkin.Cream; break;
        }
        Label.color = text;

        Vector2 push = look == GameHudSkin.ButtonLook.Pressed ? new Vector2(0f, -1f) : Vector2.zero;
        Label.rectTransform.anchoredPosition = LabelHome + push;
    }

    public void SetText(string text)
    {
        if (Label.text != text) Label.text = text;
    }
}
