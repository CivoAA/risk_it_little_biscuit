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

    // --- Endkampf im Herzen (VerkohlterHerzFinale)
    private static AudioClip shot, fireball, erupt, armor, charge, beam, pop;
    private static AudioSource loopSource;
    private static float nextShot;

    /// <summary>Funkenring/Spirale - gedrosselt, sonst rattert es bei jedem Geschoss.</summary>
    public static void Shot()
    {
        if (Time.time < nextShot) return;
        nextShot = Time.time + 0.09f;
        Play(shot ??= Whoosh("HerzFunke", 0.16f, 900f, 260f, 0.5f), 0.35f);
    }
    public static void Fireball() { Play(fireball ??= Whoosh("HerzGlutkugel", 0.38f, 320f, 90f, 1f), 0.7f); }
    public static void Erupt() { Play(erupt ??= Thump("HerzEruption", 0.7f, 90f, 30f, 1f, wobble: true), 0.9f); }
    public static void Armor() { Play(armor ??= Grind("HerzPanzer", 0.9f), 0.9f); }
    public static void Pop() { Play(pop ??= Burst("HerzAderPlatzt", 0.6f), 0.7f); }

    /// <summary>Aufladen des Glutstrahls: ein Ton, der steigt (laeuft als Schleifenquelle, Stop mit <see cref="StopLoop"/>).</summary>
    public static void BeamCharge() { PlayLoop(charge ??= Rise("HerzLaden", 1.3f), 0.55f, false); }
    /// <summary>Der Glutstrahl brennt: tiefes Fauchen in der Schleife, bis <see cref="StopLoop"/>.</summary>
    public static void Beam() { PlayLoop(beam ??= Roar("HerzStrahl", 2f), 0.6f, true); }

    public static void StopLoop()
    {
        if (loopSource != null) loopSource.Stop();
    }

    private static void PlayLoop(AudioClip clip, float volume, bool loop)
    {
        if (clip == null) return;
        if (loopSource == null)
        {
            AudioSource main = Source();
            if (main == null) return;
            loopSource = main.gameObject.AddComponent<AudioSource>();
            loopSource.playOnAwake = false;
            loopSource.spatialBlend = 0f;
            loopSource.outputAudioMixerGroup = main.outputAudioMixerGroup;
        }
        loopSource.clip = clip;
        loopSource.loop = loop;
        loopSource.volume = volume;
        loopSource.Play();
    }

    /// <summary>Kurzes Fauchen: Rauschen durch einen Tiefpass, der zufaellt, dazu ein fallender Ton.</summary>
    private static AudioClip Whoosh(string name, float seconds, float f0, float f1, float noise)
    {
        int n = Mathf.RoundToInt(seconds * Rate);
        var data = new float[n];
        var rng = new System.Random(31);
        float lp = 0f, phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float q = t / seconds;
            float white = (float)(rng.NextDouble() * 2 - 1);
            lp += (white - lp) * Mathf.Lerp(0.45f, 0.06f, q);
            phase += 2f * Mathf.PI * Mathf.Lerp(f0, f1, Mathf.Sqrt(q)) / Rate;
            float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-q * 4.5f);
            data[i] = Mathf.Clamp((lp * 1.6f * noise + Mathf.Sin(phase) * 0.45f) * env, -1f, 1f);
        }
        AudioClip clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Stein auf Stein: tiefes Knirschen mit Klackern (der Panzer schliesst sich).</summary>
    private static AudioClip Grind(string name, float seconds)
    {
        int n = Mathf.RoundToInt(seconds * Rate);
        var data = new float[n];
        var rng = new System.Random(41);
        float lp = 0f, phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float q = t / seconds;
            float white = (float)(rng.NextDouble() * 2 - 1);
            lp += (white - lp) * 0.08f;
            phase += 2f * Mathf.PI * (48f + 6f * Mathf.Sin(t * 31f)) / Rate;
            float grit = lp * 2.4f * (0.6f + 0.4f * Mathf.Sin(t * 70f));
            float clack = rng.NextDouble() < 0.0016 * (1f - q) ? (float)(rng.NextDouble() * 2 - 1) : 0f;
            float env = Mathf.Min(1f, t / 0.02f) * (1f - q) * (1f - q);
            data[i] = Mathf.Clamp((grit + Mathf.Sin(phase) * 0.5f) * env + clack, -1f, 1f);
        }
        AudioClip clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Steigender, flirrender Ton mit Rauschen (Glutstrahl laedt).</summary>
    private static AudioClip Rise(string name, float seconds)
    {
        int n = Mathf.RoundToInt(seconds * Rate);
        var data = new float[n];
        var rng = new System.Random(51);
        float phase = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float q = t / seconds;
            phase += 2f * Mathf.PI * Mathf.Lerp(110f, 520f, q * q) / Rate;
            float white = (float)(rng.NextDouble() * 2 - 1);
            lp += (white - lp) * Mathf.Lerp(0.02f, 0.3f, q);
            float tone = Mathf.Sin(phase) * 0.4f + Mathf.Sin(phase * 1.5f) * 0.2f * q;
            float trem = 0.7f + 0.3f * Mathf.Sin(t * Mathf.Lerp(12f, 40f, q));
            data[i] = Mathf.Clamp((tone + lp * 1.5f) * trem * Mathf.Min(1f, q * 3f), -1f, 1f);
        }
        AudioClip clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Fauchendes Brennen fuer die Schleife (Anfang und Ende passen aneinander).</summary>
    private static AudioClip Roar(string name, float seconds)
    {
        int n = Mathf.RoundToInt(seconds * Rate);
        var data = new float[n];
        var rng = new System.Random(61);
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float white = (float)(rng.NextDouble() * 2 - 1);
            lp += (white - lp) * 0.12f;
            lp2 += (lp - lp2) * 0.2f;
            // ganze Perioden in der Schleife, damit sie nahtlos laeuft
            float hum = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.35f + Mathf.Sin(2f * Mathf.PI * 82.5f * t) * 0.15f;
            float flutter = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 6f * t);
            data[i] = Mathf.Clamp((lp2 * 3.4f + hum) * flutter, -1f, 1f);
        }
        // Naht weich machen
        int fade = Rate / 50;
        for (int i = 0; i < fade; i++)
        {
            float k = i / (float)fade;
            data[i] = data[i] * k + data[n - fade + i] * (1f - k);
        }
        AudioClip clip = AudioClip.Create(name, n - fade, 1, Rate, false);
        var trimmed = new float[n - fade];
        System.Array.Copy(data, trimmed, n - fade);
        clip.SetData(trimmed, 0);
        return clip;
    }

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
