using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Der Auftritt von "Second Chance" (<see cref="PlayerController.TryUseSecondChance"/>):
/// ein goldener Lichtstrahl faellt auf die Figur, BLITZ, ein Ring laeuft
/// auseinander und stoesst die Gegner weg, kleine Engelsfluegel schlagen
/// zweimal und zerfallen zu Goldstaub. Danach schwebt ein Heiligenschein ueber
/// dem Kopf, solange die Unverwundbarkeit laeuft.
///
/// Bilder aus Resources/SecondChance (second_chance.py im Artwork-Ordner),
/// Klang Resources/Sounds/second_chance.wav (second_chance_sound.py) - beides
/// ist auf denselben Takt gebaut: der Blitz kommt nach 0.2 s (Bild 4).
///
/// Laeuft auf Echtzeit, damit die kurze Zeitlupe beim Blitz nur die Welt
/// bremst und nicht die Animation. Steht das Spiel (Pause, Level-Up), steht
/// auch der Effekt.
/// </summary>
public class SecondChanceFx : MonoBehaviour
{
    private const float PPU = 32f;
    private const float BurstFps = 20f;
    private const float WingFps = 20f;
    private const float HaloFps = 10f;

    /// <summary>Wann der Blitz kommt (Bild 4 des Bursts) - Ring, Stoss, Wackeln.</summary>
    private const float FlashAt = 4f / BurstFps;
    /// <summary>Die Fluegel kommen kurz nach dem Strahl hinter der Figur hervor.</summary>
    private const float WingsAt = 3f / BurstFps;
    private const float HaloAt = 6f / BurstFps;

    // Zeitlupe beim Blitz: kurz fast still, dann weich zurueck.
    private const float SlowScale = 0.2f;
    private const float SlowHold = 0.22f;
    private const float SlowRamp = 0.35f;

    // Der Ring schafft Platz - kein Schaden, Bosse bleiben stehen (ApplyPull).
    private const float PushRadius = 3.5f;
    private const float PushSpeed = 13f;
    private const float PushTime = 0.3f;

    public const float Volume = 0.85f;

    private static readonly Vector2 WingOffset = new Vector2(0f, 3f / PPU);
    private static readonly Vector2 HaloOffset = new Vector2(0f, 19f / PPU);

    private static Sprite[] burstFrames, wingFrames, haloFrames;
    private static AudioClip clip;
    private static SecondChanceFx current;

    private PlayerController player;
    private SpriteRenderer burst, wings, halo;
    private float age;
    private float haloUntil;
    private bool flashed;

    // Zeitlupe: nur anfassen, solange niemand sonst an timeScale gedreht hat.
    private bool slowActive;
    private float slowStart;
    private float timeScaleBefore = 1f;
    private float timeScaleSet;

    /// <summary>Spielt den Auftritt an der Figur ab; <paramref name="immunity"/> = wie lange der Heiligenschein bleibt.</summary>
    public static void Play(PlayerController player, float immunity)
    {
        if (player == null) return;
        if (current != null) Destroy(current.gameObject);

        GameObject go = new GameObject("SecondChanceFx");
        Scene run = RunScene.Current;
        if (run.IsValid() && run.isLoaded) SceneManager.MoveGameObjectToScene(go, run);

        SecondChanceFx fx = go.AddComponent<SecondChanceFx>();
        fx.player = player;
        fx.haloUntil = Mathf.Max(HaloAt + 1f, immunity);
        current = fx;
        fx.Build();
        fx.Follow();
        fx.PlaySound();
    }

    private void Build()
    {
        burst = Child("Burst", 4);
        wings = Child("Wings", -1);
        halo = Child("Halo", 5);
        Show(burst, Frames(ref burstFrames, "second_chance_burst"), 0);
        wings.enabled = false;
        halo.enabled = false;
    }

    private SpriteRenderer Child(string name, int sortingOffset)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        SaladFan.CopySorting(sr, sortingOffset);
        return sr;
    }

    private void PlaySound()
    {
        if (clip == null) clip = Resources.Load<AudioClip>("Sounds/second_chance");
        if (clip == null) return;

        AudioSource src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.priority = 40;   // vor den Waffen - das hier soll man hoeren
        AudioController audio = AudioController.Instance;
        if (audio != null)
        {
            // Derselbe Kanal wie der Level-Up-Klang, der hier frueher kam.
            if (audio.LevelUpSound != null) src.outputAudioMixerGroup = audio.LevelUpSound.outputAudioMixerGroup;
            src.volume = Volume * audio.masterVolume;
        }
        else
        {
            src.volume = Volume;
        }
        src.clip = clip;
        src.Play();
    }

    void Update()
    {
        if (player == null || !player.gameObject.activeInHierarchy)
        {
            Destroy(gameObject);
            return;
        }

        UpdateSlowMotion();

        // Pause / Level-Up: alles bleibt stehen.
        if (Time.timeScale > 0f) age += Time.unscaledDeltaTime;

        if (!flashed && age >= FlashAt) Flash();

        Follow();
        Animate();

        if (age > haloUntil && age > BurstLength && !slowActive) Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (current == this) current = null;
        // Mitten in der Zeitlupe weg (Szenenwechsel, Tod): Zeit zurueckgeben.
        if (slowActive && Time.timeScale == timeScaleSet) Time.timeScale = timeScaleBefore;
    }

    private float BurstLength => (burstFrames != null ? burstFrames.Length : 18) / BurstFps;

    private void Flash()
    {
        flashed = true;
        Vector2 center = BodyCenter();

        var enemies = Enemy.Alive;
        float r2 = PushRadius * PushRadius;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy e = enemies[i];
            if (e == null) continue;
            Vector2 away = (Vector2)e.transform.position - center;
            if (away.sqrMagnitude > r2) continue;
            if (away.sqrMagnitude < 0.0001f) away = Random.insideUnitCircle;
            e.ApplyPull(away.normalized * PushSpeed, PushTime);
        }

        ScreenShake.Kick(4f, 0.35f);
        StartSlowMotion();
    }

    private void StartSlowMotion()
    {
        if (Time.timeScale <= 0f) return;
        timeScaleBefore = Time.timeScale;
        timeScaleSet = SlowScale * timeScaleBefore;
        Time.timeScale = timeScaleSet;
        slowStart = Time.unscaledTime;
        slowActive = true;
    }

    private void UpdateSlowMotion()
    {
        if (!slowActive) return;

        // Jemand anderes (Pause, Level-Up, Game Over) hat uebernommen - der
        // stellt die Zeit hinterher selbst zurueck.
        if (Time.timeScale != timeScaleSet)
        {
            slowActive = false;
            return;
        }

        float t = (Time.unscaledTime - slowStart - SlowHold) / SlowRamp;
        if (t >= 1f)
        {
            Time.timeScale = timeScaleBefore;
            slowActive = false;
            return;
        }
        float k = Mathf.Clamp01(t);
        k = k * k * (3f - 2f * k);
        timeScaleSet = Mathf.Lerp(SlowScale * timeScaleBefore, timeScaleBefore, k);
        Time.timeScale = timeScaleSet;
    }

    private void Animate()
    {
        Show(burst, Frames(ref burstFrames, "second_chance_burst"), Mathf.FloorToInt(age * BurstFps));

        if (age >= WingsAt)
            Show(wings, Frames(ref wingFrames, "second_chance_wings"), Mathf.FloorToInt((age - WingsAt) * WingFps));

        if (age >= HaloAt && age < haloUntil)
        {
            Sprite[] f = Frames(ref haloFrames, "second_chance_halo");
            if (f.Length > 0)
            {
                // Die letzte halbe Sekunde blinkt er - gleich ist man wieder verwundbar.
                float left = haloUntil - age;
                halo.enabled = left > 0.6f || Mathf.FloorToInt(left * 12f) % 2 == 0;
                halo.sprite = f[Mathf.FloorToInt((age - HaloAt) * HaloFps) % f.Length];
            }
        }
        else
        {
            halo.enabled = false;
        }
    }

    /// <summary>Bild <paramref name="index"/> zeigen, nach dem letzten ausblenden.</summary>
    private static void Show(SpriteRenderer sr, Sprite[] frames, int index)
    {
        if (index < 0 || index >= frames.Length)
        {
            sr.enabled = false;
            return;
        }
        sr.enabled = true;
        sr.sprite = frames[index];
    }

    private void Follow()
    {
        Vector2 c = BodyCenter();
        transform.position = Snap(c);
        wings.transform.position = Snap(c + WingOffset);

        // Der Heiligenschein wippt einen Pixel auf und ab.
        float bob = Mathf.Round(Mathf.Sin(age * 5f)) / PPU;
        halo.transform.position = Snap(c + HaloOffset + new Vector2(0f, bob));
    }

    private Vector2 BodyCenter()
    {
        SpriteRenderer body = player.BodyRenderer();
        return body != null ? (Vector2)body.bounds.center : (Vector2)player.transform.position;
    }

    private static Vector3 Snap(Vector2 p)
    {
        return new Vector3(Mathf.Round(p.x * PPU) / PPU, Mathf.Round(p.y * PPU) / PPU, 0f);
    }

    private static Sprite[] Frames(ref Sprite[] cache, string name)
    {
        if (cache == null || cache.Length == 0) cache = SpriteStrip.Load("SecondChance/" + name);
        return cache;
    }
}
