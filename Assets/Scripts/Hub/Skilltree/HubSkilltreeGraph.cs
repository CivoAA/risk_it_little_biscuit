using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// DER BAUM IM GROSSEN FELD des Skilltrees (<see cref="HubSkilltreeUI"/>).
///
/// Drei Bahnen (oben, Mitte, unten) laufen von links nach rechts, ganz links
/// steht der Startknoten, alles Weitere haengt daran. Passt eine Kategorie nicht
/// in die Breite, wird waagerecht gescrollt (Mausrad oder die Pfeile am Rand).
///
/// Seit dem UI-2.0-Umbau liegt das Feld auf der 480x270-Seite von
/// <see cref="OptionsKit"/> und bekommt die Maus ueber Zeiger-Ereignisse je
/// Knoten - keine eigene Trefferrechnung mehr.
///
/// Kein MonoBehaviour: das Fenster baut das Feld und reicht Tasten durch.
/// Was ein Knoten gibt, steht im Baum-Asset (<see cref="SkillTreeAsset"/>),
/// hier geht es nur um Formen, Farben und Linien.
/// </summary>
public sealed class HubSkilltreeGraph : System.IDisposable
{
    // ---------------------------------------------------------------- Masse
    // Seitenpixel.

    /// <summary>Kantenlaenge der Form - ungerade, damit es eine echte Mitte gibt.</summary>
    public const int Shape = 19;

    /// <summary>Platz eines Knotens: Form plus zwei Pixel ringsum fuer die Eckwinkel.</summary>
    public const int NodeSize = Shape + 4;

    /// <summary>Abstand zweier Schritte (Spalten).</summary>
    const int StepX = 34;

    /// <summary>
    /// Abstand der Bahnen. Muss gleich <see cref="StepX"/> sein: nur dann sind
    /// Schraegen exakt 45° (bzw. 2:1, 3:1 ueber mehrere Spalten) und die
    /// Pixeltreppe bleibt gleichmaessig statt zu wabbeln.
    /// </summary>
    const int LaneY = StepX;

    /// <summary>Luft links und rechts im Feld.</summary>
    const int PadX = 10;

    const int Mid = NodeSize / 2;   // 11: Mitte des Knotens, auf einem ganzen Pixel

    class Node
    {
        public SkillNodeDef Def;
        public int X, Y;            // links oben im Inhalt
        public Image Shadow, Fill, Gloss, Outline, Symbol;
        public Image[] Corners;
        public TextMeshProUGUI Price;
    }

    class Segment
    {
        public SkillNodeDef From, To;
        public Image Image;
    }

    readonly RectInt area;
    readonly TMP_FontAsset font;
    readonly SkillShapeSprites shapes = new SkillShapeSprites(Shape);

    readonly List<Node> nodes = new List<Node>();
    readonly List<Segment> segments = new List<Segment>();
    readonly List<SkillNodeDef> order = new List<SkillNodeDef>();

    // Die Schraegen sind zur Laufzeit gezeichnet und muessen wieder weg.
    readonly List<Texture2D> lineTextures = new List<Texture2D>();
    readonly List<Sprite> lineSprites = new List<Sprite>();

    RectTransform viewport, content;
    SkinButton scrollLeft, scrollRight;

    SkillBranchDef branch;
    Color accent = Color.white;
    float scrollX, maxScroll;

    Node hovered;
    SkillNodeDef selected;

    /// <summary>Maus ist auf einen Knoten gekommen (null = runter).</summary>
    public System.Action<SkillNodeDef> HoverChanged;

    /// <summary>Knoten angeklickt.</summary>
    public System.Action<SkillNodeDef> Clicked;

    public HubSkilltreeGraph(RectTransform parent, RectInt area, TMP_FontAsset font)
    {
        this.area = area;
        this.font = font;

        viewport = OptionsKit.Rect("TreeViewport", parent, area.x, area.y, area.width, area.height);
        viewport.gameObject.AddComponent<RectMask2D>();
        content = OptionsKit.Rect("TreeContent", viewport, 0, 0, area.width, area.height);

        // Neben dem Viewport, sonst wuerden sie mitgescrollt.
        Sprite l = GameHudSkin.ArrowLeft, r = GameHudSkin.Arrow;
        int ay = area.y + area.height / 2 - 7;
        scrollLeft = SkinButton.Create(parent, area.x + 1, ay, 14, 14, "", font, SkinButton.Kind.Wood,
                                       () => Scroll(-2f), l);
        scrollRight = SkinButton.Create(parent, area.xMax - 15, ay, 14, 14, "", font, SkinButton.Kind.Wood,
                                        () => Scroll(2f), r);
    }

    // ==================================================================
    //  Abfragen
    // ==================================================================

    public SkillNodeDef Hovered => hovered != null ? hovered.Def : null;

    /// <summary>Die Knoten der Kategorie in Lesereihenfolge (Spalte, dann Bahn) - fuer die Tasten.</summary>
    public IReadOnlyList<SkillNodeDef> Order => order;

    public SkillNodeDef Selected
    {
        get => selected;
        set
        {
            selected = value;
            if (selected != null) ScrollTo(selected);
            Refresh();
        }
    }

    // ==================================================================
    //  Aufbau
    // ==================================================================

    /// <summary>Baut das Feld fuer eine Kategorie komplett neu.</summary>
    public void Show(SkillBranchDef newBranch)
    {
        branch = newBranch;
        accent = branch != null ? branch.Color : Color.white;

        OptionsKit.Clear(content);
        DestroyLineTextures();
        nodes.Clear();
        segments.Clear();
        order.Clear();
        hovered = null;
        selected = null;
        scrollX = 0f;

        if (branch != null)
        {
            // Linien zuerst - sie liegen hinter den Knoten.
            foreach (SkillNodeDef def in branch.Nodes)
                foreach (SkillNodeDef p in def.Requires)
                    AddConnection(p, def);

            foreach (SkillNodeDef def in branch.Nodes) AddNode(def);

            order.AddRange(branch.Nodes);
            order.Sort((a, b) => a.Step != b.Step
                ? a.Step.CompareTo(b.Step)
                : SkillTreeLayout.LaneOffset(b.Lane).CompareTo(SkillTreeLayout.LaneOffset(a.Lane)));

            float contentWidth = PadX * 2 + branch.MaxStep * StepX + NodeSize;
            maxScroll = Mathf.Max(0f, contentWidth - area.width);
            content.sizeDelta = new Vector2(Mathf.Max(area.width, contentWidth), area.height);
        }
        else
        {
            maxScroll = 0f;
        }

        ApplyScroll();
        Refresh();
    }

    /// <summary>Links oben eines Knotens im Inhalt - immer ganze Pixel.</summary>
    Vector2Int PosOf(SkillNodeDef def)
    {
        // Etwas ueber der Mitte: unter dem Startknoten steht noch START.
        int top0 = (area.height - NodeSize) / 2 - 3;
        return new Vector2Int(PadX + def.Step * StepX,
                              top0 - SkillTreeLayout.LaneOffset(def.Lane) * LaneY);
    }

    void AddNode(SkillNodeDef def)
    {
        Vector2Int p = PosOf(def);
        RectTransform root = OptionsKit.Rect("Node_" + def.LocalId, content, p.x, p.y, NodeSize, NodeSize);

        // Trefferflaeche: unsichtbar, aber sie faengt Maus und Klick.
        Image hit = root.gameObject.AddComponent<Image>();
        hit.sprite = GameHudSkin.White;
        hit.color = Color.clear;
        hit.raycastTarget = true;

        var n = new Node { Def = def, X = p.x, Y = p.y };

        const int inset = (NodeSize - Shape) / 2;
        n.Shadow = OptionsKit.Img("Shadow", root, inset, inset + 2, Shape, Shape, shapes.Fill(def.Shape));
        n.Fill = OptionsKit.Img("Fill", root, inset, inset, Shape, Shape, shapes.Fill(def.Shape));
        n.Gloss = OptionsKit.Img("Gloss", root, inset, inset, Shape, Shape, shapes.Gloss(def.Shape));
        n.Outline = OptionsKit.Img("Outline", root, inset, inset, Shape, Shape, shapes.Outline(def.Shape));
        n.Symbol = OptionsKit.Img("Symbol", root, 0, 0, 1, 1, null);

        n.Corners = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            int cx = i == 1 || i == 2 ? NodeSize - 5 : 0;
            int cy = i == 2 || i == 3 ? NodeSize - 5 : 0;
            n.Corners[i] = OptionsKit.Img("Corner" + i, root, cx, cy, 5, 5, GameHudSkin.Corner(i));
        }

        // Preis unter kaufbaren Knoten, START unter dem Anker der Bahn.
        n.Price = OptionsKit.Label("Price", content, p.x - 10, p.y + NodeSize - 1, NodeSize + 20, 13,
                                   def.IsStart ? Loc.Get("ui.skilltree.start", "START") : "", font,
                                   OptionsKit.SizeText, GameHudSkin.Stone, TextAlignmentOptions.Center);

        PointerRelay relay = root.gameObject.AddComponent<PointerRelay>();
        relay.Enter = () => { hovered = n; Refresh(); HoverChanged?.Invoke(def); };
        relay.Exit = () =>
        {
            if (hovered != n) return;
            hovered = null;
            Refresh();
            HoverChanged?.Invoke(null);
        };
        relay.Down = () => Clicked?.Invoke(def);

        nodes.Add(n);
    }

    /// <summary>
    /// Verbindung zweier Knoten. In derselben Bahn oder Spalte ein gerader
    /// Strich, ueber Bahnen hinweg eine direkte Schraege von Mitte zu Mitte.
    /// Jede Verbindung ist ein eigener Strich - sie treffen sich erst im Knoten,
    /// der sie verdeckt (Linien liegen hinter den Knoten).
    ///
    /// Alle Striche laufen von Mitte zu Mitte, nicht nur bis zum Kasten um die
    /// Form: so reichen sie bei jeder Form (Dreieck, Raute, Stern) bis an den Rand.
    /// </summary>
    void AddConnection(SkillNodeDef from, SkillNodeDef to)
    {
        Vector2Int a = PosOf(from) + new Vector2Int(Mid, Mid);
        Vector2Int b = PosOf(to) + new Vector2Int(Mid, Mid);

        if (a.x == b.x)
        {
            AddSegment(from, to, a.x, Mathf.Min(a.y, b.y), 1, Mathf.Abs(b.y - a.y) + 1);
            return;
        }

        if (a.y == b.y)
        {
            AddSegment(from, to, Mathf.Min(a.x, b.x), a.y, Mathf.Abs(b.x - a.x) + 1, 1);
            return;
        }

        AddDiagonal(from, to, a, b);
    }

    void AddSegment(SkillNodeDef from, SkillNodeDef to, int x, int y, int w, int h)
    {
        if (w <= 0 || h <= 0) return;
        Image img = OptionsKit.Img("Line", content, x, y, w, h, GameHudSkin.White, GameHudSkin.StoneDark);
        img.transform.SetAsFirstSibling();
        segments.Add(new Segment { From = from, To = to, Image = img });
    }

    /// <summary>
    /// Schraege als eigene kleine Textur, Pixel fuer Pixel (Bresenham) - so bleibt
    /// sie bei 1 px Staerke scharf, statt ein gedrehtes, verwaschenes Bild zu sein.
    /// </summary>
    void AddDiagonal(SkillNodeDef from, SkillNodeDef to, Vector2Int a, Vector2Int b)
    {
        int left = Mathf.Min(a.x, b.x), top = Mathf.Min(a.y, b.y);
        int w = Mathf.Abs(b.x - a.x) + 1, h = Mathf.Abs(b.y - a.y) + 1;

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        var pixels = new Color32[w * h];

        int x = a.x - left, y = a.y - top;
        int ex = b.x - left, ey = b.y - top;
        int dx = Mathf.Abs(ex - x), dy = -Mathf.Abs(ey - y);
        int sx = x < ex ? 1 : -1, sy = y < ey ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            // Seitenpixel zaehlen von oben, Texturzeilen von unten.
            pixels[(h - 1 - y) * w + x] = new Color32(255, 255, 255, 255);
            if (x == ex && y == ey) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x += sx; }
            if (e2 <= dx) { err += dx; y += sy; }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        lineTextures.Add(tex);

        Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 1f);
        lineSprites.Add(sprite);

        Image img = OptionsKit.Img("Line", content, left, top, w, h, sprite, GameHudSkin.StoneDark);
        img.transform.SetAsFirstSibling();
        segments.Add(new Segment { From = from, To = to, Image = img });
    }

    void DestroyLineTextures()
    {
        foreach (Sprite s in lineSprites) if (s != null) Object.Destroy(s);
        foreach (Texture2D t in lineTextures) if (t != null) Object.Destroy(t);
        lineSprites.Clear();
        lineTextures.Clear();
    }

    // ==================================================================
    //  Scrollen
    // ==================================================================

    public bool CanScroll => maxScroll > 0f;

    /// <summary>Verschiebt das Feld. Ein Schritt ist ein Knotenabstand.</summary>
    public bool Scroll(float steps)
    {
        if (maxScroll <= 0f) return false;

        float before = scrollX;
        scrollX = Mathf.Clamp(scrollX + steps * StepX, 0f, maxScroll);
        if (Mathf.Approximately(before, scrollX)) return false;

        ApplyScroll();
        return true;
    }

    void ScrollTo(SkillNodeDef def)
    {
        if (maxScroll <= 0f) return;
        int x = PosOf(def).x;
        if (x - PadX < scrollX) scrollX = x - PadX;
        else if (x + NodeSize + PadX > scrollX + area.width) scrollX = x + NodeSize + PadX - area.width;
        scrollX = Mathf.Clamp(scrollX, 0f, maxScroll);
        ApplyScroll();
    }

    void ApplyScroll()
    {
        content.anchoredPosition = new Vector2(-Mathf.Round(scrollX), 0f);
        scrollLeft.gameObject.SetActive(scrollX > 0.5f);
        scrollRight.gameObject.SetActive(scrollX < maxScroll - 0.5f);
    }

    // ==================================================================
    //  Anzeige
    // ==================================================================

    public void Refresh()
    {
        Color edge = HubSkilltreeUI.Shade(accent, 0.5f);

        foreach (Node n in nodes)
        {
            SkillNodeDef d = n.Def;
            bool unlocked = d.IsStart || Skills.IsUnlocked(d);
            bool open = !unlocked && Skills.RequirementsMet(d);
            bool affordable = open && Skills.Currency >= d.Price;
            bool isHover = n == hovered;

            n.Shadow.color = new Color(0f, 0f, 0f, unlocked || open ? 0.55f : 0.3f);

            if (unlocked)
            {
                n.Fill.color = isHover ? HubSkilltreeUI.Lift(accent, 0.2f) : accent;
                n.Outline.color = edge;
                n.Gloss.color = new Color(1f, 1f, 1f, isHover ? 0.55f : 0.38f);
            }
            else if (open)
            {
                // Kaufbar: dunkler Kern, Kante in Kategoriefarbe. Reicht das Geld
                // nicht, bleibt die Kante gedaempft.
                n.Fill.color = isHover ? (Color)GameHudSkin.StoneDark : (Color)GameHudSkin.Trough;
                n.Outline.color = affordable ? accent : Color.Lerp(accent, GameHudSkin.Stone, 0.55f);
                n.Gloss.color = new Color(1f, 1f, 1f, 0.12f);
            }
            else
            {
                n.Fill.color = isHover ? (Color)GameHudSkin.Stone : (Color)GameHudSkin.StoneDark;
                n.Outline.color = GameHudSkin.Hex(0x3a2e34);
                n.Gloss.color = new Color(1f, 1f, 1f, 0.07f);
            }

            // Symbol: eigenes Bild gewinnt, sonst Haken bzw. Schloss.
            Sprite symbol = null;
            Color symbolColor = Color.white;
            if (d.Icon != null)
            {
                symbol = d.Icon;
                symbolColor = unlocked ? Color.white : new Color(1f, 1f, 1f, open ? 0.6f : 0.35f);
            }
            else if (unlocked && !d.IsStart)
            {
                symbol = shapes.Check;
                symbolColor = HubSkilltreeUI.InkOn(accent);
            }
            else if (!unlocked && !open)
            {
                symbol = shapes.Padlock;
                symbolColor = GameHudSkin.Stone;
            }

            n.Symbol.enabled = symbol != null;
            if (symbol != null)
            {
                n.Symbol.sprite = symbol;
                n.Symbol.color = symbolColor;
                float w = symbol.rect.width, h = symbol.rect.height;
                OptionsKit.Move(n.Symbol.rectTransform, Mathf.Floor((NodeSize - w) / 2f),
                                Mathf.Floor((NodeSize - h) / 2f), w, h);
            }

            if (!d.IsStart)
            {
                n.Price.text = open ? d.Price.ToString() : "";
                n.Price.color = affordable ? GameHudSkin.Gold : GameHudSkin.JamLight;
            }

            bool sel = d == selected;
            foreach (Image c in n.Corners)
            {
                c.enabled = sel || isHover;
                c.color = sel ? GameHudSkin.Gold : GameHudSkin.Cream;
            }
        }

        // Eine Linie leuchtet, sobald der Knoten davor gelernt ist; ganz, wenn
        // beide Enden gelernt sind.
        foreach (Segment s in segments)
        {
            bool a = s.From.IsStart || Skills.IsUnlocked(s.From);
            bool b = Skills.IsUnlocked(s.To);
            s.Image.color = a && b ? accent
                          : a ? HubSkilltreeUI.Shade(accent, 0.35f)
                          : (Color)GameHudSkin.StoneDark;
        }
    }

    /// <summary>
    /// Das bisschen Leben im Feld: kaufbare Kanten atmen, die Eckwinkel am
    /// Knoten unter der Maus pulsen. Nur Farbe, kein Pixel bewegt sich.
    /// </summary>
    public void Animate(float time)
    {
        float wave = 0.5f + 0.5f * Mathf.Sin(time * 3.2f);

        foreach (Node n in nodes)
        {
            SkillNodeDef d = n.Def;
            bool unlocked = d.IsStart || Skills.IsUnlocked(d);
            if (!unlocked && Skills.RequirementsMet(d) && Skills.Currency >= d.Price)
                n.Outline.color = Color.Lerp(accent, HubSkilltreeUI.Lift(accent, 0.55f), wave);

            if (n == hovered && d != selected)
            {
                Color c = GameHudSkin.Cream;
                c.a = 0.55f + 0.45f * wave;
                foreach (Image corner in n.Corners) corner.color = c;
            }
        }
    }

    public void Dispose()
    {
        shapes.Dispose();
        DestroyLineTextures();
        nodes.Clear();
        segments.Clear();
    }
}
