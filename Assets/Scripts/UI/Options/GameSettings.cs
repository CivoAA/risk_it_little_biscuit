using UnityEngine;

/// <summary>
/// Alles, was der Optionen-Bildschirm einstellt - an einer Stelle, damit das
/// Fenster kein zweites Datenmodell braucht.
///
/// Gehalten wird in PlayerPrefs, mit denselben Schluesseln wie bisher
/// (<c>opt_res_w</c>, <c>opt_res_h</c>, <c>opt_vsync</c>); dazu kommen Modus,
/// FPS-Limit, Schadenszahlen und Tipps. <b>Ton laeuft nicht hier durch</b> -
/// den haelt <see cref="AudioSettingsManager"/> in seiner eigenen Datei, und
/// die Sprache haelt <see cref="Loc"/>.
///
/// <see cref="Apply"/> laeuft vor der ersten Szene und stellt Aufloesung,
/// Modus, VSync und FPS-Limit wieder her.
/// </summary>
public static class GameSettings
{
    private const string KeyMode   = "opt_mode";
    private const string KeyResW   = "opt_res_w";
    private const string KeyResH   = "opt_res_h";
    private const string KeyVSync  = "opt_vsync";
    private const string KeyFps    = "opt_fps";
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

    /// <summary>Die drei Bildraten. 0 heisst "ohne Begrenzung".</summary>
    public static readonly int[] FpsLimits = { 60, 120, 0 };

    // ---------- Standardwerte ----------

    public const DisplayMode DefaultMode = DisplayMode.Borderless;
    public const int DefaultResIndex = 1;      // 1920x1080
    // So lief das Spiel bisher: MenuManager hat in Awake fest 60 fps ohne VSync
    // gesetzt. Wer nichts umstellt, bekommt weiterhin genau das.
    public const bool DefaultVSync = false;
    public const int DefaultFpsIndex = 0;      // 60
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
            for (int i = 0; i < Resolutions.Length; i++)
                if (Resolutions[i].x == w && Resolutions[i].y == h) return i;

            return DefaultResIndex;
        }
        set
        {
            Vector2Int r = Resolutions[Mathf.Clamp(value, 0, Resolutions.Length - 1)];
            PlayerPrefs.SetInt(KeyResW, r.x);
            PlayerPrefs.SetInt(KeyResH, r.y);
            ApplyScreen();
            Save();
        }
    }

    public static bool VSync
    {
        get => PlayerPrefs.GetInt(KeyVSync, DefaultVSync ? 1 : 0) > 0;
        set
        {
            PlayerPrefs.SetInt(KeyVSync, value ? 1 : 0);
            ApplyFrameRate();
            Save();
        }
    }

    public static int FpsIndex
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(KeyFps, DefaultFpsIndex), 0, FpsLimits.Length - 1);
        set
        {
            PlayerPrefs.SetInt(KeyFps, Mathf.Clamp(value, 0, FpsLimits.Length - 1));
            ApplyFrameRate();
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
        Vector2Int r = Resolutions[Mathf.Clamp(ResolutionIndex, 0, Resolutions.Length - 1)];
        Screen.SetResolution(r.x, r.y, ToUnityMode(Mode));
    }

    private static void ApplyFrameRate()
    {
        QualitySettings.vSyncCount = VSync ? 1 : 0;

        // Mit VSync haengt die Bildrate am Monitor - ein zusaetzliches Limit
        // wuerde nur dagegenarbeiten.
        Application.targetFrameRate = VSync ? -1 : FpsLimit;
    }

    /// <summary>Das eingestellte Limit in Bildern pro Sekunde. -1 heisst "ohne".</summary>
    public static int FpsLimit
    {
        get
        {
            int fps = FpsLimits[FpsIndex];
            return fps <= 0 ? -1 : fps;
        }
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
        PlayerPrefs.SetInt(KeyResW, Resolutions[DefaultResIndex].x);
        PlayerPrefs.SetInt(KeyResH, Resolutions[DefaultResIndex].y);
        PlayerPrefs.SetInt(KeyVSync, DefaultVSync ? 1 : 0);
        PlayerPrefs.SetInt(KeyFps, DefaultFpsIndex);
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
