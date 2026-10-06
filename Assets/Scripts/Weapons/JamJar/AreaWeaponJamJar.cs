using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

/// <summary>
/// Throwing Jam Jar - Startwaffe der Marmelade.
///
/// Wirft alle <see cref="Weapon.CurrentCooldown"/> Sekunden ein Glas auf einen
/// Punkt um den Spieler. Dort platzt es sofort und hinterlaesst eine Lache
/// (<see cref="AreaWeaponPrefabJamJar"/>), die Gegner darin regelmaessig
/// verletzt. Extra-Schuss wirkt bewusst NICHT - es bleibt ein Glas pro Wurf.
///
/// Die Evo <see cref="StickyShatterEvo"/> erbt von hier, damit Zielwurf und
/// Einkochen fuer beide gelten. Sie ueberschreibt nur, was beim Aufprall passiert.
///
/// cooldown    = Pause zwischen zwei Wuerfen
/// duration    = Lebensdauer einer Lache
/// damage      = Schaden pro Tick
/// range       = Groesse einer Lache
/// AttackSpeed = Abstand zwischen zwei Ticks
/// </summary>
public class AreaWeaponJamJar : Weapon
{
    [Tooltip("Das fliegende Glas.")]
    [FormerlySerializedAs("JamJarprefab")]
    [SerializeField] protected GameObject jarPrefab;

    [Tooltip("Die Lache nach dem Aufprall.")]
    [FormerlySerializedAs("prefab")]
    [SerializeField] protected GameObject puddlePrefab;

    [Tooltip("So weit fliegt ein Glas hoechstens.")]
    [SerializeField] protected float throwRange = 5f;

    /// <summary>So weit fliegt ein Glas mindestens - es soll sichtbar fliegen.</summary>
    private const float MinThrowDistance = 1.5f;

    /// <summary>Fluggeschwindigkeit in Einheiten pro Sekunde.</summary>
    private const float FlightSpeed = 10f;

    // ------------------------------------------------------------ Zielwurf

    /// <summary>Jedes wievielte Glas mit Zielwurf in die dichteste Gruppe fliegt.</summary>
    public const int AimedEvery = 3;

    /// <summary>So weit um den Spieler sucht der Zielwurf nach Gegnern.</summary>
    public const float AimedRange = 8f;

    /// <summary>Gegner in diesem Abstand zaehlen zur selben Gruppe.</summary>
    private const float GroupRadius = 2.5f;

    private float spawnCounter;
    private int throwCount;

    private static readonly List<Vector2> NearBuffer = new List<Vector2>();

    protected virtual void Update()
    {
        if (!IsActive) return;

        UnlockAchievements();

        spawnCounter -= Time.deltaTime;
        if (spawnCounter > 0f) return;

        spawnCounter = CurrentCooldown;
        throwCount++;

        bool aimed = Skills.HasGrant(SkillGrants.Zielwurf) && throwCount % AimedEvery == 0;
        Throw(aimed && TryFindGroup(out Vector2 group) ? group : RandomTarget());
    }

    protected virtual void UnlockAchievements()
    {
        if (weaponLevel == maxweaponLevel) Achievements.Unlock(Ach.MaxThrowingJamJar);
    }

    // ------------------------------------------------------------ Wurf

    /// <summary>
    /// Wirft ein Glas nach <paramref name="target"/>. Das Glas fliegt selbst
    /// (<see cref="JamJarFlight"/>) und haengt nicht am Spieler - es kommt also
    /// immer an, egal wie man sich bewegt.
    /// </summary>
    private void Throw(Vector2 target)
    {
        GameObject jar = Instantiate(jarPrefab, transform.position, Quaternion.identity);
        MoveToRunScene(jar);
        JamJarFlight.Launch(jar, target, FlightSpeed, OnJarLanded);
    }

    private void OnJarLanded(Vector2 position)
    {
        // Die Waffe kann waehrend des Flugs verschwunden sein (Szenenwechsel).
        if (this == null) return;

        if (AudioController.Instance != null)
        {
            AudioController.Instance.PalySound(AudioController.Instance.JarJamBreakingGlass, 0.1f);
        }

        // Durch die Evo ersetzt: das Glas zerbricht, hinterlaesst aber nichts.
        if (!IsActive) return;

        Landed(position);
    }

    /// <summary>Was beim Aufprall entsteht. Hier: eine Lache.</summary>
    protected virtual void Landed(Vector2 position)
    {
        SpawnPuddle(position);
    }

    protected void SpawnPuddle(Vector2 position)
    {
        GameObject puddle = Instantiate(puddlePrefab, position, Quaternion.identity);
        MoveToRunScene(puddle);

        AreaWeaponPrefabJamJar script = puddle.GetComponent<AreaWeaponPrefabJamJar>();
        if (script != null) script.weapon = this;
    }

    private static void MoveToRunScene(GameObject go)
    {
        Scene run = RunScene.Current;
        if (run.IsValid() && run.isLoaded) SceneManager.MoveGameObjectToScene(go, run);
    }

    // ------------------------------------------------------------ Ziele

    /// <summary>Zufaelliger Punkt im Ring zwischen Mindestweite und <see cref="throwRange"/>.</summary>
    private Vector2 RandomTarget()
    {
        float min = Mathf.Min(MinThrowDistance, throwRange);
        // Wurzel, damit die Punkte gleichmaessig ueber die Ringflaeche verteilt sind.
        float t = Random.value;
        float distance = Mathf.Sqrt(Mathf.Lerp(min * min, throwRange * throwRange, t));
        Vector2 dir = Random.insideUnitCircle.normalized;
        if (dir == Vector2.zero) dir = Vector2.right;

        return (Vector2)transform.position + dir * distance;
    }

    /// <summary>
    /// Skilltree "Zielwurf": die Mitte der groessten Gegnergruppe im Umkreis
    /// von <see cref="AimedRange"/>. False, wenn niemand da ist.
    /// </summary>
    private bool TryFindGroup(out Vector2 center)
    {
        center = Vector2.zero;
        Vector2 origin = transform.position;
        var enemies = Enemy.Alive;

        // Erst die Gegner in Reichweite sammeln, dann nur unter denen zaehlen.
        List<Vector2> near = NearBuffer;
        near.Clear();
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy e = enemies[i];
            if (e == null) continue;

            Vector2 p = e.transform.position;
            if ((p - origin).sqrMagnitude <= AimedRange * AimedRange) near.Add(p);
        }

        if (near.Count == 0) return false;

        int bestCount = 0;
        float groupSqr = GroupRadius * GroupRadius;

        for (int i = 0; i < near.Count; i++)
        {
            int count = 0;
            Vector2 sum = Vector2.zero;

            for (int j = 0; j < near.Count; j++)
            {
                if ((near[j] - near[i]).sqrMagnitude > groupSqr) continue;
                count++;
                sum += near[j];
            }

            if (count > bestCount)
            {
                bestCount = count;
                center = sum / count;
            }
        }

        return true;
    }
}
