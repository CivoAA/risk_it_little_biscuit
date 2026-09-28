using UnityEngine;

/// <summary>
/// Alles, was der Optionen-Bildschirm einstellt - an einer Stelle, damit das
/// Fenster kein zweites Datenmodell braucht.
///
/// Gehalten wird in PlayerPrefs, mit denselben Schluesseln wie bisher
/// (<c>opt_res_w</c>, <c>opt_res_h</c>); dazu kommen Modus,
/// Schadenszahlen und Tipps. <b>Ton laeuft nicht hier durch</b> -
/// den haelt <see cref="AudioSettingsManager"/> in seiner eigenen Datei, und
/// die Sprache haelt <see cref="Loc"/>.
///
/// <see cref="Apply"/> laeuft vor der ersten Szene und stellt Aufloesung,
/// Modus und VSync wieder her.
/// </summary>
public static class GameSettings
{
    private const string KeyMode   = "opt_mode";
    private const string KeyResW   = "opt_res_w";
    private const string KeyResH   = "opt_res_h";
    private const string KeyDamage = "opt_damage_numbers";
    private const string KeyTips   = "opt_tips";

    // Der alte Schluessel aus der ersten Fassung des Optionen-Fensters: 1 =
    // Vollbild, 0 = Fenster. Wird einmal in KeyMode ueberfuehrt.
    private const string KeyLegacyFullscreen = "opt_fullscreen";

    /// <summary>0 = Fenster, 1 = Randlos, 2 = Vollbild.</summary>
    public enum DisplayMode { Window, Borderless, Fullscreen }

    /// <summary>Die drei Aufloesungen, die zur Auswahl stehen.</summary>
    public static readonly Vector2Int[] Resolutions =
    {
        new Vector2Int(1280, 720),
        new Vector2Int(1920, 1080),
        new Vector2Int(2560, 1440),
    };

    /// <summary>
    /// Index fuer "Nativ" - die letzte Wahl hinter den festen Aufloesungen.
    /// Gespeichert als 0x0, damit es auf jedem Monitor dessen eigene Aufloesung
    /// bleibt. Pixelart braucht das: alles, was der Monitor selbst hochskalieren
    /// muss (z.B. 1080p auf 1440p = Faktor 1,33), wird weich gefiltert und
    /// flimmert beim Laufen.
    /// </summary>
    public static int NativeIndex => Resolutions.Length;

    /// <summary>Anzahl der Auswahlmoeglichkeiten inklusive "Nativ".</summary>
    public static int ResolutionCount => Resolutions.Length + 1;

    /// <summary>Die Aufloesung hinter einem Index; "Nativ" liefert die des Monitors.</summary>
    public static Vector2Int ResolutionAt(int index)
    {
        if (index < 0 || index >= NativeIndex)
            return new Vector2Int(Display.main.systemWidth, Display.main.systemHeight);
        return Resolutions[index];
    }

    // ---------- Standardwerte ----------

    public const DisplayMode DefaultMode = DisplayMode.Borderless;
    public static int DefaultResIndex => NativeIndex;
    public const bool DefaultDamageNumbers = true;
    public const bool DefaultTips = true;
    public const float DefaultVolume = 1f;

    /// <summary>Feuert, wenn sich etwas geaendert hat - fuer alles, was mitziehen muss.</summary>
    public static event System.Action Changed;

    // ---------- Anzeige ----------

    public static DisplayMode Mode
    {
        get
        {
            if (PlayerPrefs.HasKey(KeyMode))
                return (DisplayMode)Mathf.Clamp(PlayerPrefs.GetInt(KeyMode), 0, 2);

            if (PlayerPrefs.HasKey(KeyLegacyFullscreen))
                return PlayerPrefs.GetInt(KeyLegacyFullscreen) == 1
                    ? DisplayMode.Borderless : DisplayMode.Window;

            return DefaultMode;
        }
        set
        {
            PlayerPrefs.SetInt(KeyMode, (int)value);
            ApplyScreen();
            Save();
        }
    }

    public static int ResolutionIndex
    {
        get
        {
            if (!PlayerPrefs.HasKey(KeyResW)) return DefaultResIndex;

            int w = PlayerPrefs.GetInt(KeyResW);
            int h = PlayerPrefs.GetInt(KeyResH, 0);
            if (w <= 0 || h <= 0) return NativeIndex;
            for (int i = 0; i < Resolutions.Length; i++)
                if (Resolutions[i].x == w && Resolutions[i].y == h) return i;

            return DefaultResIndex;
        }
        set
        {
            int index = Mathf.Clamp(value, 0, NativeIndex);
            Vector2Int r = index == NativeIndex ? Vector2Int.zero : Resolutions[index];
            PlayerPrefs.SetInt(KeyResW, r.x);
            PlayerPrefs.SetInt(KeyResH, r.y);
            ApplyScreen();
            Save();
        }
    }

    // ---------- Spiel ----------

    /// <summary>Schadenszahlen ueber den Gegnern. Aus heisst: ruhigeres Bild.</summary>
    public static bool DamageNumbers
    {
        get => PlayerPrefs.GetInt(KeyDamage, DefaultDamageNumbers ? 1 : 0) > 0;
        set { PlayerPrefs.SetInt(KeyDamage, value ? 1 : 0); Save(); }
    }

    /// <summary>
    /// Bedienhinweise: der [E]-Hinweis im Hub und die kleinen Zeilen unter den
    /// Menues ("ESC schliesst das Menue").
    /// </summary>
    public static bool Tips
    {
        get => PlayerPrefs.GetInt(KeyTips, DefaultTips ? 1 : 0) > 0;
        set { PlayerPrefs.SetInt(KeyTips, value ? 1 : 0); Save(); }
    }

    // ---------- Anwenden ----------

    /// <summary>
    /// Holt die gespeicherten Anzeige-Einstellungen zurueck, bevor die erste
    /// Szene laedt. Wurde noch nie etwas gesetzt, bleibt alles so, wie das
    /// System es vorgibt - nur die Bildrate wird gesetzt, sonst laeuft das
    /// Spiel ungebremst.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Apply()
    {
        ApplyFrameRate();
        if (PlayerPrefs.HasKey(KeyResW) || PlayerPrefs.HasKey(KeyMode)) ApplyScreen();
    }

    private static void ApplyScreen()
    {
        // Im Editor tut SetResolution nichts - testen nur im Build.
        // Die Bildwiederholrate des Monitors ausdruecklich mitgeben, sonst kann
        // exklusives Vollbild auf 60 Hz zurueckfallen.
        Vector2Int r = ResolutionAt(ResolutionIndex);
        Screen.SetResolution(r.x, r.y, ToUnityMode(Mode), Screen.currentResolution.refreshRateRatio);
    }

    /// <summary>
    /// VSync ist fest an und keine Option: ohne VSync ruckelt die Pixel-Perfect-
    /// Kamera auf 144/160-Hz-Monitoren so stark, dass das Spiel unspielbar wird
    /// (Bilder stehen mal 1, mal 2 Refreshes). Die Bildrate folgt dem Monitor.
    /// </summary>
    private static void ApplyFrameRate()
    {
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = -1;
    }

    private static FullScreenMode ToUnityMode(DisplayMode mode)
    {
        switch (mode)
        {
            case DisplayMode.Window:     return FullScreenMode.Windowed;
            case DisplayMode.Fullscreen: return FullScreenMode.ExclusiveFullScreen;
            default:                     return FullScreenMode.FullScreenWindow;
        }
    }

    /// <summary>Alles zurueck auf Werk - Anzeige, Spiel, Ton und Sprache.</summary>
    public static void ResetToDefaults()
    {
        PlayerPrefs.SetInt(KeyMode, (int)DefaultMode);
        PlayerPrefs.SetInt(KeyResW, 0);   // 0x0 = Nativ
        PlayerPrefs.SetInt(KeyResH, 0);
        PlayerPrefs.SetInt(KeyDamage, DefaultDamageNumbers ? 1 : 0);
        PlayerPrefs.SetInt(KeyTips, DefaultTips ? 1 : 0);
        PlayerPrefs.DeleteKey(KeyLegacyFullscreen);

        AudioSettingsManager audio = AudioSettingsManager.Instance;
        if (audio != null)
        {
            audio.SetMasterVolume(DefaultVolume);
            audio.SetMusicVolume(DefaultVolume);
            audio.SetEffectsVolume(DefaultVolume);
        }

        // Nicht stur Englisch: "Standard" heisst hier die Sprache, die das
        // Spiel ohne eigene Wahl genommen haette.
        Loc.SetLanguage(Loc.SystemDefault());

        ApplyFrameRate();
        ApplyScreen();
        Save();
    }

    private static void Save()
    {
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
