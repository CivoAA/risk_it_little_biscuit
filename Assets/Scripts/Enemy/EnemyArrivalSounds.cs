using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Kurzer Hinweis-Klang, wenn ein besonderer Gegner auftaucht - im Code
/// erzeugt (wie <see cref="StorySounds"/>):
///
///   Elite      zwei helle Glockentoene nach oben, ~0,35 s
///   MiniBoss   zwei tiefe, gedaempfte Schlaege "dun-dun", ~0,6 s
///   Boss       tiefer Gong mit dumpfem Stoss, ~1,4 s
///
/// Gegen Nerven: pro Rolle eine Mindestpause - was darin noch einmal kommt,
/// faellt weg. Ausgabe in der Gruppe der Menueklicks, der Effekte-Regler
/// greift also mit. Ausgeloest von <see cref="SpawnDirector"/> beim Spawnen.
/// </summary>
public static class EnemyArrivalSounds
{
    private const int Rate = 44100;

    private static AudioClip elite, miniBoss, boss;
    private static AudioSource source;
    private static float eliteNext, miniBossNext, bossNext;

    /// <summary>Spielt den Klang zur Rolle; Normalgegner und Todes-Boss bleiben still.</summary>
    public static void Play(EnemyRole role)
    {
        float now = Time.unscaledTime;
        switch (role)
        {
            case EnemyRole.Elite:
                if (now < eliteNext) return;
                eliteNext = now + 1.5f;
                PlayClip(elite ??= MakeElite(), 0.32f);
                break;
            case EnemyRole.MiniBoss:
                if (now < miniBossNext) return;
                miniBossNext = now + 3f;
                PlayClip(miniBoss ??= MakeMiniBoss(), 0.5f);
                break;
            case EnemyRole.Boss:
                if (now < bossNext) return;
                bossNext = now + 3f;
                PlayClip(boss ??= MakeBoss(), 0.6f);
                break;
        }
    }

    private static void PlayClip(AudioClip clip, float volume)
    {
        if (source == null) source = MakeSource();
        if (source == null || clip == null) return;
        source.PlayOneShot(clip, volume);
    }

    private static AudioSource MakeSource()
    {
        var go = new GameObject("EnemyArrivalSounds");
        Object.DontDestroyOnLoad(go);
        AudioSource src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        AudioMixerGroup group = null;
        if (AudioController.Instance != null && AudioController.Instance.MenuClick != null)
            group = AudioController.Instance.MenuClick.outputAudioMixerGroup;
        src.outputAudioMixerGroup = group;
        return src;
    }

    // ==================================================================
    //  Synthese
    // ==================================================================

    private static AudioClip Clip(string name, float[] data)
    {
        AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Elite: zwei kurze Glockentoene (A5 -> E6), weich angeschlagen.</summary>
    private static AudioClip MakeElite()
    {
        float seconds = 0.45f;
        int n = Mathf.RoundToInt(seconds * Rate);
        var d = new float[n];
        float[] notes = { 880f, 1318.5f };
        for (int k = 0; k < notes.Length; k++)
        {
            float start = k * 0.09f;
            for (int i = Mathf.RoundToInt(start * Rate); i < n; i++)
            {
                float u = i / (float)Rate - start;
                float s = Mathf.Sin(2f * Mathf.PI * notes[k] * u)
                        + 0.25f * Mathf.Sin(2f * Mathf.PI * notes[k] * 2.01f * u) * Mathf.Exp(-u * 30f);
                d[i] += s * Mathf.Min(1f, u / 0.003f) * Mathf.Exp(-u * 11f) * 0.45f;
            }
        }
        Fade(d);
        return Clip("ArrivalElite", d);
    }

    /// <summary>Zwischenboss: zwei tiefe, gedaempfte Schlaege, der zweite tiefer.</summary>
    private static AudioClip MakeMiniBoss()
    {
        float seconds = 0.7f;
        int n = Mathf.RoundToInt(seconds * Rate);
        var d = new float[n];
        float[] notes = { 196f, 146.8f };     // G3 -> D3
        for (int k = 0; k < notes.Length; k++)
        {
            float start = k * 0.16f;
            float phase = 0f, lp = 0f;
            for (int i = Mathf.RoundToInt(start * Rate); i < n; i++)
            {
                float u = i / (float)Rate - start;
                // kleiner Tonhoehen-Fall am Anschlag macht den Schlag dumpf
                phase += 2f * Mathf.PI * notes[k] * (1f + 0.4f * Mathf.Exp(-u * 60f)) / Rate;
                float sq = Mathf.Sin(phase) > 0f ? 1f : -1f;
                lp += (sq - lp) * 0.06f;
                float body = lp * 0.6f + Mathf.Sin(phase) * 0.5f;
                d[i] += body * Mathf.Min(1f, u / 0.004f) * Mathf.Exp(-u * 9f) * 0.55f;
            }
        }
        Fade(d);
        return Clip("ArrivalMiniBoss", d);
    }

    /// <summary>Boss: tiefer Gong (unharmonische Teiltoene) mit dumpfem Stoss.</summary>
    private static AudioClip MakeBoss()
    {
        float seconds = 1.5f;
        int n = Mathf.RoundToInt(seconds * Rate);
        var d = new float[n];
        const float f0 = 98f;                 // G2
        float[] partials = { 1f, 1.52f, 2.31f, 3.43f, 4.17f };
        float[] amps = { 1f, 0.6f, 0.38f, 0.22f, 0.12f };
        float thumpPhase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float s = 0f;
            for (int p = 0; p < partials.Length; p++)
                s += Mathf.Sin(2f * Mathf.PI * f0 * partials[p] * t) * amps[p] * Mathf.Exp(-t * (2.2f + p * 1.4f));
            thumpPhase += 2f * Mathf.PI * Mathf.Lerp(90f, 40f, Mathf.Min(1f, t / 0.25f)) / Rate;
            float thump = Mathf.Sin(thumpPhase) * Mathf.Exp(-t * 14f);
            d[i] = (s * 0.3f + thump * 0.5f) * Mathf.Min(1f, t / 0.004f);
        }
        Fade(d);
        return Clip("ArrivalBoss", d);
    }

    /// <summary>Die letzten 30 ms ausblenden und begrenzen, damit nichts knackt.</summary>
    private static void Fade(float[] d)
    {
        int tail = Mathf.Min(d.Length, Mathf.RoundToInt(0.03f * Rate));
        for (int i = 0; i < d.Length; i++)
        {
            int left = d.Length - 1 - i;
            float f = left < tail ? left / (float)tail : 1f;
            d[i] = Mathf.Clamp(d[i] * f, -1f, 1f);
        }
    }
}
