#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Gemeinsames Setup für die Waffen-Tests: lädt die Test-Szene, schaltet den
/// Ton ab und legt ein sauberes Loadout an.
/// </summary>
public abstract class WeaponTestBase
{
    protected PlayerController player;

    /// <summary>Layer "Enemys " – so heisst er im TagManager (mit Leerzeichen).</summary>
    protected const int EnemyLayer = 7;

    [UnitySetUp]
    public IEnumerator BaseSetUp()
    {
        // Ton aus – Tests laufen stumm.
        AudioListener.volume = 0f;
        AudioListener.pause = true;

        yield return SceneManager.LoadSceneAsync("test_scene", LoadSceneMode.Single);

        // TestSceneBootstrap räumt im ersten Update alle Waffen ab.
        // Erst danach darf der Test etwas setzen.
        for (int i = 0; i < 5; i++) yield return null;

        player = PlayerController.Instance;
        Assert.IsNotNull(player, "PlayerController fehlt in der Test-Szene");

        TestSceneLoadout.ClearAll(player);
        player.playerShots = 0;
        player.damageMultiplier = 1f;
        player.critChance = 0f;

        yield return null;
    }

    [TearDown]
    public void BaseTearDown()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        AudioListener.volume = 1f;
    }

    protected static T FindWeapon<T>(string objectName) where T : Weapon
    {
        GameObject go = GameObject.Find(objectName);
        Assert.IsNotNull(go, "GameObject '" + objectName + "' nicht in der Szene gefunden");

        T weapon = go.GetComponent<T>();
        Assert.IsNotNull(weapon, "'" + objectName + "' hat keine Komponente " + typeof(T).Name);
        return weapon;
    }

    /// <summary>Legt einen ruhenden Test-Gegner an.</summary>
    protected static DamageProbe CreateProbe(Vector3 position, Vector2 size)
    {
        GameObject go = new GameObject("DamageProbe");
        go.tag = "Enemy";
        go.layer = EnemyLayer;
        go.transform.position = position;

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = size;

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        rb.gravityScale = 0f;
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep;

        return go.AddComponent<DamageProbe>();
    }

    /// <summary>Wartet, bis die Bedingung erfüllt ist, höchstens aber so lange.</summary>
    protected static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds, string message)
    {
        float deadline = Time.time + timeoutSeconds;
        while (Time.time < deadline)
        {
            if (condition()) yield break;
            yield return null;
        }

        Assert.Fail("Timeout nach " + timeoutSeconds + "s: " + message);
    }
}
#endif
