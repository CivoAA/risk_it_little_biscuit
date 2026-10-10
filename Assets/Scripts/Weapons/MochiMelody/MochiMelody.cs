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
/// Mit der Level-Musik: Mochi spielt im Takt und in der Tonart des Stuecks mit
/// (<see cref="MusicClock"/>, <see cref="SongSheet"/>). Die Toene gehen per
/// PlayScheduled sample-genau auf die Schlaege, die Melodie denkt sich
/// <see cref="MelodyWriter"/> zu den Akkorden des Stuecks aus. Als Notenwert
/// nimmt sie Halbe, Viertel oder Achtel - was dem Cooldown am naechsten kommt;
/// der Schaden wird so umgerechnet, dass Schaden pro Sekunde gleich bleibt.
/// Ohne Notenblatt fuer die laufende Musik spielt sie frei im eigenen Tempo.
///
/// cooldown = Laenge eines Schlags in Sekunden (Cooldown-Buff = schnelleres Tempo,
///            mit Musik: Wechsel auf Achtel)
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
    //  Takt: Mochi spielt mit der Level-Musik
    // ------------------------------------------------------------------

    /// <summary>
    /// So weit im Voraus wird ein Schlag geplant: der Ton geht per
    /// PlayScheduled sample-genau raus, die Note fliegt erst, wenn er erklingt.
    /// </summary>
    private const double Lookahead = 0.1;

    /// <summary>Notenwerte, zwischen denen Mochi waehlt: Halbe, Viertel, Achtel.</summary>
    private static readonly double[] StepChoices = { 2.0, 1.0, 0.5 };

    /// <summary>Erst wechseln, wenn der neue Notenwert so viel besser zum Cooldown passt.</summary>
    private const float SwitchMargin = 1.15f;

    private const float ToneVolume = 0.2f;
    private const float ChordVolume = 0.11f;
    private const float ChimeVolume = 0.16f;
    private const int Voices = 10;

    [Tooltip("Mitte der Mochi relativ zum Spieler-Pivot (der liegt unter den Fuessen).")]
    [SerializeField] private Vector2 originOffset = new Vector2(0f, 0.45f);

    [Tooltip("Hier steigen die Noten auf - ueber dem Kopf.")]
    [SerializeField] private Vector2 singOffset = new Vector2(0f, 0.95f);

    private readonly MelodyWriter writer = new MelodyWriter();

    // im Takt der Musik
    private bool synced;
    private int syncedLock;
    private double stepBeats = 1.0;
    private MusicClock.Step nextStep;

    // frei (Musik ohne Notenblatt): eigener Takt aus dem Cooldown
    private float beatTimer;
    private int freeBeat;

    private int motif;          // zaehlt Akkorde - waehlt die Phrase
    private int chordCount;     // fuer den Notenregen (jeder vierte)
    private int crescendoHits;
    private bool wasActive;

    /// <summary>Ein geplanter Schlag: Ton ist schon raus, Note/Akkord kommt, wenn er erklingt.</summary>
    private struct Pending
    {
        public double dsp;
        public bool chord;
        public int color;
        public float damageScale;
        public bool finale;
    }

    private readonly List<Pending> pending = new List<Pending>();
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
            pending.Clear();
            return;
        }
        if (!wasActive)
        {
            // neu im Spiel: kurz anzaehlen, dann geht es los
            wasActive = true;
            synced = false;
            beatTimer = 0.6f;
            freeBeat = 0;
            crescendoHits = 0;
            writer.Reset();
        }

        // Pause: die Musik laeuft weiter, Mochi nicht. Danach neu einsteigen.
        if (Time.timeScale <= 0f)
        {
            synced = false;
            return;
        }

        if (MusicClock.Poll()) PlanWithMusic();
        else PlayFree();

        FireDue();
    }

    /// <summary>Plant alle Schlaege, die in den naechsten <see cref="Lookahead"/> Sekunden erklingen.</summary>
    private void PlanWithMusic()
    {
        double now = MusicClock.Now;
        if (!synced || syncedLock != MusicClock.LockId || nextStep.time < now - 0.05)
        {
            // (neu) einsteigen: Notenwert waehlen, ab dem naechsten Rasterpunkt
            synced = true;
            syncedLock = MusicClock.LockId;
            stepBeats = ChooseStep(MusicClock.Sheet, stepBeats, true);
            nextStep = MusicClock.NextStep(now + 0.02, stepBeats);
        }

        for (int guard = 0; guard < 8 && nextStep.time - now < Lookahead; guard++)
        {
            Plan(MusicClock.Sheet, nextStep, MusicClock.ToDsp(nextStep.time), StepSeconds(MusicClock.Sheet));

            // Notenwert nur auf der Eins wechseln, sonst stolpert der Rhythmus
            double after = nextStep.time + 1e-4;
            MusicClock.Step peek = MusicClock.NextStep(after, stepBeats);
            if (Mathf.Abs((float)(peek.beat % 4.0)) < 1e-3f)
            {
                double chosen = ChooseStep(MusicClock.Sheet, stepBeats, false);
                if (chosen != stepBeats)
                {
                    stepBeats = chosen;
                    peek = MusicClock.NextStep(after, stepBeats);
                }
            }
            nextStep = peek;
        }
    }

    /// <summary>Ohne Notenblatt (Hub, Test-Szene, fremde Musik): Takt aus dem Cooldown, wie frueher.</summary>
    private void PlayFree()
    {
        synced = false;
        beatTimer -= Time.deltaTime;
        if (beatTimer > 0f) return;

        float beatLength = Mathf.Max(MinBeat, CurrentCooldown);
        beatTimer += beatLength;
        if (beatTimer < 0f) beatTimer = 0f;   // nach einem Haenger nicht alles nachholen

        MusicClock.Step step = new MusicClock.Step { time = 0, beat = freeBeat };
        freeBeat = (freeBeat + 1) % (SongSheet.Solo.bars * 4);
        stepBeats = 1.0;
        Plan(SongSheet.Solo, step, AudioSettings.dspTime, beatLength);
    }

    private double StepSeconds(SongSheet sheet) => stepBeats * sheet.BeatLength;

    /// <summary>
    /// Der Notenwert, der dem Cooldown am naechsten kommt - so bleibt der
    /// Schaden pro Sekunde wie gebaut, egal wie schnell das Stueck ist.
    /// </summary>
    private double ChooseStep(SongSheet sheet, double current, bool force)
    {
        float cooldown = Mathf.Max(MinBeat, CurrentCooldown);
        double best = current;
        float bestMiss = float.MaxValue;
        float currentMiss = float.MaxValue;
        foreach (double choice in StepChoices)
        {
            double seconds = choice * sheet.BeatLength;
            if (seconds < MinBeat) continue;
            float miss = Mathf.Abs(Mathf.Log((float)seconds / cooldown));
            if (choice == current) currentMiss = miss;
            if (miss < bestMiss)
            {
                bestMiss = miss;
                best = choice;
            }
        }
        if (!force && currentMiss < float.MaxValue && currentMiss - bestMiss < Mathf.Log(SwitchMargin)) return current;
        return best;
    }

    /// <summary>
    /// Ein Schlag: Akkord-Ring auf dem letzten Platz jeder Vierergruppe,
    /// sonst Noten. Bei Vierteln liegt der Ring auf der Vier, bei Achteln auf
    /// Zwei und Vier - dort, wo im Stueck die Snare sitzt.
    /// </summary>
    private void Plan(SongSheet sheet, MusicClock.Step step, double dsp, double stepSeconds)
    {
        long index = (long)System.Math.Round(step.beat / stepBeats);
        int shift = stepBeats < 1.0 ? 1 : 0;
        int slot = (int)((index + shift) % 4);
        Chord chord = sheet.ChordAt(step.beat);
        float damageScale = Mathf.Clamp((float)stepSeconds / Mathf.Max(MinBeat, CurrentCooldown), 0.5f, 2f);

        if (slot < 3) PlanNote(sheet, chord, slot, dsp, damageScale);
        else PlanChord(chord, dsp, damageScale);
    }

    private void PlanNote(SongSheet sheet, Chord chord, int slot, double dsp, float damageScale)
    {
        // Die Melodie laeuft auch ohne Gegner weiter (sie bleibt dann stumm),
        // damit sie beim naechsten Gegner an der richtigen Stelle weitermacht.
        int pitch = writer.Note(sheet, chord, motif, slot);
        if (!AnyTargetNear(CurrentStats.range)) return;   // keiner da - Mochi summt still mit

        ScheduleTone(pitch, ToneVolume, dsp);
        pending.Add(new Pending { dsp = dsp, chord = false, color = slot, damageScale = damageScale });
    }

    private void PlanChord(Chord chord, double dsp, float damageScale)
    {
        int thisChord = chordCount;
        chordCount++;
        motif++;
        int[] tones = writer.Voicing(chord);

        bool finale = weaponLevel >= FinaleLevel && thisChord % 4 == 3;
        // Ohne Gegner und ohne Treffer im Takt bleibt der Akkord stumm
        if (crescendoHits == 0 && !AnyTargetNear(Mathf.Max(CurrentStats.range, ChordReach()))) return;

        for (int i = 0; i < tones.Length; i++) ScheduleTone(tones[i], ChordVolume, dsp);
        if (crescendoHits > 0 || finale)
        {
            ScheduleClip(ChimeClip, ChimeVolume, Mathf.Pow(2f, MelodyWriter.ChimeShift(chord) / 12f), dsp);
        }
        pending.Add(new Pending { dsp = dsp, chord = true, damageScale = damageScale, finale = finale });
    }

    /// <summary>Was geplant war und jetzt erklingt, fliegt los.</summary>
    private void FireDue()
    {
        double dspNow = AudioSettings.dspTime;
        while (pending.Count > 0 && pending[0].dsp <= dspNow)
        {
            Pending due = pending[0];
            pending.RemoveAt(0);
            if (due.chord) StrikeChord(due.damageScale, due.finale);
            else SingNote(due.color, due.damageScale);
        }
    }

    private bool AnyTargetNear(float radius)
    {
        Vector2 at = Center;
        float maxSqr = radius * radius;
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy enemy = alive[i];
            if (enemy == null || enemy.Untouchable) continue;
            if (((Vector2)enemy.transform.position - at).sqrMagnitude <= maxSqr) return true;
        }
        return false;
    }

    /// <summary>Eine Note hat getroffen - der naechste Akkord wird lauter.</summary>
    public void NoteLanded()
    {
        crescendoHits++;
    }

    // ------------------------------------------------------------------
    //  Noten
    // ------------------------------------------------------------------

    private void SingNote(int color, float damageScale)
    {
        PlayerController player = PlayerController.Instance;
        int count = Mathf.Clamp(Mathf.RoundToInt(CurrentStats.shots + player.ExtraShots), 1, 8);
        Vector2 mouth = (Vector2)transform.position + singOffset;
        FindTargets(mouth, CurrentStats.range, count);
        if (targets.Count == 0) return;

        float damage = CurrentStats.damage * damageScale;
        for (int i = 0; i < targets.Count; i++)
        {
            // Faecher nach oben, die Note schwenkt dann auf ihr Ziel ein
            float spread = targets.Count > 1 ? Mathf.Lerp(-40f, 40f, i / (float)(targets.Count - 1)) : 0f;
            Vector2 toward = ((Vector2)targets[i].transform.position - mouth).normalized;
            Vector2 dir = ((Vector2)(Quaternion.Euler(0f, 0f, spread) * Vector2.up) + toward * 0.6f).normalized;
            MelodyNote.Launch(this, mouth, dir, targets[i], damage, CurrentBounces, color);
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
    //  Akkord-Ring
    // ------------------------------------------------------------------

    private static readonly List<Enemy> hitBuffer = new List<Enemy>();

    private float ChordRadiusNow()
    {
        return ChordRadius * ChordPerLevel[LevelIndex(ChordPerLevel.Length)] * AoeFactor();
    }

    private float ChordReach() => ChordRadiusNow() + 0.3f;

    private void StrikeChord(float damageScale, bool finale)
    {
        float hits = crescendoHits;
        crescendoHits = 0;

        Vector2 at = Center;
        float radius = ChordRadiusNow();
        float reach = radius + 0.3f;

        // erst sammeln, dann treffen - TakeDamage kann Gegner aus Alive nehmen
        hitBuffer.Clear();
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy enemy = alive[i];
            if (enemy == null || enemy.Untouchable) continue;
            float sqr = ((Vector2)enemy.transform.position - at).sqrMagnitude;
            if (sqr <= reach * reach) hitBuffer.Add(enemy);
        }

        FoxFx.Play(ChordFor(radius), at, 20f, 3);

        float damage = CurrentStats.damage * damageScale * ChordFactor
                       * (1f + Mathf.Min(CrescendoMax, hits * CrescendoStep));
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

        if (finale) NoteRain(at, damageScale);
    }

    /// <summary>Finale: ein Kranz Noten fliegt in alle Richtungen und sucht sich dann Gegner.</summary>
    private void NoteRain(Vector2 at, float damageScale)
    {
        for (int i = 0; i < FinaleNotes; i++)
        {
            float a = (i + 0.5f) * Mathf.PI * 2f / FinaleNotes;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Enemy target = NearestTo(at + dir * 2.5f, CurrentStats.range);
            MelodyNote.Launch(this, at + dir * 0.3f, dir, target, CurrentStats.damage * damageScale, CurrentBounces, 3);
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

    private void ScheduleTone(int semitones, float volume, double dsp)
    {
        ScheduleClip(ToneClip, volume, Mathf.Pow(2f, semitones / 12f), dsp);
    }

    /// <summary>
    /// Eigene Stimmen statt der festen Quellen im AudioController: der spielt
    /// pro Quelle nur einen Ton gleichzeitig, ein Akkord braucht mehrere.
    /// Die Mixer-Gruppe kommt vom Wurf-Geraeusch, damit der Effekte-Regler greift.
    /// Gespielt wird per PlayScheduled zur dspTime <paramref name="dsp"/> -
    /// liegt die schon zurueck, klingt der Ton sofort.
    /// </summary>
    private void ScheduleClip(AudioClip clip, float volume, float pitch, double dsp)
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
        voice.PlayScheduled(System.Math.Max(dsp, AudioSettings.dspTime));
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
