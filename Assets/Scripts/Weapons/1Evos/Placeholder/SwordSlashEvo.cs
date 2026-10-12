/// <summary>
/// Platzhalter-Evo von <see cref="SwordSlash"/> (siehe <see cref="PlaceholderEvo"/>).
/// Noch ohne Rezept - die richtige Evo kommt spaeter.
/// </summary>
public class SwordSlashEvo : SwordSlash
{
    protected override int TableLevel => int.MaxValue;

    protected override void Awake()
    {
        base.Awake();
        stats = PlaceholderEvo.From(stats, "Platzhalter-Evo des Schwerthiebs");
        maxweaponLevel = 0;
    }
}
