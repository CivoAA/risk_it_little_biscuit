using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Die Kapsel im Hub: Erfolge, Unlocks und - sobald ein Charakter den Knoten
/// im Skilltree hat - das Bestiarium. Im Stil von "UI 2.0" wie
/// <see cref="OptionsPanel"/>, <see cref="AchievementPanel"/> und der Level-Up:
/// dunkle Karte, Titelband, Holzknoepfe.
///
///   AchievementsBookPanel.Toggle();
///   AchievementsBookPanel.Open(AchievementsBookPanel.TabUnlocks);
///
/// Aufbau (Seitenpixel, 480x270):
///   Titelband   Name des Reiters
///   Reiter      ERFOLGE | UNLOCKS | BESTIARIUM          12 / 43 [=====   ]
///   links       Erfolge: Liste nach Kategorie, mit Fortschrittsstrich
///               Unlocks/Bestiarium: Kachelgitter
///   rechts      das Gewaehlte: Bild im Fenster, Name, Text, Balken, Belohnung
///   Fussleiste  ZURUECK                                 Tastenhinweis
///
/// Hinter der Karte steigen gelbe Blasen auf - die Fluessigkeit der Kapsel.
///
/// Bedienung: Maus, A/D bzw. Links/Rechts = Reiter, W/S bzw. Hoch/Runter =
/// Eintrag, Mausrad, E oder ESC schliesst.
///
/// Baut sich komplett per Code auf und laesst sich aus jeder Szene oeffnen.
/// Inhalt kommt aus <see cref="Ach"/>, <see cref="Unlocks"/> und
/// <see cref="Bestiary"/> - dieses Skript kennt keinen Eintrag beim Namen.
/// </summary>
public class AchievementsBookPanel : MonoBehaviour
{
    public const int TabAchievements = 0, TabUnlocks = 1, TabBestiary = 2;

    // ==================================================================
    //  Masse (Seitenpixel, Ursprung oben links)
    // ==================================================================

    private const int CardX = 24, CardY = 38, CardW = 432, CardH = 212;
    private const int RibbonY = 26;
    private static readonly RectInt Content = new RectInt(CardX - 12, RibbonY, CardW + 24, CardY + CardH - RibbonY);

    private const int InX = CardX + 14, InW = CardW - 28;

    private const int TabY = 52, TabH = 18, TabW = 86, TabGap = 4;
    private const int TotalBarW = 90;

    private const int BodyY = 76, BodyH = 140;
    private const int ListX = InX, ListW = 236;
    private const int Pad = 4;
    private const int ViewX = ListX + Pad, ViewY = BodyY + Pad, ViewW = ListW - 2 * Pad, ViewH = BodyH - 2 * Pad;

    private const int DetX = ListX + ListW + 6, DetY = BodyY, DetW = InX + InW - DetX, DetH = BodyH;
    private const int WinS = 56;                 // Fenster fuer das grosse Bild (SlotFrame, innen 50)

    private const int FootY = 224, FootH = 18, BackW = 76;

    // Liste (Erfolge)
    private const int HeadH = 15, RowH = 24, RowGap = 1, RowIcon = 21;

    // Gitter: Unlocks 36er Kacheln (32er Symbol), Bestiarium 52er (bis 48er Bild)
    private const int TileGap = 2;
    private static int TileSize(int tab) => tab == TabBestiary ? 52 : 36;

    private static readonly Color LockedTint = new Color(0.45f, 0.40f, 0.44f, 1f);
    private static readonly Color Silhouette = new Color(0.10f, 0.07f, 0.09f, 0.85f);

    // ==================================================================
    //  Zustand
    // ==================================================================

    private static AchievementsBookPanel instance;
    public static bool IsOpen => instance != null;

    /// <summary>Reiter, der beim naechsten Oeffnen vorne liegt.</summary>
    private static int startTab;

    private TMP_FontAsset textFont, pixelFont;
    private RectTransform page, ribbonHolder, bubbleHolder;
    private CanvasScaler scaler;
    private Vector2Int lastScreen;
    private int openedFrame;

    private int tab, tabCount = 2;
    private SkinButton[] tabs;
    private TextMeshProUGUI counter;
    private Image totalFill;

    private RectTransform viewport, content;
    private Image scrollHandle;
    private float scrollTop, contentHeight;

    private readonly List<Entry> entries = new List<Entry>();
    private readonly List<View> views = new List<View>();
    private int selected, hovered = -1;

    private Image winFrame, winBack, detailIcon, detailLock, detailCheck;
    private TextMeshProUGUI detailName, detailSub, detailDesc, detailProgress, detailReward;
    private Image barFrame, barFill, rewardBox, rewardIcon;

    private readonly List<Bubble> bubbles = new List<Bubble>();
    private HubUI hub;

    /// <summary>Ein Eintrag, egal ob Erfolg, Unlock oder Gegner.</summary>
    private class Entry
    {
        public string Title, Sub, Desc;
        public Color SubColor;
        public Sprite RowIcon, Icon;
        public bool Done;
        public bool Shadow;            // noch unbekannt: nur der Umriss
        public float Progress = -1f;   // -1 = kein Balken
        public string ProgressText;
        public string Reward;
        public Sprite RewardIcon;
        public Color RewardColor;
        public string Badge;           // Ecke der Kachel (Bestiarium: "+2%")
        public string Group;           // Erfolge: Kategorie, fuer die Zwischenueberschrift
    }

    /// <summary>Zeile oder Kachel auf dem Bildschirm.</summary>
    private class View
    {
        public int Index;
        public float Top, Height;
        public Image Back, Tile, Hover, Select, Icon, Lock, Check, BarBack, BarFill;
        public TextMeshProUGUI Label, Right, Badge;
    }

    private class Bubble
    {
        public RectTransform Rt;
        public Image Img;
        public float X, Y, Speed, Phase, Sway;
    }

    // ==================================================================
    //  Oeffnen / Schliessen
    // ==================================================================

    public static void Open()
    {
        if (instance != null) return;
        GameObject go = new GameObject("AchievementsBookPanel");
        instance = go.AddComponent<AchievementsBookPanel>();
    }

    /// <summary>Oeffnet direkt auf einem Reiter: 0 = Erfolge, 1 = Unlocks, 2 = Bestiarium.</summary>
    public static void Open(int tabIndex)
    {
        startTab = Mathf.Clamp(tabIndex, 0, TabBestiary);
        Open();
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
    /// Liest die Kataloge neu, falls das Fenster gerade offen ist. Fuer Aenderungen,
    /// die kein Ereignis feuern - etwa <see cref="Achievements.ResetAll"/> aus der
    /// Cheat-Konsole.
    /// </summary>
    public static void RefreshIfOpen()
    {
        if (instance != null) instance.Rebuild();
    }

    private void Awake()
    {
        instance = this;
        openedFrame = Time.frameCount;
        textFont = PixelUI.FindTextFont();
        pixelFont = PixelUI.FindPixelFont();
        Build();
        BlockHub(true);
    }

    private void OnEnable()
    {
        Loc.LanguageChanged += Rebuild;
        Achievements.Unlocked += OnAchievementUnlocked;
        Unlocks.Granted += OnUnlockGranted;
        Bestiary.Changed += Refresh;
    }

    private void OnDisable()
    {
        Loc.LanguageChanged -= Rebuild;
        Achievements.Unlocked -= OnAchievementUnlocked;
        Unlocks.Granted -= OnUnlockGranted;
        Bestiary.Changed -= Refresh;
    }

    private void OnDestroy()
    {
        startTab = tab;
        BlockHub(false);
        if (instance == this) instance = null;
    }

    private void OnAchievementUnlocked(AchievementDef def) => Refresh();
    private void OnUnlockGranted(UnlockDef def) => Refresh();

    /// <summary>
    /// Sperrt den Hub, solange das Fenster offen ist - wie Shop, Skilltree und
    /// Charakterauswahl. Ausserhalb des Hubs passiert nichts, und es wird auch
    /// kein HubUI angelegt.
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
        var size = new Vector2Int(Screen.width, Screen.height);
        if (page != null && size != lastScreen)
        {
            lastScreen = size;
            OptionsKit.Layout(page, scaler, Content);
        }

        AnimateBubbles();

        // Das [E], mit dem die Kapsel aufgeht, darf sie nicht gleich wieder schliessen.
        if (Time.frameCount == openedFrame) return;

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))
        {
            OptionsKit.PlayClick();
            Close();
            return;
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) StepTab(-1);
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.Tab))
            StepTab(1);
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) StepEntry(-1);
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) StepEntry(1);

        float wheel = Input.mouseScrollDelta.y;
        if (!Mathf.Approximately(wheel, 0f)) SetScroll(scrollTop - wheel * (GridMode ? TileSize(tab) + TileGap : RowH + RowGap));
    }

    private void StepTab(int delta)
    {
        if (tabCount <= 1) return;
        OptionsKit.PlayClick();
        SetTab((tab + delta + tabCount) % tabCount);
    }

    private void StepEntry(int delta)
    {
        if (entries.Count == 0) return;
        OptionsKit.PlayClick();
        Select(Mathf.Clamp(selected + delta, 0, entries.Count - 1), true);
    }

    // ==================================================================
    //  Aufbau
    // ==================================================================

    private void Rebuild()
    {
        if (page == null) return;
        bubbles.Clear();
        OptionsKit.Clear(transform);
        textFont = PixelUI.FindTextFont();
        Build();
    }

    private void Build()
    {
        // Ueber Shop 130, Levelauswahl 135 und Skilltree 140; unter Pause und Optionen.
        page = OptionsKit.CreatePage(gameObject, 150, 0.84f, out scaler);

        bubbleHolder = OptionsKit.Rect("Bubbles", page, 0, 0, OptionsKit.RefW, OptionsKit.RefH);
        BuildBubbles();

        OptionsKit.Img("Card", page, CardX, CardY, CardW, CardH, GameHudSkin.Card, true);
        ribbonHolder = OptionsKit.Rect("RibbonHolder", page, 0, 0, OptionsKit.RefW, OptionsKit.RefH);

        tabCount = Bestiary.IsVisible ? 3 : 2;
        BuildTabs();
        BuildList();
        BuildDetail();
        BuildFooter();

        OptionsKit.Layout(page, scaler, Content);
        lastScreen = new Vector2Int(Screen.width, Screen.height);

        tab = -1;
        SetTab(Mathf.Clamp(startTab, 0, tabCount - 1));
    }

    private static string TabName(int i)
    {
        switch (i)
        {
            case TabAchievements: return Loc.Get("ui.book.tab.achievements", "ERFOLGE");
            case TabUnlocks:      return Loc.Get("ui.book.tab.unlocks", "UNLOCKS");
            default:              return Loc.Get("ui.book.tab.bestiary", "BESTIARIUM");
        }
    }

    private static Sprite TabIcon(int i)
    {
        switch (i)
        {
            case TabAchievements: return GameHudSkin.Star;
            case TabUnlocks:      return GameHudSkin.Key;
            default:              return GameHudSkin.Skull;
        }
    }

    private void BuildTabs()
    {
        tabs = new SkinButton[tabCount];
        for (int i = 0; i < tabCount; i++)
        {
            int index = i;
            tabs[i] = SkinButton.Create(page, InX + i * (TabW + TabGap), TabY, TabW, TabH, TabName(i),
                                        textFont, SkinButton.Kind.Wood, () => SetTab(index), TabIcon(i));
        }

        // Stand des Reiters rechts: Zahl und Balken.
        int barX = InX + InW - TotalBarW;
        OptionsKit.Img("TotalFrame", page, barX, TabY + 5, TotalBarW, 8, GameHudSkin.BarFrame, true);
        totalFill = OptionsKit.Img("TotalFill", page, barX + 1, TabY + 6, 0, 6, GameHudSkin.White, GameHudSkin.Gold);
        counter = OptionsKit.Label("Counter", page, barX - 52, TabY + 2, 48, 13, "", textFont,
                                   OptionsKit.SizeText, GameHudSkin.Cream, TextAlignmentOptions.Right);
    }

    private void BuildList()
    {
        OptionsKit.Img("ListWell", page, ListX, BodyY, ListW, BodyH, GameHudSkin.Well, true);

        viewport = OptionsKit.Rect("Viewport", page, ViewX, ViewY, ViewW, ViewH);
        viewport.gameObject.AddComponent<RectMask2D>();
        content = OptionsKit.Rect("Content", viewport, 0, 0, ViewW, ViewH);

        scrollHandle = OptionsKit.Img("ScrollHandle", page, ListX + ListW - 3, ViewY, 2, 10,
                                      GameHudSkin.White, GameHudSkin.Stone);
    }

    private void BuildDetail()
    {
        OptionsKit.Img("DetailWell", page, DetX, DetY, DetW, DetH, GameHudSkin.Well, true);

        int wx = DetX + 6, wy = DetY + 6;
        winBack = OptionsKit.Img("WinBack", page, wx + 3, wy + 3, WinS - 6, WinS - 6,
                                 GameHudSkin.SlotBack(WinS - 6, WinS - 6));
        detailIcon = OptionsKit.Img("Icon", page, wx, wy, 32, 32, null);
        winFrame = OptionsKit.Img("WinFrame", page, wx, wy, WinS, WinS,
                                  GameHudSkin.SlotFrame(GameHudSkin.SlotLook.Wood), true);

        Sprite lockSprite = GameHudSkin.Lock;
        detailLock = OptionsKit.Img("Lock", page, wx + WinS - lockSprite.rect.width - 2,
                                    wy + WinS - lockSprite.rect.height - 2,
                                    lockSprite.rect.width, lockSprite.rect.height, lockSprite);
        Sprite check = GameHudSkin.Check;
        detailCheck = OptionsKit.Img("Check", page, wx + WinS - check.rect.width + 1, wy - 2,
                                     check.rect.width, check.rect.height, check);

        int tx = wx + WinS + 6, tw = DetX + DetW - 6 - tx;
        detailName = OptionsKit.Label("Name", page, tx, wy + 2, tw, 28, "", textFont, OptionsKit.SizeText,
                                      GameHudSkin.Gold, TextAlignmentOptions.TopLeft);
        detailName.textWrappingMode = TextWrappingModes.Normal;
        detailSub = OptionsKit.Label("Sub", page, tx, wy + WinS - 15, tw, 13, "", textFont, OptionsKit.SizeText,
                                     GameHudSkin.Stone, TextAlignmentOptions.BottomLeft);

        OptionsKit.Img("Rule", page, DetX + 6, DetY + WinS + 10, DetW - 12, 2, GameHudSkin.Rule, true);

        detailDesc = OptionsKit.Label("Desc", page, DetX + 7, DetY + WinS + 15, DetW - 14, 40, "", textFont,
                                      OptionsKit.SizeText, GameHudSkin.Parchment, TextAlignmentOptions.TopLeft);
        detailDesc.textWrappingMode = TextWrappingModes.Normal;
        detailDesc.richText = true;

        int barY = DetY + DetH - 30;
        int barW = DetW - 12 - 44;
        barFrame = OptionsKit.Img("BarFrame", page, DetX + 6, barY, barW, 8, GameHudSkin.BarFrame, true);
        barFill = OptionsKit.Img("BarFill", page, DetX + 7, barY + 1, 0, 6, GameHudSkin.White, GameHudSkin.Gold);
        detailProgress = OptionsKit.Label("Progress", page, DetX + 6 + barW, barY - 3, 44, 13, "", textFont,
                                          OptionsKit.SizeText, GameHudSkin.Cream, TextAlignmentOptions.Right);

        rewardBox = OptionsKit.Img("RewardBox", page, DetX + 4, DetY + DetH - 19, DetW - 8, 15,
                                   GameHudSkin.Plate, true);
        rewardIcon = OptionsKit.Img("RewardIcon", page, DetX + 8, DetY + DetH - 17, 9, 9, null);
        detailReward = OptionsKit.Label("Reward", page, DetX + 20, DetY + DetH - 18, DetW - 26, 13, "", textFont,
                                        OptionsKit.SizeText, GameHudSkin.GoldLight, TextAlignmentOptions.Left);
    }

    private void BuildFooter()
    {
        SkinButton.Create(page, InX, FootY, BackW, FootH, Loc.Get("ui.achievements.close", "ZURÜCK"),
                          textFont, SkinButton.Kind.Wood, Close);

        OptionsKit.Label("Hint", page, InX + BackW + 8, FootY + 2, InW - BackW - 8, 13,
                         Loc.Get("ui.book.hint", "A/D REITER   W/S AUSWAHL   E SCHLIESSEN"), textFont,
                         OptionsKit.SizeText, GameHudSkin.Stone, TextAlignmentOptions.Right);
    }

    // ---------- Blasen ----------

    private void BuildBubbles()
    {
        int[] sizes = { 3, 3, 5, 3, 7, 5, 3, 5, 3, 7, 3, 5 };
        for (int i = 0; i < sizes.Length; i++)
        {
            int s = sizes[i];
            Image img = OptionsKit.Img("Bubble", bubbleHolder, 0, 0, s, s, GameHudSkin.Bubble(s),
                                       OptionsKit.WithAlpha(GameHudSkin.Capsule, 0.45f));
            bubbles.Add(new Bubble
            {
                Rt = img.rectTransform,
                Img = img,
                X = Random.Range(0f, OptionsKit.RefW),
                Y = Random.Range(0f, OptionsKit.RefH),
                Speed = Mathf.Lerp(16f, 7f, (s - 3) / 4f),
                Phase = Random.Range(0f, Mathf.PI * 2f),
                Sway = Random.Range(1f, 3f),
            });
        }
    }

    /// <summary>Blasen steigen auf und pendeln leicht - immer auf ganzen Pixeln.</summary>
    private void AnimateBubbles()
    {
        float dt = Time.unscaledDeltaTime, t = Time.unscaledTime;
        foreach (Bubble b in bubbles)
        {
            if (b.Rt == null) continue;
            b.Y -= b.Speed * dt;
            if (b.Y < -10f)
            {
                b.Y = OptionsKit.RefH + Random.Range(0f, 30f);
                b.X = Random.Range(0f, OptionsKit.RefW);
            }
            float x = b.X + Mathf.Sin(t * 0.8f + b.Phase) * b.Sway;
            b.Rt.anchoredPosition = new Vector2(Mathf.Round(x), -Mathf.Round(b.Y));
        }
    }

    // ==================================================================
    //  Reiter
    // ==================================================================

    private bool GridMode => tab != TabAchievements;

    private void SetTab(int index)
    {
        index = Mathf.Clamp(index, 0, tabCount - 1);
        bool changed = index != tab;
        tab = index;

        for (int i = 0; i < tabs.Length; i++) tabs[i].Active = i == tab;

        OptionsKit.Clear(ribbonHolder);
        OptionsKit.Ribbon(ribbonHolder, CardX + CardW / 2f, RibbonY, TabName(tab), pixelFont, textFont);

        if (changed) selected = 0;
        BuildEntries();
        Select(selected, true);
    }

    /// <summary>Liest den Reiter neu ein und baut Liste bzw. Gitter.</summary>
    private void BuildEntries()
    {
        CollectEntries();

        OptionsKit.Clear(content);
        views.Clear();
        hovered = -1;

        if (GridMode) LayoutGrid();
        else LayoutList();

        content.sizeDelta = new Vector2(ViewW, Mathf.Max(ViewH, contentHeight));
        selected = entries.Count == 0 ? 0 : Mathf.Clamp(selected, 0, entries.Count - 1);
        SetScroll(scrollTop);

        RefreshCounter();
        PaintViews();
    }

    /// <summary>Wie <see cref="BuildEntries"/>, behaelt aber Auswahl und Scrollstand.</summary>
    private void Refresh()
    {
        if (page == null) return;
        int keepSel = selected;
        float keepScroll = scrollTop;
        BuildEntries();
        selected = Mathf.Clamp(keepSel, 0, Mathf.Max(0, entries.Count - 1));
        SetScroll(keepScroll);
        PaintViews();
        ShowDetail();
    }

    private void RefreshCounter()
    {
        int done = 0, total = entries.Count;
        foreach (Entry e in entries) if (e.Done) done++;

        counter.text = string.Format(Loc.Get("ui.achievements.counter", "{0} / {1}"), done, total);
        SetFill(totalFill, TotalBarW - 2, total > 0 ? done / (float)total : 0f);
    }

    // ==================================================================
    //  Inhalt
    // ==================================================================

    private void CollectEntries()
    {
        entries.Clear();
        switch (tab)
        {
            case TabAchievements: CollectAchievements(); break;
            case TabUnlocks:      CollectUnlocks(); break;
            default:              CollectBestiary(); break;
        }
    }

    private void CollectAchievements()
    {
        foreach (AchievementCategory category in System.Enum.GetValues(typeof(AchievementCategory)))
        {
            string group = Loc.Get($"ach.cat.{category}", category.ToString().ToUpperInvariant());
            foreach (AchievementDef def in Ach.All)
            {
                if (def.Category != category) continue;

                bool done = def.IsUnlocked;
                string key = def.Hidden && !done ? "_hidden" : def.IconKey;

                var e = new Entry
                {
                    Title = def.Name,
                    Sub = group,
                    SubColor = GameHudSkin.Stone,
                    Desc = def.Description,
                    RowIcon = BookIcon(key, 21) ?? def.Icon,
                    Icon = BookIcon(key, 32) ?? def.Icon,
                    Done = done,
                    Group = group,
                };

                if (!done && def.HasProgress)
                {
                    e.Progress = Mathf.Clamp01(def.Value / def.Goal);
                    e.ProgressText = $"{Mathf.FloorToInt(Mathf.Min(def.Value, def.Goal))}/{Mathf.FloorToInt(def.Goal)}";
                }

                if (done)
                {
                    e.Reward = Loc.Get("ui.achievements.unlocked", "Freigeschaltet");
                    e.RewardIcon = GameHudSkin.Check;
                    e.RewardColor = GameHudSkin.Mint;
                }
                else if (def.Souls > 0)
                {
                    e.Reward = $"+{def.Souls} {Loc.Get("ui.achievements.souls", "Cookie Souls")}";
                    e.RewardIcon = GameHudSkin.Coin;
                    e.RewardColor = GameHudSkin.GoldLight;
                }

                entries.Add(e);
            }
        }
    }

    private void CollectUnlocks()
    {
        foreach (UnlockDef def in Unlocks.All)
        {
            bool done = def.IsUnlocked;
            AchievementDef source = Ach.FindByUnlock(def.Id);

            var e = new Entry
            {
                Title = done ? def.Name : "???",
                Sub = done ? Loc.Get("ui.unlocks.owned", "Freigeschaltet").ToUpperInvariant()
                           : Loc.Get("ui.book.locked", "GESPERRT"),
                SubColor = done ? GameHudSkin.Mint : GameHudSkin.Stone,
                Desc = done ? def.Description : Loc.Get("ui.unlocks.hint", "Spiel weiter, dann taucht das hier auf."),
                Icon = BookIcon(def.IconKey, 32) ?? def.Icon,
                Done = done,
                Shadow = !done,
            };

            // Woher der Unlock kommt, steht unten in der Belohnungszeile.
            if (source != null)
            {
                e.Reward = string.Format(Loc.Get("ui.unlocks.from", "Aus: {0}"), source.Name);
                e.RewardIcon = GameHudSkin.Star;
                e.RewardColor = done ? GameHudSkin.StoneLight : GameHudSkin.GoldLight;
            }

            entries.Add(e);
        }
    }

    private void CollectBestiary()
    {
        bool counting = Skills.HasGrant(SkillGrants.Bestiarium);

        foreach (EnemyId id in Bestiary.Enemies)
        {
            int kills = Bestiary.Kills(id);
            int bonus = Bestiary.BonusPercent(id);
            int into = kills % Bestiary.KillsPerPercent;
            bool seen = kills > 0;

            string desc = seen
                ? string.Format(Loc.Get("ui.bestiary.next", "Nächstes +1% in {0} Kills"),
                                (Bestiary.KillsPerPercent - into).ToString("N0"))
                : Loc.Get("ui.bestiary.unseen", "Noch nie besiegt. Wer ist das wohl?");

            if (!counting)
            {
                string warn = Loc.Get("ui.bestiary.inactive", "Dieser Charakter zählt nicht mit.");
                desc += $"\n<color=#{ColorUtility.ToHtmlStringRGB(GameHudSkin.JamLight)}>{warn}</color>";
            }

            entries.Add(new Entry
            {
                Title = seen ? Bestiary.NameOf(id) : "???",
                Sub = string.Format(Loc.Get("ui.bestiary.kills", "Kills: {0}"), kills.ToString("N0")).ToUpperInvariant(),
                SubColor = seen ? GameHudSkin.Cream : GameHudSkin.Stone,
                Desc = desc,
                Icon = Bestiary.Icon(id),
                Done = seen,
                Shadow = !seen,
                Progress = into / (float)Bestiary.KillsPerPercent,
                ProgressText = $"{into}/{Bestiary.KillsPerPercent}",
                Reward = string.Format(Loc.Get("ui.bestiary.bonus", "+{0}% Schaden"), bonus),
                RewardIcon = GameHudSkin.Crit,
                RewardColor = bonus > 0 ? GameHudSkin.GoldLight : GameHudSkin.Stone,
                Badge = bonus > 0 ? $"+{bonus}%" : "",
            });
        }
    }

    // ---------- Liste ----------

    private void LayoutList()
    {
        float y = 0f;
        string group = null;

        for (int i = 0; i < entries.Count; i++)
        {
            Entry e = entries[i];

            if (e.Group != group)
            {
                group = e.Group;
                if (i > 0) y += 3f;
                AddHeader(group, y);
                y += HeadH;
            }

            views.Add(AddRow(i, y));
            y += RowH + RowGap;
        }

        contentHeight = Mathf.Max(0f, y - RowGap);
    }

    /// <summary>Zwischenueberschrift: Kategorie in Gold, rechts "3/8", darunter eine Linie.</summary>
    private void AddHeader(string group, float y)
    {
        int done = 0, total = 0;
        foreach (Entry e in entries)
        {
            if (e.Group != group) continue;
            total++;
            if (e.Done) done++;
        }

        OptionsKit.ShadowLabel("Head", content, 3, y, ViewW - 6, 13, group, textFont, OptionsKit.SizeText,
                               GameHudSkin.Gold, TextAlignmentOptions.Left);
        OptionsKit.Label("HeadCount", content, 3, y, ViewW - 9, 13, $"{done}/{total}", textFont,
                         OptionsKit.SizeText, done == total ? GameHudSkin.Mint : GameHudSkin.Stone,
                         TextAlignmentOptions.Right);
        OptionsKit.Img("HeadRule", content, 0, y + 12, ViewW - 4, 2, GameHudSkin.Rule, true);
    }

    private View AddRow(int index, float y)
    {
        Entry e = entries[index];
        float w = ViewW - 4;

        RectTransform row = OptionsKit.Rect("Row", content, 0, y, w, RowH);
        var v = new View { Index = index, Top = y, Height = RowH };

        v.Back = row.gameObject.AddComponent<Image>();
        v.Back.sprite = GameHudSkin.White;
        v.Back.raycastTarget = true;

        v.Icon = OptionsKit.Img("Icon", row, 2, Mathf.Floor((RowH - RowIcon) / 2f), RowIcon, RowIcon, e.RowIcon);
        v.Icon.enabled = e.RowIcon != null;

        v.Label = OptionsKit.Label("Label", row, RowIcon + 7, e.Progress >= 0f ? 1 : 5, w - RowIcon - 50, 13,
                                   e.Title, textFont, OptionsKit.SizeText, GameHudSkin.Parchment,
                                   TextAlignmentOptions.Left);
        v.Label.overflowMode = TextOverflowModes.Ellipsis;

        // Laufender Fortschritt: ein zwei Pixel duenner Strich unter dem Namen.
        if (e.Progress >= 0f)
        {
            const int barW = 96;
            v.BarBack = OptionsKit.Img("BarBack", row, RowIcon + 7, 16, barW, 3, GameHudSkin.White,
                                       GameHudSkin.Night);
            v.BarFill = OptionsKit.Img("BarFill", row, RowIcon + 7, 16, 0, 3, GameHudSkin.White,
                                       GameHudSkin.GoldDark);
            SetFill(v.BarFill, barW, e.Progress);
        }

        v.Right = OptionsKit.Label("Right", row, w - 48, 5, 44, 13, e.Done ? "" : e.ProgressText ?? "",
                                   textFont, OptionsKit.SizeText, GameHudSkin.Stone, TextAlignmentOptions.Right);

        Sprite c = GameHudSkin.Check;
        v.Check = OptionsKit.Img("Check", row, w - 5 - c.rect.width, Mathf.Round((RowH - c.rect.height) / 2f),
                                 c.rect.width, c.rect.height, c);

        Hook(row.gameObject, v);
        return v;
    }

    // ---------- Gitter ----------

    private void LayoutGrid()
    {
        int s = TileSize(tab), step = s + TileGap;
        int cols = Mathf.Max(1, (ViewW - 4 + TileGap) / step);
        int x0 = (ViewW - 4 - (cols * s + (cols - 1) * TileGap)) / 2;

        for (int i = 0; i < entries.Count; i++)
        {
            int cx = i % cols, cy = i / cols;
            views.Add(AddTile(i, x0 + cx * step, 1 + cy * step, s));
        }

        int lines = Mathf.CeilToInt(entries.Count / (float)cols);
        contentHeight = lines > 0 ? 1 + lines * step - TileGap + 1 : 0;
    }

    private View AddTile(int index, float x, float y, int s)
    {
        Entry e = entries[index];
        RectTransform root = OptionsKit.Rect("Tile", content, x, y, s, s);
        var v = new View { Index = index, Top = y, Height = s };

        v.Tile = root.gameObject.AddComponent<Image>();
        v.Tile.sprite = GameHudSkin.Tile(s, e.Done ? GameHudSkin.TileKind.Weapon : GameHudSkin.TileKind.Empty);
        v.Tile.raycastTarget = true;

        v.Icon = OptionsKit.Img("Icon", root, 0, 0, 1, 1, e.Icon);
        FitIcon(v.Icon, e.Icon, 0, 0, s, s - 1);

        if (e.Shadow)
        {
            Sprite l = GameHudSkin.Lock;
            v.Lock = OptionsKit.Img("Lock", root, s - l.rect.width - 2, s - l.rect.height - 3,
                                    l.rect.width, l.rect.height, l);
        }

        if (!string.IsNullOrEmpty(e.Badge))
        {
            v.Badge = OptionsKit.ShadowLabel("Badge", root, 2, s - 15, s - 5, 13, e.Badge, textFont,
                                             OptionsKit.SizeText, GameHudSkin.GoldLight, TextAlignmentOptions.Right);
        }

        v.Hover = OptionsKit.Img("Hover", root, -1, -1, s + 2, s + 2,
                                 GameHudSkin.SlotFrame(GameHudSkin.SlotLook.Hover), true);
        v.Select = OptionsKit.Img("Select", root, -1, -1, s + 2, s + 2,
                                  GameHudSkin.SlotFrame(GameHudSkin.SlotLook.Gold), true);

        Hook(root.gameObject, v);
        return v;
    }

    /// <summary>Setzt ein Bild in seiner echten Groesse mittig in einen Kasten - nie gestreckt.</summary>
    private static void FitIcon(Image img, Sprite sprite, float x, float y, float w, float h)
    {
        img.enabled = sprite != null;
        if (sprite == null) return;
        img.sprite = sprite;

        float sw = sprite.rect.width, sh = sprite.rect.height;
        OptionsKit.Move(img.rectTransform, x + Mathf.Floor((w - sw) / 2f), y + Mathf.Floor((h - sh) / 2f), sw, sh);
    }

    private void Hook(GameObject go, View v)
    {
        PointerRelay relay = go.AddComponent<PointerRelay>();
        relay.Enter = () => { hovered = v.Index; PaintViews(); };
        relay.Exit = () => { if (hovered == v.Index) hovered = -1; PaintViews(); };
        relay.Down = () =>
        {
            if (v.Index == selected) return;
            OptionsKit.PlayClick();
            Select(v.Index, false);
        };
    }

    // ---------- Auswahl, Scrollen ----------

    private void Select(int index, bool scrollIntoView)
    {
        selected = entries.Count == 0 ? 0 : Mathf.Clamp(index, 0, entries.Count - 1);
        PaintViews();
        ShowDetail();

        if (!scrollIntoView || views.Count == 0) return;

        View v = views[selected];
        // In der Liste die Zwischenueberschrift mitnehmen, wenn es die erste Zeile der Gruppe ist.
        float top = v.Top - (!GridMode && (selected == 0 || entries[selected - 1].Group != entries[selected].Group)
                                 ? HeadH + 3 : 1);
        float bottom = v.Top + v.Height + 1;
        if (top < scrollTop) SetScroll(top);
        else if (bottom > scrollTop + ViewH) SetScroll(bottom - ViewH);
    }

    private void SetScroll(float value)
    {
        float hidden = Mathf.Max(0f, contentHeight - ViewH);
        scrollTop = Mathf.Clamp(value, 0f, hidden);
        // Ganze Pixel - sonst verschmiert der Inhalt beim Hochskalieren.
        content.anchoredPosition = new Vector2(0f, Mathf.Round(scrollTop));
        UpdateScrollBar();
    }

    private void UpdateScrollBar()
    {
        float hidden = Mathf.Max(0f, contentHeight - ViewH);
        scrollHandle.enabled = hidden > 0f;
        if (hidden <= 0f) return;

        float size = Mathf.Max(8f, Mathf.Round(ViewH * (ViewH / contentHeight)));
        float y = ViewY + Mathf.Round((ViewH - size) * (scrollTop / hidden));
        OptionsKit.Move(scrollHandle.rectTransform, ListX + ListW - 3, y, 2, size);
    }

    /// <summary>Zustand jeder Zeile/Kachel: erledigt, gewaehlt, Maus.</summary>
    private void PaintViews()
    {
        foreach (View v in views)
        {
            Entry e = entries[v.Index];
            bool sel = v.Index == selected, hov = v.Index == hovered;

            if (v.Back != null)
            {
                Color c;
                if (sel) c = GameHudSkin.WoodMid;
                else if (hov) c = GameHudSkin.StoneDark;
                else if (v.Index % 2 == 0) c = GameHudSkin.CardFill;
                else c = Color.clear;
                v.Back.color = c;

                v.Label.color = sel ? GameHudSkin.Cream : e.Done ? GameHudSkin.Parchment : GameHudSkin.StoneLight;
                v.Icon.color = e.Done ? Color.white : LockedTint;
                v.Check.enabled = e.Done;
                if (v.BarFill != null) v.BarFill.color = sel ? GameHudSkin.Gold : GameHudSkin.GoldDark;
            }
            else
            {
                v.Icon.color = e.Shadow ? Silhouette : Color.white;
                v.Hover.enabled = hov && !sel;
                v.Select.enabled = sel;
            }
        }
    }

    // ==================================================================
    //  Detail
    // ==================================================================

    private void ShowDetail()
    {
        Entry e = entries.Count > 0 ? entries[Mathf.Clamp(selected, 0, entries.Count - 1)] : null;
        bool has = e != null;

        winBack.enabled = winFrame.enabled = has;
        detailLock.enabled = detailCheck.enabled = false;
        barFrame.enabled = barFill.enabled = false;
        detailProgress.text = "";
        rewardBox.enabled = rewardIcon.enabled = false;
        detailReward.text = "";

        if (!has)
        {
            detailIcon.enabled = false;
            detailName.text = Loc.Get("ui.achievements.empty", "Hier ist noch nichts.");
            detailName.color = GameHudSkin.Stone;
            detailSub.text = detailDesc.text = "";
            return;
        }

        int wx = DetX + 6, wy = DetY + 6;
        FitIcon(detailIcon, e.Icon, wx + 3, wy + 3, WinS - 6, WinS - 6);
        detailIcon.color = e.Shadow ? Silhouette : e.Done || GridMode ? Color.white : LockedTint;

        winFrame.sprite = GameHudSkin.SlotFrame(e.Done ? GameHudSkin.SlotLook.Gold : GameHudSkin.SlotLook.Wood);
        winBack.color = e.Done ? Color.white : new Color(0.72f, 0.66f, 0.66f, 1f);
        detailLock.enabled = e.Shadow;
        detailCheck.enabled = e.Done && tab != TabBestiary;

        detailName.text = e.Title.ToUpperInvariant();
        detailName.color = e.Done ? GameHudSkin.Gold : GameHudSkin.Cream;
        detailSub.text = e.Sub;
        detailSub.color = e.SubColor;
        detailDesc.text = e.Desc;

        if (e.Progress >= 0f)
        {
            barFrame.enabled = true;
            SetFill(barFill, barFrame.rectTransform.sizeDelta.x - 2f, e.Progress);
            detailProgress.text = e.ProgressText;
        }

        if (!string.IsNullOrEmpty(e.Reward))
        {
            rewardBox.enabled = true;
            detailReward.text = e.Reward;
            detailReward.color = e.RewardColor;

            if (e.RewardIcon != null)
            {
                Sprite s = e.RewardIcon;
                rewardIcon.sprite = s;
                rewardIcon.enabled = true;
                int rbY = DetY + DetH - 19;
                OptionsKit.Move(rewardIcon.rectTransform, DetX + 8 + Mathf.Floor((9 - s.rect.width) / 2f),
                                rbY + Mathf.Floor((15 - s.rect.height) / 2f), s.rect.width, s.rect.height);
            }
        }
    }

    // ==================================================================
    //  Helfer
    // ==================================================================

    /// <summary>Die Achievement-/Unlock-Bilder aus Resources/AchievementsBook/ in 12, 21 oder 32 px.</summary>
    private static Sprite BookIcon(string iconKey, int size)
    {
        if (string.IsNullOrEmpty(iconKey)) return null;
        return Resources.Load<Sprite>($"AchievementsBook/{iconKey}_{size}");
    }

    /// <summary>Fuellbalken auf ganze Pixel: Breite = Anteil der Innenbreite.</summary>
    private static void SetFill(Image fill, float innerWidth, float t)
    {
        RectTransform rt = fill.rectTransform;
        float w = Mathf.Round(innerWidth * Mathf.Clamp01(t));
        rt.sizeDelta = new Vector2(w, rt.sizeDelta.y);
        fill.enabled = w > 0f;
    }
}
