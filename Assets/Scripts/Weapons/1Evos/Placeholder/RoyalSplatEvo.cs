/// <summary>
/// Platzhalter-Evo von <see cref="RoyalSplat"/> (siehe <see cref="PlaceholderEvo"/>).
/// Noch ohne Rezept - die richtige Evo kommt spaeter.
/// </summary>
public class RoyalSplatEvo : RoyalSplat
{
    protected override int TableLevel => int.MaxValue;

    protected override void Awake()
    {
        base.Awake();
        stats = PlaceholderEvo.From(stats, "Platzhalter-Evo des Königsplumps");
        maxweaponLevel = 0;
    }
}
