using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// DER BAUM IM GROSSEN FELD - also das, was zwischen den Kategorieknoepfen links
/// und der Beschreibungskarte rechts liegt.
///
/// Aufbau wie im Konzeptbild: drei Bahnen (oben, Mitte, unten) laufen von links
/// nach rechts. Ganz links steht der Startknoten jeder Bahn, alles Weitere haengt
/// daran. Passt eine Kategorie nicht in die Breite, wird nach rechts gescrollt -
/// mit Mausrad, A/D, den Pfeiltasten oder den beiden Pfeilen am Rand.
///
/// Diese Klasse ist bewusst KEIN MonoBehaviour: <see cref="HubSkilltreeUI"/> baut
/// das Fenster, diese hier baut nur das Feld darin und bekommt Maus und Tasten
/// von dort durchgereicht. So bleiben beide ueberschaubar.
///
/// Was ein Knoten gibt, steht nicht hier - das kommt aus dem Baum-Asset (siehe
/// <see cref="SkillTreeAsset"/>). Hier geht es nur um Formen, Farben und Linien.
/// </summary>
public sealed class HubSkilltreeGraph : System.IDisposable
{
    /// <summary>Die Farben, die sich das Feld mit dem Rest des Fensters teilt.</summary>
    public struct Palette
    {
        public Color PanelFill;
        public Color PanelInset;
        public Color PanelBorder;
        public Color PanelInk;
        public Color PanelInkDim;
        public Color TextOnColor;

        /// <summary>Wie viel dunkler die Kante gegenueber der Kategoriefarbe ist.</summary>
        public float BorderShade;

        /// <summary>Wie viel heller ein Knoten unter der Maus wird.</summary>
        public float HoverLift;
    }

    // ---------------------------------------------------------------- Masse
    // Alles in Pixeln der 320x180-Vorlage.

    /// <summary>Kantenlaenge eines Knotens.</summary>
    public const float NodeSize = 17f;

    /// <summary>Abstand zweier Schritte - muss zu SkillTreeLayout.SpacingX passen.</summary>
    const float StepX = 34f;

    /// <summary>Abstand der Bahnen zur Mitte.</summary>
    const float LaneY = 32f;

    /// <summary>Luft links und rechts im Feld, damit der erste Knoten nicht klebt.</summary>
    const float PadX = 14f;

    /// <summary>Dicke der Verbindungslinien.</summary>
    const float LineThickness = 1f;

    class Node
    {
        public SkillNodeDef Def;
        public GameObject Go;
        public Image Outline;
        public Image Fill;
        public Image Symbol;     // Schloss, Haken oder eigenes Bild
        public Vector2 Local;    // Mitte, in Feldkoordinaten (links = 0)
    }

    class Segment
    {
        public SkillNodeDef From;
        public Image Image;
    }

    readonly Transform parent;
    readonly Rect area;              // das Feld im 320x180-Bild
    readonly Palette palette;
    readonly SkillShapeSprites shapes = new SkillShapeSprites();

    readonly List<Node> nodes = new List<Node>();
    readonly List<Segment> segments = new List<Segment>();
    readonly List<GameObject> spawned = new List<GameObject>();

    RectTransform viewport;   // schneidet ab, was rechts und links heraushaengt
    RectTransform content;    // wird beim Scrollen verschoben
    Image scrollLeft, scrollRight;

    SkillBranchDef branch;
    Color accent = Color.white;

    float scrollX;
    float maxScroll;

    Node hovered;

    public HubSkilltreeGraph(Transform parent, Rect area, Palette palette)
    {
        this.parent  = parent;
        this.area    = area;
        this.palette = palette;

        BuildFrame();
    }

    // ==================================================================
    //  Abfragen fuer das Fenster drumherum
    // ==================================================================

    /// <summary>Der Knoten unter der Maus, oder null.</summary>
    public SkillNodeDef Hovered => hovered != null ? hovered.Def : null;

    public bool CanScrollLeft  => scrollX > 0.5f;
    public bool CanScrollRight => scrollX < maxScroll - 0.5f;

    /// <summary>Die Kaesten der beiden Blaetterpfeile - fuer die Trefferpruefung.</summary>
    public Rect ScrollLeftArea =>
        new Rect(area.x + 1f, area.y + area.height * 0.5f - 4.5f, 5f, 9f);

    public Rect ScrollRightArea =>
        new Rect(area.xMax - 6f, area.y + area.height * 0.5f - 4.5f, 5f, 9f);

    // ==================================================================
    //  Aufbau
    // ==================================================================

    void BuildFrame()
    {
        GameObject vp = HubUiKit.NewRect("TreeViewport", parent);
        HubUiKit.Place((RectTransform)vp.transform, area);
        viewport = (RectTransform)vp.transform;
        spawned.Add(vp);

        // Schneidet ab, was beim Scrollen aus dem Feld laeuft. Ohne das wuerden
        // die Knoten ueber die Kategorieknoepfe und die Karte rechts wandern.
        vp.AddComponent<RectMask2D>();

        GameObject c = HubUiKit.NewRect("TreeContent", viewport);
        content = (RectTransform)c.transform;
        content.anchorMin = content.anchorMax = content.pivot = new Vector2(0f, 1f);
        content.sizeDelta = new Vector2(area.width, area.height);
        content.anchoredPosition = Vector2.zero;
        spawned.Add(c);

        // Die Blaetterpfeile liegen NEBEN dem Viewport, sonst wuerden sie
        // mitgescrollt und vom Rand abgeschnitten.
        scrollLeft = HubUiKit.NewImage("TreeScrollLeft", parent, PixelArrow(false), palette.PanelInkDim);
        scrollLeft.preserveAspect = false;
        HubUiKit.Place((RectTransform)scrollLeft.transform, ScrollLeftArea);
        spawned.Add(scrollLeft.gameObject);

        scrollRight = HubUiKit.NewImage("TreeScrollRight", parent, PixelArrow(true), palette.PanelInkDim);
        scrollRight.preserveAspect = false;
        HubUiKit.Place((RectTransform)scrollRight.transform, ScrollRightArea);
        spawned.Add(scrollRight.gameObject);
    }

    Sprite PixelArrow(bool right)
    {
        // Die Pfeile gibt es schon im Hub-Baukasten - eine eigene Instanz dafuer
        // waere Verschwendung, also werden sie hier einmal erzeugt und behalten.
        arrows = arrows ?? new HubPixelSprites();
        return right ? arrows.ArrowRight : arrows.ArrowLeft;
    }

    HubPixelSprites arrows;

    /// <summary>Baut das Feld fuer eine Kategorie komplett neu.</summary>
    public void Show(SkillBranchDef newBranch)
    {
        branch = newBranch;
        accent = branch != null ? branch.Color : Color.white;

        Clear();

        if (branch == null)
        {
            UpdateScrollState();
            return;
        }

        float centerY = area.height * 0.5f;

        // Erst die Linien, dann die Knoten - so liegen die Linien dahinter.
        foreach (SkillNodeDef def in branch.Nodes)
        {
            foreach (SkillNodeDef p in def.Requires) AddConnection(p, def, centerY);
        }

        foreach (SkillNodeDef def in branch.Nodes) AddNode(def, centerY);

        // Wie weit man scrollen kann: Inhaltsbreite minus sichtbare Breite.
        float contentWidth = PadX * 2f + branch.MaxStep * StepX + NodeSize;
        maxScroll = Mathf.Max(0f, contentWidth - area.width);
        scrollX   = Mathf.Clamp(scrollX, 0f, maxScroll);

        ApplyScroll();
        Refresh();
    }

    void Clear()
    {
        foreach (Node n in nodes)
        {
            if (n.Go != null) Object.Destroy(n.Go);
        }

        foreach (Segment s in segments)
        {
            if (s.Image != null) Object.Destroy(s.Image.gameObject);
        }

        nodes.Clear();
        segments.Clear();
        hovered = null;
    }

    Vector2 LocalOf(SkillNodeDef def, float centerY)
    {
        return new Vector2(PadX + NodeSize * 0.5f + def.Step * StepX,
                           centerY - SkillTreeLayout.LaneOffset(def.Lane) * LaneY);
    }

    void AddNode(SkillNodeDef def, float centerY)
    {
        Vector2 local = LocalOf(def, centerY);

        GameObject go = HubUiKit.NewRect("Node_" + def.LocalId, content);
        PlaceInContent((RectTransform)go.transform,
                       new Rect(local.x - NodeSize * 0.5f, local.y - NodeSize * 0.5f,
                                NodeSize, NodeSize));

        var node = new Node { Def = def, Go = go, Local = local };

        // Kante zuerst, Fuellung darueber - beide in derselben Groesse, die Kante
        // schaut nur an den Raendern hervor (siehe SkillShapeSprites.Outline).
        node.Fill = HubUiKit.NewImage("Fill", go.transform, shapes.Fill(def.Shape), Color.white);
        node.Fill.preserveAspect = false;
        Stretch((RectTransform)node.Fill.transform);

        node.Outline = HubUiKit.NewImage("Outline", go.transform, shapes.Outline(def.Shape), Color.white);
        node.Outline.preserveAspect = false;
        Stretch((RectTransform)node.Outline.transform);

        node.Symbol = HubUiKit.NewImage("Symbol", go.transform, null, Color.white);
        node.Symbol.preserveAspect = true;
        PlaceCentered((RectTransform)node.Symbol.transform, 8f, 10f);

        nodes.Add(node);
    }

    /// <summary>
    /// Die Verbindung zweier Knoten. In derselben Bahn ist das ein Strich; ueber
    /// Bahnen hinweg ein Winkel aus drei Stuecken - waagerecht, senkrecht,
    /// waagerecht. Eine echte Schraege waere bei einem Pixel Staerke eine
    /// Treppe, der Winkel bleibt sauber.
    /// </summary>
    void AddConnection(SkillNodeDef from, SkillNodeDef to, float centerY)
    {
        Vector2 a = LocalOf(from, centerY);
        Vector2 b = LocalOf(to, centerY);

        float half = NodeSize * 0.5f;
        float x0 = Mathf.Min(a.x, b.x) + half;
        float x1 = Mathf.Max(a.x, b.x) - half;

        if (x1 <= x0)
        {
            // Beide Knoten stehen uebereinander: nur der senkrechte Teil.
            AddSegment(from, VerticalRect(a.x, a.y, b.y, half));
            return;
        }

        if (Mathf.Approximately(a.y, b.y))
        {
            AddSegment(from, new Rect(x0, a.y - LineThickness * 0.5f, x1 - x0, LineThickness));
            return;
        }

        // Der Knick sitzt in der Mitte zwischen beiden Knoten.
        float mid = (x0 + x1) * 0.5f;

        AddSegment(from, new Rect(x0, a.y - LineThickness * 0.5f, mid - x0, LineThickness));
        AddSegment(from, VerticalRect(mid, a.y, b.y, 0f));
        AddSegment(from, new Rect(mid, b.y - LineThickness * 0.5f, x1 - mid, LineThickness));
    }

    static Rect VerticalRect(float x, float y0, float y1, float trim)
    {
        float top    = Mathf.Min(y0, y1) + trim;
        float bottom = Mathf.Max(y0, y1) - trim;

        return new Rect(x - LineThickness * 0.5f, top, LineThickness, Mathf.Max(0f, bottom - top));
    }

    void AddSegment(SkillNodeDef from, Rect rect)
    {
        if (rect.width <= 0f || rect.height <= 0f) return;

        Image img = HubUiKit.NewImage("Line", content, shapes.Line, palette.PanelBorder);
        img.preserveAspect = false;
        PlaceInContent((RectTransform)img.transform, rect);

        segments.Add(new Segment { From = from, Image = img });
    }

    /// <summary>Wie HubUiKit.Place, aber relativ zum scrollenden Inhalt.</summary>
    static void PlaceInContent(RectTransform rect, Rect pixels)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(pixels.width, pixels.height);
        rect.anchoredPosition = new Vector2(pixels.x, -pixels.y);
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    static void PlaceCentered(RectTransform r, float w, float h)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(w, h);
        r.anchoredPosition = Vector2.zero;
    }

    // ==================================================================
    //  Scrollen
    // ==================================================================

    /// <summary>Verschiebt das Feld. Ein Schritt ist ein Knotenabstand.</summary>
    public bool Scroll(float steps)
    {
        if (maxScroll <= 0f) return false;

        float before = scrollX;
        scrollX = Mathf.Clamp(scrollX + steps * StepX, 0f, maxScroll);

        if (Mathf.Approximately(before, scrollX)) return false;

        ApplyScroll();
        UpdateScrollState();
        return true;
    }

    /// <summary>Scrollt so weit, dass dieser Knoten im Feld liegt.</summary>
    public void ScrollTo(SkillNodeDef def)
    {
        if (def == null || maxScroll <= 0f) return;

        float x = PadX + NodeSize * 0.5f + def.Step * StepX;

        float min = x + NodeSize - area.width + PadX;
        float max = x - NodeSize - PadX;

        scrollX = Mathf.Clamp(scrollX, Mathf.Max(0f, min), Mathf.Max(0f, max));
        scrollX = Mathf.Clamp(scrollX, 0f, maxScroll);

        ApplyScroll();
        UpdateScrollState();
    }

    void ApplyScroll()
    {
        if (content == null) return;
        content.anchoredPosition = new Vector2(-Mathf.Round(scrollX), 0f);
    }

    void UpdateScrollState()
    {
        if (scrollLeft != null)  scrollLeft.gameObject.SetActive(CanScrollLeft);
        if (scrollRight != null) scrollRight.gameObject.SetActive(CanScrollRight);
    }

    // ==================================================================
    //  Maus
    // ==================================================================

    /// <summary>
    /// Sucht den Knoten unter der Maus. Der Punkt kommt in Pixeln der
    /// 320x180-Vorlage, Nullpunkt links oben - genau wie im Rest des Fensters.
    /// Gibt true zurueck, wenn sich etwas geaendert hat.
    /// </summary>
    public bool UpdateHover(bool mouseValid, Vector2 mousePixel)
    {
        Node was = hovered;
        hovered = null;

        if (mouseValid && area.Contains(mousePixel))
        {
            foreach (Node n in nodes)
            {
                if (!ScreenRectOf(n).Contains(mousePixel)) continue;
                hovered = n;
                break;
            }
        }

        if (hovered == was) return false;

        Refresh();
        return true;
    }

    /// <summary>Wo ein Knoten gerade wirklich auf dem Bildschirm sitzt.</summary>
    Rect ScreenRectOf(Node n)
    {
        float x = area.x + n.Local.x - scrollX;
        float y = area.y + n.Local.y;

        return new Rect(x - NodeSize * 0.5f, y - NodeSize * 0.5f, NodeSize, NodeSize);
    }

    /// <summary>
    /// Kauft den Knoten unter der Maus. Gibt zurueck, ob gekauft wurde - das
    /// Fenster spielt danach den passenden Ton.
    /// </summary>
    public bool ClickHovered(out bool hitNode)
    {
        // Der Startknoten ist nur der Anker der Kategorie: kein Kauf, kein Ton.
        hitNode = hovered != null && !hovered.Def.IsStart;
        if (!hitNode) return false;

        bool bought = Skills.TryUnlock(hovered.Def);
        Refresh();
        return bought;
    }

    // ==================================================================
    //  Anzeige
    // ==================================================================

    public void Refresh()
    {
        Color border = Shade(accent, palette.BorderShade);

        foreach (Node n in nodes)
        {
            bool unlocked = Skills.IsUnlocked(n.Def);
            bool open     = !unlocked && Skills.RequirementsMet(n.Def);
            bool affordable = open && Skills.Currency >= n.Def.Price;
            bool isHover  = n == hovered;

            if (unlocked)
            {
                // Gekauft: traegt die Kategoriefarbe, wie die Kugeln im Konzeptbild.
                n.Fill.color    = isHover ? Lift(accent, palette.HoverLift) : accent;
                n.Outline.color = border;
            }
            else if (open)
            {
                // Kaufbar: heller Kern, Kante in Kategoriefarbe. Reicht das Geld
                // nicht, bleibt die Kante gedaempft - man sieht also auf einen
                // Blick, was man sich gerade leisten kann.
                n.Fill.color    = isHover ? Lift(palette.PanelFill, palette.HoverLift * 0.6f)
                                          : palette.PanelInset;
                n.Outline.color = affordable ? accent : Shade(accent, palette.BorderShade * 1.4f);
            }
            else
            {
                n.Fill.color    = palette.PanelBorder;
                n.Outline.color = Shade(palette.PanelBorder, 0.35f);
            }

            // Symbol: eigenes Bild gewinnt, sonst Haken bzw. Schloss.
            if (n.Def.Icon != null)
            {
                n.Symbol.sprite = n.Def.Icon;
                n.Symbol.color  = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.45f);
                n.Symbol.gameObject.SetActive(true);
            }
            else if (unlocked && !n.Def.IsStart)   // der Start traegt keinen Haken
            {
                n.Symbol.sprite = shapes.Check;
                n.Symbol.color  = InkOn(accent);
                n.Symbol.gameObject.SetActive(true);
            }
            else if (!unlocked && !open)
            {
                n.Symbol.sprite = shapes.Padlock;
                n.Symbol.color  = Shade(palette.PanelBorder, 0.45f);
                n.Symbol.gameObject.SetActive(true);
            }
            else
            {
                n.Symbol.gameObject.SetActive(false);
            }
        }

        // Eine Linie leuchtet, sobald der Knoten davor gekauft ist - so sieht man,
        // wie weit der Pfad schon offen ist.
        foreach (Segment s in segments)
        {
            s.Image.color = Skills.IsUnlocked(s.From) ? accent : palette.PanelBorder;
        }

        if (scrollLeft != null)  scrollLeft.color  = palette.PanelInkDim;
        if (scrollRight != null) scrollRight.color = palette.PanelInkDim;

        UpdateScrollState();
    }

    Color InkOn(Color background)
    {
        float luma = 0.299f * background.r + 0.587f * background.g + 0.114f * background.b;
        return luma > 0.6f ? palette.PanelInk : palette.TextOnColor;
    }

    static Color Lift(Color c, float amount) =>
        new Color(Mathf.Lerp(c.r, 1f, amount), Mathf.Lerp(c.g, 1f, amount),
                  Mathf.Lerp(c.b, 1f, amount), c.a);

    static Color Shade(Color c, float amount) =>
        new Color(Mathf.Lerp(c.r, 0f, amount), Mathf.Lerp(c.g, 0f, amount),
                  Mathf.Lerp(c.b, 0f, amount), c.a);

    // ==================================================================

    public void Dispose()
    {
        shapes.Dispose();
        arrows?.Dispose();
        arrows = null;

        foreach (GameObject go in spawned)
        {
            if (go == null) continue;
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }

        spawned.Clear();
        nodes.Clear();
        segments.Clear();
    }
}
