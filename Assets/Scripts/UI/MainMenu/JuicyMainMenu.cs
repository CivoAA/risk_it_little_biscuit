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
        public string label;
        public UnityEvent onClick = new UnityEvent();
    }

    [Header("Einträge (oben nach unten)")]
    [SerializeField] private List<Entry> entries = new List<Entry>();

    [Header("Box")]
    [SerializeField, Tooltip("Aus = keine graue Box, nur die Einträge direkt auf dem Hintergrund " +
                             "(dunkle Schrift, keine Partikel). An = graue Box mit heller Schrift.")]
    private bool showBox = false;
    [SerializeField, Tooltip("An = jeder Eintrag sitzt auf einer Button-Platte (btuuon), beim Hover die helle Platte.")]
    private bool showPlates = true;
    [SerializeField] private Sprite plateSprite;
    [SerializeField] private Sprite plateHoverSprite;
    [SerializeField, Tooltip("Mitte der Box relativ zur Canvas-Mitte.")]
    private Vector2 boxCenter = new Vector2(0f, 4f);
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
    [SerializeField, Tooltip("Leer = die Pixel-Font des Projekts wird gesucht.")]
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
    [SerializeField, Tooltip("Seitlicher Versatz des gewählten Eintrags.")]
    private float hoverOffset = -2f;
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
        public float textWidth;
        public float baseY;
        public float scale = 1f;
        public float offset;
        public float underlineWidth;
        public float colorT;
        public float alpha = 1f;
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
    private float elapsed;
    private int hoveredIndex = -1;
    private float dimT;
    private float diamondX, diamondY, diamondHalfWidth, diamondAlpha;

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
        showPlates &= plateSprite != null;
        textColor = showPlates ? plateTextColor : showBox ? boxTextColor : plainTextColor;
        hoverColor = showPlates ? plateHoverColor : showBox ? boxHoverColor : plainHoverColor;
        // Die Rauten sitzen neben dem Eintrag auf dem Hintergrund, nicht auf der Platte.
        diamondColor = showBox ? boxHoverColor : plainHoverColor;
        Build();
    }

    private void Build()
    {
        root = PixelUI.Rect("JuicyMenu", transform, Vector2.zero, Vector2.zero);

        // Breite aus dem längsten Text: Platz für Punch-Größe plus Rauten links und rechts.
        float widest = 0f;
        foreach (Entry e in entries) widest = Mathf.Max(widest, MeasureText(e.label));
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
        diamondLeft = BuildDiamond("DiamondL", list, out diamondLeftImage);
        diamondRight = BuildDiamond("DiamondR", list, out diamondRightImage);

        for (int i = 0; i < entries.Count; i++) items.Add(BuildItem(i, list, inner.x));
        LinkNavigation();

    }

    /// <summary>
    /// Platten in Originalgröße, wenn der längste Text reinpasst - sonst in die
    /// Breite gezogen (9-Slice, Rahmen bleibt 1:1). Nie skaliert: gestreckte
    /// Pixelgrafik verschmiert.
    /// </summary>
    private void BuildPlateSprites(float widest, ref int boxWidth)
    {
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
                                       fontSize, Color.clear, TextAlignmentOptions.Center, font);
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

        if (showPlates)
        {
            item.plate = PixelUI.Panel("Plate", item.rect, plateSize, Vector2.zero, Color.white);
            item.plate.sprite = plateNormal;
            item.plate.type = plateNormal == plateSprite ? Image.Type.Simple : Image.Type.Sliced;
        }

        item.visual = PixelUI.Rect("Visual", item.rect, new Vector2(width, rowStep), Vector2.zero);
        item.label = PixelUI.Label("Label", item.visual, new Vector2(width, rowStep), Vector2.zero, entry.label,
                                   fontSize, textColor, TextAlignmentOptions.Center, font);
        item.label.textWrappingMode = TextWrappingModes.NoWrap;
        item.label.ForceMeshUpdate();
        item.textWidth = item.label.GetPreferredValues(entry.label).x;

        // Unterstrich knapp unter der Grundlinie.
        float baseline = item.label.textInfo.lineCount > 0
            ? item.label.textInfo.lineInfo[0].baseline
            : -fontSize * 0.5f;
        item.underlineImage = PixelUI.Panel("Underline", item.visual, new Vector2(0f, 1f),
                                            new Vector2(0f, Mathf.Round(baseline) - 1.5f), textColor);
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

    private void OnDestroy()
    {
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

    private static bool OverlayOpen => OptionsPanel.IsOpen || AchievementPanel.IsOpen;

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
        AnimateItems(t, time, dt, active);
        AnimateDiamonds(time, dt, active);
        AnimateParticles(time, dt);
    }

    // ---------- 1. Box aufpoppen ----------
    private void AnimateBox(float t)
    {
        float p = Mathf.Clamp01(t / boxPopTime);
        menuGroup.alpha = p;
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
            if (item.plate != null) item.plate.sprite = isActive ? platePressed : plateNormal;
            item.visual.localScale = Vector3.one * item.scale;

            // ---------- Wachsender Unterstrich ----------
            item.underlineWidth = Damp(item.underlineWidth, isActive ? item.textWidth + underlinePadding : 0f,
                                       underlineSpeed, dt);
            item.underline.sizeDelta = new Vector2(Mathf.Round(item.underlineWidth), 1f);

            // ---------- Farbe: leicht blau für den gewählten, abgedunkelt für den Rest ----------
            item.colorT = Damp(item.colorT, isActive ? 1f : 0f, 12f, dt);
            float targetAlpha = isActive ? 1f : Mathf.Lerp(1f, showPlates ? plateDimAlpha : dimAlpha, dimT);
            item.alpha = Damp(item.alpha, targetAlpha, dimSpeed, dt);

            Color c = Color.Lerp(textColor, hoverColor, item.colorT);
            c.a = item.alpha;
            item.label.color = c;
            item.underlineImage.color = c;
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
