#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Tests zum Umgang mit ersetzten Waffen (weaponLevel = Weapon.RemovedLevel)
/// und zur aktiven Trefferabfrage.
/// </summary>
public class WeaponCoreTests : WeaponTestBase
{
    [UnityTest]
    public IEnumerator IsActive_gilt_nur_bei_gueltigem_Level()
    {
        BladeSwarm swarm = FindWeapon<BladeSwarm>("Blade Swarm");

        swarm.weaponLevel = -1;
        Assert.IsFalse(swarm.IsActive, "Nicht erhaltene Waffe darf nicht aktiv sein");
        Assert.IsNull(swarm.CurrentStats, "Nicht erhaltene Waffe darf keine Stats liefern");

        swarm.weaponLevel = Weapon.RemovedLevel;
        Assert.IsFalse(swarm.IsActive, "Durch eine Evo ersetzte Waffe darf nicht aktiv sein");
        Assert.IsNull(swarm.CurrentStats, "Ersetzte Waffe darf keine Stats liefern");

        swarm.weaponLevel = swarm.stats.Count; // ein Level zu hoch
        Assert.IsFalse(swarm.IsActive, "Level ausserhalb der Stats-Liste darf nicht aktiv sein");

        swarm.weaponLevel = 0;
        Assert.IsTrue(swarm.IsActive, "Erhaltene Waffe muss aktiv sein");
        Assert.AreSame(swarm.stats[0], swarm.CurrentStats);

        yield return null;
    }

    /// <summary>
    /// Der Kern-Bug: die Evo setzt die Basiswaffe auf RemovedLevel (-99), die
    /// alte Prüfung lauschte aber auf -10. Die Klingen blieben dadurch als
    /// wirkungslose Geister am Spieler hängen.
    /// </summary>
    [UnityTest]
    public IEnumerator BladeSwarm_raeumt_seine_Klingen_weg_wenn_die_Evo_sie_ersetzt()
    {
        BladeSwarm swarm = FindWeapon<BladeSwarm>("Blade Swarm");
        swarm.weaponLevel = 0;

        yield return WaitUntil(
            () => Object.FindObjectsByType<BladeSwarmPrefab>(FindObjectsSortMode.None).Length > 0,
            5f, "Blade Swarm hat keine Klingen gespawnt");

        // Genau das macht LevelUpButton beim Kauf der Evo:
        swarm.weaponLevel = Weapon.RemovedLevel;

        yield return null;
        yield return null;

        Assert.AreEqual(0,
            Object.FindObjectsByType<BladeSwarmPrefab>(FindObjectsSortMode.None).Length,
            "Nach dem Evo-Kauf dürfen keine Blade-Swarm-Klingen übrig bleiben");
    }

    /// <summary>
    /// Selbst wenn eine Klinge die Waffe überlebt, darf ihr Treffer keine
    /// IndexOutOfRangeException mehr werfen (stats[-99]) und keinen Schaden machen.
    /// </summary>
    [UnityTest]
    public IEnumerator Klinge_einer_ersetzten_Waffe_wirft_keine_Exception()
    {
        BladeSwarm swarm = FindWeapon<BladeSwarm>("Blade Swarm");
        swarm.weaponLevel = 0;

        yield return WaitUntil(
            () => Object.FindObjectsByType<BladeSwarmPrefab>(FindObjectsSortMode.None).Length > 0,
            5f, "Blade Swarm hat keine Klingen gespawnt");

        BladeSwarmPrefab blade = Object.FindObjectsByType<BladeSwarmPrefab>(FindObjectsSortMode.None)[0];
        blade.weapon = swarm;

        DamageProbe probe = CreateProbe(blade.transform.position, Vector2.one);

        // Ohne yield: die Waffe hat noch keine Gelegenheit aufzuräumen,
        // die Klinge ist also genau in dem Zustand, der früher geknallt hat.
        swarm.weaponLevel = Weapon.RemovedLevel;

        MethodInfo onTrigger = typeof(BladeSwarmPrefab).GetMethod(
            "OnTriggerEnter2D", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(onTrigger, "OnTriggerEnter2D nicht gefunden");

        try
        {
            onTrigger.Invoke(blade, new object[] { probe.GetComponent<Collider2D>() });
        }
        catch (TargetInvocationException e)
        {
            Assert.Fail("Treffer einer ersetzten Waffe hat geworfen: " + e.InnerException);
        }

        Assert.AreEqual(0, probe.hits, "Eine ersetzte Waffe darf keinen Schaden mehr machen");

        Object.Destroy(probe.gameObject);
        yield return null;
    }

    /// <summary>
    /// Schnelle Projektile legen pro Frame mehr Strecke zurück als ihr Collider
    /// breit ist. Die reine Endpositions-Abfrage übersieht den Gegner, die
    /// Wegabfrage muss ihn finden.
    /// </summary>
    [UnityTest]
    public IEnumerator Sweep_findet_Gegner_auf_der_Flugbahn_die_Endposition_nicht()
    {
        DamageProbe probe = CreateProbe(new Vector3(0f, 50f, 0f), Vector2.one);

        GameObject projectile = new GameObject("Projectile");
        BoxCollider2D box = projectile.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(0.3f, 1f);

        Vector2 start = new Vector2(-5f, 50f);
        Vector2 end = new Vector2(5f, 50f);

        projectile.transform.position = start;
        yield return new WaitForFixedUpdate();

        // Endposition auf der anderen Seite des Gegners – ohne Berührung.
        projectile.transform.position = end;

        var found = new System.Collections.Generic.List<Enemy>();

        OverlapDamage.FindEnemies(box, found);
        Assert.AreEqual(0, found.Count,
            "Die reine Endpositions-Abfrage darf den übersprungenen Gegner nicht sehen");

        OverlapDamage.SweepEnemies(box, start, found);
        Assert.AreEqual(1, found.Count,
            "Die Wegabfrage muss den Gegner auf der Flugbahn finden");
        Assert.AreSame(probe, found[0]);

        Object.Destroy(projectile);
        Object.Destroy(probe.gameObject);
        yield return null;
    }
}
#endif
