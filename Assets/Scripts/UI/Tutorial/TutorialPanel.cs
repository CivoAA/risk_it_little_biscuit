using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tutorial-Fenster im Stil von "UI 2.0" (wie <see cref="OptionsPanel"/>):
/// dunkle Karte, Titelband, Holzknoepfe. Links eine animierte Bildtafel
/// (<see cref="TutorialPicture"/>), rechts Ueberschrift, Text und ein
/// Hinweiskasten, unten Seitenpunkte und Blaettern.
///
///   TutorialPanel.ShowOnce(TutorialTopic.Skilltree);   // beim ersten Oeffnen einer Station
///   TutorialPanel.OpenLibrary();                       // Pausenmenue im Hub: alle Themen
///
/// Zwei Auspraegungen:
///   Einzeln     - ein Thema, ÜBERSPRINGEN / Zurueck / WEITER, auf der letzten
///                 Seite LOS GEHT'S!. Liegt ueber dem Fenster, das es erklaert.
///   Bibliothek  - links alle Themen als Liste, rechts dieselben Seiten.
///
/// Welche Themen schon gezeigt wurden, steht in den PlayerPrefs
/// (<c>tutorial.seen.[key]</c>); "Spielstand zuruecksetzen" loescht das mit.
/// Wer in den Optionen "Tipps" abschaltet, bekommt keine Tutorials mehr
/// ungefragt - nachschlagen geht immer.
///
/// Solange das Fenster offen ist, ruhen die Fenster darunter: sie fragen
/// <see cref="Blocking"/> ab, bevor sie Tasten oder Mausklicks lesen.
///
/// Masse in Seitenpixeln (480x270, Ursprung oben links), ganzzahlig skaliert.
/// Texte: ui.tutorial.* in de/en.json, Inhalte in <see cref="TutorialContent"/>.
/// </summary>
public class TutorialPanel : MonoBehaviour
{
    // ==================================================================
    //  Masse (Seitenpixel)
    // ==================================================================

    private const int Pad = 14;
    private const int PicW = TutorialPicture.W, PicH = TutorialPicture.H;
    private const int ColGap = 12, TextW = 184;
    private const int BlockW = PicW + ColGap + TextW;

    private const int ListW = 84, ListGap = 10, ListBtnH = 20, ListStep = 24;

    private const int BodyTop = 20;
    private const int FootGap = 10, FootH = 18, FootBottom = 12;
    private const int SkipW = 88, NextW = 88, PrevW = 24, BtnGap = 4;
    private const int DotStep = 8;

    private const int HeadH = 14, HeadGap = 3, TipGap = 7, TipPad = 5;

    /// <summary>Ueber allen Hub-Fenstern und der Muenzanzeige (205), unter Feedback (220).</summary>
    private const int SortingOrder = 210;

    private const float SlideTime = 0.16f;
    private const int SlideDistance = 8;

    private const string SeenPrefix = "tutorial.seen.";

    // ==================================================================
    //  Zustand
    // ==================================================================

    private static TutorialPanel instance;
    private static int closedFrame = -10;

    public static bool IsOpen => instance != null;

    /// <summary>
    /// Offen - oder im selben bzw. vorigen Frame geschlossen. Fenster darunter
    /// lesen solange keine Eingabe: der ESC, der das Tutorial schliesst, soll
    /// nicht auch noch den Skilltree zumachen.
    /// </summary>
    public static bool Blocking => instance != null || Time.frameCount - closedFrame <= 1;

    private bool library;
    private List<TutorialTopicDef> topics;
    private int topicIndex, pageIndex;

    private TMP_FontAsset textFont, pixelFont;
    private RectTransform page;
    private CanvasScaler scaler;
    private Image dim;
    private RectInt content;
    private Vector2Int lastScreen;
    private Vector2 pageHome;

    private int cardX, cardY, cardW, cardH, bodyH;
    private TutorialPicture picture;
    private float pageShownAt, openedAt;
    private int openedFrame;
    private bool dirty;
    private bool modalPushed;

    // ==================================================================
    //  Oeffnen / Schliessen
    // ==================================================================

    /// <summary>
    /// Zeigt das Thema, wenn es noch nie gezeigt wurde (und "Tipps" an ist).
    /// Gehoert ans Ende der Open()-Methode eines Fensters.
    /// </summary>
    public static void ShowOnce(TutorialTopic topic)
    {
        if (!GameSettings.Tips) return;
        if (HasSeen(topic) || instance != null) return;

        MarkSeen(topic);
        Open(topic);
    }

    /// <summary>Ein Thema, Seite 1.</summary>
    public static void Open(TutorialTopic topic)
    {
        if (instance != null) return;
        Create(false, topic);
    }

    /// <summary>Alle Themen zum Nachschlagen.</summary>
    public static void OpenLibrary(TutorialTopic first = TutorialTopic.Character)
    {
        if (instance != null) return;
        Create(true, first);
    }

    public static void Close()
    {
        if (instance == null) return;
        TutorialPanel panel = instance;
        instance = null;
        closedFrame = Time.frameCount;
        Destroy(panel.gameObject);
    }

    public static bool HasSeen(TutorialTopic topic) =>
        PlayerPrefs.GetInt(SaveProfiles.PrefKey(SeenPrefix + TutorialContent.Get(topic).Key), 0) == 1;

    public static void MarkSeen(TutorialTopic topic)
    {
        PlayerPrefs.SetInt(SaveProfiles.PrefKey(SeenPrefix + TutorialContent.Get(topic).Key), 1);
        PlayerPrefs.Save();
    }

    /// <summary>Alle Tutorials kommen beim naechsten Oeffnen wieder (Spielstand zuruecksetzen).</summary>
    public static void ResetSeen()
    {
        foreach (TutorialTopicDef t in TutorialContent.All) PlayerPrefs.DeleteKey(SaveProfiles.PrefKey(SeenPrefix + t.Key));
        PlayerPrefs.Save();
    }

    /// <summary>Die PlayerPrefs-Schluessel aller Tutorials, ohne Profil-Vorsilbe (fuer <see cref="SaveProfiles"/>).</summary>
    public static System.Collections.Generic.IEnumerable<string> AllSeenKeys()
    {
        foreach (TutorialTopicDef t in TutorialContent.All) yield return SeenPrefix + t.Key;
    }

    private static void Create(bool asLibrary, TutorialTopic topic)
    {
        GameObject go = new GameObject("TutorialPanel");
        TutorialPanel panel = go.AddComponent<TutorialPanel>();
        instance = panel;

        panel.library = asLibrary;
        panel.topics = asLibrary
            ? new List<TutorialTopicDef>(TutorialContent.All)
            : new List<TutorialTopicDef> { TutorialContent.Get(topic) };
        panel.topicIndex = Mathf.Max(0, panel.topics.FindIndex(t => t.Topic == topic));
        panel.pageIndex = 0;
        panel.Build();
    }

    private void Awake()
    {
        openedFrame = Time.frameCount;
        openedAt = Time.unscaledTime;
        textFont = PixelUI.FindTextFont();
        pixelFont = PixelUI.FindPixelFont();
    }

    private void OnEnable() => Loc.LanguageChanged += OnLanguageChanged;

    private void OnDisable() => Loc.LanguageChanged -= OnLanguageChanged;

    private void OnDestroy()
    {
        picture?.Dispose();
        picture = null;

        if (modalPushed)
        {
            HubUI.PopModal();
            modalPushed = false;
        }

        if (instance == this)
        {
            instance = null;
            closedFrame = Time.frameCount;
        }
    }

    private void OnLanguageChanged() => dirty = true;

    // ==================================================================
    //  Eingabe
    // ==================================================================

    private void Update()
    {
        if (dirty)
        {
            dirty = false;
            Rebuild();
        }

        var size = new Vector2Int(Screen.width, Screen.height);
        if (page != null && size != lastScreen)
        {
            lastScreen = size;
            OptionsKit.Layout(page, scaler, content);
            pageHome = page.anchoredPosition;
        }

        AnimateOpen();
        picture?.Run(Time.unscaledTime - pageShownAt);

        // Der Tastendruck, der das Fenster darunter geoeffnet hat, zaehlt hier nicht.
        if (Time.frameCount == openedFrame) return;

        // Feedback & Co. koennten ueber uns liegen - dann gehoert ihnen die Tastatur.
        if (FeedbackPanel.IsOpen) return;

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))
        {
            OptionsKit.PlayClick();
            Close();
            return;
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Step(-1, true);

        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)
            || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
            || Input.GetKeyDown(KeyCode.Space))
            Step(+1, true);

        if (library)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) SelectTopic(topicIndex - 1, true);
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) SelectTopic(topicIndex + 1, true);
        }
    }

    private TutorialTopicDef Topic => topics[topicIndex];
    private bool IsLastPage => pageIndex >= Topic.Pages.Length - 1;
    private bool IsFirstOverall => pageIndex == 0 && (!library || topicIndex == 0);
    private bool IsLastOverall => IsLastPage && (!library || topicIndex >= topics.Count - 1);

    /// <summary>Eine Seite weiter/zurueck. In der Bibliothek geht es ueber das Themenende hinaus.</summary>
    private void Step(int delta, bool sound)
    {
        if (delta > 0)
        {
            if (!IsLastPage) pageIndex++;
            else if (library && topicIndex < topics.Count - 1) { topicIndex++; pageIndex = 0; }
            else if (!library) { if (sound) OptionsKit.PlayClick(); Close(); return; }
            else return;
        }
        else
        {
            if (pageIndex > 0) pageIndex--;
            else if (library && topicIndex > 0) { topicIndex--; pageIndex = topics[topicIndex].Pages.Length - 1; }
            else return;
        }

        if (sound) OptionsKit.PlayClick();
        dirty = true;
    }

    private void SelectTopic(int index, bool sound)
    {
        index = Mathf.Clamp(index, 0, topics.Count - 1);
        if (index == topicIndex) return;
        topicIndex = index;
        pageIndex = 0;
        if (sound) OptionsKit.PlayClick();
        dirty = true;
    }

    /// <summary>Kurzes Hereingleiten beim Oeffnen - in ganzen Pixeln, ohne CanvasGroup.</summary>
    private void AnimateOpen()
    {
        if (page == null) return;
        float p = Mathf.Clamp01((Time.unscaledTime - openedAt) / SlideTime);
        float e = 1f - (1f - p) * (1f - p);
        float offset = Mathf.Round((1f - e) * SlideDistance);
        page.anchoredPosition = pageHome + new Vector2(0f, -offset);
        // Im linearen Farbraum wirkt Alpha schwaecher als eingestellt - ueber einem
        // Fenster kraeftig abdunkeln, sonst konkurrieren die beiden Titelbaender.
        if (dim != null) dim.color = new Color(0.06f, 0.04f, 0.05f, (library ? 0.75f : 0.88f) * e);
    }

    // ==================================================================
    //  Aufbau
    // ==================================================================

    private void Build()
    {
        // Sperrt den Hub wie jedes andere Fenster - meist liegt ohnehin eins
        // darunter, gezaehlt wird trotzdem, damit [E] im Hub sicher ruht.
        HubUI.PushModal();
        modalPushed = true;

        BuildPage();
    }

    private void Rebuild()
    {
        picture?.Dispose();
        picture = null;
        OptionsKit.Clear(transform);
        BuildPage();
    }

    private void BuildPage()
    {
        page = OptionsKit.CreatePage(gameObject, SortingOrder, 0f, out scaler);
        dim = transform.Find("Dim")?.GetComponent<Image>();

        bodyH = MeasureBody();

        cardW = library ? Pad + ListW + ListGap + BlockW + Pad : Pad + BlockW + Pad;
        cardH = BodyTop + bodyH + FootGap + FootH + FootBottom;
        cardX = (OptionsKit.RefW - cardW) / 2;
        cardY = 12 + (OptionsKit.RefH - 12 - cardH) / 2;
        int ribbonY = cardY - 12;

        OptionsKit.Img("Card", page, cardX, cardY, cardW, cardH, GameHudSkin.Card, true);
        OptionsKit.Ribbon(page, cardX + cardW / 2f, ribbonY,
                          library ? Loc.Get("ui.tutorial.library.title", "TUTORIALS") : Topic.TitleText,
                          pixelFont, textFont);

        int bodyY = cardY + BodyTop;
        int blockX = library ? cardX + Pad + ListW + ListGap : cardX + Pad;

        if (library) BuildList(cardX + Pad, bodyY);

        BuildPicture(blockX, bodyY);
        BuildText(blockX + PicW + ColGap, bodyY, Topic, pageIndex, page);
        BuildFooter(blockX, bodyY + bodyH + FootGap);

        content = new RectInt(cardX - 12, ribbonY, cardW + 24, cardY + cardH - ribbonY);
        OptionsKit.Layout(page, scaler, content);
        pageHome = page.anchoredPosition;
        lastScreen = new Vector2Int(Screen.width, Screen.height);
        AnimateOpen();

        pageShownAt = Time.unscaledTime;
        picture?.Run(0f);
    }

    /// <summary>
    /// Hoehe des Inhalts: die laengste Seite aller gezeigten Themen. So bleibt
    /// die Karte beim Blaettern gleich gross und springt nicht.
    /// </summary>
    private int MeasureBody()
    {
        int h = PicH;
        if (library) h = Mathf.Max(h, topics.Count * ListStep - (ListStep - ListBtnH));

        RectTransform probe = OptionsKit.Rect("Probe", page, 0, 0, TextW, 10);
        foreach (TutorialTopicDef t in topics)
            for (int i = 0; i < t.Pages.Length; i++)
                h = Mathf.Max(h, BuildText(0, 0, t, i, probe));

        probe.SetParent(null, false);
        Destroy(probe.gameObject);
        return h;
    }

    // ---------- Themenliste (Bibliothek) ----------

    private void BuildList(int x, int y)
    {
        for (int i = 0; i < topics.Count; i++)
        {
            int index = i;
            TutorialTopicDef t = topics[i];
            SkinButton b = SkinButton.Create(page, x, y + i * ListStep, ListW, ListBtnH, t.TitleText, textFont,
                                             SkinButton.Kind.Wood, () => SelectTopic(index, false), t.Icon());
            b.Active = i == topicIndex;
        }
    }

    // ---------- Bildtafel ----------

    private void BuildPicture(int x, int y)
    {
        OptionsKit.Img("PictureWell", page, x, y, PicW, PicH, GameHudSkin.Well, true);

        RectTransform root = OptionsKit.Rect("Picture", page, x, y, PicW, PicH);
        root.gameObject.AddComponent<RectMask2D>();

        picture = new TutorialPicture(root, textFont);
        try
        {
            Topic.Pages[pageIndex].Draw?.Invoke(picture);
        }
        catch (System.Exception e)
        {
            // Eine kaputte Tafel soll das Tutorial nicht mitreissen - der Text steht trotzdem.
            Debug.LogWarning($"[Tutorial] Bildtafel {Topic.Key}/{pageIndex + 1} fehlgeschlagen: {e}");
        }
    }

    // ---------- Textspalte ----------

    /// <summary>Ueberschrift, Text, Hinweiskasten. Gibt die Hoehe zurueck.</summary>
    private int BuildText(int x, int y, TutorialTopicDef t, int index, Transform parent)
    {
        int top = y;

        OptionsKit.ShadowLabel("Head", parent, x, y, TextW, HeadH, t.Head(index), textFont,
                               OptionsKit.SizeText, GameHudSkin.Gold, TextAlignmentOptions.Left);
        y += HeadH + HeadGap;

        y += Paragraph(parent, "Body", x, y, TextW, t.Body(index), GameHudSkin.Parchment);

        string tip = t.Tip(index);
        if (!string.IsNullOrEmpty(tip))
        {
            y += TipGap;

            Sprite icon = t.Pages[index].TipIcon?.Invoke() ?? GameHudSkin.Bulb;
            int iconW = Mathf.RoundToInt(icon.rect.width), iconH = Mathf.RoundToInt(icon.rect.height);

            RectTransform box = OptionsKit.Rect("Tip", parent, x, y, TextW, 10);
            Image bg = OptionsKit.Img("TipBg", box, 0, 0, TextW, 10, GameHudSkin.LevelChip, true);
            int textX = TipPad + iconW + 4;
            int textH = Paragraph(box, "TipText", textX, TipPad - 1, TextW - textX - TipPad, tip, GameHudSkin.ParchDark);
            int boxH = textH + TipPad * 2 - 1;
            OptionsKit.Move(bg.rectTransform, 0, 0, TextW, boxH);
            OptionsKit.Move(box, x, y, TextW, boxH);
            OptionsKit.Img("TipIcon", box, TipPad, TipPad + Mathf.Max(0, (11 - iconH) / 2), iconW, iconH, icon);
            y += boxH;
        }

        return y - top;
    }

    /// <summary>Umbrechender Absatz mit Hervorhebungen; gibt seine Hoehe zurueck.</summary>
    private int Paragraph(Transform parent, string name, float x, float y, float w, string text, Color color)
    {
        string marked = Mark(text);
        TextMeshProUGUI t = OptionsKit.Label(name, parent, x, y, w, 10, marked, textFont, OptionsKit.SizeText,
                                             color, TextAlignmentOptions.TopLeft);
        t.richText = true;
        t.textWrappingMode = TextWrappingModes.Normal;
        int h = Mathf.CeilToInt(t.GetPreferredValues(marked, w, 0f).y) + 1;
        OptionsKit.Move(t.rectTransform, x, y, w, h);
        return h;
    }

    private static readonly string GoldTag = "<color=#" + ColorUtility.ToHtmlStringRGB(GameHudSkin.Gold) + ">";

    /// <summary>*Wort* wird golden. Ein einzelnes Sternchen ohne Partner bleibt stehen.</summary>
    private static string Mark(string text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('*') < 0) return text;

        var sb = new StringBuilder(text.Length + 32);
        int i = 0;
        while (i < text.Length)
        {
            int open = text.IndexOf('*', i);
            int close = open >= 0 ? text.IndexOf('*', open + 1) : -1;
            if (open < 0 || close < 0)
            {
                sb.Append(text, i, text.Length - i);
                break;
            }

            sb.Append(text, i, open - i);
            sb.Append(GoldTag).Append(text, open + 1, close - open - 1).Append("</color>");
            i = close + 1;
        }
        return sb.ToString();
    }

    // ---------- Fussleiste ----------

    private void BuildFooter(int x, int y)
    {
        int right = x + BlockW;
        int nextX = right - NextW;
        int prevX = nextX - BtnGap - PrevW;

        int dotsFrom = x, dotsTo = prevX - BtnGap;

        if (library)
        {
            SkinButton.Create(page, cardX + Pad, y, ListW, FootH, Loc.Get("ui.tutorial.close", "SCHLIESSEN"),
                              textFont, SkinButton.Kind.Wood, Close);
        }
        else if (!IsLastPage)
        {
            SkinButton.Create(page, x, y, SkipW, FootH, Loc.Get("ui.tutorial.skip", "ÜBERSPRINGEN"),
                              textFont, SkinButton.Kind.Wood, Close);
            dotsFrom = x + SkipW + BtnGap;
        }

        SkinButton prev = SkinButton.Create(page, prevX, y, PrevW, FootH, "", textFont, SkinButton.Kind.Wood,
                                            () => Step(-1, false), GameHudSkin.ArrowLeft);
        prev.Disabled = IsFirstOverall;

        bool finish = !library && IsLastPage;
        string nextText = finish ? Loc.Get("ui.tutorial.done", "LOS GEHT'S!") : Loc.Get("ui.tutorial.next", "WEITER");
        SkinButton next = SkinButton.Create(page, nextX, y, NextW, FootH, nextText, textFont,
                                            finish ? SkinButton.Kind.Primary : SkinButton.Kind.Wood,
                                            () => Step(+1, false));
        next.Disabled = library && IsLastOverall;

        BuildDots(dotsFrom, dotsTo, y);
    }

    /// <summary>Seitenpunkte des aktuellen Themas, mittig zwischen den Knoepfen.</summary>
    private void BuildDots(int from, int to, int y)
    {
        int count = Topic.Pages.Length;
        if (count <= 1) return;

        Sprite full = GameHudSkin.Pip(GameHudSkin.PipKind.Full);
        Sprite empty = GameHudSkin.Pip(GameHudSkin.PipKind.Empty);
        int pw = Mathf.RoundToInt(full.rect.width), ph = Mathf.RoundToInt(full.rect.height);

        int total = (count - 1) * DotStep + pw;
        int x0 = from + (to - from - total) / 2;
        int dy = y + (FootH - 1 - ph) / 2;

        for (int i = 0; i < count; i++)
            OptionsKit.Img("Dot_" + i, page, x0 + i * DotStep, dy, pw, ph, i == pageIndex ? full : empty);
    }
}
