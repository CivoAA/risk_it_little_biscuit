#if UNITY_INCLUDE_TESTS
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Kaffeepool nach der Evo, Cooldown ohne Ziel (Deathstrike, Feuerball),
/// Evo mitten in der Salve (Spike Fork, Candy Bomb) und Time-Laser-Strahlen.
/// </summary>
public class WeaponFixTests : WeaponTestBase
{
    [UnityTest]
    public IEnumerator Kaffeepool_verschwindet_sofort_wenn_die_Evo_ihn_ersetzt()
    {
        AreaWeapon coffee = FindWeapon<AreaWeapon>("Coffe Pool");
        coffee.weaponLevel = 0;

        yield return WaitUntil(
            () => Object.FindObjectsByType<AreaWeaponPrefab>(FindObjectsSortMode.None).Length > 0,
            5f, "Der Kaffeepool hat keine Lache gelegt");

        coffee.weaponLevel = Weapon.RemovedLevel;
        yield return null;
        yield return null;

        Assert.AreEqual(0,
            Object.FindObjectsByType<AreaWeaponPrefab>(FindObjectsSortMode.None).Length,
            "Nach dem Evo-Kauf darf keine Kaffeelache uebrig bleiben");
    }

    [UnityTest]
    public IEnumerator Deathstrike_startet_den_Cooldown_erst_mit_einem_Gegner()
    {
        Deathstrike strike = FindWeapon<Deathstrike>("Deathstrike");
        strike.weaponLevel = 0;

        // Ohne Gegner darf der Cooldown nicht verbraucht werden.
        yield return WithoutEnemies(1f);

        DamageProbe probe = CreateProbe(player.transform.position + new Vector3(1.5f, 0f, 0f),
                                        new Vector2(0.6f, 0.6f));
        yield return WaitUntil(
            () => Object.FindObjectsByType<DeathstrikePrefab>(FindObjectsSortMode.None).Length > 0, 1f,
            "Deathstrike hat nicht sofort zugeschlagen - der Cooldown lief ohne Gegner ab");

        Object.Destroy(probe.gameObject);
    }

    [UnityTest]
    public IEnumerator Feuerball_startet_den_Cooldown_erst_mit_einem_Ziel()
    {
        FireBall fire = FindWeapon<FireBall>("Fire Ball");
        fire.weaponLevel = 0;

        yield return WithoutEnemies(1f);

        DamageProbe probe = CreateProbe(player.transform.position + new Vector3(1.5f, 0f, 0f),
                                        new Vector2(0.6f, 0.6f));
        yield return WaitUntil(
            () => Object.FindObjectsByType<FireBallPrefab>(FindObjectsSortMode.None).Length > 0, 1f,
            "Der Feuerball hat nicht sofort geschossen - der Cooldown lief ohne Ziel ab");

        Object.Destroy(probe.gameObject);
    }

    /// <summary>Haelt die Szene so lange frei von Gegnern (die Test-Szene spawnt welche).</summary>
    private static IEnumerator WithoutEnemies(float seconds)
    {
        float end = Time.time + seconds;
        while (Time.time < end)
        {
            foreach (Enemy e in Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))
                Object.Destroy(e.gameObject);
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator SpikeFork_Evo_mitten_in_der_Salve_wirft_keine_Exception()
    {
        Spikefork fork = FindWeapon<Spikefork>("Spike Fork");
        fork.weaponLevel = fork.stats.Count - 1;
        player.playerShots = 3; // lange Salve

        yield return WaitUntil(
            () => Object.FindObjectsByType<SpikeforkPrefab>(FindObjectsSortMode.None).Length > 0,
            5f, "Die Spike Fork hat nicht zugestochen");

        fork.weaponLevel = Weapon.RemovedLevel;

        // Rest der Salve abwarten - eine Exception liesse den Test scheitern.
        yield return new WaitForSeconds(3f);
    }

    [UnityTest]
    public IEnumerator CandyBomb_Evo_mitten_in_der_Salve_wirft_keine_Exception()
    {
        CandyBomb bomb = FindWeapon<CandyBomb>("Candy Bomb");
        bomb.weaponLevel = bomb.stats.Count - 1;
        player.playerShots = 3;

        yield return WaitUntil(
            () => Object.FindObjectsByType<CandyBombPrefab>(FindObjectsSortMode.None).Length > 0,
            5f, "Die Candy Bomb hat keine Bombe gelegt");

        bomb.weaponLevel = Weapon.RemovedLevel;

        yield return new WaitForSeconds(4f);
    }

    /// <summary>Strahlen = Schuesse der Stufe halbiert, halbe aufgerundet (2,5 -> 3).</summary>
    [UnityTest]
    public IEnumerator TimeLaser_rundet_halbe_Strahlen_auf()
    {
        TimeLaser laser = FindWeapon<TimeLaser>("Time Laser");
        int[] expected = { 1, 1, 2, 2, 3, 4 };
        Assert.AreEqual(expected.Length, laser.stats.Count, "Time Laser hat eine andere Stufenzahl");

        for (int level = 0; level < expected.Length; level++)
        {
            Assert.AreEqual(expected[level],
                Mathf.FloorToInt(laser.stats[level].shots * 0.5f + 0.5f),
                "Stufe " + level + ": falsche Strahlenzahl in den Stats");
        }

        yield return WithoutEnemies(0.2f);

        // Stufe 4 im Spiel: 3 Strahlen auf den einzigen Gegner
        laser.weaponLevel = 4;
        DamageProbe probe = CreateProbe(player.transform.position + new Vector3(1.5f, 0f, 0f),
                                        new Vector2(0.6f, 0.6f));

        yield return WaitUntil(() => probe.hits > 0, 5f, "Der Time Laser hat nicht geschossen");
        yield return new WaitForSeconds(0.8f); // Rest der Salve, deutlich unter dem Cooldown

        Assert.AreEqual(3, probe.hits, "Stufe 4 muss 3 Strahlen schiessen");

        Object.Destroy(probe.gameObject);
    }
}
#endif
