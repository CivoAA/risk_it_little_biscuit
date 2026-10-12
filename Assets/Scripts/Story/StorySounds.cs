using UnityEngine;
using UnityEngine.Audio;
using Who = StoryTexts.Who;

/// <summary>
/// Klaenge der Story, im Code erzeugt (wie <see cref="VerkohlterSounds"/>):
///
///   Stimme   ein kurzer Klick je getipptem Buchstaben - Pomo "tickt" und
///            "tackt" abwechselnd, jeder Boss hat seine eigene Tonlage
///   Ring     die alte Kuechenuhr klingelt: Hammer auf zwei Glocken
///   Shard    heller Glas-Glut-Klang, wenn ein Splitter eingesammelt wird
///   Whoosh   Luftzug fuer Bildwechsel und fliegende Splitter
///   Boom     dumpfer Schlag (Herz, Titel)
///
/// Laeuft ueber eine eigene, szenenuebergreifende Quelle in der Gruppe der
/// Menueklicks - der Effekte-Regler greift also mit, und die Klaenge
/// ueberleben den Wechsel Lauf -> Hub.
/// </summary>
public static class StorySounds
{
    private const int Rate = 44100;

    private static AudioClip tick, tack, blipLow, blipMid, blipHigh, blipGhost, ring, shard, whoosh, boom;
    private static AudioSource source, voiceSource;
    private static int voiceCounter;

    public static void Ring() => Play(ring ??= MakeRing(), 0.55f);
    public static void Shard() => Play(shard ??= MakeShard(), 0.6f);
    public static void Whoosh() => Play(whoosh ??= MakeWhoosh(), 0.35f);
    public static void Boom() => Play(boom ??= MakeBoom(), 0.7f);

    /// <summary>Ein Stimmen-Klick fuer einen getippten Buchstaben.</summary>
    public static void Voice(Who who, char c)
    {
        if (char.IsWhiteSpace(c) || char.IsPunctuation(c)) return;
        voiceCounter++;
        // Nicht jeder Buchstabe klickt - sonst knattert es.
        if (voiceCounter % 2 != 0) return;

        AudioClip clip;
        float pitch = 1f, vol = 0.22f;
        switch (who)
        {
            case Who.Pomo:
                clip = (voiceCounter / 2) % 2 == 0 ? (tick ??= MakeClick("StoryTick", 2600f, 0.025f))
                                                   : (tack ??= MakeClick("StoryTack", 1900f, 0.03f));
                vol = 0.3f;
                break;
            case Who.KeksKoenig: clip = blipMid ??= MakeBlip("StoryBlipMid", 330f); pitch = 0.9f; break;
            case Who.Glutwurz: clip = blipLow ??= MakeBlip("StoryBlipLow", 150f); break;
            case Who.Eiskaiser: clip = blipHigh ??= MakeBlip("StoryBlipHigh", 520f); pitch = 0.95f; break;
            case Who.Gespenst: clip = blipGhost ??= MakeBlip("StoryBlipGhost", 260f, true); break;
            case Who.Squiddy: clip = blipHigh ??= MakeBlip("StoryBlipHigh", 520f); pitch = 1.25f; break;
            case Who.Verkohlter: clip = blipLow ??= MakeBlip("StoryBlipLow", 150f); pitch = 0.75f; break;
            default: clip = blipMid ??= MakeBlip("StoryBlipMid", 330f); pitch = 1.15f; break;
        }
        pitch *= 1f + Random.Range(-0.05f, 0.05f);
        // Die Stimme hat ihre eigene Quelle: die Tonhoehe gilt fuer alles, was
        // auf einer Quelle gerade klingt - das Klingeln wuerde sonst mit verstimmt.
        if (voiceSource == null) voiceSource = MakeSource("StoryVoice");
        if (voiceSource == null || clip == null) return;
        voiceSource.pitch = pitch;
        voiceSource.PlayOneShot(clip, vol);
    }

    private static void Play(AudioClip clip, float volume)
    {
        if (source == null) source = MakeSource("StorySounds");
        if (source == null || clip == null) return;
        source.PlayOneShot(clip, volume);
    }

    private static AudioSource MakeSource(string name)
    {
        var go = new GameObject(name);
        Object.DontDestroyOnLoad(go);
        AudioSource src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.ignoreListenerPause = true;
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

    /// <summary>Uhrwerk-Klick: kurzer, gefilterter Rausch-Impuls mit Resonanz.</summary>
    private static AudioClip MakeClick(string name, float freq, float seconds)
    {
        int n = Mathf.RoundToInt(seconds * Rate);
        var d = new float[n];
        var rng = new System.Random(name.GetHashCode());
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            phase += 2f * Mathf.PI * freq / Rate;
            float noise = (float)(rng.NextDouble() * 2 - 1);
            float env = Mathf.Exp(-t * 260f);
            d[i] = Mathf.Clamp((Mathf.Sin(phase) * 0.7f + noise * 0.5f) * env, -1f, 1f);
        }
        return Clip(name, d);
    }

    /// <summary>Weicher Sprech-Blip (Rechteck mit Tiefpass), optional mit Geister-Vibrato.</summary>
    private static AudioClip MakeBlip(string name, float freq, bool ghost = false)
    {
        float seconds = 0.06f;
        int n = Mathf.RoundToInt(seconds * Rate);
        var d = new float[n];
        float phase = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float f = freq * (ghost ? 1f + 0.08f * Mathf.Sin(t * 90f) : 1f);
            phase += 2f * Mathf.PI * f / Rate;
            float sq = Mathf.Sin(phase) > 0f ? 1f : -1f;
            lp += (sq - lp) * 0.18f;
            float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * 38f);
            d[i] = lp * env * 0.6f;
        }
        return Clip(name, d);
    }

    /// <summary>Kuechenwecker: Hammer schlaegt schnell auf zwei Glocken, 1,6 s.</summary>
    private static AudioClip MakeRing()
    {
        float seconds = 1.8f;
        int n = Mathf.RoundToInt(seconds * Rate);
        var d = new float[n];
        float[] partials = { 1f, 2.76f, 5.4f, 8.93f };
        float[] amps = { 1f, 0.55f, 0.28f, 0.12f };
        const float hitRate = 22f;            // Schlaege pro Sekunde
        const float ringFor = 1.45f;
        for (int b = 0; b < 2; b++)
        {
            float f0 = b == 0 ? 1320f : 1480f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                // Zeit seit dem letzten Schlag auf diese Glocke
                float period = 1f / hitRate;
                float since = (t + b * period * 0.5f) % period;
                float hit = t < ringFor ? Mathf.Exp(-since * 40f) : 0f;
                float tail = t < ringFor ? 1f : Mathf.Exp(-(t - ringFor) * 7f);
                float s = 0f;
                for (int p = 0; p < partials.Length; p++)
                    s += Mathf.Sin(2f * Mathf.PI * f0 * partials[p] * t) * amps[p] * Mathf.Exp(-t * (0.6f + p * 1.6f));
                d[i] += s * (0.35f + 0.65f * hit) * tail * 0.22f;
            }
        }
        for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(d[i] * Mathf.Min(1f, i / (Rate * 0.004f)), -1f, 1f);
        return Clip("StoryRing", d);
    }

    /// <summary>Splitter: glaserner Akkord, der nach oben glitzert, plus Glut-Knistern.</summary>
    private static AudioClip MakeShard()
    {
        float seconds = 1.4f;
        int n = Mathf.RoundToInt(seconds * Rate);
        var d = new float[n];
        float[] notes = { 659.25f, 830.61f, 987.77f, 1318.5f };   // E-Dur nach oben
        var rng = new System.Random(5);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float s = 0f;
            for (int k = 0; k < notes.Length; k++)
            {
                float start = k * 0.07f;
                if (t < start) continue;
                float u = t - start;
                s += (Mathf.Sin(2f * Mathf.PI * notes[k] * u) + 0.3f * Mathf.Sin(2f * Mathf.PI * notes[k] * 3f * u))
                     * Mathf.Exp(-u * 3.2f) * 0.22f;
            }
            float crackle = rng.NextDouble() < 0.002 ? (float)(rng.NextDouble() * 2 - 1) * 0.3f * (1f - t / seconds) : 0f;
            d[i] = Mathf.Clamp((s + crackle) * Mathf.Min(1f, t / 0.003f), -1f, 1f);
        }
        return Clip("StoryShard", d);
    }

    private static AudioClip MakeWhoosh()
    {
        float seconds = 0.5f;
        int n = Mathf.RoundToInt(seconds * Rate);
        var d = new float[n];
        var rng = new System.Random(3);
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float q = i / (float)n;
            float white = (float)(rng.NextDouble() * 2 - 1);
            float cut = Mathf.Lerp(0.02f, 0.2f, Mathf.Sin(q * Mathf.PI));
            lp += (white - lp) * cut;
            lp2 += (lp - lp2) * cut;
            d[i] = lp2 * 3f * Mathf.Sin(q * Mathf.PI);
        }
        return Clip("StoryWhoosh", d);
    }

    private static AudioClip MakeBoom()
    {
        float seconds = 1.1f;
        int n = Mathf.RoundToInt(seconds * Rate);
        var d = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float q = t / seconds;
            phase += 2f * Mathf.PI * Mathf.Lerp(80f, 30f, Mathf.Sqrt(q)) / Rate;
            d[i] = Mathf.Sin(phase) * Mathf.Min(1f, t / 0.005f) * Mathf.Exp(-q * 5f) * 0.9f;
        }
        return Clip("StoryBoom", d);
    }
}
