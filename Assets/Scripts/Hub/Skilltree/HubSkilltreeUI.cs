using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// ---------------------------------------------------------------------------
///  DER SKILLTREE IM HUB - im Stil von "UI 2.0" wie Optionen, Pause, Level-Up
///  und die Kapsel: dunkle Karte, Titelband, Holzknoepfe, 480x270-Seite mit
///  ganzzahliger Skalierung (<see cref="OptionsKit"/>).
///
///  Aufbau (Seitenpixel):
///    Titelband   SKILLTREE
///    Kopfzeile   Baum des Charakters · gelernt 14/43 [====]    ◆ 12 SKILLPUNKTE
///    links       die vier Kategorien - die gewaehlte traegt ihre Farbe und
///                greift wie ein Reiter ins Feld
///    Mitte       Banner mit dem Pfadnamen, darunter der Baum (HubSkilltreeGraph)
///    rechts      der Knoten unter der Maus bzw. der gewaehlte: Form, Name,
///                Wirkung, Preis/Zustand
///    Fussleiste  ZURUECK  RESET        Tastenhinweis             LERNEN
///
///  Klick auf einen Knoten waehlt ihn, LERNEN (oder ein zweiter Klick, Enter)
///  kauft ihn. So kauft niemand aus Versehen im Vorbeiklicken.
///
///  RESET vergisst nach einer Rueckfrage alle Knoten dieses Baums. Die Punkte
///  sind danach von selbst wieder frei (sie werden aus dem Level gerechnet).
///
///  WO DER INHALT HERKOMMT: aus dem Baum-Asset des Charakters unter
///  Assets/Resources/SkillTrees/ (Tools -> Skilltree -> Editor). Beim Oeffnen
///  holt sich das Fenster ueber Skills.Sync() den Baum des gewaehlten Charakters.
///
///  Bedienung: Maus, W/S bzw. Hoch/Runter = Kategorie, A/D bzw. Links/Rechts =
///  Knoten, Enter/Leertaste = lernen, Mausrad = Baum scrollen, E/ESC = zu.
///
///  Das Fenster haengt als Komponente am Skilltree-Objekt im Hub (siehe
///  <see cref="HubSkilltreeTrigger"/>); gebaut wird es erst beim Oeffnen.
/// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public class HubSkilltreeUI : MonoBehaviour
{
    /// <summary>Steht offen? Der Hub sperrt solange seine Interaktionen.</summary>
    public static bool IsOpen { get; private set; }

    [Header("Ton")]
    [SerializeField] private AudioClip openClip;
    [SerializeField] private AudioClip closeClip;
    [SerializeField] private AudioClip moveClip;
    [Tooltip("Leer = wird auf diesem Objekt angelegt.")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    // ==================================================================
    //  Masse (Seitenpixel, Ursprung oben links)
    // ==================================================================

    const int CardX = 16, CardY = 38, CardW = 448, CardH = 212;
    const int RibbonY = 26;
    static readonly RectInt Content = new RectInt(CardX - 12, RibbonY, CardW + 24, CardY + CardH - RibbonY);

    const int InX = CardX + 14, InW = CardW - 28;
    const int TopY = 52;
    const int XpBarW = 120;
    const int BodyY = 74, BodyH = 144;

    const int CatX = InX, CatW = 84, CatH = 30, CatGap = 4;

    const int FieldX = CatX + CatW + 4, FieldW = 220;
    const int BannerH = 16;
    static readonly RectInt TreeArea = new RectInt(FieldX + 2, BodyY + BannerH + 6, FieldW - 4, BodyH - BannerH - 8);

    const int DetX = FieldX + FieldW + 6, DetW = InX + InW - DetX;
    const int PlateS = 40;

    const int FootY = 226, FootH = 18, BackW = 76, ResetW = 56;

    const int DlgX = 146, DlgY = 92, DlgW = 188, DlgH = 86;
    const int DlgTextY = 101, DlgTextH = 40;
    const int DlgBtnY = 150, DlgBtnW = 80, DlgBtnH = 18;

    // ==================================================================
    //  Zustand
    // ==================================================================

    class CatView
    {
        public RectTransform Root;
        public Image Back, IconImg;
        public TextMeshProUGUI Label, Count;
    }

    GameObject canvasGo;
    RectTransform page;
    CanvasScaler scaler;
    Vector2Int lastScreen;
    TMP_FontAsset textFont, pixelFont;

    HubSkilltreeGraph graph;
    SkillShapeSprites detailShapes;

    readonly List<SkillBranchDef> branches = new List<SkillBranchDef>();
    readonly List<CatView> cats = new List<CatView>();
    int selectedBranch, hoverBranch = -1;
    SkillNodeDef selectedNode;

    TextMeshProUGUI treeName, learnedText, pointsText, bannerText, bannerShadow;
    Image learnedFill, xpFill, banner, bannerIcon;

    Image plateImg, shapeShadow, shapeFill, shapeGloss, shapeOutline, plateIcon, statusIcon;
    TextMeshProUGUI detailName, detailSub, detailDesc, statusText;
    Image detailRule;
    SkinButton learnButton, resetButton;

    RectTransform dialog, dialogPage;

    int openedOnFrame = -1;

    SkillBranchDef Current =>
        selectedBranch >= 0 && selectedBranch < branches.Count ? branches[selectedBranch] : null;

    // ==================================================================
    //  Oeffnen / Schliessen
    // ==================================================================

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        openedOnFrame = Time.frameCount;

        // Der Baum haengt am gewaehlten Charakter - erst holen, dann anzeigen.
        Skills.Sync();
        selectedBranch = Mathf.Clamp(selectedBranch, 0, 3);
        selectedNode = null;
        hoverBranch = -1;

        Build();

        Skills.Changed += OnSkillsChanged;
        Loc.LanguageChanged += OnLanguageChanged;

        HubUI.PushModal();
        HubUI.Instance.SetPlayerFrozen(true);

        PlaySfx(openClip);
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;

        Teardown();
        Skills.Changed -= OnSkillsChanged;
        Loc.LanguageChanged -= OnLanguageChanged;

        HubUI.PopModal();
        HubUI.Instance.SetPlayerFrozen(false);

        PlaySfx(closeClip);
    }

    void OnDestroy()
    {
        Skills.Changed -= OnSkillsChanged;
        Loc.LanguageChanged -= OnLanguageChanged;
        Teardown();

        // Szenenwechsel mit offenem Fenster: die Sperre wieder abmelden, sonst
        // reagiert der Hub beim naechsten Mal auf gar nichts mehr.
        if (!IsOpen) return;
        IsOpen = false;
        HubUI.PopModal();
    }

    void Teardown()
    {
        graph?.Dispose();
        graph = null;
        detailShapes?.Dispose();
        detailShapes = null;
        cats.Clear();

        if (canvasGo != null) Destroy(canvasGo);
        canvasGo = null;
        page = null;
        dialog = dialogPage = null;
    }

    void OnLanguageChanged()
    {
        if (!IsOpen) return;
        SkillNodeDef keep = selectedNode;
        Teardown();
        Build();
        if (keep != null && keep.Branch == Current) SelectNode(keep);
    }

    void OnSkillsChanged()
    {
        if (!IsOpen || page == null) return;
        graph.Refresh();
        Refresh();
    }

    // ==================================================================
    //  Aufbau
    // ==================================================================

    void Build()
    {
        textFont = PixelUI.FindTextFont();
        pixelFont = PixelUI.FindPixelFont();
        detailShapes = new SkillShapeSprites(HubSkilltreeGraph.Shape);

        // Eigenes Wurzelobjekt: das Skilltree-Objekt im Hub ist skaliert und
        // traegt Sprite und Zone - die Leinwand gehoert nicht daran.
        canvasGo = new GameObject("SkilltreeCanvas");
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0) canvasGo.layer = uiLayer;

        // Ueber Textbox (100), Konsole (120), Shop (130) und Levelauswahl (135).
        page = OptionsKit.CreatePage(canvasGo, 140, 0.86f, out scaler);

        OptionsKit.Img("Card", page, CardX, CardY, CardW, CardH, GameHudSkin.Card, true);
        OptionsKit.Ribbon(page, CardX + CardW / 2f, RibbonY, Loc.Get("ui.skilltree.title", "SKILLTREE"),
                          pixelFont, textFont);

        branches.Clear();
        SkillTreeDef tree = Skills.ActiveTree;
        if (tree != null) branches.AddRange(tree.Branches);
        selectedBranch = Mathf.Clamp(selectedBranch, 0, Mathf.Max(0, branches.Count - 1));

        BuildTop(tree);
        BuildField();
        BuildCategories();
        BuildDetail();
        BuildFooter();

        OptionsKit.Layout(page, scaler, Content);
        lastScreen = new Vector2Int(Screen.width, Screen.height);

        graph.Show(Current);
        Refresh();
    }

    void BuildTop(SkillTreeDef tree)
    {
        // Jeder Charakter hat seinen eigenen Baum - darum steht hier, wessen es ist.
        treeName = OptionsKit.Label("Tree", page, InX + 2, TopY, 165, 13,
                                    Characters.DisplayName(Shop.SkinIndex).ToUpperInvariant(), textFont,
                                    OptionsKit.SizeText, GameHudSkin.Parchment, TextAlignmentOptions.Left);
        treeName.richText = true;   // die XP-Zahlen stehen gedaempft hinter dem Level

        // Duenner XP-Balken unter dem Namen: Fortschritt zum naechsten Level.
        OptionsKit.Img("XpTrack", page, InX + 2, TopY + 14, XpBarW, 3, GameHudSkin.White, GameHudSkin.Trough);
        xpFill = OptionsKit.Img("XpFill", page, InX + 2, TopY + 14, 0, 3, GameHudSkin.White, GameHudSkin.Gold);

        // Gelernt: Zahl und Balken, mittig ueber dem Feld.
        const int barW = 80;
        int barX = FieldX + FieldW - barW;
        OptionsKit.Img("LearnedFrame", page, barX, TopY + 3, barW, 8, GameHudSkin.BarFrame, true);
        learnedFill = OptionsKit.Img("LearnedFill", page, barX + 1, TopY + 4, 0, 6, GameHudSkin.White,
                                     GameHudSkin.Mint);
        learnedText = OptionsKit.Label("Learned", page, barX - 124, TopY, 120, 13, "", textFont,
                                       OptionsKit.SizeText, GameHudSkin.Stone, TextAlignmentOptions.Right);

        // Skillpunkte rechts ueber der Karte.
        OptionsKit.Img("PointsPlate", page, DetX, TopY - 2, DetW, 16, GameHudSkin.Plate, true);
        Sprite gem = GameHudSkin.Gem;
        OptionsKit.Img("Gem", page, DetX + 5, TopY - 2 + Mathf.Floor((16 - gem.rect.height) / 2f),
                       gem.rect.width, gem.rect.height, gem);
        pointsText = OptionsKit.Label("Points", page, DetX + 8 + gem.rect.width, TopY - 1, DetW - 12 - gem.rect.width, 13,
                                      "", textFont, OptionsKit.SizeText, GameHudSkin.Gold, TextAlignmentOptions.Left);
    }

    void BuildField()
    {
        OptionsKit.Img("FieldWell", page, FieldX, BodyY, FieldW, BodyH, GameHudSkin.Well, true);

        banner = OptionsKit.Img("Banner", page, FieldX + 3, BodyY + 3, FieldW - 6, BannerH,
                                GameHudSkin.TintButton(true), true);
        bannerIcon = OptionsKit.Img("BannerIcon", page, FieldX + 8, BodyY + 5, 12, 12, null);
        bannerShadow = OptionsKit.Label("BannerTextShadow", page, FieldX + 3, BodyY + 5, FieldW - 6, 13, "",
                                        textFont, OptionsKit.SizeText, GameHudSkin.Ink, TextAlignmentOptions.Center);
        bannerText = OptionsKit.Label("BannerText", page, FieldX + 3, BodyY + 4, FieldW - 6, 13, "", textFont,
                                      OptionsKit.SizeText, GameHudSkin.Cream, TextAlignmentOptions.Center);

        graph = new HubSkilltreeGraph(page, TreeArea, textFont)
        {
            HoverChanged = _ => ShowDetail(),
            Clicked = OnNodeClicked,
        };
    }

    /// <summary>
    /// Die Kategorien links. Nach dem Feld gebaut, damit die gewaehlte ueber
    /// dessen Kante greifen kann - so haengt sie wie ein Reiter am Feld.
    /// </summary>
    void BuildCategories()
    {
        cats.Clear();
        for (int i = 0; i < branches.Count; i++)
        {
            int index = i;
            SkillBranchDef b = branches[i];
            var v = new CatView();

            v.Root = OptionsKit.Rect("Cat_" + b.Id, page, CatX, BodyY + i * (CatH + CatGap), CatW, CatH);
            v.Back = v.Root.gameObject.AddComponent<Image>();
            v.Back.type = Image.Type.Sliced;
            v.Back.raycastTarget = true;

            OptionsKit.Img("Socket", v.Root, 5, 6, 18, 18, GameHudSkin.Socket, true);
            v.IconImg = OptionsKit.Img("Icon", v.Root, 0, 0, 1, 1, b.Icon);
            FitIcon(v.IconImg, b.Icon, 5, 6, 18, 18, 1);

            // In der Demo gesperrt: Schloss rechts, der Name macht ihm Platz.
            bool demoLocked = Demo.IsSkillCategoryLocked(b.Category);
            v.Label = OptionsKit.Label("Name", v.Root, 27, 3, demoLocked ? CatW - 41 : CatW - 29, 13, b.Name.ToUpperInvariant(), textFont,
                                       OptionsKit.SizeText, GameHudSkin.Cream, TextAlignmentOptions.Left);
            if (demoLocked)
            {
                Sprite lockSprite = GameHudSkin.Lock;
                OptionsKit.Img("DemoLock", v.Root, CatW - 13, Mathf.Floor((CatH - lockSprite.rect.height) / 2f),
                               lockSprite.rect.width, lockSprite.rect.height, lockSprite);
            }
            v.Label.overflowMode = TextOverflowModes.Ellipsis;
            v.Count = OptionsKit.Label("Count", v.Root, 27, 14, CatW - 29, 13, "", textFont,
                                       OptionsKit.SizeText, GameHudSkin.Stone, TextAlignmentOptions.Left);

            PointerRelay relay = v.Root.gameObject.AddComponent<PointerRelay>();
            relay.Enter = () => { hoverBranch = index; RefreshCategories(); ShowDetail(); };
            relay.Exit = () =>
            {
                if (hoverBranch != index) return;
                hoverBranch = -1;
                RefreshCategories();
                ShowDetail();
            };
            relay.Down = () =>
            {
                if (index == selectedBranch) return;
                OptionsKit.PlayClick();
                SelectBranch(index);
            };

            cats.Add(v);
        }
    }

    void BuildDetail()
    {
        OptionsKit.Img("DetailWell", page, DetX, BodyY, DetW, BodyH, GameHudSkin.Well, true);

        int px = DetX + (DetW - PlateS) / 2, py = BodyY + 7;
        plateImg = OptionsKit.Img("Plate", page, px, py, PlateS, PlateS, GameHudSkin.IconPlate(false));

        int s = HubSkilltreeGraph.Shape, o = (PlateS - s) / 2;
        shapeShadow = OptionsKit.Img("ShapeShadow", page, px + o, py + o + 2, s, s, null);
        shapeFill = OptionsKit.Img("ShapeFill", page, px + o, py + o, s, s, null);
        shapeGloss = OptionsKit.Img("ShapeGloss", page, px + o, py + o, s, s, null);
        shapeOutline = OptionsKit.Img("ShapeOutline", page, px + o, py + o, s, s, null);
        plateIcon = OptionsKit.Img("PlateIcon", page, px, py, 1, 1, null);

        int tx = DetX + 5, tw = DetW - 10;
        detailName = OptionsKit.Label("Name", page, tx, py + PlateS + 4, tw, 26, "", textFont,
                                      OptionsKit.SizeText, GameHudSkin.Gold, TextAlignmentOptions.Top);
        detailName.textWrappingMode = TextWrappingModes.Normal;
        detailSub = OptionsKit.Label("Sub", page, tx, py + PlateS + 29, tw, 13, "", textFont,
                                     OptionsKit.SizeText, GameHudSkin.Stone, TextAlignmentOptions.Center);

        detailRule = OptionsKit.Img("Rule", page, DetX + 5, py + PlateS + 44, DetW - 10, 2, GameHudSkin.Rule, true);

        detailDesc = OptionsKit.Label("Desc", page, tx, py + PlateS + 48, tw, 40, "", textFont,
                                      OptionsKit.SizeText, GameHudSkin.Parchment, TextAlignmentOptions.TopLeft);
        detailDesc.textWrappingMode = TextWrappingModes.Normal;
        detailDesc.richText = true;

        int sy = BodyY + BodyH - 17;
        OptionsKit.Img("StatusPlate", page, DetX + 3, sy, DetW - 6, 14, GameHudSkin.Plate, true);
        statusIcon = OptionsKit.Img("StatusIcon", page, DetX + 7, sy, 1, 1, null);
        statusText = OptionsKit.Label("Status", page, DetX + 19, sy + 1, DetW - 24, 13, "", textFont,
                                      OptionsKit.SizeText, GameHudSkin.Cream, TextAlignmentOptions.Left);
        statusText.overflowMode = TextOverflowModes.Ellipsis;
    }

    void BuildFooter()
    {
        SkinButton.Create(page, InX, FootY, BackW, FootH, Loc.Get("ui.skilltree.back", "ZURÜCK"), textFont,
                          SkinButton.Kind.Wood, Close);

        resetButton = SkinButton.Create(page, InX + BackW + 4, FootY, ResetW, FootH,
                                        Loc.Get("ui.skilltree.reset", "RESET"), textFont,
                                        SkinButton.Kind.Danger, AskReset);

        int hintX = InX + BackW + 4 + ResetW + 6;
        OptionsKit.Label("Hint", page, hintX, FootY + 2, DetX - hintX - 6, 13,
                         Loc.Get("ui.skilltree.hint", "W/S PFAD  A/D KNOTEN  ENTER LERNEN"), textFont,
                         OptionsKit.SizeText, GameHudSkin.Stone, TextAlignmentOptions.Center);

        learnButton = SkinButton.Create(page, DetX, FootY, DetW, FootH, Loc.Get("ui.skilltree.learn", "LERNEN"),
                                        textFont, SkinButton.Kind.Primary, LearnSelected, GameHudSkin.Gem);
    }

    // ==================================================================
    //  Laufzeit
    // ==================================================================

    void Update()
    {
        if (!IsOpen || page == null) return;

        var size = new Vector2Int(Screen.width, Screen.height);
        if (size != lastScreen)
        {
            lastScreen = size;
            OptionsKit.Layout(page, scaler, Content);
            if (dialogPage != null) dialogPage.anchoredPosition = page.anchoredPosition;
        }

        graph.Animate(Time.unscaledTime);

        // Das [E], mit dem das Fenster aufgeht, darf es nicht gleich schliessen.
        if (Time.frameCount == openedOnFrame) return;

        // Offene Rueckfrage: nur ESC/E bricht ab, der Baum dahinter ruht.
        if (dialog != null)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))
            {
                OptionsKit.PlayClick();
                CloseDialog();
            }
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))
        {
            OptionsKit.PlayClick();
            Close();
            return;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) StepBranch(-1);
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) StepBranch(+1);
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) StepNode(-1);
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) StepNode(+1);
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
            Input.GetKeyDown(KeyCode.Space))
            LearnSelected();

        float wheel = Input.mouseScrollDelta.y;
        if (!Mathf.Approximately(wheel, 0f)) graph.Scroll(wheel > 0f ? -1f : 1f);
    }

    void StepBranch(int delta)
    {
        if (branches.Count == 0) return;
        int next = Mathf.Clamp(selectedBranch + delta, 0, branches.Count - 1);
        if (next == selectedBranch) return;
        SelectBranch(next);
    }

    void StepNode(int delta)
    {
        IReadOnlyList<SkillNodeDef> order = graph.Order;
        if (order.Count == 0) return;

        int i = selectedNode != null ? IndexOf(order, selectedNode) : -1;
        i = i < 0 ? (delta > 0 ? 0 : order.Count - 1) : Mathf.Clamp(i + delta, 0, order.Count - 1);
        SelectNode(order[i]);
        PlaySfx(moveClip);
    }

    static int IndexOf(IReadOnlyList<SkillNodeDef> list, SkillNodeDef def)
    {
        for (int i = 0; i < list.Count; i++) if (list[i] == def) return i;
        return -1;
    }

    void SelectBranch(int index)
    {
        selectedBranch = index;
        selectedNode = null;
        PlaySfx(moveClip);
        graph.Show(Current);
        Refresh();
    }

    void SelectNode(SkillNodeDef node)
    {
        selectedNode = node;
        graph.Selected = node;
        Refresh();
    }

    void OnNodeClicked(SkillNodeDef node)
    {
        // Zweiter Klick auf den gewaehlten Knoten lernt ihn.
        if (node == selectedNode && CanLearn(node))
        {
            LearnSelected();
            return;
        }

        OptionsKit.PlayClick();
        SelectNode(node);
    }

    static bool CanLearn(SkillNodeDef node) =>
        node != null && !node.IsStart && !Skills.IsUnlocked(node) && Skills.CanUnlock(node);

    void LearnSelected()
    {
        SkillNodeDef node = selectedNode;
        if (node == null || node.IsStart || Skills.IsUnlocked(node)) return;

        bool bought = Skills.TryUnlock(node);

        // Gelernt klingt wie ein Menueklick, abgelehnt wie ein Treffer - wie im Shop.
        AudioController ac = AudioController.Instance;
        if (ac != null) ac.PalySound(bought ? ac.MenuClick : ac.PlayerHit);

        graph.Refresh();
        Refresh();
    }

    // ==================================================================
    //  Zuruecksetzen
    // ==================================================================

    void AskReset()
    {
        SkillTreeDef tree = Skills.ActiveTree;
        if (tree == null || dialog != null || Skills.SpentIn(tree) <= 0) return;

        // Eigene Ebene ueber der Seite: als letztes Kind gezeichnet, und die
        // vollflaechige Abdunklung faengt jeden Klick daneben ab.
        Image shade = OptionsKit.Stretch("Dialog", canvasGo.transform, GameHudSkin.White,
                                         new Color(0.06f, 0.04f, 0.05f, 0.7f));
        shade.raycastTarget = true;
        dialog = shade.rectTransform;

        dialogPage = OptionsKit.Rect("Page", dialog, 0, 0, OptionsKit.RefW, OptionsKit.RefH);
        dialogPage.anchoredPosition = page.anchoredPosition;

        string question = string.Format(
            Loc.Get("ui.skilltree.reset.confirm", "Skilltree von {0} zurücksetzen? Du bekommst {1} Skillpunkte zurück."),
            Characters.DisplayName(Shop.SkinIndex), Skills.SpentIn(tree));

        OptionsKit.Img("Card", dialogPage, DlgX, DlgY, DlgW, DlgH, GameHudSkin.Card, true);
        TextMeshProUGUI q = OptionsKit.Label("Question", dialogPage, DlgX + 10, DlgTextY, DlgW - 20, DlgTextH,
                                             question, textFont, OptionsKit.SizeText, GameHudSkin.Parchment,
                                             TextAlignmentOptions.Center);
        q.textWrappingMode = TextWrappingModes.Normal;

        int gap = DlgW - 20 - 2 * DlgBtnW;
        SkinButton.Create(dialogPage, DlgX + 10, DlgBtnY, DlgBtnW, DlgBtnH,
                          Loc.Get("ui.pause.dialog.cancel", "ABBRECHEN"), textFont, SkinButton.Kind.Wood,
                          CloseDialog);
        SkinButton.Create(dialogPage, DlgX + 10 + DlgBtnW + gap, DlgBtnY, DlgBtnW, DlgBtnH,
                          Loc.Get("ui.pause.dialog.confirm", "BESTÄTIGEN"), textFont, SkinButton.Kind.Danger,
                          ConfirmReset);
    }

    void CloseDialog()
    {
        if (dialog == null) return;
        dialog.SetParent(null, false);
        Destroy(dialog.gameObject);
        dialog = dialogPage = null;
    }

    void ConfirmReset()
    {
        CloseDialog();

        selectedNode = null;
        graph.Selected = null;
        Skills.ResetActiveTree();   // feuert Changed -> Baum und Anzeige frisch

        AudioController ac = AudioController.Instance;
        if (ac != null) ac.PalySound(ac.MenuClick);
    }

    // ==================================================================
    //  Anzeige
    // ==================================================================

    void Refresh()
    {
        if (page == null) return;

        pointsText.text = string.Format(Loc.Get("ui.skilltree.points", "{0} SKILLPUNKTE"), Skills.Points);

        // Charakter-Level und XP bis zum naechsten, gleich hinter dem Namen.
        double xp = Skills.Xp;
        int xpLevel = CharLevel.LevelFor(xp);
        double next = CharLevel.XpForLevel(Mathf.Min(xpLevel + 1, CharLevel.MaxLevel));
        string xpText = string.Format(Loc.Get("ui.skilltree.xp", "{0}/{1} XP"), FormatXp(xp), FormatXp(next));
        treeName.text = Characters.DisplayName(Shop.SkinIndex).ToUpperInvariant() + "  " +
                        string.Format(Loc.Get("ui.skilltree.charlevel", "LV {0}"), Skills.Level) +
                        "  <color=#" + ColorUtility.ToHtmlStringRGB(GameHudSkin.Stone) + ">" + xpText + "</color>";
        SetFill(xpFill, XpBarW, CharLevel.Progress(xp));

        int learned = 0, total = 0;
        foreach (SkillBranchDef b in branches)
        {
            CountBranch(b, out int d, out int t);
            learned += d;
            total += t;
        }
        learnedText.text = string.Format(Loc.Get("ui.skilltree.learned", "GELERNT {0}/{1}"), learned, total);
        SetFill(learnedFill, 78, total > 0 ? learned / (float)total : 0f);

        // Nichts gelernt, nichts zurueckzugeben.
        resetButton.Disabled = Skills.SpentIn(Skills.ActiveTree) <= 0;

        RefreshCategories();
        RefreshBanner();
        ShowDetail();
    }

    static void CountBranch(SkillBranchDef b, out int done, out int total)
    {
        done = total = 0;
        foreach (SkillNodeDef n in b.Nodes)
        {
            if (n.IsStart) continue;
            total++;
            if (Skills.IsUnlocked(n)) done++;
        }
    }

    void RefreshCategories()
    {
        for (int i = 0; i < cats.Count && i < branches.Count; i++)
        {
            CatView v = cats[i];
            SkillBranchDef b = branches[i];
            bool sel = i == selectedBranch, hov = i == hoverBranch;

            // Die gewaehlte ragt vier Pixel ins Feld - wie ein Reiter.
            OptionsKit.Move(v.Root, CatX, BodyY + i * (CatH + CatGap), sel ? CatW + 6 : CatW, CatH);

            if (sel)
            {
                v.Back.sprite = GameHudSkin.TintButton(true);
                v.Back.color = b.Color;
            }
            else
            {
                v.Back.sprite = GameHudSkin.Button(hov ? GameHudSkin.ButtonLook.Hover : GameHudSkin.ButtonLook.Wood);
                v.Back.color = Color.white;
            }

            Color ink = sel ? InkOn(b.Color) : (Color)GameHudSkin.Cream;
            v.Label.color = ink;

            CountBranch(b, out int d, out int t);
            v.Count.text = Demo.IsSkillCategoryLocked(b.Category) ? Demo.LockedLabel : $"{d}/{t}";
            v.Count.color = sel ? new Color(ink.r, ink.g, ink.b, 0.75f)
                          : d == t && t > 0 ? (Color)GameHudSkin.Mint : (Color)GameHudSkin.ParchDark;
        }
    }

    void RefreshBanner()
    {
        SkillBranchDef b = Current;
        banner.enabled = b != null;
        if (b == null)
        {
            bannerText.text = bannerShadow.text = "";
            bannerIcon.enabled = false;
            return;
        }

        banner.color = b.Color;
        Color ink = InkOn(b.Color);
        bannerText.color = ink;
        bannerShadow.color = ink == (Color)GameHudSkin.Ink ? new Color(1f, 1f, 1f, 0.35f) : Shade(b.Color, 0.55f);
        bannerText.text = bannerShadow.text = (string.IsNullOrWhiteSpace(b.Path) ? b.Name : b.Path).ToUpperInvariant();

        FitIcon(bannerIcon, b.Icon, FieldX + 6, BodyY + 3, 14, BannerH - 1, 1);
    }

    /// <summary>
    /// Die Karte rechts, der Reihe nach: Knoten unter der Maus, Kategorie unter
    /// der Maus (Vorschau vor dem Klick), gewaehlter Knoten, gewaehlte Kategorie.
    /// </summary>
    void ShowDetail()
    {
        if (page == null) return;

        SkillNodeDef node = graph.Hovered;
        SkillBranchDef branch = null;

        if (node == null && hoverBranch >= 0 && hoverBranch < branches.Count) branch = branches[hoverBranch];
        else if (node == null) node = selectedNode;
        if (node == null && branch == null) branch = Current;

        if (node != null) ShowNode(node);
        else ShowBranch(branch);

        ReflowDetail();

        // Der Knopf gilt immer dem GEWAEHLTEN Knoten, nicht dem unter der Maus.
        SkillNodeDef target = selectedNode;
        learnButton.gameObject.SetActive(target != null && !target.IsStart && !Skills.IsUnlocked(target));
        learnButton.Disabled = !CanLearn(target);
        if (target != null)
            learnButton.SetText(string.Format(Loc.Get("ui.skilltree.learn.cost", "LERNEN  {0}"), target.Price));
    }

    void ShowNode(SkillNodeDef node)
    {
        Color accent = node.Branch != null ? node.Branch.Color : Color.white;
        bool unlocked = node.IsStart || Skills.IsUnlocked(node);
        bool open = !unlocked && Skills.RequirementsMet(node);
        bool affordable = open && Skills.CanAfford(node);

        SetShape(node.Shape, true);
        shapeFill.color = unlocked ? accent : open ? (Color)GameHudSkin.Trough : (Color)GameHudSkin.StoneDark;
        shapeOutline.color = unlocked ? Shade(accent, 0.5f) : open ? accent : GameHudSkin.Hex(0x3a2e34);
        shapeGloss.color = new Color(1f, 1f, 1f, unlocked ? 0.4f : 0.1f);
        shapeShadow.color = new Color(0f, 0f, 0f, 0.5f);

        Sprite symbol = node.Icon;
        if (symbol == null && !unlocked && !open) symbol = GameHudSkin.Lock;
        FitPlateIcon(symbol, unlocked || node.Icon == null ? Color.white : new Color(1f, 1f, 1f, 0.6f));

        detailName.text = node.Name.ToUpperInvariant();
        detailName.color = unlocked ? GameHudSkin.Gold : GameHudSkin.Cream;
        detailSub.text = node.Branch != null ? node.Branch.Name.ToUpperInvariant() : "";
        detailSub.color = Lift(accent, 0.25f);
        detailDesc.text = node.Description;

        if (node.IsStart)
            SetStatus(null, Loc.Get("ui.skilltree.status.start", "Hier beginnt der Pfad."), GameHudSkin.Stone);
        else if (unlocked)
            SetStatus(GameHudSkin.Check, Loc.Get("ui.skilltree.status.owned", "GELERNT"), GameHudSkin.Mint);
        else if (open)
            SetStatus(GameHudSkin.Gem,
                      string.Format(Loc.Get("ui.skilltree.status.price", "{0} SKILLPUNKTE"), node.Price),
                      affordable ? GameHudSkin.Gold : GameHudSkin.JamLight);
        else
            SetStatus(GameHudSkin.Lock, MissingText(node), GameHudSkin.JamLight);
    }

    void ShowBranch(SkillBranchDef b)
    {
        if (b == null)
        {
            SetShape(SkillShape.Kreis, false);
            FitPlateIcon(null, Color.white);
            detailName.text = detailSub.text = detailDesc.text = "";
            SetStatus(null, "", GameHudSkin.Stone);
            return;
        }

        SetShape(SkillShape.Kreis, true);
        shapeFill.color = b.Color;
        shapeOutline.color = Shade(b.Color, 0.5f);
        shapeGloss.color = new Color(1f, 1f, 1f, 0.4f);
        shapeShadow.color = new Color(0f, 0f, 0f, 0.5f);
        FitPlateIcon(b.Icon, Color.white);

        detailName.text = b.Name.ToUpperInvariant();
        detailName.color = Lift(b.Color, 0.35f);
        detailSub.text = (string.IsNullOrWhiteSpace(b.Path) ? "" : b.Path).ToUpperInvariant();
        detailSub.color = GameHudSkin.Stone;
        // Das Zitat steht unter der Beschreibung, eine Stufe leiser.
        detailDesc.text = string.IsNullOrWhiteSpace(b.Quote)
            ? b.Description
            : $"{b.Description}\n<color=#{ColorUtility.ToHtmlStringRGB(GameHudSkin.StoneLight)}>{b.Quote}</color>";

        if (Demo.IsSkillCategoryLocked(b.Category))
        {
            SetStatus(GameHudSkin.Lock, Demo.LockedHint, GameHudSkin.JamLight);
            return;
        }

        CountBranch(b, out int d, out int t);
        SetStatus(d == t && t > 0 ? GameHudSkin.Check : null,
                  string.Format(Loc.Get("ui.skilltree.branch.count", "{0} von {1} gelernt"), d, t),
                  d == t && t > 0 ? GameHudSkin.Mint : GameHudSkin.StoneLight);
    }

    /// <summary>
    /// Name, Unterzeile, Linie und Text ruecken zusammen, wenn der Name nur
    /// eine Zeile braucht - der Text bekommt dann eine Zeile mehr.
    /// </summary>
    void ReflowDetail()
    {
        int tx = DetX + 5, tw = DetW - 10;
        int y = BodyY + 7 + PlateS + 4;

        float pref = string.IsNullOrEmpty(detailName.text) ? 0f : detailName.GetPreferredValues(detailName.text, tw, 0f).y;
        int nameH = pref > 14f ? 25 : 13;
        OptionsKit.Move(detailName.rectTransform, tx, y, tw, nameH + 1);
        y += nameH;

        OptionsKit.Move(detailSub.rectTransform, tx, y, tw, 13);
        y += 15;

        OptionsKit.Move(detailRule.rectTransform, DetX + 5, y, DetW - 10, 2);
        y += 4;

        int statusY = BodyY + BodyH - 17;
        OptionsKit.Move(detailDesc.rectTransform, tx, y, tw, statusY - y - 1);
    }

    void SetShape(SkillShape shape, bool visible)
    {
        shapeShadow.enabled = shapeFill.enabled = shapeGloss.enabled = shapeOutline.enabled = visible;
        if (!visible) return;
        shapeShadow.sprite = detailShapes.Fill(shape);
        shapeFill.sprite = detailShapes.Fill(shape);
        shapeGloss.sprite = detailShapes.Gloss(shape);
        shapeOutline.sprite = detailShapes.Outline(shape);
    }

    /// <summary>Symbol mittig auf die Plakette, in ganzem Vielfachen seiner Groesse (hoechstens 16 px).</summary>
    void FitPlateIcon(Sprite s, Color color)
    {
        int px = DetX + (DetW - PlateS) / 2, py = BodyY + 7;
        FitIcon(plateIcon, s, px, py, PlateS, PlateS, 16);
        plateIcon.color = color;
    }

    void SetStatus(Sprite icon, string text, Color color)
    {
        int sy = BodyY + BodyH - 17;
        statusIcon.enabled = icon != null;
        if (icon != null)
        {
            statusIcon.sprite = icon;
            OptionsKit.Move(statusIcon.rectTransform, DetX + 7 + Mathf.Floor((9 - icon.rect.width) / 2f),
                            sy + Mathf.Floor((14 - icon.rect.height) / 2f), icon.rect.width, icon.rect.height);
        }

        statusText.text = text;
        statusText.color = color;
        OptionsKit.Move(statusText.rectTransform, icon != null ? DetX + 19 : DetX + 7, sy + 1,
                        icon != null ? DetW - 24 : DetW - 12, 13);
        statusText.alignment = icon != null ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;
    }

    /// <summary>Welche Knoten noch fehlen. Mehr als zwei werden nicht aufgezaehlt.</summary>
    static string MissingText(SkillNodeDef node)
    {
        if (Skills.IsDemoLocked(node)) return Demo.LockedHint;

        string locked = Loc.Get("ui.skilltree.status.locked", "GESPERRT");
        var missing = new List<string>();
        foreach (SkillNodeDef parent in node.Requires)
            if (!Skills.IsUnlocked(parent)) missing.Add(parent.Name);

        if (missing.Count == 0 || missing.Count > 2) return locked;
        return string.Format(Loc.Get("ui.skilltree.status.needs", "BRAUCHT: {0}"), string.Join(" + ", missing));
    }

    // ==================================================================
    //  Helfer
    // ==================================================================

    /// <summary>
    /// Setzt ein Bild mittig in einen Kasten - in seiner echten Groesse oder
    /// einem ganzen Vielfachen, solange es hoechstens <paramref name="maxSize"/>
    /// breit wird. Nie gestreckt.
    /// </summary>
    static void FitIcon(Image img, Sprite s, float x, float y, float w, float h, int maxSize)
    {
        img.enabled = s != null;
        if (s == null) return;
        img.sprite = s;

        float sw = s.rect.width, sh = s.rect.height;
        float k = Mathf.Max(1f, Mathf.Floor(maxSize / Mathf.Max(sw, sh)));
        sw *= k;
        sh *= k;
        OptionsKit.Move(img.rectTransform, x + Mathf.Floor((w - sw) / 2f), y + Mathf.Floor((h - sh) / 2f), sw, sh);
    }

    /// <summary>XP kurz genug fuer die Kopfzeile: bis 99.999 voll, darueber in k bzw. M.</summary>
    static string FormatXp(double xp)
    {
        if (xp < 100000) return System.Math.Floor(xp).ToString("N0");
        if (xp < 10000000) return System.Math.Floor(xp / 1000).ToString("N0") + "k";
        return (xp / 1000000).ToString("0.#") + "M";
    }

    static void SetFill(Image fill, float innerWidth, float t)
    {
        RectTransform rt = fill.rectTransform;
        float w = Mathf.Round(innerWidth * Mathf.Clamp01(t));
        rt.sizeDelta = new Vector2(w, rt.sizeDelta.y);
        fill.enabled = w > 0f;
    }

    /// <summary>Hellt eine Farbe auf, ohne ihre Deckkraft anzutasten.</summary>
    public static Color Lift(Color c, float amount) =>
        new Color(Mathf.Lerp(c.r, 1f, amount), Mathf.Lerp(c.g, 1f, amount), Mathf.Lerp(c.b, 1f, amount), c.a);

    /// <summary>Dunkelt eine Farbe ab, ohne ihre Deckkraft anzutasten.</summary>
    public static Color Shade(Color c, float amount) =>
        new Color(Mathf.Lerp(c.r, 0f, amount), Mathf.Lerp(c.g, 0f, amount), Mathf.Lerp(c.b, 0f, amount), c.a);

    /// <summary>
    /// Welche Schriftfarbe auf dieser Flaeche lesbar ist. Blau, Rot und Gruen
    /// vertragen helle Schrift - Gelb nicht, darauf wird sie dunkel.
    /// </summary>
    public static Color InkOn(Color background)
    {
        float luma = 0.299f * background.r + 0.587f * background.g + 0.114f * background.b;
        return luma > 0.6f ? (Color)GameHudSkin.Ink : (Color)GameHudSkin.Cream;
    }

    void PlaySfx(AudioClip clip)
    {
        if (clip == null) return;
        if (sfxSource == null) sfxSource = GetComponent<AudioSource>();
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }
        sfxSource.PlayOneShot(clip, sfxVolume);
    }
}
