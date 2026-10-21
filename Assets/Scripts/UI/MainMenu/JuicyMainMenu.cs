using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Das Hauptmenü als graue Box mit weißen Text-Einträgen statt Knöpfen - nachgebaut
/// nach dem "juicy game menu"-Reel von mii_misan. Die Schritte aus dem Video stehen
/// als Überschriften im Code (Einfliegen, Versatz, Schweben, Partikel, Hover-Größe,
/// Versatz, Unterstrich, Abdunkeln, Hover-Farbe, Rauten, Punch). Cursor-Spur, Screen-Shake
/// und die durchlaufende Pastellfarbe aus dem Video sind bewusst weggelassen.
///
/// Die Komponente gehört auf das Menü-Canvas. Die Einträge stehen im Inspector unter
/// "entries": Beschriftung plus OnClick, genau wie vorher an den Buttons. Reihenfolge
/// in der Liste = Reihenfolge von oben nach unten. Alles Sichtbare baut sich beim
/// Start selbst, im Editor ist die Box deshalb erst im Play-Modus zu sehen.
///
/// Maus, Tastatur (Pfeile/WS + Enter) und Controller funktionieren: jeder Eintrag
/// ist intern ein Button mit expliziter Hoch/Runter-Navigation.
///
/// Alle Maße sind Canvas-Einheiten im 320x180-Raster.
/// </summary>
[DisallowMultipleComponent]
public class JuicyMainMenu : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        [Tooltip("Rückfall-Text. Übersetzt wird über den Schlüssel ui.menu.<label> in Resources/Localization.")]
        public string label;
        public UnityEvent onClick = new UnityEvent();

        public string Text => Loc.Get("ui.menu." + label.ToLowerInvariant(), label.ToUpperInvariant());
    }

    public enum PlateStyle
    {
        /// <summary>Holzknöpfe aus GameHudSkin wie in Optionen und Pause, der erste Eintrag in Gold.</summary>
        Skin,
        /// <summary>Die gezeichneten Platten aus plateSprite / plateHoverSprite.</summary>
        Sprite,
        /// <summary>
        /// Kleine Holzschilder wie das Logo (GameHudSkin.MenuSign): Maserung, Nieten,
        /// Kante, Schatten, Symbol links. Gewaehlt: Goldrahmen, Schild hebt sich,
        /// Kekse zeigen von beiden Seiten drauf, es funkelt.
        /// </summary>
        Sign
    }

    [Header("Einträge (oben nach unten)")]
    [SerializeField] private List<Entry> entries = new List<Entry>();

    [Header("Box")]
    [SerializeField, Tooltip("Aus = keine graue Box, nur die Einträge direkt auf dem Hintergrund " +
                             "(dunkle Schrift, keine Partikel). An = graue Box mit heller Schrift.")]
    private bool showBox = false;
    [SerializeField, Tooltip("An = jeder Eintrag sitzt auf einer Button-Platte (btuuon), beim Hover die helle Platte.")]
    private bool showPlates = true;
    [SerializeField, Tooltip("Skin = Holzknöpfe im Stil von Optionen/Pause, Sprite = plateSprite/plateHoverSprite.")]
    private PlateStyle plateStyle = PlateStyle.Skin;
    [SerializeField] private Sprite plateSprite;
    [SerializeField] private Sprite plateHoverSprite;
    [SerializeField, Tooltip("Höhe der Holzknöpfe (nur Skin). Gerade Zahl, sonst liegt die Mitte auf halben Pixeln.")]
    private int skinPlateHeight = 14;
    [SerializeField, Tooltip("Mindestbreite der Holzknöpfe (nur Skin).")]
    private int skinPlateMinWidth = 92;
    [SerializeField, Tooltip("Mindestbreite der Holzschilder (nur Sign). Gerade Zahl.")]
    private int signMinWidth = 100;
    [SerializeField, Tooltip("Mitte der Box relativ zur Canvas-Mitte.")]
    private Vector2 boxCenter = new Vector2(0f, 0f);
    [SerializeField] private int minBoxWidth = 104;
    [SerializeField, Tooltip("Luft über dem ersten und unter dem letzten Eintrag.")]
    private int paddingY = 8;
    [SerializeField, Tooltip("Abstand von Eintrag zu Eintrag. Gerade Zahl = Einträge liegen auf ganzen Pixeln.")]
    private int rowStep = 18;
    [SerializeField] private Color boxFill = new Color32(0x2b, 0x2b, 0x30, 0xf0);
    [SerializeField] private Color checkerA = new Color32(0x2b, 0x2b, 0x30, 0xff);
    [SerializeField] private Color checkerB = new Color32(0x30, 0x30, 0x36, 0xff);
    [SerializeField, Tooltip("Kantenlänge eines Schachbrettfelds.")]
    private int checkerCell = 4;
    [SerializeField] private Color boxBorder = new Color32(0x5e, 0x5e, 0x68, 0xff);
    [SerializeField] private Color boxOutline = new Color32(0x14, 0x14, 0x17, 0xff);
    [SerializeField] private Color boxHighlight = new Color32(0x3c, 0x3c, 0x44, 0xff);
    [SerializeField] private Color boxShadow = new Color(0f, 0f, 0f, 0.35f);

    [Header("Text")]
    [SerializeField, Tooltip("Leer = die Pixel-Font des Projekts wird gesucht. Texte mit Zeichen, " +
                             "die sie nicht kennt (Umlaute), bekommen automatisch Jersey10.")]
    private TMP_FontAsset font;
    [SerializeField] private float fontSize = 8f;
    [SerializeField, Tooltip("Schrift in der grauen Box.")]
    private Color boxTextColor = new Color32(0xfa, 0xf4, 0xec, 0xff);
    [SerializeField, Tooltip("Schrift ohne Box, direkt auf dem Hintergrund.")]
    private Color plainTextColor = new Color32(0x3d, 0x37, 0x36, 0xff);
    [SerializeField, Tooltip("Schrift auf den Button-Platten.")]
    private Color plateTextColor = new Color32(0xff, 0xf8, 0xcd, 0xff);

    [Header("1. Einfliegen")]
    [SerializeField] private float boxPopTime = 0.3f;
    [SerializeField] private float slideDuration = 0.55f;
    [SerializeField, Tooltip("btn.delay = i * 0.15")]
    private float staggerDelay = 0.15f;
    [SerializeField, Tooltip("Ohne Box: so weit links starten die Einträge und blenden dabei ein. " +
                             "Mit Box kommen sie hinter der Boxkante hervor.")]
    private float slideDistance = 40f;

    [Header("2. Schweben")]
    [SerializeField] private float floatSpeed = 1.5f;
    [SerializeField] private float floatPhase = 0.8f;
    [SerializeField, Tooltip("0 = kein Schweben. Neben der statischen Szene wirkte das unruhig.")]
    private float floatAmplitude = 0f;

    [Header("3. Hover")]
    [SerializeField] private float hoverScale = 1.15f;
    [SerializeField] private float scaleSpeed = 8f;
    [SerializeField, Tooltip("Seitlicher Versatz des gewählten Eintrags. Mit Platten wirkte -2 wie verrutscht.")]
    private float hoverOffset = 0f;
    [SerializeField] private float offsetSpeed = 10f;
    [SerializeField, Tooltip("So viel breiter als der Text wird der Unterstrich.")]
    private float underlinePadding = 4f;
    [SerializeField] private float underlineSpeed = 14f;
    [SerializeField, Tooltip("Deckkraft der nicht gewählten Einträge.")]
    private float dimAlpha = 0.3f;
    [SerializeField] private float dimSpeed = 10f;
    [SerializeField, Tooltip("Feste Farbe des gewählten Eintrags in der Box - Orange.")]
    private Color boxHoverColor = new Color32(0xe8, 0x86, 0x2a, 0xff);
    [SerializeField, Tooltip("Farbe der Rauten auf der Wand (und des gewählten Eintrags ohne Box/Platten) - dunkles Orange.")]
    private Color plainHoverColor = new Color32(0xc8, 0x62, 0x14, 0xff);
    [SerializeField, Tooltip("Schrift auf der hellen Platte des gewählten Eintrags - dunkles Orange.")]
    private Color plateHoverColor = new Color32(0xe8, 0x86, 0x2a, 0xff);
    [SerializeField, Tooltip("Deckkraft der Schrift nicht gewählter Einträge auf Platten " +
                             "(auf Braun wäre 0.3 kaum noch lesbar).")]
    private float plateDimAlpha = 0.6f;

    [Header("4. Rauten")]
    [SerializeField] private float diamondGap = 5f;
    [SerializeField] private float diamondFollow = 12f;
    [SerializeField] private float pulseSpeed = 6f;
    [SerializeField] private float pulseAmount = 0.15f;

    [Header("5. Klick")]
    [SerializeField] private float punchScale = 1.3f;
    [SerializeField] private float punchReturn = 12f;

    [Header("6. Partikel")]
    [SerializeField] private int particleCount = 14;

    [Header("Version")]
    [SerializeField, Tooltip("Zeigt Application.version (Player Settings > Version) unten rechts.")]
    private bool showVersion = true;
    [SerializeField, Tooltip("Abstand zur Ecke unten rechts.")]
    private Vector2Int versionMargin = new Vector2Int(4, 3);

    private class Item
    {
        public RectTransform rect;
        public RectTransform visual;
        public Image plate;
        public CanvasGroup group;
        public TMP_Text label;
        public RectTransform underline;
        public Image underlineImage;
        public JuicyMenuItem relay;
        public Sprite plateNormal, platePressed;
        public Color textColor, hoverColor;
        // Nur Sign:
        public Image ring;
        public TMP_Text shadow;
        public RectTransform icon;
        public Sprite signLit, signDown, signLitDown;
        public Color dimColor, shadowColor;
        public Vector2 labelHome, iconHome;
        public float pressT, lift, litT;
        public bool waving;
        public RectTransform shineMask;
        public Image shine;
        public float shineT = 1f, dimStrength;
        public float textWidth;
        public float baseY;
        public float scale = 1f;
        public float offset;
        public float underlineWidth;
        public float colorT;
        public float alpha = 1f;
    }

    private struct Sparkle
    {
        public Image image;
        public float age, life;
    }

    private struct Particle
    {
        public Image image;
        public float x0, y, speed, phase, drift, baseAlpha;
    }

    private readonly List<Item> items = new List<Item>();
    private readonly List<Particle> particles = new List<Particle>();

    private RectTransform root;
    private RectTransform menu;
    private CanvasGroup menuGroup;
    private RectTransform diamondLeft, diamondRight;
    private Image diamondLeftImage, diamondRightImage;
    private Texture2D checkerTexture, diamondTexture;
    private Sprite diamondSprite;
    private readonly List<Sprite> createdSprites = new List<Sprite>();

    private Vector2 boxSize;
    private Color textColor, hoverColor, diamondColor;
    private Sprite plateNormal, platePressed;
    private Vector2 plateSize;
    private TMP_FontAsset textFont;
    private CanvasGroup versionGroup;
    private float elapsed;
    private int hoveredIndex = -1;
    private float dimT;
    private float diamondX, diamondY, diamondHalfWidth, diamondAlpha;
    private int lastActive = -1;
    private float hopT, blinkTimer = 2.5f, sparkleTimer, startShineTimer = 0.4f;
    private Sparkle[] sparkles;

    // ==================================================================
    //  Aufbau
    // ==================================================================

    private void Start()
    {
        if (entries.Count == 0)
        {
            Debug.LogWarning("[JuicyMainMenu] Keine Einträge eingetragen.", this);
            enabled = false;
            return;
        }

        if (font == null) font = PixelUI.FindPixelFont();
        textFont = PixelUI.FindTextFont();
        if (plateStyle == PlateStyle.Sprite) showPlates &= plateSprite != null;
        textColor = SkinPlates || SignPlates ? (Color)GameHudSkin.Cream
                  : showPlates ? plateTextColor : showBox ? boxTextColor : plainTextColor;
        hoverColor = SkinPlates || SignPlates ? (Color)GameHudSkin.Cream
                   : showPlates ? plateHoverColor : showBox ? boxHoverColor : plainHoverColor;
        // Die Rauten sitzen neben dem Eintrag auf dem Hintergrund, nicht auf der Platte.
        diamondColor = showBox ? boxHoverColor : plainHoverColor;
        Build();
        BuildVersion();
        BuildExtras();
        Loc.LanguageChanged += OnLanguageChanged;
    }

    /// <summary>
    /// Kueche mit Leben (<see cref="MainMenuStage"/>), das Profil-Schild oben
    /// links (<see cref="MainMenuProfileBadge"/>) und das Scoreboard
    /// unten links (<see cref="MainMenuScoreboard"/>) - alle ohne Szenenobjekt.
    /// </summary>
    private void BuildExtras()
    {
        if (GetComponent<MainMenuStage>() == null) gameObject.AddComponent<MainMenuStage>();
        if (GetComponent<MainMenuScoreboard>() == null) gameObject.AddComponent<MainMenuScoreboard>();

        MainMenuProfileBadge badge = GetComponent<MainMenuProfileBadge>();
        if (badge == null) badge = gameObject.AddComponent<MainMenuProfileBadge>();
        MainMenuController controller = GetComponent<MainMenuController>();
        badge.OnOpen = () =>
        {
            if (controller != null) controller.OpenProfiles();
            else ProfilePanel.Open();
        };
    }

    private bool SkinPlates => showPlates && plateStyle == PlateStyle.Skin;
    private bool SignPlates => showPlates && plateStyle == PlateStyle.Sign;

    /// <summary>
    /// Die Menü-Schrift, solange sie jedes Zeichen kennt - sonst die Schrift mit
    /// Umlauten. ThaleahFat hat kein Ä/Ö/Ü/ß.
    /// </summary>
    private TMP_FontAsset FontFor(string text) => OptionsKit.PickFont(text, font, textFont);

    /// <summary>Sprache gewechselt (Optionen): Menü mit neuen Texten neu bauen, ohne neuen Auftritt.</summary>
    private void OnLanguageChanged()
    {
        if (root == null) return;
        Destroy(root.gameObject);
        items.Clear();
        particles.Clear();
        hoveredIndex = -1;
        Build();
        menuGroup.alpha = 1f;
        elapsed = Mathf.Max(elapsed, 100f);
    }

    private void Build()
    {
        root = PixelUI.Rect("JuicyMenu", transform, Vector2.zero, Vector2.zero);

        // Breite aus dem längsten Text: Platz für Punch-Größe plus Rauten links und rechts.
        float widest = 0f;
        foreach (Entry e in entries) widest = Mathf.Max(widest, MeasureText(e.Text));
        int w = Mathf.Max(minBoxWidth, Mathf.CeilToInt(widest * punchScale + 2f * (diamondGap + 10f)));
        if (showPlates) BuildPlateSprites(widest, ref w);
        int h = entries.Count * rowStep + 2 * paddingY;
        // Gerade Maße, sonst landen die Kanten der mittig gesetzten Box auf halben Pixeln.
        if (w % 2 != 0) w++;
        if (h % 2 != 0) h++;
        boxSize = new Vector2(w, h);

        menu = PixelUI.Rect("Menu", root, boxSize, boxCenter);
        menuGroup = menu.gameObject.AddComponent<CanvasGroup>();
        menuGroup.alpha = 0f;

        Vector2 inner = boxSize - new Vector2(2f, 2f);
        if (showBox)
        {
            // Box: Schatten, dunkle Außenkante, helle Kante, Schachbrett-Füllung, Lichtkante oben.
            PixelUI.Panel("Shadow", menu, boxSize + new Vector2(2f, 2f), new Vector2(2f, -2f), boxShadow);
            PixelUI.Panel("Outline", menu, boxSize + new Vector2(2f, 2f), Vector2.zero, boxOutline);
            PixelUI.Panel("Border", menu, boxSize, Vector2.zero, boxBorder);
            PixelUI.Panel("Fill", menu, inner, Vector2.zero, boxFill);
            BuildChecker(inner);
            PixelUI.Panel("TopLight", menu, new Vector2(inner.x, 1f), new Vector2(0f, inner.y * 0.5f - 0.5f), boxHighlight);
        }

        // Mit Box kommen die Einträge hinter der Boxkante hervor. Ohne Box gäbe es
        // dort nur eine unsichtbare Schnittkante mitten auf der Wand - dann blenden
        // sie stattdessen beim Reinrutschen ein.
        RectTransform clip = PixelUI.Rect("Clip", menu, inner, Vector2.zero);
        if (showBox) clip.gameObject.AddComponent<RectMask2D>();

        if (showBox) BuildParticles(clip, inner);

        RectTransform list = PixelUI.Rect("Items", clip, inner, Vector2.zero);
        if (SignPlates)
        {
            for (int i = 0; i < entries.Count; i++) items.Add(BuildSignItem(i, list));
            BuildCookies(list);
            BuildSparkles(list);
        }
        else
        {
            diamondLeft = BuildDiamond("DiamondL", list, out diamondLeftImage);
            diamondRight = BuildDiamond("DiamondR", list, out diamondRightImage);
            for (int i = 0; i < entries.Count; i++) items.Add(BuildItem(i, list, inner.x));
        }
        LinkNavigation();

    }

    /// <summary>
    /// Platten in Originalgröße, wenn der längste Text reinpasst - sonst in die
    /// Breite gezogen (9-Slice, Rahmen bleibt 1:1). Nie skaliert: gestreckte
    /// Pixelgrafik verschmiert.
    /// </summary>
    private void BuildPlateSprites(float widest, ref int boxWidth)
    {
        if (SignPlates)
        {
            // Platz fuer Symbol + Nieten links und gleich viel rechts, damit die Schrift mittig bleibt.
            int w = Mathf.Max(signMinWidth, Mathf.CeilToInt(widest) + 2 * 22);
            if (w % 2 != 0) w++;
            plateSize = new Vector2(w + 2, GameHudSkin.SignH);
            // Kekse links und rechts: 14 breit, 3 Abstand, 2 Luft zum Wippen.
            boxWidth = Mathf.Max(boxWidth, w + 2 * (14 + 3 + 2) + 4);
            return;
        }

        if (SkinPlates)
        {
            // Holzknopf aus GameHudSkin: 9-Slice mit PPU 100, also ein Texel = ein Pixel.
            int w = Mathf.Max(skinPlateMinWidth, Mathf.CeilToInt(widest * hoverScale) + 16);
            if (w % 2 != 0) w++;
            plateSize = new Vector2(w, skinPlateHeight);
            plateNormal = GameHudSkin.Button(GameHudSkin.ButtonLook.Wood);
            platePressed = GameHudSkin.Button(GameHudSkin.ButtonLook.Hover);
            boxWidth = Mathf.Max(boxWidth, w + 2 * Mathf.CeilToInt(diamondGap + 6f) + 2);
            return;
        }

        Vector2 native = plateSprite.rect.size;
        int plateW = Mathf.Max((int)native.x, Mathf.CeilToInt(widest) + 12);
        if (plateW % 2 != 0) plateW++;
        plateSize = new Vector2(plateW, native.y);

        Sprite hover = plateHoverSprite != null ? plateHoverSprite : plateSprite;
        if (plateW == (int)native.x)
        {
            plateNormal = plateSprite;
            platePressed = hover;
        }
        else
        {
            plateNormal = Sliced(plateSprite);
            platePressed = Sliced(hover);
        }

        // Platz für Platte plus Rauten links und rechts innerhalb der Maske.
        boxWidth = Mathf.Max(boxWidth, plateW + 2 * Mathf.CeilToInt(diamondGap + 6f) + 2);
    }

    private Sprite Sliced(Sprite source)
    {
        Sprite s = Sprite.Create(source.texture, source.rect, new Vector2(0.5f, 0.5f), source.pixelsPerUnit,
                                 0, SpriteMeshType.FullRect, new Vector4(2f, 2f, 2f, 2f));
        createdSprites.Add(s);
        return s;
    }

    private float MeasureText(string text)
    {
        TMP_Text probe = PixelUI.Label("Probe", root, new Vector2(400f, 40f), Vector2.zero, text,
                                       fontSize, Color.clear, TextAlignmentOptions.Center, FontFor(text));
        float width = probe.GetPreferredValues(text).x;
        Destroy(probe.gameObject);
        return width;
    }

    private void BuildChecker(Vector2 size)
    {
        checkerTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
        };
        checkerTexture.SetPixels(new[] { checkerA, checkerB, checkerB, checkerA });
        checkerTexture.Apply();

        RectTransform rt = PixelUI.Rect("Checker", menu, size, Vector2.zero);
        RawImage raw = rt.gameObject.AddComponent<RawImage>();
        raw.texture = checkerTexture;
        raw.raycastTarget = false;
        // Ein Texel = ein Feld; die Kachel ist zwei Felder breit.
        raw.uvRect = new Rect(0f, 0f, size.x / (checkerCell * 2f), size.y / (checkerCell * 2f));
        // Deckkraft der Füllung übernehmen, damit der Hintergrund leicht durchscheint.
        raw.color = new Color(1f, 1f, 1f, boxFill.a);
    }

    private RectTransform BuildDiamond(string name, Transform parent, out Image image)
    {
        if (diamondSprite == null)
        {
            // 5x5-Raute als Pixelgrafik, damit sie nicht wie ein gedrehtes Quadrat verschmiert.
            string[] rows = { "..#..", ".###.", "#####", ".###.", "..#.." };
            diamondTexture = new Texture2D(5, 5, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < 5; y++)
                for (int x = 0; x < 5; x++)
                    diamondTexture.SetPixel(x, y, rows[y][x] == '#' ? Color.white : Color.clear);
            diamondTexture.Apply();
            diamondSprite = Sprite.Create(diamondTexture, new Rect(0, 0, 5, 5), new Vector2(0.5f, 0.5f), 5f);
        }

        image = PixelUI.Panel(name, parent, new Vector2(5f, 5f), Vector2.zero, Color.clear);
        image.sprite = diamondSprite;
        return image.rectTransform;
    }

    private Item BuildItem(int index, Transform parent, float width)
    {
        Entry entry = entries[index];
        Item item = new Item();

        // Zeilenmitten auf ganzen Pixeln: gerade Höhe, gerader Zeilenabstand.
        item.baseY = boxSize.y * 0.5f - paddingY - rowStep * (index + 0.5f);

        // Trefferfläche: die Platte bzw. ohne Platte die ganze Zeilenbreite, unsichtbar.
        Vector2 hitSize = showPlates ? plateSize : new Vector2(width, rowStep);
        Image hit = PixelUI.Panel("Item_" + entry.label, parent, hitSize,
                                  new Vector2(-width, item.baseY), Color.clear);
        hit.raycastTarget = true;
        item.rect = hit.rectTransform;
        item.group = hit.gameObject.AddComponent<CanvasGroup>();

        // Der erste Eintrag (Start) ist im Holz-Stil der goldene Hauptknopf - wie
        // FERTIG in den Optionen. Auf Gold steht dunkle Schrift.
        bool primary = SkinPlates && index == 0;
        item.plateNormal = primary ? GameHudSkin.Button(GameHudSkin.ButtonLook.Gold) : plateNormal;
        item.platePressed = primary ? GameHudSkin.Button(GameHudSkin.ButtonLook.GoldHover) : platePressed;
        item.textColor = primary ? (Color)GameHudSkin.Ink : textColor;
        item.hoverColor = primary ? (Color)GameHudSkin.Ink : hoverColor;

        if (showPlates)
        {
            item.plate = PixelUI.Panel("Plate", item.rect, plateSize, Vector2.zero, Color.white);
            item.plate.sprite = item.plateNormal;
            item.plate.type = item.plateNormal == plateSprite ? Image.Type.Simple : Image.Type.Sliced;
        }

        // Holzknopf: unten liegt ein 2 px dunkles Band, die optische Mitte ist 1 px höher.
        Vector2 labelPos = SkinPlates ? new Vector2(0f, 1f) : Vector2.zero;
        string text = entry.Text;
        item.visual = PixelUI.Rect("Visual", item.rect, new Vector2(width, rowStep), labelPos);
        item.label = PixelUI.Label("Label", item.visual, new Vector2(width, rowStep), Vector2.zero, text,
                                   fontSize, item.textColor, TextAlignmentOptions.Center, FontFor(text));
        item.label.textWrappingMode = TextWrappingModes.NoWrap;
        item.label.ForceMeshUpdate();
        item.textWidth = item.label.GetPreferredValues(text).x;

        // Unterstrich knapp unter der Grundlinie.
        float baseline = item.label.textInfo.lineCount > 0
            ? item.label.textInfo.lineInfo[0].baseline
            : -fontSize * 0.5f;
        item.underlineImage = PixelUI.Panel("Underline", item.visual, new Vector2(0f, 1f),
                                            new Vector2(0f, Mathf.Round(baseline) - 1.5f), item.textColor);
        item.underline = item.underlineImage.rectTransform;
        // Auf der Platte zeigt die helle Platte die Auswahl - ein Strich darin wäre doppelt.
        item.underlineImage.enabled = !showPlates;

        Button button = hit.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = hit;
        int captured = index;
        button.onClick.AddListener(() => Activate(captured));

        item.relay = hit.gameObject.AddComponent<JuicyMenuItem>();
        item.relay.Owner = this;
        item.relay.Index = index;

        return item;
    }

    // ---------- Holzschild (Sign) ----------

    /// <summary>Gerade Groesse: Mitte auf ganzem Pixel. Ungerade: auf halbem, sonst liegen die Kanten krumm.</summary>
    private static float Half(float size) => Mathf.RoundToInt(size) % 2 == 1 ? 0.5f : 0f;

    /// <summary>Mitte des Bretts relativ zur Bildmitte des Schilds (Brett sitzt oben, darunter Kante und Schatten).</summary>
    private float FaceMid => plateSize.y * 0.5f - (GameHudSkin.SignFaceTop + GameHudSkin.SignFaceH * 0.5f);

    private Item BuildSignItem(int index, Transform parent)
    {
        Entry entry = entries[index];
        Item item = new Item();
        item.baseY = boxSize.y * 0.5f - paddingY - rowStep * (index + 0.5f);

        // Der erste Eintrag (Start) ist das goldene Schild, darauf dunkle Schrift.
        int w = Mathf.RoundToInt(plateSize.x) - 2;
        bool primary = index == 0;
        GameHudSkin.SignLook look = primary ? GameHudSkin.SignLook.Gold : GameHudSkin.SignLook.Wood;
        GameHudSkin.SignLook lit = primary ? GameHudSkin.SignLook.GoldLit : GameHudSkin.SignLook.WoodLit;
        item.plateNormal = GameHudSkin.MenuSign(w, look, false);
        item.signDown = GameHudSkin.MenuSign(w, look, true);
        item.signLit = GameHudSkin.MenuSign(w, lit, false);
        item.signLitDown = GameHudSkin.MenuSign(w, lit, true);

        // Trefferflaeche = nur das Schild, ohne Schatten darunter.
        Image hit = PixelUI.Panel("Item_" + entry.label, parent, plateSize, new Vector2(-plateSize.x, item.baseY), Color.clear);
        hit.raycastTarget = true;
        hit.raycastPadding = new Vector4(1f, 4f, 1f, 1f);
        item.rect = hit.rectTransform;
        item.group = hit.gameObject.AddComponent<CanvasGroup>();

        item.ring = PixelUI.Panel("Ring", item.rect, plateSize, Vector2.zero, new Color(1f, 1f, 1f, 0f));
        item.ring.sprite = GameHudSkin.MenuSignRing(w);
        item.plate = PixelUI.Panel("Plate", item.rect, plateSize, Vector2.zero, Color.white);
        item.plate.sprite = item.plateNormal;

        // Lichtschimmer, auf das Brett beschnitten (RectMask2D schneidet auf ganzen Pixeln).
        item.shineMask = PixelUI.Rect("ShineMask", item.rect, new Vector2(plateSize.x - 4f, GameHudSkin.SignFaceH),
                                      new Vector2(0f, FaceMid));
        item.shineMask.gameObject.AddComponent<RectMask2D>();
        Sprite shineSprite = GameHudSkin.MenuShine;
        item.shine = PixelUI.Panel("Shine", item.shineMask, shineSprite.rect.size, Vector2.zero, Color.white);
        item.shine.sprite = shineSprite;
        item.shine.enabled = false;

        // Gold dunkelt weniger ab - sonst wird es senffarben.
        item.dimStrength = primary ? 0.1f : 0.2f;
        item.textColor = primary ? (Color)GameHudSkin.Ink : (Color)GameHudSkin.Cream;
        item.hoverColor = primary ? (Color)GameHudSkin.Night : Color.white;
        item.dimColor = primary ? (Color)GameHudSkin.Ink : (Color)GameHudSkin.ParchDark;
        item.shadowColor = primary ? (Color)GameHudSkin.GoldLight : (Color)GameHudSkin.Ink;

        Sprite iconSprite = GameHudSkin.MenuIcon(entry.label.ToLowerInvariant());
        if (iconSprite != null)
        {
            Vector2 iconSize = iconSprite.rect.size;
            // Links neben der Niete; senkrecht auf die Brettmitte.
            float ix = -plateSize.x * 0.5f + 8f + iconSize.x * 0.5f;
            float iy = Mathf.Round(FaceMid - Half(iconSize.y)) + Half(iconSize.y);
            item.iconHome = new Vector2(ix, iy);
            Image icon = PixelUI.Panel("Icon", item.rect, iconSize, item.iconHome, Color.white);
            icon.sprite = iconSprite;
            item.icon = icon.rectTransform;
        }

        string text = entry.Text;
        item.labelHome = new Vector2(0f, Mathf.Round(FaceMid));
        Vector2 labelSize = new Vector2(plateSize.x, GameHudSkin.SignFaceH);
        item.visual = PixelUI.Rect("Visual", item.rect, labelSize, item.labelHome);
        // Harte Pixel-Kante unter der Schrift: auf Holz dunkel, auf Gold hell (eingeschnitzt).
        item.shadow = PixelUI.Label("Shadow", item.visual, labelSize, new Vector2(0f, -1f), text, fontSize,
                                    item.shadowColor, TextAlignmentOptions.Center, FontFor(text));
        item.shadow.textWrappingMode = TextWrappingModes.NoWrap;
        item.label = PixelUI.Label("Label", item.visual, labelSize, Vector2.zero, text, fontSize,
                                   item.textColor, TextAlignmentOptions.Center, FontFor(text));
        item.label.textWrappingMode = TextWrappingModes.NoWrap;
        item.textWidth = item.label.GetPreferredValues(text).x;

        Button button = hit.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = hit;
        int captured = index;
        button.onClick.AddListener(() => Activate(captured));

        item.relay = hit.gameObject.AddComponent<JuicyMenuItem>();
        item.relay.Owner = this;
        item.relay.Index = index;
        return item;
    }

    /// <summary>Statt der Rauten: der Keks aus dem Logo links und rechts vom gewaehlten Schild.</summary>
    private void BuildCookies(Transform parent)
    {
        Sprite cookie = GameHudSkin.MenuCookie(false);
        diamondLeftImage = PixelUI.Panel("CookieL", parent, cookie.rect.size, Vector2.zero, Color.clear);
        diamondRightImage = PixelUI.Panel("CookieR", parent, cookie.rect.size, Vector2.zero, Color.clear);
        diamondLeftImage.sprite = diamondRightImage.sprite = cookie;
        diamondLeft = diamondLeftImage.rectTransform;
        diamondRight = diamondRightImage.rectTransform;
        // Rechts gespiegelt, schaut dann auch zum Schild. -1 ist ganzzahlig, die Pixel bleiben heil.
        diamondRight.localScale = new Vector3(-1f, 1f, 1f);
    }

    private void BuildSparkles(Transform parent)
    {
        sparkles = new Sparkle[12];
        for (int i = 0; i < sparkles.Length; i++)
        {
            Image img = PixelUI.Panel("Sparkle", parent, new Vector2(5f, 5f), Vector2.zero, Color.white);
            img.sprite = GameHudSkin.MenuSparkle(0);
            img.enabled = false;
            sparkles[i] = new Sparkle { image = img };
        }
    }

    private void LinkNavigation()
    {
        for (int i = 0; i < items.Count; i++)
        {
            Button b = items[i].rect.GetComponent<Button>();
            Navigation nav = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = items[(i - 1 + items.Count) % items.Count].rect.GetComponent<Button>(),
                selectOnDown = items[(i + 1) % items.Count].rect.GetComponent<Button>()
            };
            b.navigation = nav;
        }
    }

    private void BuildParticles(Transform parent, Vector2 area)
    {
        for (int i = 0; i < particleCount; i++)
        {
            Image img = PixelUI.Panel("Particle", parent, Vector2.one, Vector2.zero, Color.white);
            particles.Add(new Particle
            {
                image = img,
                x0 = UnityEngine.Random.Range(-area.x * 0.5f, area.x * 0.5f),
                y = UnityEngine.Random.Range(-area.y * 0.5f, area.y * 0.5f),
                speed = UnityEngine.Random.Range(3f, 9f),
                phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f),
                drift = UnityEngine.Random.Range(1f, 4f),
                baseAlpha = UnityEngine.Random.Range(0.12f, 0.35f)
            });
        }
    }

    /// <summary>
    /// Versionsnummer unten rechts, klein und zurückhaltend. Kommt aus
    /// Application.version - also Player Settings > Version, keine zweite Stelle
    /// zum Pflegen. Sitzt direkt am Canvas, damit sie an der Ecke klebt, egal
    /// wie breit das Bild ist.
    /// </summary>
    private void BuildVersion()
    {
        if (!showVersion || string.IsNullOrEmpty(Application.version)) return;

        string text = "v" + Application.version;
        Vector2 corner = new Vector2(1f, 0f);
        RectTransform box = PixelUI.Rect("Version", transform, new Vector2(80f, 12f),
                                         new Vector2(-versionMargin.x, versionMargin.y), corner);
        versionGroup = box.gameObject.AddComponent<CanvasGroup>();
        versionGroup.alpha = 0f;
        versionGroup.blocksRaycasts = false;

        TMP_FontAsset f = FontFor(text);
        // Harte Pixel-Kontur statt weichem Schatten: vier Kopien in Tinte, eine obendrauf.
        Vector2[] offsets = { new Vector2(-1f, 0f), new Vector2(1f, 0f), new Vector2(0f, -1f), new Vector2(0f, 1f) };
        foreach (Vector2 o in offsets)
            MakeVersionLabel(box, o, text, GameHudSkin.Ink, f);
        MakeVersionLabel(box, Vector2.zero, text, GameHudSkin.Cream, f);
    }

    private void MakeVersionLabel(RectTransform parent, Vector2 offset, string text, Color color, TMP_FontAsset f)
    {
        TMP_Text t = PixelUI.Label("Label", parent, parent.sizeDelta, offset, text, fontSize, color,
                                   TextAlignmentOptions.BottomRight, f);
        t.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private void OnDestroy()
    {
        Loc.LanguageChanged -= OnLanguageChanged;
        if (checkerTexture != null) Destroy(checkerTexture);
        if (diamondTexture != null) Destroy(diamondTexture);
        if (diamondSprite != null) Destroy(diamondSprite);
        foreach (Sprite s in createdSprites) if (s != null) Destroy(s);
    }

    // ==================================================================
    //  Eingabe
    // ==================================================================

    public void OnItemPointerEnter(JuicyMenuItem item)
    {
        hoveredIndex = item.Index;
        // Die Maus übernimmt die Auswahl, damit die Tastatur danach von hier weiterläuft.
        if (EventSystem.current != null && !OverlayOpen)
            EventSystem.current.SetSelectedGameObject(item.gameObject);
    }

    public void OnItemPointerExit(JuicyMenuItem item)
    {
        if (hoveredIndex == item.Index) hoveredIndex = -1;
        // Wie im Video: Maus runter = nichts mehr hervorgehoben.
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == item.gameObject)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private static bool OverlayOpen => OptionsPanel.IsOpen || AchievementPanel.IsOpen || ProfilePanel.IsOpen;

    private int ResolveActive()
    {
        if (OverlayOpen) return -1;
        if (hoveredIndex >= 0) return hoveredIndex;

        for (int i = 0; i < items.Count; i++)
            if (items[i].relay.Selected) return i;
        return -1;
    }

    /// <summary>Erster Tastendruck wählt einen Eintrag an - danach navigiert das EventSystem.</summary>
    private void HandleFirstNavigation()
    {
        EventSystem es = EventSystem.current;
        if (es == null || OverlayOpen || hoveredIndex >= 0) return;
        if (es.currentSelectedGameObject != null && es.currentSelectedGameObject.activeInHierarchy) return;

        bool down = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
        bool up = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
        var pad = UnityEngine.InputSystem.Gamepad.current;
        if (pad != null)
        {
            down |= pad.dpad.down.wasPressedThisFrame || pad.leftStick.down.wasPressedThisFrame;
            up |= pad.dpad.up.wasPressedThisFrame || pad.leftStick.up.wasPressedThisFrame;
        }

        if (down) es.SetSelectedGameObject(items[0].rect.gameObject);
        else if (up) es.SetSelectedGameObject(items[items.Count - 1].rect.gameObject);
    }

    private void Activate(int index)
    {
        if (OverlayOpen || SceneFader.IsFading) return;

        // ---------- 5. Punch beim Klick ----------
        // Aktion sofort, ohne Wartezeit - der Punch läuft einfach mit.
        items[index].scale = punchScale;
        if (SignPlates)
        {
            // Schild wird ins Brett gedrueckt, die Kekse huepfen, es staubt Gold.
            items[index].pressT = 0.16f;
            hopT = 1f;
            for (int i = 0; i < 6; i++) SpawnSparkle(index, true);
        }
        entries[index].onClick.Invoke();
    }

    // ==================================================================
    //  Animation
    // ==================================================================

    private void Update()
    {
        if (menu == null) return;
        // Erst wenn das Studio-Logo weg ist - sonst liefe der Auftritt ungesehen darunter ab.
        if (StudioSplash.IsShowing) return;

        // Eigene Uhr mit gekapptem Schritt: der erste Frame nach dem Laden ist oft
        // Hunderte Millisekunden lang und würde das Einfliegen sonst überspringen.
        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
        elapsed += dt;
        float time = Time.unscaledTime;
        float t = elapsed;

        HandleFirstNavigation();
        int active = ResolveActive();

        AnimateBox(t);
        if (SignPlates)
        {
            AnimateSigns(t, time, dt, active);
            AnimateCookies(time, dt, active);
            AnimateSparkles(dt, active);
        }
        else
        {
            AnimateItems(t, time, dt, active);
            AnimateDiamonds(time, dt, active);
        }
        AnimateParticles(time, dt);
    }

    // ---------- 1. Box aufpoppen ----------
    private void AnimateBox(float t)
    {
        float p = Mathf.Clamp01(t / boxPopTime);
        menuGroup.alpha = p;
        if (versionGroup != null) versionGroup.alpha = p;
        menu.localScale = Vector3.one * Mathf.LerpUnclamped(0.9f, 1f, BackOut(p));
    }

    private void AnimateItems(float t, float time, float dt, int active)
    {
        // ---------- Andere abdunkeln ----------
        dimT = Damp(dimT, active >= 0 ? 1f : 0f, dimSpeed, dt);

        for (int i = 0; i < items.Count; i++)
        {
            Item item = items[i];
            bool isActive = i == active;

            // ---------- Reinrutschen: back_out, versetzt nach Index ----------
            float s = Mathf.Clamp01((t - boxPopTime * 0.5f - i * staggerDelay) / slideDuration);
            float slideFrom = showBox ? boxSize.x : slideDistance;
            float slideX = Mathf.LerpUnclamped(-slideFrom, 0f, BackOut(s));
            item.group.alpha = showBox ? 1f : Mathf.Clamp01(s * 2.5f);

            // ---------- Schweben ----------
            float floatY = Mathf.Sin(time * floatSpeed + i * floatPhase) * floatAmplitude;

            // ---------- Hover: Größe + Versatz (Punch federt schneller zurück) ----------
            float targetScale = isActive ? hoverScale : 1f;
            float speed = item.scale > hoverScale + 0.01f ? punchReturn : scaleSpeed;
            item.scale = Damp(item.scale, targetScale, speed, dt);
            item.offset = Damp(item.offset, isActive ? hoverOffset : 0f, offsetSpeed, dt);

            Vector2 pos = new Vector2(slideX + item.offset, item.baseY + floatY);
            // Platten auf ganze Pixel, sonst verschmiert die Pixelgrafik (die Schrift
            // darf weiter wachsen, sie ist ein eigenes Kind und wird nicht gerundet).
            if (showPlates) pos = new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y));
            item.rect.anchoredPosition = pos;
            if (item.plate != null) item.plate.sprite = isActive ? item.platePressed : item.plateNormal;
            item.visual.localScale = Vector3.one * item.scale;

            // ---------- Wachsender Unterstrich ----------
            item.underlineWidth = Damp(item.underlineWidth, isActive ? item.textWidth + underlinePadding : 0f,
                                       underlineSpeed, dt);
            item.underline.sizeDelta = new Vector2(Mathf.Round(item.underlineWidth), 1f);

            // ---------- Farbe: leicht blau für den gewählten, abgedunkelt für den Rest ----------
            item.colorT = Damp(item.colorT, isActive ? 1f : 0f, 12f, dt);
            float targetAlpha = isActive ? 1f : Mathf.Lerp(1f, showPlates ? plateDimAlpha : dimAlpha, dimT);
            item.alpha = Damp(item.alpha, targetAlpha, dimSpeed, dt);

            Color c = Color.Lerp(item.textColor, item.hoverColor, item.colorT);
            c.a = item.alpha;
            item.label.color = c;
            item.underlineImage.color = c;
        }
    }

    // ---------- Holzschilder (Sign) ----------
    private void AnimateSigns(float t, float time, float dt, int active)
    {
        dimT = Damp(dimT, active >= 0 ? 1f : 0f, dimSpeed, dt);

        for (int i = 0; i < items.Count; i++)
        {
            Item item = items[i];
            bool isActive = i == active;

            float s = Mathf.Clamp01((t - boxPopTime * 0.5f - i * staggerDelay) / slideDuration);
            float slideX = Mathf.LerpUnclamped(-slideDistance, 0f, BackOut(s));
            item.group.alpha = Mathf.Clamp01(s * 2.5f);

            // Gewaehlt hebt sich das Schild um einen Pixel - der Schatten bleibt an der Wand.
            item.lift = Damp(item.lift, isActive ? 1f : 0f, 18f, dt);
            item.litT = Damp(item.litT, isActive ? 1f : 0f, 14f, dt);
            item.pressT = Mathf.Max(0f, item.pressT - dt);
            bool down = item.pressT > 0f;

            item.rect.anchoredPosition = new Vector2(Mathf.Round(slideX), item.baseY + Mathf.Round(item.lift));
            item.plate.sprite = isActive ? (down ? item.signLitDown : item.signLit)
                                         : (down ? item.signDown : item.plateNormal);

            // Abdunkeln ueber die Farbe statt die Deckkraft: die Wand scheint nicht
            // durch, und dunkle Schrift auf Gold wird nicht grau.
            float dim = isActive ? 0f : dimT;
            float k = 1f - item.dimStrength * dim;
            item.plate.color = new Color(k, k * 0.97f, k * 0.97f, 1f);
            if (item.icon != null) item.icon.GetComponent<Image>().color = new Color(k, k, k, 1f);

            float ringPulse = 0.7f + 0.3f * Mathf.Sin(time * 5f);
            item.ring.color = new Color(1f, 1f, 1f, item.litT * ringPulse);

            Vector2 press = down ? new Vector2(0f, -2f) : Vector2.zero;
            item.visual.anchoredPosition = item.labelHome + press;
            item.shineMask.anchoredPosition = new Vector2(0f, FaceMid) + press;

            // Schimmer: beim Anwaehlen einmal, START zieht ihn alle paar Sekunden von selbst
            // (sobald es fertig eingeflogen ist) - der Hauptknopf lebt auch ohne Maus.
            if (i == 0 && s >= 1f)
            {
                startShineTimer -= dt;
                if (startShineTimer <= 0f)
                {
                    startShineTimer = 4f;
                    item.shineT = 0f;
                }
            }
            AnimateShine(item, dt);
            if (item.icon != null) item.icon.anchoredPosition = item.iconHome + press;

            Color c = Color.Lerp(item.textColor, item.dimColor, dim);
            item.label.color = Color.Lerp(c, item.hoverColor, item.litT);
            Color sh = item.shadowColor;
            sh.a = Mathf.Lerp(1f, 0.6f, dim);
            item.shadow.color = sh;

            bool wave = isActive && item.litT > 0.5f;
            if (wave || item.waving)
            {
                Wave(item.label, time, wave);
                Wave(item.shadow, time, wave);
                item.waving = wave;
            }
        }
    }

    private void AnimateShine(Item item, float dt)
    {
        if (item.shineT >= 1f)
        {
            item.shine.enabled = false;
            return;
        }
        item.shineT = Mathf.Min(1f, item.shineT + dt / 0.45f);
        float half = item.shineMask.sizeDelta.x * 0.5f + item.shine.rectTransform.sizeDelta.x;
        float x = Mathf.Lerp(-half, half, item.shineT * item.shineT * (3f - 2f * item.shineT));
        // 11 breit, 12 hoch: waagerecht auf halbem, senkrecht auf ganzem Pixel.
        item.shine.rectTransform.anchoredPosition = new Vector2(Mathf.Round(x) + 0.5f, 0f);
        item.shine.enabled = true;
    }

    /// <summary>
    /// Eine kleine Welle laeuft alle knapp zwei Sekunden durch die Schrift: jeder
    /// Buchstabe hopst nacheinander genau einen Pixel hoch - ganze Pixel, kein
    /// weiches Wabern. Aus = Mesh einmal frisch, alles wieder auf der Linie.
    /// </summary>
    private static void Wave(TMP_Text text, float time, bool on)
    {
        text.ForceMeshUpdate();
        if (!on) return;

        TMP_TextInfo info = text.textInfo;
        float head = Mathf.Repeat(time * 14f, 26f);
        for (int c = 0; c < info.characterCount; c++)
        {
            TMP_CharacterInfo ch = info.characterInfo[c];
            if (!ch.isVisible) continue;
            float p = head - c;
            if (p < 0f || p >= 2f) continue;
            Vector3[] v = info.meshInfo[ch.materialReferenceIndex].vertices;
            for (int k = 0; k < 4; k++) v[ch.vertexIndex + k].y += 1f;
        }
        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
    }

    // ---------- Kekse links und rechts vom gewaehlten Schild ----------
    private void AnimateCookies(float time, float dt, int active)
    {
        if (active != lastActive && active >= 0)
        {
            hopT = 1f;
            items[active].shineT = 0f;
        }
        lastActive = active;

        if (active >= 0)
        {
            Vector2 pos = items[active].rect.anchoredPosition;
            float y = pos.y + Mathf.Round(FaceMid);
            // Frisch eingeblendet: direkt hin statt quer durchs Menue zu fliegen.
            if (diamondAlpha < 0.05f)
            {
                diamondX = pos.x;
                diamondY = y;
            }
            diamondX = Damp(diamondX, pos.x, 20f, dt);
            diamondY = Damp(diamondY, y, 20f, dt);
            diamondHalfWidth = plateSize.x * 0.5f + 3f + diamondLeft.sizeDelta.x * 0.5f;
        }
        diamondAlpha = Damp(diamondAlpha, active >= 0 ? 1f : 0f, 16f, dt);

        // Wippt zum Schild hin wie ein Zeigefinger; beim Wechsel ein Hopser.
        float nudge = Mathf.Round((0.5f + 0.5f * Mathf.Sin(time * 7f)) * 2f);
        hopT = Mathf.Max(0f, hopT - dt * 4.5f);
        float hop = Mathf.Round(Mathf.Sin(hopT * Mathf.PI) * 3f);

        // Ab und zu blinzeln.
        blinkTimer -= dt;
        if (blinkTimer < -0.12f) blinkTimer = UnityEngine.Random.Range(1.8f, 3.6f);
        Sprite face = GameHudSkin.MenuCookie(blinkTimer < 0f);

        float x = Mathf.Round(diamondX), y0 = Mathf.Round(diamondY) + hop;
        float hw = Mathf.Round(diamondHalfWidth);
        diamondLeft.anchoredPosition = new Vector2(x - hw + nudge, y0);
        diamondRight.anchoredPosition = new Vector2(x + hw - nudge, y0);
        diamondLeftImage.color = diamondRightImage.color = new Color(1f, 1f, 1f, diamondAlpha);
        diamondLeftImage.sprite = diamondRightImage.sprite = face;
    }

    // ---------- Funkeln am gewaehlten Schild, wie die Sterne im Logo ----------
    private void AnimateSparkles(float dt, int active)
    {
        if (active >= 0)
        {
            sparkleTimer -= dt;
            if (sparkleTimer <= 0f)
            {
                sparkleTimer = UnityEngine.Random.Range(0.16f, 0.36f);
                SpawnSparkle(active, false);
            }
        }

        for (int i = 0; i < sparkles.Length; i++)
        {
            Sparkle sp = sparkles[i];
            if (!sp.image.enabled) continue;
            sp.age += dt;
            float u = sp.age / sp.life;
            if (u >= 1f) sp.image.enabled = false;
            // Punkt -> kleiner Stern -> grosser Stern -> kleiner -> Punkt.
            else sp.image.sprite = GameHudSkin.MenuSparkle(u < 0.2f || u > 0.85f ? 0 : u < 0.4f || u > 0.65f ? 1 : 2);
            sparkles[i] = sp;
        }
    }

    private void SpawnSparkle(int index, bool burst)
    {
        for (int i = 0; i < sparkles.Length; i++)
        {
            if (sparkles[i].image.enabled) continue;

            // Auf dem Goldrahmen: meist Ober- oder Unterkante, sonst die Seiten.
            Vector2 c = items[index].rect.anchoredPosition;
            float hw = plateSize.x * 0.5f;
            float top = plateSize.y * 0.5f - 1f, bottom = plateSize.y * 0.5f - 19f;
            float x, y;
            if (UnityEngine.Random.value < 0.75f)
            {
                x = UnityEngine.Random.Range(-hw + 6f, hw - 6f);
                y = UnityEngine.Random.value < 0.6f ? top : bottom;
            }
            else
            {
                x = (UnityEngine.Random.value < 0.5f ? -1f : 1f) * hw;
                y = UnityEngine.Random.Range(bottom + 3f, top - 3f);
            }
            if (burst)
            {
                x += UnityEngine.Random.Range(-3f, 3f);
                y += UnityEngine.Random.Range(-3f, 3f);
            }

            // 5x5: Mitte auf halbem Pixel.
            sparkles[i].image.rectTransform.anchoredPosition =
                new Vector2(Mathf.Round(c.x + x) + 0.5f, Mathf.Round(c.y + y) + 0.5f);
            sparkles[i].image.sprite = GameHudSkin.MenuSparkle(0);
            sparkles[i].image.enabled = true;
            sparkles[i].age = 0f;
            sparkles[i].life = burst ? UnityEngine.Random.Range(0.35f, 0.55f) : UnityEngine.Random.Range(0.45f, 0.7f);
            return;
        }
    }

    // ---------- 4. Rauten links und rechts vom gewählten Eintrag ----------
    private void AnimateDiamonds(float time, float dt, int active)
    {
        if (active >= 0)
        {
            Item item = items[active];
            Vector2 pos = item.rect.anchoredPosition;
            float halfWidth = showPlates
                ? plateSize.x * 0.5f + diamondGap
                : item.textWidth * item.scale * 0.5f + diamondGap;

            // Frisch eingeblendet: direkt hinspringen statt quer durch die Box zu fliegen.
            if (diamondAlpha < 0.05f)
            {
                diamondX = pos.x;
                diamondY = pos.y;
                diamondHalfWidth = halfWidth;
            }

            diamondX = Damp(diamondX, pos.x, diamondFollow, dt);
            diamondY = Damp(diamondY, pos.y, diamondFollow, dt);
            diamondHalfWidth = Damp(diamondHalfWidth, halfWidth, diamondFollow, dt);
        }

        diamondAlpha = Damp(diamondAlpha, active >= 0 ? 1f : 0f, diamondFollow, dt);

        float pulse = 1f + Mathf.Sin(time * pulseSpeed) * pulseAmount;
        Color c = diamondColor;
        c.a = diamondAlpha;

        diamondLeft.anchoredPosition = new Vector2(diamondX - diamondHalfWidth, diamondY);
        diamondRight.anchoredPosition = new Vector2(diamondX + diamondHalfWidth, diamondY);
        diamondLeft.localScale = diamondRight.localScale = Vector3.one * pulse;
        diamondLeftImage.color = diamondRightImage.color = c;
    }

    // ---------- Hintergrund-Partikel ----------
    private void AnimateParticles(float time, float dt)
    {
        float halfW = (boxSize.x - 2f) * 0.5f;
        float halfH = (boxSize.y - 2f) * 0.5f;

        for (int i = 0; i < particles.Count; i++)
        {
            Particle p = particles[i];
            p.y += p.speed * dt;
            if (p.y > halfH)
            {
                p.y = -halfH;
                p.x0 = UnityEngine.Random.Range(-halfW, halfW);
            }

            float x = p.x0 + Mathf.Sin(time + p.phase) * p.drift;
            // Oben und unten weich ein-/ausblenden statt hart aufzutauchen.
            float fade = Mathf.Clamp01((halfH - Mathf.Abs(p.y)) / 8f);

            p.image.rectTransform.anchoredPosition = new Vector2(Mathf.Round(x) + 0.5f, Mathf.Round(p.y) + 0.5f);
            p.image.color = new Color(1f, 1f, 1f, p.baseAlpha * fade);
            particles[i] = p;
        }
    }

    // ==================================================================
    //  Helfer
    // ==================================================================

    /// <summary>lerp(a, b, dt * k) aus dem Video, nur bildratenunabhängig.</summary>
    private static float Damp(float a, float b, float speed, float dt)
    {
        return Mathf.Lerp(a, b, 1f - Mathf.Exp(-speed * dt));
    }

    /// <summary>back_out: schießt kurz übers Ziel hinaus und federt zurück.</summary>
    private static float BackOut(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }
}
