using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Klaenge der Waffen. Die Clips liegen in Resources/Sounds/Weapons als
/// &lt;name&gt;_0.wav, &lt;name&gt;_1.wav ... (erzeugt von waffen_sounds.py im
/// Artwork-Ordner) - jede Variante klingt etwas anders, damit Dauerfeuer nicht
/// nach Maschinengewehr klingt.
///
/// Gegen Nerven: jeder Sound hat eine Mindestpause (gap). Was in der Pause
/// noch einmal ausgeloest wird, faellt einfach weg - zehn Explosionen im selben
/// Frame klingen wie eine. Dazu leichte Tonhoehen-Streuung und nie zweimal
/// dieselbe Variante hintereinander.
///
/// Wie laut ein Sound ingame ist, steht nur hier in der Tabelle (volume); die
/// WAVs selbst sind alle gleich laut normiert. Ausgabe ueber die Effekt-Gruppe
/// des AudioControllers (Lautstaerke-Regler), dazu der eigene Waffen-Regler
/// aus den Optionen (AudioSettingsManager.WeaponsScale).
/// </summary>
public static class WeaponSounds
{
    /// <summary>Ein Waffen-Sound: Dateiname, Lautstaerke, Mindestpause, Tonhoehen-Streuung.</summary>
    public sealed class Sfx
    {
        public readonly string file;
        public readonly float volume;
        public readonly float gap;
        public readonly float pitchJitter;
        public readonly float pitch;

        internal AudioClip[] clips;
        internal float nextTime;
        internal int lastVariant = -1;

        public Sfx(string file, float volume, float gap, float pitchJitter = 0.05f, float pitch = 1f)
        {
            this.file = file;
            this.volume = volume;
            this.gap = gap;
            this.pitchJitter = pitchJitter;
            this.pitch = pitch;
        }
    }

    // ------------------------------------------------------------ Tabelle
    // volume: Spitzen-RMS der WAVs ist ~0.22 - mal volume ergibt die Lautheit
    // ingame. Zum Vergleich: der Gegner-Tod liegt bei ~0.075, also volume ~0.34.

    // Dauerfeuer - leise und kurz
    public static readonly Sfx ChocoShot      = new Sfx("choco_shot",      0.13f, 0.045f, 0.07f);
    public static readonly Sfx TurretShot     = new Sfx("turret_shot",     0.09f, 0.06f,  0.08f);
    public static readonly Sfx CrumbDrop      = new Sfx("crumb_drop",      0.06f, 0.2f,   0.1f);
    public static readonly Sfx SawCrunch      = new Sfx("saw_crunch",      0.10f, 0.11f,  0.1f);
    public static readonly Sfx StarBounce     = new Sfx("star_bounce",     0.08f, 0.12f,  0f);
    public static readonly Sfx KunaiThrow     = new Sfx("kunai_throw",     0.12f, 0.05f,  0.06f);
    public static readonly Sfx BobaEvoShot    = new Sfx("boba_shot",       0.10f, 0.09f,  0.1f, 1.05f);

    // Schuesse / Wuerfe
    public static readonly Sfx BobaShot       = new Sfx("boba_shot",       0.16f, 0.08f,  0.07f);
    public static readonly Sfx ButterShot     = new Sfx("butter_shot",     0.20f, 0.08f,  0.05f);
    public static readonly Sfx FireballShot   = new Sfx("fireball_shot",   0.15f, 0.08f,  0.06f);
    public static readonly Sfx StarLaunch     = new Sfx("star_launch",     0.14f, 0.3f,   0.02f);
    public static readonly Sfx CandyDrop      = new Sfx("candy_drop",      0.12f, 0.15f,  0.02f);
    public static readonly Sfx KunaiSpawn     = new Sfx("kunai_spawn",     0.07f, 0.15f,  0.04f);
    public static readonly Sfx VoidKunaiThrow = new Sfx("voidkunai_throw", 0.13f, 0.05f,  0.06f);
    public static readonly Sfx BoomerangThrow = new Sfx("boomerang_throw", 0.16f, 0.1f,   0.05f);
    public static readonly Sfx BoomerangEvo   = new Sfx("boomerang_evo",   0.22f, 0.2f,   0.04f);
    public static readonly Sfx ShuriThrow     = new Sfx("shuri_throw",     0.15f, 0.08f,  0.06f);
    public static readonly Sfx ShuriBlast     = new Sfx("shuriblast_throw",0.18f, 0.1f,   0.05f);
    public static readonly Sfx ForkStab       = new Sfx("fork_stab",       0.15f, 0.1f,   0.05f);
    public static readonly Sfx BloodyFork     = new Sfx("bloodyfork_stab", 0.18f, 0.15f,  0.05f);
    public static readonly Sfx SlashSwing     = new Sfx("slash_swing",     0.17f, 0.06f,  0.06f);
    public static readonly Sfx SlashFinisher  = new Sfx("slash_finisher",  0.22f, 0.1f,   0.04f);
    public static readonly Sfx SaladGust      = new Sfx("salad_gust",      0.16f, 0.1f,   0.06f);
    public static readonly Sfx TyphoonGust    = new Sfx("typhoon_gust",    0.20f, 0.12f,  0.05f);
    public static readonly Sfx LaserShot      = new Sfx("laser_shot",      0.13f, 0.07f,  0.05f);
    public static readonly Sfx TurretBuild    = new Sfx("turret_build",    0.18f, 0.15f,  0.04f);
    public static readonly Sfx FoxfireLaunch  = new Sfx("foxfire_launch",  0.13f, 0.07f,  0.04f);
    public static readonly Sfx SplatThrow     = new Sfx("splat_throw",     0.14f, 0.08f,  0.08f);

    // Flaechen / Einschlaege
    public static readonly Sfx FireballBoom   = new Sfx("fireball_boom",   0.22f, 0.1f,   0.07f);
    public static readonly Sfx CandyBoom      = new Sfx("candy_boom",      0.26f, 0.15f,  0.04f);
    public static readonly Sfx SawBoom        = new Sfx("saw_boom",        0.15f, 0.1f,   0.08f);
    public static readonly Sfx StarBoom       = new Sfx("star_boom",       0.17f, 0.1f,   0.07f);
    public static readonly Sfx CoffeePool     = new Sfx("coffee_pool",     0.20f, 0.2f,   0.05f);
    public static readonly Sfx JamSplat       = new Sfx("jam_splat",       0.17f, 0.09f,  0.07f);
    public static readonly Sfx MilkWave       = new Sfx("milk_wave",       0.22f, 0.15f,  0.04f);
    public static readonly Sfx MilkCrown      = new Sfx("milk_crown",      0.24f, 0.3f,   0.04f);
    public static readonly Sfx SawSpawn       = new Sfx("saw_spawn",       0.15f, 0.3f,   0.04f);
    public static readonly Sfx VortexSpawn    = new Sfx("vortex_spawn",    0.20f, 0.2f,   0.05f);
    public static readonly Sfx VoidSpike      = new Sfx("void_spike",      0.18f, 0.08f,  0.06f);
    public static readonly Sfx Deathstrike    = new Sfx("deathstrike",     0.19f, 0.07f,  0.08f);
    public static readonly Sfx FoxfireBurst   = new Sfx("foxfire_burst",   0.15f, 0.08f,  0.07f);
    public static readonly Sfx SplatLand      = new Sfx("splat_land",      0.20f, 0.08f,  0.07f);

    // Kyuubi (Fuchsfeuer-Evo): Seelen leise, das Erwachen gross
    public static readonly Sfx KyubiSoul      = new Sfx("kyubi_soul",      0.10f, 0.06f,  0.03f);
    public static readonly Sfx KyubiRise      = new Sfx("kyubi_rise",      0.30f, 1.0f,   0.02f);
    public static readonly Sfx KyubiSlam      = new Sfx("kyubi_slam",      0.34f, 1.0f,   0.03f);
    public static readonly Sfx KyubiHowl      = new Sfx("kyubi_howl",      0.24f, 1.0f,   0.02f);

    // ------------------------------------------------------------ Abspielen

    private const string Folder = "Sounds/Weapons/";
    private const int Voices = 14;

    private static readonly List<AudioSource> sources = new List<AudioSource>();
    private static GameObject host;
    private static int nextSource;

    /// <summary>
    /// Spielt <paramref name="sfx"/>, wenn seine Mindestpause um ist.
    /// <paramref name="volumeScale"/> z.B. fuer leisere Echos,
    /// <paramref name="pitchScale"/> fuer Varianten einer Waffe (Evo tiefer).
    /// </summary>
    public static void Play(Sfx sfx, float volumeScale = 1f, float pitchScale = 1f)
    {
        if (sfx == null) return;

        float now = Time.unscaledTime;
        if (now < sfx.nextTime) return;

        AudioClip clip = PickClip(sfx);
        if (clip == null) return;

        AudioSource src = NextSource();
        if (src == null) return;

        sfx.nextTime = now + sfx.gap;

        src.Stop();
        src.clip = clip;
        src.volume = Mathf.Clamp01(sfx.volume * volumeScale * AudioSettingsManager.WeaponsScale);
        src.pitch = sfx.pitch * pitchScale * (1f + Random.Range(-sfx.pitchJitter, sfx.pitchJitter));
        src.Play();
    }

    /// <summary>Zufaellige Variante, aber nie zweimal dieselbe hintereinander.</summary>
    private static AudioClip PickClip(Sfx sfx)
    {
        if (sfx.clips == null) sfx.clips = Load(sfx.file);
        int count = sfx.clips.Length;
        if (count == 0) return null;

        int pick = Random.Range(0, count);
        if (count > 1 && pick == sfx.lastVariant) pick = (pick + 1 + Random.Range(0, count - 1)) % count;
        sfx.lastVariant = pick;
        return sfx.clips[pick];
    }

    private static readonly Dictionary<string, AudioClip[]> cache = new Dictionary<string, AudioClip[]>();

    private static AudioClip[] Load(string file)
    {
        if (cache.TryGetValue(file, out AudioClip[] cached)) return cached;

        var list = new List<AudioClip>();
        for (int i = 0; i < 16; i++)
        {
            AudioClip clip = Resources.Load<AudioClip>(Folder + file + "_" + i);
            if (clip == null) break;
            list.Add(clip);
        }
        if (list.Count == 0) Debug.LogWarning($"[WeaponSounds] Keine Clips fuer '{file}' in Resources/{Folder}.");

        AudioClip[] clips = list.ToArray();
        cache[file] = clips;
        return clips;
    }

    /// <summary>
    /// Freie Stimme aus dem Pool - sind alle belegt, wird die aelteste
    /// abgeschnitten. Der Pool lebt in der Lauf-Szene und wird nach jedem
    /// Lauf neu angelegt.
    /// </summary>
    private static AudioSource NextSource()
    {
        if (host == null) CreatePool();
        if (sources.Count == 0) return null;

        for (int i = 0; i < sources.Count; i++)
        {
            int k = (nextSource + i) % sources.Count;
            if (sources[k] != null && !sources[k].isPlaying)
            {
                nextSource = (k + 1) % sources.Count;
                return sources[k];
            }
        }

        AudioSource oldest = sources[nextSource];
        nextSource = (nextSource + 1) % sources.Count;
        return oldest;
    }

    private static void CreatePool()
    {
        sources.Clear();
        nextSource = 0;

        host = new GameObject("WeaponSounds");
        RunScene.Place(host, "Effekte");

        AudioMixerGroup group = null;
        if (AudioController.Instance != null && AudioController.Instance.EarthHit != null)
            group = AudioController.Instance.EarthHit.outputAudioMixerGroup;

        for (int i = 0; i < Voices; i++)
        {
            AudioSource src = host.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f;
            src.priority = 160;   // Musik und Boss-Sounds gehen vor
            src.outputAudioMixerGroup = group;
            sources.Add(src);
        }
    }
}
