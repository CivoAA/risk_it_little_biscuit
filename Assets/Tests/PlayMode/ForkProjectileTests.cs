#if UNITY_INCLUDE_TESTS
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Deckt den gemeldeten Fall ab: "im Stand spawnt die Gabel ohne Collider und
/// macht keinen Schaden". Geprüft wird in allen drei Bewegungszuständen, dass
/// der Collider dort sitzt wo das Sprite ist, seine volle Grösse hat und ein
/// Gegner direkt daneben Schaden bekommt.
/// </summary>
public class ForkProjectileTests : WeaponTestBase
{
    private enum Motion { Standing, Moving, StoppedJustNow }

    [UnityTest]
    public IEnumerator Gabel_trifft_und_sitzt_richtig_im_Stand()
    {
        yield return PruefeGabel(Motion.Standing);
    }

    [UnityTest]
    public IEnumerator Gabel_trifft_und_sitzt_richtig_in_Bewegung()
    {
        yield return PruefeGabel(Motion.Moving);
    }

    [UnityTest]
    public IEnumerator Gabel_trifft_und_sitzt_richtig_direkt_nach_dem_Anhalten()
    {
        yield return PruefeGabel(Motion.StoppedJustNow);
    }

    /// <summary>
    /// Die Evo skaliert ihre Gabeln mit PlayerController.AOERange. Wäre der
    /// Wert 0, hätten die Collider keine Fläche – genau das Bild von
    /// "spawnt ohne Collider".
    /// </summary>
    [UnityTest]
    public IEnumerator BloodyForkEvo_spawnt_acht_Gabeln_mit_flaechigem_Collider()
    {
        BloodyFork evo = FindWeapon<BloodyFork>("Bloody Fork Evo");
        evo.weaponLevel = 0;

        yield return WaitUntil(
            () => Object.FindObjectsByType<BloodyForkPrefab>(FindObjectsSortMode.None).Length > 0,
            5f, "Die Evo hat keine Gabel gespawnt");

        yield return new WaitForFixedUpdate();

        var forks = Object.FindObjectsByType<BloodyForkPrefab>(FindObjectsSortMode.None);
        Assert.AreEqual(8, forks.Length, "Die Evo muss in acht Richtungen spawnen");

        foreach (BloodyForkPrefab forkPrefab in forks)
        {
            BoxCollider2D box = forkPrefab.GetComponent<BoxCollider2D>();
            Assert.IsNotNull(box, "Eine Evo-Gabel hat keinen BoxCollider2D");
            Assert.IsTrue(box.enabled, "Der Collider einer Evo-Gabel ist abgeschaltet");
            Assert.Greater(box.bounds.size.x * box.bounds.size.y, 0.01f,
                "Der Collider einer Evo-Gabel hat praktisch keine Fläche "
                + "(AOERange=" + player.AOERange + ")");
        }
    }

    private IEnumerator PruefeGabel(Motion motion)
    {
        if (motion != Motion.Standing)
        {
            float until = Time.time + 1f;
            while (Time.time < until)
            {
                player.transform.position += new Vector3(4f * Time.deltaTime, 0f, 0f);
                yield return null;
            }
        }

        if (motion == Motion.StoppedJustNow) yield return null;
        else if (motion == Motion.Standing) yield return new WaitForSeconds(2f);

        // Gegner mit dynamischem Rigidbody2D, wie im echten Spiel.
        GameObject enemy = new GameObject("Probe");
        enemy.tag = "Enemy";
        enemy.layer = EnemyLayer;
        enemy.transform.position = player.transform.position + new Vector3(0.4f, 0.45f, 0f);
        enemy.AddComponent<BoxCollider2D>().size = new Vector2(0.6f, 0.6f);
        enemy.AddComponent<Rigidbody2D>().gravityScale = 0f;
        DamageProbe probe = enemy.AddComponent<DamageProbe>();

        Spikefork fork = FindWeapon<Spikefork>("Spike Fork");
        fork.weaponLevel = 0;

        SpikeforkPrefab projectile = null;
        float deadline = Time.time + 5f;
        while (Time.time < deadline && projectile == null)
        {
            var found = Object.FindObjectsByType<SpikeforkPrefab>(FindObjectsSortMode.None);
            if (found.Length > 0) projectile = found[0];

            if (motion == Motion.Moving)
                player.transform.position += new Vector3(4f * Time.deltaTime, 0f, 0f);

            yield return null;
        }
        Assert.IsNotNull(projectile, "Es wurde keine Gabel gespawnt (" + motion + ")");

        yield return new WaitForFixedUpdate();

        BoxCollider2D box = projectile.GetComponent<BoxCollider2D>();
        Assert.IsNotNull(box, "Die Gabel hat keinen BoxCollider2D");
        Assert.IsTrue(box.enabled, "Der Collider der Gabel ist abgeschaltet");

        float drift = Vector2.Distance(OverlapDamage.WorldCenter(box), (Vector2)box.bounds.center);
        Assert.Less(drift, 0.05f,
            "Der Collider sitzt " + drift.ToString("F3") + " Einheiten neben dem Sprite ("
            + motion + ") – Treffer gingen ins Leere");

        Assert.Greater(box.bounds.size.x * box.bounds.size.y, 0.01f,
            "Der Collider der Gabel hat praktisch keine Fläche (" + motion + ")");

        Assert.Greater(probe.hits, 0,
            "Der Gegner direkt an der Gabel hat keinen Schaden bekommen (" + motion + ")");

        Object.Destroy(enemy);
    }
}
#endif
