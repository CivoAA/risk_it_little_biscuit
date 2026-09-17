using UnityEngine;

/// <summary>
/// Gruppe, unter der ein Achievement in der Anzeige einsortiert wird.
/// Die Reihenfolge hier ist auch die Reihenfolge der Abschnitte in der Liste.
/// </summary>
public enum AchievementCategory
{
    Progression,
    Combat,
    Weapons,
    Buffs,
    Evolutions,
    Secrets,
}

/// <summary>
/// Die unveränderliche Beschreibung eines Achievements - alles, was zum Spiel
/// gehört und nicht zum Spielstand: Text, Ziel, Symbol, Belohnung, Kategorie.
///
/// Angelegt wird so ein Def ausschließlich im Katalog <see cref="Ach"/>. Der
/// Fortschritt (freigeschaltet, Zählerstand) lebt getrennt davon in
/// <see cref="AchievementStore"/> und damit in der Save-Datei des Spielers.
/// Genau diese Trennung sorgt dafür, dass neue Achievements alte Spielstände
/// nicht anfassen können.
/// </summary>
public sealed class AchievementDef
{
    /// <summary>Stabiler Schlüssel. Steht so in der Save-Datei UND bei Steam. Niemals ändern.</summary>
    public readonly string Id;

    /// <summary>Englischer Originaltext. Dient als Fallback, wenn eine Sprachdatei den Key nicht kennt.</summary>
    public readonly string NameEn;
    public readonly string DescEn;

    /// <summary>Dateiname (ohne Endung) in Assets/Resources/Achievements/. Standard: Id in Kleinbuchstaben.</summary>
    public readonly string IconKey;

    /// <summary>Zielwert. 1 = einfacher Unlock, grösser = Fortschrittsbalken.</summary>
    public readonly float Goal;

    /// <summary>Skill-Währung ("Cookie Souls"), die es beim Freischalten gibt.</summary>
    public readonly int Souls;

    /// <summary>Optional: Unlock-ID, die beim Freischalten mit vergeben wird. Leer = keine.</summary>
    public readonly string GrantsUnlock;

    public readonly AchievementCategory Category;

    /// <summary>Versteckt: Name und Beschreibung bleiben bis zum Freischalten verdeckt.</summary>
    public readonly bool Hidden;

    /// <summary>Name im Steamworks-Backend. Standard: gleich der Id.</summary>
    public readonly string SteamApiName;

    /// <summary>Position im Katalog - entspricht der Reihenfolge in Ach.cs.</summary>
    public int Order { get; internal set; }

    public AchievementDef(string id, string nameEn, string descEn, string iconKey, float goal,
                          int souls, string grantsUnlock, AchievementCategory category,
                          bool hidden, string steamApiName)
    {
        Id           = id;
        NameEn       = nameEn;
        DescEn       = descEn;
        IconKey      = string.IsNullOrEmpty(iconKey) ? id.ToLowerInvariant() : iconKey;
        Goal         = Mathf.Max(1f, goal);
        Souls        = Mathf.Max(0, souls);
        GrantsUnlock = grantsUnlock;
        Category     = category;
        Hidden       = hidden;
        SteamApiName = string.IsNullOrEmpty(steamApiName) ? id : steamApiName;
    }

    /// <summary>Hat einen Zähler statt nur an/aus.</summary>
    public bool HasProgress => Goal > 1f;

    public bool IsUnlocked => Achievements.IsUnlocked(this);
    public float Value => Achievements.GetValue(this);

    /// <summary>Übersetzter Name. Versteckte Achievements bleiben bis zum Unlock verdeckt.</summary>
    public string Name
    {
        get
        {
            if (Hidden && !IsUnlocked) return Loc.Get("ach.hidden.name", "???");
            return Loc.Get($"ach.{Id}.name", NameEn);
        }
    }

    /// <summary>Übersetzte Beschreibung. Versteckte Achievements bleiben bis zum Unlock verdeckt.</summary>
    public string Description
    {
        get
        {
            if (Hidden && !IsUnlocked) return Loc.Get("ach.hidden.desc", "A secret waiting to be found.");
            return Loc.Get($"ach.{Id}.desc", DescEn);
        }
    }

    public Sprite Icon => AchievementIcons.Get(Hidden && !IsUnlocked ? AchievementIcons.HiddenKey : IconKey);

    public override string ToString() => Id;
}
