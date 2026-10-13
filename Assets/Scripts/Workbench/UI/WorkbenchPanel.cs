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
    // Preset-Knoepfe stehen, wo frueher "Build uebernehmen" stand: 4 x 21 + 3 x 2 = 90.
    private const float PresetX = 84f, PresetY = 150f, PresetW = 21f, PresetH = 14f, PresetStep = 23f;

    // Rechte Spalte
    private const float TabX = 180f, TabY = 30f, TabW = 40f, TabH = 12f, TabStep = 42f;
    private static readonly Vector4 Library = new Vector4(180f, 42f, 133f, 124f);
    private static readonly Vector4 LibHead = new Vector4(186f, 46f, 119f, 9f);
    // Drei Kachelreihen (3 x 16 = 48) reichen fuer jeden Reiter; was darueber
    // hinausgeht, wird gescrollt. Der Platz darunter gehoert der Info-Box,
    // weil dort bis zu fuenf Zeilen stehen: Beschreibung (max. 3), Evo und bei
    // der Startwaffe der Hinweis, dass sie fest im Build liegt.
    private static readonly Vector4 LibView = new Vector4(186f, 57f, 119f, 48f);
    private const float LibGridDX = 4f;
    private const int LibCols = 7;
    private static readonly Vector4 InfoBox  = new Vector4(186f, 108f, 119f, 50f);
    private static readonly Vector4 InfoName = new Vector4(190f, 111f, 96f, 9f);
    private static readonly Vector4 InfoDesc = new Vector4(190f, 121f, 111f, 35f);
    // "?" oben rechts in der Info-Box: oeffnet den Skalierungs-Text (wie btn_close 14x10)
    private static readonly Vector4 BtnScaling = new Vector4(288f, 110f, 14f, 10f);

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
    private bool scalingOpen;

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
    private TMP_Text infoName, infoDesc;
    private GameObject scalingButton;

    // Skalierungs-Overlay: wie die Waffe mit Extra-Schuss, AOE, Wirkdauer skaliert
    private GameObject scalingRoot;
    private TMP_Text scalingTitle, scalingText;
    private RectTransform scalingContent;
    private readonly List<PresetButton> presetButtons = new List<PresetButton>();

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

        /// <summary>Schloss ueber einem gesperrten Platz (mehr Slots im Shop kaufen).</summary>
        public Image Lock;

        /// <summary>Gesperrt, weil der Lauf nicht genug Slots hat - siehe Loadout.Capacity.</summary>
        public bool Closed;
    }

    /// <summary>Eine Kachel in der Bibliothek.</summary>
    private class LibTile
    {
        public GameObject Root;
        public Image Frame;
        public Image Icon;
        public Image Hover;
        public Image Lock;
        public WeaponDef Def;
    }

    /// <summary>Ein Knopf fuer einen gespeicherten Build.</summary>
    private class PresetButton
    {
        public Image Frame;
        public Image Hover;
        public Image Lock;
        public TMP_Text Label;
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
    private class Hoverable : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public Image Target;
        public Action<bool> Notify;

        /// <summary>Rechtsklick - bei Kacheln: Skalierungs-Text oeffnen.</summary>
        public Action RightClick;

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Right) RightClick?.Invoke();
        }

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

        TutorialPanel.ShowOnce(TutorialTopic.Workbench);
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

        // Das Tutorial liegt obendrauf - solange ruht die Werkbank.
        if (TutorialPanel.Blocking) return;

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))
        {
            if (scalingOpen) SetScaling(null);
            else if (overlayOpen) SetOverlay(false);
            else Close();
            return;
        }

        if (overlayOpen || scalingOpen) return;

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) StepTab(-1);
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) StepTab(1);
        if (Input.GetKeyDown(KeyCode.Tab)) StepTab(1);

        // 1-4 springen direkt auf einen gespeicherten Build
        for (int i = 0; i < presetButtons.Count; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) ClickPreset(i);
        }
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
        BuildScalingOverlay();

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

        BuildPoolGrid(weaponSlots, PoolKind.Weapon, GridWeaponsY, Loadout.GridSize(PoolKind.Weapon));
        BuildPoolGrid(buffSlots, PoolKind.Buff, GridBuffsY, Loadout.GridSize(PoolKind.Buff));
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

            slot.Lock = LockIcon(slot.Frame.rectTransform);

            Hoverable hover = slot.Frame.gameObject.AddComponent<Hoverable>();
            hover.Target = slot.Hover;
            PoolSlot captured = slot;
            hover.Notify = on =>
            {
                if (captured.Closed) InspectClosed(kind);
                else Inspect(on ? captured.Def : null);
            };
            hover.RightClick = () => { if (!captured.Closed) SetScaling(captured.Def); };

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

            // Klick legt beide Zutaten in den Verteiler oder nimmt sie raus.
            EvoChip captured = chip;
            MakeEvoClickable(chip.Frame, () => captured.Def);
            Hoverable chipHover = chip.Frame.GetComponent<Hoverable>();
            chipHover.Notify = on =>
            {
                if (on && captured.Def != null) Inspect(captured.Def.Result);
            };
            chipHover.RightClick = () => { if (captured.Def != null) SetScaling(captured.Def.Result); };

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

        // Gespeicherte Builds. Der zuletzt gewaehlte geht mit in den Lauf -
        // ein "Uebernehmen" braucht es dafuer nicht.
        for (int i = 0; i < Loadout.MaxPresets; i++)
        {
            Vector4 r = new Vector4(PresetX + i * PresetStep, PresetY, PresetW, PresetH);

            PresetButton pb = new PresetButton();
            pb.Frame = Img($"BtnPreset_{i}", page, r, Gfx("btn_narrow"));
            pb.Frame.type = Image.Type.Sliced;
            pb.Frame.raycastTarget = true;

            pb.Label = Label("Label", pb.Frame.rectTransform, new Vector4(2f, 2f, PresetW - 4f, 10f),
                             (i + 1).ToString(), SizeText, TextCream, TextAlignmentOptions.Center);

            pb.Lock = Img("Lock", pb.Frame.rectTransform,
                          new Vector4((PresetW - 12f) * 0.5f, 1f, 12f, 12f), LockSprite());
            pb.Lock.enabled = false;

            int index = i;
            Button b = pb.Frame.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.targetGraphic = pb.Frame;
            b.onClick.AddListener(() => ClickPreset(index));

            // Der gewaehlte hebt sich schon durch sein Sprite ab.
            pb.Hover = AddHover(pb.Frame, () => index != Loadout.ActivePreset);
            Hoverable hover = pb.Frame.GetComponent<Hoverable>();
            hover.Notify = on => { if (on) InspectPreset(index); };

            presetButtons.Add(pb);
        }
    }

    private void ClickPreset(int index)
    {
        if (!Loadout.IsPresetOpen(index))
        {
            InspectPreset(index);
            return;
        }

        if (index == Loadout.ActivePreset) return;
        PlayClick();
        Loadout.SelectPreset(index);
        InspectPreset(index);
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

        // Das Sprite ist 119x30, die Box hoeher - gesliced bleibt der 2px-Rand scharf.
        Img("InfoBox", page, InfoBox, Gfx("info_box")).type = Image.Type.Sliced;
        infoName = Label("InfoName", page, InfoName, "", SizeText, TextCream,
                         TextAlignmentOptions.Left);
        infoDesc = Label("InfoDesc", page, InfoDesc, "", SizeSmall, TextDim,
                         TextAlignmentOptions.TopLeft);
        infoDesc.textWrappingMode = TextWrappingModes.Normal;

        // "?" - zeigt, wie die Kachel in der Info-Box skaliert. Rechtsklick
        // auf eine Kachel tut dasselbe, ohne erst hierher zu muessen.
        Image help = Img("BtnScaling", page, BtnScaling, Gfx("btn_close"));
        help.raycastTarget = true;
        Label("Q", help.rectTransform, new Vector4(0f, 1f, BtnScaling.z, 8f), "?",
              SizeText, TextCream, TextAlignmentOptions.Center);

        Button hb = help.gameObject.AddComponent<Button>();
        hb.transition = Selectable.Transition.None;
        hb.targetGraphic = help;
        hb.onClick.AddListener(() => SetScaling(inspected));

        AddHover(help);
        scalingButton = help.gameObject;
        scalingButton.SetActive(false);
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

        // Waffen, die einem anderen Charakter allein gehoeren, gar nicht erst zeigen.
        List<WeaponDef> defs = Loadout.Available(tab);
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

            t.Lock = LockIcon(t.Frame.rectTransform);

            Hoverable hover = t.Frame.gameObject.AddComponent<Hoverable>();
            hover.Target = t.Hover;
            hover.Notify = on => Inspect(on ? def : null);
            hover.RightClick = () => SetScaling(def);

            Button button = t.Frame.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = t.Frame;
            button.onClick.AddListener(() =>
            {
                // Eine Evo selbst kommt nicht in den Verteiler - ein Klick
                // raeumt stattdessen ihre beiden Zutaten ein oder aus.
                if (def.Kind == PoolKind.Evo)
                {
                    ClickEvo(WeaponCatalog.FindEvo(def.Id));
                    return;
                }

                // Die Standardwaffe liegt fest im Verteiler: Loadout.Remove
                // sagt ohnehin nein - dann soll es auch nicht klacken, als
                // waere etwas passiert.
                // Ungekauftes (Schloss) laesst sich erst nach dem Shop-Kauf waehlen.
                if (Loadout.IsLocked(def.Id)) return;
                if (!Loadout.IsSelectable(def.Id)) return;
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

            EvoRow captured = row;
            MakeEvoClickable(row.Frame, () => captured.Def);

            rows.Add(row);
        }

        overlayRoot.SetActive(false);
    }

    /// <summary>
    /// Macht einen Evo-Chip oder eine Evo-Zeile anklickbar: der Klick raeumt
    /// beide Zutaten ein oder aus (<see cref="Loadout.ToggleEvo"/>). Die Evo
    /// kommt ueber <paramref name="evo"/>, weil Chips und Zeilen bei jedem
    /// Auffrischen neu sortiert werden.
    /// </summary>
    private void MakeEvoClickable(Image frame, Func<EvoDef> evo)
    {
        frame.raycastTarget = true;

        Button button = frame.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = frame;
        button.onClick.AddListener(() => ClickEvo(evo()));

        AddHover(frame);
    }

    private void ClickEvo(EvoDef evo)
    {
        if (!Loadout.ToggleEvo(evo)) return;
        PlayClick();
    }

    // ---------- Skalierungs-Overlay ----------

    /// <summary>
    /// Liegt auf derselben Flaeche wie das Evo-Overlay: Name oben, darunter
    /// scrollbar der Text aus <see cref="WeaponDef.Scaling"/> - wie die Kachel
    /// mit Extra-Schuss, AOE, Wirkdauer und Cooldown umgeht.
    /// </summary>
    private void BuildScalingOverlay()
    {
        scalingRoot = NewRect("ScalingOverlay", page).gameObject;
        RectTransform root = (RectTransform)scalingRoot.transform;
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0f, 1f);
        root.sizeDelta = new Vector2(RefW, RefH);
        root.anchoredPosition = Vector2.zero;

        Image bg = Img("Sheet", root, Overlay, Gfx("overlay_evo"));
        bg.raycastTarget = true;

        scalingTitle = Label("Title", root, OverlayTitle, "", SizeText, TextCream,
                             TextAlignmentOptions.Left);
        scalingTitle.overflowMode = TextOverflowModes.Ellipsis;

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
            SetScaling(null);
        });

        AddHover(close);

        RectTransform view = Rect("View", root, OverlayView.x, OverlayView.y,
                                  OverlayView.z, OverlayView.w);
        view.gameObject.AddComponent<RectMask2D>();
        CatchScroll(view);

        ScrollRect scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 8f;
        scroll.viewport = view;

        scalingContent = Rect("Content", view, 0f, 0f, OverlayView.z, OverlayView.w);
        scroll.content = scalingContent;

        scalingText = Label("Text", scalingContent, new Vector4(2f, 1f, OverlayView.z - 4f, OverlayView.w),
                            "", SizeSmall, TextCream, TextAlignmentOptions.TopLeft);
        scalingText.textWrappingMode = TextWrappingModes.Normal;

        scalingRoot.SetActive(false);
    }

    /// <summary>Oeffnet den Skalierungs-Text fuer <paramref name="def"/>; null schliesst.</summary>
    private void SetScaling(WeaponDef def)
    {
        string text = def != null ? def.Scaling : null;
        bool open = !string.IsNullOrEmpty(text);

        if (open && overlayOpen) SetOverlay(false);
        if (open) PlayClick();

        scalingOpen = open;
        scalingRoot.SetActive(open);
        if (!open) return;

        scalingTitle.text = def.Name;
        scalingText.text = text;

        // Hoehe nach dem Text, damit der ScrollRect weiss, wie weit es geht
        float height = Mathf.Ceil(scalingText.GetPreferredValues(text, OverlayView.z - 4f, 0f).y) + 2f;
        scalingText.rectTransform.sizeDelta = new Vector2(OverlayView.z - 4f, height);
        scalingContent.sizeDelta = new Vector2(OverlayView.z, Mathf.Max(OverlayView.w, height + 2f));
        scalingContent.anchoredPosition = Vector2.zero;
    }

    private void SetOverlay(bool open)
    {
        if (open && scalingOpen) SetScaling(null);
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
        RefreshPresets();
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
        int open = Loadout.Capacity(kind);

        for (int i = 0; i < slots.Count; i++)
        {
            PoolSlot slot = slots[i];
            slot.Closed = i >= open;
            if (slot.Lock != null) slot.Lock.enabled = slot.Closed;

            // Gesperrt: mehr Slots im Lauf (Shop) oeffnen je drei Plaetze.
            if (slot.Closed)
            {
                slot.Def = null;
                slot.Frame.sprite = Gfx("slot_used");
                slot.Icon.enabled = false;
                continue;
            }

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

            // Noch nicht gekauft: sichtbar, aber mit Schloss und nicht waehlbar.
            bool closed = t.Def.Kind != PoolKind.Evo && !Loadout.IsSelectable(t.Def.Id);
            if (closed) sprite = "slot_used";
            if (t.Lock != null) t.Lock.enabled = closed;

            // Evos leuchten, sobald beide Zutaten im Verteiler liegen.
            bool dim = t.Def.Kind == PoolKind.Evo
                ? Loadout.StateOf(WeaponCatalog.FindEvo(t.Def.Id)) != EvoState.Ready
                : inPool || closed;

            t.Frame.sprite = Gfx(sprite);
            t.Icon.color = dim ? IconUsedTint : Color.white;
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
            row.Sub.text = def.IngredientCount == 1 ? a : $"{a} + {b}";

            if (def.IngredientCount == 1)
            {
                // Evo ohne zweite Zutat (Tofu): nur 1/1 oder 0/1.
                row.State.text = state == EvoState.Ready
                    ? Loc.Get("ui.workbench.evo.solo_ready", "1/1")
                    : Loc.Get("ui.workbench.evo.solo_none", "0/1");
            }
            else
            {
                row.State.text = state switch
                {
                    EvoState.Ready   => Loc.Get("ui.workbench.evo.ready", "2/2"),
                    EvoState.Partial => Loc.Get("ui.workbench.evo.partial", "1/2"),
                    _                => Loc.Get("ui.workbench.evo.none", "0/2"),
                };
            }
            row.State.color = state == EvoState.Ready ? TextGreen : TextDim;
        }
    }

    /// <summary>
    /// Zutat, Zutat, Ergebnis. Eine fehlende Zutat bleibt gedaempft - so sieht
    /// man auf dem Chip, was noch fehlt, ohne den Namen lesen zu muessen.
    /// </summary>
    private void SetEvoIcons(Image[] icons, EvoDef def)
    {
        // Die Tofu-Evo entsteht aus ihrer Waffe allein - die steht dann auf
        // beiden Zutatenplaetzen, statt dass einer leer bleibt.
        WeaponDef b = def.IngredientCount == 1 ? def.A : def.B;
        WeaponDef[] parts = { def.A, b, def.Result };

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

    /// <summary>
    /// Gewaehlt = gold, offen = Holz, nicht gekauft = grau mit Schloss statt Zahl
    /// (faellt das Schloss-Sprite weg, bleibt die Zahl stehen).
    /// </summary>
    private void RefreshPresets()
    {
        int active = Loadout.ActivePreset;

        for (int i = 0; i < presetButtons.Count; i++)
        {
            PresetButton pb = presetButtons[i];
            bool open = Loadout.IsPresetOpen(i);
            bool isActive = open && i == active;

            pb.Frame.sprite = Gfx(isActive ? "btn_primary" : open ? "btn_narrow" : "btn_primary_disabled");
            pb.Label.color = isActive ? TextOnGold : open ? TextCream : TextMid;

            bool showLock = !open && pb.Lock.sprite != null;
            pb.Lock.enabled = showLock;
            pb.Label.enabled = !showLock;

            // Wird der Knopf unter der Maus zum gewaehlten, kommt kein
            // PointerExit - der Rahmen muss hier selbst weg.
            if (isActive && pb.Hover != null) pb.Hover.enabled = false;
        }
    }

    /// <summary>Info-Box fuer einen Preset-Knopf.</summary>
    private void InspectPreset(int index)
    {
        inspected = null;
        scalingButton.SetActive(false);

        infoName.text = string.Format(Loc.Get("ui.workbench.preset.name", "BUILD {0}"), index + 1);

        if (!Loadout.IsPresetOpen(index))
        {
            infoName.color = TextDim;
            infoDesc.text = Loc.Get("ui.workbench.preset.locked",
                "Weitere Build-Plätze gibt es im Shop.");
            return;
        }

        infoName.color = index == Loadout.ActivePreset ? TextGold : TextCream;
        infoDesc.text = Loc.Get("ui.workbench.preset.desc",
            "Gespeicherter Build. Der zuletzt gewählte geht mit in den Lauf.");
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
        scalingButton.SetActive(def != null && !string.IsNullOrEmpty(def.Scaling));

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
            EvoDef recipe = WeaponCatalog.FindEvo(def.Id);

            if (recipe != null)
            {
                string a = recipe.A != null ? recipe.A.Name : recipe.IngredientA;
                string b = recipe.B != null ? recipe.B.Name : recipe.IngredientB;
                lines.Add(recipe.IngredientCount == 1
                    ? string.Format(Loc.Get("ui.workbench.info.recipe_solo", "Braucht {0} auf Maximalstufe"), a)
                    : string.Format(Loc.Get("ui.workbench.info.recipe", "Braucht {0} + {1}"), a, b));
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
            else if (!Loadout.IsSelectable(def.Id))
            {
                lines.Add(Loc.Get("ui.workbench.info.shop",
                                  "Noch gesperrt - im Shop freischalten."));
            }
        }

        infoDesc.text = string.Join("\n", lines);
    }

    /// <summary>Info-Box fuer einen gesperrten Platz im Verteiler.</summary>
    private void InspectClosed(PoolKind kind)
    {
        inspected = null;
        scalingButton.SetActive(false);
        infoName.text = Loc.Get("ui.workbench.closed.title", "Gesperrter Platz");
        infoName.color = TextDim;
        infoDesc.text = kind == PoolKind.Buff
            ? string.Format(Loc.Get("ui.workbench.closed.buffs",
                  "Jeder Buff-Slot im Lauf öffnet {0} Plätze, höchstens {1}. Mehr Slots gibt es im Shop."),
                  Loadout.PicksPerSlot, Loadout.MaxBuffs)
            : string.Format(Loc.Get("ui.workbench.closed.weapons",
                  "Jeder Waffen-Slot im Lauf öffnet {0} Plätze. Mehr Slots gibt es im Shop."),
                  Loadout.PicksPerSlot);
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

    /// <summary>
    /// Schloss (12x12, aus dem Erfolgsbuch) mittig auf einer 14er-Kachel,
    /// zunaechst aus. Fehlt das Sprite, bleibt es bei der gedimmten Kachel.
    /// </summary>
    private static Image LockIcon(RectTransform parent)
    {
        Image img = Img("Lock", parent, new Vector4(1f, 1f, 12f, 12f), LockSprite());
        img.raycastTarget = false;
        img.enabled = false;
        return lockSprite != null ? img : null;
    }

    private static Sprite LockSprite()
    {
        if (lockSprite == null) lockSprite = Resources.Load<Sprite>(LockSpritePath);
        return lockSprite;
    }

    private const string LockSpritePath = "AchievementsBook/ui/icon_locked";
    private static Sprite lockSprite;

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
