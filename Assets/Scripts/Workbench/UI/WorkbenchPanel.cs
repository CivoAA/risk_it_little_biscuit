using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Die Werkbank: der Verteiler-Bildschirm, an dem der Spieler vor dem Lauf
/// zusammenstellt, WORAUS beim Level-Up gezogen wird.
///
///   WorkbenchPanel.Toggle();
///
/// Baut sich komplett per Code auf - kein Szenenobjekt, kein Prefab, keine
/// Verdrahtung im Inspector, genau wie <see cref="AchievementsBookPanel"/>
/// und <see cref="HubShopUI"/>. Damit laesst es sich aus jeder Szene oeffnen.
///
/// Die Grafik kommt aus Assets/Resources/Workbench/ui/ (siehe
/// Assets/Art/UI_Objects/Workbench/WORKBENCH_UI.md - dort stehen alle Masse,
/// Textzonen und 9-Slice-Raender, nach denen hier gerechnet wird). Jede Zahl
/// in diesem Skript ist ein Pixel im 320x180-Raster, Ursprung oben links.
/// Landet etwas auf einem halben Pixel, verschmiert es beim Hochskalieren.
///
/// Inhalt kommt aus <see cref="WeaponCatalog"/>, der Zustand aus
/// <see cref="Loadout"/> - dieses Skript kennt keine einzige Waffe beim Namen.
/// </summary>
public class WorkbenchPanel : MonoBehaviour
{
    // ==================================================================
    //  Masse (alle aus WORKBENCH_UI.md)
    // ==================================================================

    private const int RefW = 320;
    private const int RefH = 180;

    private const string UiPath = "Workbench/ui/";

    // Kopfzeile
    private static readonly Vector4 Title   = new Vector4(9f, 14f, 140f, 9f);
    private static readonly Vector4 BtnBack = new Vector4(259f, 12f, 52f, 14f);

    // Verteiler-Board (Sprite ist 167x80: 165x78 plus 2px Schlagschatten)
    private static readonly Vector4 Parchment = new Vector4(9f, 26f, 167f, 80f);

    private const float GroupDotX = 13f;
    private const float GroupTextX = 20f, GroupTextW = 80f;
    private const float GroupCountX = 110f, GroupCountW = 60f;
    private const float RuleX = 13f, RuleW = 158f;

    private const float HeadWeaponsY = 29f, RuleWeaponsY = 39f, GridWeaponsY = 42f;
    private const float HeadBuffsY = 74f, RuleBuffsY = 84f, GridBuffsY = 86f;

    private const float SlotSize = 14f, SlotStep = 16f;
    private const float GridX = 13f;
    private const int GridCols = 10;

    // Evo-Leiste (Sprite ist 167x42)
    private static readonly Vector4 EvoBar = new Vector4(9f, 106f, 167f, 42f);
    private const float EvoHeadY = 108f;
    private static readonly Vector4 EvoAllButton = new Vector4(142f, 108f, 28f, 10f);

    private const float ChipX = 13f, ChipY = 120f, ChipStep = 40f;
    private const float ChipW = 38f, ChipH = 24f;
    private const int ChipCount = 4;
    private const float EvoSlot = 10f;
    private static readonly float[] ChipSlotDX = { 1f, 14f, 27f };
    private static readonly float[] ChipSignDX = { 11f, 24f };
    private const float ChipSlotDY = 2f, ChipSignDY = 5f;

    // Knopfzeile
    private static readonly Vector4 BtnClear = new Vector4(9f, 150f, 34f, 14f);
    private static readonly Vector4 BtnRandom = new Vector4(47f, 150f, 34f, 14f);
    private static readonly Vector4 BtnApply = new Vector4(84f, 150f, 90f, 14f);

    // Rechte Spalte
    private const float TabX = 180f, TabY = 30f, TabW = 40f, TabH = 12f, TabStep = 42f;
    private static readonly Vector4 Library = new Vector4(180f, 42f, 133f, 124f);
    private static readonly Vector4 LibHead = new Vector4(186f, 46f, 119f, 9f);
    private static readonly Vector4 LibView = new Vector4(186f, 57f, 119f, 62f);
    private const float LibGridDX = 4f;
    private const int LibCols = 7;
    private static readonly Vector4 InfoBox  = new Vector4(186f, 128f, 119f, 30f);
    private static readonly Vector4 InfoName = new Vector4(190f, 131f, 111f, 9f);
    private static readonly Vector4 InfoDesc = new Vector4(190f, 141f, 111f, 16f);

    // Evo-Overlay
    private static readonly Vector4 Overlay      = new Vector4(180f, 42f, 131f, 122f);
    private static readonly Vector4 OverlayTitle = new Vector4(186f, 46f, 100f, 9f);
    private static readonly Vector4 BtnClose     = new Vector4(293f, 45f, 14f, 10f);
    private static readonly Vector4 OverlayView  = new Vector4(186f, 60f, 119f, 98f);
    private const float RowW = 119f, RowH = 18f, RowStep = 20f;
    private static readonly float[] RowSlotDX = { 2f, 17f, 32f };
    private static readonly float[] RowSignDX = { 13f, 28f };
    private const float RowSlotDY = 4f, RowSignDY = 7f;
    private const float RowTextX = 46f, RowTextW = 58f;
    private const float RowStateX = 104f, RowStateW = 13f;

    private const float SizeTitle = 10f, SizeText = 8f, SizeSmall = 6f;

    // Palette
    private static readonly Color TextDark   = Hex(0x3b2b33);
    private static readonly Color TextMid    = Hex(0x6f4630);
    private static readonly Color TextDim    = Hex(0xb99772);
    private static readonly Color TextCream  = Hex(0xf2dcbc);
    private static readonly Color TextGold   = Hex(0xe8b93c);
    private static readonly Color TextGreen  = Hex(0x7fa65a);
    private static readonly Color TextOnGold = Hex(0x4d2e1e);
    private static readonly Color RuleColor  = Hex(0xd9b189);
    private static readonly Color DotWeapon  = Hex(0x8c5a3c);
    private static readonly Color DotBuff    = Hex(0x96384c);

    /// <summary>Was schon im Verteiler liegt, wird in der Bibliothek gedaempft.</summary>
    private static readonly Color IconUsedTint = new Color(0.45f, 0.45f, 0.45f, 1f);

    // ==================================================================
    //  Zustand
    // ==================================================================

    private static WorkbenchPanel instance;
    public static bool IsOpen => instance != null;

    private TMP_FontAsset font;
    private PoolKind tab = PoolKind.Weapon;
    private bool overlayOpen;

    // Geoeffnet wird mit [E], geschlossen auch - im Frame des Oeffnens darf die
    // Taste darum nicht noch einmal zaehlen.
    private int openedFrame;

    private readonly List<PoolSlot> weaponSlots = new List<PoolSlot>();
    private readonly List<PoolSlot> buffSlots = new List<PoolSlot>();
    private readonly List<LibTile> libTiles = new List<LibTile>();
    private readonly List<EvoChip> chips = new List<EvoChip>();
    private readonly List<EvoRow> rows = new List<EvoRow>();

    private RectTransform page;
    private RectTransform libContent, overlayContent;
    private ScrollRect libScroll, overlayScroll;
    private GameObject overlayRoot;

    private Image[] tabImages;
    private TMP_Text[] tabLabels;
    private TMP_Text countWeapons, countBuffs, evoTitle, libHead;
    private TMP_Text infoName, infoDesc, applyLabel;
    private Image applyImage, applyHover;
    private Button applyButton;

    private WeaponDef inspected;
    private HubUI hub;

    /// <summary>
    /// Fuer welchen Charakter die Bibliothek gebaut wurde. Die Reihenfolge
    /// haengt an seiner Startwaffe (<see cref="LockedFirst"/>), ein Wechsel
    /// unter dem offenen Fenster muss das Raster also neu legen.
    /// </summary>
    private int libCharacter = -1;

    /// <summary>Ein Platz im Verteiler. Leer, solange <see cref="Def"/> null ist.</summary>
    private class PoolSlot
    {
        public Image Frame;
        public Image Icon;
        public Image Hover;
        public WeaponDef Def;

        /// <summary>
        /// Der erste Waffenplatz: dort liegt die Standardwaffe des Charakters,
        /// sie laesst sich nicht herausnehmen.
        /// </summary>
        public bool Locked;
    }

    /// <summary>Eine Kachel in der Bibliothek.</summary>
    private class LibTile
    {
        public GameObject Root;
        public Image Frame;
        public Image Icon;
        public Image Hover;
        public WeaponDef Def;
    }

    /// <summary>Ein Evo-Chip in der Leiste: Zutat + Zutat = Ergebnis.</summary>
    private class EvoChip
    {
        public GameObject Root;
        public Image Frame;
        public Image[] Icons = new Image[3];
        public TMP_Text Label;
        public EvoDef Def;
    }

    /// <summary>Eine Zeile im Evo-Overlay.</summary>
    private class EvoRow
    {
        public GameObject Root;
        public Image Frame;
        public Image[] Icons = new Image[3];
        public TMP_Text Name;
        public TMP_Text Sub;
        public TMP_Text State;
        public EvoDef Def;
    }

    /// <summary>
    /// Schaltet den Hover-Rahmen und meldet, worueber die Maus steht. Eigene
    /// Komponente statt eines EventTriggers - das ist billiger und liest sich
    /// besser.
    /// </summary>
    private class Hoverable : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Image Target;
        public Action<bool> Notify;

        /// <summary>
        /// Null heisst: immer. Sonst entscheidet die Bedingung - ein grauer
        /// Knopf soll nicht aufleuchten, als liesse sich etwas anklicken.
        /// </summary>
        public Func<bool> CanHover;

        public void OnPointerEnter(PointerEventData e)
        {
            if (CanHover != null && !CanHover()) return;
            if (Target != null) Target.enabled = true;
            Notify?.Invoke(true);
        }

        public void OnPointerExit(PointerEventData e)
        {
            if (Target != null) Target.enabled = false;
            Notify?.Invoke(false);
        }

        private void OnDisable()
        {
            if (Target != null) Target.enabled = false;
        }
    }

    // ==================================================================
    //  Oeffnen / Schliessen
    // ==================================================================

    public static void Open()
    {
        if (instance != null) return;
        GameObject go = new GameObject("WorkbenchPanel");
        instance = go.AddComponent<WorkbenchPanel>();
    }

    public static void Close()
    {
        if (instance == null) return;
        Destroy(instance.gameObject);
        instance = null;
    }

    public static void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    /// <summary>
    /// Liest den Verteiler neu, falls das Fenster gerade offen ist. Fuer
    /// Aenderungen, die kein Ereignis feuern - etwa aus der Cheat-Konsole.
    /// </summary>
    public static void RefreshIfOpen()
    {
        if (instance != null) instance.Refresh();
    }

    private void Awake()
    {
        instance = this;
        openedFrame = Time.frameCount;
        font = FindFont();

        // Der Charakter kann seit dem letzten Mal gewechselt haben - dann
        // gehoert hier der Verteiler des neuen her, mit seiner Standardwaffe
        // auf dem ersten Platz.
        Loadout.SyncCharacter();

        Build();
        BlockHub(true);
    }

    private void OnEnable()
    {
        Loc.LanguageChanged += Rebuild;
        Loadout.Changed += Refresh;
    }

    private void OnDisable()
    {
        Loc.LanguageChanged -= Rebuild;
        Loadout.Changed -= Refresh;
    }

    private void OnDestroy()
    {
        BlockHub(false);
        if (instance == this) instance = null;
    }

    /// <summary>
    /// Sperrt den Hub, solange das Fenster offen ist - so wie Shop, Skilltree
    /// und das Erfolge-Buch es tun. Laeuft das Fenster woanders, passiert
    /// nichts und es wird auch kein HubUI angelegt.
    /// </summary>
    private void BlockHub(bool blocked)
    {
        if (blocked)
        {
            hub = FindAnyObjectByType<HubUI>();
            if (hub == null) return;

            HubUI.PushModal();
            hub.SetPlayerFrozen(true);
            return;
        }

        if (hub == null) return;

        HubUI.PopModal();
        hub.SetPlayerFrozen(false);
        hub = null;
    }

    // ==================================================================
    //  Eingabe
    // ==================================================================

    private void Update()
    {
        // Der Tastendruck, der das Fenster aufgemacht hat, darf es nicht gleich
        // wieder zumachen.
        if (Time.frameCount == openedFrame) return;

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))
        {
            if (overlayOpen) SetOverlay(false);
            else Close();
            return;
        }

        if (overlayOpen) return;

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) StepTab(-1);
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) StepTab(1);
        if (Input.GetKeyDown(KeyCode.Tab)) StepTab(1);
    }

    private void StepTab(int delta)
    {
        int next = Mathf.Clamp((int)tab + delta, 0, 2);
        SetTab((PoolKind)next);
    }

    // ==================================================================
    //  Aufbau
    // ==================================================================

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 155;   // ueber Shop 130, Levelauswahl 135, Skilltree 140, Buch 150

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefW, RefH);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        gameObject.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();

        // Die Werkbankplatte fuellt den ganzen Bildschirm. Als 9-Slice, damit
        // die Rahmenleiste auch bei anderen Seitenverhaeltnissen am Rand klebt
        // und ihre Staerke behaelt - nur das Holz dazwischen wird gedehnt.
        Image bg = StretchImage("Backdrop", transform, Gfx("bg_workbench"));
        bg.type = Image.Type.Sliced;
        bg.raycastTarget = true;

        // Die Seite selbst bleibt immer exakt 320x180 und mittig, egal wie
        // breit der Bildschirm ist.
        page = Rect("Page", transform, 0f, 0f, RefW, RefH);
        page.anchorMin = page.anchorMax = page.pivot = new Vector2(0.5f, 0.5f);
        page.anchoredPosition = Vector2.zero;

        BuildHeader();
        BuildDistributor();
        BuildEvoBar();
        BuildButtons();
        BuildTabs();
        BuildLibrary();
        BuildOverlay();

        Rebuild();
    }

    // ---------- Kopfzeile ----------

    private void BuildHeader()
    {
        Label("Title", page, Title, Loc.Get("ui.workbench.title", "WERKBANK"),
              SizeTitle, TextCream, TextAlignmentOptions.Left);

        // Raus geht es mit E und Esc - aber nicht jeder probiert Tasten aus.
        // Die rechte Kante liegt buendig mit der Bibliothek darunter.
        MakeButton("BtnBack", BtnBack, "btn_back",
                   Loc.Get("ui.workbench.btn.back", "ZURÜCK"), TextCream, () =>
        {
            PlayClick();
            Close();
        });
    }

    // ---------- Verteiler ----------

    private void BuildDistributor()
    {
        Img("Parchment", page, Parchment, Gfx("panel_parchment"));

        BuildGroupHead("Weapons", HeadWeaponsY, RuleWeaponsY, DotWeapon,
                       Loc.Get("ui.workbench.group.weapons", "WAFFEN"), out countWeapons);
        BuildGroupHead("Buffs", HeadBuffsY, RuleBuffsY, DotBuff,
                       Loc.Get("ui.workbench.group.buffs", "BUFFS"), out countBuffs);

        BuildPoolGrid(weaponSlots, PoolKind.Weapon, GridWeaponsY, Loadout.WeaponSlots);
        BuildPoolGrid(buffSlots, PoolKind.Buff, GridBuffsY, Loadout.BuffSlots);
    }

    private void BuildGroupHead(string name, float y, float ruleY, Color dot, string text,
                                out TMP_Text counter)
    {
        Image square = Img($"Dot_{name}", page, new Vector4(GroupDotX, y + 2f, 4f, 4f), null);
        square.color = dot;

        Label($"Group_{name}", page, new Vector4(GroupTextX, y, GroupTextW, 9f),
              text, SizeText, TextDark, TextAlignmentOptions.Left);

        counter = Label($"Count_{name}", page, new Vector4(GroupCountX, y, GroupCountW, 9f),
                        "", SizeText, TextMid, TextAlignmentOptions.Right);

        Image rule = Img($"Rule_{name}", page, new Vector4(RuleX, ruleY, RuleW, 1f), null);
        rule.color = RuleColor;
    }

    private void BuildPoolGrid(List<PoolSlot> target, PoolKind kind, float y, int count)
    {
        for (int i = 0; i < count; i++)
        {
            // Platz 0 der Waffen gehoert der Standardwaffe des Charakters
            bool locked = kind == PoolKind.Weapon && i == 0;
            float x = GridX + (i % GridCols) * SlotStep;
            float sy = y + (i / GridCols) * SlotStep;

            PoolSlot slot = new PoolSlot { Locked = locked };
            slot.Frame = Img($"Slot_{kind}_{i}", page, new Vector4(x, sy, SlotSize, SlotSize),
                             Gfx("slot_empty"));
            slot.Frame.raycastTarget = true;

            slot.Icon = Img("Icon", slot.Frame.rectTransform, new Vector4(0f, 0f, SlotSize, SlotSize), null);
            slot.Icon.enabled = false;

            slot.Hover = Img("Hover", slot.Frame.rectTransform, new Vector4(0f, 0f, SlotSize, SlotSize),
                             Gfx("slot_highlight"));
            slot.Hover.enabled = false;

            Hoverable hover = slot.Frame.gameObject.AddComponent<Hoverable>();
            hover.Target = slot.Hover;
            PoolSlot captured = slot;
            hover.Notify = on => Inspect(on ? captured.Def : null);

            Button button = slot.Frame.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = slot.Frame;
            button.onClick.AddListener(() =>
            {
                if (captured.Def == null || captured.Locked) return;
                PlayClick();
                Loadout.Remove(captured.Def.Id);
            });

            target.Add(slot);
        }
    }

    // ---------- Evo-Leiste ----------

    private void BuildEvoBar()
    {
        Img("EvoBar", page, EvoBar, Gfx("panel_dark"));

        Image square = Img("Dot_Evo", page, new Vector4(GroupDotX, EvoHeadY + 2f, 4f, 4f), null);
        square.color = TextGreen;

        evoTitle = Label("EvoTitle", page, new Vector4(GroupTextX, EvoHeadY, GroupTextW, 9f),
                         "", SizeText, TextCream, TextAlignmentOptions.Left);

        Image allButton = Img("BtnEvoAll", page, EvoAllButton, Gfx("btn_small"));
        allButton.raycastTarget = true;
        Label("Label", allButton.rectTransform, new Vector4(2f, 2f, EvoAllButton.z - 4f, 7f),
              Loc.Get("ui.workbench.evo.all", "alle"), SizeSmall, TextDim,
              TextAlignmentOptions.Center);

        Button b = allButton.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.targetGraphic = allButton;
        b.onClick.AddListener(() =>
        {
            PlayClick();
            SetOverlay(!overlayOpen);
        });

        AddHover(allButton);

        for (int i = 0; i < ChipCount; i++)
        {
            float x = ChipX + i * ChipStep;

            EvoChip chip = new EvoChip();
            chip.Frame = Img($"Chip_{i}", page, new Vector4(x, ChipY, ChipW, ChipH),
                             Gfx("evo_chip_pending"));
            chip.Root = chip.Frame.gameObject;

            for (int k = 0; k < 3; k++)
            {
                Img($"Slot_{k}", chip.Frame.rectTransform,
                    new Vector4(ChipSlotDX[k], ChipSlotDY, EvoSlot, EvoSlot), Gfx("slot_evo"));

                chip.Icons[k] = Img($"Icon_{k}", chip.Frame.rectTransform,
                                    new Vector4(ChipSlotDX[k], ChipSlotDY, EvoSlot, EvoSlot), null);
                chip.Icons[k].enabled = false;
            }

            for (int k = 0; k < 2; k++)
            {
                Img($"Sign_{k}", chip.Frame.rectTransform,
                    new Vector4(ChipSignDX[k], ChipSignDY, 3f, 3f),
                    Gfx(k == 0 ? "sign_plus" : "sign_equals"));
            }

            chip.Label = Label("Name", chip.Frame.rectTransform,
                               new Vector4(2f, 13f, ChipW - 4f, 9f), "",
                               SizeSmall, TextDim, TextAlignmentOptions.Center);
            // 34px sind fuer "Explosivstern" zu wenig. Lieber abgeschnitten als
            // ueber den Chiprand hinaus - den ganzen Namen zeigt das Overlay.
            chip.Label.overflowMode = TextOverflowModes.Ellipsis;

            chips.Add(chip);
        }
    }

    // ---------- Knopfzeile ----------

    private void BuildButtons()
    {
        MakeButton("BtnClear", BtnClear, "btn_narrow",
                   Loc.Get("ui.workbench.btn.clear", "LEEREN"), TextCream, () =>
        {
            PlayClick();
            Loadout.Clear();
        });

        MakeButton("BtnRandom", BtnRandom, "btn_narrow",
                   Loc.Get("ui.workbench.btn.random", "ZUFÄLLIG"), TextCream, () =>
        {
            PlayClick();
            Loadout.FillRandom();
        });

        applyImage = Img("BtnApply", page, BtnApply, Gfx("btn_primary"));
        applyImage.raycastTarget = true;

        applyLabel = Label("Label", applyImage.rectTransform,
                           new Vector4(2f, 2f, BtnApply.z - 4f, 10f), "",
                           SizeText, TextOnGold, TextAlignmentOptions.Center);

        applyButton = applyImage.gameObject.AddComponent<Button>();
        applyButton.transition = Selectable.Transition.None;
        applyButton.targetGraphic = applyImage;
        applyButton.onClick.AddListener(() =>
        {
            PlayClick();
            Loadout.Apply();
        });

        applyHover = AddHover(applyImage, () => Loadout.IsComplete);
    }

    private Image MakeButton(string name, Vector4 rect, string sprite, string text,
                             Color color, Action onClick)
    {
        Image img = Img(name, page, rect, Gfx(sprite));
        img.raycastTarget = true;

        Label("Label", img.rectTransform, new Vector4(2f, 2f, rect.z - 4f, 10f),
              text, SizeText, color, TextAlignmentOptions.Center);

        Button b = img.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.targetGraphic = img;
        b.onClick.AddListener(() => onClick());

        AddHover(img);
        return img;
    }

    /// <summary>
    /// Legt den Maus-Rahmen ueber einen Knopf oder Reiter: dieselbe goldene
    /// 1px-Kante, die auch die Kacheln benutzen. Ein Fenster, eine Sprache.
    ///
    /// Das Sprite ist 6x6 und wird als 9-Slice auf jede Knopfgroesse gezogen -
    /// bei PPU 100 bleibt die Kante dabei genau einen Pixel breit. Es liegt
    /// ueber dem Knopf statt sein Sprite zu tauschen, denn welches Sprite ein
    /// Knopf traegt, entscheidet Refresh() - ein getauschtes waere beim
    /// naechsten Auffrischen wieder weg.
    /// </summary>
    private Image AddHover(Image target, Func<bool> canHover = null)
    {
        RectTransform rt = NewRect("Hover", target.rectTransform);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        Image frame = rt.gameObject.AddComponent<Image>();
        frame.sprite = Gfx("hover_frame");
        frame.type = Image.Type.Sliced;
        frame.raycastTarget = false;
        frame.enabled = false;

        Hoverable hover = target.gameObject.AddComponent<Hoverable>();
        hover.Target = frame;
        hover.CanHover = canHover;
        return frame;
    }

    // ---------- Reiter ----------

    private void BuildTabs()
    {
        tabImages = new Image[3];
        tabLabels = new TMP_Text[3];

        string[] names =
        {
            Loc.Get("ui.workbench.tab.weapons", "WAFFEN"),
            Loc.Get("ui.workbench.tab.buffs", "BUFFS"),
            Loc.Get("ui.workbench.tab.evos", "EVOS"),
        };

        for (int i = 0; i < 3; i++)
        {
            Image img = Img($"Tab_{i}", page, new Vector4(TabX + i * TabStep, TabY, TabW, TabH),
                            Gfx("tab_inactive"));
            img.raycastTarget = true;
            tabImages[i] = img;

            tabLabels[i] = Label("Label", img.rectTransform, new Vector4(2f, 2f, TabW - 4f, 9f),
                                 names[i], SizeText, TextCream, TextAlignmentOptions.Center);

            Button button = img.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = img;

            PoolKind captured = (PoolKind)i;
            button.onClick.AddListener(() =>
            {
                PlayClick();
                SetTab(captured);
            });

            // Der aktive Reiter hebt sich schon durch sein Sprite ab - ein
            // Rahmen darauf sagte nur noch einmal dasselbe.
            AddHover(img, () => tab != captured);
        }
    }

    // ---------- Bibliothek ----------

    private void BuildLibrary()
    {
        Img("Library", page, Library, Gfx("panel_library"));

        libHead = Label("LibHead", page, LibHead, "", SizeSmall, TextDim, TextAlignmentOptions.Left);

        RectTransform view = Rect("LibView", page, LibView.x, LibView.y, LibView.z, LibView.w);
        view.gameObject.AddComponent<RectMask2D>();
        CatchScroll(view);

        libScroll = view.gameObject.AddComponent<ScrollRect>();
        libScroll.horizontal = false;
        libScroll.movementType = ScrollRect.MovementType.Clamped;
        libScroll.scrollSensitivity = SlotStep;
        libScroll.viewport = view;

        libContent = Rect("Content", view, 0f, 0f, LibView.z, LibView.w);
        libScroll.content = libContent;

        Img("InfoBox", page, InfoBox, Gfx("info_box"));
        infoName = Label("InfoName", page, InfoName, "", SizeText, TextCream,
                         TextAlignmentOptions.Left);
        infoDesc = Label("InfoDesc", page, InfoDesc, "", SizeSmall, TextDim,
                         TextAlignmentOptions.TopLeft);
        infoDesc.textWrappingMode = TextWrappingModes.Normal;
    }

    /// <summary>
    /// Zieht die Standardwaffe des Charakters an den Anfang der Liste. In der
    /// Bibliothek steht sie damit auf derselben Stelle wie im Verteiler: ganz
    /// vorne. Ist keine dabei - anderer Reiter, Startwaffe nicht im Katalog -
    /// bleibt die Katalogreihenfolge, wie sie ist.
    /// </summary>
    private static void LockedFirst(List<WeaponDef> defs)
    {
        for (int i = 1; i < defs.Count; i++)
        {
            if (!Loadout.IsLocked(defs[i].Id)) continue;

            WeaponDef locked = defs[i];
            defs.RemoveAt(i);
            defs.Insert(0, locked);
            return;
        }
    }

    /// <summary>
    /// Legt das Raster fuer den aktuellen Reiter neu an. Die Kachelzahl
    /// wechselt mit dem Reiter, darum wird hier wirklich abgerissen und neu
    /// gebaut statt nur umgefaerbt.
    /// </summary>
    private void FillLibrary()
    {
        foreach (LibTile t in libTiles)
        {
            if (t.Root == null) continue;
            // Destroy raeumt erst am Frameende auf. Ohne das Abschalten laegen
            // die alten Kacheln diesen einen Frame lang unter den neuen.
            t.Root.SetActive(false);
            Destroy(t.Root);
        }
        libTiles.Clear();

        List<WeaponDef> defs = WeaponCatalog.OfKind(tab);
        LockedFirst(defs);
        libCharacter = Loadout.CurrentCharacter;
        int rowsCount = Mathf.CeilToInt(defs.Count / (float)LibCols);
        libContent.sizeDelta = new Vector2(LibView.z, Mathf.Max(LibView.w, rowsCount * SlotStep));
        libContent.anchoredPosition = Vector2.zero;

        for (int i = 0; i < defs.Count; i++)
        {
            WeaponDef def = defs[i];
            float x = LibGridDX + (i % LibCols) * SlotStep;
            float y = (i / LibCols) * SlotStep;

            LibTile t = new LibTile { Def = def };
            t.Frame = Img($"Lib_{def.Id}", libContent, new Vector4(x, y, SlotSize, SlotSize), null);
            t.Frame.raycastTarget = true;
            t.Root = t.Frame.gameObject;

            t.Icon = Img("Icon", t.Frame.rectTransform, new Vector4(0f, 0f, SlotSize, SlotSize),
                         def.Icon);
            // Ein Image ohne Sprite malt ein weisses Rechteck - lieber nichts
            t.Icon.enabled = def.Icon != null;

            t.Hover = Img("Hover", t.Frame.rectTransform, new Vector4(0f, 0f, SlotSize, SlotSize),
                          Gfx("slot_highlight"));
            t.Hover.enabled = false;

            Hoverable hover = t.Frame.gameObject.AddComponent<Hoverable>();
            hover.Target = t.Hover;
            hover.Notify = on => Inspect(on ? def : null);

            Button button = t.Frame.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = t.Frame;
            button.onClick.AddListener(() =>
            {
                // Evos kann man nicht verteilen - die entstehen aus ihren Zutaten.
                // Die Standardwaffe liegt fest im Verteiler: Loadout.Remove
                // sagt ohnehin nein - dann soll es auch nicht klacken, als
                // waere etwas passiert.
                if (def.Kind == PoolKind.Evo || Loadout.IsLocked(def.Id)) return;
                PlayClick();
                Loadout.Toggle(def.Id);
            });

            libTiles.Add(t);
        }
    }

    // ---------- Evo-Overlay ----------

    private void BuildOverlay()
    {
        overlayRoot = NewRect("EvoOverlay", page).gameObject;
        RectTransform root = (RectTransform)overlayRoot.transform;
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0f, 1f);
        root.sizeDelta = new Vector2(RefW, RefH);
        root.anchoredPosition = Vector2.zero;

        Image bg = Img("Sheet", root, Overlay, Gfx("overlay_evo"));
        bg.raycastTarget = true;

        Label("Title", root, OverlayTitle,
              Loc.Get("ui.workbench.overlay.title", "ALLE EVOLUTIONEN"),
              SizeText, TextCream, TextAlignmentOptions.Left);

        Image close = Img("BtnClose", root, BtnClose, Gfx("btn_close"));
        close.raycastTarget = true;
        Label("X", close.rectTransform, new Vector4(0f, 1f, BtnClose.z, 8f), "x",
              SizeText, TextCream, TextAlignmentOptions.Center);

        Button cb = close.gameObject.AddComponent<Button>();
        cb.transition = Selectable.Transition.None;
        cb.targetGraphic = close;
        cb.onClick.AddListener(() =>
        {
            PlayClick();
            SetOverlay(false);
        });

        AddHover(close);

        RectTransform view = Rect("View", root, OverlayView.x, OverlayView.y,
                                  OverlayView.z, OverlayView.w);
        view.gameObject.AddComponent<RectMask2D>();
        CatchScroll(view);

        overlayScroll = view.gameObject.AddComponent<ScrollRect>();
        overlayScroll.horizontal = false;
        overlayScroll.movementType = ScrollRect.MovementType.Clamped;
        overlayScroll.scrollSensitivity = RowStep;
        overlayScroll.viewport = view;

        overlayContent = Rect("Content", view, 0f, 0f, OverlayView.z, OverlayView.w);
        overlayScroll.content = overlayContent;

        int count = WeaponCatalog.Evos.Count;
        overlayContent.sizeDelta = new Vector2(OverlayView.z,
                                               Mathf.Max(OverlayView.w, count * RowStep));

        for (int i = 0; i < count; i++)
        {
            EvoRow row = new EvoRow();
            row.Frame = Img($"Row_{i}", overlayContent, new Vector4(0f, i * RowStep, RowW, RowH),
                            Gfx("evo_row_pending"));
            row.Root = row.Frame.gameObject;

            for (int k = 0; k < 3; k++)
            {
                Img($"Slot_{k}", row.Frame.rectTransform,
                    new Vector4(RowSlotDX[k], RowSlotDY, EvoSlot, EvoSlot), Gfx("slot_evo"));

                row.Icons[k] = Img($"Icon_{k}", row.Frame.rectTransform,
                                   new Vector4(RowSlotDX[k], RowSlotDY, EvoSlot, EvoSlot), null);
                row.Icons[k].enabled = false;
            }

            for (int k = 0; k < 2; k++)
            {
                Img($"Sign_{k}", row.Frame.rectTransform,
                    new Vector4(RowSignDX[k], RowSignDY, 3f, 3f),
                    Gfx(k == 0 ? "sign_plus" : "sign_equals"));
            }

            row.Name = Label("Name", row.Frame.rectTransform,
                             new Vector4(RowTextX, 2f, RowTextW, 9f), "",
                             SizeText, TextCream, TextAlignmentOptions.Left);
            row.Sub = Label("Sub", row.Frame.rectTransform,
                            new Vector4(RowTextX, 10f, RowTextW, 7f), "",
                            SizeSmall, TextDim, TextAlignmentOptions.Left);
            row.Sub.overflowMode = TextOverflowModes.Ellipsis;
            row.State = Label("State", row.Frame.rectTransform,
                              new Vector4(RowStateX, 5f, RowStateW, 9f), "",
                              SizeSmall, TextDim, TextAlignmentOptions.Right);

            rows.Add(row);
        }

        overlayRoot.SetActive(false);
    }

    private void SetOverlay(bool open)
    {
        overlayOpen = open;
        overlayRoot.SetActive(open);
        if (open)
        {
            overlayContent.anchoredPosition = Vector2.zero;
            RefreshOverlay();
        }
    }

    // ==================================================================
    //  Auffrischen
    // ==================================================================

    private void SetTab(PoolKind next)
    {
        if (tab == next) return;
        tab = next;
        ClearInspect();
        FillLibrary();
        Refresh();
    }

    /// <summary>Kompletter Neuaufbau der Inhalte - nach Sprachwechsel und beim Start.</summary>
    private void Rebuild()
    {
        FillLibrary();
        ClearInspect();
        Refresh();
    }

    private void Refresh()
    {
        if (libCharacter != Loadout.CurrentCharacter) FillLibrary();

        RefreshCounters();
        RefreshPool(weaponSlots, PoolKind.Weapon);
        RefreshPool(buffSlots, PoolKind.Buff);
        RefreshLibrary();
        RefreshTabs();
        RefreshEvoBar();
        RefreshApply();
        if (overlayOpen) RefreshOverlay();
    }

    private void RefreshCounters()
    {
        string format = Loc.Get("ui.workbench.counter", "{0} / {1}");

        countWeapons.text = string.Format(format, Loadout.Count(PoolKind.Weapon),
                                          Loadout.RequiredWeapons);
        countWeapons.color = Loadout.Count(PoolKind.Weapon) >= Loadout.RequiredWeapons
            ? TextGreen : TextMid;

        countBuffs.text = string.Format(format, Loadout.Count(PoolKind.Buff),
                                        Loadout.RequiredBuffs);
        countBuffs.color = Loadout.Count(PoolKind.Buff) >= Loadout.RequiredBuffs
            ? TextGreen : TextMid;

    }

    private void RefreshPool(List<PoolSlot> slots, PoolKind kind)
    {
        List<string> ids = Loadout.Get(kind);

        for (int i = 0; i < slots.Count; i++)
        {
            PoolSlot slot = slots[i];
            WeaponDef def = i < ids.Count ? WeaponCatalog.Find(ids[i]) : null;
            slot.Def = def;

            if (def == null)
            {
                slot.Frame.sprite = Gfx("slot_empty");
                slot.Icon.enabled = false;
                continue;
            }

            slot.Frame.sprite = Gfx(slot.Locked ? "slot_locked"
                                  : kind == PoolKind.Buff ? "slot_buff" : "slot_weapon");
            slot.Icon.sprite = def.Icon;
            slot.Icon.color = Color.white;
            slot.Icon.enabled = def.Icon != null;
        }
    }

    private void RefreshLibrary()
    {
        string head = tab switch
        {
            PoolKind.Buff => Loc.Get("ui.workbench.library.buffs", "Bibliothek - Buffs"),
            PoolKind.Evo  => Loc.Get("ui.workbench.library.evos", "Bibliothek - Evolutionen"),
            _             => Loc.Get("ui.workbench.library.weapons", "Bibliothek - Waffen"),
        };

        if (libHead != null) libHead.text = head;

        foreach (LibTile t in libTiles)
        {
            bool inPool = Loadout.Contains(t.Def.Id);

            // Die Standardwaffe faellt hier unter "schon verteilt": sie liegt
            // fest im Verteiler, also sieht sie auch so aus. Den goldenen
            // Rahmen (slot_locked) traegt nur der Platz im Verteiler selbst -
            // in der Bibliothek hiesse er "waehl mich", und genau das geht nicht.
            string sprite = t.Def.Kind switch
            {
                PoolKind.Buff => inPool ? "slot_used" : "slot_buff",
                PoolKind.Evo  => "slot_used",
                _             => inPool ? "slot_used" : "slot_weapon",
            };

            t.Frame.sprite = Gfx(sprite);
            t.Icon.color = inPool || t.Def.Kind == PoolKind.Evo ? IconUsedTint : Color.white;
        }
    }

    private void RefreshTabs()
    {
        for (int i = 0; i < tabImages.Length; i++)
        {
            bool active = (int)tab == i;
            tabImages[i].sprite = Gfx(active ? "tab_active" : "tab_inactive");
            tabLabels[i].color = active ? TextDark : TextCream;
        }
    }

    private void RefreshEvoBar()
    {
        evoTitle.text = string.Format(
            Loc.Get("ui.workbench.evo.title", "EVOS {0} / {1}"),
            Loadout.ReadyEvoCount(), WeaponCatalog.Evos.Count);

        List<EvoDef> sorted = Loadout.EvosByProgress();

        for (int i = 0; i < chips.Count; i++)
        {
            EvoChip chip = chips[i];
            EvoDef def = i < sorted.Count ? sorted[i] : null;
            chip.Def = def;

            if (def == null)
            {
                chip.Root.SetActive(false);
                continue;
            }

            chip.Root.SetActive(true);
            bool ready = Loadout.StateOf(def) == EvoState.Ready;
            chip.Frame.sprite = Gfx(ready ? "evo_chip_ok" : "evo_chip_pending");

            SetEvoIcons(chip.Icons, def);

            chip.Label.text = def.Name;
            chip.Label.color = ready ? TextGreen : TextDim;
        }
    }

    private void RefreshOverlay()
    {
        List<EvoDef> sorted = Loadout.EvosByProgress();

        for (int i = 0; i < rows.Count; i++)
        {
            EvoRow row = rows[i];
            EvoDef def = i < sorted.Count ? sorted[i] : null;
            row.Def = def;

            if (def == null)
            {
                row.Root.SetActive(false);
                continue;
            }

            row.Root.SetActive(true);
            EvoState state = Loadout.StateOf(def);
            row.Frame.sprite = Gfx(state == EvoState.Ready ? "evo_row_ok" : "evo_row_pending");

            SetEvoIcons(row.Icons, def);

            row.Name.text = def.Name;
            row.Name.color = state == EvoState.Ready ? TextGreen : TextCream;

            string a = def.A != null ? def.A.Name : def.IngredientA;
            string b = def.B != null ? def.B.Name : def.IngredientB;
            row.Sub.text = $"{a} + {b}";

            row.State.text = state switch
            {
                EvoState.Ready   => Loc.Get("ui.workbench.evo.ready", "2/2"),
                EvoState.Partial => Loc.Get("ui.workbench.evo.partial", "1/2"),
                _                => Loc.Get("ui.workbench.evo.none", "0/2"),
            };
            row.State.color = state == EvoState.Ready ? TextGreen : TextDim;
        }
    }

    /// <summary>
    /// Zutat, Zutat, Ergebnis. Eine fehlende Zutat bleibt gedaempft - so sieht
    /// man auf dem Chip, was noch fehlt, ohne den Namen lesen zu muessen.
    /// </summary>
    private void SetEvoIcons(Image[] icons, EvoDef def)
    {
        WeaponDef[] parts = { def.A, def.B, def.Result };

        for (int k = 0; k < 3; k++)
        {
            WeaponDef part = parts[k];
            Sprite sprite = part != null ? part.IconSmall : null;

            icons[k].sprite = sprite;
            icons[k].enabled = sprite != null;

            bool have = k == 2 || (part != null && Loadout.Contains(part.Id));
            icons[k].color = have ? Color.white : IconUsedTint;
        }
    }

    private void RefreshApply()
    {
        bool complete = Loadout.IsComplete;

        applyImage.sprite = Gfx(complete ? "btn_primary" : "btn_primary_disabled");
        applyButton.interactable = complete;

        applyLabel.text = Loadout.IsActive
            ? Loc.Get("ui.workbench.btn.applied", "BUILD ÜBERNOMMEN")
            : Loc.Get("ui.workbench.btn.apply", "BUILD ÜBERNEHMEN");
        applyLabel.color = complete ? TextOnGold : TextMid;

        // Die Maus kann auf dem Knopf liegen, waehrend er grau wird - dann
        // bleibt sonst ein goldener Rahmen um einen toten Knopf stehen.
        if (!complete && applyHover != null) applyHover.enabled = false;
    }

    /// <summary>
    /// Fuellt die Info-Box unter der Bibliothek.
    ///
    /// Verlaesst die Maus eine Kachel, bleibt der letzte Eintrag stehen - sonst
    /// flackert die Box bei jeder Bewegung zwischen zwei Kacheln. Geleert wird
    /// nur absichtlich, ueber <see cref="ClearInspect"/>.
    /// </summary>
    private void Inspect(WeaponDef def)
    {
        if (def == null && inspected != null) return;
        inspected = def;

        if (def == null)
        {
            infoName.text = "";
            infoDesc.text = Loc.Get("ui.workbench.info.empty",
                                    "Zeig auf eine Kachel, um sie zu lesen.");
            return;
        }

        infoName.text = def.Name;
        infoName.color = Loadout.Contains(def.Id) ? TextGold : TextCream;
        if (Loadout.IsLocked(def.Id)) infoName.color = TextGreen;

        List<string> lines = new List<string>();

        string desc = def.Description;
        if (!string.IsNullOrEmpty(desc)) lines.Add(desc);

        if (def.Kind == PoolKind.Evo)
        {
            EvoDef recipe = null;
            foreach (EvoDef e in WeaponCatalog.Evos)
            {
                if (e.Id == def.Id) { recipe = e; break; }
            }

            if (recipe != null)
            {
                string a = recipe.A != null ? recipe.A.Name : recipe.IngredientA;
                string b = recipe.B != null ? recipe.B.Name : recipe.IngredientB;
                lines.Add(string.Format(Loc.Get("ui.workbench.info.recipe", "Braucht {0} + {1}"), a, b));
            }
        }
        else
        {
            List<EvoDef> uses = WeaponCatalog.RecipesUsing(def.Id);
            if (uses.Count > 0)
            {
                List<string> names = new List<string>();
                foreach (EvoDef e in uses) names.Add(e.Name);
                lines.Add(string.Format(Loc.Get("ui.workbench.info.partof", "Evo: {0}"),
                                        string.Join(", ", names)));
            }

            if (Loadout.IsLocked(def.Id))
            {
                lines.Add(Loc.Get("ui.workbench.info.locked",
                                  "Startwaffe des Charakters - fest im Build."));
            }
            else if (Loadout.Contains(def.Id))
            {
                lines.Add(Loc.Get("ui.workbench.info.inpool", "Liegt im Verteiler."));
            }
        }

        infoDesc.text = string.Join("\n", lines);
    }

    private void ClearInspect()
    {
        inspected = null;
        Inspect(null);
    }

    // ==================================================================
    //  Kleinkram
    // ==================================================================

    private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

    private static Sprite Gfx(string name)
    {
        // != null statt TryGetValue allein: raeumt Unity die Resources zwischen
        // zwei Szenen auf, steht im Cache eine zerstoerte Referenz.
        if (spriteCache.TryGetValue(name, out Sprite cached) && cached != null) return cached;

        Sprite s = Resources.Load<Sprite>(UiPath + name);
        if (s == null)
        {
            Debug.LogWarning($"[Werkbank] Sprite '{name}' fehlt - erwartet wird " +
                             $"Assets/Resources/{UiPath}{name}.png");
        }
        spriteCache[name] = s;
        return s;
    }

    /// <summary>
    /// Sucht eine Pixelschrift, die wirklich Umlaute kann.
    ///
    /// Die Hausschrift ThaleahFat (das TMP-Asset heisst PixelArtFont) kennt nur
    /// 106 Zeichen und **kein** ae/oe/ue - aus "ZUFÄLLIG" wird dort "ZUF LLIG".
    /// In diesem Fenster steht deutscher Text, also wird nicht nach einem Namen
    /// gesucht, sondern nach dem, worauf es ankommt: kann die Schrift ein 'ü'?
    ///
    /// Jersey10 (Assets/Imports/Jersey10_PixelFont) kann es und liegt im Hub
    /// ohnehin geladen vor, weil Textbox, Shop, Levelauswahl und Skilltree es
    /// benutzen.
    /// </summary>
    private static TMP_FontAsset FindFont()
    {
        TMP_FontAsset best = null;

        foreach (TMP_FontAsset f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (f == null) continue;
            if (!HasUmlaut(f)) continue;

            // Jersey10 ist die abgestimmte Wahl; alles andere ist Notnagel.
            if (f.name.StartsWith("Jersey10")) return f;
            if (best == null) best = f;
        }

        if (best != null) return best;

        Debug.LogWarning("[Werkbank] Keine geladene Schrift kann Umlaute - der Text " +
                         "bekommt Luecken. Erwartet wird Jersey10 aus " +
                         "Assets/Imports/Jersey10_PixelFont.");
        return PixelUI.FindPixelFont();
    }

    /// <summary>
    /// Dynamische Schriften melden ein Zeichen erst, wenn es im Atlas liegt -
    /// deshalb mit tryAddCharacter fragen, sonst sagen alle erstmal nein.
    /// </summary>
    private static bool HasUmlaut(TMP_FontAsset font)
    {
        try
        {
            return font.HasCharacter('ü', true, true);
        }
        catch
        {
            // Manche Assets (Bitmap ohne Quelldatei) mögen das Nachladen nicht
            return font.HasCharacter('ü');
        }
    }

    private static Color Hex(int rgb) => new Color32(
        (byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    /// <summary>Rechteck im 320x180-Raster: x/y zaehlen von oben links.</summary>
    private static RectTransform Rect(string name, Transform parent, float x, float y,
                                      float w, float h)
    {
        RectTransform rt = NewRect(name, parent);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, -y);
        return rt;
    }

    private static Image Img(string name, Transform parent, Vector4 r, Sprite sprite)
    {
        RectTransform rt = Rect(name, parent, r.x, r.y, r.z, r.w);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    private static Image StretchImage(string name, Transform parent, Sprite sprite)
    {
        RectTransform rt = NewRect(name, parent);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>
    /// Die Hoehe muss groesser sein als die Schriftgroesse. Ist das Rechteck zu
    /// flach, wirft TextMeshPro die Zeile still weg und es steht gar nichts da.
    /// </summary>
    private TMP_Text Label(string name, Transform parent, Vector4 r, string text,
                           float size, Color color, TextAlignmentOptions align)
    {
        RectTransform rt = Rect(name, parent, r.x, r.y, r.z, r.w);

        TextMeshProUGUI label = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = align;
        label.raycastTarget = false;
        label.enableAutoSizing = false;
        label.margin = Vector4.zero;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        return label;
    }

    private static void PlayClick()
    {
        AudioController audio = AudioController.Instance;
        if (audio != null && audio.MenuClick != null) audio.PalySound(audio.MenuClick);
    }

    /// <summary>
    /// Legt eine unsichtbare Flaeche ueber ein Sichtfenster, damit das Mausrad
    /// dort ankommt.
    ///
    /// Ein ScrollRect bekommt Rad-Ereignisse nur ueber einen Graphic mit
    /// raycastTarget - ohne den scrollt nur, wo zufaellig eine anklickbare
    /// Kachel liegt. Im Evo-Overlay liegt gar keine, dort ging es deshalb
    /// ueberhaupt nicht.
    /// </summary>
    private static void CatchScroll(RectTransform view)
    {
        GameObject go = new GameObject("ScrollCatcher", typeof(RectTransform));
        go.transform.SetParent(view, false);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        rt.SetAsFirstSibling();

        Image img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = true;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(es);
    }
}
