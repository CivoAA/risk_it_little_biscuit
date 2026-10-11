using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Klaenge des Milchpanzers, im Code erzeugt (wie <see cref="EiskaiserSounds"/>):
///
///   StrawPop  der Strohhalm schnellt aus dem Deckel (Quietscher nach oben)
///   Slurp     Schluerfen durch den Halm: gurgelndes Rauschen, so lang wie der Sog
///   Gulp      ein Gegner verschwindet im Halm
///   Burp      der Ruelpser: tiefer, rauer Ton mit Flattern, faellt ab
///
/// Alles ueber die Effekt-Gruppe des AudioControllers (Lautstaerke-Regler).
/// </summary>
public static class MilchpanzerSounds
{
    private const int Rate = 44100;

    private static AudioClip pop, gulp, burp;
    private static AudioClip slurp;
    private static float slurpSeconds;
    private static AudioSource oneShots;

    public static void StrawPop() => Play(pop ??= Squeak("MilchpanzerHalm", 0.22f), 0.5f);
    public static void Gulp() => Play(gulp ??= Plop("MilchpanzerSchluck", 0.14f), 0.35f);
    public static void Burp() => Play(burp ??= Burping("MilchpanzerRuelps", 0.95f), 1f);

    public static void Slurp(float seconds)
    {
        if (slurp == null || !Mathf.Approximately(slurpSeconds, seconds))
        {
            slurp = Slurping("MilchpanzerSchluerfen", seconds);
            slurpSeconds = seconds;
        }
        Play(slurp, 0.55f);
    }

    // ------------------------------------------------------------ Ausgabe

    private static void Play(AudioClip clip, float volume)
    {
        AudioSource src = OneShots();
        if (src != null && clip != null) src.PlayOneShot(clip, volume);
    }

    private static AudioSource OneShots() => oneShots != null ? oneShots : oneShots = NewSource("MilchpanzerSounds");

    private static AudioSource NewSource(string name)
    {
        var go = new GameObject(name);
        RunScene.Place(go, "Effekte");
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        AudioMixerGroup group = null;
        if (AudioController.Instance != null && AudioController.Instance.EarthHit != null)
            group = AudioController.Instance.EarthHit.outputAudioMixerGroup;
        src.outputAudioMixerGroup = group;
        return src;
    }

    private static AudioClip Make(string name, float[] data)
    {
        AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // ------------------------------------------------------------ Synthese

    /// <summary>Kurzer Quietscher, der nach oben rutscht (Halm im Deckel).</summary>
    private static AudioClip Squeak(string name, float seconds)
    {
        int n = Mathf.RoundToInt(seconds * Rate);
        var data = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float q = t / seconds;
            phase += 2f * Mathf.PI * Mathf.Lerp(420f, 1500f, q * q) / Rate;
            float env = Mathf.Min(1f, t / 0.01f) * (1f - q);
            float s = Mathf.Sin(phase) + 0.35f * Mathf.Sin(phase * 2f) + 0.15f * Mathf.Sin(phase * 3f);
            data[i] = Mathf.Clamp(s * env * 0.45f, -1f, 1f);
        }
        return Make(name, data);
    }

    /// <summary>Plopp: kurzer Sinus, der schnell nach unten faellt.</summary>
    private static AudioClip Plop(string name, float seconds)
    {
        int n = Mathf.RoundToInt(seconds * Rate);
        var data = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float q = t / seconds;
            phase += 2f * Mathf.PI * Mathf.Lerp(520f, 150f, Mathf.Sqrt(q)) / Rate;
            float env = Mathf.Min(1f, t / 0.003f) * Mathf.Exp(-q * 5f);
            data[i] = Mathf.Sin(phase) * env * 0.8f;
        }
        return Make(name, data);
    }

    /// <summary>
    /// Schluerfen: Rauschen durch ein schmales, wanderndes Bandpassfilter,
    /// dazu Blubber-Stoesse. Schwillt an und reisst am Ende ab.
    /// </summary>
    private static AudioClip Slurping(string name, float seconds)
    {
        int n = Mathf.RoundToInt(seconds * Rate);
        var data = new float[n];
        var rng = new System.Random(21);
        float low = 0f, band = 0f;
        float bubblePhase = 0f, bubbleEnv = 0f, bubbleFreq = 300f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float q = t / seconds;

            // Bandpass (State-Variable-Filter) mit wanderndem Ton
            float center = 900f + 500f * Mathf.Sin(t * 7.3f) + 250f * Mathf.Sin(t * 17.1f);
            float f = 2f * Mathf.Sin(Mathf.PI * center / Rate);
            float white = (float)(rng.NextDouble() * 2 - 1);
            low += f * band;
            float high = white - low - 0.25f * band;
            band += f * high;

            // Blubber: ab und zu ein kurzer, hochrutschender Ton
            if (rng.NextDouble() < 0.0006)
            {
                bubbleEnv = 1f;
                bubbleFreq = 220f + (float)rng.NextDouble() * 260f;
            }
            bubbleFreq *= 1.00004f;
            bubblePhase += 2f * Mathf.PI * bubbleFreq / Rate;
            bubbleEnv *= 0.9993f;

            float env = Mathf.Min(1f, q / 0.12f) * (q > 0.92f ? (1f - q) / 0.08f : 1f) * (0.7f + 0.3f * q);
            float s = band * 0.55f + Mathf.Sin(bubblePhase) * bubbleEnv * 0.35f;
            data[i] = Mathf.Clamp(s * env, -1f, 1f);
        }
        return Make(name, data);
    }

    /// <summary>
    /// Ruelpser: Saegezahn um 85 Hz mit Flattern (die Stimmlippen rattern),
    /// durch einen Formanten gefaerbt, dazu ein dumpfer Stoss am Anfang.
    /// </summary>
    private static AudioClip Burping(string name, float seconds)
    {
        int n = Mathf.RoundToInt(seconds * Rate);
        var data = new float[n];
        var rng = new System.Random(33);
        float phase = 0f, low = 0f, band = 0f, thump = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float q = t / seconds;

            float freq = Mathf.Lerp(95f, 62f, q) * (1f + 0.12f * Mathf.Sin(t * 2f * Mathf.PI * 23f));
            phase += freq / Rate;
            phase -= Mathf.Floor(phase);
            float saw = phase * 2f - 1f;
            float rasp = (float)(rng.NextDouble() * 2 - 1) * 0.35f;
            float flutter = 0.65f + 0.35f * Mathf.Sin(t * 2f * Mathf.PI * 31f);

            // Formant um 480 Hz: "Ooorp"
            float fc = Mathf.Lerp(560f, 380f, q);
            float f = 2f * Mathf.Sin(Mathf.PI * fc / Rate);
            low += f * band;
            float high = (saw + rasp * saw) - low - 0.35f * band;
            band += f * high;

            thump += 2f * Mathf.PI * Mathf.Lerp(70f, 35f, Mathf.Min(1f, t / 0.25f)) / Rate;
            float boom = Mathf.Sin(thump) * Mathf.Exp(-t * 12f);

            float env = Mathf.Min(1f, t / 0.015f) * (q > 0.7f ? (1f - q) / 0.3f : 1f);
            float s = (band * 0.5f + low * 0.25f) * flutter + boom * 0.6f;
            data[i] = Mathf.Clamp(s * env, -1f, 1f);
        }
        return Make(name, data);
    }
}
