using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Der Erklaer-Zettel im Pausenmenue: Maus ueber einem Stat zeigt, was er
/// tut und was der aktuelle Wert gerade bringt.
///
/// Die Rechnungen spiegeln den Spielcode - aendert sich dort eine Formel,
/// muss sie hier mit:
///   Ruestung      PlayerController.TakeDamage   (Schaden * 0,9^Ruestung)
///   Ausweichen    PlayerController.TakeDamage
///   Regeneration  HPReg (Takt = stats[level].cooldown, sonst 2,5 s)
///   Lebensraub    LifeSteal.StealLife (1 HP, Abklingzeit aus stats)
///   Krit          Enemy.TakeDamage
///   Glueck        PowerUpRaritySelector (Basis 2/5/10/25 %, * (1 + Glueck/100))
///   Gold          GameManager.EstimateCurrency
///
/// Der Zettel sitzt links neben der Spalte des Stats, damit er die Zeile
/// selbst nie verdeckt, und faengt keine Zeigerereignisse ab.
/// </summary>
public partial class PauseMenuPanel
{
    private const int TipW = 168, TipPad = 6, TipTitleH = 13, TipGap = 4;

    private RectTransform tip;
    private Image tipRing;
    private TextMeshProUGUI tipTitle, tipBody;
    private Image hoverMark;
    private int hoverIndex = -1;

    private void ResetStatInfo()
    {
        tip = null;
        tipRing = null;
        tipTitle = tipBody = null;
        hoverMark = null;
        hoverIndex = -1;
    }

    /// <summary>Unsichtbare Trefferflaeche ueber einer Statzeile.</summary>
    private void AddStatHover(int x, int y, int w, int h, int index)
    {
        Image hit = OptionsKit.Img("Hover_" + index, page, x, y, w, h, GameHudSkin.White, new Color(1f, 1f, 1f, 0f));
        hit.raycastTarget = true;

        PointerRelay relay = hit.gameObject.AddComponent<PointerRelay>();
        relay.Enter = () => ShowStatInfo(index);
        relay.Exit = () => { if (hoverIndex == index) HideStatInfo(); };
    }

    /// <summary>Zettel + Zeilenmarke. Laeuft nach den Stats, liegt also obenauf.</summary>
    private void BuildStatInfo()
    {
        hoverMark = OptionsKit.Img("HoverMark", page, 0, 0, ColW, StatRowH, GameHudSkin.White,
                                   OptionsKit.WithAlpha(GameHudSkin.Cream, 0.12f));
        hoverMark.gameObject.SetActive(false);

        tip = OptionsKit.Rect("StatInfo", page, 0, 0, TipW, 40);
        OptionsKit.Stretch("Card", tip, GameHudSkin.Card, Color.white).type = Image.Type.Sliced;
        tipRing = OptionsKit.Stretch("Ring", tip, GameHudSkin.Ring, Color.white);
        tipRing.type = Image.Type.Sliced;

        tipTitle = OptionsKit.Label("Title", tip, TipPad, TipPad - 2, TipW - 2 * TipPad, TipTitleH, "",
                                    textFont, OptionsKit.SizeText, GameHudSkin.Cream, TextAlignmentOptions.Left);
        tipBody = OptionsKit.Label("Body", tip, TipPad, TipPad - 2 + TipTitleH, TipW - 2 * TipPad, 20, "",
                                   textFont, OptionsKit.SizeText, GameHudSkin.Parchment,
                                   TextAlignmentOptions.TopLeft);
        tipBody.richText = true;
        tipBody.textWrappingMode = TextWrappingModes.Normal;

        tip.gameObject.SetActive(false);
    }

    private void ShowStatInfo(int index)
    {
        if (tip == null || dialog != null || index < 0 || index >= cells.Count) return;

        hoverIndex = index;
        StatCells cell = cells[index];

        tipTitle.text = Loc.Get(cell.Def.Key, cell.Def.Fallback);
        tipTitle.color = cell.Color;
        tipRing.color = cell.Color;

        string body = StatInfoText(cell.Def.Id);
        tipBody.text = body;

        int bodyW = TipW - 2 * TipPad;
        int bodyH = Mathf.CeilToInt(tipBody.GetPreferredValues(body, bodyW, 0f).y) + 2;
        int h = TipPad - 2 + TipTitleH + bodyH + TipPad;
        h += h & 1;

        // Links neben die Spalte, oben buendig mit der Zeile - und immer
        // innerhalb des Bereichs, den Layout garantiert sichtbar haelt.
        int x = Mathf.Max(RunContent.x, cell.X - TipGap - TipW);
        int y = Mathf.Clamp(cell.Y - 4, RunContent.y, Mathf.Max(RunContent.y, RunContent.yMax - h));

        OptionsKit.Move(tip, x, y, TipW, h);
        OptionsKit.Move(tipBody.rectTransform, TipPad, TipPad - 2 + TipTitleH, bodyW, bodyH);
        tip.gameObject.SetActive(true);

        OptionsKit.Move(hoverMark.rectTransform, cell.X, cell.Y, ColW, StatRowH);
        hoverMark.gameObject.SetActive(true);
    }

    private void HideStatInfo()
    {
        hoverIndex = -1;
        if (tip != null) tip.gameObject.SetActive(false);
        if (hoverMark != null) hoverMark.gameObject.SetActive(false);
    }

    /// <summary>Werte koennen sich in der Pause aendern (Cheats) - der Zettel zieht mit.</summary>
    private void RefreshStatInfo()
    {
        if (hoverIndex >= 0) ShowStatInfo(hoverIndex);
    }

    // ==================================================================
    //  Texte
    // ==================================================================

    private static string Hi(Color32 c) => "#" + ColorUtility.ToHtmlStringRGB(c);

    /// <summary>Beschreibung (Pergament) + "Jetzt"-Zeilen (Gold) eines Stats.</summary>
    private static string StatInfoText(string id)
    {
        PlayerController p = PlayerController.Instance;
        var sb = new StringBuilder();

        sb.Append(Loc.Get("ui.pause.info." + id, InfoFallback(id)));

        if (id == "lifesteal")
            sb.Replace("{0}", Num(LifeStealCooldown(), "0.#"));

        string now = p != null ? NowText(id, p) : null;
        if (!string.IsNullOrEmpty(now))
        {
            sb.Append("\n<color=").Append(Hi(GameHudSkin.GoldLight)).Append('>')
              .Append(now).Append("</color>");
        }

        if (id == "luck" && p != null) AppendLuckTable(sb, p.luck);

        return sb.ToString();
    }

    private static string InfoFallback(string id)
    {
        switch (id)
        {
            case "maxhp":      return "Deine maximalen Lebenspunkte. Heilung füllt dich bis zu diesem Wert auf.";
            case "regen":      return "Heilt dich in festen Abständen von selbst, solange du nicht volle HP hast.";
            case "armor":      return "Jeder Punkt senkt erlittenen Schaden um 10 %. Mehrere Punkte multiplizieren sich: 10 Punkte = 65 % weniger.";
            case "dodge":      return "Chance, einem Treffer komplett auszuweichen. Dann gibt es gar keinen Schaden.";
            case "lifesteal":  return "Chance bei jedem Treffer an einem Gegner, dich um 1 HP zu heilen. Höchstens einmal alle {0} s.";
            case "damage":     return "Multipliziert den Schaden aller deiner Waffen.";
            case "critchance": return "Chance, dass ein Treffer kritisch ist. Krits machen Schaden mal Krit-Schaden.";
            case "critdamage": return "Wie hart kritische Treffer zuschlagen: ihr Schaden wird mit diesem Wert malgenommen.";
            case "size":       return "Vergrößert Flächen, Explosionen und Waffen, die eine Reichweite haben.";
            case "shots":      return "Zusätzliche Geschosse pro Angriff. Nur ganze Punkte zählen: 1,5 ergibt einen Extra-Schuss.";
            case "speed":      return "Wie schnell du läufst.";
            case "luck":       return "Macht im Mixer seltenere Karten wahrscheinlicher. Jeder Punkt hebt die Chance auf Ungewöhnlich bis Legendär um 1 % ihres Grundwerts: 100 Glück = doppelt so oft.";
            case "pickup":     return "Wie weit du XP, Gold und andere Pickups zu dir ziehst.";
            case "xp":         return "Multipliziert jede Erfahrung, die du einsammelst. Du levelst schneller auf.";
            case "gold":       return "Multipliziert das Gold, das du am Ende des Laufs bekommst.";
            default:           return "";
        }
    }

    /// <summary>Was der aktuelle Wert gerade bewirkt - eine oder zwei Zeilen.</summary>
    private static string NowText(string id, PlayerController p)
    {
        switch (id)
        {
            case "maxhp":
                return Fmt("ui.pause.info.maxhp.now", "Jetzt: {0} von {1} HP",
                           Num(Mathf.Max(0f, p.playerHealth), "0"), Num(p.playerMaxHealth, "0"));

            case "regen":
            {
                float tick = RegenInterval();
                if (p.playerHealthReg <= 0f) return Loc.Get("ui.pause.info.regen.none", "Jetzt: keine Regeneration");
                return Fmt("ui.pause.info.regen.now", "Jetzt: +{0} HP alle {1} s (ca. {2} HP pro Minute)",
                           Num(p.playerHealthReg, "0.#"), Num(tick, "0.#"),
                           Num(p.playerHealthReg * 60f / Mathf.Max(0.1f, tick), "0"));
            }

            case "armor":
            {
                float cut = (1f - Mathf.Pow(0.9f, p.playerArmor)) * 100f;
                float next = (1f - Mathf.Pow(0.9f, p.playerArmor + 1f)) * 100f;
                return Fmt("ui.pause.info.armor.now", "Jetzt: {0} % weniger Schaden\nMit einem Punkt mehr: {1} %",
                           Num(cut, "0"), Num(next, "0"));
            }

            case "dodge":
                return Fmt("ui.pause.info.dodge.now", "Jetzt: {0} % aller Treffer gehen daneben",
                           Num(Mathf.Clamp01(p.dodgeChance) * 100f, "0.#"));

            case "lifesteal":
                return Fmt("ui.pause.info.lifesteal.now", "Jetzt: {0} % Chance pro Treffer",
                           Num(Mathf.Clamp(p.lifeStealChance, 0f, 100f), "0.#"));

            case "damage":
                return Fmt("ui.pause.info.damage.now", "Jetzt: x{0} ({1} Schaden)",
                           Num(p.damageMultiplier, "0.##"), Pct(p.damageMultiplier - 1f));

            case "critchance":
            {
                float c = Mathf.Clamp01(p.critChance);
                if (c <= 0f) return Loc.Get("ui.pause.info.critchance.none", "Jetzt: noch keine Krits");
                if (c >= 1f) return Loc.Get("ui.pause.info.critchance.all", "Jetzt: jeder Treffer ist ein Krit");
                return Fmt("ui.pause.info.critchance.now", "Jetzt: {0} %, etwa jeder {1}. Treffer",
                           Num(c * 100f, "0.#"), Num(1f / c, "0"));
            }

            case "critdamage":
            {
                float avg = Mathf.Clamp01(p.critChance) * (p.critDamage - 1f);
                return Fmt("ui.pause.info.critdamage.now",
                           "Jetzt: Krits machen x{0} Schaden\nMit deiner Krit-Chance im Schnitt {1} Schaden",
                           Num(p.critDamage, "0.##"), Pct(avg));
            }

            case "size":
                return Fmt("ui.pause.info.size.now", "Jetzt: x{0} ({1} Größe)",
                           Num(p.AOERange, "0.##"), Pct(p.AOERange - 1f));

            case "shots":
            {
                int whole = p.ExtraShots;
                string text = Fmt("ui.pause.info.shots.now", "Jetzt: +{0} Geschosse pro Angriff", whole.ToString());
                float rest = p.playerShots - whole;
                if (rest > 0.001f)
                    text += "\n" + Fmt("ui.pause.info.shots.chance", "Dazu {0} % Chance auf eins mehr",
                                       Num(rest * 100f, "0"));
                return text;
            }

            case "speed":
                return Fmt("ui.pause.info.speed.now", "Jetzt: {0} Einheiten pro Sekunde", Num(p.moveSpeed, "0.#"));

            case "luck":
                return null;

            case "pickup":
                return Fmt("ui.pause.info.pickup.now", "Jetzt: x{0} ({1} Radius)",
                           Num(p.pickupRange, "0.##"), Pct(p.pickupRange - 1f));

            case "xp":
                return Fmt("ui.pause.info.xp.now", "Jetzt: x{0} ({1} XP)",
                           Num(p.experienceMultiplier, "0.##"), Pct(p.experienceMultiplier - 1f));

            case "gold":
            {
                float g = GameManager.Instance != null ? GameManager.Instance.currencyGainMultiplire : 1f;
                return Fmt("ui.pause.info.gold.now", "Jetzt: x{0} ({1} Gold)", Num(g, "0.##"), Pct(g - 1f));
            }

            default:
                return null;
        }
    }

    /// <summary>
    /// Seltenheits-Chancen im Mixer: ohne Glueck und mit dem jetzigen -
    /// so sieht man direkt, wie viel oefter die guten Karten kommen.
    /// </summary>
    private static void AppendLuckTable(StringBuilder sb, float luck)
    {
        float[] without = RarityChances(0f);
        float[] with = RarityChances(luck);

        PowerUpRarity[] order =
        {
            PowerUpRarity.Common, PowerUpRarity.Uncommon, PowerUpRarity.Rare,
            PowerUpRarity.Epic, PowerUpRarity.Legendary,
        };

        sb.Append("\n<color=").Append(Hi(GameHudSkin.StoneLight)).Append('>')
          .Append(Loc.Get("ui.pause.info.luck.col.rarity", "Seltenheit"))
          .Append("<pos=58%>").Append(Loc.Get("ui.pause.info.luck.col.base", "Ohne"))
          .Append("<pos=80%>").Append(Fmt("ui.pause.info.luck.col.now", "Mit {0}", Num(luck, "0")))
          .Append("</color>");

        for (int i = 0; i < order.Length; i++)
        {
            sb.Append("\n<color=").Append(Hi(RarityColor(order[i]))).Append('>')
              .Append(RarityLabel(order[i])).Append("</color>")
              .Append("<pos=58%>").Append(Chance(without[i]))
              .Append("<pos=80%><color=").Append(Hi(GameHudSkin.GoldLight)).Append('>')
              .Append(Chance(with[i])).Append("</color>");
        }
    }

    /// <summary>
    /// Dieselbe Rechnung wie <see cref="PowerUpRaritySelector.GetRandomRarity"/>:
    /// Reihenfolge Gewoehnlich, Ungewoehnlich, Selten, Episch, Legendaer.
    /// </summary>
    private static float[] RarityChances(float luck)
    {
        float mult = 1f + luck / 100f;
        // Legendaer zuerst - so zieht der Selector auch.
        float[] baseChances = { 2f, 5f, 10f, 25f };
        float[] top = new float[4];

        float total = 0f;
        for (int i = 0; i < baseChances.Length; i++)
        {
            float c = baseChances[i] * mult;
            if (total >= 100f) c = 0f;
            else if (total + c > 100f) c = 100f - total;
            top[i] = c;
            total += c;
        }

        return new[] { Mathf.Max(0f, 100f - total), top[3], top[2], top[1], top[0] };
    }

    private static string Chance(float c)
    {
        float shown = c > 0f && c < 1f ? Mathf.Round(c * 10f) / 10f : Mathf.Round(c);
        return Num(shown, "0.#") + " %";
    }

    private static string RarityLabel(PowerUpRarity r)
    {
        switch (r)
        {
            case PowerUpRarity.Uncommon:  return Loc.Get("ui.pause.info.rarity.uncommon", "Ungewöhnlich");
            case PowerUpRarity.Rare:      return Loc.Get("ui.pause.info.rarity.rare", "Selten");
            case PowerUpRarity.Epic:      return Loc.Get("ui.pause.info.rarity.epic", "Episch");
            case PowerUpRarity.Legendary: return Loc.Get("ui.pause.info.rarity.legendary", "Legendär");
            default:                      return Loc.Get("ui.pause.info.rarity.common", "Gewöhnlich");
        }
    }

    /// <summary>Farben wie die Karten im Mixer.</summary>
    private static Color32 RarityColor(PowerUpRarity r)
    {
        switch (r)
        {
            case PowerUpRarity.Uncommon:  return GameHudSkin.Mint;
            case PowerUpRarity.Rare:      return GameHudSkin.Icing;
            case PowerUpRarity.Epic:      return GameHudSkin.Grape;
            case PowerUpRarity.Legendary: return GameHudSkin.Gold;
            default:                      return GameHudSkin.Parchment;
        }
    }

    // ==================================================================
    //  Spielwerte, die nicht am Spieler haengen
    // ==================================================================

    private static float RegenInterval()
    {
        HPReg reg = FindAnyObjectByType<HPReg>();
        return StatCooldown(reg, 2.5f);
    }

    private static float LifeStealCooldown() => StatCooldown(LifeSteal.Instance, 1f);

    private static float StatCooldown(Weapon w, float fallback)
    {
        if (w == null || w.stats == null || w.weaponLevel < 0 || w.weaponLevel >= w.stats.Count) return fallback;
        return w.stats[w.weaponLevel].cooldown;
    }

    // ==================================================================
    //  Format
    // ==================================================================

    private static string Fmt(string key, string fallback, params object[] args)
    {
        return string.Format(Loc.Get(key, fallback), args);
    }

    private static string Num(float v, string format) => v.ToString(format);

    /// <summary>Anteil als "+12 %" / "-5 %" - 0,12 wird zu "+12 %".</summary>
    private static string Pct(float fraction)
    {
        float v = Mathf.Round(fraction * 100f);
        return (v >= 0f ? "+" : "") + v.ToString("0") + " %";
    }
}
