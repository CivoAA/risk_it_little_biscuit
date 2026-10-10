using UnityEngine;

/// <summary>
/// Musik der Phase 3 des Verkohlten - alles ueber die Lauf-Musik-Quelle des
/// <see cref="AudioController"/>, damit die Mochi-Melodie mitspielt:
///   * <see cref="Inhale"/>: er holt Luft - die Level-Musik wird mit eingesaugt,
///   * <see cref="Enter"/>: Landung in der Kammer - "Herz der Glut" setzt mit
///     einem Einschlag ein (erste Eins = Landung),
///   * <see cref="HeartDying"/>: das Herz ueberhitzt - die Musik bricht weg,
///   * <see cref="Finale"/>: das Herz birst - Fanfare in Dur,
///   * <see cref="Leave"/>: Test-Szene, zurueck in die Welt - altes Stueck.
///
/// Bilder/Toene: Tools/herz_musik.py -> Resources/Music/herzkammer(_finale).
/// Das Herz schlaegt im Takt der Musik: <see cref="HeartClock"/>.
/// </summary>
public static class VerkohlterMusik
{
    public const string ClipName = "herzkammer";

    private const float InhaleFade = 2.0f;
    private const float DyingFade = 0.7f;

    private static AudioClip loop, finale;
    private static AudioClip before;

    /// <summary>Clips schon laden, wenn der Verkohlte auftaucht - nicht erst im Sog (Ruckler).</summary>
    public static void Preload()
    {
        if (loop == null) loop = Resources.Load<AudioClip>("Music/" + ClipName);
        if (finale == null) finale = Resources.Load<AudioClip>("Music/" + ClipName + "_finale");
        if (loop != null && loop.loadState == AudioDataLoadState.Unloaded) loop.LoadAudioData();
        if (finale != null && finale.loadState == AudioDataLoadState.Unloaded) finale.LoadAudioData();
    }

    public static void Inhale()
    {
        AudioController audio = AudioController.Instance;
        if (audio == null) return;
        if (audio.CurrentRunClip != null && audio.CurrentRunClip != loop) before = audio.CurrentRunClip;
        audio.FadeRunMusic(InhaleFade);
    }

    public static void Enter()
    {
        Preload();
        AudioController audio = AudioController.Instance;
        if (audio == null || loop == null) return;
        // Sofort voll da - der Einschlag am Anfang ist die Landung.
        audio.SwapRunMusic(loop, 0.3f, 0f);
    }

    public static void HeartDying()
    {
        AudioController audio = AudioController.Instance;
        if (audio != null) audio.FadeRunMusic(DyingFade);
    }

    public static void Finale()
    {
        Preload();
        AudioController audio = AudioController.Instance;
        if (audio != null) audio.PlayRunStinger(finale);
    }

    /// <summary>Raus aus der Kammer, ohne dass der Lauf endet: das Stueck von vorher kommt wieder.</summary>
    public static void Leave()
    {
        AudioController audio = AudioController.Instance;
        if (audio == null) return;
        if (before != null) audio.SwapRunMusic(before, 1.0f, 2.0f);
        else audio.FadeRunMusic(1.0f);
        before = null;
    }

    /// <summary>
    /// Wo der Herzschlag gerade steht (Sekunden, laeuft weiter), wenn "Herz der
    /// Glut" spielt: ein Schlag = 2 Viertel, das BUM (Bild <see cref="VerkohlterArt.BeatLub"/>)
    /// auf 1 und 3. False = die Musik laeuft nicht, das Herz schlaegt nach eigener Uhr.
    /// </summary>
    public static bool HeartClock(out double clock)
    {
        clock = 0.0;
        if (!MusicClock.Poll()) return false;
        SongSheet sheet = MusicClock.Sheet;
        if (sheet == null || sheet.clipName != ClipName) return false;
        clock = System.Math.Max(0.0, MusicClock.Now - sheet.firstDownbeat + (double)VerkohlterArt.BeatLub / VerkohlterArt.Fps);
        return true;
    }
}
