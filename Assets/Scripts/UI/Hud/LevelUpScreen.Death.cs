using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Death-Screen des <see cref="LevelUpScreen"/>. Schaut wie die anderen
/// Ansichten nur zu: GameManager.ShowGameOverScreen macht GameOverPanel an,
/// hier wird es neu gezeichnet. Die Knoepfe rufen dieselben Methoden wie die
/// alten (GameManager.Restart / GoToMainMenu).
///
/// Ablauf in Stufen: Titel knallt herein, das Portraet taucht auf, die
/// Bilanz zaehlt Zeile fuer Zeile hoch, dann die Beute, der Build Kachel fuer
/// Kachel und zuletzt, was neu freigeschaltet wurde. Klick oder Taste
/// ueberspringt den Auftritt; erst danach zaehlen die Knoepfe.
/// </summary>
public partial class LevelUpScreen
{
    // ---- Masse (480x270) ----
    private const int DeathTitleY = 10;
    private const int PortraitX = 58, PortraitY = 56, PortraitS = 76;
    private const int StatsX = 172, StatsY = 54, StatsW = 262, StatsH = 64;
    private const int LootY = 124, LootH = 26;
    private const int BuildX = 46, BuildLabelY = 160, BuildTilesY = 172;
    private const int NewX = 262, NewW = 172;
    private const int DeathButtonsY = 228, DeathButtonW = 124, DeathButtonGap = 12;

    // ---- Zeitplan (Sekunden nach dem Oeffnen) ----
    private const float TTitle = 0.05f, TPortrait = 0.35f, TStats = 0.6f, TStatStep = 0.22f;
    private const float TLoot = 1.35f, TBuild = 2.0f, TNew = 2.3f, TButtons = 2.6f, TDone = 2.8f;

    // ---- Zustand ----
    private RectTransform deathLayer;
    private RectTransform deathRibbon, deathTailL, deathTailR, portraitRoot, statsRoot, buildLabelRoot, newLabelRoot;
    private PixText deathTitle, deathSubtitle, buildLabel, newLabel, portraitBadgeText;
    private Image portraitImage, portraitCrack;
    private RectTransform portraitBadge;
    private readonly List<DeathStatRow> statRows = new List<DeathStatRow>();
    private LootChip coinChip, soulChip;
    private readonly List<StripTileView> deathTiles = new List<StripTileView>();
    private readonly List<NewEntry> newEntries = new List<NewEntry>();
    private ButtonView hubButton, menuButton;
    private float deathOpened;
    private int deathTileCount, newCount;
    private Sprite deadSprite;

    // ==================================================================
    //  Aufbau
    // ==================================================================

    private void BuildDeath()
    {
        deathLayer = NewRect("Death", page);
        Place(deathLayer, new RectInt(0, 0, RefW, RefH));

        deathTailL = Img("TailL", deathLayer, new RectInt(0, DeathTitleY + 4, 12, 16), GameHudSkin.RibbonTail(true), Color.white).rectTransform;
        deathTailR = Img("TailR", deathLayer, new RectInt(0, DeathTitleY + 4, 12, 16), GameHudSkin.RibbonTail(false), Color.white).rectTransform;
        deathRibbon = Img("Ribbon", deathLayer, new RectInt(160, DeathTitleY, 160, TitleH), GameHudSkin.Ribbon, Color.white, true).rectTransform;
        deathTitle = Text("Title", deathRibbon, new RectInt(0, 0, 160, TitleH - 1), SizeTitle, GameHudSkin.Cream,
                          TextAlignmentOptions.Center, TextStyle.Shadow);
        deathSubtitle = Text("Subtitle", deathLayer, new RectInt(0, 38, RefW, SubtitleH), SizeText,
                             GameHudSkin.Parchment, TextAlignmentOptions.Center, TextStyle.Outline);

        // ---- Portraet ----
        portraitRoot = NewRect("Portrait", deathLayer);
        Place(portraitRoot, new RectInt(PortraitX, PortraitY, PortraitS, PortraitS));
        Img("Frame", portraitRoot, new RectInt(0, 0, PortraitS, PortraitS), GameHudSkin.Card, Color.white, true);
        int inner = PortraitS - 8;
        Img("BandTop", portraitRoot, new RectInt(4, 4, inner, 24), GameHudSkin.White, GameHudSkin.Parchment);
        Img("BandMid", portraitRoot, new RectInt(4, 28, inner, 22), GameHudSkin.White, GameHudSkin.ParchMid);
        Img("BandLow", portraitRoot, new RectInt(4, 50, inner, inner - 46), GameHudSkin.White, GameHudSkin.ParchDark);
        portraitImage = Img("Dead", portraitRoot, new RectInt(4, 4, inner, inner), null, Color.white);
        portraitImage.preserveAspect = true;
        portraitCrack = Img("Crack", portraitRoot, new RectInt(0, 0, PortraitS, PortraitS), GameHudSkin.Ring, Color.clear, true);
        portraitBadge = Img("Badge", portraitRoot, new RectInt(PortraitS / 2 - 20, PortraitS - 6, 40, BadgeH), GameHudSkin.Badge,
                            Color.white, true).rectTransform;
        portraitBadgeText = Text("BadgeText", portraitBadge, new RectInt(0, 0, 40, BadgeH), SizeText, GameHudSkin.Ink,
                                 TextAlignmentOptions.Center, TextStyle.Plain);

        // ---- Bilanz ----
        statsRoot = NewRect("Stats", deathLayer);
        Place(statsRoot, new RectInt(StatsX, StatsY, StatsW, StatsH));
        Img("Frame", statsRoot, new RectInt(0, 0, StatsW, StatsH), GameHudSkin.Card, Color.white, true);
        for (int i = 0; i < 3; i++)
            statRows.Add(new DeathStatRow(this, statsRoot, 6 + i * 19, StatsW));

        coinChip = new LootChip(this, deathLayer, new RectInt(StatsX, LootY, (StatsW - 6) / 2, LootH), GameHudSkin.Coin,
                                GameHudSkin.Gold, GameHudSkin.GoldLight);
        soulChip = new LootChip(this, deathLayer, new RectInt(StatsX + (StatsW + 6) / 2, LootY, (StatsW - 6) / 2, LootH),
                                GameHudSkin.Gem, GameHudSkin.Icing, GameHudSkin.IcingLight);

        // ---- Build ----
        buildLabel = Text("BuildLabel", deathLayer, new RectInt(BuildX, BuildLabelY, 200, 11), SizeText,
                          GameHudSkin.ParchDark, TextAlignmentOptions.Left, TextStyle.Outline);
        buildLabelRoot = buildLabel.Holder;
        for (int i = 0; i < 16; i++) deathTiles.Add(new StripTileView(this, deathLayer));

        // ---- Neu freigeschaltet ----
        newLabel = Text("NewLabel", deathLayer, new RectInt(NewX, BuildLabelY, NewW, 11), SizeText,
                        GameHudSkin.GoldLight, TextAlignmentOptions.Left, TextStyle.Outline);
        newLabelRoot = newLabel.Holder;
        for (int i = 0; i < 3; i++)
            newEntries.Add(new NewEntry(this, deathLayer, new RectInt(NewX, BuildTilesY + i * 17, NewW, 15)));

        int total = 2 * DeathButtonW + DeathButtonGap;
        int bx = (RefW - total) / 2;
        hubButton = new ButtonView(this, deathLayer, new RectInt(bx, DeathButtonsY, DeathButtonW, ButtonH), GameHudSkin.Arrow, "R");
        menuButton = new ButtonView(this, deathLayer, new RectInt(bx + DeathButtonW + DeathButtonGap, DeathButtonsY, DeathButtonW, ButtonH),
                                    null, "M");

        deathLayer.gameObject.SetActive(false);
    }

    // ==================================================================
    //  Oeffnen
    // ==================================================================

    private void EnterDeath(float now)
    {
        deathOpened = now;
        inputFrom = now + 0.5f;
        PlayerController p = PlayerController.Instance;
        GameManager gm = GameManager.Instance;

        // ---- Titel ----
        deathTitle.Set(Loc.Get("ui.death.title", "CRUMBLED!"));
        int w = Mathf.CeilToInt(deathTitle.Width) + 28;
        w += w & 1;
        int x = (RefW - w) / 2;
        deathRibbon.pivot = new Vector2(0f, 1f);
        deathRibbon.sizeDelta = new Vector2(w, TitleH);
        deathRibbon.anchoredPosition = new Vector2(x, -DeathTitleY);
        deathTitle.Resize(w, TitleH - 1);
        deathTailL.anchoredPosition = new Vector2(x - 8, -(DeathTitleY + 4));
        deathTailR.anchoredPosition = new Vector2(x + w - 4, -(DeathTitleY + 4));
        SetPivotMiddle(deathRibbon);
        deathSubtitle.Set(Loc.Get("ui.death.subtitle", "Your cookie crumbled - but your progress stays!"));

        // ---- Portraet: das Todesbild aus dem alten Panel ----
        if (deadSprite == null) deadSprite = FindDeadSprite();
        Sprite portrait = deadSprite;
        if (portrait == null && p != null)
        {
            SpriteRenderer sr = p.GetComponentInChildren<SpriteRenderer>(true);
            if (sr != null) portrait = sr.sprite;
        }
        portraitImage.sprite = portrait;
        portraitImage.enabled = portrait != null;
        if (portrait != null)
        {
            // Ganze Vergroesserung, bei grossen Bildern ein ganzer Teiler
            // (1/2, 1/3, ...) - so bleibt jeder Pixel gleich breit.
            int inner = PortraitS - 8;
            int sw = Mathf.RoundToInt(portrait.rect.width), sh = Mathf.RoundToInt(portrait.rect.height);
            int m = Mathf.Max(1, Mathf.Max(sw, sh));
            float scale = m <= inner ? Mathf.Max(1, inner / m) : 1f / Mathf.CeilToInt(m / (float)inner);
            int dw = Mathf.RoundToInt(sw * scale), dh = Mathf.RoundToInt(sh * scale);
            RectTransform pr = portraitImage.rectTransform;
            pr.sizeDelta = new Vector2(dw, dh);
            pr.anchoredPosition = new Vector2(4 + (inner - dw) / 2, -(4 + (inner - dh) / 2));
        }
        int level = p != null ? p.currentLevel : 1;
        portraitBadgeText.Set(Loc.Get("ui.hud.level", "LV") + " " + level);
        int bw = Mathf.CeilToInt(portraitBadgeText.Width) + 8;
        bw += bw & 1;
        portraitBadge.sizeDelta = new Vector2(bw, BadgeH);
        portraitBadge.anchoredPosition = new Vector2((PortraitS - bw) / 2, -(PortraitS - 6));
        portraitBadgeText.Resize(bw, BadgeH);

        // ---- Bilanz ----
        float time = gm != null ? gm.gameTime : 0f;
        statRows[0].Set(GameHudSkin.Clock, Loc.Get("ui.death.time", "SURVIVED"), time, v =>
        {
            int s = Mathf.FloorToInt(v);
            return (s / 60) + ":" + (s % 60).ToString("00");
        });
        statRows[1].Set(GameHudSkin.Skull, Loc.Get("ui.death.kills", "ENEMIES DEFEATED"), gm != null ? gm.kills : 0,
                        v => Mathf.RoundToInt(v).ToString(CultureInfo.InvariantCulture));
        statRows[2].Set(GameHudSkin.Star, Loc.Get("ui.death.level", "LEVEL REACHED"), level,
                        v => Mathf.RoundToInt(v).ToString(CultureInfo.InvariantCulture));

        coinChip.Set(Loc.Get("ui.death.coins", "COINS"), gm != null ? gm.currency : 0);
        // Charakter-XP aus diesem Lauf. Ist dabei ein Charakter-Level gefallen,
        // steht das neue Level statt der Ueberschrift da.
        int charLevel = Skills.Level;
        string charLabel = gm != null && charLevel > gm.charLevelBeforeGame
            ? string.Format(Loc.Get("ui.death.charlevelup", "CHAR LV {0}!"), charLevel)
            : Loc.Get("ui.death.charxp", "CHAR XP");
        soulChip.Set(charLabel, gm != null ? Mathf.RoundToInt(gm.charXpGained) : 0);

        // ---- Build ----
        buildLabel.Set(Loc.Get("ui.death.build", "YOUR BUILD"));
        var weapons = new List<Weapon>();
        var buffs = new List<Weapon>();
        if (p != null)
        {
            if (p.activeEvos != null) foreach (Weapon wpn in p.activeEvos) if (Owned(wpn)) weapons.Add(wpn);
            if (p.activeWeapon != null) foreach (Weapon wpn in p.activeWeapon) if (Owned(wpn)) weapons.Add(wpn);
            if (p.activeBuffs != null) foreach (Weapon wpn in p.activeBuffs) if (Owned(wpn)) buffs.Add(wpn);
        }
        deathTileCount = 0;
        int tx = BuildX;
        foreach (Weapon wpn in weapons)
        {
            if (deathTileCount >= deathTiles.Count || tx > NewX - 30) break;
            deathTiles[deathTileCount++].Set(tx, BuildTilesY, wpn, IsEvo(wpn) ? GameHudSkin.TileKind.Evo : GameHudSkin.TileKind.Weapon);
            tx += StripStep;
        }
        if (weapons.Count > 0 && buffs.Count > 0) tx += 6;
        int rowStart = BuildX;
        int ty = BuildTilesY;
        foreach (Weapon wpn in buffs)
        {
            if (deathTileCount >= deathTiles.Count) break;
            if (tx > NewX - 30)
            {
                // Zweite Reihe, falls der Build breit ist.
                tx = rowStart;
                ty += StripStep + 2;
            }
            deathTiles[deathTileCount++].Set(tx, ty, wpn, GameHudSkin.TileKind.Buff);
            tx += StripStep;
        }
        for (int i = 0; i < deathTiles.Count; i++) deathTiles[i].Root.gameObject.SetActive(false);

        // ---- Neu freigeschaltet ----
        var items = new List<(Sprite icon, string name)>();
        SessionProgressTracker tracker = SessionProgressTracker.Instance;
        if (tracker != null)
        {
            foreach (AchievementDef def in tracker.newAchievements)
                if (def != null) items.Add((def.Icon, def.Name));
            foreach (string id in tracker.newUnlocks)
            {
                UnlockDef u = Unlocks.Find(id);
                if (u != null) items.Add((u.Icon, u.Name));
            }
        }
        newCount = Mathf.Min(items.Count, newEntries.Count);
        newLabel.Set(items.Count > 0
            ? Loc.Get("ui.death.new", "NEW UNLOCKS") + (items.Count > newEntries.Count ? "  (+" + (items.Count - newEntries.Count) + ")" : "")
            : Loc.Get("ui.death.new_none", "NOTHING NEW - NEXT TIME!"));
        newLabel.SetColor(items.Count > 0 ? GameHudSkin.GoldLight : GameHudSkin.StoneLight);
        for (int i = 0; i < newEntries.Count; i++)
        {
            bool on = i < newCount;
            if (on) newEntries[i].Set(items[i].icon, items[i].name);
            newEntries[i].Root.gameObject.SetActive(false);
        }

        // ---- Knoepfe ----
        bool toHub = !string.IsNullOrEmpty(GameSession.ReturnScene);
        hubButton.SetLabel(toHub ? Loc.Get("ui.death.to_hub", "TO THE HUB") : Loc.Get("ui.death.again", "AGAIN"));
        hubButton.SetCount("", false);
        menuButton.SetLabel(Loc.Get("ui.death.menu", "MAIN MENU"));
        menuButton.SetCount("", false);
    }

    /// <summary>Das gemalte Todesbild haengt als SpriteRenderer im alten Panel - Bild merken, Renderer aus.</summary>
    private Sprite FindDeadSprite()
    {
        Sprite found = null;
        if (ui.GameOverPanel == null) return null;
        foreach (SpriteRenderer sr in ui.GameOverPanel.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (found == null && sr.sprite != null) found = sr.sprite;
            sr.enabled = false;
        }
        return found;
    }

    private static void SetPivotMiddle(RectTransform r)
    {
        Vector2 size = r.sizeDelta;
        Vector2 pos = r.anchoredPosition;
        if (r.pivot == new Vector2(0.5f, 0.5f)) return;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos + new Vector2(size.x * 0.5f, -size.y * 0.5f);
    }

    // ==================================================================
    //  Laufzeit
    // ==================================================================

    private void UpdateDeath(float now, Vector2 mouse)
    {
        float t = now - deathOpened;

        // Etwas roetlicher und dunkler als die Auswahl-Fenster.
        Color bc = backdrop.color;
        backdrop.color = new Color(0.11f, 0.04f, 0.06f, Mathf.MoveTowards(bc.a, 0.96f, Time.unscaledDeltaTime * 3f));

        // Klick oder Taste waehrend des Auftritts: sofort alles zeigen.
        if (t < TDone && now >= inputFrom
            && (Input.GetMouseButtonDown(0) || Input.anyKeyDown))
        {
            deathOpened = now - TDone;
            t = TDone;
            inputFrom = now + 0.25f;
        }

        // ---- Titel: faellt gross herein und setzt hart auf ----
        float tt = t - TTitle;
        float s = tt < 0f ? 0f : tt < 0.06f ? 1.8f : tt < 0.12f ? 1.3f : tt < 0.18f ? 0.95f : 1f;
        deathRibbon.localScale = new Vector3(s, s, 1f);
        bool ribbonOn = tt >= 0f;
        SetActive(deathRibbon, ribbonOn);
        SetActive(deathTailL, ribbonOn && tt >= 0.12f);
        SetActive(deathTailR, ribbonOn && tt >= 0.12f);
        deathTitle.SetColor(tt >= 0f && tt < 0.5f && Mathf.Repeat(tt * 10f, 1f) < 0.5f
                                ? (Color)GameHudSkin.JamLight : (Color)GameHudSkin.Cream);
        deathSubtitle.SetActive(t > TPortrait);

        // Einschlag: die ganze Seite ruckt kurz.
        int shake = tt > 0.12f && tt < 0.3f ? (Mathf.FloorToInt(now * 30f) % 2 == 0 ? 2 : -2) : 0;
        SetPos(deathLayer, shake, 0f);

        // ---- Portraet ----
        float pt = t - TPortrait;
        SetActive(portraitRoot, pt >= 0f);
        if (pt >= 0f)
        {
            int rise = pt < 0.06f ? 10 : pt < 0.12f ? 4 : pt < 0.18f ? -1 : 0;
            SetPos(portraitRoot, PortraitX, -(PortraitY + rise));
            // Das Portraet pocht zweimal rot - dann bleibt es grau.
            bool pulse = pt < 0.8f && Mathf.Repeat(pt * 5f, 1f) < 0.5f;
            SetColor(portraitCrack, pulse ? (Color)GameHudSkin.Jam : new Color(0f, 0f, 0f, 0f));
            SetColor(portraitImage, pt < 0.8f ? Color.white : new Color(0.82f, 0.78f, 0.8f, 1f));
        }

        // ---- Bilanz ----
        SetActive(statsRoot, t >= TStats - 0.1f);
        for (int i = 0; i < statRows.Count; i++)
            statRows[i].Animate(now, t - TStats - i * TStatStep);

        // ---- Beute ----
        coinChip.Animate(now, t - TLoot);
        soulChip.Animate(now, t - TLoot - 0.15f);

        // ---- Build ----
        SetActive(buildLabelRoot, t >= TBuild && deathTileCount > 0);
        for (int i = 0; i < deathTileCount; i++)
        {
            float bt = t - TBuild - i * 0.05f;
            StripTileView v = deathTiles[i];
            SetActive(v.Root, bt >= 0f);
            if (bt >= 0f) v.Highlight(Color.clear, false, false, null, now);
        }

        // ---- Neu ----
        SetActive(newLabelRoot, t >= TNew);
        for (int i = 0; i < newCount; i++)
            newEntries[i].Animate(now, t - TNew - i * 0.12f);

        // ---- Knoepfe ----
        bool buttons = t >= TButtons;
        SetActive(hubButton.Root, buttons);
        SetActive(menuButton.Root, buttons);
        if (!buttons) return;

        bool live = now >= inputFrom && t >= TDone;
        bool hub = hubButton.Update(now, mouse, live);
        bool menu = menuButton.Update(now, mouse, live);
        if (live)
        {
            if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Return)
                || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) hub = true;
            if (Input.GetKeyDown(KeyCode.M)) menu = true;
        }

        GameManager gm = GameManager.Instance;
        if (gm == null) return;
        if (hub)
        {
            Click();
            inputFrom = now + 5f;   // doppelt ausloesen verhindern
            gm.Restart();
        }
        else if (menu)
        {
            Click();
            inputFrom = now + 5f;
            gm.GoToMainMenu();
        }
    }

    // ==================================================================
    //  Bausteine
    // ==================================================================

    /// <summary>Zeile der Bilanz: Symbol, Beschriftung, Wert zaehlt hoch.</summary>
    private sealed class DeathStatRow
    {
        private readonly RectTransform root;
        private readonly Image icon;
        private readonly PixText label, value;
        private readonly int y;
        private float target;
        private System.Func<float, string> format;

        public DeathStatRow(LevelUpScreen s, Transform parent, int y, int width)
        {
            this.y = y;
            root = NewRect("Row", parent);
            Place(root, new RectInt(0, y, width, 16));
            s.Img("Well", root, new RectInt(4, 0, width - 8, 16), GameHudSkin.Well, Color.white, true);
            icon = s.Img("Icon", root, new RectInt(9, 3, 9, 9), null, Color.white);
            label = s.Text("Label", root, new RectInt(23, 2, 150, 12), SizeText, GameHudSkin.ParchDark,
                           TextAlignmentOptions.Left, TextStyle.Shadow);
            value = s.Text("Value", root, new RectInt(width - 110, 2, 100, 12), SizeText, GameHudSkin.Cream,
                           TextAlignmentOptions.Right, TextStyle.Outline);
        }

        public void Set(Sprite sprite, string text, float v, System.Func<float, string> fmt)
        {
            icon.sprite = sprite;
            int w = Mathf.RoundToInt(sprite.rect.width), h = Mathf.RoundToInt(sprite.rect.height);
            icon.rectTransform.sizeDelta = new Vector2(w, h);
            icon.rectTransform.anchoredPosition = new Vector2(9 + (9 - w) / 2, -(8 - h / 2));
            label.Set(text);
            target = v;
            format = fmt;
            value.Set(fmt(0f));
        }

        public void Animate(float now, float t)
        {
            SetActive(root, t >= 0f);
            if (t < 0f) return;
            float k = Mathf.Clamp01(t / 0.5f);
            k = 1f - (1f - k) * (1f - k);
            value.Set(format(target * k));
            bool done = k >= 1f;
            // Fertig gezaehlt: Wert blitzt einmal gold.
            bool flash = done && t < 0.75f;
            value.SetColor(flash ? (Color)GameHudSkin.GoldLight : (Color)GameHudSkin.Cream);
            int slide = t < 0.05f ? 6 : t < 0.1f ? 2 : 0;
            SetPos(root, slide, -y);
        }
    }

    /// <summary>Beute-Chip: grosses Symbol, Zahl zaehlt hoch, am Ende Funkeln.</summary>
    private sealed class LootChip
    {
        private readonly RectTransform root;
        private readonly Image ring, sparkle;
        private readonly PixText label, value;
        private readonly RectInt rect;
        private readonly Color accent, accentLight;
        private int target;

        public LootChip(LevelUpScreen s, Transform parent, RectInt r, Sprite iconSprite, Color accent, Color accentLight)
        {
            rect = r;
            this.accent = accent;
            this.accentLight = accentLight;
            root = NewRect("Loot", parent);
            Place(root, r);
            s.Img("Frame", root, new RectInt(0, 0, r.width, r.height), GameHudSkin.Card, Color.white, true);

            // Symbol doppelt gross in einer Senke.
            s.Img("Well", root, new RectInt(4, 4, 18, 18), GameHudSkin.Well, Color.white, true);
            int iw = Mathf.RoundToInt(iconSprite.rect.width) * 2, ih = Mathf.RoundToInt(iconSprite.rect.height) * 2;
            s.Img("Icon", root, new RectInt(4 + (18 - iw) / 2, 4 + (18 - ih) / 2, iw, ih), iconSprite, Color.white);

            label = s.Text("Label", root, new RectInt(26, 2, r.width - 30, 11), SizeText, GameHudSkin.ParchDark,
                           TextAlignmentOptions.Left, TextStyle.Shadow);
            value = s.Text("Value", root, new RectInt(26, 12, r.width - 32, 12), SizeText, accentLight,
                           TextAlignmentOptions.Left, TextStyle.Outline);
            ring = s.Img("Ring", root, new RectInt(0, 0, r.width, r.height), GameHudSkin.Ring, Color.clear, true);
            sparkle = s.Img("Sparkle", root, new RectInt(r.width - 8, 3, 3, 3), GameHudSkin.Sparkle, Color.white);
        }

        public void Set(string text, int amount)
        {
            label.Set(text);
            target = Mathf.Max(0, amount);
            value.Set("+0");
        }

        public void Animate(float now, float t)
        {
            SetActive(root, t >= 0f);
            if (t < 0f) return;
            float k = Mathf.Clamp01(t / 0.8f);
            k = 1f - (1f - k) * (1f - k) * (1f - k);
            value.Set("+" + Mathf.RoundToInt(target * k).ToString(CultureInfo.InvariantCulture));

            bool done = k >= 1f;
            float after = t - 0.8f;
            Color rc = Color.clear;
            if (done && after < 0.4f && target > 0) rc = accentLight;
            else if (done && target > 0)
            {
                rc = accent;
                rc.a = Mathf.Repeat(now * 1.2f + rect.x * 0.01f, 1f) < 0.5f ? 0.45f : 0.2f;
            }
            SetColor(ring, rc);

            float cycle = now * 0.9f + rect.x * 0.013f;
            sparkle.enabled = done && target > 0 && Mathf.Repeat(cycle, 1f) < 0.25f;
            int pop = t < 0.05f ? 3 : t < 0.1f ? 1 : 0;
            SetPos(root, rect.x, -(rect.y + pop));
        }
    }

    /// <summary>Eintrag "neu freigeschaltet": Symbol und Name auf einer kleinen Karte.</summary>
    private sealed class NewEntry
    {
        public readonly RectTransform Root;
        private readonly Image icon, flash;
        private readonly PixText name;
        private readonly RectInt rect;

        public NewEntry(LevelUpScreen s, Transform parent, RectInt r)
        {
            rect = r;
            Root = NewRect("New", parent);
            Place(Root, r);
            s.Img("Frame", Root, new RectInt(0, 0, r.width, r.height), GameHudSkin.Card, Color.white, true);
            icon = s.Img("Icon", Root, new RectInt(3, 2, 11, 11), null, Color.white);
            icon.preserveAspect = true;
            name = s.Text("Name", Root, new RectInt(17, 1, r.width - 20, 12), SizeText, GameHudSkin.Cream,
                          TextAlignmentOptions.Left, TextStyle.Shadow);
            flash = s.Img("Flash", Root, new RectInt(1, 1, r.width - 2, r.height - 2), GameHudSkin.White, new Color(1f, 1f, 1f, 0f));
        }

        public void Set(Sprite sprite, string text)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            name.Set((text ?? "").ToUpperInvariant());
            // Zu lang fuer die Karte? Dann kuerzen.
            string t = name.Main.text;
            while (name.Width > rect.width - 22 && t.Length > 4)
            {
                t = t.Substring(0, t.Length - 2);
                name.Set(t + ".");
            }
        }

        public void Animate(float now, float t)
        {
            SetActive(Root, t >= 0f);
            if (t < 0f) return;
            float f = t < 0.25f ? 1f - t / 0.25f : 0f;
            SetColor(flash, new Color(1f, 1f, 1f, Mathf.Round(f * 3f) / 3f * 0.8f));
            int slide = t < 0.05f ? 8 : t < 0.1f ? 3 : 0;
            SetPos(Root, rect.x + slide, -rect.y);
        }
    }
}
