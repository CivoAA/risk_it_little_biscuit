using System.Collections.Generic;

/// <summary>
/// Evo des Kruemelchaos (Tofu). Braucht keine zweite Zutat: sobald das
/// Kruemelchaos auf Maximalstufe ist, bietet das Level-Up sie an
/// (<c>RequiredWeapon2</c> bleibt im Rezept leer).
///
/// Funktioniert genau wie <see cref="CrumbMayhem"/>, kopiert aber die Evos der
/// Startwaffen (<see cref="Characters.StartEvoId"/>) - fuer Charaktere ohne
/// eigene Evo die Platzhalter aus <see cref="PlaceholderEvo"/>. Evos haben nur
/// eine Stufe; der Schadensbonus bleibt auf dem Stand der Maximalstufe
/// (+10 % je Kruemelchaos-Stufe, also +120 %).
/// </summary>
public class CrumbMayhemEvo : CrumbMayhem
{
    protected override bool CopiesEvos => true;

    public override float DamageFactor => 1f + DamagePerLevel * Levels;

    protected override int CopiedLevel => 0;

    protected override void Awake()
    {
        stats = new List<WeaponStats>
        {
            new WeaponStats
            {
                description = $"Wird alle {SwapInterval:0} s zur Evo eines anderen Charakters (+{DamagePerLevel * Levels * 100f:0} % Schaden)",
            },
        };
        maxweaponLevel = 0;
    }
}
