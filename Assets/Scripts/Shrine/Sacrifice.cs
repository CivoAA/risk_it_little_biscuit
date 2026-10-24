using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Regeln des Opferschreins (<see cref="SacrificeShrine"/>): eine Waffe oder
/// einen Buff loswerden, um den Slot wieder frei zu bekommen.
///
///  - Das Teil ist danach fuer den Rest des Laufs verbannt: weaponLevel auf
///    <see cref="Weapon.RemovedLevel"/> wie bei einer Evo, die ihre Zutaten
///    frisst - es belegt keinen Slot mehr und kommt im Level-Up nie wieder.
///  - Sein Effekt geht weg (<see cref="Weapon.RemoveEffect"/>).
///  - Der Spieler verliert so viele Lauf-Level, wie das Teil Stufen hatte
///    (Stufe 6 = 6 Level), die Erfahrung ist weg.
///  - Fuer <see cref="BaseSeconds"/> minus <see cref="SecondsPerLevel"/> je
///    Stufe gibt es <see cref="RunDifficulty.SacrificePressure"/> mehr Chaos.
///
/// Startwaffe und Evos lassen sich nicht opfern. Alle Zahlen stehen hier.
/// </summary>
public static class Sacrifice
{
    /// <summary>So lange wirkt der Druck bei einem Teil ohne Stufe (5 Minuten).</summary>
    public const float BaseSeconds = 300f;

    /// <summary>So viel kuerzer je Stufe des geopferten Teils (Stufe 6 = 2 Minuten).</summary>
    public const float SecondsPerLevel = 30f;

    /// <summary>Kuerzer wird es nie.</summary>
    public const float MinSeconds = 30f;

    /// <summary>Warum ein Teil nicht auf den Altar darf.</summary>
    public enum Block { None, NotOwned, StartWeapon, Evo }

    /// <summary>Stufe, wie das Spiel sie zeigt (weaponLevel 0 = Stufe 1).</summary>
    public static int Levels(Weapon w) => w != null ? Mathf.Max(0, w.weaponLevel + 1) : 0;

    /// <summary>Wie lange der Druck nach dem Opfer von <paramref name="w"/> anhaelt.</summary>
    public static float Seconds(Weapon w) => Mathf.Max(MinSeconds, BaseSeconds - SecondsPerLevel * Levels(w));

    public static bool Owned(Weapon w) => w != null && w.weaponLevel >= 0 && !w.hasBeenRemoved;

    public static bool IsStartWeapon(Weapon w)
    {
        PlayerController p = PlayerController.Instance;
        if (p == null || w == null || p.activeWeapon == null || p.activeWeapon.Length == 0) return false;
        int start = Mathf.Clamp(Shop.RunStartWeapon, 0, p.activeWeapon.Length - 1);
        return p.activeWeapon[start] == w;
    }

    public static bool IsEvo(Weapon w)
    {
        PlayerController p = PlayerController.Instance;
        if (p == null || w == null) return false;
        if (p.activeEvos != null && System.Array.IndexOf(p.activeEvos, w) >= 0) return true;
        if (p.EvoCombinations != null)
            foreach (EvoRecipe r in p.EvoCombinations)
                if (r != null && r.EvoWeapon == w) return true;
        return false;
    }

    public static bool IsBuff(Weapon w)
    {
        PlayerController p = PlayerController.Instance;
        return p != null && w != null && p.activeBuffs != null && System.Array.IndexOf(p.activeBuffs, w) >= 0;
    }

    public static Block Check(Weapon w)
    {
        if (!Owned(w)) return Block.NotOwned;
        if (IsEvo(w)) return Block.Evo;
        if (IsStartWeapon(w)) return Block.StartWeapon;
        return Block.None;
    }

    /// <summary>
    /// Alles, was der Spieler gerade traegt: Evos und Waffen in
    /// <paramref name="weapons"/>, Buffs in <paramref name="buffs"/> - auch
    /// das, was nicht geopfert werden darf (der Schrein zeigt es gesperrt).
    /// </summary>
    public static void CollectOwned(List<Weapon> weapons, List<Weapon> buffs)
    {
        weapons.Clear();
        buffs.Clear();
        PlayerController p = PlayerController.Instance;
        if (p == null) return;
        if (p.activeEvos != null)
            foreach (Weapon w in p.activeEvos) if (Owned(w)) weapons.Add(w);
        if (p.activeWeapon != null)
            foreach (Weapon w in p.activeWeapon) if (Owned(w)) weapons.Add(w);
        if (p.activeBuffs != null)
            foreach (Weapon w in p.activeBuffs) if (Owned(w)) buffs.Add(w);
    }

    /// <summary>Gibt es ueberhaupt etwas, das auf den Altar darf?</summary>
    public static bool AnythingToSacrifice()
    {
        var weapons = new List<Weapon>();
        var buffs = new List<Weapon>();
        CollectOwned(weapons, buffs);
        foreach (Weapon w in weapons) if (Check(w) == Block.None) return true;
        foreach (Weapon w in buffs) if (Check(w) == Block.None) return true;
        return false;
    }

    /// <summary>
    /// Evos, die mit dem Opfer unmoeglich werden, obwohl ihre andere Zutat
    /// schon im Inventar liegt - dann fragt der Schrein nach.
    /// </summary>
    public static List<EvoRecipe> LostEvos(Weapon w)
    {
        var lost = new List<EvoRecipe>();
        PlayerController p = PlayerController.Instance;
        if (p == null || w == null || p.EvoCombinations == null) return lost;

        foreach (EvoRecipe r in p.EvoCombinations)
        {
            if (r == null || r.EvoWeapon == null || r.EvoWeapon.hasBeenRemoved || Owned(r.EvoWeapon)) continue;
            Weapon partner = r.RequiredWeapon1 == w ? r.RequiredWeapon2
                           : r.RequiredWeapon2 == w ? r.RequiredWeapon1
                           : null;
            if (partner != null && partner != w && Owned(partner)) lost.Add(r);
        }
        return lost;
    }

    /// <summary>
    /// Das Opfer selbst. Prueft die Sperren noch einmal - der Schrein soll
    /// nichts ausfuehren koennen, was er nicht anbieten durfte.
    /// </summary>
    public static bool Execute(Weapon w)
    {
        PlayerController p = PlayerController.Instance;
        if (p == null || Check(w) != Block.None) return false;

        int levels = Levels(w);
        float seconds = Seconds(w);

        // Erst den Effekt abziehen, solange der Buff noch weiss, was er gegeben hat.
        w.RemoveEffect();
        w.weaponLevel = Weapon.RemovedLevel;
        w.hasBeenRemoved = true;
        w.posssibleEvo = false;

        int lost = p.LoseLevels(levels);
        RunDifficulty.AddSacrifice(seconds);

        if (DamageNumberController.Instance != null)
        {
            Vector3 at = p.transform.position;
            DamageNumberController.Instance.CreateText(
                string.Format(Loc.Get("ui.shrine.float_levels", "-{0} LEVEL"), lost), at);
            DamageNumberController.Instance.CreateText(
                string.Format(Loc.Get("ui.shrine.float_pressure", "PRESSURE +{0}%"),
                              Mathf.RoundToInt(RunDifficulty.SacrificePressure * 100f)),
                at + Vector3.up * 0.6f);
        }

        // Zum Nachpruefen in der Konsole: so wirkt der Druck auf alles, was ab jetzt spawnt.
        Debug.Log($"[Sacrifice] {w.weaponID} (Stufe {levels}) geopfert: -{lost} Level, " +
                  $"Druck +{RunDifficulty.SacrificePressure:0.00} fuer {seconds:0} s. Chaos jetzt {RunDifficulty.Effective:0.00} " +
                  $"(Gegner x{RunDifficulty.CountFactor:0.00}, Leben x{RunDifficulty.HealthFactor:0.00}, " +
                  $"Schaden x{RunDifficulty.DamageFactor:0.00}, Belohnung x{RunDifficulty.RewardFactor:0.00}).");
        return true;
    }

    /// <summary>"2:00" fuer 120 Sekunden.</summary>
    public static string FormatTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return (s / 60) + ":" + (s % 60).ToString("00");
    }
}
