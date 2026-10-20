/// <summary>
/// Herausforderungs-Erfolge, die ueber einen Lauf (oder mehrere) beobachtet
/// werden:
///
///   Fuenf-Gaenge-Menue (Ach.FiveCourse): alle Story-Welten mit demselben
///     Charakter schaffen, ohne dazwischen einen Story-Lauf zu verlieren.
///     Tod oder Aufgeben setzt die Serie zurueck, ein anderer Charakter
///     beginnt sie neu. Endless zaehlt nicht. Stand liegt in ProfilePrefs
///     (Steam Cloud), Fortschritt = geschaffte Welten der Serie.
///
///   Ungemixt (Ach.HardNoMixer): Story-Sieg auf Schwer ohne einen Mixer.
///
/// Nur auf echten Karten, nicht in der Test-Szene.
/// </summary>
public static class RunChallenges
{
    private const string StreakSkinKey = "ach_five_course_skin";   // Charakter + 1, 0 = keine Serie
    private const string StreakMapsKey = "ach_five_course_maps";   // Bitmaske der Map-Ids

    private static bool mixerUsed;

    private static bool OnMap => MapDefinition.Active != null;

    /// <summary>Vom GameManager beim Start eines Laufs.</summary>
    public static void RunStarted() => mixerUsed = false;

    /// <summary>Ein Mixer wurde benutzt (<see cref="MixerObject"/>).</summary>
    public static void MixerUsed() => mixerUsed = true;

    /// <summary>Vom GameManager am Ende eines Laufs (Sieg, Tod oder Aufgeben).</summary>
    public static void RunEnded(bool won)
    {
        // Endless endet immer mit dem Tod - das ist keine verlorene Serie.
        if (!OnMap || GameSession.IsEndless) return;

        if (won && RunDifficulty.Level == Difficulty.Level.Hard && !mixerUsed)
            Achievements.Unlock(Ach.HardNoMixer);

        UpdateStreak(won);
    }

    private static void UpdateStreak(bool won)
    {
        int skin = Shop.RunSkinIndex + 1;
        int maps = ProfilePrefs.GetInt(StreakMapsKey);

        if (!won)
        {
            maps = 0;
        }
        else
        {
            if (ProfilePrefs.GetInt(StreakSkinKey) != skin) maps = 0;
            int mapId = LevelRecords.CurrentMapId();
            if (mapId >= 0 && mapId < 31) maps |= 1 << mapId;
        }

        ProfilePrefs.SetInt(StreakSkinKey, won ? skin : 0);
        ProfilePrefs.SetInt(StreakMapsKey, maps);
        Achievements.SetCountedValue(Ach.FiveCourse, CountBits(maps));
    }

    private static int CountBits(int mask)
    {
        int n = 0;
        for (; mask != 0; mask &= mask - 1) n++;
        return n;
    }
}
