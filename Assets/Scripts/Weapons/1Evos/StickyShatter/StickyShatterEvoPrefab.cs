/// <summary>
/// Eine Marmeladenlache der <see cref="StickyShatterEvo"/>. Alles Uebrige -
/// Schaden, Einkochen, Marmeladenbad - kommt aus <see cref="AreaWeaponPrefabJamJar"/>;
/// hier kommt nur die Bremse dazu, die ueber <see cref="Enemy.TakeDamage"/>
/// laeuft (derselbe Weg wie beim Time Laser).
/// </summary>
public class StickyShatterEvoPrefab : AreaWeaponPrefabJamJar
{
    protected override float? Slow => (weapon as StickyShatterEvo)?.SlowMultiplier;

    protected override AreaWeaponJamJar FindWeapon() => WeaponFinder.Find<StickyShatterEvo>("Sticky Shatter Evo");
}
