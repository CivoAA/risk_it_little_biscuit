using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Line = StoryTexts.Line;
using Who = StoryTexts.Who;
using Mood = StoryTexts.Mood;

/// <summary>
/// Die Dialogbox der Story, im Stil von "UI 2.0" (<see cref="OptionsKit"/>):
///
///   - unten die Box (GameHudSkin.Card), darin der getippte Text
///     (<see cref="DialogueText"/>: Pop, Welle, Zittern, farbige Woerter)
///   - darueber das Portrait des Sprechers, doppelt so gross, auf seiner
///     Seite (Pomo links, alle anderen rechts). Wechselt der Sprecher,
///     gleitet das neue herein. Waehrend des Tippens redet Pomo (Mund auf/zu)
///     und blinzelt dazwischen, andere wippen.
///   - auf der Oberkante das Namensschild in der Farbe des Sprechers
///   - unten rechts der Weiter-Pfeil, oben rechts "ESC Ueberspringen"
///
/// Steuerung: Klick / Leertaste / Enter / E = weiter (erst fertig tippen),
/// Esc = ganzes Gespraech ueberspringen.
///
/// Im Hub sperrt die Box den Hub (wie die anderen Fenster), im Lauf haelt sie
/// die Zeit an. Ueber einer Cutscene liegt sie ohne Abdunklung.
/// </summary>
public class StoryDialog : MonoBehaviour
{
    // ==================================================================
    //  Masse (Seitenpixel, 480x270)
    // ==================================================================

    private const int BoxX = 52, BoxY = 190, BoxW = 376, BoxH = 70;
    private const int TextPad = 12;
    private const int PortraitScale = 2;
    private const int PortraitSide = 22;          // Abstand Portrait zum Boxrand
    private const int PortraitSink = 14;          // so weit steckt das Portrait hinter der Box
    private const int NameH = 16;
    private static readonly RectInt Content = new RectInt(0, 0, OptionsKit.RefW, OptionsKit.RefH);

    public class Options
    {
        public float Dim = 0.45f;
        public bool FreezeTime = true;
        public bool BlockHub = true;
        public int SortingOrder = 300;
        public bool AllowSkip = true;
        public object[] FormatArgs;
    }

    // ==================================================================

    private static StoryDialog instance;
    public static bool IsOpen => instance != null;

    /// <summary>Wer gerade spricht (nur gueltig, solange <see cref="IsOpen"/>).</summary>
    public static Who CurrentSpeaker => instance != null ? instance.who : Who.Pomo;

    private Line[] lines;
    private int index = -1;
    private Action onDone;
    private Options options;

    private TMP_FontAsset textFont, pixelFont;
    private RectTransform page;
    private CanvasScaler scaler;
    private Vector2Int lastScreen;

    private RectTransform portraitRt, nameRt, arrowRt;
    private Image portraitImg, nameImg;
    private TextMeshProUGUI nameText, nameShadow, bodyText;
    private DialogueText typer;
    private Who who;
    private Mood mood;
    private bool left;
    private float swapAt = -10f, nextBlink, blinkUntil;
    private int openedFrame;
    private float oldTimeScale = 1f;
    private HubUI hub;
    private bool finished;

    /// <summary>Spielt ein Gespraech ab. onDone kommt auch beim Ueberspringen.</summary>
    public static StoryDialog Play(Line[] lines, Action onDone, Options options = null)
    {
        if (lines == null || lines.Length == 0)
        {
            onDone?.Invoke();
            return null;
        }
        if (instance != null) instance.Finish(false);

        var go = new GameObject("StoryDialog");
        DontDestroyOnLoad(go);
        StoryDialog d = go.AddComponent<StoryDialog>();
        d.lines = lines;
        d.onDone = onDone;
        d.options = options ?? new Options();
        d.Begin();
        return d;
    }

    /// <summary>Gespraech nach Id aus <see cref="StoryTexts"/>.</summary>
    public static StoryDialog Play(string convoId, Action onDone, Options options = null) =>
        Play(StoryTexts.Convo(convoId), onDone, options);

    // ------------------------------------------------------------------

    private void Begin()
    {
        instance = this;
        openedFrame = Time.frameCount;
        textFont = PixelUI.FindTextFont();
        pixelFont = PixelUI.FindPixelFont();

        if (options.FreezeTime)
        {
            oldTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
        if (options.BlockHub) BlockHub(true);

        Build();
        Next();
    }

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

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (!finished) Restore();
    }

    private void Restore()
    {
        if (options == null) return;
        if (options.FreezeTime && Time.timeScale == 0f) Time.timeScale = oldTimeScale;
        if (options.BlockHub) BlockHub(false);
    }

    private void Finish(bool callBack)
    {
        if (finished) return;
        finished = true;
        Restore();
        if (instance == this) instance = null;
        Destroy(gameObject);
        if (callBack) onDone?.Invoke();
    }

    // ==================================================================
    //  Aufbau
    // ==================================================================

    private void Build()
    {
        page = OptionsKit.CreatePage(gameObject, options.SortingOrder, options.Dim, out scaler);

        // Portrait zuerst - die Box liegt darueber und verdeckt den Fuss.
        portraitRt = OptionsKit.Rect("Portrait", page, 0, 0, 96, 96);
        portraitImg = portraitRt.gameObject.AddComponent<Image>();
        portraitImg.raycastTarget = false;
        portraitImg.preserveAspect = true;

        OptionsKit.Img("Box", page, BoxX, BoxY, BoxW, BoxH, GameHudSkin.Card, true);
        // feine Innenkante oben fuer Tiefe
        OptionsKit.Img("BoxLine", page, BoxX + 4, BoxY + 4, BoxW - 8, 1, GameHudSkin.White,
                       OptionsKit.WithAlpha(GameHudSkin.Ink, 0.6f));

        nameRt = OptionsKit.Rect("Name", page, 0, BoxY - NameH + 4, 80, NameH);
        nameImg = nameRt.gameObject.AddComponent<Image>();
        nameImg.sprite = GameHudSkin.Button(GameHudSkin.ButtonLook.Wood);
        nameImg.type = Image.Type.Sliced;
        nameImg.raycastTarget = false;
        nameShadow = OptionsKit.Label("NameShadow", nameRt, 0, 2, 80, NameH - 2, "", textFont,
                                      OptionsKit.SizeText, GameHudSkin.Ink, TextAlignmentOptions.Center);
        nameText = OptionsKit.Label("NameText", nameRt, 0, 1, 80, NameH - 2, "", textFont,
                                    OptionsKit.SizeText, GameHudSkin.Cream, TextAlignmentOptions.Center);

        bodyText = OptionsKit.Label("Body", page, BoxX + TextPad, BoxY + 12, BoxW - 2 * TextPad, BoxH - 22, "",
                                    textFont, OptionsKit.SizeText, GameHudSkin.Cream, TextAlignmentOptions.TopLeft);
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        bodyText.lineSpacing = 6f;
        typer = bodyText.gameObject.AddComponent<DialogueText>();
        typer.CharRevealed += c => StorySounds.Voice(who, c);

        Image arrow = OptionsKit.Img("Arrow", page, BoxX + BoxW - 18, BoxY + BoxH - 15, 8, 8, GameHudSkin.Arrow);
        arrowRt = arrow.rectTransform;
        arrowRt.pivot = new Vector2(0.5f, 0.5f);
        arrowRt.localEulerAngles = new Vector3(0f, 0f, -90f);
        arrow.color = GameHudSkin.Gold;

        if (options.AllowSkip)
        {
            OptionsKit.Label("Skip", page, BoxX + BoxW - 160, BoxY + BoxH + 1, 156, 10, StoryTexts.T("story.ui.skip"),
                             textFont, 8f, OptionsKit.WithAlpha(GameHudSkin.StoneLight, 0.9f),
                             TextAlignmentOptions.Right);
        }

        OptionsKit.Layout(page, scaler, Content);
        lastScreen = new Vector2Int(Screen.width, Screen.height);
    }

    // ==================================================================
    //  Ablauf
    // ==================================================================

    private void Next()
    {
        index++;
        if (index >= lines.Length)
        {
            Finish(true);
            return;
        }

        Line line = lines[index];
        bool newSpeaker = index == 0 || line.Who != who;
        who = line.Who;
        mood = line.Mood;
        left = StoryArt.OnLeft(who);
        if (newSpeaker) swapAt = Time.unscaledTime;

        string text = line.Text;
        if (options.FormatArgs != null && options.FormatArgs.Length > 0)
        {
            try { text = string.Format(text, options.FormatArgs); }
            catch (FormatException) { }
        }

        typer.CharsPerSecond = who == Who.Pomo ? 36f : who == Who.Verkohlter ? 24f : 30f;
        typer.Play(text);

        // Namensschild
        string name = StoryArt.NameOf(who) ?? "";
        nameText.text = nameShadow.text = name;
        nameText.color = StoryArt.AccentOf(who);
        float w = Mathf.Ceil(OptionsKit.Measure(nameText, name)) + 16f;
        if (((int)w & 1) == 1) w += 1f;
        float x = left ? BoxX + 12 : BoxX + BoxW - 12 - w;
        OptionsKit.Move(nameRt, x, BoxY - NameH + 4, w, NameH);
        OptionsKit.Move((RectTransform)nameText.transform, 0, 1, w, NameH - 2);
        OptionsKit.Move((RectTransform)nameShadow.transform, 0, 2, w, NameH - 2);

        nextBlink = Time.unscaledTime + UnityEngine.Random.Range(1.5f, 3.5f);
        UpdatePortrait();
    }

    private void Update()
    {
        var size = new Vector2Int(Screen.width, Screen.height);
        if (size != lastScreen)
        {
            lastScreen = size;
            OptionsKit.Layout(page, scaler, Content);
        }

        UpdatePortrait();
        UpdateArrow();

        if (Time.frameCount == openedFrame) return;

        if (options.AllowSkip && Input.GetKeyDown(KeyCode.Escape))
        {
            OptionsKit.PlayClick();
            Finish(true);
            return;
        }

        bool advance = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) ||
                       Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                       Input.GetKeyDown(KeyCode.E);
        if (!advance) return;

        if (!typer.IsDone)
        {
            typer.Complete();
            return;
        }
        OptionsKit.PlayClick();
        Next();
    }

    private void UpdateArrow()
    {
        bool done = typer != null && typer.IsDone;
        arrowRt.gameObject.SetActive(done);
        if (!done) return;
        float bob = Mathf.Floor(Mathf.Sin(Time.unscaledTime * 6f) * 1.5f + 0.5f);
        arrowRt.anchoredPosition = new Vector2(BoxX + BoxW - 14, -(BoxY + BoxH - 11 + bob));
    }

    private void UpdatePortrait()
    {
        if (portraitImg == null) return;
        float now = Time.unscaledTime;
        bool talking = typer != null && typer.IsTyping;

        bool blink = false;
        if (!talking && now >= nextBlink)
        {
            blinkUntil = now + 0.12f;
            nextBlink = now + UnityEngine.Random.Range(2f, 4.5f);
        }
        if (now < blinkUntil) blink = true;

        // Hereingleiten beim Sprecherwechsel (0.18 s, ganzzahlig)
        float t = Mathf.Clamp01((now - swapAt) / 0.18f);
        float ease = 1f - (1f - t) * (1f - t);
        float slide = Mathf.Round((1f - ease) * 24f) * (left ? -1f : 1f);

        if (who == Who.Player)
        {
            // Die Spielfigur selbst (ohne Keksdosen-Hintergrund), dreifach,
            // steht mit den Fuessen auf der Oberkante der Box.
            Sprite ps = StoryArt.PlayerFrame();
            portraitImg.sprite = ps;
            portraitImg.color = ps != null ? new Color(1f, 1f, 1f, ease) : Color.clear;
            if (ps == null) return;
            float cx = left ? BoxX + PortraitSide + 48 : BoxX + BoxW - PortraitSide - 48;
            Rect r = StoryArt.PlayerRect(ps, cx + slide, BoxY + 3, 3);
            OptionsKit.Move(portraitRt, r.x, r.y, r.width, r.height);
            portraitRt.localScale = Vector3.one;
            return;
        }

        Sprite s = StoryArt.Portrait(who, mood, talking, blink);
        portraitImg.sprite = s;
        portraitImg.color = s != null ? Color.white : Color.clear;
        if (s == null) return;

        float pw = s.rect.width * PortraitScale, ph = s.rect.height * PortraitScale;
        float baseX = left ? BoxX + PortraitSide : BoxX + BoxW - PortraitSide - pw;
        float baseY = BoxY - ph + PortraitSink;

        // Nicht-Pomo-Sprecher wippen beim Reden um einen Bildpixel
        float bob = 0f;
        if (talking && who != Who.Pomo) bob = (Mathf.FloorToInt(now * 8f) % 2) * PortraitScale;

        OptionsKit.Move(portraitRt, baseX + slide, baseY - bob, pw, ph);
        portraitImg.color = new Color(1f, 1f, 1f, ease);
        portraitRt.localScale = Vector3.one;
    }
}
