using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Das HUD im Lauf: Portraet mit Level, Lebens- und Erfahrungsleiste, Uhr am
/// Holzschild, Phasenname bzw. Ansage, Gold und die Waffen- und Buff-Slots.
///
///   GameHud.Create(uiController);   // macht UIController.Awake selbst
///
/// Baut sich komplett per Code auf, wie <see cref="PauseMenuPanel"/> - kein
/// Prefab, nichts im Inspector. Die Grafik malt <see cref="GameHudSkin"/>.
///
/// Gerechnet wird in Pixeln eines 480x270-Rasters, Ursprung oben links -
/// feiner als die 320x180 des Pausenmenues. Das HUD liegt die ganze Zeit
/// ueber dem Spiel und soll leise sein; ausserdem landen die 64er-Waffenicons
/// in 16er-Slots bei 1080p so genau 1:1 auf dem Bildschirm. Skaliert wird nur
/// in GANZEN Stufen (1080p = 4, 4K = 8), damit jeder Pixel der Leisten und
/// Rahmen gleich dick bleibt. Die drei Gruppen (links, Mitte, rechts) haengen
/// an den Bildschirmkanten, ihre Lage wird auf ganze Pixel gerundet (siehe
/// [[pixel-ui-ganze-pixel]]).
///
/// Die Daten kommen live aus PlayerController, GameManager, SpawnDirector
/// (nur Phasenname und Ansagen) und den beiden Item-Menues (<see cref="ItemMenu.CurrentSlots"/>). Die alten
/// Anzeigen blendet UIController aus, laesst sie aber weiterlaufen.
/// </summary>
public class GameHud : MonoBehaviour
{
    // ==================================================================
    //  Masse (Pixel im 480x270-Raster)
    // ==================================================================

    private const int RefW = 480, RefH = 270;
    private const int Margin = 4;

    // ---- links: Portraet, Leisten ----
    private static readonly RectInt MedallionR = new RectInt(4, 4, 30, 30);
    private static readonly RectInt PortraitR  = new RectInt(7, 7, 24, 24);
    private static readonly RectInt BadgeR     = new RectInt(7, 27, 24, 11);
    private static readonly RectInt PlateR     = new RectInt(30, 4, 114, 28);

    private static readonly RectInt HpFrameR   = new RectInt(46, 8, 94, 12);
    private static readonly RectInt HeartR     = new RectInt(36, 10, 9, 8);
    private static readonly RectInt XpFrameR   = new RectInt(46, 21, 94, 7);
    private static readonly RectInt GemR       = new RectInt(38, 21, 7, 7);

    // ---- links: Slots ----
    // Waffen und Buffs sind gleich gross; unterscheiden tun sie sich an der
    // Farbe der Kachel-Unterkante (Waffen gold, Buffs mint, Evos rosa).
    private const int SlotSize = 22, SlotStep = 24;
    private const int WeaponY = 41, BuffY = 68;
    private const int MaxSlots = 8;

    // ---- Mitte: Schild, Phase ----
    private const int CenterW = 96;
    private static readonly RectInt SignR   = new RectInt(20, 4, 56, 22);
    private static readonly RectInt LabelR  = new RectInt(0, 30, 96, 11);
    private const int RibbonY = 30, RibbonH = 14;

    // ---- rechts: Gold (Chip waechst mit der Zahl nach links) ----
    private const int RightW = 80;
    private const int GoldChipY = 4, GoldChipH = 13;
    private const int CoinDX = 3, CoinDY = 3, CoinSize = 7;

    // ---- Schrift ----
    // Jersey10 ist auf 10 px gezeichnet: nur in Vielfachen von 10 liegt jeder
    // Schriftpixel auf genau einem HUD-Pixel - und die 1-px-Kontur passt dazu.
    private const float SizeTimer = 20f;
    private const float SizeValue = 10f;
    private const float SizeGold = 10f;
    private const float SizeLabel = 10f;
    private const float SizeRibbon = 10f;

    // ---- Tempo ----
    private const float TrailHold = 0.4f;     // so lange steht der Schadensrest, bevor er abtropft
    private const float TrailDrain = 0.9f;    // Anteil der Leiste pro Sekunde
    private const float HealRise = 1.4f;
    private const float LowHealth = 0.3f;

    // ==================================================================
    //  Zustand
    // ==================================================================

    private UIController ui;
    private Canvas canvas;
    private CanvasScaler scaler;
    private CanvasGroup group;
    private TMP_FontAsset font, textFont;

    private RectTransform root, left, center, right;
    private Vector2Int lastScreen;

    // Portraet
    private Image portraitBack, portrait;
    private RectTransform portraitRect;
    private SpriteRenderer playerSprite;
    private Animator playerAnimator;

    // Zweiter, unsichtbarer Animator mit demselben Controller wie der Spieler,
    // fest auf "Idle, Blick nach vorn". Das Portraet laeuft nicht mit, wenn
    // der Keks rennt - und ein Skinwechsel kommt trotzdem an.
    private Animator idleAnimator;
    private SpriteRenderer idleSprite;
    private RuntimeAnimatorController idleController;
    private RectTransform badge;
    private Shadowed badgeText;
    private int shownLevel = -1;
    private float badgePop;

    // Leben
    private Bar hp;
    private Shadowed hpText;
    private RectTransform shieldBar;
    private Image shieldHi;
    private int lastShield = -1;
    private const int ShieldH = 5;   // obere Haelfte der 10px Balkenfuellung
    private Image heart;
    private float hpShown = 1f, hpTrail = 1f, trailHoldUntil;
    private bool trailIsHeal;
    private float hitFlash, shakeUntil;
    private float lastHp = -1f;
    private int lastHpCur = -1, lastHpMax = -1;
    private int lastShake;

    // Erfahrung
    private Bar xp;
    private float xpShown;
    private float levelFlash;

    // Uhr
    private RectTransform sign, signShadow;
    private Shadowed timerText;
    private int lastSecond = -1, lastMinute = -1;
    private float minuteBump;

    // Phase / Ansage
    private Shadowed phaseText;
    private RectTransform ribbon;
    private Image ribbonImage;
    private Shadowed ribbonText;
    private string lastLabel;
    private bool lastAnnouncing;
    private float labelFlash, ribbonStart;

    // Gold
    private RectTransform goldChip, coin;
    private Shadowed goldText;
    private int goldTextWidth = -1;
    private float goldShown;
    private int goldTarget, goldDrawn = -1;
    private float nextGoldPoll;

    // Slots
    private readonly List<SlotView> weaponViews = new List<SlotView>();
    private readonly List<SlotView> buffViews = new List<SlotView>();
    private ItemMenu itemMenu;
    private ItemMenuBuffs itemMenuBuffs;

    // ==================================================================
    //  Aufbau
    // ==================================================================

    public static GameHud Create(UIController ui)
    {
        var go = new GameObject("GameHud");
        // Neue Objekte landen in der AKTIVEN Szene - waehrend eines Laufs ist
        // das der Hub. Das HUD gehoert aber zum Lauf und soll mit ihm gehen.
        if (ui.gameObject.scene.IsValid() && go.scene != ui.gameObject.scene)
            SceneManager.MoveGameObjectToScene(go, ui.gameObject.scene);

        GameHud hud = go.AddComponent<GameHud>();
        hud.ui = ui;
        hud.Build(ui.GetComponent<Canvas>());
        return hud;
    }

    private void Build(Canvas uiCanvas)
    {
        // ThaleahFat, nicht Jersey10: GameCore bringt sie selbst mit (jeder alte
        // Text dort nutzt sie), das HUD sieht also immer gleich aus - egal ob
        // der Lauf aus dem Hub kommt oder die Map direkt gestartet wurde. Ihre
        // Ziffern sind kraeftiger. Umlaute kann sie nicht; ein Text, der welche
        // braucht, weicht einzeln auf Jersey10 aus (siehe Shadowed.Set).
        font = PixelUI.FindPixelFont();
        textFont = PixelUI.FindTextFont();
        if (font == null) font = textFont;

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        // Unter dem alten UI-Canvas: Level-Up, Evo, Gamba, Game Over und Sieg
        // liegen darueber, das Pausenmenue (200) sowieso.
        if (uiCanvas != null)
        {
            canvas.sortingLayerID = uiCanvas.sortingLayerID;
            canvas.sortingOrder = uiCanvas.sortingOrder - 1;
        }

        scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.referencePixelsPerUnit = 100f;

        group = gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        root = (RectTransform)transform;

        left = Group("Left");
        center = Group("Center");
        right = Group("Right");

        BuildLeft();
        BuildCenter();
        BuildRight();

        ApplyLayout(true);
    }

    private RectTransform Group(string name)
    {
        RectTransform r = NewRect(name, root);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        r.sizeDelta = new Vector2(RefW, RefH);
        return r;
    }

    // ---------- links ----------

    private void BuildLeft()
    {
        Img("Plate", left, PlateR, GameHudSkin.Plate, Color.white, true);

        // Leben
        heart = Img("Heart", left, HeartR, GameHudSkin.Heart, Color.white);
        hp = new Bar(this, "Hp", left, HpFrameR, GameHudSkin.Jam, GameHudSkin.JamLight, GameHudSkin.JamDark);

        // Schild aus Ueberheilung (Skilltree Geist): ein Glasurstreifen ueber
        // der oberen Haelfte des Balkens, von links - so bleibt das Rot darunter
        // lesbar und man sieht trotzdem sofort, wie viel Puffer da ist.
        shieldBar = Img("Shield", left, new RectInt(HpFrameR.x + 1, HpFrameR.y + 1, 1, ShieldH),
                        GameHudSkin.White, GameHudSkin.Icing).rectTransform;
        shieldHi = Img("ShieldHi", shieldBar, new RectInt(0, 0, 1, 1), GameHudSkin.White, GameHudSkin.IcingLight);
        RectTransform hiRect = shieldHi.rectTransform;
        hiRect.anchorMin = new Vector2(0f, 1f); hiRect.anchorMax = new Vector2(1f, 1f);
        hiRect.pivot = new Vector2(0f, 1f);
        hiRect.sizeDelta = new Vector2(0f, 1f);
        hiRect.anchoredPosition = Vector2.zero;
        shieldBar.gameObject.SetActive(false);
        for (int q = 1; q < 4; q++)
        {
            int x = HpFrameR.x + 1 + Mathf.RoundToInt(hp.InnerW * q / 4f);
            Color tick = GameHudSkin.Ink; tick.a = 0.35f;
            Img("Tick" + q, left, new RectInt(x, HpFrameR.y + 1, 1, HpFrameR.height - 2), GameHudSkin.White, tick);
        }
        hpText = Text("HpText", left, new RectInt(HpFrameR.x, HpFrameR.y, HpFrameR.width, HpFrameR.height),
                      SizeValue, GameHudSkin.Cream, TextAlignmentOptions.Center, TextStyle.Outline);

        // Erfahrung
        Img("Gem", left, GemR, GameHudSkin.Gem, Color.white);
        xp = new Bar(this, "Xp", left, XpFrameR, GameHudSkin.Icing, GameHudSkin.IcingLight, GameHudSkin.IcingDark);

        // Portraet: Hintergrund, Keks (beschnitten), Ring darueber
        portraitBack = Img("PortraitBack", left, PortraitR, GameHudSkin.MedallionBack, Color.white);
        RectTransform window = NewRect("PortraitWindow", left);
        Place(window, PortraitR);
        window.gameObject.AddComponent<RectMask2D>();
        portrait = Img("Portrait", window, new RectInt(0, 0, 24, 24), null, Color.white);
        portrait.enabled = false;
        portraitRect = portrait.rectTransform;
        portraitRect.pivot = new Vector2(0.5f, 0.5f);
        Img("Medallion", left, MedallionR, GameHudSkin.Medallion, Color.white);

        badge = Img("Badge", left, BadgeR, GameHudSkin.Badge, Color.white, true).rectTransform;
        SetPivotCenter(badge);
        badgeText = Text("BadgeText", badge, new RectInt(0, 0, BadgeR.width, BadgeR.height),
                         SizeValue, GameHudSkin.Ink, TextAlignmentOptions.Center, TextStyle.Plain);
    }

    // ---------- Mitte ----------

    private void BuildCenter()
    {
        // Zwei Schnuere, an denen das Schild vom Bildschirmrand haengt.
        Img("RopeL", center, new RectInt(SignR.x + 6, 0, 1, SignR.y + 2), GameHudSkin.White, GameHudSkin.Ink);
        Img("RopeR", center, new RectInt(SignR.xMax - 7, 0, 1, SignR.y + 2), GameHudSkin.White, GameHudSkin.Ink);

        Color shadow = new Color(0f, 0f, 0f, 0.35f);
        signShadow = Img("SignShadow", center, new RectInt(SignR.x + 1, SignR.y + 1, SignR.width, SignR.height),
                         GameHudSkin.Sign, shadow, true).rectTransform;
        sign = Img("Sign", center, SignR, GameHudSkin.Sign, Color.white, true).rectTransform;

        // Naegel, wo die Schnuere ansetzen
        Img("NailL", sign, new RectInt(5, 2, 2, 2), GameHudSkin.White, GameHudSkin.ParchDark);
        Img("NailR", sign, new RectInt(SignR.width - 7, 2, 2, 2), GameHudSkin.White, GameHudSkin.ParchDark);

        timerText = Text("Timer", sign, new RectInt(0, 0, SignR.width, SignR.height - 1),
                         SizeTimer, GameHudSkin.Cream, TextAlignmentOptions.Center);

        phaseText = Text("Phase", center, LabelR, SizeLabel, GameHudSkin.Parchment, TextAlignmentOptions.Center,
                         TextStyle.Outline);

        ribbonImage = Img("Ribbon", center, new RectInt(8, RibbonY, 80, RibbonH), GameHudSkin.Ribbon, Color.white, true);
        ribbon = ribbonImage.rectTransform;
        ribbonText = Text("RibbonText", ribbon, new RectInt(0, 0, 80, RibbonH), SizeRibbon,
                          GameHudSkin.Cream, TextAlignmentOptions.Center);
        ribbon.gameObject.SetActive(false);
    }

    // ---------- rechts ----------

    private void BuildRight()
    {
        goldChip = Img("GoldChip", right, new RectInt(0, GoldChipY, 30, GoldChipH), GameHudSkin.Plate,
                       Color.white, true).rectTransform;
        coin = Img("Coin", right, new RectInt(0, GoldChipY + CoinDY, CoinSize, CoinSize), GameHudSkin.Coin,
                   Color.white).rectTransform;
        goldText = Text("Gold", right, new RectInt(0, GoldChipY, RightW - Margin - 3, GoldChipH),
                        SizeGold, GameHudSkin.Gold, TextAlignmentOptions.Right, TextStyle.Outline);
        goldText.Set("0");
        FitGoldChip();
    }

    /// <summary>Chip rechtsbuendig, so breit wie Muenze plus Zahl - keine leere Mitte.</summary>
    private void FitGoldChip()
    {
        goldText.Main.ForceMeshUpdate();
        int textW = Mathf.CeilToInt(goldText.Main.preferredWidth);
        if (textW == goldTextWidth) return;
        goldTextWidth = textW;

        int w = CoinDX + CoinSize + 3 + textW + 4;
        int x = RightW - Margin - w;
        goldChip.anchoredPosition = new Vector2(x, -GoldChipY);
        goldChip.sizeDelta = new Vector2(w, GoldChipH);
        coin.anchoredPosition = new Vector2(x + CoinDX, -(GoldChipY + CoinDY));
    }

    // ==================================================================
    //  Laufzeit
    // ==================================================================

    private void LateUpdate()
    {
        ApplyLayout(false);

        float dt = Time.unscaledDeltaTime;
        float now = Time.unscaledTime;

        UpdateVisibility(dt);
        UpdatePortrait();
        UpdateHealth(dt, now);
        UpdateExperience(dt);
        UpdateTimer(now);
        UpdateLabel(now);
        UpdateGold(dt, now);
        UpdateSlots(now);
        UpdateShake(now);
    }

    /// <summary>
    /// Ganzzahlige Skalierung und die drei Gruppen an ihre Kanten - nur wenn
    /// sich die Bildschirmgroesse geaendert hat.
    /// </summary>
    private void ApplyLayout(bool force)
    {
        var size = new Vector2Int(Screen.width, Screen.height);
        if (!force && size == lastScreen) return;
        lastScreen = size;

        // Gerundet statt abgeschnitten: 1366x768 bekommt 3 statt 2, sonst waere
        // das HUD dort winzig. Die Gruppen passen bis ~400 HUD-Pixel Breite.
        int scale = Mathf.Max(1, Mathf.RoundToInt(Mathf.Min(size.x / (float)RefW, size.y / (float)RefH)));
        scaler.scaleFactor = scale;

        // Breite in HUD-Pixeln, abgerundet - damit auch rechts und in der
        // Mitte alles auf ganzen Pixeln landet.
        int w = size.x / scale;
        left.anchoredPosition = Vector2.zero;
        center.anchoredPosition = new Vector2((w - CenterW) / 2, 0f);
        right.anchoredPosition = new Vector2(w - RightW, 0f);
    }

    private void UpdateVisibility(float dt)
    {
        bool over = ui != null
                 && ((ui.GameOverPanel != null && ui.GameOverPanel.activeSelf)
                  || (ui.WinPanel != null && ui.WinPanel.activeSelf));
        float target = over ? 0f : 1f;
        group.alpha = Mathf.MoveTowards(group.alpha, target, dt * 4f);
    }

    // ---------- Portraet ----------

    private void UpdatePortrait()
    {
        PlayerController player = PlayerController.Instance;
        if (player == null) return;

        if (playerSprite == null)
        {
            PlayerHitFeedback feedback = player.GetComponent<PlayerHitFeedback>();
            playerSprite = feedback != null && feedback.playerSpriteRenderer != null
                ? feedback.playerSpriteRenderer
                : player.GetComponentInChildren<SpriteRenderer>();
            if (playerSprite == null) return;
            playerAnimator = playerSprite.GetComponent<Animator>();
        }

        Sprite s = IdleFrame();
        if (s == null) s = playerSprite.sprite;
        if (s == null) return;

        portrait.enabled = true;
        if (portrait.sprite != s)
        {
            portrait.sprite = s;
            // Auf 32 HUD-Pixel in ganzen Stufen: 64er-Frames halb (2 Bildpunkte
            // je Texel bei 1080p), 32er-Charaktere 1:1, 16er doppelt. Gemessen
            // am Koerper - der Zwiebelritter hat 64er-Zellen, ist aber ein 32er.
            Rect body = CharacterLooks.BodyRect(s);
            float tex = Mathf.Max(body.width, body.height);
            float k = tex <= 32f ? Mathf.Floor(32f / tex) : 1f / Mathf.Ceil(tex / 32f);
            int w = Mathf.Max(2, Mathf.RoundToInt(s.rect.width * k) & ~1);
            int h = Mathf.Max(2, Mathf.RoundToInt(s.rect.height * k) & ~1);
            // Der Koerper sitzt mittig im Medaillon, Ueberstehendes schneidet das Fenster ab.
            float dx = Mathf.Round((s.rect.width / 2f - body.center.x) * k);
            float dy = Mathf.Round((s.rect.height / 2f - body.center.y) * k);
            portraitRect.anchorMin = portraitRect.anchorMax = new Vector2(0.5f, 0.5f);
            portraitRect.sizeDelta = new Vector2(w, h);
            portraitRect.anchoredPosition = new Vector2(dx, 1f + dy);
        }

        bool alive = player.gameObject.activeInHierarchy;
        Color c = playerSprite.color;
        portrait.color = alive ? c : new Color(0.45f, 0.4f, 0.42f, 1f);
    }

    /// <summary>
    /// Das aktuelle Bild der Idle-Animation von vorn, aus dem Schatten-Animator.
    /// Null, wenn der Spieler keinen Animator hat - dann nimmt das Portraet
    /// sein echtes Bild.
    /// </summary>
    private Sprite IdleFrame()
    {
        if (playerAnimator == null || playerAnimator.runtimeAnimatorController == null) return null;

        if (idleAnimator == null)
        {
            var go = new GameObject("PortraitIdle");
            go.transform.SetParent(transform, false);
            idleSprite = go.AddComponent<SpriteRenderer>();
            idleSprite.enabled = false;   // nur Bildquelle, wird nie gezeichnet
            idleAnimator = go.AddComponent<Animator>();
            idleAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            // Atmet auch weiter, waehrend Level-Up oder Pause die Zeit anhalten.
            idleAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }

        RuntimeAnimatorController c = playerAnimator.runtimeAnimatorController;
        if (c != idleController)
        {
            idleController = c;
            idleAnimator.runtimeAnimatorController = c;
            idleAnimator.Play("Idle", 0, 0f);
        }

        // Die Idle-Blendtree waehlt die Richtung ueber LastMove: (0, -1) = vorn.
        idleAnimator.SetBool("moving", false);
        idleAnimator.SetFloat("LastMoveX", 0f);
        idleAnimator.SetFloat("LastMoveY", -1f);

        return idleSprite.sprite;
    }

    // ---------- Leben ----------

    private void UpdateHealth(float dt, float now)
    {
        PlayerController p = PlayerController.Instance;
        if (p == null) return;

        float max = Mathf.Max(1f, p.playerMaxHealth);
        float cur = Mathf.Clamp(p.playerHealth, 0f, max);
        float frac = cur / max;

        // Steigt nur das Maximum (Level-Up gibt +1), sinkt der Anteil, ohne
        // dass etwas getroffen hat - dann ohne Blitz und Wackeln nachziehen.
        bool tookDamage = lastHp >= 0f && cur < lastHp - 0.0001f;
        lastHp = cur;

        if (frac < hpShown - 0.0001f && !tookDamage)
        {
            // Kein Rest sichtbar? Dann zieht er gleich mit, statt nachzutropfen.
            if (!trailIsHeal && hpTrail <= hpShown + 0.0001f) hpTrail = frac;
            hpShown = frac;
        }
        else if (frac < hpShown - 0.0001f)
        {
            // Schaden: rot springt sofort, der helle Rest bleibt kurz stehen.
            if (trailIsHeal || hpTrail < hpShown) hpTrail = hpShown;
            trailIsHeal = false;
            hpShown = frac;
            trailHoldUntil = now + TrailHold;
            hitFlash = 0.12f;
            shakeUntil = now + 0.2f;
        }
        else if (frac > hpShown + 0.0001f)
        {
            // Heilung: gruen springt vor, rot laeuft hinterher.
            trailIsHeal = true;
            hpTrail = frac;
            hpShown = Mathf.MoveTowards(hpShown, frac, dt * HealRise);
        }
        else if (!trailIsHeal && now >= trailHoldUntil)
        {
            hpTrail = Mathf.MoveTowards(hpTrail, hpShown, dt * TrailDrain);
        }

        if (trailIsHeal && hpShown >= hpTrail - 0.0001f)
        {
            trailIsHeal = false;
            hpTrail = hpShown;
        }

        hitFlash = Mathf.Max(0f, hitFlash - dt);

        bool low = frac <= LowHealth && cur > 0f;
        bool blink = low && Mathf.Repeat(now * 2.5f, 1f) < 0.5f;

        Color main = hitFlash > 0f ? (Color)GameHudSkin.Cream
                   : blink ? (Color)GameHudSkin.JamLight
                   : (Color)GameHudSkin.Jam;
        hp.SetColors(main, hitFlash > 0f ? GameHudSkin.Cream : GameHudSkin.JamLight, GameHudSkin.JamDark);
        hp.SetFill(hpShown, cur > 0f);
        hp.SetTrail(hpTrail, trailIsHeal ? GameHudSkin.Mint : GameHudSkin.Rose);

        // Das Herz schlaegt - bei wenig Leben schnell und sichtbar.
        float beat = low ? 2.5f : 0.8f;
        bool up = Mathf.Repeat(now * beat, 1f) < (low ? 0.5f : 0.12f);
        SetPos(heart.rectTransform, HeartR.x, -(HeartR.y - (up ? 1 : 0)));

        portraitBack.color = low && blink ? new Color(1f, 0.72f, 0.72f, 1f) : Color.white;

        // Schild: Breite im selben Massstab wie das Leben, hoechstens der ganze Balken.
        int shieldW = Mathf.RoundToInt(Mathf.Clamp01(p.shield / max) * hp.InnerW);
        if (p.shield > 0f && shieldW < 1) shieldW = 1;
        bool shieldOn = shieldW > 0;
        if (shieldBar.gameObject.activeSelf != shieldOn) shieldBar.gameObject.SetActive(shieldOn);
        if (shieldOn) shieldBar.sizeDelta = new Vector2(shieldW, ShieldH);

        // Nur neu setzen, wenn sich eine Zahl aendert - kein String pro Frame.
        int shownCur = cur > 0f ? Mathf.CeilToInt(cur) : 0;
        int shownMax = Mathf.CeilToInt(max);
        int shownShield = p.shield > 0f ? Mathf.CeilToInt(p.shield) : 0;
        if (shownCur != lastHpCur || shownMax != lastHpMax || shownShield != lastShield)
        {
            lastHpCur = shownCur;
            lastHpMax = shownMax;
            lastShield = shownShield;
            // Ohne Farbcode: der Umriss-Text wuerde ihn mit einfaerben.
            hpText.Set(shownShield > 0
                ? shownCur + "+" + shownShield + " / " + shownMax
                : shownCur + " / " + shownMax);
        }
    }

    private void UpdateShake(float now)
    {
        // Ein Pixel hin und her, zwei Takte - genug, dass man den Treffer spuert.
        int dx = now < shakeUntil ? (Mathf.FloorToInt(now * 30f) % 2 == 0 ? 1 : -1) : 0;
        if (dx == lastShake) return;
        lastShake = dx;
        left.anchoredPosition = new Vector2(dx, 0f);
    }

    // ---------- Erfahrung ----------

    private void UpdateExperience(float dt)
    {
        PlayerController p = PlayerController.Instance;
        if (p == null) return;

        int level = p.currentLevel;
        bool maxed = p.maxLevel > 0 && level >= p.maxLevel;

        float need = 1f;
        if (p.playerLevels != null && level >= 1 && level - 1 < p.playerLevels.Count)
            need = Mathf.Max(1f, p.playerLevels[level - 1]);
        float frac = maxed ? 1f : Mathf.Clamp01(p.experience / need);

        if (level != shownLevel)
        {
            if (shownLevel >= 0 && level > shownLevel)
            {
                levelFlash = 0.35f;
                badgePop = 0.3f;
                xpShown = 0f;
            }
            shownLevel = level;
            badgeText.Set(Loc.Get("ui.hud.level", "LV") + " " + level);
        }

        xpShown = frac > xpShown ? Mathf.MoveTowards(xpShown, frac, dt * 2.5f) : frac;
        xp.SetFill(xpShown, frac > 0f);

        levelFlash = Mathf.Max(0f, levelFlash - dt);
        bool flash = levelFlash > 0f && Mathf.Repeat(levelFlash * 12f, 1f) < 0.5f;
        if (maxed) xp.SetColors(GameHudSkin.Gold, GameHudSkin.GoldLight, GameHudSkin.GoldDark);
        else if (flash) xp.SetColors(GameHudSkin.Cream, GameHudSkin.Cream, GameHudSkin.IcingLight);
        else xp.SetColors(GameHudSkin.Icing, GameHudSkin.IcingLight, GameHudSkin.IcingDark);

        badgePop = Mathf.Max(0f, badgePop - dt);
        float pop = 1f + Mathf.Round(badgePop / 0.3f * 4f) * 0.1f;   // in Stufen, nicht fliessend
        SetScale(badge, pop, pop);
    }

    // ---------- Uhr ----------

    private void UpdateTimer(float now)
    {
        GameManager gm = GameManager.Instance;
        float t = gm != null ? gm.gameTime : 0f;
        int second = Mathf.FloorToInt(t);

        if (second != lastSecond)
        {
            lastSecond = second;
            int minute = second / 60;
            timerText.Set(minute + ":" + (second % 60).ToString("00"));

            if (lastMinute >= 0 && minute != lastMinute) minuteBump = now + 0.25f;
            lastMinute = minute;
        }

        // Zur vollen Minute wippt das Schild kurz und die Ziffern leuchten.
        bool bump = now < minuteBump;
        int dy = bump ? 1 : 0;
        SetPos(sign, SignR.x, -(SignR.y + dy));
        SetPos(signShadow, SignR.x + 1, -(SignR.y + 1 + dy));
        timerText.SetColor(bump ? GameHudSkin.GoldLight : GameHudSkin.Cream);
    }

    // ---------- Phase / Ansage ----------

    private void UpdateLabel(float now)
    {
        SpawnDirector d = SpawnDirector.Active;
        string label = d != null ? d.CurrentLabel : "";
        bool announcing = d != null && d.IsAnnouncing;

        if (label != lastLabel || announcing != lastAnnouncing)
        {
            if (!announcing && !string.IsNullOrEmpty(lastLabel) && label != lastLabel) labelFlash = now + 0.6f;
            if (announcing && !lastAnnouncing) ribbonStart = now;

            lastLabel = label;
            lastAnnouncing = announcing;

            // Der Phasenname ("ANKUNFT") steht nicht mehr im HUD - Nick war er
            // zu viel. Nur Ansagen (Boss, "ACHTUNG!") kommen noch als Band.
            phaseText.Set("");
            ribbon.gameObject.SetActive(announcing);

            if (announcing)
            {
                ribbonText.Set(label.ToUpperInvariant());
                ribbonText.Main.ForceMeshUpdate();
                int w = Mathf.CeilToInt(ribbonText.Main.preferredWidth) + 12;
                w += w & 1;   // gerade, damit es in der Mitte aufgeht
                w = Mathf.Clamp(w, 30, CenterW);
                ribbon.sizeDelta = new Vector2(w, RibbonH);
                ribbon.anchoredPosition = new Vector2((CenterW - w) / 2, -RibbonY);
                ribbonText.Resize(w, RibbonH);
            }
        }

        if (announcing)
        {
            // Die erste halbe Sekunde blinkt das Band, danach wippt es sacht.
            float age = now - ribbonStart;
            bool on = age > 0.6f || Mathf.Repeat(age * 8f, 1f) < 0.6f;
            ribbonImage.color = on ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            int bob = age > 0.6f && Mathf.Repeat(now * 1.5f, 1f) < 0.5f ? 1 : 0;
            SetPos(ribbon, ribbon.anchoredPosition.x, -(RibbonY + bob));
        }
        else
        {
            bool flash = now < labelFlash && Mathf.Repeat(now * 8f, 1f) < 0.5f;
            phaseText.SetColor(flash ? GameHudSkin.GoldLight : GameHudSkin.Parchment);
        }
    }

    // ---------- Gold ----------

    private void UpdateGold(float dt, float now)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        if (now >= nextGoldPoll)
        {
            nextGoldPoll = now + 0.25f;
            goldTarget = gm.EstimateCurrency();
        }

        if (Mathf.Abs(goldShown - goldTarget) < 0.5f) goldShown = goldTarget;
        else goldShown = Mathf.MoveTowards(goldShown, goldTarget, Mathf.Max(8f, Mathf.Abs(goldTarget - goldShown) * 4f) * dt);

        int drawn = Mathf.RoundToInt(goldShown);
        if (drawn == goldDrawn) return;
        goldDrawn = drawn;
        if (goldText.Set(drawn.ToString())) FitGoldChip();
    }

    // ---------- Slots ----------

    private void UpdateSlots(float now)
    {
        PlayerController p = PlayerController.Instance;
        if (p == null) return;

        if (itemMenu == null)
            itemMenu = GameManager.Instance != null && GameManager.Instance.itemMenu != null
                ? GameManager.Instance.itemMenu
                : FindAnyObjectByType<ItemMenu>();
        if (itemMenuBuffs == null)
            itemMenuBuffs = GameManager.Instance != null && GameManager.Instance.itemMenuBuffs != null
                ? GameManager.Instance.itemMenuBuffs
                : FindAnyObjectByType<ItemMenuBuffs>();

        SyncRow(weaponViews, itemMenu != null ? itemMenu.CurrentSlots : null,
                Mathf.Clamp(p.WeaponSlots, 0, MaxSlots), p.EvoSlots, WeaponY, GameHudSkin.TileKind.Weapon, now);
        SyncRow(buffViews, itemMenuBuffs != null ? itemMenuBuffs.CurrentSlots : null,
                Mathf.Clamp(p.BuffSlots, 0, MaxSlots), 0, BuffY, GameHudSkin.TileKind.Buff, now);
    }

    private void SyncRow(List<SlotView> views, List<ItemSlotInfo> items, int count, int evoSlots,
                         int y, GameHudSkin.TileKind kind, float now)
    {
        while (views.Count < count)
        {
            int i = views.Count;
            views.Add(new SlotView(this, left, Margin + i * SlotStep, y, SlotSize, kind));
        }

        for (int i = 0; i < views.Count; i++)
        {
            SlotView v = views[i];
            bool on = i < count;
            if (v.Root.gameObject.activeSelf != on) v.Root.gameObject.SetActive(on);
            if (!on) continue;

            ItemSlotInfo item = items != null && i < items.Count ? items[i] : default;
            v.Apply(item, i < evoSlots, now);
        }
    }

    // ==================================================================
    //  Bausteine
    // ==================================================================

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0) go.layer = uiLayer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    /// <summary>Oben links verankert, Pixel im Raster des Elternteils.</summary>
    private static void Place(RectTransform r, RectInt px)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        r.sizeDelta = new Vector2(px.width, px.height);
        r.anchoredPosition = new Vector2(px.x, -px.y);
    }

    // Nur schreiben, was sich aendert - jede Zuweisung an ein RectTransform
    // stoesst sonst einen Neuaufbau des Canvas an, und das HUD laeuft jeden Frame.
    private static void SetPos(RectTransform r, float x, float y)
    {
        var p = new Vector2(x, y);
        if (r.anchoredPosition != p) r.anchoredPosition = p;
    }

    private static void SetSize(RectTransform r, float w, float h)
    {
        var s = new Vector2(w, h);
        if (r.sizeDelta != s) r.sizeDelta = s;
    }

    private static void SetScale(RectTransform r, float x, float y)
    {
        var s = new Vector3(x, y, 1f);
        if (r.localScale != s) r.localScale = s;
    }

    /// <summary>Pivot in die Mitte, ohne dass sich die Lage aendert - fuer Skalier-Effekte.</summary>
    private static void SetPivotCenter(RectTransform r)
    {
        Vector2 size = r.sizeDelta;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition += new Vector2(size.x * 0.5f, -size.y * 0.5f);
    }

    private Image Img(string name, Transform parent, RectInt px, Sprite sprite, Color color, bool sliced = false)
    {
        RectTransform r = NewRect(name, parent);
        Place(r, px);
        Image img = r.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        if (sliced)
        {
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
        }
        return img;
    }

    private enum TextStyle { Plain, Shadow, Outline }

    private Shadowed Text(string name, Transform parent, RectInt px, float size, Color color,
                          TextAlignmentOptions align, TextStyle style = TextStyle.Shadow)
    {
        return new Shadowed(this, name, parent, px, size, color, align, style);
    }

    private TextMeshProUGUI NewLabel(string name, Transform parent, RectInt px, float size, Color color,
                                     TextAlignmentOptions align)
    {
        RectTransform r = NewRect(name, parent);
        Place(r, px);
        TextMeshProUGUI t = r.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.enableAutoSizing = false;
        t.margin = Vector4.zero;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    /// <summary>
    /// Text mit hartem Schatten einen Pixel darunter oder mit einer 1-px-Kontur
    /// aus versetzten Kopien. Eine TMP-Kontur waere bei einer Pixelschrift
    /// weich und zu dick; die Kopien bleiben scharf und lesbar auf jedem Grund
    /// (auch auf dem hellen Schadensrest der Lebensleiste).
    /// </summary>
    private sealed class Shadowed
    {
        public readonly TextMeshProUGUI Main;
        private readonly GameHud hud;
        private readonly List<TextMeshProUGUI> backs = new List<TextMeshProUGUI>();
        private string text;

        private static readonly Vector2Int[] ShadowOffsets = { new Vector2Int(0, 1) };
        private static readonly Vector2Int[] OutlineOffsets =
        {
            new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(0, 1),
            new Vector2Int(1, 1),
        };

        public Shadowed(GameHud hud, string name, Transform parent, RectInt px, float size, Color color,
                        TextAlignmentOptions align, TextStyle style)
        {
            this.hud = hud;
            Vector2Int[] offsets = style == TextStyle.Outline ? OutlineOffsets
                                 : style == TextStyle.Shadow ? ShadowOffsets
                                 : new Vector2Int[0];
            foreach (Vector2Int o in offsets)
            {
                backs.Add(hud.NewLabel(name + "Back", parent,
                                       new RectInt(px.x + o.x, px.y + o.y, px.width, px.height),
                                       size, GameHudSkin.Ink, align));
            }
            Main = hud.NewLabel(name, parent, px, size, color, align);
        }

        /// <summary>Setzt den Text; true, wenn er sich geaendert hat.</summary>
        public bool Set(string value)
        {
            if (value == text) return false;
            text = value;

            TMP_FontAsset f = PickFont(value);
            if (f != null && Main.font != f)
            {
                Main.font = f;
                foreach (TextMeshProUGUI b in backs) b.font = f;
            }

            Main.text = value;
            foreach (TextMeshProUGUI b in backs) b.text = value;
            return true;
        }

        private TMP_FontAsset PickFont(string value)
        {
            if (hud.font == null || string.IsNullOrEmpty(value)) return hud.font;
            if (hud.textFont == null || hud.textFont == hud.font) return hud.font;
            return hud.font.HasCharacters(value, out uint[] _, false, true) ? hud.font : hud.textFont;
        }

        public void SetColor(Color c)
        {
            if (Main.color != c) Main.color = c;
        }

        public void Resize(float w, float h)
        {
            Main.rectTransform.sizeDelta = new Vector2(w, h);
            foreach (TextMeshProUGUI b in backs) b.rectTransform.sizeDelta = new Vector2(w, h);
        }
    }

    /// <summary>
    /// Eine Leiste: Rahmen, darin ein Rest (Schaden/Heilung) und die Fuellung
    /// aus drei Lagen - Grundfarbe, eine helle Zeile oben, eine dunkle unten.
    /// Breiten sind ganze Pixel, die Leiste waechst also in Pixelschritten.
    /// </summary>
    private sealed class Bar
    {
        public readonly int InnerW;
        private readonly int innerH;
        private readonly RectTransform fill, trail;
        private readonly Image main, hi, lo, trailImg;

        public Bar(GameHud hud, string name, Transform parent, RectInt frame, Color c, Color cHi, Color cLo)
        {
            hud.Img(name + "Frame", parent, frame, GameHudSkin.BarFrame, Color.white, true);
            InnerW = frame.width - 2;
            innerH = frame.height - 2;
            var inner = new RectInt(frame.x + 1, frame.y + 1, InnerW, innerH);

            trailImg = hud.Img(name + "Trail", parent, inner, GameHudSkin.White, GameHudSkin.Parchment);
            trail = trailImg.rectTransform;

            fill = NewRect(name + "Fill", parent);
            Place(fill, inner);
            main = Stretched(hud, "Main", fill, c, 0);
            hi = Stretched(hud, "Hi", fill, cHi, 1);
            lo = innerH >= 3 ? Stretched(hud, "Lo", fill, cLo, -1) : null;
            trail.gameObject.SetActive(false);
        }

        private static Image Stretched(GameHud hud, string name, RectTransform parent, Color color, int row)
        {
            Image img = hud.Img(name, parent, new RectInt(0, 0, 1, 1), GameHudSkin.White, color);
            RectTransform r = img.rectTransform;
            if (row == 0)
            {
                r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
                r.offsetMin = r.offsetMax = Vector2.zero;
            }
            else
            {
                float y = row > 0 ? 1f : 0f;
                r.anchorMin = new Vector2(0f, y); r.anchorMax = new Vector2(1f, y);
                r.pivot = new Vector2(0f, y);
                r.sizeDelta = new Vector2(0f, 1f);
                r.anchoredPosition = Vector2.zero;
            }
            return img;
        }

        public void SetFill(float frac, bool atLeastOne)
        {
            int w = Mathf.RoundToInt(Mathf.Clamp01(frac) * InnerW);
            if (atLeastOne && w < 1) w = 1;
            SetSize(fill, w, innerH);
            if (fill.gameObject.activeSelf != (w > 0)) fill.gameObject.SetActive(w > 0);
        }

        public void SetTrail(float frac, Color color)
        {
            int w = Mathf.RoundToInt(Mathf.Clamp01(frac) * InnerW);
            bool on = w > Mathf.RoundToInt(fill.sizeDelta.x);
            if (trail.gameObject.activeSelf != on) trail.gameObject.SetActive(on);
            if (!on) return;
            SetSize(trail, w, innerH);
            trailImg.color = color;
        }

        public void SetColors(Color c, Color cHi, Color cLo)
        {
            if (main.color != c) main.color = c;
            if (hi.color != cHi) hi.color = cHi;
            if (lo != null && lo.color != cLo) lo.color = cLo;
        }
    }

    /// <summary>
    /// Ein Waffen- oder Buff-Slot als dunkle Kachel: farbige Unterkante je nach
    /// Art, das Symbol 1:1, unten rechts die Stufe als Zahl (auf Max ein
    /// goldener Stern). Dazu ein weisser Blitz fuer Neues und ein Funkeln fuer Evos.
    /// </summary>
    private sealed class SlotView
    {
        public readonly RectTransform Root;
        private readonly int size;
        private readonly GameHudSkin.TileKind kind;
        private readonly Image frame, icon, flash, sparkle, star, evoGlow;
        private readonly RectTransform levelRect, levelChip;
        private readonly Shadowed levelText;

        private Sprite lastIcon;
        private int lastLevel = int.MinValue;
        private GameHudSkin.TileKind lastTile = (GameHudSkin.TileKind)(-1);
        private float popUntil, flashUntil, levelFlashUntil;

        private const float PopTime = 0.25f;
        private const float LevelFlashTime = 0.6f;
        private const int IconSize = 16;

        public SlotView(GameHud hud, Transform parent, int x, int y, int size, GameHudSkin.TileKind kind)
        {
            this.size = size;
            this.kind = kind;
            Root = NewRect("Slot", parent);
            Place(Root, new RectInt(x, y, size, size));

            frame = hud.Img("Tile", Root, new RectInt(0, 0, size, size), GameHudSkin.Tile(size, kind), Color.white);

            // Symbol mittig ueber der farbigen Unterkante (die ist 2 px hoch).
            var iconR = new RectInt((size - IconSize) / 2, (size - 3 - IconSize) / 2 + 1, IconSize, IconSize);
            icon = hud.Img("Icon", Root, iconR, null, Color.white);
            icon.preserveAspect = true;
            icon.enabled = false;
            SetPivotCenter(icon.rectTransform);

            flash = hud.Img("Flash", Root, new RectInt(2, 2, size - 4, size - 4), GameHudSkin.White,
                            new Color(1f, 1f, 1f, 0f));

            // Evo: ein Goldrahmen, der langsam atmet - ueber der Kachel, unter Stern und Stufe.
            evoGlow = hud.Img("EvoGlow", Root, new RectInt(-1, -1, size + 2, size + 2), GameHudSkin.Ring,
                              Color.clear, true);

            sparkle = hud.Img("Sparkle", Root, new RectInt(size - 5, 2, 3, 3), GameHudSkin.Sparkle, Color.white);
            sparkle.enabled = false;

            // Stufe: rechts unten, ragt 2 px ueber die Kachel hinaus - so steht
            // sie frei und verdeckt vom Symbol nur die Ecke.
            levelRect = NewRect("LevelBox", Root);
            Place(levelRect, new RectInt(0, size - 8, size + 2, 10));
            levelChip = hud.Img("Chip", levelRect, new RectInt(size - 6, 0, 8, 10), GameHudSkin.LevelChip,
                                Color.white, true).rectTransform;
            levelText = hud.Text("Level", levelRect, new RectInt(0, 0, size, 10), SizeValue,
                                 GameHudSkin.Cream, TextAlignmentOptions.Right, TextStyle.Shadow);
            star = hud.Img("Max", Root, new RectInt(size - 8, size - 8, 9, 9), GameHudSkin.Star, Color.white);
            star.enabled = false;
        }

        public void Apply(ItemSlotInfo item, bool evoSlot, float now)
        {
            Sprite s = item.Icon;
            bool has = s != null;

            GameHudSkin.TileKind tile = !has ? GameHudSkin.TileKind.Empty
                                      : evoSlot || item.IsEvo ? GameHudSkin.TileKind.Evo
                                      : kind;
            if (tile != lastTile)
            {
                lastTile = tile;
                frame.sprite = GameHudSkin.Tile(size, tile);
            }

            if (s != lastIcon)
            {
                if (has)
                {
                    popUntil = now + PopTime;
                    flashUntil = now + 0.3f;
                }
                lastIcon = s;
                icon.sprite = s;
                icon.enabled = has;
                lastLevel = has ? item.Level : int.MinValue;
            }
            else if (has && item.Level > lastLevel)
            {
                levelFlashUntil = now + LevelFlashTime;
                flashUntil = now + 0.15f;
                lastLevel = item.Level;
            }

            // Neues Symbol springt aus 140 % in zwei Stufen zurueck.
            float pop = popUntil > now ? (popUntil - now) / PopTime : 0f;
            float scale = pop > 0.5f ? 1.4f : pop > 0f ? 1.2f : 1f;
            SetScale(icon.rectTransform, scale, scale);

            float f = flashUntil > now ? (flashUntil - now) / 0.3f : 0f;
            flash.color = new Color(1f, 1f, 1f, Mathf.Round(f * 3f) / 3f * 0.8f);

            UpdateLevel(item, has, now);
            UpdateSparkle(tile == GameHudSkin.TileKind.Evo && has, now);
            UpdateEvoGlow(has && item.IsEvo, now);
        }

        private void UpdateLevel(ItemSlotInfo item, bool has, float now)
        {
            // Evos haben im Prefab maxweaponLevel 0 - sie sind fertig, also immer
            // der Stern. Sonst nur, wo es etwas zu zaehlen gibt.
            bool evo = has && item.IsEvo;
            bool counts = has && (item.MaxLevel > 0 || evo);
            bool maxed = counts && (evo || item.Level >= item.MaxLevel);

            if (star.enabled != maxed) star.enabled = maxed;

            // Die Zahl sitzt auf einem dunklen Schildchen, das mit ihr waechst.
            bool number = counts && !maxed;
            if (levelChip.gameObject.activeSelf != number) levelChip.gameObject.SetActive(number);
            if (levelText.Set(number ? (item.Level + 1).ToString() : "") && number)
            {
                levelText.Main.ForceMeshUpdate();
                int w = Mathf.CeilToInt(levelText.Main.preferredWidth) + 4;
                levelChip.sizeDelta = new Vector2(w, 10);
                levelChip.anchoredPosition = new Vector2(size + 2 - w, 0f);
            }

            // Stufenaufstieg: die Zahl leuchtet gold und hebt sich einen Pixel.
            bool glow = now < levelFlashUntil;
            bool blink = glow && Mathf.Repeat(now * 8f, 1f) < 0.5f;
            levelText.SetColor(blink ? Color.white : glow ? (Color)GameHudSkin.GoldLight : (Color)GameHudSkin.Cream);
            SetPos(levelRect, 0f, -(size - 8 - (glow ? 1 : 0)));
            SetPos(star.rectTransform, size - 8, -(size - 8 - (glow ? 1 : 0)));
        }

        private void UpdateEvoGlow(bool on, float now)
        {
            Color c = Color.clear;
            if (on)
            {
                // Drei Helligkeitsstufen statt eines weichen Verlaufs - Pixel-Look.
                float t = Mathf.Repeat(now * 0.8f + Root.anchoredPosition.x * 0.02f, 1f);
                c = GameHudSkin.Gold;
                c.a = t < 0.33f ? 0.85f : t < 0.66f ? 0.55f : 0.3f;
            }
            if (evoGlow.color != c) evoGlow.color = c;
        }

        private void UpdateSparkle(bool on, float now)
        {
            // Evos funkeln: kurz an, lange aus, reihum an drei Ecken.
            if (!on)
            {
                if (sparkle.enabled) sparkle.enabled = false;
                return;
            }

            float cycle = now * 0.7f + Root.anchoredPosition.x * 0.013f;
            int corner = Mathf.FloorToInt(cycle) % 3;
            bool visible = Mathf.Repeat(cycle, 1f) < 0.22f;
            sparkle.enabled = visible;
            if (!visible) return;

            Vector2Int pos = corner == 0 ? new Vector2Int(size - 5, 2)
                           : corner == 1 ? new Vector2Int(2, size - 8)
                           : new Vector2Int(2, 2);
            SetPos(sparkle.rectTransform, pos.x, -pos.y);
        }
    }
}
