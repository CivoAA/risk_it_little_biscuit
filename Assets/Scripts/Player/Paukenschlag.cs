using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Skilltree "Paukenschlag" (<see cref="SkillGrants.Paukenschlag"/>): faellt das
/// Leben unter 35 %, haut Mochi auf der naechsten Eins der Level-Musik auf die
/// Pauke. Alle Gegner im <see cref="Radius"/> fliegen weit weg und stehen danach
/// <see cref="StunTime"/> Sekunden still. Macht keinen Schaden. Bosse und
/// Minibosse bleiben stehen.
///
/// Der Schlag geht immer von Mochi aus - auch mit der Evo, deren Musikboxen
/// woanders stehen.
///
/// Ohne Notenblatt fuer die laufende Musik (Hub, Test-Szene) schlaegt er sofort.
/// Ausgeloest wird er von <see cref="PlayerController.TakeDamage"/>, der auch
/// die Abklingzeit haelt.
/// </summary>
public class Paukenschlag : MonoBehaviour
{
    /// <summary>Ab diesem Lebensanteil schlaegt die Pauke.</summary>
    public const float Threshold = 0.35f;

    /// <summary>Abklingzeit, gerechnet ab dem Schlag.</summary>
    public const float Cooldown = 35f;

    /// <summary>Reichweite in Tiles - passt zum groessten gemalten Akkord-Ring (160 px).</summary>
    public const float Radius = 5f;

    /// <summary>So schnell fliegen die Gegner weg (Tiles pro Sekunde) ...</summary>
    public const float FlingSpeed = 18f;

    /// <summary>... und so lange.</summary>
    public const float FlingTime = 0.3f;

    /// <summary>So lange stehen sie danach still.</summary>
    public const float StunTime = 1.5f;

    /// <summary>Hoechstens so lange wird auf die Eins gewartet (Sekunden).</summary>
    private const double MaxWait = 3.0;

    private static readonly Vector2 CenterOffset = new Vector2(0f, 0.45f);

    private Transform owner;
    private double strikeDsp;

    /// <summary>Plant den Paukenschlag um <paramref name="player"/> auf die naechste Eins.</summary>
    public static void Fire(Transform player)
    {
        GameObject go = new GameObject("Paukenschlag");
        go.transform.position = player.position;

        Scene run = RunScene.Current;
        if (run.IsValid() && run.isLoaded) SceneManager.MoveGameObjectToScene(go, run);

        Paukenschlag drum = go.AddComponent<Paukenschlag>();
        drum.owner = player;
        drum.Plan();
    }

    private void Plan()
    {
        double now = AudioSettings.dspTime;
        strikeDsp = now + 0.05;
        if (MusicClock.Poll())
        {
            MusicClock.Step one = MusicClock.NextStep(MusicClock.Now + 0.05, 4.0);
            double dsp = MusicClock.ToDsp(one.time);
            if (dsp > now && dsp - now < MaxWait) strikeDsp = dsp;
        }

        // Tiefe Pauke: der Melodie-Ton zwei und eine Oktave runter, dazu das Glockenspiel
        Schedule(MochiMelody.ToneClip, 0.5f, 0.25f);
        Schedule(MochiMelody.ToneClip, 0.3f, 0.5f);
        Schedule(MochiMelody.ChimeClip, 0.25f, 0.5f);
    }

    private void Schedule(AudioClip clip, float volume, float pitch)
    {
        if (clip == null) return;
        AudioSource src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        AudioController audio = AudioController.Instance;
        if (audio != null && audio.Werfen != null) src.outputAudioMixerGroup = audio.Werfen.outputAudioMixerGroup;
        src.clip = clip;
        src.pitch = pitch;
        src.volume = volume * (audio != null ? audio.masterVolume : 1f);
        src.PlayScheduled(strikeDsp);
    }

    private void Update()
    {
        if (owner == null)
        {
            Destroy(gameObject);
            return;
        }
        if (Time.timeScale <= 0f || AudioSettings.dspTime < strikeDsp) return;

        Strike((Vector2)owner.position + CenterOffset);
        // Die Toene klingen noch aus, dann weg
        Destroy(gameObject, 3f);
        enabled = false;
    }

    private static void Strike(Vector2 at)
    {
        FoxFx.Play(MochiMelody.ChordFor(Radius), at, 18f, 3);

        float r2 = Radius * Radius;
        var enemies = Enemy.Alive;
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            Enemy e = enemies[i];
            if (e == null || e.IsBoss || e.Untouchable) continue;

            Vector2 away = (Vector2)e.transform.position - at;
            if (away.sqrMagnitude > r2) continue;
            if (away.sqrMagnitude < 0.0001f) away = Random.insideUnitCircle;

            e.ApplyPull(away.normalized * FlingSpeed, FlingTime);
            e.ApplySlow(0f, FlingTime + StunTime);
        }
    }
}
