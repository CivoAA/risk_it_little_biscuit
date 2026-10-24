using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Opferschrein-Ansicht des <see cref="LevelUpScreen"/>: alles, was der
/// Spieler traegt, in zwei Reihen (Waffen mit Evos, Buffs). Ein Klick waehlt
/// aus, unten steht, was das Opfer kostet, OPFERN fuehrt es aus.
///
/// Offen ist sie, solange <see cref="SacrificeShrine.Current"/> gesetzt ist -
/// der Schrein haelt das Spiel an, dieses Fenster schliesst ihn wieder
/// (<see cref="SacrificeShrine.Close"/>). Die Regeln stehen in <see cref="Sacrifice"/>.
///
/// Startwaffe und Evos stehen gesperrt mit Schloss da. Geht mit dem Opfer eine
/// Evo verloren, deren andere Zutat schon im Inventar liegt, fragt OPFERN
/// einmal nach ("SICHER?").
///
/// Tasten: Pfeile/A/D/W/S waehlen, Enter/Leertaste opfert, Esc geht.
/// </summary>
public partial class LevelUpScreen
{
    // ---- Masse (480x270) ----
    private const int ShrineTile = 40, ShrineStep = 46, ShrineMaxRow = 9;
    private const int ShrineWeaponLabelY = 54, ShrineWeaponY = 65;
    private const int ShrineBuffLabelY = 110, ShrineBuffY = 121;
    private const int ShrineInfoY = 168, ShrineInfoW = 320, ShrineInfoH = 38;
    private const int ShrineButtonsY = 214;

    // ---- Zustand ----
    private RectTransform shrineLayer, shrineRibbon, shrineTailL, shrineTailR;
    private PixText shrineTitle, shrineSubtitle, shrineWeaponLabel, shrineBuffLabel;
    private PixText shrineInfoName, shrineInfoCost, shrineInfoNote;
    private ButtonView shrineSacrificeButton, shrineLeaveButton;
    private readonly List<ShrineTileView> shrineTiles = new List<ShrineTileView>();
    private readonly List<Weapon> shrineItems = new List<Weapon>();
    private readonly List<Weapon> shrineWeapons = new List<Weapon>();
    private readonly List<Weapon> shrineBuffs = new List<Weapon>();
    private int shrineWeaponCount;
    private int shrineSel = -1, shrineHover = -1;
    private bool shrineArmed;   // Evo-Warnung gezeigt, naechstes OPFERN gilt

    // ==================================================================
    //  Aufbau
    // ==================================================================

    private void BuildShrine()
    {
        shrineLayer = NewRect("Shrine", page);
        Place(shrineLayer, new RectInt(0, 0, RefW, RefH));

        shrineTailL = Img("TailL", shrineLayer, new RectInt(0, TitleY + 4, 12, 16), GameHudSkin.RibbonTail(true), Color.white).rectTransform;
        shrineTailR = Img("TailR", shrineLayer, new RectInt(0, TitleY + 4, 12, 16), GameHudSkin.RibbonTail(false), Color.white).rectTransform;
        shrineRibbon = Img("Ribbon", shrineLayer, new RectInt(180, TitleY, 120, TitleH), GameHudSkin.Ribbon, Color.white, true).rectTransform;
        shrineTitle = Text("Title", shrineRibbon, new RectInt(0, 0, 120, TitleH - 1), SizeTitle, GameHudSkin.Cream,
                           TextAlignmentOptions.Center, TextStyle.Shadow);

        shrineSubtitle = Text("Subtitle", shrineLayer, new RectInt(0, SubtitleY - 6, RefW, SubtitleH), SizeText,
                              GameHudSkin.Parchment, TextAlignmentOptions.Center, TextStyle.Outline);

        shrineWeaponLabel = Text("WeaponsLabel", shrineLayer, new RectInt(0, ShrineWeaponLabelY, RefW, SubtitleH), SizeText,
                                 GameHudSkin.Gold, TextAlignmentOptions.Center, TextStyle.Outline);
        shrineBuffLabel = Text("BuffsLabel", shrineLayer, new RectInt(0, ShrineBuffLabelY, RefW, SubtitleH), SizeText,
                               GameHudSkin.Mint, TextAlignmentOptions.Center, TextStyle.Outline);

        int ix = (RefW - ShrineInfoW) / 2;
        Img("InfoBg", shrineLayer, new RectInt(ix, ShrineInfoY, ShrineInfoW, ShrineInfoH), GameHudSkin.Card, Color.white, true);
        shrineInfoName = Text("InfoName", shrineLayer, new RectInt(ix, ShrineInfoY + 2, ShrineInfoW, 12), SizeText,
                              GameHudSkin.Cream, TextAlignmentOptions.Center, TextStyle.Shadow);
        shrineInfoCost = Text("InfoCost", shrineLayer, new RectInt(ix, ShrineInfoY + 13, ShrineInfoW, 12), SizeText,
                              GameHudSkin.JamLight, TextAlignmentOptions.Center, TextStyle.Shadow);
        shrineInfoNote = Text("InfoNote", shrineLayer, new RectInt(ix, ShrineInfoY + 24, ShrineInfoW, 12), SizeText,
                              GameHudSkin.ParchDark, TextAlignmentOptions.Center, TextStyle.Shadow);

        int bx = (RefW - 2 * ButtonW - ButtonGap) / 2;
        shrineSacrificeButton = new ButtonView(this, shrineLayer, new RectInt(bx, ShrineButtonsY, ButtonW, ButtonH),
                                               GameHudSkin.Skull, null);
        shrineLeaveButton = new ButtonView(this, shrineLayer, new RectInt(bx + ButtonW + ButtonGap, ShrineButtonsY, ButtonW, ButtonH),
                                           null, "ESC");

        shrineLayer.gameObject.SetActive(false);
    }

    // ==================================================================
    //  Laufzeit
    // ==================================================================

    private void EnterShrine(float now)
    {
        openedAt = now;
        inputFrom = now + InputDelay;
        toastUntil = 0f;
        shrineSel = -1;
        shrineHover = -1;
        shrineArmed = false;

        shrineTitle.Set(Loc.Get("ui.shrine.title", "SACRIFICE"));
        int w = Mathf.CeilToInt(shrineTitle.Width) + 24;
        w += w & 1;
        int x = (RefW - w) / 2;
        shrineRibbon.sizeDelta = new Vector2(w, TitleH);
        shrineRibbon.anchoredPosition = new Vector2(x, -TitleY);
        shrineTitle.Resize(w, TitleH - 1);
        shrineTailL.anchoredPosition = new Vector2(x - 8, -(TitleY + 4));
        shrineTailR.anchoredPosition = new Vector2(x + w - 4, -(TitleY + 4));

        shrineWeaponLabel.Set(Loc.Get("ui.shrine.weapons", "WEAPONS"));
        shrineBuffLabel.Set(Loc.Get("ui.shrine.buffs", "BUFFS"));
        shrineSacrificeButton.SetLabel(Loc.Get("ui.shrine.sacrifice", "SACRIFICE"));
        shrineLeaveButton.SetLabel(Loc.Get("ui.shrine.leave", "LEAVE"));

        LayoutShrine();

        // Gleich das erste opferbare Teil vorwaehlen - Enter allein soll nie ins Leere gehen.
        for (int i = 0; i < shrineItems.Count; i++)
            if (Sacrifice.Check(shrineItems[i]) == Sacrifice.Block.None) { shrineSel = i; break; }
    }

    private void LayoutShrine()
    {
        Sacrifice.CollectOwned(shrineWeapons, shrineBuffs);
        if (shrineWeapons.Count > ShrineMaxRow) shrineWeapons.RemoveRange(ShrineMaxRow, shrineWeapons.Count - ShrineMaxRow);
        if (shrineBuffs.Count > ShrineMaxRow) shrineBuffs.RemoveRange(ShrineMaxRow, shrineBuffs.Count - ShrineMaxRow);

        shrineItems.Clear();
        shrineItems.AddRange(shrineWeapons);
        shrineItems.AddRange(shrineBuffs);
        shrineWeaponCount = shrineWeapons.Count;

        while (shrineTiles.Count < shrineItems.Count) shrineTiles.Add(new ShrineTileView(this, shrineLayer));
        for (int i = 0; i < shrineTiles.Count; i++)
            shrineTiles[i].Root.gameObject.SetActive(i < shrineItems.Count);

        PlaceShrineRow(0, shrineWeapons.Count, ShrineWeaponY);
        PlaceShrineRow(shrineWeaponCount, shrineBuffs.Count, ShrineBuffY);

        shrineWeaponLabel.SetActive(shrineWeapons.Count > 0);
        shrineBuffLabel.SetActive(shrineBuffs.Count > 0);
    }

    private void PlaceShrineRow(int first, int count, int y)
    {
        int total = count * ShrineStep - (count > 0 ? ShrineStep - ShrineTile : 0);
        int x0 = (RefW - total) / 2;
        for (int i = 0; i < count; i++)
        {
            Weapon w = shrineItems[first + i];
            GameHudSkin.TileKind kind = Sacrifice.IsEvo(w) ? GameHudSkin.TileKind.Evo
                                      : Sacrifice.IsBuff(w) ? GameHudSkin.TileKind.Buff
                                      : GameHudSkin.TileKind.Weapon;
            shrineTiles[first + i].Set(new RectInt(x0 + i * ShrineStep, y, ShrineTile, ShrineTile), w, kind,
                                       Sacrifice.Check(w) != Sacrifice.Block.None);
        }
    }

    private void UpdateShrine(float now, Vector2 mouse, bool mouseMoved)
    {
        bool inputOpen = now >= inputFrom;

        shrineHover = -1;
        for (int i = 0; i < shrineItems.Count; i++)
            if (Hit(shrineTiles[i].Rect, mouse)) shrineHover = i;

        if (inputOpen)
        {
            if (Input.GetMouseButtonDown(0)) pressed = shrineHover;
            if (Input.GetMouseButtonUp(0))
            {
                if (pressed >= 0 && pressed == shrineHover) SelectShrine(shrineHover);
                pressed = -1;
            }
            HandleShrineKeys();
        }

        if (SacrificeShrine.Current == null) return;   // eben geopfert oder gegangen

        for (int i = 0; i < shrineItems.Count; i++)
            shrineTiles[i].Animate(now, i == shrineSel, i == shrineHover);

        UpdateShrineHead(now);
        UpdateShrineInfo(now);
        UpdateShrineButtons(now, mouse, inputOpen);
    }

    private void HandleShrineKeys()
    {
        int n = shrineItems.Count;
        if (n > 0)
        {
            int move = 0;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) move = -1;
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) move = 1;
            if (move != 0)
                SelectShrine(shrineSel < 0 ? (move > 0 ? 0 : n - 1) : (shrineSel + move + n) % n);

            // Hoch/runter: in die andere Reihe, moeglichst an dieselbe Stelle.
            bool up = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
            bool down = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
            int buffCount = n - shrineWeaponCount;
            if (down && shrineSel < shrineWeaponCount && buffCount > 0)
                SelectShrine(shrineWeaponCount + Mathf.Clamp(Mathf.Max(0, shrineSel), 0, buffCount - 1));
            else if (up && shrineSel >= shrineWeaponCount && shrineWeaponCount > 0)
                SelectShrine(Mathf.Clamp(shrineSel - shrineWeaponCount, 0, shrineWeaponCount - 1));
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            TrySacrifice();
        else if (Input.GetKeyDown(KeyCode.Escape))
            LeaveShrine();
    }

    private void SelectShrine(int index)
    {
        if (index < 0 || index >= shrineItems.Count) return;
        if (index != shrineSel) shrineArmed = false;
        shrineSel = index;
        Click();
    }

    private Weapon ShrineSelected => shrineSel >= 0 && shrineSel < shrineItems.Count ? shrineItems[shrineSel] : null;

    private void TrySacrifice()
    {
        Weapon w = ShrineSelected;
        if (w == null)
        {
            DenyButton(shrineSacrificeButton, Loc.Get("ui.shrine.pick_first", "Pick something first"));
            return;
        }

        Sacrifice.Block block = Sacrifice.Check(w);
        if (block != Sacrifice.Block.None)
        {
            shrineTiles[shrineSel].ShakeUntil = Time.unscaledTime + 0.25f;
            DenyButton(shrineSacrificeButton, BlockText(block));
            return;
        }

        if (!shrineArmed && Sacrifice.LostEvos(w).Count > 0)
        {
            shrineArmed = true;
            shrineSacrificeButton.Bump();
            ShowToast(Loc.Get("ui.shrine.sure_hint", "Press SACRIFICE again to confirm"), true);
            Click();
            return;
        }

        if (!Sacrifice.Execute(w)) return;
        if (AudioController.Instance != null)
            AudioController.Instance.PalySound(AudioController.Instance.selectUpgrade);
        SacrificeShrine.Close(true, w.weaponIcon);
    }

    private void LeaveShrine()
    {
        Click();
        SacrificeShrine.Close(false);
    }

    private static string BlockText(Sacrifice.Block block)
    {
        switch (block)
        {
            case Sacrifice.Block.StartWeapon: return Loc.Get("ui.shrine.block_start", "Your starting weapon stays");
            case Sacrifice.Block.Evo:         return Loc.Get("ui.shrine.block_evo", "Evos can't be sacrificed");
            default:                          return Loc.Get("ui.shrine.pick_first", "Pick something first");
        }
    }

    // ---------- Kopf, Info, Knoepfe ----------

    private void UpdateShrineHead(float now)
    {
        float age = now - openedAt;
        int drop = age < 0.05f ? -12 : age < 0.1f ? -6 : age < 0.15f ? 1 : 0;
        int bob = age > 0.6f && Mathf.Repeat(now * 1.2f, 1f) < 0.5f ? 1 : 0;
        int y = TitleY + drop + bob;
        SetPos(shrineRibbon, shrineRibbon.anchoredPosition.x, -y);
        SetPos(shrineTailL, shrineTailL.anchoredPosition.x, -(y + 4));
        SetPos(shrineTailR, shrineTailR.anchoredPosition.x, -(y + 4));

        bool flash = age < 0.6f && Mathf.Repeat(age * 8f, 1f) < 0.5f;
        shrineTitle.SetColor(flash ? (Color)GameHudSkin.JamLight : (Color)GameHudSkin.Cream);

        string text;
        Color color;
        int shake = 0;
        if (now < toastUntil)
        {
            text = toast;
            color = toastBad ? GameHudSkin.JamLight : GameHudSkin.GoldLight;
            if (toastUntil - now > ToastTime - 0.25f) shake = Mathf.FloorToInt(now * 30f) % 2 == 0 ? 1 : -1;
        }
        else if (!Sacrifice.AnythingToSacrifice())
        {
            text = Loc.Get("ui.shrine.nothing", "Nothing here the shrine would take");
            color = GameHudSkin.ParchDark;
        }
        else
        {
            text = Loc.Get("ui.shrine.pick", "Sacrifice a weapon or buff - it's banished for this run");
            color = GameHudSkin.Parchment;
        }
        shrineSubtitle.Set(text);
        shrineSubtitle.SetColor(color);
        shrineSubtitle.Move(shake, SubtitleY - 6);
    }

    private void UpdateShrineInfo(float now)
    {
        // Unter dem Zeiger zeigt die Info, was dort waere - sonst die Auswahl.
        Weapon w = shrineHover >= 0 ? shrineItems[shrineHover] : ShrineSelected;
        if (w == null)
        {
            shrineInfoName.Set(Loc.Get("ui.shrine.pick_first", "Pick something first"));
            shrineInfoName.SetColor(GameHudSkin.ParchDark);
            shrineInfoCost.Set("");
            shrineInfoNote.Set("");
            return;
        }

        int levels = Sacrifice.Levels(w);
        shrineInfoName.Set(NameOf(w) + "  <color=" + DimHex + ">" + Loc.Get("ui.hud.level", "LV") + " " + levels + "</color>");
        shrineInfoName.SetColor(GameHudSkin.Cream);

        Sacrifice.Block block = Sacrifice.Check(w);
        if (block != Sacrifice.Block.None)
        {
            shrineInfoCost.Set(BlockText(block));
            shrineInfoCost.SetColor(GameHudSkin.StoneLight);
            shrineInfoNote.Set("");
            return;
        }

        shrineInfoCost.Set(string.Format(Loc.Get("ui.shrine.cost", "-{0} LEVEL  -  PRESSURE +{1}% FOR {2}"),
                                         levels, Mathf.RoundToInt(RunDifficulty.SacrificePressure * 100f),
                                         Sacrifice.FormatTime(Sacrifice.Seconds(w))));
        shrineInfoCost.SetColor(GameHudSkin.JamLight);

        List<EvoRecipe> lost = Sacrifice.LostEvos(w);
        if (lost.Count > 0)
        {
            var names = new List<string>();
            foreach (EvoRecipe r in lost) names.Add(NameOf(r.EvoWeapon));
            bool armedHere = shrineArmed && w == ShrineSelected;
            shrineInfoNote.Set(string.Format(Loc.Get("ui.shrine.lose_evo", "You lose the evo: {0}"), string.Join(", ", names)));
            bool blink = armedHere && Mathf.Repeat(now * 3f, 1f) < 0.5f;
            shrineInfoNote.SetColor(blink ? (Color)GameHudSkin.Cream : (Color)GameHudSkin.JamLight);
        }
        else
        {
            shrineInfoNote.Set(Loc.Get("ui.shrine.banished", "Gone for the rest of the run"));
            shrineInfoNote.SetColor(GameHudSkin.ParchDark);
        }
    }

    private void UpdateShrineButtons(float now, Vector2 mouse, bool inputOpen)
    {
        Weapon w = ShrineSelected;
        bool can = w != null && Sacrifice.Check(w) == Sacrifice.Block.None;
        shrineSacrificeButton.SetLabel(shrineArmed && can
            ? Loc.Get("ui.shrine.sure", "SURE?")
            : Loc.Get("ui.shrine.sacrifice", "SACRIFICE"));
        shrineSacrificeButton.Disabled = !can;
        shrineSacrificeButton.Active = shrineArmed && can;

        if (shrineSacrificeButton.Update(now, mouse, inputOpen)) TrySacrifice();
        else if (SacrificeShrine.Current != null && shrineLeaveButton.Update(now, mouse, inputOpen)) LeaveShrine();
    }

    // ==================================================================
    //  Kachel
    // ==================================================================

    private sealed class ShrineTileView
    {
        public readonly RectTransform Root;
        public RectInt Rect;
        public float ShakeUntil;

        private readonly Image tile, icon, ring, shade, lockIcon, star;
        private readonly RectTransform chip;
        private readonly PixText level;
        private bool locked;

        public ShrineTileView(LevelUpScreen s, Transform parent)
        {
            Root = NewRect("ShrineTile", parent);
            Place(Root, new RectInt(0, 0, ShrineTile, ShrineTile));
            tile = s.Img("Tile", Root, new RectInt(0, 0, ShrineTile, ShrineTile), null, Color.white);
            icon = s.Img("Icon", Root, new RectInt(4, 3, 32, 32), null, Color.white);
            icon.preserveAspect = true;
            shade = s.Img("Shade", Root, new RectInt(1, 1, ShrineTile - 2, ShrineTile - 2), GameHudSkin.White,
                          new Color(0.07f, 0.05f, 0.06f, 0.6f));
            Sprite lockSprite = GameHudSkin.Lock;
            int lw = Mathf.RoundToInt(lockSprite.rect.width), lh = Mathf.RoundToInt(lockSprite.rect.height);
            lockIcon = s.Img("Lock", Root, new RectInt((ShrineTile - lw * 2) / 2, (ShrineTile - lh * 2) / 2, lw * 2, lh * 2),
                             lockSprite, Color.white);
            ring = s.Img("Ring", Root, new RectInt(-1, -1, ShrineTile + 2, ShrineTile + 2), GameHudSkin.Ring, Color.clear, true);
            chip = s.Img("Chip", Root, new RectInt(ShrineTile - 10, ShrineTile - 9, 12, 10), GameHudSkin.LevelChip,
                         Color.white, true).rectTransform;
            level = s.Text("Level", chip, new RectInt(0, 0, 12, 10), SizeText, GameHudSkin.Cream,
                           TextAlignmentOptions.Center, TextStyle.Plain);
            star = s.Img("Max", Root, new RectInt(ShrineTile - 9, ShrineTile - 9, 9, 9), GameHudSkin.Star, Color.white);
        }

        public void Set(RectInt rect, Weapon w, GameHudSkin.TileKind kind, bool isLocked)
        {
            Rect = rect;
            locked = isLocked;
            SetPos(Root, rect.x, -rect.y);
            tile.sprite = GameHudSkin.Tile(ShrineTile, kind);
            icon.sprite = w != null ? w.weaponIcon : null;
            icon.enabled = icon.sprite != null;
            SetActive(shade, locked);
            SetActive(lockIcon, locked);

            bool counts = w != null && w.maxweaponLevel > 0;
            bool maxed = counts && w.weaponLevel >= w.maxweaponLevel;
            SetActive(star, maxed);
            SetActive(chip, counts && !maxed);
            if (counts && !maxed && level.Set((w.weaponLevel + 1).ToString()))
            {
                int cw = Mathf.CeilToInt(level.Width) + 4;
                chip.sizeDelta = new Vector2(cw, 10);
                chip.anchoredPosition = new Vector2(ShrineTile + 2 - cw, -(ShrineTile - 9));
                level.Resize(cw, 10);
            }
        }

        public void Animate(float now, bool selected, bool hover)
        {
            Color rc = Color.clear;
            if (selected) rc = locked ? (Color)GameHudSkin.StoneLight
                                      : Mathf.Repeat(now * 2.5f, 1f) < 0.6f ? (Color)GameHudSkin.JamLight : (Color)GameHudSkin.Jam;
            else if (hover) rc = locked ? new Color(1f, 1f, 1f, 0.25f) : (Color)GameHudSkin.Rose;
            SetColor(ring, rc);

            int lift = selected && !locked ? 2 : hover ? 1 : 0;
            int shake = now < ShakeUntil ? (Mathf.FloorToInt(now * 30f) % 2 == 0 ? 1 : -1) : 0;
            SetPos(Root, Rect.x + shake, -(Rect.y - lift));
        }
    }
}
