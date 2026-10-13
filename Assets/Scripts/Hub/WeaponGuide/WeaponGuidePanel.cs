using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Waffenkunde: erklaert, wie Waffen, Buffs und Evos mit den Stats skalieren.
/// Geoeffnet ueber <see cref="WeaponGuideTrigger"/> (spaeter ein NPC, vorerst
/// an der rechten Wand in Ricos Laden). Stil wie <see cref="DifficultyPanel"/>.
///
///   Titelband   WAFFENKUNDE
///   Reiter      GRUNDLAGEN  WAFFEN  BUFFS  EVOS
///   links       Liste der Eintraege (Symbol + Name)
///   rechts      Name, Kurzbeschreibung, darunter der Skalierungs-Text
///   Fussleiste  SCHLIESSEN
///
/// Die Texte der Waffen, Buffs und Evos sind weapon.[id].scaling in den
/// Sprachdateien (gepflegt ueber Tools > Werkbank > Skalierungs-Texte), die
/// Grundlagen ui.guide.basics.*. A/D wechselt den Reiter, W/S den Eintrag,
/// Esc/E schliesst.
/// </summary>
public class WeaponGuidePanel : MonoBehaviour
{
    // ==================================================================
    //  Masse (Seitenpixel, 480x270)
    // ==================================================================

    private const int CardX = 36, CardY = 40, CardW = 408, CardH = 200;
    private const int RibbonY = 28;
    private static readonly RectInt Content = new RectInt(CardX - 12, RibbonY, CardW + 24, CardY + CardH - RibbonY);

    private const int TabY = 52, TabW = 84, TabH = 16, TabGap = 6;
    private const int TabX = CardX + (CardW - 4 * TabW - 3 * TabGap) / 2;

    private const int ListX = 48, ListY = 74, ListW = 136, ListH = 134;
    private const int RowH = 16, RowStep = 17, RowInset = 3;

    private const int DetailX = 192, DetailY = 74, DetailW = 240, DetailH = 134;
    private const int DetailPad = 6;
    private const int IconSize = 14;

    private const int FootY = 216, FootH = 18, FootW = 86;

    private enum Tab { Basics = 0, Weapons = 1, Buffs = 2, Evos = 3 }

    private static readonly string[] TabKeys = { "ui.guide.tab.basics", "ui.guide.tab.weapons", "ui.guide.tab.buffs", "ui.guide.tab.evos" };
    private static readonly string[] TabFallbacks = { "GRUNDLAGEN", "WAFFEN", "BUFFS", "EVOS" };

    /// <summary>Ein Eintrag der Liste: Katalog-Eintrag oder Grundlagen-Seite.</summary>
    private class Entry
    {
        public WeaponDef Def;
        public string BasicsId;

        public string Name => Def != null
            ? Def.Name
            : Loc.Get($"ui.guide.basics.{BasicsId}.name", BasicsId);
    }

    /// <summary>Grundlagen-Seiten in Reihenfolge; Texte unter ui.guide.basics.[id].name/.text.</summary>
    private static readonly string[] BasicsIds = { "overview", "extra_shot", "aoe", "duration", "cooldown", "damage" };

    // ==================================================================

    private static WeaponGuidePanel instance;
    public static bool IsOpen => instance != null;

    private static Tab lastTab = Tab.Basics;

    private TMP_FontAsset textFont, pixelFont;
    private RectTransform page;
    private CanvasScaler scaler;
    private Vector2Int lastScreen;
    private int openedFrame;
    private HubUI hub;

    private Tab tab = Tab.Basics;
    private int selected;
    private readonly List<Entry> entries = new List<Entry>();
    private readonly List<SkinButton> rows = new List<SkinButton>();
    private readonly SkinButton[] tabButtons = new SkinButton[4];

    private RectTransform listContent, detailContent;
    private ScrollRect listScroll, detailScroll;
    private Image detailIcon;
    private TextMeshProUGUI detailName, detailDesc, detailText;
    private Image detailRule;

    public static void Open()
    {
        if (instance != null) return;
        GameObject go = new GameObject("WeaponGuidePanel");
        instance = go.AddComponent<WeaponGuidePanel>();
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
        textFont = PixelUI.FindTextFont();
        pixelFont = PixelUI.FindPixelFont();
        tab = lastTab;
        Build();
        BlockHub(true);
    }

    private void OnEnable() => Loc.LanguageChanged += Build;
    private void OnDisable() => Loc.LanguageChanged -= Build;

    private void OnDestroy()
    {
        BlockHub(false);
        if (instance == this) instance = null;
    }

    /// <summary>Sperrt den Hub wie Werkbank und Shop, solange das Fenster offen ist.</summary>
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

    private void Update()
    {
        var size = new Vector2Int(Screen.width, Screen.height);
        if (size != lastScreen)
        {
            lastScreen = size;
            OptionsKit.Layout(page, scaler, Content);
        }

        // Der Tastendruck, der das Fenster geoeffnet hat, darf es nicht gleich schliessen.
        if (Time.frameCount == openedFrame) return;

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))
        {
            OptionsKit.PlayClick();
            Close();
            return;
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) StepTab(-1);
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) StepTab(1);
        if (Input.GetKeyDown(KeyCode.Tab)) StepTab(1);
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) StepEntry(-1);
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) StepEntry(1);
    }

    private void StepTab(int delta)
    {
        int next = ((int)tab + delta + 4) % 4;
        OptionsKit.PlayClick();
        SetTab((Tab)next);
    }

    private void StepEntry(int delta)
    {
        if (entries.Count == 0) return;
        int next = Mathf.Clamp(selected + delta, 0, entries.Count - 1);
        if (next == selected) return;
        OptionsKit.PlayClick();
        Select(next);
        KeepRowVisible(next);
    }

    // ==================================================================
    //  Aufbau
    // ==================================================================

    private void Build()
    {
        OptionsKit.Clear(transform);
        rows.Clear();

        // Wie die Schwierigkeitswahl: ueber Shop (130) und Werkbank (155), unter den Optionen (210).
        page = OptionsKit.CreatePage(gameObject, 160, 0.6f, out scaler);

        OptionsKit.Img("Card", page, CardX, CardY, CardW, CardH, GameHudSkin.Card, true);
        OptionsKit.Ribbon(page, CardX + CardW / 2f, RibbonY, Loc.Get("ui.guide.title", "WAFFENKUNDE"),
                          pixelFont, textFont);

        for (int i = 0; i < 4; i++)
        {
            Tab t = (Tab)i;
            tabButtons[i] = SkinButton.Create(page, TabX + i * (TabW + TabGap), TabY, TabW, TabH,
                                              Loc.Get(TabKeys[i], TabFallbacks[i]), textFont,
                                              SkinButton.Kind.Wood, () => SetTab(t));
        }

        BuildList();
        BuildDetail();

        SkinButton.Create(page, CardX + (CardW - FootW) / 2, FootY, FootW, FootH,
                          Loc.Get("ui.guide.close", "SCHLIESSEN"), textFont, SkinButton.Kind.Primary, Close);

        Fill();
        OptionsKit.Layout(page, scaler, Content);
        lastScreen = new Vector2Int(Screen.width, Screen.height);
    }

    private void BuildList()
    {
        OptionsKit.Img("ListWell", page, ListX, ListY, ListW, ListH, GameHudSkin.Well, true);

        RectTransform view = OptionsKit.Rect("ListView", page, ListX + RowInset, ListY + RowInset,
                                             ListW - 2 * RowInset, ListH - 2 * RowInset);
        listScroll = MakeScroll(view, RowStep, out listContent);
    }

    private void BuildDetail()
    {
        OptionsKit.Img("DetailWell", page, DetailX, DetailY, DetailW, DetailH, GameHudSkin.Well, true);

        RectTransform view = OptionsKit.Rect("DetailView", page, DetailX + DetailPad, DetailY + DetailPad - 2,
                                             DetailW - 2 * DetailPad, DetailH - 2 * DetailPad + 4);
        detailScroll = MakeScroll(view, 12f, out detailContent);

        float w = DetailW - 2 * DetailPad;

        detailIcon = OptionsKit.Img("Icon", detailContent, 0, 2, IconSize, IconSize, null);
        detailName = OptionsKit.Label("Name", detailContent, IconSize + 4, 2, w - IconSize - 4, 14, "",
                                      textFont, OptionsKit.SizeText, GameHudSkin.Gold, TextAlignmentOptions.Left);
        detailName.overflowMode = TextOverflowModes.Ellipsis;

        detailDesc = OptionsKit.Label("Desc", detailContent, 0, 20, w, 14, "", textFont,
                                      OptionsKit.SizeText, GameHudSkin.ParchDark, TextAlignmentOptions.TopLeft);
        detailDesc.textWrappingMode = TextWrappingModes.Normal;

        detailRule = OptionsKit.Img("Rule", detailContent, 0, 40, w, 1, GameHudSkin.White, GameHudSkin.StoneDark);

        detailText = OptionsKit.Label("Text", detailContent, 0, 44, w, 14, "", textFont,
                                      OptionsKit.SizeText, GameHudSkin.Cream, TextAlignmentOptions.TopLeft);
        detailText.textWrappingMode = TextWrappingModes.Normal;
    }

    /// <summary>Senkrechter ScrollRect mit Maske; das Mausrad greift auf der ganzen Flaeche.</summary>
    private static ScrollRect MakeScroll(RectTransform view, float step, out RectTransform content)
    {
        view.gameObject.AddComponent<RectMask2D>();

        // Ohne Graphic mit raycastTarget kommt das Mausrad nur ueber Knoepfen an.
        Image catcher = OptionsKit.Stretch("ScrollCatcher", view, GameHudSkin.White, new Color(0f, 0f, 0f, 0f));
        catcher.raycastTarget = true;

        ScrollRect scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = step;
        scroll.viewport = view;

        content = OptionsKit.Rect("Content", view, 0, 0, view.sizeDelta.x, view.sizeDelta.y);
        scroll.content = content;
        return scroll;
    }

    // ==================================================================
    //  Inhalt
    // ==================================================================

    private void SetTab(Tab next)
    {
        if (tab == next) return;
        tab = next;
        lastTab = next;
        Fill();
    }

    /// <summary>Liste fuer den aktuellen Reiter neu anlegen und den ersten Eintrag zeigen.</summary>
    private void Fill()
    {
        for (int i = 0; i < tabButtons.Length; i++)
            if (tabButtons[i] != null) tabButtons[i].Active = i == (int)tab;

        entries.Clear();
        if (tab == Tab.Basics)
        {
            foreach (string id in BasicsIds) entries.Add(new Entry { BasicsId = id });
        }
        else
        {
            PoolKind kind = tab == Tab.Weapons ? PoolKind.Weapon : tab == Tab.Buffs ? PoolKind.Buff : PoolKind.Evo;
            foreach (WeaponDef def in WeaponCatalog.OfKind(kind))
            {
                // Was es im Spiel gar nicht gibt (z.B. der alte Mochi-Faden), bleibt draussen.
                if (kind != PoolKind.Evo && !WeaponCatalog.IsObtainable(def.Id)) continue;
                entries.Add(new Entry { Def = def });
            }
        }

        foreach (SkinButton row in rows)
            if (row != null) Destroy(row.gameObject);
        rows.Clear();

        float rowW = listContent.sizeDelta.x;
        for (int i = 0; i < entries.Count; i++)
        {
            int index = i;
            Entry e = entries[i];
            Sprite icon = e.Def != null ? e.Def.IconSmall : null;
            SkinButton row = SkinButton.Create(listContent, 0, i * RowStep, rowW, RowH, e.Name, textFont,
                                               SkinButton.Kind.Wood, () => Select(index), icon);
            if (row.Label != null) row.Label.overflowMode = TextOverflowModes.Ellipsis;
            rows.Add(row);
        }

        listContent.sizeDelta = new Vector2(rowW, Mathf.Max(ListH - 2 * RowInset, entries.Count * RowStep));
        listContent.anchoredPosition = Vector2.zero;

        Select(0);
    }

    private void Select(int index)
    {
        if (entries.Count == 0)
        {
            ShowDetail(null, "", "", "");
            return;
        }

        selected = Mathf.Clamp(index, 0, entries.Count - 1);
        for (int i = 0; i < rows.Count; i++) rows[i].Active = i == selected;

        Entry e = entries[selected];
        if (e.Def == null)
        {
            ShowDetail(null, e.Name, "",
                       Loc.Get($"ui.guide.basics.{e.BasicsId}.text", ""));
            return;
        }

        ShowDetail(e.Def.Icon, e.Def.Name, InfoText(e.Def), e.Def.Scaling);
    }

    /// <summary>Kurzbeschreibung, dazu Rezept (Evo) oder wofuer es Zutat ist.</summary>
    private static string InfoText(WeaponDef def)
    {
        List<string> lines = new List<string>();
        if (!string.IsNullOrEmpty(def.Description)) lines.Add(def.Description);

        if (def.Kind == PoolKind.Evo)
        {
            EvoDef recipe = WeaponCatalog.FindEvo(def.Id);
            if (recipe != null)
            {
                string a = recipe.A != null ? recipe.A.Name : recipe.IngredientA;
                string b = recipe.B != null ? recipe.B.Name : recipe.IngredientB;
                lines.Add(recipe.IngredientCount == 1
                    ? string.Format(Loc.Get("ui.guide.recipe_solo", "Rezept: {0} auf Maximalstufe"), a)
                    : string.Format(Loc.Get("ui.guide.recipe", "Rezept: {0} + {1}"), a, b));
            }
        }
        else
        {
            List<EvoDef> uses = WeaponCatalog.RecipesUsing(def.Id);
            if (uses.Count > 0)
            {
                List<string> names = new List<string>();
                foreach (EvoDef evo in uses) names.Add(evo.Name);
                lines.Add(string.Format(Loc.Get("ui.guide.partof", "Zutat für: {0}"), string.Join(", ", names)));
            }
        }

        return string.Join("\n", lines);
    }

    /// <summary>Rechte Seite: Kopf, Beschreibung, Trennlinie, Skalierungs-Text - alles scrollt zusammen.</summary>
    private void ShowDetail(Sprite icon, string name, string info, string text)
    {
        float w = DetailW - 2 * DetailPad;

        detailIcon.sprite = icon;
        detailIcon.enabled = icon != null;
        float nameX = icon != null ? IconSize + 4 : 0;
        OptionsKit.Move(detailName.rectTransform, nameX, 2, w - nameX, 14);
        detailName.text = name;

        float y = 20f;

        detailDesc.gameObject.SetActive(!string.IsNullOrEmpty(info));
        if (!string.IsNullOrEmpty(info))
        {
            float h = Height(detailDesc, info, w);
            detailDesc.text = info;
            OptionsKit.Move(detailDesc.rectTransform, 0, y, w, h);
            y += h + 4f;
        }

        OptionsKit.Move(detailRule.rectTransform, 0, y, w, 1);
        y += 5f;

        if (string.IsNullOrEmpty(text))
            text = Loc.Get("ui.guide.empty", "Dazu steht hier noch nichts.");

        float th = Height(detailText, text, w);
        detailText.text = text;
        OptionsKit.Move(detailText.rectTransform, 0, y, w, th);
        y += th + 2f;

        RectTransform view = (RectTransform)detailContent.parent;
        detailContent.sizeDelta = new Vector2(w, Mathf.Max(view.sizeDelta.y, Mathf.Ceil(y)));
        detailContent.anchoredPosition = Vector2.zero;
    }

    private static float Height(TMP_Text label, string text, float width)
    {
        return Mathf.Ceil(label.GetPreferredValues(text, width, 0f).y) + 1f;
    }

    /// <summary>Scrollt die Liste so, dass die gewaehlte Zeile (Pfeiltasten) zu sehen ist.</summary>
    private void KeepRowVisible(int index)
    {
        RectTransform view = (RectTransform)listContent.parent;
        float viewH = view.sizeDelta.y;
        float top = index * RowStep, bottom = top + RowH;
        float scroll = listContent.anchoredPosition.y;

        if (top < scroll) scroll = top;
        else if (bottom > scroll + viewH) scroll = bottom - viewH;

        float max = Mathf.Max(0f, listContent.sizeDelta.y - viewH);
        listContent.anchoredPosition = new Vector2(0f, Mathf.Clamp(scroll, 0f, max));
    }
}
