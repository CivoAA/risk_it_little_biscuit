using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Line = StoryTexts.Line;
using Who = StoryTexts.Who;

/// <summary>
/// Zwischenruf im Lauf: eine kleine Karte unter dem Timer - Portrait, Name,
/// eine getippte Zeile. Haelt das Spiel NICHT an. Faehrt von oben herein,
/// bleibt ein paar Sekunden und faehrt wieder hoch. Ein neuer Ruf ersetzt
/// den alten.
///
/// Die Karte sitzt oben mittig am Bildschirmrand (nicht auf der 480x270-Seite),
/// damit sie bei jedem Seitenverhaeltnis unter dem Timer des HUD steht.
/// </summary>
public class StoryBark : MonoBehaviour
{
    private const int W = 236, H = 40, Top = 46;
    private const float Hold = 4.2f, SlideTime = 0.22f;

    private static StoryBark instance;

    private RectTransform card;
    private Image portrait;
    private DialogueText typer;
    private CanvasScaler scaler;
    private Who who;
    private float shownAt, hideAt;

    public static void Show(Line? line)
    {
        if (line == null) return;
        if (instance != null) Destroy(instance.gameObject);
        var go = new GameObject("StoryBark");
        RunScene.Place(go, "UI");
        instance = go.AddComponent<StoryBark>();
        instance.Build(line.Value);
    }

    public static void HideNow()
    {
        if (instance != null) Destroy(instance.gameObject);
        instance = null;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Build(Line line)
    {
        who = line.Who;
        TMP_FontAsset font = PixelUI.FindTextFont();

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = 90;
        scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        ApplyScale();

        GameObject c = new GameObject("Card", typeof(RectTransform));
        c.transform.SetParent(transform, false);
        card = (RectTransform)c.transform;
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 1f);
        card.sizeDelta = new Vector2(W, H);

        OptionsKit.Img("Plate", card, 0, 0, W, H, GameHudSkin.Card, true);

        // Portrait 1:1 links, ragt oben etwas heraus
        Sprite s = StoryArt.Portrait(who, line.Mood, false, false);
        if (who == Who.Player) s = StoryArt.PlayerFrame();
        portrait = OptionsKit.Img("Portrait", card, -6, -10, s != null ? s.rect.width : 1, s != null ? s.rect.height : 1, s);
        portrait.color = s != null ? Color.white : Color.clear;

        int tx = 46;
        TextMeshProUGUI name = OptionsKit.ShadowLabel("Name", card, tx, 3, W - tx - 6, 11, StoryArt.NameOf(who), font,
                                                      OptionsKit.SizeText, StoryArt.AccentOf(who), TextAlignmentOptions.Left);
        name.color = StoryArt.AccentOf(who);

        TextMeshProUGUI body = OptionsKit.Label("Text", card, tx, 14, W - tx - 8, H - 16, "", font, OptionsKit.SizeText,
                                                GameHudSkin.Cream, TextAlignmentOptions.TopLeft);
        body.textWrappingMode = TextWrappingModes.Normal;
        typer = body.gameObject.AddComponent<DialogueText>();
        typer.CharRevealed += ch => StorySounds.Voice(who, ch);
        typer.Play(line.Text);

        shownAt = Time.unscaledTime;
        hideAt = shownAt + Hold + line.Text.Length / 30f;
        Place();
    }

    private void ApplyScale()
    {
        int s = Mathf.Max(1, Mathf.RoundToInt(Mathf.Min(Screen.width / (float)OptionsKit.RefW,
                                                        Screen.height / (float)OptionsKit.RefH)));
        scaler.scaleFactor = s;
    }

    private void Place()
    {
        float now = Time.unscaledTime;
        float inT = Mathf.Clamp01((now - shownAt) / SlideTime);
        float outT = Mathf.Clamp01((now - hideAt) / SlideTime);
        float shown = (1f - (1f - inT) * (1f - inT)) * (1f - outT * outT);
        float y = Mathf.Round(Mathf.Lerp(-(H + 12), Top, shown));
        card.anchoredPosition = new Vector2(0f, -y);
    }

    private void Update()
    {
        ApplyScale();
        if (portrait != null && who == Who.Pomo)
            portrait.sprite = StoryArt.Portrait(Who.Pomo, StoryTexts.Mood.Normal, typer.IsTyping, false);
        Place();
        if (Time.unscaledTime > hideAt + SlideTime) Destroy(gameObject);
    }
}
