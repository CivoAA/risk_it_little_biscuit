using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Was das HUD im Koop zusaetzlich zeigt. Das normale HUD (<see cref="GameHud"/>)
/// bleibt wie allein - das hier liegt daneben:
///
///  * links unter den Slots die Rahmen der Mitspieler: Portraet, Name,
///    Lebensleiste, Level, und was gerade mit ihnen ist (WAEHLT, UNTEN 0:32, RAUS)
///  * ueber jeder Figur eines Mitspielers ihr Name in seiner Farbe
///  * am Bildrand Pfeile zu Mitspielern, die nicht im Bild sind
///  * ueber niedergegangenen Figuren der Rettungsbalken
///  * in der Mitte: Warten beim Laden, "NICK WAEHLT ...", Meldungen
///    (niedergegangen, gerettet, raus), der eigene Geist-Zustand und das Zuschauen
///
/// Gerechnet wird wie im GameHud in einem 480x270-Raster mit ganzzahliger
/// Skalierung, damit jeder Pixel gleich dick ist.
/// </summary>
public class NetHud : MonoBehaviour
{
    private const int RefW = 480, RefH = 270;
    private const int FrameX = 4, FrameY = 96, FrameW = 104, FrameH = 24, FrameStep = 27;
    private const int Win = 20;
    private const float SizeText = 10f;
    private const float ToastTime = 3.2f;

    private class Frame
    {
        public RectTransform Root;
        public Image Back, Portrait, Fill, BarFrame, Chip;
        public Label Name, Level, Status;
        public int Slot = -1;
    }

    private class Marker
    {
        public RectTransform Root;
        public Label Name;
        public Image BarBack, BarFill;
        public RectTransform Arrow;
        public Image ArrowImg;
        public Label ArrowName;
        public Image ReviveBack, ReviveFill;
    }

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

        public void SetColor(Color c) => Main.color = c;

        public void SetActive(bool on)
        {
            if (Main.gameObject.activeSelf == on) return;
            Main.gameObject.SetActive(on);
            foreach (var b in Backs) b.gameObject.SetActive(on);
        }

        public void SetAlpha(float a)
        {
            Color c = Main.color;
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

    private struct ToastEntry
    {
        public string Text;
        public Color Color;
        public float Until;
    }

    private Canvas canvas, topCanvas;
    private CanvasScaler scaler, topScaler;
    private RectTransform root, topRoot;
    private TMP_FontAsset pixelFont, textFont;
    private Vector2Int lastScreen;
    private int scale = 1;

    private NetRun run;
    private readonly Frame[] frames = new Frame[NetSession.MaxPlayers];
    private readonly Marker[] markers = new Marker[NetSession.MaxPlayers];
    private Label center, centerSub, banner, runningHint;
    private readonly Label[] toastLabels = new Label[4];
    private readonly List<ToastEntry> toasts = new List<ToastEntry>();
    private Marker selfRevive;
    private readonly List<string> choosing = new List<string>();

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

        for (int i = 0; i < frames.Length; i++) frames[i] = BuildFrame(i);
        for (int i = 0; i < markers.Length; i++) markers[i] = BuildMarker(i);
        selfRevive = BuildMarker(-1);

        center = NewLabel("Center", root, new RectInt(0, 112, RefW, 14), GameHudSkin.Cream, TextAlignmentOptions.Center);
        centerSub = NewLabel("CenterSub", root, new RectInt(0, 126, RefW, 14), GameHudSkin.Parchment, TextAlignmentOptions.Center);
        banner = NewLabel("Banner", root, new RectInt(0, 48, RefW, 14), GameHudSkin.Gold, TextAlignmentOptions.Center);
        for (int i = 0; i < toastLabels.Length; i++)
            toastLabels[i] = NewLabel("Toast" + i, root, new RectInt(0, 64 + i * 12, RefW, 14), GameHudSkin.Cream,
                                      TextAlignmentOptions.Center);
        runningHint = NewLabel("Running", topRoot, new RectInt(0, 6, RefW, 14), GameHudSkin.GoldLight,
                               TextAlignmentOptions.Center);

        NetRun.Toast += OnToast;
        ApplyLayout(true);
    }

    private void OnDestroy()
    {
        NetRun.Toast -= OnToast;
    }

    private void OnToast(string text, Color color)
    {
        toasts.Add(new ToastEntry { Text = text, Color = color, Until = Time.unscaledTime + ToastTime });
        while (toasts.Count > toastLabels.Length) toasts.RemoveAt(0);
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

    private Frame BuildFrame(int i)
    {
        var f = new Frame();
        f.Root = OptionsKit.Rect("Mate" + i, root, FrameX, FrameY + i * FrameStep, FrameW, FrameH);
        f.Back = OptionsKit.Img("Back", f.Root, 0, 0, FrameW, FrameH, GameHudSkin.Plate, true);

        Image win = OptionsKit.Img("WinBack", f.Root, 2, 2, Win, Win, GameHudSkin.SlotBack(Win, Win));
        RectTransform window = OptionsKit.Rect("Window", f.Root, 2, 2, Win, Win);
        window.gameObject.AddComponent<RectMask2D>();
        f.Portrait = OptionsKit.Img("Portrait", window, -11, -11, 42, 42, null);

        f.Name = NewLabel("Name", f.Root, new RectInt(25, 0, 62, 12), GameHudSkin.Cream, TextAlignmentOptions.Left);
        f.BarFrame = OptionsKit.Img("Bar", f.Root, 25, 14, 58, 7, GameHudSkin.BarFrame, true);
        f.Fill = OptionsKit.Img("Fill", f.Root, 26, 15, 56, 5, GameHudSkin.White, GameHudSkin.Jam);
        f.Chip = OptionsKit.Img("Chip", f.Root, 86, 11, 16, 11, GameHudSkin.LevelChip, true);
        f.Level = NewLabel("Level", f.Root, new RectInt(86, 10, 16, 12), GameHudSkin.Parchment, TextAlignmentOptions.Center);
        f.Status = NewLabel("Status", f.Root, new RectInt(25, 11, 60, 12), GameHudSkin.JamLight, TextAlignmentOptions.Left);
        f.Root.gameObject.SetActive(false);
        return f;
    }

    private Marker BuildMarker(int i)
    {
        var m = new Marker();
        m.Root = OptionsKit.Rect("Marker" + i, root, 0, 0, 60, 40);
        m.Name = NewLabel("Name", m.Root, new RectInt(0, 0, 60, 12), GameHudSkin.Cream, TextAlignmentOptions.Center);
        m.BarBack = OptionsKit.Img("BarBack", m.Root, 20, 12, 20, 4, GameHudSkin.White, GameHudSkin.Ink);
        m.BarFill = OptionsKit.Img("BarFill", m.Root, 21, 13, 18, 2, GameHudSkin.White, GameHudSkin.Jam);
        m.ReviveBack = OptionsKit.Img("ReviveBack", m.Root, 14, 18, 32, 5, GameHudSkin.White, GameHudSkin.Ink);
        m.ReviveFill = OptionsKit.Img("ReviveFill", m.Root, 15, 19, 30, 3, GameHudSkin.White, GameHudSkin.Mint);

        m.Arrow = OptionsKit.Rect("Arrow" + i, root, 0, 0, 40, 20);
        m.ArrowImg = OptionsKit.Img("Img", m.Arrow, 0, 0, 9, 9, GameHudSkin.Arrow);
        m.ArrowName = NewLabel("ArrowName", m.Arrow, new RectInt(0, 9, 40, 12), GameHudSkin.Cream, TextAlignmentOptions.Center);
        m.Root.gameObject.SetActive(false);
        m.Arrow.gameObject.SetActive(false);
        return m;
    }

    private void ApplyLayout(bool force)
    {
        var size = new Vector2Int(Screen.width, Screen.height);
        if (!force && size == lastScreen) return;
        lastScreen = size;
        scale = Mathf.Max(1, Mathf.RoundToInt(Mathf.Min(size.x / (float)RefW, size.y / (float)RefH)));
        scaler.scaleFactor = scale;
        topScaler.scaleFactor = scale;

        // Mitte-Texte ueber die ganze (abgerundete) HUD-Breite.
        int w = size.x / scale;
        foreach (Label l in new[] { center, centerSub, banner, runningHint })
            Stretch(l, w);
        foreach (Label l in toastLabels) Stretch(l, w);
    }

    private static void Stretch(Label l, int w)
    {
        if (l == null) return;
        Resize(l.Main.rectTransform, w);
        foreach (var b in l.Backs) Resize(b.rectTransform, w);
    }

    private static void Resize(RectTransform rt, int w)
    {
        rt.sizeDelta = new Vector2(w, rt.sizeDelta.y);
    }

    // ==================================================================
    //  Takt
    // ==================================================================

    private void LateUpdate()
    {
        if (run == null) return;
        ApplyLayout(false);

        NetSession s = NetSession.Instance;
        UpdateFrames(s);
        UpdateMarkers(s);
        UpdateCenter(s);
        UpdateToasts();

        bool pauseOpen = PauseMenuPanel.IsOpen && run.CurrentPhase == NetRun.Phase.Running;
        runningHint.SetActive(pauseOpen);
        if (pauseOpen)
            runningHint.Set(NetText.Get("net.hud.still_running", "KOOP: DAS SPIEL LÄUFT WEITER!"), pixelFont, textFont);
    }

    private void UpdateFrames(NetSession s)
    {
        int row = 0;
        PlayerController pc = PlayerController.Instance;
        int level = pc != null ? pc.currentLevel : 1;

        for (int i = 0; i < frames.Length; i++) frames[i].Root.gameObject.SetActive(false);
        if (s == null) return;

        for (int slot = 0; slot < run.Peers.Length; slot++)
        {
            NetRun.Peer p = run.Peers[slot];
            if (!p.Playing || slot == s.LocalSlot) continue;
            Frame f = frames[row++];
            f.Root.gameObject.SetActive(true);
            f.Slot = slot;

            Sprite[] portrait = CharacterLooks.PortraitFor(p.Character);
            if (portrait != null && f.Portrait.sprite != portrait[0])
            {
                f.Portrait.sprite = portrait[0];
                f.Portrait.rectTransform.sizeDelta = new Vector2(portrait[0].rect.width, portrait[0].rect.height);
                f.Portrait.rectTransform.anchoredPosition = new Vector2(-Mathf.Round((portrait[0].rect.width - Win) / 2f),
                                                                        Mathf.Round((portrait[0].rect.height - Win) / 2f));
            }
            f.Portrait.color = p.State == NetRun.PeerState.Alive ? Color.white : new Color(0.45f, 0.45f, 0.55f, 1f);

            f.Name.Set(p.Name.ToUpperInvariant(), pixelFont, textFont);
            f.Name.SetColor(CharacterLooks.AccentFor(p.Character));
            f.Level.Set(level.ToString(), pixelFont, textFont);

            float hp = p.MaxHealth > 0f ? Mathf.Clamp01(p.Health / p.MaxHealth) : 0f;
            bool showBar = p.State == NetRun.PeerState.Alive;
            f.BarFrame.enabled = showBar;
            f.Fill.enabled = showBar;
            if (showBar) f.Fill.rectTransform.sizeDelta = new Vector2(Mathf.Max(0, Mathf.RoundToInt(56 * hp)), 5);

            string status = "";
            Color color = GameHudSkin.JamLight;
            switch (p.State)
            {
                case NetRun.PeerState.Downed:
                    status = string.Format(NetText.Get("net.hud.down", "UNTEN {0}"), Clock(p.Bleed));
                    break;
                case NetRun.PeerState.Out:
                    status = NetText.Get("net.hud.out", "RAUS");
                    color = GameHudSkin.StoneLight;
                    break;
                case NetRun.PeerState.Left:
                    status = NetText.Get("net.hud.left", "WEG");
                    color = GameHudSkin.StoneLight;
                    break;
                default:
                    if (p.Blocking)
                    {
                        status = NetText.Get("net.hud.choosing", "WÄHLT ...");
                        color = GameHudSkin.Gold;
                        f.BarFrame.enabled = false;
                        f.Fill.enabled = false;
                    }
                    break;
            }
            f.Status.SetActive(status.Length > 0);
            if (status.Length > 0)
            {
                f.Status.Set(status, pixelFont, textFont);
                f.Status.SetColor(color);
            }
        }
    }

    private static string Clock(float seconds)
    {
        int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return (s / 60) + ":" + (s % 60).ToString("00");
    }

    private void UpdateMarkers(NetSession s)
    {
        Camera cam = Camera.main;
        int w = Screen.width / scale, h = Screen.height / scale;

        for (int slot = 0; slot < markers.Length; slot++)
        {
            Marker m = markers[slot];
            NetAvatar a = s != null && slot != s.LocalSlot ? NetPlayers.AvatarOf(slot) : null;
            NetRun.Peer p = run.Peers[slot];
            bool show = a != null && cam != null && p.State != NetRun.PeerState.Out && p.State != NetRun.PeerState.Left;
            if (!show)
            {
                m.Root.gameObject.SetActive(false);
                m.Arrow.gameObject.SetActive(false);
                continue;
            }

            Color accent = CharacterLooks.AccentFor(p.Character);
            Vector3 head = a.transform.position + Vector3.up * 1.15f;
            Vector3 sp = cam.WorldToScreenPoint(head);
            Vector2 px = new Vector2(sp.x / scale, h - sp.y / scale);
            bool onScreen = sp.z > 0f && px.x > 8 && px.x < w - 8 && px.y > 8 && px.y < h - 8;

            m.Root.gameObject.SetActive(onScreen);
            m.Arrow.gameObject.SetActive(!onScreen);

            if (onScreen)
            {
                m.Root.anchoredPosition = new Vector2(Mathf.Round(px.x - 30f), -Mathf.Round(px.y - 6f));
                m.Name.Set(p.Name.ToUpperInvariant(), pixelFont, textFont);
                m.Name.SetColor(accent);

                bool alive = p.State == NetRun.PeerState.Alive;
                float hp = p.MaxHealth > 0f ? Mathf.Clamp01(p.Health / p.MaxHealth) : 0f;
                m.BarBack.enabled = alive;
                m.BarFill.enabled = alive;
                if (alive) m.BarFill.rectTransform.sizeDelta = new Vector2(Mathf.RoundToInt(18 * hp), 2);

                bool downed = p.State == NetRun.PeerState.Downed;
                m.ReviveBack.enabled = downed;
                m.ReviveFill.enabled = downed;
                if (downed) m.ReviveFill.rectTransform.sizeDelta = new Vector2(Mathf.RoundToInt(30 * Mathf.Clamp01(p.Revive)), 3);
            }
            else
            {
                PlaceArrow(m, px, w, h, accent, p.Name, cam, a.transform.position);
            }
        }

        // Eigener Rettungsbalken ueber dem eigenen Geist.
        NetRun.Peer me = run.LocalPeer;
        PlayerController pc = PlayerController.Instance;
        bool selfDown = me != null && me.State == NetRun.PeerState.Downed && pc != null && cam != null;
        selfRevive.Root.gameObject.SetActive(selfDown);
        selfRevive.Arrow.gameObject.SetActive(false);
        if (selfDown)
        {
            Vector3 sp = cam.WorldToScreenPoint(pc.transform.position + Vector3.up * 1.15f);
            selfRevive.Root.anchoredPosition = new Vector2(Mathf.Round(sp.x / scale - 30f), -Mathf.Round(h - sp.y / scale - 6f));
            selfRevive.Name.Set("", pixelFont, textFont);
            selfRevive.BarBack.enabled = false;
            selfRevive.BarFill.enabled = false;
            selfRevive.ReviveBack.enabled = true;
            selfRevive.ReviveFill.enabled = true;
            selfRevive.ReviveFill.rectTransform.sizeDelta = new Vector2(Mathf.RoundToInt(30 * Mathf.Clamp01(me.Revive)), 3);
        }
    }

    private void PlaceArrow(Marker m, Vector2 px, int w, int h, Color accent, string name, Camera cam, Vector3 world)
    {
        // Richtung von der Bildmitte zum Mitspieler, an den Rand geklemmt.
        Vector2 mid = new Vector2(w / 2f, h / 2f);
        Vector3 sp = cam.WorldToScreenPoint(world);
        Vector2 target = new Vector2(sp.x / scale, h - sp.y / scale);
        if (sp.z < 0f) target = mid - (target - mid);
        Vector2 dir = target - mid;
        if (dir.sqrMagnitude < 0.01f) dir = Vector2.right;

        float hw = w / 2f - 22f, hh = h / 2f - 18f;
        float t = Mathf.Min(hw / Mathf.Max(0.001f, Mathf.Abs(dir.x)), hh / Mathf.Max(0.001f, Mathf.Abs(dir.y)));
        Vector2 at = mid + dir * t;

        Sprite sprite;
        Vector3 flip = Vector3.one;
        if (Mathf.Abs(dir.x) * hh > Mathf.Abs(dir.y) * hw)
            sprite = dir.x > 0f ? GameHudSkin.Arrow : GameHudSkin.ArrowLeft;
        else
        {
            sprite = GameHudSkin.ArrowDown;
            if (dir.y < 0f) flip = new Vector3(1f, -1f, 1f);   // nach oben
        }

        if (m.ArrowImg.sprite != sprite)
        {
            m.ArrowImg.sprite = sprite;
            m.ArrowImg.rectTransform.sizeDelta = new Vector2(sprite.rect.width, sprite.rect.height);
        }
        m.ArrowImg.rectTransform.localScale = flip;
        m.ArrowImg.color = accent;
        m.ArrowImg.rectTransform.anchoredPosition = new Vector2(Mathf.Round((40 - sprite.rect.width) / 2f),
                                                                flip.y < 0f ? -sprite.rect.height : 0f);
        m.Arrow.anchoredPosition = new Vector2(Mathf.Round(at.x - 20f), -Mathf.Round(at.y - 5f));

        string shortName = name.Length > 6 ? name.Substring(0, 6) : name;
        m.ArrowName.Set(shortName.ToUpperInvariant(), pixelFont, textFont);
        m.ArrowName.SetColor(accent);
    }

    private void UpdateCenter(NetSession s)
    {
        string main = "", sub = "", top = "";
        Color mainColor = GameHudSkin.Cream;

        switch (run.CurrentPhase)
        {
            case NetRun.Phase.Loading:
                main = NetText.Get("net.hud.waiting", "WARTE AUF MITSPIELER ...");
                break;

            case NetRun.Phase.Running:
                NetRun.Peer me = run.LocalPeer;
                if (me != null && me.State == NetRun.PeerState.Downed)
                {
                    main = NetText.Get("net.hud.you_down", "DU BIST NIEDERGEGANGEN!");
                    mainColor = GameHudSkin.JamLight;
                    sub = me.Revive > 0.01f
                        ? NetText.Get("net.hud.being_revived", "DU WIRST GERETTET ...")
                        : string.Format(NetText.Get("net.hud.wait_revive", "EIN MITSPIELER KANN DICH RETTEN - {0}"), Clock(me.Bleed));
                }
                else if (me != null && me.State == NetRun.PeerState.Out)
                {
                    NetRun.Peer watched = run.Spectating >= 0 ? run.Peers[run.Spectating] : null;
                    main = NetText.Get("net.hud.you_out", "AUSGESCHIEDEN");
                    mainColor = GameHudSkin.StoneLight;
                    sub = watched != null
                        ? string.Format(NetText.Get("net.hud.spectating", "DU SCHAUST {0} ZU - LEERTASTE: WECHSELN"),
                                        watched.Name.ToUpperInvariant())
                        : "";
                }

                if (run.Paused && (me == null || !me.Blocking))
                {
                    run.CollectChoosing(choosing);
                    if (choosing.Count > 0)
                        top = string.Format(NetText.Get("net.hud.others_choosing", "{0} WÄHLT ..."),
                                            string.Join(", ", choosing).ToUpperInvariant());
                }
                break;
        }

        center.SetActive(main.Length > 0);
        centerSub.SetActive(sub.Length > 0);
        banner.SetActive(top.Length > 0);
        if (main.Length > 0) { center.Set(main, pixelFont, textFont); center.SetColor(mainColor); }
        if (sub.Length > 0) centerSub.Set(sub, pixelFont, textFont);
        if (top.Length > 0) banner.Set(top, pixelFont, textFont);

        // Waehrend man selbst waehlt, liegt der Level-Up-Bildschirm drueber - dann
        // gehoert die Mitte ihm. Der Hinweis auf die anderen steht in seinem Kopf.
        canvas.enabled = !LocalChoosing();
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

    private void UpdateToasts()
    {
        float now = Time.unscaledTime;
        toasts.RemoveAll(t => t.Until <= now);
        for (int i = 0; i < toastLabels.Length; i++)
        {
            bool on = i < toasts.Count;
            toastLabels[i].SetActive(on);
            if (!on) continue;
            ToastEntry t = toasts[i];
            toastLabels[i].Set(t.Text, pixelFont, textFont);
            toastLabels[i].SetColor(t.Color);
            toastLabels[i].SetAlpha(Mathf.Clamp01((t.Until - now) / 0.5f));
        }
    }
}
