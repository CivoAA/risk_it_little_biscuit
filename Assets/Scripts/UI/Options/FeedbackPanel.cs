using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Feedback / Bug-Report, geoeffnet aus den Optionen. Liegt ueber dem
/// <see cref="OptionsPanel"/> und sieht aus wie dieses.
///
///   Titelband   FEEDBACK
///   Art         [BUG] [FEEDBACK]
///   Name / Discord-ID   ________________
///   Beschreibung        ________________  0/1000
///                       ________________
///   Fussleiste  ABBRECHEN   (Status)   SENDEN
///
/// Gesendet wird ueber <see cref="FeedbackReport"/> an einen Discord-Webhook.
/// Die eingegebenen Texte ueberleben das Schliessen, bis sie angekommen sind -
/// wer versehentlich ESC drueckt, verliert nichts.
/// </summary>
public class FeedbackPanel : MonoBehaviour
{
    // Deckungsgleich mit der Karte der Optionen - sonst schaut deren Rand daneben heraus.
    private const int CardX = 80, CardY = 40, CardW = 320, CardH = 208;
    private const int RibbonY = 28;
    private static readonly RectInt Content = new RectInt(CardX - 12, RibbonY, CardW + 24, CardY + CardH - RibbonY);

    private const int InX = CardX + 14, InW = CardW - 28;
    private const int IntroY = 52;
    private const int KindY = 67, KindH = 16, KindW = 82;
    private const int NameLabelY = 88, NameY = 100, NameH = 18;
    private const int MsgLabelY = 122, MsgY = 134, MsgH = 70;
    private const int InfoY = 207;
    private const int FootY = 223, FootH = 18, FootW = 80;

    private static FeedbackPanel instance;
    public static bool IsOpen => instance != null;

    // Entwurf - bleibt ueber Schliessen und Oeffnen hinweg stehen.
    private static FeedbackReport.Kind draftKind = FeedbackReport.Kind.Bug;
    private static string draftName = "", draftMessage = "";

    private TMP_FontAsset textFont, pixelFont;
    private RectTransform page;
    private UnityEngine.UI.CanvasScaler scaler;
    private Vector2Int lastScreen;

    private SkinButton bugButton, feedbackButton, sendButton, cancelButton;
    private TMP_InputField nameField, messageField;
    private TextMeshProUGUI counter, status;
    private bool sending;
    private int openedFrame;

    public static void Open()
    {
        if (instance != null) return;
        GameObject go = new GameObject("FeedbackPanel");
        instance = go.AddComponent<FeedbackPanel>();
    }

    public static void Close()
    {
        if (instance == null) return;
        instance.SaveDraft();
        Destroy(instance.gameObject);
        instance = null;
    }

    private void Awake()
    {
        instance = this;
        openedFrame = Time.frameCount;
        textFont = PixelUI.FindTextFont();
        pixelFont = PixelUI.FindPixelFont();
        Build();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Update()
    {
        var size = new Vector2Int(Screen.width, Screen.height);
        if (size != lastScreen)
        {
            lastScreen = size;
            OptionsKit.Layout(page, scaler, Content);
        }

        OptionsKit.UpdateFocus(nameField);
        OptionsKit.UpdateFocus(messageField);
        counter.text = messageField.text.Length + "/" + FeedbackReport.MaxMessage;

        if (Time.frameCount == openedFrame) return;

        if (Input.GetKeyDown(KeyCode.Escape) && !sending)
        {
            OptionsKit.PlayClick();
            Close();
            return;
        }

        // Tab springt zwischen den beiden Feldern.
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            TMP_InputField next = nameField.isFocused ? messageField : nameField;
            EventSystem.current?.SetSelectedGameObject(next.gameObject);
            next.ActivateInputField();
        }
    }

    // ==================================================================
    //  Aufbau
    // ==================================================================

    private void Build()
    {
        // Ueber den Optionen (210).
        page = OptionsKit.CreatePage(gameObject, 220, 0.6f, out scaler);

        OptionsKit.Img("Card", page, CardX, CardY, CardW, CardH, GameHudSkin.Card, true);
        OptionsKit.Ribbon(page, CardX + CardW / 2f, RibbonY, Loc.Get("ui.feedback.title", "FEEDBACK"),
                          pixelFont, textFont);

        OptionsKit.Label("Intro", page, InX, IntroY, InW, 13,
                         Loc.Get("ui.feedback.intro", "Fehler gefunden oder eine Idee? Schreib uns!"),
                         textFont, OptionsKit.SizeText, GameHudSkin.Parchment, TextAlignmentOptions.Center);

        // Art: zwei Knoepfe, mittig.
        int kx = CardX + (CardW - (KindW * 2 + 4)) / 2;
        bugButton = SkinButton.Create(page, kx, KindY, KindW, KindH,
                                      Loc.Get("ui.feedback.kind.bug", "BUG"), textFont, SkinButton.Kind.Wood,
                                      () => SetKind(FeedbackReport.Kind.Bug), GameHudSkin.Bug);
        feedbackButton = SkinButton.Create(page, kx + KindW + 4, KindY, KindW, KindH,
                                           Loc.Get("ui.feedback.kind.feedback", "FEEDBACK"), textFont,
                                           SkinButton.Kind.Wood, () => SetKind(FeedbackReport.Kind.Feedback),
                                           GameHudSkin.Speech);

        Caption(NameLabelY, Loc.Get("ui.feedback.name", "Name / Discord-ID"));
        nameField = OptionsKit.Input(page, InX, NameY, InW, NameH, textFont,
                                     Loc.Get("ui.feedback.name.placeholder", "optional - damit wir nachfragen können"),
                                     false, FeedbackReport.MaxName);
        nameField.text = draftName;

        Caption(MsgLabelY, Loc.Get("ui.feedback.message", "Beschreibung"));
        counter = OptionsKit.Label("Counter", page, InX, MsgLabelY, InW, 13, "", textFont, OptionsKit.SizeText,
                                   GameHudSkin.Stone, TextAlignmentOptions.Right);
        messageField = OptionsKit.Input(page, InX, MsgY, InW, MsgH, textFont, "", true, FeedbackReport.MaxMessage);
        messageField.text = draftMessage;

        // Hinweiszeile - wird nach einem Sendeversuch zur Statuszeile.
        status = OptionsKit.Label("Status", page, InX, InfoY, InW, 13,
                                  Loc.Get("ui.feedback.info", "Mitgesendet: Spielversion, System, Hardware und Szene."),
                                  textFont, OptionsKit.SizeText, GameHudSkin.Stone, TextAlignmentOptions.Center);

        cancelButton = SkinButton.Create(page, InX, FootY, FootW, FootH,
                                         Loc.Get("ui.feedback.cancel", "ABBRECHEN"), textFont,
                                         SkinButton.Kind.Wood, Close);
        sendButton = SkinButton.Create(page, InX + InW - FootW, FootY, FootW, FootH,
                                       Loc.Get("ui.feedback.send", "SENDEN"), textFont,
                                       SkinButton.Kind.Primary, Send);

        OptionsKit.Layout(page, scaler, Content);
        lastScreen = new Vector2Int(Screen.width, Screen.height);

        SetKind(draftKind);

        // Gleich losschreiben koennen.
        TMP_InputField first = string.IsNullOrEmpty(draftName) ? nameField : messageField;
        EventSystem.current?.SetSelectedGameObject(first.gameObject);
        first.ActivateInputField();
    }

    private void Caption(int y, string text)
    {
        OptionsKit.Label("Caption", page, InX + 1, y, InW, 13, text, textFont, OptionsKit.SizeText,
                         GameHudSkin.ParchDark, TextAlignmentOptions.Left);
    }

    private void SetKind(FeedbackReport.Kind kind)
    {
        draftKind = kind;
        bugButton.Active = kind == FeedbackReport.Kind.Bug;
        feedbackButton.Active = kind == FeedbackReport.Kind.Feedback;

        var hint = (TextMeshProUGUI)messageField.placeholder;
        hint.text = kind == FeedbackReport.Kind.Bug
            ? Loc.Get("ui.feedback.message.bug", "Was ist passiert? Was hast du gerade gemacht?")
            : Loc.Get("ui.feedback.message.feedback", "Was gefällt dir, was fehlt dir?");
    }

    private void SaveDraft()
    {
        if (nameField != null) draftName = nameField.text;
        if (messageField != null) draftMessage = messageField.text;
    }

    // ==================================================================
    //  Senden
    // ==================================================================

    private void Send()
    {
        if (sending) return;

        if (messageField.text.Trim().Length < FeedbackReport.MinMessage)
        {
            SetStatus(Loc.Get("ui.feedback.status.short", "Bitte etwas genauer beschreiben."), GameHudSkin.JamLight);
            return;
        }
        if (FeedbackReport.CoolingDown)
        {
            SetStatus(Loc.Get("ui.feedback.status.wait", "Bitte kurz warten."), GameHudSkin.JamLight);
            return;
        }
        if (FeedbackReport.WebhookUrl == null)
        {
            SetStatus(Loc.Get("ui.feedback.status.nourl", "Kein Webhook eingerichtet."), GameHudSkin.JamLight);
            return;
        }

        StartCoroutine(SendRoutine());
    }

    private IEnumerator SendRoutine()
    {
        sending = true;
        sendButton.Disabled = cancelButton.Disabled = true;
        nameField.interactable = messageField.interactable = false;
        SetStatus(Loc.Get("ui.feedback.status.sending", "Wird gesendet..."), GameHudSkin.Parchment);

        bool ok = false;
        yield return FeedbackReport.Send(draftKind, nameField.text, messageField.text, r => ok = r);

        sending = false;
        sendButton.Disabled = cancelButton.Disabled = false;
        nameField.interactable = messageField.interactable = true;

        if (!ok)
        {
            SetStatus(Loc.Get("ui.feedback.status.failed", "Senden fehlgeschlagen."), GameHudSkin.JamLight);
            yield break;
        }

        // Angekommen: Text leeren (der Name bleibt fuer das naechste Mal).
        SetStatus(Loc.Get("ui.feedback.status.sent", "Danke! Ist angekommen."), GameHudSkin.Mint);
        messageField.text = "";
        draftMessage = "";

        yield return new WaitForSecondsRealtime(1.4f);
        Close();
    }

    private void SetStatus(string text, Color color)
    {
        status.text = text;
        status.color = color;
    }
}
