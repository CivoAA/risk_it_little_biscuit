using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mixer-Ansicht des <see cref="LevelUpScreen"/>: die drei Power-ups, die ein
/// Mixer anbietet. Wie bei der Level-Up-Auswahl bleibt die Logik im alten
/// Panel - MixerObject macht PowerUpPanel an und wuerfelt die drei
/// <see cref="PowerUpButton"/>s aus, ein Klick ruft deren SelectUpgrade.
///
/// Jede Karte zeigt die Seltenheit als Farbe und Punkte, die Stat-Kategorie
/// als Symbol, den Bonus gross, darunter "jetzt > danach", wie gut der Wurf
/// innerhalb seiner Seltenheit war und den moeglichen Bereich. Unten stehen
/// alle Kategorien der PowerUpDatabase mit ihrem aktuellen Wert; unter dem
/// Zeiger leuchtet die, die sich aendern wuerde.
/// </summary>
public partial class LevelUpScreen
{
    // ---- Masse (480x270) ----
    private const int MixHintY = 212;
    private const int ChipY = 228, ChipW = 58, ChipH = 16, ChipGap = 4, MaxChips = 8;

    // ---- Zustand ----
    private RectTransform mixRibbon, mixTailL, mixTailR;
    private PixText mixTitle, mixSubtitle, mixHint;
    private readonly List<MixerCardView> mixCards = new List<MixerCardView>();
    private readonly List<PowerUpButton> mixOffered = new List<PowerUpButton>();
    private readonly List<StatChipView> statChips = new List<StatChipView>();
    private string mixSignature;

    // ==================================================================
    //  Aufbau
    // ==================================================================

    private void BuildMixer()
    {
        mixTailL = Img("TailL", mixerLayer, new RectInt(0, TitleY + 4, 12, 16), GameHudSkin.RibbonTailBlue(true), Color.white).rectTransform;
        mixTailR = Img("TailR", mixerLayer, new RectInt(0, TitleY + 4, 12, 16), GameHudSkin.RibbonTailBlue(false), Color.white).rectTransform;
        mixRibbon = Img("Ribbon", mixerLayer, new RectInt(180, TitleY, 120, TitleH), GameHudSkin.RibbonBlue, Color.white, true).rectTransform;
        mixTitle = Text("Title", mixRibbon, new RectInt(0, 0, 120, TitleH - 1), SizeTitle, GameHudSkin.Cream,
                        TextAlignmentOptions.Center, TextStyle.Shadow);

        mixSubtitle = Text("Subtitle", mixerLayer, new RectInt(0, SubtitleY - 6, RefW, SubtitleH), SizeText,
                           GameHudSkin.Parchment, TextAlignmentOptions.Center, TextStyle.Outline);

        for (int i = 0; i < MaxCards; i++) mixCards.Add(new MixerCardView(this, mixerLayer, i));

        mixHint = Text("Hint", mixerLayer, new RectInt(0, MixHintY, RefW, SubtitleH), SizeText,
                       GameHudSkin.ParchDark, TextAlignmentOptions.Center, TextStyle.Outline);

        for (int i = 0; i < MaxChips; i++) statChips.Add(new StatChipView(this, mixerLayer));
    }

    // ==================================================================
    //  Laufzeit
    // ==================================================================

    private void EnterMixer(float now)
    {
        openedAt = now;
        inputFrom = now + InputDelay;
        pendingPick = -1;
        focus = -1;
        keyboardFocus = false;
        mixSignature = null;
        dealAt = now;

        // Skilltree-Mixer: Jackpot (alle drei) schlaegt "alles legendaer" im Titel.
        mixTitle.Set(MixerObject.Jackpot      ? Loc.Get("ui.mixer.jackpot", "JACKPOT!")
                   : MixerObject.AllLegendary ? Loc.Get("ui.mixer.all_legendary", "LEGENDARY!")
                   :                            Loc.Get("ui.mixer.title", "POWER-UP!"));
        int w = Mathf.CeilToInt(mixTitle.Width) + 24;
        w += w & 1;
        int x = (RefW - w) / 2;
        mixRibbon.sizeDelta = new Vector2(w, TitleH);
        mixRibbon.anchoredPosition = new Vector2(x, -TitleY);
        mixTitle.Resize(w, TitleH - 1);
        mixTailL.anchoredPosition = new Vector2(x - 8, -(TitleY + 4));
        mixTailR.anchoredPosition = new Vector2(x + w - 4, -(TitleY + 4));

        mixHint.Set(MixerObject.Jackpot
            ? Loc.Get("ui.mixer.jackpot_hint", "Pick any card - you get all three")
            : Loc.Get("ui.mixer.luck_hint", "More luck, rarer rolls"));
        mixHint.SetColor(MixerObject.Jackpot ? (Color)GameHudSkin.GoldLight : (Color)GameHudSkin.ParchDark);
        CollectMixOffered();
        RefreshStatChips();
    }

    private void UpdateMixer(float now, Vector2 mouse, bool mouseMoved)
    {
        CollectMixOffered();
        string sig = MixSignature();
        if (sig != mixSignature)
        {
            // Die Knoepfe werden im selben Frame ausgewuerfelt, in dem das
            // Panel aufgeht - also auch hier neu austeilen, wenn sich etwas tut.
            if (mixSignature != null) dealAt = now;
            mixSignature = sig;
            LayoutMixCards();
        }

        if (pendingPick >= 0)
        {
            if (now >= pendingAt) CommitMixPick();
            AnimateMixCards(now);
            UpdateMixerHead(now);
            UpdateStatChips(now);
            return;
        }

        int hover = -1;
        for (int i = 0; i < mixOffered.Count; i++)
            if (Hit(mixCards[i].Rect, mouse)) hover = i;
        if (mouseMoved && !(keyboardFocus && hover < 0))
        {
            keyboardFocus = false;
            focus = hover;
        }
        else if (!keyboardFocus)
        {
            focus = hover;
        }

        if (now >= inputFrom)
        {
            if (Input.GetMouseButtonDown(0)) pressed = hover;
            if (Input.GetMouseButtonUp(0))
            {
                if (pressed >= 0 && pressed == hover) MixPick(hover);
                pressed = -1;
            }
            HandleMixerKeys();
        }

        AnimateMixCards(now);
        UpdateMixerHead(now);
        UpdateStatChips(now);
    }

    private void HandleMixerKeys()
    {
        for (int i = 0; i < mixOffered.Count && i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
            {
                focus = i;
                keyboardFocus = true;
                MixPick(i);
                return;
            }
        }

        if (mixOffered.Count == 0) return;
        int move = 0;
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) move = -1;
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) move = 1;
        if (move != 0)
        {
            focus = focus < 0 ? (move > 0 ? 0 : mixOffered.Count - 1) : (focus + move + mixOffered.Count) % mixOffered.Count;
            keyboardFocus = true;
        }
        if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
             || Input.GetKeyDown(KeyCode.Space)) && focus >= 0)
            MixPick(focus);
    }

    private void MixPick(int index)
    {
        if (index < 0 || index >= mixOffered.Count) return;
        pendingPick = index;
        pendingAt = Time.unscaledTime + PickTime;
        mixCards[index].PickedAt = Time.unscaledTime;
    }

    private void CommitMixPick()
    {
        int index = pendingPick;
        pendingPick = -1;
        if (index < 0 || index >= mixOffered.Count) return;
        mixOffered[index].SelectUpgrade();
        RefreshStatChips();
    }

    private void UpdateMixerHead(float now)
    {
        float age = now - openedAt;
        int drop = age < 0.05f ? -12 : age < 0.1f ? -6 : age < 0.15f ? 1 : 0;
        int bob = age > 0.6f && Mathf.Repeat(now * 1.2f, 1f) < 0.5f ? 1 : 0;
        int y = TitleY + drop + bob;
        SetPos(mixRibbon, mixRibbon.anchoredPosition.x, -y);
        SetPos(mixTailL, mixTailL.anchoredPosition.x, -(y + 4));
        SetPos(mixTailR, mixTailR.anchoredPosition.x, -(y + 4));

        bool flash = age < 0.6f && Mathf.Repeat(age * 8f, 1f) < 0.5f;
        mixTitle.SetColor(flash ? (Color)GameHudSkin.IcingLight : (Color)GameHudSkin.Cream);

        // Unterzeile: unter dem Zeiger die Seltenheit mit Chance, sonst die Aufforderung.
        if (focus >= 0 && focus < mixOffered.Count)
        {
            PowerUpRarity r = mixOffered[focus].Rarity;
            mixSubtitle.Set(RarityName(r) + "  -  " + Loc.Get("ui.mixer.chance", "chance") + " " + RarityChance(r));
            mixSubtitle.SetColor(RarityLight(r));
        }
        else
        {
            mixSubtitle.Set(Loc.Get("ui.mixer.pick", "Pick a stat boost"));
            mixSubtitle.SetColor(GameHudSkin.Parchment);
        }
    }

    // ---------- Daten ----------

    private void CollectMixOffered()
    {
        mixOffered.Clear();
        PowerUpButton[] b = ui.PowerUpButtons;
        if (b == null) return;
        foreach (PowerUpButton pb in b)
        {
            if (pb == null || !pb.gameObject.activeSelf || pb.assignedPowerUp == null) continue;
            if (mixOffered.Count >= MaxCards) break;
            mixOffered.Add(pb);
        }
    }

    private string MixSignature()
    {
        var sb = new System.Text.StringBuilder();
        foreach (PowerUpButton b in mixOffered)
            sb.Append(b.assignedPowerUp.powerUpName).Append('|').Append((int)b.Rarity).Append('|')
              .Append(b.Value.ToString("R", CultureInfo.InvariantCulture)).Append(';');
        return sb.ToString();
    }

    private void LayoutMixCards()
    {
        int n = mixOffered.Count;
        int total = n * CardW + Mathf.Max(0, n - 1) * CardGap;
        int x0 = (RefW - total) / 2;
        for (int i = 0; i < mixCards.Count; i++)
        {
            MixerCardView c = mixCards[i];
            bool on = i < n;
            c.Root.gameObject.SetActive(on);
            if (!on) continue;
            c.Rect = new RectInt(x0 + i * (CardW + CardGap), CardsY, CardW, CardH);
            c.Fill(mixOffered[i]);
        }
        if (focus >= n) focus = n - 1;
    }

    private void AnimateMixCards(float now)
    {
        for (int i = 0; i < mixOffered.Count; i++)
        {
            float t = now - dealAt - i * DealStagger;
            mixCards[i].Animate(now, t, i == focus && pendingPick < 0, i == pressed, pendingPick, i);
        }
    }

    // ---------- Stat-Leiste ----------

    private void RefreshStatChips()
    {
        var names = new List<string>();
        PowerUpDatabase db = ui.PowerUpDatabase;
        if (db != null && db.powerUps != null)
            foreach (PowerUpEntry e in db.powerUps)
                if (e != null && !string.IsNullOrEmpty(e.powerUpName) && !names.Contains(e.powerUpName))
                    names.Add(e.powerUpName);
        // Was angeboten wird, soll auch unten stehen - selbst wenn es nicht
        // (mehr) in der Datenbank waere.
        foreach (PowerUpButton b in mixOffered)
            if (!names.Contains(b.assignedPowerUp.powerUpName)) names.Add(b.assignedPowerUp.powerUpName);

        int n = Mathf.Min(names.Count, MaxChips);
        int total = n * ChipW + Mathf.Max(0, n - 1) * ChipGap;
        int x0 = (RefW - total) / 2;
        for (int i = 0; i < statChips.Count; i++)
        {
            bool on = i < n;
            statChips[i].Root.gameObject.SetActive(on);
            if (on) statChips[i].Set(names[i], x0 + i * (ChipW + ChipGap), ChipY);
        }
    }

    private void UpdateStatChips(float now)
    {
        int target = pendingPick >= 0 ? pendingPick : focus;
        PowerUpButton b = target >= 0 && target < mixOffered.Count ? mixOffered[target] : null;
        foreach (StatChipView c in statChips)
            if (c.Root.gameObject.activeSelf) c.Animate(now, b);
    }

    // ==================================================================
    //  Stat-Kategorien und Seltenheit
    // ==================================================================

    private struct StatMeta
    {
        public string Id, Fallback;
        public Sprite Icon;
        public bool Pct;       // Multiplikator: als Prozent zeigen (2.05 -> 205%)
        public int Decimals;
    }

    private static StatMeta Meta(string field)
    {
        switch (field)
        {
            case "playerMaxHealth":      return M("maxhp", "Max HP", GameHudSkin.Heart, false, 1);
            case "moveSpeed":            return M("movespeed", "Speed", GameHudSkin.Speed, false, 2);
            case "luck":                 return M("luck", "Luck", GameHudSkin.Clover, false, 1);
            case "playerShots":          return M("extrashots", "Extra shots", GameHudSkin.Shots, false, 2);
            case "playerArmor":          return M("armor", "Armor", GameHudSkin.Shield, false, 2);
            case "critDamage":           return M("critdamage", "Crit damage", GameHudSkin.Crit, true, 0);
            case "critChance":           return M("critchance", "Crit chance", GameHudSkin.Crit, true, 0);
            case "damageMultiplier":     return M("damage", "Damage", GameHudSkin.Crit, true, 0);
            case "experienceMultiplier": return M("xp", "XP gain", GameHudSkin.Gem, true, 0);
            case "dodgeChance":          return M("dodge", "Dodge", GameHudSkin.Speed, true, 0);
            case "pickupRange":          return M("pickup", "Pickup range", GameHudSkin.Gem, false, 2);
            case "playerHealthReg":      return M("regen", "Regeneration", GameHudSkin.Heart, false, 2);
            default:                     return M(field, field, GameHudSkin.Star, false, 2);
        }
    }

    private static StatMeta M(string id, string fallback, Sprite icon, bool pct, int decimals)
    {
        return new StatMeta { Id = id, Fallback = fallback, Icon = icon, Pct = pct, Decimals = decimals };
    }

    private static string StatName(string field)
    {
        StatMeta m = Meta(field);
        return Loc.Get("ui.levelup.stat." + m.Id, m.Fallback).ToUpperInvariant();
    }

    private static string FormatStat(float v, StatMeta m, bool sign)
    {
        string s = m.Pct
            ? Mathf.RoundToInt(v * 100f).ToString(CultureInfo.InvariantCulture) + "%"
            : v.ToString(m.Decimals <= 0 ? "0" : "0." + new string('#', m.Decimals), CultureInfo.InvariantCulture);
        if (s == "-0") s = "0";
        return sign && v >= 0f ? "+" + s : s;
    }

    /// <summary>Aktueller Wert des Feldes am Spieler - dasselbe Feld, das SelectUpgrade erhoeht.</summary>
    private static bool TryGetStat(string field, out float value)
    {
        value = 0f;
        PlayerController p = PlayerController.Instance;
        if (p == null || string.IsNullOrEmpty(field)) return false;
        FieldInfo f = typeof(PlayerController).GetField(field);
        if (f == null) return false;
        object v = f.GetValue(p);
        if (v is float fl) { value = fl; return true; }
        if (v is int i) { value = i; return true; }
        return false;
    }

    /// <summary>Was das Feld nach der Wahl waere - int-Felder runden wie SelectUpgrade.</summary>
    private static float AfterPick(string field, float current, float gain)
    {
        FieldInfo f = typeof(PlayerController).GetField(field);
        if (f != null && f.FieldType == typeof(int)) return current + Mathf.RoundToInt(gain);
        return current + gain;
    }

    private static string RarityName(PowerUpRarity r)
    {
        switch (r)
        {
            case PowerUpRarity.Uncommon:  return Loc.Get("ui.mixer.rarity.uncommon", "UNCOMMON");
            case PowerUpRarity.Rare:      return Loc.Get("ui.mixer.rarity.rare", "RARE");
            case PowerUpRarity.Epic:      return Loc.Get("ui.mixer.rarity.epic", "EPIC");
            case PowerUpRarity.Legendary: return Loc.Get("ui.mixer.rarity.legendary", "LEGENDARY");
            default:                      return Loc.Get("ui.mixer.rarity.common", "COMMON");
        }
    }

    /// <summary>
    /// Die Chance der Seltenheit beim aktuellen Glueck - dieselbe Rechnung wie
    /// <see cref="PowerUpRaritySelector"/> (Basis 2/5/10/25 %, Glueck
    /// multipliziert, ueber 100 % faellt der Rest weg).
    /// </summary>
    private static string RarityChance(PowerUpRarity r)
    {
        float luck = PlayerController.Instance != null ? PlayerController.Instance.luck : 0f;
        float mult = 1f + luck / 100f;
        float[] baseChances = { 2f, 5f, 10f, 25f };
        PowerUpRarity[] order = { PowerUpRarity.Legendary, PowerUpRarity.Epic, PowerUpRarity.Rare, PowerUpRarity.Uncommon };

        float total = 0f;
        float chance = -1f;
        for (int i = 0; i < baseChances.Length; i++)
        {
            float c = baseChances[i] * mult;
            if (total >= 100f) c = 0f;
            else if (total + c > 100f) c = 100f - total;
            total += c;
            if (order[i] == r) chance = c;
        }
        if (r == PowerUpRarity.Common) chance = Mathf.Max(0f, 100f - total);

        float shown = chance < 1f ? Mathf.Round(chance * 10f) / 10f : Mathf.Round(chance);
        return shown.ToString("0.#", CultureInfo.InvariantCulture) + "%";
    }

    private static void RarityColors(PowerUpRarity r, out Color main, out Color hi, out Color lo, out Color text)
    {
        switch (r)
        {
            case PowerUpRarity.Uncommon:
                main = GameHudSkin.Mint; hi = GameHudSkin.MintLight; lo = GameHudSkin.MintDark; text = GameHudSkin.Ink; break;
            case PowerUpRarity.Rare:
                main = GameHudSkin.Icing; hi = GameHudSkin.IcingLight; lo = GameHudSkin.IcingDark; text = GameHudSkin.Ink; break;
            case PowerUpRarity.Epic:
                main = GameHudSkin.Grape; hi = GameHudSkin.GrapeLight; lo = GameHudSkin.GrapeDark; text = GameHudSkin.Cream; break;
            case PowerUpRarity.Legendary:
                main = GameHudSkin.Gold; hi = GameHudSkin.GoldLight; lo = GameHudSkin.GoldDark; text = GameHudSkin.Ink; break;
            default:
                main = GameHudSkin.Stone; hi = GameHudSkin.StoneLight; lo = GameHudSkin.StoneDark; text = GameHudSkin.Cream; break;
        }
    }

    private static Color RarityLight(PowerUpRarity r)
    {
        RarityColors(r, out _, out Color hi, out _, out _);
        return r == PowerUpRarity.Common ? (Color)GameHudSkin.Parchment : hi;
    }

    // ==================================================================
    //  Karte
    // ==================================================================

    private sealed class MixerCardView
    {
        public readonly RectTransform Root;
        public RectInt Rect;
        public float PickedAt = -1f;

        private readonly int index;
        private readonly CanvasGroup group;
        private readonly Image ring, flash, headMain, headHi, headLo, icon, barFill, rollStar, well;
        private readonly RectTransform barFillRect;
        private readonly PixText headText, nameText, bigValue, nowLabel, nowValue, rollLabel, rangeText, keyText;
        private readonly List<Image> pips = new List<Image>();
        private readonly Image[] sparkles = new Image[3];
        private PowerUpRarity rarity;
        private int iconW, iconH;

        private const int PlateX = 36, PlateY = 21, IconScale = 3;
        private const int BarX = 50, BarY = 118, BarW = 46, BarH = 7;

        public MixerCardView(LevelUpScreen s, Transform parent, int index)
        {
            this.index = index;
            Root = NewRect("MixCard" + index, parent);
            Place(Root, new RectInt(0, CardsY, CardW, CardH));
            group = Root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            RectTransform body = NewRect("Body", Root);
            Place(body, new RectInt(0, 0, CardW, CardH));
            s.Img("Frame", body, new RectInt(0, 0, CardW, CardH), GameHudSkin.Card, Color.white, true);

            headMain = s.Img("HeadMain", body, new RectInt(2, 2, CardW - 4, 13), GameHudSkin.White, Color.white);
            headHi = s.Img("HeadHi", body, new RectInt(3, 2, CardW - 6, 1), GameHudSkin.White, Color.white);
            headLo = s.Img("HeadLo", body, new RectInt(2, 14, CardW - 4, 1), GameHudSkin.White, Color.white);
            s.Img("HeadEdge", body, new RectInt(2, 15, CardW - 4, 1), GameHudSkin.White, GameHudSkin.Ink);
            headText = s.Text("Rarity", body, new RectInt(6, 2, 70, 13), SizeText, GameHudSkin.Ink,
                              TextAlignmentOptions.Left, TextStyle.Plain);
            for (int i = 0; i < 5; i++)
                pips.Add(s.Img("Pip" + i, body, new RectInt(CardW - 35 + i * 6, 6, 5, 5),
                               GameHudSkin.Pip(GameHudSkin.PipKind.Empty), Color.white));

            s.Img("Plate", body, new RectInt(PlateX, PlateY, 40, 40), GameHudSkin.IconPlate(false), Color.white);
            icon = s.Img("Icon", body, new RectInt(PlateX + 4, PlateY + 4, 32, 32), null, Color.white);
            for (int i = 0; i < sparkles.Length; i++)
            {
                sparkles[i] = s.Img("Sparkle" + i, body, new RectInt(0, 0, 3, 3), GameHudSkin.Sparkle, Color.white);
                sparkles[i].enabled = false;
            }

            nameText = s.Text("Stat", body, new RectInt(3, 63, CardW - 6, 12), SizeText, GameHudSkin.Cream,
                              TextAlignmentOptions.Center, TextStyle.Outline);
            bigValue = s.Text("Value", body, new RectInt(3, 76, CardW - 6, 20), SizeTitle, GameHudSkin.Cream,
                              TextAlignmentOptions.Center, TextStyle.Outline);

            Color line = GameHudSkin.Ink; line.a = 0.8f;
            s.Img("Divider", body, new RectInt(8, 100, CardW - 16, 1), GameHudSkin.White, line);
            Color lineHi = GameHudSkin.Cream; lineHi.a = 0.06f;
            s.Img("DividerHi", body, new RectInt(8, 101, CardW - 16, 1), GameHudSkin.White, lineHi);

            nowLabel = s.Text("NowLabel", body, new RectInt(7, 104, 50, 11), SizeText, GameHudSkin.ParchDark,
                              TextAlignmentOptions.Left, TextStyle.Shadow);
            nowValue = s.Text("NowValue", body, new RectInt(38, 104, CardW - 45, 11), SizeText, GameHudSkin.Cream,
                              TextAlignmentOptions.Right, TextStyle.Shadow);

            rollLabel = s.Text("RollLabel", body, new RectInt(7, 115, 44, 11), SizeText, GameHudSkin.ParchDark,
                               TextAlignmentOptions.Left, TextStyle.Shadow);
            s.Img("BarFrame", body, new RectInt(BarX, BarY, BarW, BarH), GameHudSkin.BarFrame, Color.white, true);
            barFill = s.Img("BarFill", body, new RectInt(BarX + 1, BarY + 1, BarW - 2, BarH - 2), GameHudSkin.White, Color.white);
            barFillRect = barFill.rectTransform;
            rollStar = s.Img("RollStar", body, new RectInt(BarX + BarW + 1, BarY - 1, 9, 9), GameHudSkin.Star, Color.white);

            well = s.Img("Well", body, new RectInt(4, 128, CardW - 8, 14), GameHudSkin.Well, Color.white, true);
            rangeText = s.Text("Range", body, new RectInt(4, 129, CardW - 8, 12), SizeText, GameHudSkin.Parchment,
                               TextAlignmentOptions.Center, TextStyle.Shadow);

            ring = s.Img("Ring", body, new RectInt(0, 0, CardW, CardH), GameHudSkin.Ring, Color.clear, true);
            flash = s.Img("Flash", body, new RectInt(1, 1, CardW - 2, CardH - 2), GameHudSkin.White, new Color(1f, 1f, 1f, 0f));

            RectTransform keyChip = s.Img("Key", Root, new RectInt(CardW / 2 - 5, CardH - 5, 10, 10), GameHudSkin.LevelChip,
                                          Color.white, true).rectTransform;
            keyText = s.Text("KeyText", keyChip, new RectInt(0, 0, 10, 10), SizeText, GameHudSkin.Parchment,
                             TextAlignmentOptions.Center, TextStyle.Plain);
            keyText.Set((index + 1).ToString());
        }

        public void Fill(PowerUpButton b)
        {
            PickedAt = -1f;
            rarity = b.Rarity;
            string field = b.assignedPowerUp.powerUpName;
            StatMeta meta = Meta(field);
            float gain = b.Value;

            RarityColors(rarity, out Color main, out Color hi, out Color lo, out Color text);
            headMain.color = main;
            headHi.color = hi;
            headLo.color = lo;
            headText.Set(RarityName(rarity));
            headText.SetColor(text);
            for (int i = 0; i < pips.Count; i++)
                // Helle Punkte - goldene gingen auf dem goldenen Legendaer-Kopf unter.
                pips[i].sprite = GameHudSkin.Pip(i <= (int)rarity ? GameHudSkin.PipKind.Next : GameHudSkin.PipKind.Empty);

            // Symbol in ganzer Vergroesserung mittig in die Platte.
            icon.sprite = meta.Icon;
            iconW = Mathf.RoundToInt(meta.Icon.rect.width) * IconScale;
            iconH = Mathf.RoundToInt(meta.Icon.rect.height) * IconScale;
            icon.rectTransform.sizeDelta = new Vector2(iconW, iconH);

            nameText.Set(StatName(field));
            bigValue.Set(FormatStat(gain, meta, true));
            bigValue.SetColor(RarityLight(rarity));

            nowLabel.Set(Loc.Get("ui.mixer.now", "NOW"));
            if (TryGetStat(field, out float cur))
            {
                float after = AfterPick(field, cur, gain);
                nowValue.Set("<color=" + DimHex + ">" + FormatStat(cur, meta, false) + "</color> <color=" + ArrowHex
                             + ">></color> <color=" + UpHex + ">" + FormatStat(after, meta, false) + "</color>");
            }
            else nowValue.Set("");

            // Wie gut der Wurf innerhalb seiner Seltenheit war.
            Vector2 range = b.Range;
            float q = range.y - range.x > 0.0001f ? Mathf.Clamp01((gain - range.x) / (range.y - range.x)) : 1f;
            rollLabel.Set(Loc.Get("ui.mixer.roll", "ROLL"));
            int fillW = Mathf.Max(1, Mathf.RoundToInt(q * (BarW - 2)));
            barFillRect.sizeDelta = new Vector2(fillW, BarH - 2);
            barFill.color = main;
            rollStar.enabled = q >= 0.9f;

            rangeText.Set(Loc.Get("ui.mixer.range", "RANGE") + " " + FormatStat(range.x, meta, false)
                          + " - " + FormatStat(range.y, meta, false));
        }

        public void Animate(float now, float dealT, bool focused, bool held, int picked, int myIndex)
        {
            int rise;
            float alpha;
            if (dealT < 0f) { rise = 24; alpha = 0f; }
            else if (dealT < DealTime)
            {
                float k = dealT / DealTime;
                rise = k < 0.34f ? 16 : k < 0.67f ? 6 : -2;
                alpha = k < 0.34f ? 0.35f : k < 0.67f ? 0.75f : 1f;
            }
            else { rise = 0; alpha = 1f; }

            bool isPicked = picked == myIndex;
            if (picked >= 0 && !isPicked) alpha *= 0.3f;

            int lift = focused || isPicked ? 3 : 0;
            if (held && focused) lift = 1;
            SetPos(Root, Rect.x, -(Rect.y + rise - lift));
            if (!Mathf.Approximately(group.alpha, alpha)) group.alpha = alpha;

            // Aufdecken: kurz nach dem Landen blitzt die Karte - je seltener, desto laenger.
            float reveal = dealT - DealTime;
            float revealTime = 0.08f + 0.06f * (int)rarity;
            float rf = reveal >= 0f && reveal < revealTime ? 1f - reveal / revealTime : 0f;
            float pf = isPicked ? Mathf.Clamp01(1f - (now - PickedAt) / PickTime) : 0f;
            float f = Mathf.Max(rf * (rarity >= PowerUpRarity.Epic ? 0.8f : 0.4f), pf * 0.7f);
            SetColor(flash, new Color(1f, 1f, 1f, Mathf.Round(f * 3f) / 3f));

            RarityColors(rarity, out Color main, out Color hi, out _, out _);
            Color rc = Color.clear;
            if (isPicked) rc = GameHudSkin.Cream;
            else if (focused) rc = rarity == PowerUpRarity.Common ? (Color)GameHudSkin.GoldLight : hi;
            else if (rarity >= PowerUpRarity.Rare)
            {
                rc = main;
                rc.a = Mathf.Repeat(now * 1.5f + index * 0.2f, 1f) < 0.5f ? 0.6f : 0.25f;
            }
            SetColor(ring, rc);

            int bob = focused && Mathf.Repeat(now * 2.5f, 1f) < 0.5f ? -1 : 0;
            int ix = PlateX + (40 - iconW) / 2;
            int iy = PlateY + (40 - iconH) / 2;
            SetPos(icon.rectTransform, ix, -(iy + bob));

            if (rollStar.enabled)
            {
                int sb = Mathf.Repeat(now * 2f, 1f) < 0.5f ? 0 : 1;
                SetPos(rollStar.rectTransform, BarX + BarW + 1, -(BarY - 1 - sb));
            }

            // Episch und legendaer funkeln - legendaer mit drei Funken.
            int sparks = rarity == PowerUpRarity.Legendary ? 3 : rarity == PowerUpRarity.Epic ? 2 : 0;
            for (int i = 0; i < sparkles.Length; i++)
            {
                float cycle = now * 1.1f + i * 0.37f + index * 0.21f;
                bool vis = i < sparks && Mathf.Repeat(cycle, 1f) < 0.28f;
                sparkles[i].enabled = vis;
                if (!vis) continue;
                int corner = (Mathf.FloorToInt(cycle) + i * 2) % 5;
                Vector2Int pos = corner == 0 ? new Vector2Int(PlateX - 2, PlateY - 1)
                               : corner == 1 ? new Vector2Int(PlateX + 39, PlateY + 3)
                               : corner == 2 ? new Vector2Int(PlateX + 38, PlateY + 37)
                               : corner == 3 ? new Vector2Int(PlateX - 1, PlateY + 34)
                               : new Vector2Int(CardW - 9, 20);
                SetPos(sparkles[i].rectTransform, pos.x, -pos.y);
            }

            keyText.SetColor(focused ? (Color)GameHudSkin.GoldLight : (Color)GameHudSkin.Parchment);
        }
    }

    // ==================================================================
    //  Chip in der Stat-Leiste
    // ==================================================================

    private sealed class StatChipView
    {
        public readonly RectTransform Root;
        private readonly Image bg, icon, ring;
        private readonly PixText value;
        private string field;
        private StatMeta meta;
        private int x, y;

        public StatChipView(LevelUpScreen s, Transform parent)
        {
            Root = NewRect("StatChip", parent);
            Place(Root, new RectInt(0, ChipY, ChipW, ChipH));
            bg = s.Img("Bg", Root, new RectInt(0, 0, ChipW, ChipH), GameHudSkin.Card, Color.white, true);
            icon = s.Img("Icon", Root, new RectInt(3, 3, 9, 9), null, Color.white);
            value = s.Text("Value", Root, new RectInt(14, 0, ChipW - 18, ChipH - 1), SizeText, GameHudSkin.Cream,
                           TextAlignmentOptions.Right, TextStyle.Shadow);
            ring = s.Img("Ring", Root, new RectInt(0, 0, ChipW, ChipH), GameHudSkin.Ring, Color.clear, true);
        }

        public void Set(string statField, int px, int py)
        {
            field = statField;
            meta = Meta(field);
            x = px; y = py;
            SetPos(Root, x, -y);

            icon.sprite = meta.Icon;
            int w = Mathf.RoundToInt(meta.Icon.rect.width), h = Mathf.RoundToInt(meta.Icon.rect.height);
            icon.rectTransform.sizeDelta = new Vector2(w, h);
            icon.rectTransform.anchoredPosition = new Vector2(4 + (10 - w) / 2, -((ChipH - h) / 2));
        }

        public void Animate(float now, PowerUpButton hovered)
        {
            bool mine = hovered != null && hovered.assignedPowerUp.powerUpName == field;
            bool has = TryGetStat(field, out float cur);

            string text;
            Color color;
            if (mine && has)
            {
                // Fest der Wert nach der Wahl, leicht gelblich - kein Hin- und
                // Herspringen mit dem alten Wert mehr.
                text = FormatStat(AfterPick(field, cur, hovered.Value), meta, false);
                color = GameHudSkin.GoldLight;
            }
            else
            {
                text = has ? FormatStat(cur, meta, false) : "-";
                color = GameHudSkin.Cream;
            }
            value.Set(text);
            value.SetColor(color);

            Color rc = Color.clear;
            if (mine)
            {
                RarityColors(hovered.Rarity, out Color main, out _, out _, out _);
                rc = hovered.Rarity == PowerUpRarity.Common ? (Color)GameHudSkin.GoldLight : main;
            }
            SetColor(ring, rc);
            SetPos(Root, x, -(y - (mine ? 1 : 0)));
        }
    }
}
