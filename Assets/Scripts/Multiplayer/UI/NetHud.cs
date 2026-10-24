using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Was das HUD im Koop zusaetzlich zeigt. Das normale HUD (<see cref="GameHud"/>)
/// bleibt wie allein - das hier liegt daneben:
///
///  * links unter den Slots eine Karte je Mitspieler: Portraet, Name in seiner
///    Farbe, Abstand, Lebensleiste mit Schadensrest und Schild. Am Boden pocht
///    sie rot und zeigt die Ausblutzeit, beim Waehlen ein Buch, raus = grau
///    (am Kreuz mit Fortschritt)
///  * ueber jeder Figur eines Mitspielers ein Namensschild mit Mini-Leiste;
///    liegt er am Boden, ein Punktekreis in Rettungsweite, der sich beim
///    Retten fuellt, und ein pochendes "RETTEN!"
///  * am Bildrand ein runder Portraet-Knopf mit Pfeil und Abstand fuer jeden,
///    der nicht im Bild ist (am Boden: rot blinkend mit Totenkopf)
///  * selbst am Boden: rote Vignette, Band "NIEDERGEGANGEN!", Ausblut-Leiste,
///    eigener Rettungskreis. Raus: Zuschauer-Schild unten
///  * Sprechblasen der Emotes (<see cref="NetEmotes"/>), Meldungen als
///    Plaettchen, die von oben hereinfallen, "NICK WAEHLT ..." mit Buch
///
/// Gerechnet wird wie im GameHud in einem 480x270-Raster mit ganzzahliger
/// Skalierung, damit jeder Pixel gleich dick ist.
/// </summary>
[DefaultExecutionOrder(950)]
public class NetHud : MonoBehaviour
{
    private const int RefW = 480, RefH = 270;
    private const int CardX = 4, CardY = 98, CardW = 118, CardH = 28, CardStep = 31;
    private const int Win = 22;
    private const int BarX = 30, BarY = 16, BarW = 84, BarH = 8;
    private const int RingDots = 24;
    private const float SizeText = 10f;
    private const float ToastTime = 3.4f;
    private const float HintTime = 25f;

    // ==================================================================
    //  Bausteine
    // ==================================================================

    /// <summary>Text mit 1-px-Kontur in Ink (vier versetzte Kopien darunter).</summary>
    private class Label
    {
        public TextMeshProUGUI Main;
        public readonly List<TextMeshProUGUI> Backs = new List<TextMeshProUGUI>();
        private string text;

        public void Set(string value, TMP_FontAsset pixel, TMP_FontAsset fallback)
        {
            if (value == text) return;
            text = value;
            TMP_FontAsset font = OptionsKit.PickFont(value, pixel, fallback);
            Main.font = font;
            Main.text = value;
            foreach (var b in Backs) { b.font = font; b.text = value; }
        }

        public float Width
        {
            get
            {
                Main.ForceMeshUpdate();
                return Main.preferredWidth;
            }
        }

        public void SetColor(Color c)
        {
            c.a = Main.color.a;
            if (Main.color != c) Main.color = c;
        }

        public void SetActive(bool on)
        {
            if (Main.gameObject.activeSelf == on) return;
            Main.gameObject.SetActive(on);
            foreach (var b in Backs) b.gameObject.SetActive(on);
        }

        public void SetAlpha(float a)
        {
            Color c = Main.color;
            if (Mathf.Approximately(c.a, a)) return;
            c.a = a;
            Main.color = c;
            foreach (var b in Backs)
            {
                Color bc = b.color;
                bc.a = a;
                b.color = bc;
            }
        }
    }

    private class Card
    {
        public RectTransform Root;
        public CanvasGroup Group;
        public Image Back, Stripe, Glow, Portrait, BarFrame, Trail, Fill, Shield, Revive, Icon;
        public Label Name, Right;
        public int Slot = -1;
        public float Shown = 1f, Trail01 = 1f, TrailHoldUntil, FlashUntil, ShakeUntil, LastHp = -1f;
    }

    private class Plate
    {
        public RectTransform Root;
        public Label Name, Help;
        public Image BarBack, BarFill, HelpIcon;
        public readonly Image[] Dots = new Image[RingDots];
        public RectTransform DotRoot;
    }

    private class Pointer
    {
        public RectTransform Root;
        public Image Ring, Portrait, Arrow, Skull;
        public RectTransform Mask;
        public Label Dist;
        /// <summary>Mitte des Knopfs in HUD-Pixeln (fuer Emotes am Rand).</summary>
        public Vector2 At;
    }

    private class Bubble
    {
        public RectTransform Root;
        public Image Back, Tail, Icon;
        public Label Text;
        public NetEmotes.Kind Kind = (NetEmotes.Kind)255;
    }

    private class ToastView
    {
        public RectTransform Root;
        public Image Back, Cap;
        public Label Text;
    }

    private struct ToastEntry
    {
        public string Text;
        public Color Color;
        public float Born, Until;
    }

    private Canvas canvas, topCanvas;
    private CanvasScaler scaler, topScaler;
    private RectTransform root, topRoot;
    private TMP_FontAsset pixelFont, textFont;
    private Vector2Int lastScreen;
    private int scale = 1;
    private int hudW = RefW, hudH = RefH;

    private NetRun run;
    private readonly Card[] cards = new Card[NetSession.MaxPlayers];
    private readonly Plate[] plates = new Plate[NetSession.MaxPlayers];
    private readonly Pointer[] pointers = new Pointer[NetSession.MaxPlayers];
    private readonly Bubble[] bubbles = new Bubble[NetSession.MaxPlayers];
    private Plate selfRing;

    // Selbst am Boden / raus
    private Image vignette;
    private RectTransform downRoot, downRibbon, downTailL, downTailR, downBleedFrame;
    private Label downTitle, downSub;
    private Image downBleed, downRevive;
    private RectTransform outRoot;
    private Image outBack;
    private Label outTitle, outSub;

    // Mitte
    private RectTransform chooseRoot;
    private Image chooseBack, chooseIcon;
    private Label chooseText;
    private Label center, centerSub, runningHint, emoteHint;
    private readonly ToastView[] toastViews = new ToastView[4];
    private readonly List<ToastEntry> toasts = new List<ToastEntry>();
    private readonly List<string> choosing = new List<string>();
    private float runStarted = -1f;

    // ==================================================================
    //  Aufbau
    // ==================================================================

    private void Awake()
    {
        run = GetComponent<NetRun>();
        pixelFont = PixelUI.FindPixelFont();
        textFont = PixelUI.FindTextFont();
        if (pixelFont == null) pixelFont = textFont;

        var go = new GameObject("NetHud");
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = 95;
        scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.referencePixelsPerUnit = 100f;
        root = (RectTransform)go.transform;

        // Eigene Ebene ueber dem Pausenmenue (200) fuer den Hinweis, dass es weiterlaeuft.
        var top = new GameObject("NetHudTop");
        top.transform.SetParent(transform, false);
        topCanvas = top.AddComponent<Canvas>();
        topCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        topCanvas.pixelPerfect = true;
        topCanvas.sortingOrder = 230;
        topScaler = top.AddComponent<CanvasScaler>();
        topScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        topScaler.referencePixelsPerUnit = 100f;
        topRoot = (RectTransform)top.transform;

        // Ganz unten die Vignette - alles andere liegt darueber.
        vignette = OptionsKit.Stretch("Vignette", root, GameHudSkin.Vignette(96, 54), new Color(0f, 0f, 0f, 0f));

        for (int i = 0; i < plates.Length; i++) plates[i] = BuildPlate(i);
        selfRing = BuildPlate(-1);
        for (int i = 0; i < bubbles.Length; i++) bubbles[i] = BuildBubble(i);
        for (int i = 0; i < pointers.Length; i++) pointers[i] = BuildPointer(i);
        for (int i = 0; i < cards.Length; i++) cards[i] = BuildCard(i);

        BuildDown();
        BuildOut();
        BuildChoose();

        center = NewLabel("Center", root, new RectInt(0, 112, RefW, 14), GameHudSkin.Cream, TextAlignmentOptions.Center);
        centerSub = NewLabel("CenterSub", root, new RectInt(0, 127, RefW, 14), GameHudSkin.Parchment, TextAlignmentOptions.Center);
        for (int i = 0; i < toastViews.Length; i++) toastViews[i] = BuildToast(i);
        emoteHint = NewLabel("EmoteHint", root, new RectInt(CardX, 0, 160, 12), GameHudSkin.StoneLight, TextAlignmentOptions.Left);
        runningHint = NewLabel("Running", topRoot, new RectInt(0, 6, RefW, 14), GameHudSkin.GoldLight,
                               TextAlignmentOptions.Center);

        NetRun.Toast += OnToast;
        NetEmotes.Shown += OnEmote;
        ApplyLayout(true);
    }

    private void OnDestroy()
    {
        NetRun.Toast -= OnToast;
        NetEmotes.Shown -= OnEmote;
    }

    private void OnToast(string text, Color color)
    {
        float now = Time.unscaledTime;
        toasts.Add(new ToastEntry { Text = text, Color = color, Born = now, Until = now + ToastTime });
        while (toasts.Count > toastViews.Length) toasts.RemoveAt(0);
    }

    private void OnEmote(int slot, NetEmotes.Kind kind)
    {
        // Leises Ploppen, damit man es auch mitten im Getuemmel merkt.
        OptionsKit.PlayClick();
    }

    private Label NewLabel(string name, RectTransform parent, RectInt px, Color color, TextAlignmentOptions align)
    {
        var label = new Label();
        Vector2Int[] offsets = { new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(0, 1) };
        foreach (Vector2Int o in offsets)
        {
            TextMeshProUGUI back = OptionsKit.Label(name + "Back", parent, px.x + o.x, px.y + o.y, px.width, px.height, "",
                                                    pixelFont, SizeText, GameHudSkin.Ink, align);
            label.Backs.Add(back);
        }
        label.Main = OptionsKit.Label(name, parent, px.x, px.y, px.width, px.height, "", pixelFont, SizeText, color, align);
        return label;
    }

    private Card BuildCard(int i)
    {
        var c = new Card();
        c.Root = OptionsKit.Rect("Mate" + i, root, CardX, CardY + i * CardStep, CardW, CardH);
        c.Group = c.Root.gameObject.AddComponent<CanvasGroup>();
        c.Group.blocksRaycasts = false;
        c.Group.interactable = false;
        c.Glow = OptionsKit.Img("Glow", c.Root, -1, -1, CardW + 2, CardH + 2, GameHudSkin.Plate, new Color(1f, 1f, 1f, 0f), true);
        c.Back = OptionsKit.Img("Back", c.Root, 0, 0, CardW, CardH, GameHudSkin.Plate, true);
        c.Stripe = OptionsKit.Img("Stripe", c.Root, 2, 3, 2, CardH - 6, GameHudSkin.White, Color.white);

        OptionsKit.Img("WinBack", c.Root, 5, 3, Win, Win, GameHudSkin.SlotBack(Win, Win));
        RectTransform window = OptionsKit.Rect("Window", c.Root, 5, 3, Win, Win);
        window.gameObject.AddComponent<RectMask2D>();
        c.Portrait = OptionsKit.Img("Portrait", window, -10, -10, 42, 42, null);

        c.Name = NewLabel("Name", c.Root, new RectInt(BarX, 2, 58, 12), GameHudSkin.Cream, TextAlignmentOptions.Left);
        c.Right = NewLabel("Right", c.Root, new RectInt(BarX + 30, 2, BarW - 30, 12), GameHudSkin.StoneLight,
                           TextAlignmentOptions.Right);
        c.Icon = OptionsKit.Img("Icon", c.Root, BarX + 46, 3, 9, 9, null);

        c.BarFrame = OptionsKit.Img("Bar", c.Root, BarX, BarY, BarW, BarH, GameHudSkin.BarFrame, true);
        c.Trail = OptionsKit.Img("Trail", c.Root, BarX + 1, BarY + 1, BarW - 2, BarH - 2, GameHudSkin.White, GameHudSkin.Rose);
        c.Fill = OptionsKit.Img("Fill", c.Root, BarX + 1, BarY + 1, BarW - 2, BarH - 2, GameHudSkin.White, GameHudSkin.Jam);
        c.Revive = OptionsKit.Img("Revive", c.Root, BarX + 1, BarY + 1, 0, BarH - 2, GameHudSkin.White, GameHudSkin.Mint);
        c.Shield = OptionsKit.Img("Shield", c.Root, BarX + 1, BarY + 1, 0, 2, GameHudSkin.White, GameHudSkin.IcingLight);
        c.Root.gameObject.SetActive(false);
        return c;
    }

    private Plate BuildPlate(int i)
    {
        var p = new Plate();
        p.DotRoot = OptionsKit.Rect("Ring" + i, root, 0, 0, 1, 1);
        for (int d = 0; d < RingDots; d++)
            p.Dots[d] = OptionsKit.Img("Dot" + d, p.DotRoot, 0, 0, 3, 3, GameHudSkin.Disc(3), GameHudSkin.Cream);
        p.DotRoot.gameObject.SetActive(false);

        p.Root = OptionsKit.Rect("Plate" + i, root, 0, 0, 80, 40);
        p.Help = NewLabel("Help", p.Root, new RectInt(0, 0, 80, 12), GameHudSkin.Mint, TextAlignmentOptions.Center);
        p.HelpIcon = OptionsKit.Img("HelpIcon", p.Root, 0, 2, 9, 8, GameHudSkin.ReviveHeart);
        p.Name = NewLabel("Name", p.Root, new RectInt(0, 13, 80, 12), GameHudSkin.Cream, TextAlignmentOptions.Center);
        p.BarBack = OptionsKit.Img("BarBack", p.Root, 27, 25, 26, 4, GameHudSkin.White, GameHudSkin.Ink);
        p.BarFill = OptionsKit.Img("BarFill", p.Root, 28, 26, 24, 2, GameHudSkin.White, GameHudSkin.Jam);
        p.Root.gameObject.SetActive(false);
        return p;
    }

    private Pointer BuildPointer(int i)
    {
        var p = new Pointer();
        p.Root = OptionsKit.Rect("Pointer" + i, root, 0, 0, 20, 30);
        p.Ring = OptionsKit.Img("Ring", p.Root, 0, 0, 20, 20, GameHudSkin.Disc(20));
        OptionsKit.Img("RingEdge", p.Root, 0, 0, 20, 20, GameHudSkin.DiscOutline(20));
        p.Mask = OptionsKit.Rect("Mask", p.Root, 3, 3, 14, 14);
        Image maskImg = p.Mask.gameObject.AddComponent<Image>();
        maskImg.sprite = GameHudSkin.Disc(14);
        maskImg.raycastTarget = false;
        p.Mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        maskImg.color = GameHudSkin.Night;
        p.Portrait = OptionsKit.Img("Portrait", p.Mask, -14, -14, 42, 42, null);
        p.Skull = OptionsKit.Img("Skull", p.Root, 5, 5, 9, 9, GameHudSkin.Skull);
        p.Arrow = OptionsKit.Img("Arrow", p.Root, 0, 0, 9, 9, GameHudSkin.Arrow);
        p.Dist = NewLabel("Dist", p.Root, new RectInt(-10, 20, 40, 12), GameHudSkin.Cream, TextAlignmentOptions.Center);
        p.Root.gameObject.SetActive(false);
        return p;
    }

    private Bubble BuildBubble(int i)
    {
        var b = new Bubble();
        b.Root = OptionsKit.Rect("Emote" + i, root, 0, 0, 40, 18);
        b.Back = OptionsKit.Img("Back", b.Root, 0, 0, 40, 14, GameHudSkin.EmoteBubble, true);
        b.Tail = OptionsKit.Img("Tail", b.Root, 18, 13, 5, 4, GameHudSkin.EmoteTail);
        b.Icon = OptionsKit.Img("Icon", b.Root, 3, 3, 9, 8, null);
        b.Text = NewLabel("Text", b.Root, new RectInt(13, 1, 40, 12), GameHudSkin.Ink, TextAlignmentOptions.Left);
        // Dunkle Schrift auf Creme: ohne Ink-Kontur, die wuerde sie zukleistern.
        foreach (var back in b.Text.Backs) back.gameObject.SetActive(false);
        b.Text.Backs.Clear();
        b.Root.gameObject.SetActive(false);
        return b;
    }

    private void BuildDown()
    {
        downRoot = OptionsKit.Rect("Down", root, 0, 168, RefW, 46);
        downTailL = OptionsKit.Img("TailL", downRoot, 0, 4, 12, 16, GameHudSkin.RibbonTail(true)).rectTransform;
        downTailR = OptionsKit.Img("TailR", downRoot, 0, 4, 12, 16, GameHudSkin.RibbonTail(false)).rectTransform;
        downRibbon = OptionsKit.Img("Ribbon", downRoot, 0, 0, 150, 18, GameHudSkin.Ribbon, true).rectTransform;
        downTitle = NewLabel("Title", downRibbon, new RectInt(0, 2, 150, 14), GameHudSkin.Cream, TextAlignmentOptions.Center);
        downBleedFrame = OptionsKit.Img("BleedFrame", downRoot, (RefW - 124) / 2, 22, 124, 8, GameHudSkin.BarFrame, true).rectTransform;
        downBleed = OptionsKit.Img("Bleed", downRoot, (RefW - 124) / 2 + 1, 23, 122, 6, GameHudSkin.White, GameHudSkin.Jam);
        downRevive = OptionsKit.Img("Revive", downRoot, (RefW - 124) / 2 + 1, 23, 0, 6, GameHudSkin.White, GameHudSkin.Mint);
        downSub = NewLabel("Sub", downRoot, new RectInt(0, 32, RefW, 12), GameHudSkin.Parchment, TextAlignmentOptions.Center);
        downRoot.gameObject.SetActive(false);
    }

    private void BuildOut()
    {
        outRoot = OptionsKit.Rect("Out", root, 0, 206, RefW, 30);
        outBack = OptionsKit.Img("Back", outRoot, (RefW - 200) / 2, 0, 200, 28, GameHudSkin.Plate, true);
        outTitle = NewLabel("Title", outRoot, new RectInt(0, 2, RefW, 12), GameHudSkin.Cream, TextAlignmentOptions.Center);
        outSub = NewLabel("Sub", outRoot, new RectInt(0, 14, RefW, 12), GameHudSkin.StoneLight, TextAlignmentOptions.Center);
        outRoot.gameObject.SetActive(false);
    }

    private void BuildChoose()
    {
        chooseRoot = OptionsKit.Rect("Choose", root, 0, 48, 120, 15);
        chooseBack = OptionsKit.Img("Back", chooseRoot, 0, 0, 120, 15, GameHudSkin.Plate, true);
        chooseIcon = OptionsKit.Img("Book", chooseRoot, 4, 3, 11, 8, GameHudSkin.Book);
        chooseText = NewLabel("Text", chooseRoot, new RectInt(18, 1, 200, 12), GameHudSkin.GoldLight, TextAlignmentOptions.Left);
        chooseRoot.gameObject.SetActive(false);
    }

    private ToastView BuildToast(int i)
    {
        var t = new ToastView();
        t.Root = OptionsKit.Rect("Toast" + i, root, 0, 0, 160, 14);
        t.Back = OptionsKit.Img("Back", t.Root, 0, 0, 160, 14, GameHudSkin.Plate, true);
        t.Cap = OptionsKit.Img("Cap", t.Root, 2, 2, 2, 10, GameHudSkin.White, Color.white);
        t.Text = NewLabel("Text", t.Root, new RectInt(7, 1, 300, 12), GameHudSkin.Cream, TextAlignmentOptions.Left);
        t.Root.gameObject.SetActive(false);
        return t;
    }

    private void ApplyLayout(bool force)
    {
        var size = new Vector2Int(Screen.width, Screen.height);
        if (!force && size == lastScreen) return;
        lastScreen = size;
        scale = Mathf.Max(1, Mathf.RoundToInt(Mathf.Min(size.x / (float)RefW, size.y / (float)RefH)));
        scaler.scaleFactor = scale;
        topScaler.scaleFactor = scale;
        hudW = size.x / scale;
        hudH = size.y / scale;

        // Mitte-Texte ueber die ganze (abgerundete) HUD-Breite.
        foreach (Label l in new[] { center, centerSub, runningHint, downSub, outTitle, outSub })
            Stretch(l, hudW);
        if (downRoot != null) downRoot.sizeDelta = new Vector2(hudW, downRoot.sizeDelta.y);
        if (outRoot != null) outRoot.sizeDelta = new Vector2(hudW, outRoot.sizeDelta.y);
    }

    private static void Stretch(Label l, int w)
    {
        if (l == null) return;
        l.Main.rectTransform.sizeDelta = new Vector2(w, l.Main.rectTransform.sizeDelta.y);
        foreach (var b in l.Backs) b.rectTransform.sizeDelta = new Vector2(w, b.rectTransform.sizeDelta.y);
    }

    private static void Pos(RectTransform rt, float x, float y)
    {
        var p = new Vector2(Mathf.Round(x), -Mathf.Round(y));
        if (rt.anchoredPosition != p) rt.anchoredPosition = p;
    }

    private static void Size(RectTransform rt, float w, float h)
    {
        var s = new Vector2(Mathf.Round(w), Mathf.Round(h));
        if (rt.sizeDelta != s) rt.sizeDelta = s;
    }

    /// <summary>Text samt Kontur-Kopien verschieben (die Kopien liegen 1 px daneben, Reihenfolge wie in NewLabel).</summary>
    private static readonly Vector2[] BackOffsets = { new Vector2(-1, 0), new Vector2(1, 0), new Vector2(0, -1), new Vector2(0, 1) };

    private static void MoveLabel(Label l, float x, float y)
    {
        Pos(l.Main.rectTransform, x, y);
        for (int i = 0; i < l.Backs.Count && i < BackOffsets.Length; i++)
            Pos(l.Backs[i].rectTransform, x + BackOffsets[i].x, y + BackOffsets[i].y);
    }

    private static void Show(Component c, bool on)
    {
        if (c.gameObject.activeSelf != on) c.gameObject.SetActive(on);
    }

    private static void Tint(Graphic g, Color c)
    {
        if (g.color != c) g.color = c;
    }

    // ==================================================================
    //  Takt
    // ==================================================================

    private void LateUpdate()
    {
        if (run == null) return;
        ApplyLayout(false);
        if (runStarted < 0f && run.CurrentPhase == NetRun.Phase.Running) runStarted = Time.unscaledTime;

        NetSession s = NetSession.Instance;
        Camera cam = Camera.main;
        float now = Time.unscaledTime;

        int rows = UpdateCards(s, now);
        UpdatePlates(s, cam, now);
        UpdateSelf(cam, now);
        UpdateBubbles(s, cam, now);
        UpdateCenter(now);
        UpdateToasts(now);
        UpdateHint(rows, now);

        bool pauseOpen = PauseMenuPanel.IsOpen && run.CurrentPhase == NetRun.Phase.Running;
        runningHint.SetActive(pauseOpen);
        if (pauseOpen)
            runningHint.Set(NetText.Get("net.hud.still_running", "KOOP: DAS SPIEL LÄUFT WEITER!"), pixelFont, textFont);

        // Waehrend man selbst waehlt oder die Abrechnung offen ist, gehoert der Bildschirm ihnen.
        canvas.enabled = !LocalChoosing();
    }

    // ---------- Karten ----------

    private int UpdateCards(NetSession s, float now)
    {
        int row = 0;
        PlayerController pc = PlayerController.Instance;
        Vector2 me = pc != null ? (Vector2)pc.transform.position : Vector2.zero;
        if (s == null)
        {
            foreach (Card c in cards) Show(c.Root, false);
            return 0;
        }

        for (int slot = 0; slot < run.Peers.Length; slot++)
        {
            NetRun.Peer p = run.Peers[slot];
            if (!p.Playing || slot == s.LocalSlot) continue;
            UpdateCard(cards[row], p, row, me, now);
            row++;
        }
        for (int i = row; i < cards.Length; i++) Show(cards[i].Root, false);
        return row;
    }

    private void UpdateCard(Card c, NetRun.Peer p, int row, Vector2 me, float now)
    {
        Show(c.Root, true);
        if (c.Slot != p.Slot)
        {
            c.Slot = p.Slot;
            c.LastHp = -1f;
        }

        Color accent = CharacterLooks.AccentFor(p.Character);
        Tint(c.Stripe, accent);

        Sprite[] portrait = CharacterLooks.PortraitFor(p.Character);
        if (portrait != null && c.Portrait.sprite != portrait[0])
        {
            Sprite sp = portrait[0];
            c.Portrait.sprite = sp;
            Size(c.Portrait.rectTransform, sp.rect.width, sp.rect.height);
            Pos(c.Portrait.rectTransform, -Mathf.Round((sp.rect.width - Win) / 2f), -Mathf.Round((sp.rect.height - Win) / 2f));
        }

        c.Name.Set(p.Name.ToUpperInvariant(), pixelFont, textFont);
        c.Name.SetColor(accent);

        float hp = p.MaxHealth > 0f ? Mathf.Clamp01(p.Health / p.MaxHealth) : 0f;
        bool alive = p.State == NetRun.PeerState.Alive;

        // Treffer: Karte zuckt, Leiste blitzt, der Rest bleibt kurz rosa stehen.
        if (alive && c.LastHp >= 0f && p.Health < c.LastHp - 0.01f)
        {
            c.FlashUntil = now + 0.12f;
            c.ShakeUntil = now + 0.22f;
            c.TrailHoldUntil = now + 0.45f;
        }
        c.LastHp = p.Health;
        c.Shown = hp;
        if (c.Trail01 < hp) c.Trail01 = hp;
        else if (now > c.TrailHoldUntil) c.Trail01 = Mathf.MoveTowards(c.Trail01, hp, Time.unscaledDeltaTime * 0.8f);

        int shake = now < c.ShakeUntil ? (Mathf.FloorToInt(now * 40f) % 2 == 0 ? 1 : -1) : 0;
        Pos(c.Root, CardX + shake, CardY + row * CardStep);

        string right = "";
        Color rightColor = GameHudSkin.StoneLight;
        Sprite icon = null;
        Color back = Color.white;
        Color glow = new Color(1f, 1f, 1f, 0f);
        float alpha = 1f;
        float fill = hp, trail = c.Trail01, revive = 0f;
        Color fillColor = now < c.FlashUntil ? (Color)GameHudSkin.Cream : (Color)GameHudSkin.Jam;
        bool portraitGrey = false;

        switch (p.State)
        {
            case NetRun.PeerState.Downed:
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(now * 8f);
                right = Clock(p.Bleed);
                rightColor = Color.Lerp(GameHudSkin.JamLight, GameHudSkin.Cream, pulse * 0.6f);
                icon = GameHudSkin.Skull;
                back = Color.Lerp(Color.white, new Color(1f, 0.45f, 0.5f, 1f), pulse);
                glow = OptionsKit.WithAlpha(GameHudSkin.Jam, 0.35f + 0.45f * pulse);
                fill = Mathf.Clamp01(p.Bleed / NetBalance.BleedOutSeconds);
                trail = 0f;
                fillColor = GameHudSkin.JamDark;
                revive = p.Revive;
                portraitGrey = true;
                break;
            }
            case NetRun.PeerState.Out:
            {
                portraitGrey = true;
                alpha = 0.75f;
                fill = trail = 0f;
                if (NetRevive.Active != null && NetRevive.Active.TryGetAltar(p.Slot, out float progress, out int inside))
                {
                    right = NetText.Get("net.hud.altar", "GLAS") + " " + Mathf.FloorToInt(progress * 100f) + "%";
                    rightColor = GameHudSkin.IcingLight;
                    revive = progress;
                    icon = GameHudSkin.Ghost;
                    if (inside > 0) glow = OptionsKit.WithAlpha(GameHudSkin.Icing, 0.3f + 0.3f * Mathf.Sin(now * 6f));
                }
                else
                {
                    right = NetText.Get("net.hud.out", "RAUS");
                    icon = GameHudSkin.Ghost;
                }
                break;
            }
            case NetRun.PeerState.Left:
                right = NetText.Get("net.hud.left", "WEG");
                alpha = 0.5f;
                fill = trail = 0f;
                portraitGrey = true;
                break;
            default:
                if (p.Blocking)
                {
                    int dots = 1 + Mathf.FloorToInt(now * 3f) % 3;
                    right = NetText.Get("net.hud.choosing_short", "WÄHLT") + new string('.', dots);
                    rightColor = GameHudSkin.Gold;
                    icon = GameHudSkin.Book;
                    glow = OptionsKit.WithAlpha(GameHudSkin.Gold, 0.25f + 0.2f * Mathf.Sin(now * 5f));
                }
                else if (p.LastState >= 0f)
                {
                    right = Mathf.RoundToInt((p.Position - me).magnitude) + "M";
                }
                break;
        }

        c.Group.alpha = alpha;
        Tint(c.Back, back);
        Tint(c.Glow, glow);
        Tint(c.Portrait, portraitGrey ? new Color(0.45f, 0.45f, 0.55f, 1f) : Color.white);
        c.Right.Set(right, pixelFont, textFont);
        c.Right.SetColor(rightColor);
        c.Right.SetActive(right.Length > 0);

        // Symbol links vom rechten Text, leicht wippend.
        Show(c.Icon, icon != null);
        if (icon != null)
        {
            if (c.Icon.sprite != icon)
            {
                c.Icon.sprite = icon;
                Size(c.Icon.rectTransform, icon.rect.width, icon.rect.height);
            }
            float tw = Mathf.Min(c.Right.Width, BarW - 30);
            int bob = Mathf.FloorToInt(now * 4f) % 2;
            Pos(c.Icon.rectTransform, BarX + BarW - tw - icon.rect.width - 2, 3 + bob);
        }

        const int inner = BarW - 2;
        Size(c.Trail.rectTransform, Mathf.RoundToInt(inner * Mathf.Max(trail, fill)), BarH - 2);
        Size(c.Fill.rectTransform, Mathf.RoundToInt(inner * fill), BarH - 2);
        Tint(c.Fill, fillColor);
        Size(c.Revive.rectTransform, Mathf.RoundToInt(inner * Mathf.Clamp01(revive)), BarH - 2);
        Tint(c.Revive, p.State == NetRun.PeerState.Out ? (Color)GameHudSkin.Icing : (Color)GameHudSkin.Mint);
        float shield = alive && p.MaxHealth > 0f ? Mathf.Clamp01(p.Shield / p.MaxHealth) : 0f;
        Size(c.Shield.rectTransform, Mathf.RoundToInt(inner * shield), 2);
    }

    private static string Clock(float seconds)
    {
        int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return (s / 60) + ":" + (s % 60).ToString("00");
    }

    // ---------- Namensschilder, Rettungskreise, Randzeiger ----------

    private Vector2 ToHud(Camera cam, Vector3 world, out bool front)
    {
        Vector3 sp = cam.WorldToScreenPoint(world);
        front = sp.z > 0f;
        return new Vector2(sp.x / scale, hudH - sp.y / scale);
    }

    private void UpdatePlates(NetSession s, Camera cam, float now)
    {
        PlayerController pc = PlayerController.Instance;
        Vector2 me = pc != null ? (Vector2)pc.transform.position : Vector2.zero;

        for (int slot = 0; slot < plates.Length; slot++)
        {
            Plate plate = plates[slot];
            Pointer ptr = pointers[slot];
            NetAvatar a = s != null && slot != s.LocalSlot ? NetPlayers.AvatarOf(slot) : null;
            NetRun.Peer p = run.Peers[slot];
            bool show = a != null && cam != null && p.State != NetRun.PeerState.Out && p.State != NetRun.PeerState.Left;
            if (!show)
            {
                Show(plate.Root, false);
                Show(plate.DotRoot, false);
                Show(ptr.Root, false);
                continue;
            }

            Color accent = CharacterLooks.AccentFor(p.Character);
            Vector2 px = ToHud(cam, a.transform.position + Vector3.up * 1.15f, out bool front);
            bool onScreen = front && px.x > 6 && px.x < hudW - 6 && px.y > 10 && px.y < hudH - 6;
            bool downed = p.State == NetRun.PeerState.Downed;

            Show(plate.Root, onScreen);
            Show(ptr.Root, !onScreen);
            Show(plate.DotRoot, downed && onScreen);

            if (onScreen)
            {
                Pos(plate.Root, px.x - 40f, px.y - 26f);
                plate.Name.Set(p.Name.ToUpperInvariant(), pixelFont, textFont);
                plate.Name.SetColor(accent);

                bool alive = p.State == NetRun.PeerState.Alive;
                float hp = p.MaxHealth > 0f ? Mathf.Clamp01(p.Health / p.MaxHealth) : 0f;
                Show(plate.BarBack, alive);
                Show(plate.BarFill, alive);
                if (alive)
                {
                    Size(plate.BarFill.rectTransform, Mathf.RoundToInt(24 * hp), 2);
                    Tint(plate.BarFill, hp < 0.3f && Mathf.Repeat(now * 4f, 1f) < 0.5f ? (Color)GameHudSkin.JamLight
                                                                                          : (Color)GameHudSkin.Jam);
                }

                plate.Help.SetActive(downed);
                Show(plate.HelpIcon, downed);
                if (downed)
                {
                    bool near = pc != null && NetRun.Active.LocalAlive
                             && ((Vector2)a.transform.position - me).sqrMagnitude <= NetBalance.ReviveRadius * NetBalance.ReviveRadius;
                    string help = near
                        ? NetText.Get("net.hud.reviving", "RETTE") + " " + Mathf.FloorToInt(p.Revive * 100f) + "%"
                        : NetText.Get("net.hud.revive_me", "RETTEN!") + " " + Clock(p.Bleed);
                    plate.Help.Set(help, pixelFont, textFont);
                    float pulse = 0.5f + 0.5f * Mathf.Sin(now * 7f);
                    plate.Help.SetColor(near ? (Color)GameHudSkin.MintLight
                                             : Color.Lerp(GameHudSkin.Mint, GameHudSkin.Cream, pulse * 0.5f));
                    int bob = Mathf.RoundToInt(Mathf.Sin(now * 5f) * 1.5f);
                    MoveLabel(plate.Help, 0, bob);
                    float tw = plate.Help.Width;
                    Pos(plate.HelpIcon.rectTransform, 40f - tw / 2f - 11f, 2 + bob);

                    UpdateRing(plate, cam, a.transform.position, p.Revive, near, now);
                }
            }
            else
            {
                UpdatePointer(ptr, p, a.transform.position, cam, accent, (Vector2)a.transform.position - me, now);
            }
        }
    }

    /// <summary>
    /// Punktekreis um einen Niedergegangenen, so gross wie die Rettungsweite.
    /// Die Punkte fuellen sich reihum mit dem Fortschritt; steht jemand drin,
    /// dreht sich der Kreis.
    /// </summary>
    private void UpdateRing(Plate plate, Camera cam, Vector3 at, float progress, bool active, float now)
    {
        Vector2 c = ToHud(cam, at, out _);
        Vector2 edge = ToHud(cam, at + Vector3.right * NetBalance.ReviveRadius, out _);
        float r = Mathf.Max(6f, Mathf.Abs(edge.x - c.x));
        float spin = active ? now * 1.6f : now * 0.3f;
        int lit = Mathf.FloorToInt(Mathf.Clamp01(progress) * RingDots);
        float pulse = 0.55f + 0.45f * Mathf.Sin(now * 6f);

        Pos(plate.DotRoot, 0f, 0f);
        for (int d = 0; d < RingDots; d++)
        {
            float ang = spin + d * Mathf.PI * 2f / RingDots;
            float x = c.x + Mathf.Cos(ang) * r;
            float y = c.y + Mathf.Sin(ang) * r * 0.75f + 4f;   // leicht flach, wie auf dem Boden
            Pos(plate.Dots[d].rectTransform, x - 1.5f, y - 1.5f);
            Color col = d < lit ? (Color)GameHudSkin.MintLight
                      : OptionsKit.WithAlpha(active ? GameHudSkin.Mint : GameHudSkin.Cream, active ? 0.95f : 0.6f + 0.4f * pulse);
            Tint(plate.Dots[d], col);
        }
    }

    private void UpdatePointer(Pointer ptr, NetRun.Peer p, Vector3 world, Camera cam, Color accent, Vector2 delta, float now)
    {
        // Richtung von der Bildmitte zum Mitspieler, an den Rand geklemmt.
        Vector2 mid = new Vector2(hudW / 2f, hudH / 2f);
        Vector2 target = ToHud(cam, world, out bool front);
        if (!front) target = mid - (target - mid);
        Vector2 dir = target - mid;
        if (dir.sqrMagnitude < 0.01f) dir = Vector2.right;

        float hw = hudW / 2f - 22f, hh = hudH / 2f - 24f;
        float t = Mathf.Min(hw / Mathf.Max(0.001f, Mathf.Abs(dir.x)), hh / Mathf.Max(0.001f, Mathf.Abs(dir.y)));
        Vector2 at = mid + dir * t;
        ptr.At = at;
        Pos(ptr.Root, at.x - 10f, at.y - 10f);

        bool downed = p.State == NetRun.PeerState.Downed;
        bool blink = downed && Mathf.Repeat(now * 3f, 1f) < 0.5f;
        Tint(ptr.Ring, blink ? (Color)GameHudSkin.Jam : accent);
        Show(ptr.Skull, downed);
        Show(ptr.Mask, !downed || !blink);

        Sprite[] portrait = CharacterLooks.PortraitFor(p.Character);
        if (portrait != null && ptr.Portrait.sprite != portrait[0])
        {
            Sprite sp = portrait[0];
            ptr.Portrait.sprite = sp;
            Size(ptr.Portrait.rectTransform, sp.rect.width, sp.rect.height);
            Pos(ptr.Portrait.rectTransform, -Mathf.Round((sp.rect.width - 14) / 2f), -Mathf.Round((sp.rect.height - 14) / 2f));
        }

        // Pfeil an der Seite des Knopfs, die nach draussen zeigt.
        Sprite arrow;
        Vector2 apos;
        Vector3 flip = Vector3.one;
        if (Mathf.Abs(dir.x) * hh > Mathf.Abs(dir.y) * hw)
        {
            arrow = dir.x > 0f ? GameHudSkin.Arrow : GameHudSkin.ArrowLeft;
            apos = new Vector2(dir.x > 0f ? 20f : -arrow.rect.width, (20f - arrow.rect.height) / 2f);
        }
        else
        {
            arrow = GameHudSkin.ArrowDown;
            if (dir.y < 0f) flip = new Vector3(1f, -1f, 1f);
            apos = new Vector2((20f - arrow.rect.width) / 2f, dir.y > 0f ? 20f : 0f);
        }
        if (ptr.Arrow.sprite != arrow)
        {
            ptr.Arrow.sprite = arrow;
            Size(ptr.Arrow.rectTransform, arrow.rect.width, arrow.rect.height);
        }
        int nudge = Mathf.FloorToInt(now * 4f) % 2;
        Vector2 n = dir.normalized;
        ptr.Arrow.rectTransform.localScale = flip;
        Pos(ptr.Arrow.rectTransform, apos.x + Mathf.Round(n.x * nudge), apos.y + Mathf.Round(n.y * nudge));
        Tint(ptr.Arrow, accent);

        string dist = downed ? Clock(p.Bleed) : Mathf.RoundToInt(delta.magnitude) + "M";
        ptr.Dist.Set(dist, pixelFont, textFont);
        ptr.Dist.SetColor(downed ? (Color)GameHudSkin.JamLight : (Color)GameHudSkin.Cream);
        // Abstand unter dem Knopf - am unteren Rand darueber.
        MoveLabel(ptr.Dist, -10f, at.y > hudH - 40 ? -12f : 20f);
    }

    // ---------- Eigener Zustand ----------

    private void UpdateSelf(Camera cam, float now)
    {
        NetRun.Peer me = run.LocalPeer;
        PlayerController pc = PlayerController.Instance;
        bool running = run.CurrentPhase == NetRun.Phase.Running;
        bool downed = running && me != null && me.State == NetRun.PeerState.Downed && pc != null;
        bool isOut = running && me != null && me.State == NetRun.PeerState.Out;

        // Vignette: am Boden rot pochend, sonst weg.
        float pulse = 0.5f + 0.5f * Mathf.Sin(now * (downed && me.Bleed < 10f ? 9f : 4f));
        Color vig = downed ? new Color(0.55f, 0.05f, 0.12f, 0.55f + 0.3f * pulse) : new Color(0f, 0f, 0f, 0f);
        Tint(vignette, Color.Lerp(vignette.color, vig, Time.unscaledDeltaTime * 8f));
        Show(vignette, vignette.color.a > 0.01f);

        Show(downRoot, downed);
        Show(selfRing.DotRoot, downed && cam != null);
        if (downed)
        {
            downTitle.Set(NetText.Get("net.hud.you_down", "DU BIST NIEDERGEGANGEN!"), pixelFont, textFont);
            int w = Mathf.CeilToInt(downTitle.Width) + 24;
            w += w & 1;
            int x = (hudW - w) / 2;
            int pop = now % 1f < 0.08f ? 1 : 0;
            Size(downRibbon, w, 18);
            Pos(downRibbon, x, -pop);
            Stretch(downTitle, w);
            Pos(downTailL, x - 8, 4);
            Pos(downTailR, x + w - 4, 4);

            int bx = (hudW - 124) / 2;
            Pos(downBleedFrame, bx, 22);
            Pos(downBleed.rectTransform, bx + 1, 23);
            Pos(downRevive.rectTransform, bx + 1, 23);
            Size(downBleed.rectTransform, Mathf.RoundToInt(122 * Mathf.Clamp01(me.Bleed / NetBalance.BleedOutSeconds)), 6);
            Tint(downBleed, me.Bleed < 10f && Mathf.Repeat(now * 4f, 1f) < 0.5f ? (Color)GameHudSkin.JamLight : (Color)GameHudSkin.Jam);
            Size(downRevive.rectTransform, Mathf.RoundToInt(122 * Mathf.Clamp01(me.Revive)), 6);

            downSub.Set(me.Revive > 0.01f
                ? string.Format(NetText.Get("net.hud.being_revived_pct", "DU WIRST GERETTET ... {0}%"), Mathf.FloorToInt(me.Revive * 100f))
                : string.Format(NetText.Get("net.hud.wait_revive", "EIN MITSPIELER KANN DICH RETTEN - {0}"), Clock(me.Bleed)),
                pixelFont, textFont);
            downSub.SetColor(me.Revive > 0.01f ? (Color)GameHudSkin.MintLight : (Color)GameHudSkin.Parchment);

            if (cam != null) UpdateRing(selfRing, cam, pc.transform.position, me.Revive, me.Revive > 0.01f, now);
        }

        Show(outRoot, isOut);
        if (isOut)
        {
            NetRun.Peer watched = run.Spectating >= 0 ? run.Peers[run.Spectating] : null;
            outTitle.Set(watched != null
                ? string.Format(NetText.Get("net.hud.spectating_name", "ZUSCHAUER - DU SIEHST {0}"), watched.Name.ToUpperInvariant())
                : NetText.Get("net.hud.you_out", "AUSGESCHIEDEN"), pixelFont, textFont);
            outTitle.SetColor(watched != null ? CharacterLooks.AccentFor(watched.Character) : (Color)GameHudSkin.StoneLight);

            string sub = NetText.Get("net.hud.spectate_switch", "LEERTASTE: WECHSELN");
            if (NetRevive.Active != null && NetRevive.Active.TryGetAltar(run.LocalPeer.Slot, out float progress, out int inside))
                sub = string.Format(NetText.Get("net.hud.altar_self", "DEIN TEAM BEFREIT DICH AUS DEM GLAS: {0}%"), Mathf.FloorToInt(progress * 100f))
                    + (inside > 0 ? "  (" + inside + ")" : "");
            outSub.Set(sub, pixelFont, textFont);
            int w = Mathf.CeilToInt(Mathf.Max(outTitle.Width, outSub.Width)) + 20;
            Size(outBack.rectTransform, w, 28);
            Pos(outBack.rectTransform, (hudW - w) / 2, 0);
        }
    }

    // ---------- Emotes ----------

    private void UpdateBubbles(NetSession s, Camera cam, float now)
    {
        NetEmotes emotes = NetEmotes.Active;
        for (int slot = 0; slot < bubbles.Length; slot++)
        {
            Bubble b = bubbles[slot];
            Transform who = null;
            bool atEdge = false;
            float lift = 1.15f;
            if (emotes != null && cam != null && s != null && emotes.TryGet(slot, out NetEmotes.Kind kind, out float age))
            {
                if (slot == s.LocalSlot)
                {
                    PlayerController pc = PlayerController.Instance;
                    if (pc != null && pc.gameObject.activeInHierarchy) who = pc.transform;
                }
                else
                {
                    NetAvatar a = NetPlayers.AvatarOf(slot);
                    if (a != null && plates[slot].Root.gameObject.activeSelf) { who = a.transform; lift = 1.15f; }
                    // Nicht im Bild: die Blase haengt am Randknopf - ein "HILFE!" soll man auch von weitem sehen.
                    else if (pointers[slot].Root.gameObject.activeSelf) atEdge = true;
                }

                if (who != null || atEdge)
                {
                    Show(b.Root, true);
                    SetBubble(b, kind);
                    float w = b.Root.sizeDelta.x, h = b.Root.sizeDelta.y;
                    // Auftritt: springt gross auf, setzt sich, wippt; am Ende kurz ausblenden.
                    float sc = age < 0.08f ? 0.6f : age < 0.16f ? 1.25f : 1f;
                    int bob = Mathf.RoundToInt(Mathf.Sin(age * 6f) * 1.2f);
                    b.Root.pivot = new Vector2(0.5f, 0f);
                    if (atEdge)
                    {
                        // Ueber dem Knopf, am oberen Rand darunter (dann ohne Spitze); nie aus dem Bild.
                        Vector2 at = pointers[slot].At;
                        bool below = at.y - 12f - h < 2f;
                        float x = Mathf.Clamp(at.x, w / 2f + 2f, hudW - w / 2f - 2f);
                        float bottom = below ? at.y + 34f + h : at.y - 12f;
                        if (kind == NetEmotes.Kind.Help && Mathf.Repeat(age * 2.5f, 1f) < 0.2f) sc *= 1.15f;
                        b.Root.anchoredPosition = new Vector2(Mathf.Round(x), -Mathf.Round(bottom - bob));
                        Show(b.Tail, !below);
                    }
                    else
                    {
                        Vector2 px = ToHud(cam, who.position + Vector3.up * lift, out _);
                        float extra = slot == s.LocalSlot ? 4f : 30f;   // ueber dem Namensschild der anderen
                        b.Root.anchoredPosition = new Vector2(Mathf.Round(px.x), -Mathf.Round(px.y - extra - bob));
                        Show(b.Tail, true);
                    }
                    b.Root.localScale = new Vector3(sc, sc, 1f);
                    float alpha = Mathf.Clamp01((NetEmotes.Duration - age) / 0.3f);
                    Tint(b.Back, new Color(1f, 1f, 1f, alpha));
                    Tint(b.Tail, new Color(1f, 1f, 1f, alpha));
                    Tint(b.Icon, new Color(1f, 1f, 1f, alpha));
                    b.Text.SetAlpha(alpha);
                    continue;
                }
            }
            Show(b.Root, false);
        }
    }

    private void SetBubble(Bubble b, NetEmotes.Kind kind)
    {
        if (b.Kind == kind) return;
        b.Kind = kind;
        Sprite icon;
        string text;
        Color color = GameHudSkin.Ink;
        switch (kind)
        {
            case NetEmotes.Kind.Help:
                icon = GameHudSkin.Exclaim;
                text = NetText.Get("net.emote.help", "HILFE!");
                color = GameHudSkin.JamDark;
                break;
            case NetEmotes.Kind.Haha:
                icon = GameHudSkin.CookieFace;
                text = NetText.Get("net.emote.haha", "HAHA");
                break;
            case NetEmotes.Kind.Gg:
                icon = GameHudSkin.Crown;
                text = NetText.Get("net.emote.gg", "GG");
                color = GameHudSkin.GoldDark;
                break;
            default:
                icon = GameHudSkin.Heart;
                text = "";   // das Herz spricht fuer sich
                color = GameHudSkin.JamDark;
                break;
        }

        b.Icon.sprite = icon;
        int iw = Mathf.RoundToInt(icon.rect.width), ih = Mathf.RoundToInt(icon.rect.height);
        b.Text.Set(text, pixelFont, textFont);
        b.Text.SetColor(color);
        int tw = Mathf.CeilToInt(b.Text.Width);
        int w = 4 + iw + (tw > 0 ? 2 + tw : 0) + 4;
        w += w & 1;
        int h = Mathf.Max(14, ih + 6);
        Size(b.Root, w, h + 4);
        Size(b.Back.rectTransform, w, h);
        Size(b.Icon.rectTransform, iw, ih);
        Pos(b.Icon.rectTransform, 4, (h - ih) / 2);
        MoveLabel(b.Text, 4 + iw + 2, (h - 12) / 2);
        Pos(b.Tail.rectTransform, (w - 5) / 2, h - 1);
    }

    // ---------- Mitte ----------

    private void UpdateCenter(float now)
    {
        string main = "", sub = "";
        Color mainColor = GameHudSkin.Cream;
        string choose = "";

        switch (run.CurrentPhase)
        {
            case NetRun.Phase.Loading:
            {
                int dots = 1 + Mathf.FloorToInt(now * 2.5f) % 3;
                main = NetText.Get("net.hud.waiting_short", "WARTE AUF MITSPIELER") + new string('.', dots);
                var names = new List<string>();
                foreach (NetRun.Peer p in run.Peers)
                    if (p.Playing && p.State != NetRun.PeerState.Left) names.Add(p.Name.ToUpperInvariant());
                sub = string.Join("  ", names);
                break;
            }
            case NetRun.Phase.Running:
            {
                NetRun.Peer me = run.LocalPeer;
                if (run.Paused && (me == null || !me.Blocking))
                {
                    run.CollectChoosing(choosing);
                    if (choosing.Count > 0)
                    {
                        int dots = 1 + Mathf.FloorToInt(now * 3f) % 3;
                        choose = string.Format(NetText.Get("net.hud.others_choosing_short", "{0} WÄHLT"),
                                               string.Join(", ", choosing).ToUpperInvariant()) + new string('.', dots);
                    }
                }
                break;
            }
        }

        center.SetActive(main.Length > 0);
        centerSub.SetActive(sub.Length > 0);
        if (main.Length > 0) { center.Set(main, pixelFont, textFont); center.SetColor(mainColor); }
        if (sub.Length > 0) centerSub.Set(sub, pixelFont, textFont);

        Show(chooseRoot, choose.Length > 0);
        if (choose.Length > 0)
        {
            chooseText.Set(choose, pixelFont, textFont);
            // Breite nach dem Text ohne die wechselnden Punkte, sonst zappelt das Schild.
            int w = Mathf.CeilToInt(chooseText.Width) + 26 + (3 - (1 + Mathf.FloorToInt(now * 3f) % 3)) * 3;
            w = Mathf.Max(60, w + (w & 1));
            Size(chooseRoot, w, 15);
            Size(chooseBack.rectTransform, w, 15);
            Pos(chooseRoot, (hudW - w) / 2, 48);
            Pos(chooseIcon.rectTransform, 5, 3 + (Mathf.FloorToInt(now * 3f) % 2));
        }
    }

    private void UpdateToasts(float now)
    {
        toasts.RemoveAll(t => t.Until <= now);
        int y = 66;
        for (int i = 0; i < toastViews.Length; i++)
        {
            ToastView v = toastViews[i];
            bool on = i < toasts.Count;
            Show(v.Root, on);
            if (!on) continue;
            ToastEntry t = toasts[i];
            v.Text.Set(t.Text, pixelFont, textFont);
            v.Text.SetColor(t.Color);
            Tint(v.Cap, t.Color);

            int w = Mathf.CeilToInt(v.Text.Width) + 12;
            w += w & 1;
            Size(v.Root, w, 14);
            Size(v.Back.rectTransform, w, 14);

            // Faellt von oben herein, federt kurz nach und blendet am Ende aus.
            float age = now - t.Born;
            float drop = age < 0.1f ? -8f * (1f - age / 0.1f) : age < 0.16f ? 1f : 0f;
            float alpha = Mathf.Clamp01((t.Until - now) / 0.4f) * Mathf.Clamp01(age / 0.08f);
            Pos(v.Root, (hudW - w) / 2, y + drop);
            Tint(v.Back, new Color(1f, 1f, 1f, alpha));
            Tint(v.Cap, OptionsKit.WithAlpha(t.Color, alpha));
            v.Text.SetAlpha(alpha);
            y += 16;
        }
    }

    private void UpdateHint(int rows, float now)
    {
        bool on = runStarted > 0f && now - runStarted < HintTime && run.CurrentPhase == NetRun.Phase.Running;
        emoteHint.SetActive(on);
        if (!on) return;
        emoteHint.Set(NetText.Get("net.hud.emote_hint", "1-4: EMOTES"), pixelFont, textFont);
        MoveLabel(emoteHint, CardX + 2, CardY + rows * CardStep + 1);
        emoteHint.SetAlpha(Mathf.Clamp01((HintTime - (now - runStarted)) / 2f));
    }

    private static bool LocalChoosing()
    {
        UIController ui = UIController.Instance;
        if (ui == null) return false;
        return (ui.LevelUpPanel != null && ui.LevelUpPanel.activeSelf)
            || (ui.PowerUpPanel != null && ui.PowerUpPanel.activeSelf)
            || (ui.EvoPanel != null && ui.EvoPanel.activeSelf)
            || (ui.GameOverPanel != null && ui.GameOverPanel.activeSelf)
            || (ui.WinPanel != null && ui.WinPanel.activeSelf);
    }
}
