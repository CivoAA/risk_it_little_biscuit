#if UNITY_INCLUDE_TESTS
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Beide Gabel-Waffen spawnen ihre Projektile still am Spieler. Steht der
/// Spieler, schläft dessen Rigidbody2D ein und die als Kind angehängten
/// Collider melden für bereits überlappende Gegner kein OnTriggerEnter2D mehr.
/// </summary>
public class ForkWeaponTests : WeaponTestBase
{
    [UnityTest]
    public IEnumerator SpikeFork_macht_Schaden_wenn_der_Spieler_steht()
    {
        Spikefork fork = FindWeapon<Spikefork>("Spike Fork");
        fork.weaponLevel = 0;

        DamageProbe probe = CreateProbe(player.transform.position + new Vector3(0.4f, 0.45f, 0f),
                                        new Vector2(0.6f, 0.6f));

        // Kein Input, keine Bewegung – genau der gemeldete Fall.
        yield return WaitUntil(() => probe.hits > 0, 8f,
            "Die Spike Fork hat im Stand keinen Schaden gemacht");

        Assert.AreEqual(fork.stats[0].damage, probe.totalDamage / probe.hits, 0.001f,
            "Es wurde nicht der Schadenswert der Waffe angewendet");

        Object.Destroy(probe.gameObject);
    }

    [UnityTest]
    public IEnumerator BloodyForkEvo_macht_Schaden_wenn_der_Spieler_steht()
    {
        BloodyFork evo = FindWeapon<BloodyFork>("Bloody Fork Evo");
        evo.weaponLevel = 0;

        DamageProbe probe = CreateProbe(player.transform.position + new Vector3(0.4f, 0f, 0f),
                                        new Vector2(0.6f, 0.6f));

        yield return WaitUntil(() => probe.hits > 0, 8f,
            "Die Bloody Fork Evo hat im Stand keinen Schaden gemacht");

        Object.Destroy(probe.gameObject);
    }

    /// <summary>Eine einzelne Gabel darf denselben Gegner nicht mehrfach treffen.</summary>
    [UnityTest]
    public IEnumerator Eine_Gabel_trifft_denselben_Gegner_nur_einmal()
    {
        Spikefork fork = FindWeapon<Spikefork>("Spike Fork");
        fork.weaponLevel = 0;

        DamageProbe probe = CreateProbe(player.transform.position + new Vector3(0.4f, 0.45f, 0f),
                                        new Vector2(0.6f, 0.6f));

        yield return WaitUntil(() => probe.hits > 0, 8f, "Kein Treffer");

        int hitsAfterFirstFork = probe.hits;

        // Die Gabel lebt 0.5s. Innerhalb dieser Zeit darf kein weiterer
        // Treffer derselben Gabel dazukommen.
        yield return new WaitForSeconds(0.3f);

        Assert.LessOrEqual(probe.hits, hitsAfterFirstFork + 1,
            "Dieselbe Gabel hat den Gegner mehrfach getroffen");

        Object.Destroy(probe.gameObject);
    }
}
#endif
