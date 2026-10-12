/// <summary>
/// Platzhalter-Evo von <see cref="FoxFire"/> (siehe <see cref="PlaceholderEvo"/>).
/// Noch ohne Rezept - die richtige Evo kommt spaeter.
/// </summary>
public class FoxFireEvo : FoxFire
{
    protected override int TableLevel => int.MaxValue;

    protected override void Awake()
    {
        base.Awake();
        stats = PlaceholderEvo.From(stats, "Platzhalter-Evo des Fuchsfeuers");
        maxweaponLevel = 0;
    }
}
