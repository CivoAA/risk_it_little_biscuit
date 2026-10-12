/// <summary>
/// Platzhalter-Evo von <see cref="ChocoChips"/> (siehe <see cref="PlaceholderEvo"/>).
/// Noch ohne Rezept - die richtige Evo kommt spaeter.
/// </summary>
public class ChocoChipsEvo : ChocoChips
{
    protected override int TableLevel => int.MaxValue;

    protected override void Awake()
    {
        base.Awake();
        stats = PlaceholderEvo.From(stats, "Platzhalter-Evo der Schoko-Salve");
        maxweaponLevel = 0;
    }
}
