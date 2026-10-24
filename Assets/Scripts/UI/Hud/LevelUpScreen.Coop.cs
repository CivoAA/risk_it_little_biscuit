using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Team-Abrechnung auf dem Abschlussbildschirm im Koop. Sie nimmt den Platz
/// von Build und "Neu freigeschaltet" ein (im Koop gibt es ohnehin nichts
/// freizuschalten): eine Zeile je Spieler mit Portraet, Name in seiner Farbe,
/// Schaden, Kills, Rettungen und wie oft er am Boden lag. Die Zahlen zaehlen
/// hoch, die beste Zeile bekommt die MVP-Krone, die eigene einen Goldrand.
///
/// Die Werte kommen live aus <see cref="NetStats"/> - schickt ein Mitspieler
/// seine letzte Zeile erst nach dem Oeffnen, stimmt sie trotzdem.
/// </summary>
public partial class LevelUpScreen
{
    private const int TallyX = 46, TallyW = 388, TallyLabelY = 158, TallyRowY = 170, TallyRowH = 13, TallyRowStep = 14;
    private const int TallyWin = 11;
    private static readonly int[] TallyCols = { 204, 260, 316, 364 };   // Mitte der Spalten, relativ zu TallyX
    private const int TallyColW = 50;

    private RectTransform tallyRoot;
    private PixText tallyLabel;
    private readonly Image[] tallyHeadIcons = new Image[4];
    private readonly TallyRow[] tallyRows = new TallyRow[NetSession.MaxPlayers];
    private readonly int[] tallySlots = new int[NetSession.MaxPlayers];
    private int tallyCount, tallyMvp = -1;
    private Image tallyCrown;

    private sealed class TallyRow
    {
        public RectTransform Root;
        public Image Back, Glow, Stripe, Portrait;
        public PixText Name;
        public readonly PixText[] Values = new PixText[4];
    }

    private void BuildCoopTally()
    {
        tallyRoot = NewRect("Tally", deathLayer);
        Place(tallyRoot, new RectInt(TallyX, TallyLabelY, TallyW, 70));

        tallyLabel = Text("Label", tallyRoot, new RectInt(0, 0, 200, 11), SizeText, GameHudSkin.ParchDark,
                          TextAlignmentOptions.Left, TextStyle.Outline);
        Sprite[] heads = { GameHudSkin.Sword, GameHudSkin.Skull, GameHudSkin.ReviveHeart, GameHudSkin.Ghost };
        for (int c = 0; c < heads.Length; c++)
        {
            int w = Mathf.RoundToInt(heads[c].rect.width), h = Mathf.RoundToInt(heads[c].rect.height);
            tallyHeadIcons[c] = Img("Head" + c, tallyRoot, new RectInt(TallyCols[c] - w / 2, 1, w, h), heads[c], Color.white);
        }

        for (int i = 0; i < tallyRows.Length; i++)
        {
            var row = new TallyRow();
            row.Root = NewRect("Row" + i, tallyRoot);
            Place(row.Root, new RectInt(0, TallyRowY - TallyLabelY + i * TallyRowStep, TallyW, TallyRowH));
            row.Glow = Img("Glow", row.Root, new RectInt(-1, -1, TallyW + 2, TallyRowH + 2), GameHudSkin.Plate,
                           new Color(1f, 1f, 1f, 0f), true);
            row.Back = Img("Back", row.Root, new RectInt(0, 0, TallyW, TallyRowH), GameHudSkin.Plate, Color.white, true);
            row.Stripe = Img("Stripe", row.Root, new RectInt(2, 2, 2, TallyRowH - 4), GameHudSkin.White, Color.white);
            RectTransform win = NewRect("Window", row.Root);
            Place(win, new RectInt(6, 1, TallyWin, TallyWin));
            win.gameObject.AddComponent<RectMask2D>();
            row.Portrait = Img("Portrait", win, new RectInt(0, 0, 42, 42), null, Color.white);
            row.Name = Text("Name", row.Root, new RectInt(21, 1, 150, 11), SizeText, GameHudSkin.Cream,
                            TextAlignmentOptions.Left, TextStyle.Outline);
            for (int c = 0; c < row.Values.Length; c++)
                row.Values[c] = Text("Value" + c, row.Root, new RectInt(TallyCols[c] - TallyColW / 2, 1, TallyColW, 11),
                                     SizeText, GameHudSkin.Parchment, TextAlignmentOptions.Center, TextStyle.Outline);
            tallyRows[i] = row;
        }

        Sprite crownSprite = GameHudSkin.Crown;
        tallyCrown = Img("Mvp", tallyRoot, new RectInt(0, 0, Mathf.RoundToInt(crownSprite.rect.width),
                                                       Mathf.RoundToInt(crownSprite.rect.height)), crownSprite, Color.white);
        tallyRoot.gameObject.SetActive(false);
    }

    /// <summary>Beim Oeffnen: im Koop Build und Neu ausblenden und die Zeilen fuellen.</summary>
    private void EnterCoopTally()
    {
        NetRun run = NetRun.Active;
        NetStats stats = NetStats.Active;
        bool coop = coopEnd && run != null && stats != null;
        SetActive(tallyRoot, false);
        if (!coop) return;

        // Statt Build und Neuigkeiten.
        deathTileCount = 0;
        newCount = 0;
        for (int i = 0; i < deathTiles.Count; i++) deathTiles[i].Root.gameObject.SetActive(false);

        tallyLabel.Set(Loc.Get("net.tally.title", "TEAM"));
        tallyCount = 0;
        int local = NetSession.Instance != null ? NetSession.Instance.LocalSlot : -1;
        for (int slot = 0; slot < run.Peers.Length; slot++)
        {
            NetRun.Peer p = run.Peers[slot];
            if (!p.Playing || tallyCount >= tallyRows.Length) continue;
            tallySlots[tallyCount] = slot;
            TallyRow row = tallyRows[tallyCount++];

            Color accent = CharacterLooks.AccentFor(p.Character);
            row.Stripe.color = accent;
            row.Name.Set(slot == local ? p.Name.ToUpperInvariant() + " (" + Loc.Get("net.run.you", "DU") + ")"
                                       : p.Name.ToUpperInvariant());
            row.Name.SetColor(accent);
            row.Glow.color = slot == local ? OptionsKit.WithAlpha(GameHudSkin.Gold, 0.55f) : new Color(1f, 1f, 1f, 0f);
            row.Back.color = p.State == NetRun.PeerState.Left ? new Color(0.7f, 0.7f, 0.7f, 0.8f) : Color.white;

            Sprite[] portrait = CharacterLooks.PortraitFor(p.Character);
            row.Portrait.enabled = portrait != null;
            if (portrait != null)
            {
                Sprite s = portrait[0];
                row.Portrait.sprite = s;
                // Halbe Groesse (ganzer Teiler, Pixel bleiben gleich breit), Figur mittig im Fenster.
                int pw = Mathf.RoundToInt(s.rect.width) / 2, ph = Mathf.RoundToInt(s.rect.height) / 2;
                row.Portrait.rectTransform.sizeDelta = new Vector2(pw, ph);
                SetPos(row.Portrait.rectTransform, -Mathf.Round((pw - TallyWin) / 2f), Mathf.Round((ph - TallyWin) / 2f));
            }
        }
        for (int i = tallyCount; i < tallyRows.Length; i++) SetActive(tallyRows[i].Root, false);
    }

    private void UpdateCoopTally(float t, float now)
    {
        if (!coopEnd || tallyCount == 0 || NetStats.Active == null)
        {
            if (tallyRoot != null) SetActive(tallyRoot, false);
            return;
        }

        float tt = t - TBuild;
        SetActive(tallyRoot, tt >= 0f);
        if (tt < 0f) return;

        NetStats stats = NetStats.Active;
        tallyMvp = stats.Mvp();

        for (int i = 0; i < tallyCount; i++)
        {
            TallyRow row = tallyRows[i];
            float rt = tt - i * 0.12f;
            SetActive(row.Root, rt >= 0f);
            if (rt < 0f) continue;

            // Gleitet von links herein und setzt hart auf.
            int slide = rt < 0.05f ? -24 : rt < 0.1f ? -8 : rt < 0.14f ? 2 : 0;
            SetPos(row.Root, slide, -(TallyRowY - TallyLabelY + i * TallyRowStep));

            NetStats.Line line = stats.Lines[tallySlots[i]];
            float k = Mathf.Clamp01((rt - 0.1f) / 0.7f);
            k = 1f - (1f - k) * (1f - k);
            row.Values[0].Set(FormatDamage(line.Damage * k));
            row.Values[1].Set(Mathf.RoundToInt(line.Kills * k).ToString(CultureInfo.InvariantCulture));
            row.Values[2].Set(Mathf.RoundToInt(line.Revives * k).ToString(CultureInfo.InvariantCulture));
            row.Values[3].Set(Mathf.RoundToInt(line.Downs * k).ToString(CultureInfo.InvariantCulture));
            row.Values[2].SetColor(line.Revives > 0 ? (Color)GameHudSkin.MintLight : (Color)GameHudSkin.Parchment);
            row.Values[3].SetColor(line.Downs > 0 ? (Color)GameHudSkin.IcingLight : (Color)GameHudSkin.Parchment);
        }

        // MVP-Krone faellt auf die beste Zeile, sobald alle Zahlen stehen.
        float ct = tt - tallyCount * 0.12f - 0.8f;
        int mvpRow = -1;
        for (int i = 0; i < tallyCount; i++) if (tallySlots[i] == tallyMvp) mvpRow = i;
        bool crownOn = ct >= 0f && mvpRow >= 0 && tallyCount > 1;
        SetActive(tallyCrown, crownOn);
        if (crownOn)
        {
            TallyRow row = tallyRows[mvpRow];
            int nameW = Mathf.CeilToInt(row.Name.Width);
            int drop = ct < 0.06f ? -10 : ct < 0.12f ? -4 : ct < 0.16f ? 1 : 0;
            int bob = Mathf.Repeat(now * 1.5f, 1f) < 0.5f ? 0 : 1;
            SetPos(tallyCrown.rectTransform, 21 + Mathf.Min(nameW, 150) + 3,
                   -(TallyRowY - TallyLabelY + mvpRow * TallyRowStep + 1 + drop + bob));
            row.Values[0].SetColor(GameHudSkin.GoldLight);
        }
    }

    private static string FormatDamage(float v)
    {
        int d = Mathf.RoundToInt(v);
        if (d >= 1000000) return (d / 1000000f).ToString("0.0", CultureInfo.InvariantCulture) + "M";
        if (d >= 10000) return (d / 1000) + "K";
        return d.ToString(CultureInfo.InvariantCulture);
    }
}
