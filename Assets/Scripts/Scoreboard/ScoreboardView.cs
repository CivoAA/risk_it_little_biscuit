using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Der Inhalt eines Scoreboards, ohne Rahmen: Welt (Pfeile), Freunde |
/// Weltweit, die Zeilen mit Platz, Gesicht, Name und Punkten, darunter ein
/// Hinweis. Steckt im Hauptmenue (<see cref="MainMenuScoreboard"/>) und im
/// Hub am Terminal (<see cref="HubScoreboardPanel"/>) - beide zeigen also
/// dasselbe. Daten aus <see cref="SteamScoreboard"/>.
///
/// Gewaehlte Welt und Ansicht merkt sich das Profil (<see cref="ProfilePrefs"/>),
/// fuer Menue und Hub gemeinsam.
///
/// Wer die Ansicht anlegt, ruft <see cref="Build"/> (auch nach Sprachwechsel
/// in einen frisch geleerten Elternteil) und am Ende <see cref="Dispose"/>.
/// </summary>
public sealed class ScoreboardView
{
    public const int MapY = 0, TabsY = 16, RowsY = 35, RowH = 15, FootH = 14;
    public const int MaxRows = SteamScoreboard.TopCount + 1;   // + eigene Zeile

    /// <summary>Hoehe ab <see cref="MapY"/> bis unter den Hinweis.</summary>
    public const int Height = RowsY + MaxRows * RowH + 3 + FootH;

    /// <summary>Gesicht in der Zeile: Mitte des 42er-Portraets, halb so gross.</summary>
    private const int FaceCrop = 8, FaceS = 13;

    /// <summary>So lange gilt ein geladenes Board, bevor beim Zurueckblaettern neu gefragt wird.</summary>
    private const float CacheSeconds = 60f;

    private const string MapKey = "scoreboard_map";
    private const string ScopeKey = "scoreboard_scope";

    private readonly int width;
    private readonly Func<bool> ready;
    private TMP_FontAsset font;
    private RectTransform rowsRoot;
    private TextMeshProUGUI mapLabel, message;
    private SkinButton friendsButton, globalButton;

    private int map;
    private SteamScoreboard.Scope scope;
    private int request;
    private bool alive = true;
    private List<ScoreEntry> shown;

    private readonly Dictionary<string, (float Time, List<ScoreEntry> Rows)> cache =
        new Dictionary<string, (float, List<ScoreEntry>)>();

    private readonly Dictionary<int, Sprite> faces = new Dictionary<int, Sprite>();

    /// <param name="width">Breite in Seitenpixeln.</param>
    /// <param name="ready">Darf gerade geklickt werden? (Einblenden, Fenster darueber)</param>
    public ScoreboardView(int width, Func<bool> ready)
    {
        this.width = width;
        this.ready = ready ?? (() => true);
        ReadPrefs();

        SteamScoreboard.RetryUnsent();
        SaveProfiles.Changed += OnProfileChanged;
        SteamScoreboard.Uploaded += OnUploaded;
        SteamScoreboard.NamesChanged += OnNamesChanged;
    }

    public void Dispose()
    {
        alive = false;
        SaveProfiles.Changed -= OnProfileChanged;
        SteamScoreboard.Uploaded -= OnUploaded;
        SteamScoreboard.NamesChanged -= OnNamesChanged;
        foreach (Sprite s in faces.Values) if (s != null) UnityEngine.Object.Destroy(s);
        faces.Clear();
    }

    private string World => SteamScoreboard.Worlds[map];

    private void ReadPrefs()
    {
        map = Mathf.Clamp(ProfilePrefs.GetInt(MapKey, 0), 0, SteamScoreboard.Worlds.Count - 1);
        scope = ProfilePrefs.GetInt(ScopeKey, 0) == 1 ? SteamScoreboard.Scope.Global : SteamScoreboard.Scope.Friends;
    }

    private void OnProfileChanged()
    {
        ReadPrefs();
        if (mapLabel == null) return;
        mapLabel.text = SteamScoreboard.WorldName(World);
        RefreshTabs();
        Load(false);
    }

    private void OnUploaded(string boardName)
    {
        cache.Clear();
        Load(true);
    }

    private void OnNamesChanged()
    {
        if (shown == null) return;
        for (int i = 0; i < shown.Count; i++)
        {
            ScoreEntry e = shown[i];
            e.Name = SteamScoreboard.NameOf(e.User);
            shown[i] = e;
        }
        ShowRows(shown);
    }

    // ==================================================================
    //  Aufbau
    // ==================================================================

    /// <summary>Baut die Ansicht in <paramref name="parent"/>, oben links bei (x, y).</summary>
    public void Build(RectTransform parent, int x, int y, TMP_FontAsset textFont)
    {
        font = textFont;
        RectTransform root = OptionsKit.Rect("ScoreboardView", parent, x, y, width, Height);
        int w = width;

        // Welt: < Name >
        SkinButton.Create(root, 1, MapY, 14, 13, "<", font, SkinButton.Kind.Wood, () => StepMap(-1));
        SkinButton.Create(root, w - 15, MapY, 14, 13, ">", font, SkinButton.Kind.Wood, () => StepMap(1));
        mapLabel = OptionsKit.Label("Map", root, 17, MapY, w - 34, 12, SteamScoreboard.WorldName(World), font,
                                    OptionsKit.SizeText, GameHudSkin.Cream, TextAlignmentOptions.Center);

        // Freunde | Weltweit
        int tabW = (w - 4) / 2;
        friendsButton = SkinButton.Create(root, 1, TabsY, tabW, 14, Loc.Get("ui.scoreboard.friends", "FRIENDS"), font,
                                          SkinButton.Kind.Wood, () => SetScope(SteamScoreboard.Scope.Friends));
        globalButton = SkinButton.Create(root, 3 + tabW, TabsY, tabW, 14, Loc.Get("ui.scoreboard.global", "WORLDWIDE"),
                                         font, SkinButton.Kind.Wood, () => SetScope(SteamScoreboard.Scope.Global));
        RefreshTabs();

        OptionsKit.Img("Well", root, 0, RowsY - 2, w, MaxRows * RowH + 3, GameHudSkin.White, (Color)GameHudSkin.CardDeep);
        rowsRoot = OptionsKit.Rect("Rows", root, 0, 0, w, Height);
        message = OptionsKit.Label("Message", root, 2, RowsY + 3 * RowH, w - 4, 12, "", font, OptionsKit.SizeText,
                                   GameHudSkin.StoneLight, TextAlignmentOptions.Center);

        string foot = Footer();
        if (foot != null)
            OptionsKit.Label("Foot", root, 2, Height - FootH, w - 4, 12, foot, font, OptionsKit.SizeText,
                             ScoreIntegrity.BlockedForPlayer ? GameHudSkin.JamLight : GameHudSkin.StoneLight,
                             TextAlignmentOptions.Center);

        if (shown != null) ShowRows(shown);
        else Load(false);
    }

    /// <summary>Zeile unter dem Board: warum eigene Laeufe nicht zaehlen, oder wie man reinkommt.</summary>
    private static string Footer()
    {
        if (ScoreIntegrity.ModsDetected) return Loc.Get("ui.scoreboard.mods", "MODS FOUND: NOT COUNTED");
        if (ScoreIntegrity.Cheated) return Loc.Get("ui.scoreboard.cheats", "CHEATS USED: NOT COUNTED");
        if (!Unlocks.Endless.IsUnlocked) return Loc.Get("ui.scoreboard.locked", "ENDLESS AFTER THE STORY");
        return null;
    }

    private void RefreshTabs()
    {
        if (friendsButton == null) return;
        friendsButton.Active = scope == SteamScoreboard.Scope.Friends;
        globalButton.Active = scope == SteamScoreboard.Scope.Global;
    }

    // ==================================================================
    //  Bedienung (auch per Tastatur aus dem Hub-Fenster)
    // ==================================================================

    public void StepMap(int dir)
    {
        if (!ready()) return;
        int n = SteamScoreboard.Worlds.Count;
        map = ((map + dir) % n + n) % n;
        ProfilePrefs.SetInt(MapKey, map);
        if (mapLabel != null) mapLabel.text = SteamScoreboard.WorldName(World);
        Load(false);
    }

    public void SetScope(SteamScoreboard.Scope s)
    {
        if (!ready() || scope == s) return;
        scope = s;
        ProfilePrefs.SetInt(ScopeKey, s == SteamScoreboard.Scope.Global ? 1 : 0);
        RefreshTabs();
        Load(false);
    }

    public void ToggleScope() =>
        SetScope(scope == SteamScoreboard.Scope.Friends ? SteamScoreboard.Scope.Global : SteamScoreboard.Scope.Friends);

    private void Load(bool force)
    {
        int token = ++request;

        if (!SteamScoreboard.Available)
        {
            shown = null;
            ShowMessage(Loc.Get("ui.scoreboard.nosteam", "STEAM NOT CONNECTED"));
            return;
        }

        string key = World + "|" + scope;
        if (!force && cache.TryGetValue(key, out var hit) && Time.unscaledTime - hit.Time < CacheSeconds)
        {
            shown = hit.Rows;
            ShowRows(shown);
            return;
        }

        shown = null;
        ShowMessage(Loc.Get("ui.scoreboard.loading", "LOADING..."));
        SteamScoreboard.Download(World, scope, rows =>
        {
            if (!alive || token != request) return;   // Fenster zu oder schon weitergeblaettert
            if (rows == null)
            {
                ShowMessage(Loc.Get("ui.scoreboard.error", "STEAM DIDN'T ANSWER"));
                return;
            }
            cache[key] = (Time.unscaledTime, rows);
            shown = rows;
            ShowRows(rows);
        });
    }

    // ==================================================================
    //  Zeilen
    // ==================================================================

    private void ShowMessage(string text)
    {
        if (rowsRoot == null) return;
        OptionsKit.Clear(rowsRoot);
        message.text = text;
    }

    private void ShowRows(List<ScoreEntry> rows)
    {
        if (rowsRoot == null) return;
        OptionsKit.Clear(rowsRoot);

        if (rows.Count == 0)
        {
            message.text = Loc.Get("ui.scoreboard.empty", "NO SCORES YET");
            return;
        }
        message.text = "";

        int w = width;
        int y = RowsY;
        for (int i = 0; i < rows.Count && i < MaxRows; i++)
        {
            ScoreEntry e = rows[i];

            // Die eigene Zeile hinter den Besten: ein Strich trennt die Luecke ab.
            if (i > 0 && e.Rank > rows[i - 1].Rank + 1)
                OptionsKit.Img("Gap", rowsRoot, 4, y - 1, w - 8, 1, GameHudSkin.White, (Color)GameHudSkin.StoneDark);

            Color c = e.IsMe ? (Color)GameHudSkin.Gold : e.Rank <= 3 ? (Color)GameHudSkin.Cream : (Color)GameHudSkin.ParchDark;
            if (e.IsMe) OptionsKit.Img("Me", rowsRoot, 1, y, w - 2, RowH, GameHudSkin.White, OptionsKit.WithAlpha(GameHudSkin.Gold, 0.15f));

            int textY = y + (RowH - 12) / 2;
            OptionsKit.Label("Rank", rowsRoot, 0, textY, 18, 12, e.Rank + ".", font, OptionsKit.SizeText, c,
                             TextAlignmentOptions.Right);

            Sprite face = FaceOf(e.Character);
            if (face != null) OptionsKit.Img("Face", rowsRoot, 21, y + 1, FaceS, FaceS, face);

            int nameX = 21 + FaceS + 3;
            TextMeshProUGUI name = OptionsKit.Label("Name", rowsRoot, nameX, textY, w - nameX - 56, 12,
                                                    e.Name, font, OptionsKit.SizeText, c, TextAlignmentOptions.Left);
            name.overflowMode = TextOverflowModes.Ellipsis;
            OptionsKit.Label("Score", rowsRoot, w - 56, textY, 54, 12, ProfilePanel.FormatNumber(e.Score), font,
                             OptionsKit.SizeText, c, TextAlignmentOptions.Right);
            y += RowH;
        }
    }

    /// <summary>Gesicht eines Charakters fuer die Zeile, wie im Profil-Schild aus der Portraet-Mitte geschnitten.</summary>
    private Sprite FaceOf(int character)
    {
        if (character < 0 || character >= Characters.Count) return null;
        if (faces.TryGetValue(character, out Sprite cached)) return cached;

        Sprite face = null;
        Sprite[] frames = CharacterLooks.PortraitFor(character);
        if (frames != null && frames.Length > 0 && frames[0] != null)
        {
            Rect r = frames[0].textureRect;
            face = Sprite.Create(frames[0].texture,
                                 new Rect(r.x + FaceCrop, r.y + FaceCrop, r.width - 2 * FaceCrop, r.height - 2 * FaceCrop),
                                 new Vector2(0.5f, 0.5f), 100f);
        }
        faces[character] = face;
        return face;
    }
}
