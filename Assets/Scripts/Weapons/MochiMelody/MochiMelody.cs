using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mochi-Melodie - Mochis Startwaffe (nur fuer sie). Mochi singt im Takt:
/// auf den Schlaegen 1-3 fliegt je eine Note los und jagt mit kleinem
/// Schlenker einen Gegner (<see cref="MelodyNote"/>), auf der Vier platzt ein
/// bunter Akkord-Ring um sie herum, der alles darin trifft, wegschubst und
/// kurz bremst. Jede Note, die im Takt trifft, macht den Akkord lauter
/// (Crescendo). Die Toene spielen dabei wirklich eine kleine Melodie.
///
/// cooldown = Laenge eines Schlags in Sekunden (Cooldown-Buff = schnelleres Tempo)
/// damage   = Treffer einer Note; der Akkord macht <see cref="ChordFactor"/> davon (+ Crescendo)
/// range    = Zielsuche der Noten in Tiles
/// duration = so lange bremst der Akkord (Duration-Buff zaehlt)
/// shots    = Noten pro Schlag (+ Extra-Schuss 1:1)
///
/// Die Werte stehen im Code (<see cref="LevelStats"/>), nicht am Prefab - sie
/// ueberschreiben beim Start, was im Inspector steht. Bilder und Toene aus
/// Tools/mochi_melodie.py (Resources/Weapons/melody_*, Resources/Sounds/melody_*).
/// </summary>
public class MochiMelody : Weapon
{
    /// <summary>Akkord-Schaden im Verhaeltnis zum Notenschaden (ohne Crescendo).</summary>
    public const float ChordFactor = 1.5f;

    /// <summary>Jede Note, die im Takt trifft, legt so viel auf den Akkord drauf ...</summary>
    public const float CrescendoStep = 0.15f;

    /// <summary>... hoechstens bis zu diesem Aufschlag.</summary>
    public const float CrescendoMax = 1.2f;

    /// <summary>Radius des Akkord-Rings in Tiles (vor AOE-Buff und Stufen-Faktor).</summary>
    public const float ChordRadius = 1.6f;

    /// <summary>So stark schubst der Akkord nach aussen (Tiles pro Sekunde, kurz).</summary>
    public const float ChordShove = 6f;

    /// <summary>Tempo-Faktor der Gegner, die der Akkord getroffen hat.</summary>
    public const float ChordSlow = 0.6f;

    /// <summary>Notenkette: so weit springt eine Note nach dem Treffer weiter.</summary>
    public const float BounceRange = 3.2f;

    /// <summary>Kuerzester Schlag, egal wie viel Cooldown-Buff - sonst wird es Rauschen.</summary>
    private const float MinBeat = 0.22f;

    private static readonly WeaponStats[] LevelStats =
    {
        //    Schlag  damage  Reichweite  Bremse  Noten
        Level(0.55f, 4f, 6.5f, 0.6f, 1, "Mochi singt: drei Noten pro Takt jagen Gegner, auf der Vier platzt ein Akkord-Ring"),
        Level(0.55f, 5f, 6.5f, 0.6f, 1, "Schaden +1, der Akkord bremst länger"),
        Level(0.50f, 5f, 7f,   0.8f, 2, "Zwei Noten pro Schlag, schnelleres Tempo"),
        Level(0.50f, 6f, 7f,   0.8f, 2, "Notenkette: Jede Note springt nach dem Treffer zu einem zweiten Gegner"),
        Level(0.46f, 7f, 7.5f, 1.0f, 2, "Schaden +1, schnelleres Tempo, größerer Akkord-Ring"),
        Level(0.44f, 8f, 8f,   1.2f, 3, "Großes Finale: drei Noten pro Schlag, jeder vierte Takt endet mit einem Notenregen"),
    };

    /// <summary>So oft springt eine Note nach dem ersten Treffer weiter.</summary>
    private static readonly int[] BouncesPerLevel = { 0, 0, 0, 1, 1, 2 };

    /// <summary>Faktor auf den Akkord-Ring je Stufe.</summary>
    private static readonly float[] ChordPerLevel = { 1f, 1f, 1f, 1f, 1.3f, 1.3f };

    /// <summary>Ab dieser Stufe (Index) endet jeder vierte Takt mit einem Notenregen.</summary>
    private const int FinaleLevel = 5;

    /// <summary>Noten im Notenregen des Finales.</summary>
    private const int FinaleNotes = 8;

    private static WeaponStats Level(float cooldown, float damage, float range, float duration,
                                     int shots, string description)
    {
        return new WeaponStats
        {
            cooldown = cooldown,
            damage = damage,
            range = range,
            duration = duration,
            shots = shots,
            description = description,
        };
    }

    // ------------------------------------------------------------------
    //  Die Melodie: 4 Takte, je drei Toene + ein Akkord (Halbtoene ueber C5)
    // ------------------------------------------------------------------

    private static readonly int[][] Tune =
    {
        new[] { 0, 4, 7 },
        new[] { 9, 7, 4 },
        new[] { 5, 4, 2 },
        new[] { 2, 7, 11 },
    };

    private static readonly int[][] Chords =
    {
        new[] { -12, 0, 4, 7 },     // C
        new[] { -15, -3, 0, 4 },    // a
        new[] { -19, -7, -3, 0 },   // F
        new[] { -17, -5, -1, 2 },   // G
    };

    private const float ToneVolume = 0.22f;
    private const float ChordVolume = 0.13f;
    private const float ChimeVolume = 0.2f;
    private const int Voices = 8;

    [Tooltip("Mitte der Mochi relativ zum Spieler-Pivot (der liegt unter den Fuessen).")]
    [SerializeField] private Vector2 originOffset = new Vector2(0f, 0.45f);

    [Tooltip("Hier steigen die Noten auf - ueber dem Kopf.")]
    [SerializeField] private Vector2 singOffset = new Vector2(0f, 0.95f);

    private float beatTimer;
    private int beat;
    private int bar;
    private int crescendoHits;
    private bool wasActive;

    private readonly List<Enemy> targets = new List<Enemy>();
    private AudioSource[] voices;
    private int nextVoice;

    private int LevelIndex(int length) { return Mathf.Clamp(weaponLevel, 0, length - 1); }

    public Vector2 Center => (Vector2)transform.position + originOffset;
    public int CurrentBounces => BouncesPerLevel[LevelIndex(BouncesPerLevel.Length)];

    void Awake()
    {
        stats = new List<WeaponStats>(LevelStats);
        maxweaponLevel = LevelStats.Length - 1;
    }

    void Update()
    {
        PlayerController player = PlayerController.Instance;
        bool active = player != null && IsActive;
        if (!active)
        {
            wasActive = false;
            return;
        }
        if (!wasActive)
        {
            // neu im Spiel: kurz anzaehlen, dann geht es auf der Eins los
            wasActive = true;
            beat = 0;
            beatTimer = 0.6f;
            crescendoHits = 0;
        }

        beatTimer -= Time.deltaTime;
        if (beatTimer > 0f) return;

        beatTimer += Mathf.Max(MinBeat, CurrentCooldown);
        if (beatTimer < 0f) beatTimer = 0f;   // nach einem Haenger nicht alles nachholen

        if (beat < 3) SingNote();
        else PlayChord();

        beat = (beat + 1) % 4;
    }

    /// <summary>Eine Note hat getroffen - der naechste Akkord wird lauter.</summary>
    public void NoteLanded()
    {
        crescendoHits++;
    }

    // ------------------------------------------------------------------
    //  Schlag 1-3: Noten
    // ------------------------------------------------------------------

    private void SingNote()
    {
        PlayerController player = PlayerController.Instance;
        int count = Mathf.Clamp(Mathf.RoundToInt(CurrentStats.shots + player.ExtraShots), 1, 8);
        Vector2 mouth = (Vector2)transform.position + singOffset;
        FindTargets(mouth, CurrentStats.range, count);
        if (targets.Count == 0) return;   // keiner da - Mochi summt still mit

        PlayTone(Tune[bar % Tune.Length][beat], ToneVolume);

        for (int i = 0; i < targets.Count; i++)
        {
            // Faecher nach oben, die Note schwenkt dann auf ihr Ziel ein
            float spread = targets.Count > 1 ? Mathf.Lerp(-40f, 40f, i / (float)(targets.Count - 1)) : 0f;
            Vector2 toward = ((Vector2)targets[i].transform.position - mouth).normalized;
            Vector2 dir = ((Vector2)(Quaternion.Euler(0f, 0f, spread) * Vector2.up) + toward * 0.6f).normalized;
            MelodyNote.Launch(this, mouth, dir, targets[i], CurrentStats.damage, CurrentBounces, beat);
        }
    }

    /// <summary>
    /// Die <paramref name="count"/> naechsten Gegner im Radius. Wer schon von
    /// einer Note angeflogen wird, zaehlt als weiter weg - so verteilt sich
    /// die Melodie, statt einen zu ueberschuetten.
    /// </summary>
    private void FindTargets(Vector2 origin, float radius, int count)
    {
        targets.Clear();
        float maxSqr = radius * radius;
        IReadOnlyList<Enemy> alive = Enemy.Alive;

        for (int n = 0; n < count; n++)
        {
            Enemy best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < alive.Count; i++)
            {
                Enemy enemy = alive[i];
                if (enemy == null || enemy.Untouchable || targets.Contains(enemy)) continue;
                float sqr = ((Vector2)enemy.transform.position - origin).sqrMagnitude;
                if (sqr > maxSqr) continue;
                float score = MelodyNote.IsTargeted(enemy) ? sqr * 4f : sqr;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = enemy;
                }
            }
            if (best == null) break;
            targets.Add(best);
        }

        // weniger Gegner als Noten: die uebrigen Noten teilen sich die vorhandenen
        int found = targets.Count;
        for (int n = found; n < count && found > 0; n++) targets.Add(targets[n % found]);
    }

    // ------------------------------------------------------------------
    //  Schlag 4: Akkord
    // ------------------------------------------------------------------

    private static readonly List<Enemy> hitBuffer = new List<Enemy>();

    private void PlayChord()
    {
        int thisBar = bar;
        bar++;
        float hits = crescendoHits;
        crescendoHits = 0;

        Vector2 at = Center;
        float radius = ChordRadius * ChordPerLevel[LevelIndex(ChordPerLevel.Length)] * AoeFactor();
        float reach = radius + 0.3f;
        bool finale = weaponLevel >= FinaleLevel && thisBar % 4 == 3;

        // erst sammeln, dann treffen - TakeDamage kann Gegner aus Alive nehmen
        hitBuffer.Clear();
        bool anyNear = false;
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy enemy = alive[i];
            if (enemy == null || enemy.Untouchable) continue;
            float sqr = ((Vector2)enemy.transform.position - at).sqrMagnitude;
            if (sqr <= reach * reach) hitBuffer.Add(enemy);
            else if (sqr <= CurrentStats.range * CurrentStats.range) anyNear = true;
        }

        // Ohne Gegner und ohne Treffer im Takt bleibt der Akkord stumm
        if (hitBuffer.Count == 0 && hits == 0 && !anyNear) return;

        int[] chord = Chords[thisBar % Chords.Length];
        for (int i = 0; i < chord.Length; i++) PlayTone(chord[i], ChordVolume);
        if (hits > 0 || finale) PlayClip(ChimeClip, ChimeVolume, 1f);

        FoxFx.Play(ChordFor(radius), at, 20f, 3);

        float damage = CurrentStats.damage * ChordFactor * (1f + Mathf.Min(CrescendoMax, hits * CrescendoStep));
        float slowTime = CurrentDuration;
        for (int i = 0; i < hitBuffer.Count; i++)
        {
            Enemy enemy = hitBuffer[i];
            if (enemy == null) continue;
            Vector2 away = (Vector2)enemy.transform.position - at;
            away = away.sqrMagnitude > 1e-4f ? away.normalized : Random.insideUnitCircle.normalized;
            enemy.TakeDamage(damage, null, 0f);
            if (enemy == null || !enemy.isActiveAndEnabled) continue;
            enemy.ApplyPull(away * ChordShove, 0.12f);
            enemy.ApplySlow(ChordSlow, slowTime);
        }
        hitBuffer.Clear();

        if (finale) NoteRain(at);
    }

    /// <summary>Finale: ein Kranz Noten fliegt in alle Richtungen und sucht sich dann Gegner.</summary>
    private void NoteRain(Vector2 at)
    {
        for (int i = 0; i < FinaleNotes; i++)
        {
            float a = (i + 0.5f) * Mathf.PI * 2f / FinaleNotes;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Enemy target = NearestTo(at + dir * 2.5f, CurrentStats.range);
            MelodyNote.Launch(this, at + dir * 0.3f, dir, target, CurrentStats.damage, CurrentBounces, 3);
        }
    }

    private static Enemy NearestTo(Vector2 point, float radius)
    {
        Enemy best = null;
        float bestSqr = radius * radius;
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy enemy = alive[i];
            if (enemy == null || enemy.Untouchable) continue;
            float sqr = ((Vector2)enemy.transform.position - point).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = enemy;
            }
        }
        return best;
    }

    private static float AoeFactor()
    {
        PlayerController player = PlayerController.Instance;
        return player != null ? Mathf.Max(0.5f, player.AOERange) : 1f;
    }

    // ------------------------------------------------------------------
    //  Ton
    // ------------------------------------------------------------------

    private void PlayTone(int semitones, float volume)
    {
        PlayClip(ToneClip, volume, Mathf.Pow(2f, semitones / 12f));
    }

    /// <summary>
    /// Eigene Stimmen statt der festen Quellen im AudioController: der spielt
    /// pro Quelle nur einen Ton gleichzeitig, ein Akkord braucht mehrere.
    /// Die Mixer-Gruppe kommt vom Wurf-Geraeusch, damit der Effekte-Regler greift.
    /// </summary>
    private void PlayClip(AudioClip clip, float volume, float pitch)
    {
        if (clip == null) return;
        if (voices == null)
        {
            voices = new AudioSource[Voices];
            AudioController audio = AudioController.Instance;
            for (int i = 0; i < Voices; i++)
            {
                AudioSource src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
                src.spatialBlend = 0f;
                if (audio != null && audio.Werfen != null) src.outputAudioMixerGroup = audio.Werfen.outputAudioMixerGroup;
                voices[i] = src;
            }
        }

        AudioSource voice = voices[nextVoice];
        nextVoice = (nextVoice + 1) % Voices;
        float master = AudioController.Instance != null ? AudioController.Instance.masterVolume : 1f;
        voice.Stop();
        voice.clip = clip;
        voice.pitch = pitch;
        voice.volume = volume * master;
        voice.Play();
    }

    private static AudioClip toneClip;
    private static AudioClip chimeClip;

    private static AudioClip ToneClip
    {
        get
        {
            if (toneClip == null) toneClip = Resources.Load<AudioClip>("Sounds/melody_tone");
            return toneClip;
        }
    }

    private static AudioClip ChimeClip
    {
        get
        {
            if (chimeClip == null) chimeClip = Resources.Load<AudioClip>("Sounds/melody_chime");
            return chimeClip;
        }
    }

    // ------------------------------------------------------------------
    //  Bilder
    // ------------------------------------------------------------------

    /// <summary>
    /// Radien der gemalten Akkord-Ringe in Pixeln (Tools/mochi_melodie.py,
    /// CHORD_RADII). Die Lauf-Kamera ist pixelgenau - skaliert wird nie,
    /// sondern der naechstpassende Ring genommen.
    /// </summary>
    private static readonly int[] ChordRadii = { 40, 52, 64, 80 };

    private static readonly Sprite[][] chordFrames = new Sprite[ChordRadii.Length][];
    private static readonly Sprite[][] noteFrames = new Sprite[4][];
    private static Sprite[] sparkFrames;
    private static Sprite[] hitFrames;

    private static readonly Color32[] FallbackColors =
    {
        new Color32(255, 143, 184, 255),
        new Color32(127, 224, 198, 255),
        new Color32(199, 164, 255, 255),
        new Color32(255, 226, 122, 255),
    };

    /// <summary>Note in Farbe <paramref name="color"/> (0 rosa, 1 minze, 2 lavendel, 3 zitrone).</summary>
    public static Sprite[] NoteFrames(int color)
    {
        color = Mathf.Clamp(color, 0, noteFrames.Length - 1);
        Sprite[] frames = noteFrames[color];
        if (frames == null || frames.Length == 0 || frames[0] == null)
        {
            frames = SpriteStrip.Load("Weapons/melody_note_" + color);
            if (frames.Length == 0) frames = new[] { SpriteStrip.Blob(8, FallbackColors[color]) };
            noteFrames[color] = frames;
        }
        return frames;
    }

    /// <summary>Glitzer der Notenspur (Resources/Weapons/melody_spark), Rueckfall leer.</summary>
    public static Sprite[] SparkFrames
    {
        get
        {
            if (sparkFrames == null || (sparkFrames.Length > 0 && sparkFrames[0] == null))
                sparkFrames = SpriteStrip.Load("Weapons/melody_spark");
            return sparkFrames;
        }
    }

    /// <summary>Notentreffer (Resources/Weapons/melody_hit), Rueckfall leer.</summary>
    public static Sprite[] HitFrames
    {
        get
        {
            if (hitFrames == null || (hitFrames.Length > 0 && hitFrames[0] == null))
                hitFrames = SpriteStrip.Load("Weapons/melody_hit");
            return hitFrames;
        }
    }

    /// <summary>Der gemalte Akkord-Ring, dessen Radius am besten zu <paramref name="radius"/> (Tiles) passt.</summary>
    public static Sprite[] ChordFor(float radius)
    {
        float px = radius * 32f;
        int best = 0;
        for (int i = 1; i < ChordRadii.Length; i++)
        {
            if (Mathf.Abs(ChordRadii[i] - px) < Mathf.Abs(ChordRadii[best] - px)) best = i;
        }

        Sprite[] frames = chordFrames[best];
        if (frames == null || (frames.Length > 0 && frames[0] == null))
        {
            frames = SpriteStrip.Load("Weapons/melody_chord_" + ChordRadii[best]);
            chordFrames[best] = frames;
        }
        return frames;
    }
}
