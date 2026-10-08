using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Klaenge fuer Phase 3 des Verkohlten, im Code erzeugt (es gibt noch keine
/// Dateien dafuer): der Herzschlag als tiefes Bum und leiseres bumm, das
/// anschwellende Sog-Rauschen beim Einsaugen, das dumpfe Schlucken und das
/// Bersten, wenn das Herz zerfaellt.
///
/// Gespielt wird ueber die Effekt-Gruppe des AudioControllers, damit der
/// Lautstaerke-Regler fuer Effekte mitgreift.
/// </summary>
public static class VerkohlterSounds
{
    private const int Rate = 44100;

    private static AudioClip lub, dub, suck, gulp, crack;
    private static AudioSource source;

    public static void Lub() { Play(lub ??= Thump("VerkohlterLub", 0.42f, 58f, 34f, 0.9f), 0.85f); }
    public static void Dub() { Play(dub ??= Thump("VerkohlterDub", 0.30f, 72f, 44f, 0.6f), 0.6f); }
    public static void Gulp() { Play(gulp ??= Thump("VerkohlterGulp", 0.55f, 110f, 38f, 1f, wobble: true), 1f); }
    public static void Suck() { Play(suck ??= Noise("VerkohlterSog", 2.9f), 0.55f); }
    public static void Crack() { Play(crack ??= Burst("VerkohlterBersten", 1.6f), 1f); }

    /// <summary>Haelt einen laufenden Sog an (wenn der Spieler drin ist).</summary>
    public static void StopAll()
    {
        if (source != null) source.Stop();
    }

    private static void Play(AudioClip clip, float volume)
    {
        if (clip == null) return;
        AudioSource src = Source();
        if (src == null) return;
        if (clip == suck)
        {
            // Der Sog laeuft als eigener Ton, damit er sich anhalten laesst.
            src.clip = clip;
            src.volume = volume;
            src.Play();
        }
        else
        {
            src.PlayOneShot(clip, volume);
        }
    }

    private static AudioSource Source()
    {
        if (source != null) return source;
        var go = new GameObject("VerkohlterSounds");
        RunScene.Place(go, "Effekte");
        source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        AudioMixerGroup group = null;
        if (AudioController.Instance != null && AudioController.Instance.EarthHit != null)
            group = AudioController.Instance.EarthHit.outputAudioMixerGroup;
        source.outputAudioMixerGroup = group;
        return source;
    }

    /// <summary>Tiefer Schlag: Sinus, der von f0 auf f1 faellt, mit kurzem Anschlag.</summary>
    private static AudioClip Thump(string name, float seconds, float f0, float f1, float gain, bool wobble = false)
    {
        int n = Mathf.RoundToInt(seconds * Rate);
        var data = new float[n];
        float phase = 0f;
        var rng = new System.Random(7);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float q = t / seconds;
            float f = Mathf.Lerp(f0, f1, Mathf.Sqrt(q));
            if (wobble) f *= 1f + 0.25f * Mathf.Sin(t * 38f) * (1f - q);
            phase += 2f * Mathf.PI * f / Rate;
            float env = Mathf.Min(1f, t / 0.006f) * Mathf.Exp(-q * 6.5f);
            float body = Mathf.Sin(phase) + 0.35f * Mathf.Sin(phase * 2f) * Mathf.Exp(-q * 12f);
            float click = (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-t * 180f) * 0.25f;
            data[i] = Mathf.Clamp((body * env + click) * gain * 0.8f, -1f, 1f);
        }
        AudioClip clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Bersten: harter Knall (Rauschen, schnell abklingend), tiefer Nachhall, Knistern.</summary>
    private static AudioClip Burst(string name, float seconds)
    {
        int n = Mathf.RoundToInt(seconds * Rate);
        var data = new float[n];
        var rng = new System.Random(23);
        float lp = 0f, phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float q = t / seconds;
            float white = (float)(rng.NextDouble() * 2 - 1);
            lp += (white - lp) * 0.35f;
            float bang = lp * Mathf.Exp(-t * 9f) * 1.4f;
            phase += 2f * Mathf.PI * Mathf.Lerp(70f, 28f, Mathf.Sqrt(q)) / Rate;
            float boom = Mathf.Sin(phase) * Mathf.Exp(-t * 3.2f) * 0.9f;
            // Knistern: einzelne Klicks, die seltener werden
            float crackle = rng.NextDouble() < 0.004 * (1f - q) ? (float)(rng.NextDouble() * 2 - 1) * 0.8f : 0f;
            data[i] = Mathf.Clamp((bang + boom + crackle) * Mathf.Min(1f, t / 0.003f), -1f, 1f);
        }
        AudioClip clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Rauschen, das anschwillt und immer heller wird (Tiefpass oeffnet sich), am Ende abreisst.</summary>
    private static AudioClip Noise(string name, float seconds)
    {
        int n = Mathf.RoundToInt(seconds * Rate);
        var data = new float[n];
        var rng = new System.Random(11);
        float lp = 0f, lp2 = 0f;
        float rumble = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float q = t / seconds;
            float white = (float)(rng.NextDouble() * 2 - 1);
            float cutoff = Mathf.Lerp(0.015f, 0.22f, q * q);
            lp += (white - lp) * cutoff;
            lp2 += (lp - lp2) * cutoff;
            rumble += 2f * Mathf.PI * Mathf.Lerp(40f, 70f, q) / Rate;
            float env = Mathf.Min(1f, q * 2.2f) * (q > 0.94f ? (1f - q) / 0.06f : 1f);
            float s = lp2 * 3.2f * (0.4f + 0.6f * q) + Mathf.Sin(rumble) * 0.25f * (0.3f + q);
            // ein langsames Pumpen, wie Luft, die gezogen wird
            s *= 0.75f + 0.25f * Mathf.Sin(t * 9f + q * 20f);
            data[i] = Mathf.Clamp(s * env, -1f, 1f);
        }
        AudioClip clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
