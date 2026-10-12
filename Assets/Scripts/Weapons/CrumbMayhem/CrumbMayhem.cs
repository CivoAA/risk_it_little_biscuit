using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kruemelchaos (Crumb Mayhem) - die Startwaffe des Tofus (nur fuer ihn).
///
/// STAND (11.10.2026): Platzhalter ohne Wirkung. Die Waffe laesst sich
/// erhalten, aufleveln und steht in Werkbank/Level-Up, tut im Lauf aber
/// nichts. Die Stufen unten tragen nur Texte, damit Level-Up und HUD etwas
/// anzeigen koennen - die eigentliche Funktion kommt spaeter.
///
/// Werte im Code (<see cref="LevelStats"/>), nicht am Prefab.
/// </summary>
public class CrumbMayhem : Weapon
{
    private static readonly WeaponStats[] LevelStats =
    {
        Level("Kruemelchaos - tut noch nichts"),
        Level("Stufe 2 - tut noch nichts"),
        Level("Stufe 3 - tut noch nichts"),
        Level("Stufe 4 - tut noch nichts"),
        Level("Stufe 5 - tut noch nichts"),
        Level("Stufe 6 - tut noch nichts"),
    };

    private static WeaponStats Level(string description)
    {
        return new WeaponStats
        {
            cooldown = 1f,
            shots = 1,
            description = description,
        };
    }

    void Awake()
    {
        stats = new List<WeaponStats>(LevelStats);
        maxweaponLevel = LevelStats.Length - 1;
    }
}
