/// <summary>
/// Platzhalter-Evo von <see cref="MochiMelody"/> (siehe <see cref="PlaceholderEvo"/>).
/// Noch ohne Rezept - die richtige Evo kommt spaeter.
/// </summary>
public class MochiMelodyEvo : MochiMelody
{
    protected override int TableLevel => int.MaxValue;

    protected override void Awake()
    {
        base.Awake();
        stats = PlaceholderEvo.From(stats, "Platzhalter-Evo der Mochi-Melodie");
        maxweaponLevel = 0;
    }
}
