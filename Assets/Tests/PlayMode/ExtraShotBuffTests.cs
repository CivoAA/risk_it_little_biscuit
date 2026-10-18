#if UNITY_INCLUDE_TESTS
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Der Extraschuss-Buff zaehlt bei jeder Waffe 1:1, Skilltree/Mixer
/// (playerShots) weiter nach dem Verhaeltnis der Waffe. Dazu die
/// Stufenwerte von Butterblast und Himmelsstern.
/// </summary>
public class ExtraShotBuffTests : WeaponTestBase
{
    [UnityTest]
    public IEnumerator Buff_landet_im_eigenen_Topf()
    {
        Weapon buff = FindWeapon<ExtraShot>("Extra Shot");
        buff.weaponLevel = 0;
        yield return null;
        yield return null;

        Assert.AreEqual(buff.stats[0].damage, player.buffShots, 0.0001f, "Der Buff muss in buffShots landen");
        Assert.AreEqual(0f, player.playerShots, 0.0001f, "Der Buff darf playerShots nicht anfassen");
    }

    [UnityTest]
    public IEnumerator Buff_zaehlt_1_zu_1_der_Rest_nach_Verhaeltnis()
    {
        player.buffShots = 2f;
        player.playerShots = 0f;
        Assert.AreEqual(2f, Weapon.ExtraShotsFor(1), 0.0001f, "1:1-Waffe");
        Assert.AreEqual(2f, Weapon.ExtraShotsFor(4), 0.0001f, "1:4-Waffe: Buff muss trotzdem voll zaehlen");
        Assert.AreEqual(0f, Weapon.ExtraShotsFor(0), 0.0001f, "Waffe ohne Extra-Schuss bleibt bei 0");

        player.buffShots = 0f;
        player.playerShots = 4f;
        Assert.AreEqual(1f, Weapon.ExtraShotsFor(4), 0.0001f, "Skilltree/Mixer laufen weiter 1:4");

        player.buffShots = 1f;
        Assert.AreEqual(2f, Weapon.ExtraShotsFor(4), 0.0001f, "Beides zusammen");
        yield return null;
    }

    [UnityTest]
    public IEnumerator Kunai_bekommen_vom_Buff_einen_ganzen_mehr()
    {
        player.buffShots = 1.5f;
        player.playerShots = 0f;
        Assert.AreEqual(1, BladeSwarm.ExtraBlades, "1,5 vom Buff = 1 Kunai mehr");

        player.buffShots = 0f;
        player.playerShots = 3f;
        Assert.AreEqual(1, BladeSwarm.ExtraBlades, "3 aus dem Skilltree = 1 Kunai (1:2)");
        yield return null;
    }

    [UnityTest]
    public IEnumerator Butterblast_und_Himmelsstern_werden_jede_Stufe_besser()
    {
        foreach (string name in new[] { "Butterblast", "Celestial Star" })
        {
            Weapon w = FindWeapon<Weapon>(name);
            for (int i = 1; i < w.stats.Count; i++)
            {
                WeaponStats a = w.stats[i - 1], b = w.stats[i];
                Assert.LessOrEqual(b.cooldown, a.cooldown, name + " Stufe " + (i + 1) + ": Cooldown wird schlechter");
                bool better = b.cooldown < a.cooldown || b.damage > a.damage || b.duration > a.duration
                              || b.shots > a.shots || b.AttackSpeed > a.AttackSpeed;
                Assert.IsTrue(better, name + " Stufe " + (i + 1) + " bringt nichts");
            }
        }
        yield return null;
    }
}
#endif
