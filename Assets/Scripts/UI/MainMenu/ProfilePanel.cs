using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Profilauswahl im Hauptmenue: drei Karten nebeneinander, je ein Spielstand
/// (<see cref="SaveProfiles"/>).
///
///   ProfilePanel.Open(play);
///
/// Aufbau (Seitenpixel, 480x270, Stil wie <see cref="OptionsPanel"/>):
///   Titelband   PROFILE
///   3 Karten    PROFIL n / AKTIV, Portraet des gewaehlten Charakters, Name,
///               Glutsplitter, Muenzen, Erfolge, Spielzeit, zuletzt gespielt,
///               SPIELEN bzw. AUSWAEHLEN, UMBENENNEN, LOESCHEN
///               (leerer Platz: NEUES PROFIL)
///   Fussleiste  Tastenhinweise                       ZURUECK
///
/// Umbenennen/Anlegen fragt per Eingabefeld, Loeschen muss man gedrueckt
/// halten - ein versehentlicher Klick kostet so nie einen Spielstand.
/// Steuerung: Maus, Pfeiltasten/AD + Enter, F2, Entf, Esc; Controller mit
/// Steuerkreuz, A (waehlen), B (zurueck), X (umbenennen), Y (loeschen).
/// </summary>
public class ProfilePanel : MonoBehaviour
{
    // ==================================================================
    //  Masse (Seitenpixel, Ursprung oben links)
    // ==================================================================

    private const int RibbonY = 12;
    private const int CardW = 136, CardH = 180, CardGap = 12, CardY = 42;
    private const int CardX0 = (OptionsKit.RefW - (3 * CardW + 2 * CardGap)) / 2;
    private static readonly RectInt Content = new RectInt(CardX0 - 12, RibbonY, 3 * CardW + 2 * CardGap + 24, 250);

    private const int PortraitSize = 42, PortraitY = 19;
    private const int NameY = 63;
    private const int StatY0 = 84, StatStep = 11;
    private const int BtnX = 8, BtnW = CardW - 16, PrimaryY = 142, PrimaryH = 16, SecondY = 160, SecondH = 15;

    private const int ToastY = 227;
    private const int FootY = 243, FootH = 18, BackW = 76;

    private const float HoldTime = 1.1f;

    // ==================================================================
    //  Zustand
    // ==================================================================

    private class CardView
    {
        public int slot;
        public RectTransform root;
        public CanvasGroup group;
        public Image ring, flash;
        public Image portrait;
        public Sprite[] frames;
        public SkinButton primary, rename, delete;
        public float lift, flashT;
    }

    private static ProfilePanel instance;
    public static bool IsOpen => instance != null;

    private System.Action play;

    private TMP_FontAsset textFont, pixelFont;
    private RectTransform page, head;
    private CanvasGroup headGroup;
    private CanvasScaler scaler;
    private Image dim;
    private Vector2Int lastScreen;

    private readonly CardView[] cards = new CardView[SaveProfiles.SlotCount];
    private RectTransform cardLayer;
    private int focus;
    private float openTime;
    private bool intro = true;
    private int openedFrame;

    private TextMeshProUGUI toast;
    private float toastT;

    // ---------- Dialoge ----------
    private enum DialogKind { None, Name, Delete }
    private DialogKind dialogKind;
    private RectTransform dialog, dialogPage;
    private int dialogSlot;
    private bool dialogCreate;
    private TMP_InputField nameField;
    private TextMeshProUGUI nameCounter;
    private float dialogOpenTime;

    private SkinButton holdButton;
    private Image holdFill;
    private float holdProgress;
    private bool holdPointerDown, holdPointerInside, holdKeyArmed;

    // ==================================================================
    //  Oeffnen / Schliessen
    // ==================================================================

    /// <param name="playAction">Was SPIELEN auf der Karte des aktiven Profils tut.</param>
    public static void Open(System.Action playAction = null)
    {
        if (instance != null) return;
        GameObject go = new GameObject("ProfilePanel");
        instance = go.AddComponent<ProfilePanel>();
        instance.play = playAction;
    }

    public static void Close()
    {
        if (instance == null) return;
        Destroy(instance.gameObject);
        instance = null;
    }

    private void Awake()
    {
        instance = this;
        openedFrame = Time.frameCount;
        openTime = Time.unscaledTime;
        textFont = PixelUI.FindTextFont();
        pixelFont = PixelUI.FindPixelFont();
        focus = SaveProfiles.Active;
        Build();
    }

    private void OnEnable() => Loc.LanguageChanged += OnLanguageChanged;
    private void OnDisable() => Loc.LanguageChanged -= OnLanguageChanged;

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void OnLanguageChanged()
    {
        CloseDialog();
        OptionsKit.Clear(transform);
        textFont = PixelUI.FindTextFont();
        intro = false;
        Build();
    }

    // ==================================================================
    //  Aufbau
    // ==================================================================

    private void Build()
    {
        // Gleiche Ebene wie die Optionen - beide gehen nur aus dem Hauptmenue auf.
        page = OptionsKit.CreatePage(gameObject, 210, 0.82f, out scaler);
        dim = transform.Find("Dim").GetComponent<Image>();

        head = OptionsKit.Rect("Head", page, 0, 0, OptionsKit.RefW, OptionsKit.RefH);
        headGroup = head.gameObject.AddComponent<CanvasGroup>();
        headGroup.blocksRaycasts = false;
        OptionsKit.Ribbon(head, OptionsKit.RefW / 2f, RibbonY, Loc.Get("ui.profile.title", "PROFILE"),
                          pixelFont, textFont);

        cardLayer = OptionsKit.Rect("Cards", page, 0, 0, OptionsKit.RefW, OptionsKit.RefH);
        BuildCards();

        toast = OptionsKit.ShadowLabel("Toast", page, 0, ToastY, OptionsKit.RefW, 12, "", textFont,
                                       OptionsKit.SizeText, GameHudSkin.Gold, TextAlignmentOptions.Center);
        toast.alpha = 0f;
        SetShadowAlpha(0f);

        BuildFooter();

        OptionsKit.Layout(page, scaler, Content);
        lastScreen = new Vector2Int(Screen.width, Screen.height);
        Animate(0f);
    }

    private void BuildFooter()
    {
        string hint = Loc.Get("ui.profile.hints", "PFEILE wählen    ENTER bestätigen    F2 umbenennen    ENTF löschen");
        OptionsKit.Label("Hints", page, CardX0, FootY + 3, 340, 12, hint, textFont, OptionsKit.SizeText,
                         GameHudSkin.StoneLight, TextAlignmentOptions.Left);

        SkinButton.Create(page, CardX0 + 3 * CardW + 2 * CardGap - BackW, FootY, BackW, FootH,
                          Loc.Get("ui.profile.back", "ZURÜCK"), textFont, SkinButton.Kind.Wood, Close);
    }

    private void BuildCards()
    {
        // Vorrat auf die Platte, damit die Karte des aktiven Profils den echten Stand liest.
        SaveProfiles.FlushActive();

        OptionsKit.Clear(cardLayer);
        for (int i = 0; i < cards.Length; i++) cards[i] = BuildCard(i);
    }

    private CardView BuildCard(int slot)
    {
        bool used = SaveProfiles.IsUsed(slot);
        bool active = used && SaveProfiles.Active == slot;
        ProfileSummary sum = ProfileSummary.Read(slot);

        var v = new CardView { slot = slot };
        v.root = OptionsKit.Rect("Card" + (slot + 1), cardLayer, CardX(slot), CardY, CardW, CardH);
        v.group = v.root.gameObject.AddComponent<CanvasGroup>();

        Image bg = OptionsKit.Img("Bg", v.root, 0, 0, CardW, CardH, GameHudSkin.Card, true);
        bg.raycastTarget = true;
        PointerRelay relay = bg.gameObject.AddComponent<PointerRelay>();
        relay.Enter = () => { if (dialog == null) SetFocus(slot, false); };
        relay.Down = () => { if (dialog == null) SetFocus(slot, false); };

        v.ring = OptionsKit.Img("Ring", v.root, 0, 0, CardW, CardH, GameHudSkin.Ring, OptionsKit.WithAlpha(GameHudSkin.Gold, 0f), true);

        // Kopf: Platznummer links, AKTIV rechts.
        OptionsKit.Label("Slot", v.root, 9, 4, 80, 12,
                         string.Format(Loc.Get("ui.profile.slot", "PROFIL {0}"), slot + 1), textFont,
                         OptionsKit.SizeText, GameHudSkin.StoneLight, TextAlignmentOptions.Left);
        if (active) BuildActiveChip(v.root);

        if (used) BuildUsedBody(v, sum, active);
        else BuildEmptyBody(v);

        v.flash = OptionsKit.Img("Flash", v.root, 0, 0, CardW, CardH, GameHudSkin.Card, Color.clear, true);
        return v;
    }

    private void BuildActiveChip(RectTransform card)
    {
        string text = Loc.Get("ui.profile.active", "AKTIV");
        TextMeshProUGUI label = OptionsKit.Label("Active", card, 0, 4, CardW - 9, 12, text, textFont,
                                                 OptionsKit.SizeText, GameHudSkin.Mint, TextAlignmentOptions.Right);
        float w = Mathf.Ceil(OptionsKit.Measure(label, text));
        Sprite check = GameHudSkin.Check;
        OptionsKit.Img("Check", card, CardW - 9 - w - 3 - check.rect.width, 6, check.rect.width, check.rect.height, check);
    }

    private void BuildUsedBody(CardView v, ProfileSummary sum, bool active)
    {
        // Portraet des Charakters, mit dem dieses Profil zuletzt unterwegs war.
        v.frames = CharacterLooks.PortraitFor(sum.Character) ?? CharacterLooks.PortraitFor(0);
        float px = (CardW - PortraitSize) / 2;
        OptionsKit.Img("PortraitShadow", v.root, px + 1, PortraitY + 2, PortraitSize, PortraitSize,
                       GameHudSkin.White, OptionsKit.WithAlpha(GameHudSkin.Night, 0.6f));
        v.portrait = OptionsKit.Img("Portrait", v.root, px, PortraitY, PortraitSize, PortraitSize,
                                    v.frames != null ? v.frames[0] : null);
        if (v.frames == null) v.portrait.color = Color.clear;

        string name = SaveProfiles.NameOf(v.slot);
        OptionsKit.ShadowLabel("Name", v.root, 4, NameY, CardW - 8, 14, name, textFont, OptionsKit.SizeText,
                               GameHudSkin.Cream, TextAlignmentOptions.Center);

        OptionsKit.Img("Divider", v.root, 12, NameY + 16, CardW - 24, 1, GameHudSkin.White, (Color)GameHudSkin.StoneDark);

        int row = 0;
        if (sum.FinaleDone)
            StatRow(v.root, row++, GameHudSkin.Crown, Loc.Get("ui.profile.stat.story.done", "Story geschafft"), "");
        else
            StatRow(v.root, row++, GameHudSkin.Gem, Loc.Get("ui.profile.stat.story", "Glutsplitter"),
                    sum.Shards + "/" + StoryProgress.ShardWorlds.Length);
        StatRow(v.root, row++, GameHudSkin.Coin, Loc.Get("ui.profile.stat.coins", "Münzen"), FormatNumber(sum.Coins));
        StatRow(v.root, row++, GameHudSkin.Star, Loc.Get("ui.profile.stat.achievements", "Erfolge"),
                sum.Achievements + "/" + sum.AchievementsTotal);
        StatRow(v.root, row++, GameHudSkin.Clock, Loc.Get("ui.profile.stat.time", "Spielzeit"),
                FormatPlayTime(SaveProfiles.PlaySeconds(v.slot)));
        StatRow(v.root, row, null, Loc.Get("ui.profile.stat.last", "Zuletzt"), FormatDate(SaveProfiles.LastPlayed(v.slot)));

        int slot = v.slot;
        v.primary = active
            ? SkinButton.Create(v.root, BtnX, PrimaryY, BtnW, PrimaryH, Loc.Get("ui.profile.play", "SPIELEN"),
                                textFont, SkinButton.Kind.Primary, () => Primary(slot))
            : SkinButton.Create(v.root, BtnX, PrimaryY, BtnW, PrimaryH, Loc.Get("ui.profile.select", "AUSWÄHLEN"),
                                textFont, SkinButton.Kind.Wood, () => Primary(slot));

        int half = (BtnW - 2) / 2;
        v.rename = SkinButton.Create(v.root, BtnX, SecondY, half, SecondH, Loc.Get("ui.profile.rename", "UMBENENNEN"),
                                     textFont, SkinButton.Kind.Wood, () => OpenNameDialog(slot, false));
        v.delete = SkinButton.Create(v.root, BtnX + BtnW - half, SecondY, half, SecondH,
                                     Loc.Get("ui.profile.delete", "LÖSCHEN"), textFont, SkinButton.Kind.Wood,
                                     () => OpenDeleteDialog(slot));
        foreach (SkinButton b in new[] { v.primary, v.rename, v.delete })
            b.OnHover = () => { if (dialog == null) SetFocus(slot, false); };
    }

    private void BuildEmptyBody(CardView v)
    {
        float px = (CardW - PortraitSize) / 2;
        OptionsKit.Img("Socket", v.root, px, PortraitY, PortraitSize, PortraitSize, GameHudSkin.Well, true);
        Sprite plus = GameHudSkin.Plus;
        float pw = plus.rect.width * 2, ph = plus.rect.height * 2;
        OptionsKit.Img("Plus", v.root, px + Mathf.Floor((PortraitSize - pw) / 2), PortraitY + Mathf.Floor((PortraitSize - ph) / 2),
                       pw, ph, plus, OptionsKit.WithAlpha(GameHudSkin.StoneLight, 1f));

        OptionsKit.Label("Empty", v.root, 4, NameY, CardW - 8, 14, Loc.Get("ui.profile.empty", "Leerer Platz"),
                         textFont, OptionsKit.SizeText, GameHudSkin.StoneLight, TextAlignmentOptions.Center);
        OptionsKit.Img("Divider", v.root, 12, NameY + 16, CardW - 24, 1, GameHudSkin.White, (Color)GameHudSkin.StoneDark);

        TextMeshProUGUI hint = OptionsKit.Label("Hint", v.root, 12, StatY0 + 4, CardW - 24, 44,
                                                Loc.Get("ui.profile.empty.hint", "Ein neues Abenteuer beginnt hier."),
                                                textFont, OptionsKit.SizeText, GameHudSkin.Stone, TextAlignmentOptions.Top);
        hint.textWrappingMode = TextWrappingModes.Normal;

        int slot = v.slot;
        v.primary = SkinButton.Create(v.root, BtnX, PrimaryY, BtnW, PrimaryH, Loc.Get("ui.profile.new", "NEUES PROFIL"),
                                      textFont, SkinButton.Kind.Primary, () => Primary(slot));
        v.primary.OnHover = () => { if (dialog == null) SetFocus(slot, false); };
    }

    private void StatRow(RectTransform card, int row, Sprite icon, string label, string value)
    {
        int y = StatY0 + row * StatStep;
        int textX = 12;
        if (icon != null)
        {
            float iw = icon.rect.width, ih = icon.rect.height;
            OptionsKit.Img("Icon", card, 12 + Mathf.Floor((9 - iw) / 2), y + Mathf.Floor((StatStep - ih) / 2), iw, ih, icon);
            textX = 24;
        }
        OptionsKit.Label("Stat", card, textX, y - 1, 90, 12, label, textFont, OptionsKit.SizeText,
                         GameHudSkin.ParchDark, TextAlignmentOptions.Left);
        OptionsKit.Label("Value", card, CardW - 12 - 70, y - 1, 70, 12, value, textFont, OptionsKit.SizeText,
                         GameHudSkin.Cream, TextAlignmentOptions.Right);
    }

    private static int CardX(int slot) => CardX0 + slot * (CardW + CardGap);

    // ==================================================================
    //  Aktionen
    // ==================================================================

    private void SetFocus(int slot, bool sound = true)
    {
        if (slot < 0 || slot >= cards.Length || focus == slot) return;
        focus = slot;
        if (sound) OptionsKit.PlayClick();
    }

    private void Primary(int slot)
    {
        if (!SaveProfiles.IsUsed(slot))
        {
            OpenNameDialog(slot, true);
            return;
        }

        if (SaveProfiles.Active == slot)
        {
            // SPIELEN: Fenster zu, dann wie SPIELEN im Hauptmenue.
            System.Action action = play;
            Close();
            action?.Invoke();
            return;
        }

        if (!SaveProfiles.Activate(slot)) return;
        Refresh(slot, string.Format(Loc.Get("ui.profile.toast.loaded", "{0} geladen"), SaveProfiles.NameOf(slot)));
    }

    /// <summary>Karten neu, die betroffene blitzt kurz auf, unten ein Hinweis.</summary>
    private void Refresh(int highlight, string message)
    {
        BuildCards();
        focus = Mathf.Clamp(highlight, 0, cards.Length - 1);
        if (highlight >= 0 && highlight < cards.Length) cards[highlight].flashT = 1f;
        ShowToast(message);
        Animate(0f);
    }

    private void ShowToast(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        toast.text = message;
        Transform shadow = toast.transform.parent.Find(toast.name + "Shadow");
        if (shadow != null) shadow.GetComponent<TextMeshProUGUI>().text = message;
        toastT = 2.2f;
    }

    private void SetShadowAlpha(float a)
    {
        Transform shadow = toast.transform.parent.Find(toast.name + "Shadow");
        if (shadow != null) shadow.GetComponent<TextMeshProUGUI>().alpha = a;
    }

    // ==================================================================
    //  Dialog: Name
    // ==================================================================

    private const int NameDlgW = 232, NameDlgH = 92;

    private void OpenNameDialog(int slot, bool create)
    {
        if (dialog != null) return;
        if (!create && !SaveProfiles.IsUsed(slot)) return;

        RectTransform card = OpenDialogBase(DialogKind.Name, slot, NameDlgW, NameDlgH,
            create ? Loc.Get("ui.profile.create.title", "NEUES PROFIL") : Loc.Get("ui.profile.rename.title", "UMBENENNEN"));
        dialogCreate = create;

        float x = card.anchoredPosition.x, y = -card.anchoredPosition.y;

        nameField = OptionsKit.Input(dialogPage, x + 16, y + 20, NameDlgW - 32, 18, textFont,
                                     Loc.Get("ui.profile.name.hint", "Name eingeben"), false, SaveProfiles.MaxNameLength);
        nameField.onValidateInput = (text, index, c) => SaveProfiles.IsNameChar(c) ? c : '\0';
        nameField.onFocusSelectAll = true;
        nameField.text = create ? SaveProfiles.DefaultName(slot) : SaveProfiles.NameOf(slot);
        nameField.onValueChanged.AddListener(_ => UpdateCounter());

        nameCounter = OptionsKit.Label("Counter", dialogPage, x + 16, y + 40, NameDlgW - 32, 12, "", textFont,
                                       OptionsKit.SizeText, GameHudSkin.StoneLight, TextAlignmentOptions.Right);
        UpdateCounter();

        int bw = (NameDlgW - 32 - 6) / 2;
        SkinButton.Create(dialogPage, x + 16, y + NameDlgH - 28, bw, 18, Loc.Get("ui.profile.cancel", "ABBRECHEN"),
                          textFont, SkinButton.Kind.Wood, CloseDialog);
        SkinButton.Create(dialogPage, x + NameDlgW - 16 - bw, y + NameDlgH - 28, bw, 18,
                          create ? Loc.Get("ui.profile.create", "ERSTELLEN") : Loc.Get("ui.profile.save", "SPEICHERN"),
                          textFont, SkinButton.Kind.Primary, ConfirmName);

        nameField.Select();
        nameField.ActivateInputField();
    }

    private void UpdateCounter()
    {
        if (nameField == null || nameCounter == null) return;
        nameCounter.text = nameField.text.Length + "/" + SaveProfiles.MaxNameLength;
    }

    private void ConfirmName()
    {
        if (dialogKind != DialogKind.Name || nameField == null) return;

        int slot = dialogSlot;
        bool create = dialogCreate;
        string name = nameField.text;
        CloseDialog();

        if (create)
        {
            if (!SaveProfiles.Create(slot, name)) return;
            Refresh(slot, string.Format(Loc.Get("ui.profile.toast.created", "{0} erstellt"), SaveProfiles.NameOf(slot)));
        }
        else
        {
            SaveProfiles.Rename(slot, name);
            Refresh(slot, string.Format(Loc.Get("ui.profile.toast.renamed", "Umbenannt in {0}"), SaveProfiles.NameOf(slot)));
        }
    }

    // ==================================================================
    //  Dialog: Loeschen (gedrueckt halten)
    // ==================================================================

    private const int DelDlgW = 248, DelDlgH = 100;

    private void OpenDeleteDialog(int slot)
    {
        if (dialog != null || !SaveProfiles.IsUsed(slot)) return;

        RectTransform card = OpenDialogBase(DialogKind.Delete, slot, DelDlgW, DelDlgH,
                                            Loc.Get("ui.profile.delete.title", "LÖSCHEN"));
        float x = card.anchoredPosition.x, y = -card.anchoredPosition.y;

        OptionsKit.Label("Question", dialogPage, x + 12, y + 16, DelDlgW - 24, 12,
                         string.Format(Loc.Get("ui.profile.delete.question", "\"{0}\" wirklich löschen?"), SaveProfiles.NameOf(slot)),
                         textFont, OptionsKit.SizeText, GameHudSkin.Cream, TextAlignmentOptions.Center);
        TextMeshProUGUI warn = OptionsKit.Label("Warning", dialogPage, x + 14, y + 30, DelDlgW - 28, 34,
                                                Loc.Get("ui.profile.delete.warning",
                                                        "Münzen, Upgrades, Skills, Erfolge und Story dieses Profils sind danach für immer weg."),
                                                textFont, OptionsKit.SizeText, GameHudSkin.ParchDark, TextAlignmentOptions.Top);
        warn.textWrappingMode = TextWrappingModes.Normal;

        int cancelW = 88, holdW = DelDlgW - 32 - 6 - cancelW;
        SkinButton.Create(dialogPage, x + 16, y + DelDlgH - 28, cancelW, 18, Loc.Get("ui.profile.cancel", "ABBRECHEN"),
                          textFont, SkinButton.Kind.Wood, CloseDialog);

        holdButton = SkinButton.Create(dialogPage, x + DelDlgW - 16 - holdW, y + DelDlgH - 28, holdW, 18,
                                       Loc.Get("ui.profile.delete.hold", "HALTEN ZUM LÖSCHEN"), textFont,
                                       SkinButton.Kind.Danger, null);
        holdFill = OptionsKit.Img("Fill", holdButton.transform, 2, 2, 0, 13, GameHudSkin.White,
                                  OptionsKit.WithAlpha(GameHudSkin.JamDeep, 0.75f));
        holdFill.transform.SetSiblingIndex(0);

        PointerRelay relay = holdButton.gameObject.AddComponent<PointerRelay>();
        relay.Down = () => { holdPointerDown = true; holdPointerInside = true; };
        relay.Enter = () => holdPointerInside = true;
        relay.Exit = () => holdPointerInside = false;

        holdProgress = 0f;
        holdPointerDown = holdPointerInside = false;
        holdKeyArmed = false;
    }

    private bool HoldKey()
    {
        bool key = Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter) || Input.GetKey(KeyCode.Delete);
        var pad = UnityEngine.InputSystem.Gamepad.current;
        if (pad != null) key |= pad.buttonSouth.isPressed || pad.buttonNorth.isPressed;
        return key;
    }

    private void UpdateHold(float dt)
    {
        if (holdButton == null) return;

        bool key = HoldKey();
        // Die Taste, die den Dialog geoeffnet hat, zaehlt nicht - erst loslassen.
        if (!key) holdKeyArmed = true;
        if (!Input.GetMouseButton(0)) holdPointerDown = false;

        bool holding = (holdPointerDown && holdPointerInside) || (key && holdKeyArmed);
        holdProgress = holding ? holdProgress + dt / HoldTime : Mathf.Max(0f, holdProgress - dt * 3f);

        float w = Mathf.Round((holdButton.GetComponent<RectTransform>().sizeDelta.x - 4) * Mathf.Clamp01(holdProgress));
        holdFill.rectTransform.sizeDelta = new Vector2(w, 13);

        if (holdProgress < 1f) return;

        int slot = dialogSlot;
        string name = SaveProfiles.NameOf(slot);
        CloseDialog();
        if (!SaveProfiles.Delete(slot)) return;

        // Das aktive Profil kann gewechselt haben - der Hinweis sagt, welches jetzt gilt.
        Refresh(slot, string.Format(Loc.Get("ui.profile.toast.deleted", "{0} gelöscht"), name));
        focus = slot;
    }

    // ==================================================================
    //  Dialog-Rahmen
    // ==================================================================

    private RectTransform OpenDialogBase(DialogKind kind, int slot, int w, int h, string title)
    {
        dialogKind = kind;
        dialogSlot = slot;
        dialogOpenTime = Time.unscaledTime;

        Image shade = OptionsKit.Stretch("Dialog", transform, GameHudSkin.White, new Color(0.06f, 0.04f, 0.05f, 0.72f));
        shade.raycastTarget = true;
        dialog = shade.rectTransform;

        dialogPage = OptionsKit.Rect("Page", dialog, 0, 0, OptionsKit.RefW, OptionsKit.RefH);
        dialogPage.anchoredPosition = page.anchoredPosition;

        int x = (OptionsKit.RefW - w) / 2, y = (OptionsKit.RefH - h) / 2 + 6;
        Image card = OptionsKit.Img("Card", dialogPage, x, y, w, h, GameHudSkin.Card, true);
        OptionsKit.Ribbon(dialogPage, OptionsKit.RefW / 2f, y - 14, title, pixelFont, textFont);
        return card.rectTransform;
    }

    private void CloseDialog()
    {
        if (dialog == null) return;
        dialog.SetParent(null, false);
        Destroy(dialog.gameObject);
        dialog = null;
        dialogPage = null;
        dialogKind = DialogKind.None;
        nameField = null;
        nameCounter = null;
        holdButton = null;
        holdFill = null;
    }

    // ==================================================================
    //  Eingabe + Animation
    // ==================================================================

    private void Update()
    {
        var size = new Vector2Int(Screen.width, Screen.height);
        if (size != lastScreen)
        {
            lastScreen = size;
            OptionsKit.Layout(page, scaler, Content);
            if (dialogPage != null) dialogPage.anchoredPosition = page.anchoredPosition;
        }

        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
        if (Time.frameCount != openedFrame) HandleInput(dt);
        if (instance != this) return;   // per Taste geschlossen
        Animate(dt);
    }

    private void HandleInput(float dt)
    {
        var pad = UnityEngine.InputSystem.Gamepad.current;
        bool esc = Input.GetKeyDown(KeyCode.Escape) || (pad != null && pad.buttonEast.wasPressedThisFrame);

        if (dialog != null)
        {
            if (esc)
            {
                OptionsKit.PlayClick();
                CloseDialog();
                return;
            }

            if (dialogKind == DialogKind.Name)
            {
                OptionsKit.UpdateFocus(nameField);
                bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
                // Erst nach dem oeffnenden Tastendruck - der kam vom Enter auf der Karte.
                if (enter && Time.unscaledTime - dialogOpenTime > 0.1f)
                {
                    OptionsKit.PlayClick();
                    ConfirmName();
                }
            }
            else if (dialogKind == DialogKind.Delete)
            {
                UpdateHold(dt);
            }
            return;
        }

        if (esc)
        {
            OptionsKit.PlayClick();
            Close();
            return;
        }

        bool left = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
        bool right = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
        bool confirm = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space);
        bool rename = Input.GetKeyDown(KeyCode.F2);
        bool delete = Input.GetKeyDown(KeyCode.Delete);
        if (pad != null)
        {
            left |= pad.dpad.left.wasPressedThisFrame || pad.leftStick.left.wasPressedThisFrame;
            right |= pad.dpad.right.wasPressedThisFrame || pad.leftStick.right.wasPressedThisFrame;
            confirm |= pad.buttonSouth.wasPressedThisFrame;
            rename |= pad.buttonWest.wasPressedThisFrame;
            delete |= pad.buttonNorth.wasPressedThisFrame;
        }

        if (left) SetFocus(focus - 1);
        else if (right) SetFocus(focus + 1);
        else if (confirm)
        {
            OptionsKit.PlayClick();
            Primary(focus);
        }
        else if (rename && SaveProfiles.IsUsed(focus))
        {
            OptionsKit.PlayClick();
            OpenNameDialog(focus, false);
        }
        else if (delete && SaveProfiles.IsUsed(focus))
        {
            OptionsKit.PlayClick();
            OpenDeleteDialog(focus);
        }
    }

    private void Animate(float dt)
    {
        float t = Time.unscaledTime - openTime;
        float time = Time.unscaledTime;

        // Auftritt: Abdunklung, Band, dann die Karten nacheinander von unten.
        float fade = intro ? Mathf.Clamp01(t / 0.2f) : 1f;
        dim.color = new Color(0.06f, 0.04f, 0.05f, 0.82f * fade);
        headGroup.alpha = fade;
        head.anchoredPosition = new Vector2(0f, Mathf.Round(Mathf.Lerp(12f, 0f, EaseOut(fade))));

        for (int i = 0; i < cards.Length; i++)
        {
            CardView v = cards[i];
            if (v == null || v.root == null) continue;

            float s = intro ? Mathf.Clamp01((t - 0.08f - i * 0.07f) / 0.38f) : 1f;
            v.group.alpha = Mathf.Clamp01(s * 2f);
            float slide = Mathf.LerpUnclamped(28f, 0f, BackOut(s));

            bool focused = i == focus && dialog == null;
            v.lift = Damp(v.lift, focused ? 3f : 0f, 14f, dt);
            Color ring = GameHudSkin.Gold;
            ring.a = Damp(v.ring.color.a, focused ? 0.75f + 0.25f * Mathf.Sin(time * 4f) : 0f, 12f, dt);
            v.ring.color = ring;

            v.root.anchoredPosition = new Vector2(CardX(i), -Mathf.Round(CardY + slide - v.lift));

            // Unter dem Cursor laeuft das Portraet, sonst steht Bild 0.
            if (v.portrait != null && v.frames != null && v.frames.Length > 0)
            {
                int frame = focused ? (int)(time * 7f) % v.frames.Length : 0;
                if (v.portrait.sprite != v.frames[frame]) v.portrait.sprite = v.frames[frame];
            }

            v.flashT = Mathf.Max(0f, v.flashT - dt * 2.5f);
            v.flash.color = new Color(1f, 0.95f, 0.8f, 0.45f * v.flashT * v.flashT);
        }

        if (toastT > 0f)
        {
            toastT -= dt;
            float a = Mathf.Clamp01(toastT / 0.4f) * Mathf.Clamp01((2.2f - toastT) / 0.15f);
            toast.alpha = a;
            SetShadowAlpha(a);
        }
    }

    // ==================================================================
    //  Formatierung
    // ==================================================================

    private static bool English => Loc.Language == "en";

    public static string FormatNumber(int n)
    {
        string digits = Mathf.Abs(n).ToString();
        char sep = English ? ',' : '.';
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < digits.Length; i++)
        {
            if (i > 0 && (digits.Length - i) % 3 == 0) sb.Append(sep);
            sb.Append(digits[i]);
        }
        return (n < 0 ? "-" : "") + sb;
    }

    public static string FormatPlayTime(double seconds)
    {
        int minutes = (int)(seconds / 60d);
        if (minutes < 1) return Loc.Get("ui.profile.time.short", "< 1 Min");
        if (minutes < 60) return string.Format(Loc.Get("ui.profile.time.minutes", "{0} Min"), minutes);
        return string.Format(Loc.Get("ui.profile.time.hours", "{0} Std {1} Min"), minutes / 60, minutes % 60);
    }

    private static readonly string[] MonthsEn =
        { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

    public static string FormatDate(System.DateTime? when)
    {
        if (when == null) return Loc.Get("ui.profile.never", "nie");
        System.DateTime d = when.Value.Date, today = System.DateTime.Now.Date;
        if (d == today) return Loc.Get("ui.profile.today", "Heute");
        if (d == today.AddDays(-1)) return Loc.Get("ui.profile.yesterday", "Gestern");
        return English ? $"{d.Day} {MonthsEn[d.Month - 1]} {d.Year}" : $"{d.Day:00}.{d.Month:00}.{d.Year}";
    }

    // ==================================================================

    private static float Damp(float a, float b, float speed, float dt) => Mathf.Lerp(a, b, 1f - Mathf.Exp(-speed * dt));

    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

    private static float BackOut(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }
}
