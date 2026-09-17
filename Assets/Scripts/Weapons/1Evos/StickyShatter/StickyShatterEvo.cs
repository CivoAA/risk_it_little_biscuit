using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sticky Shatter Evo (Throwing Jam Jar + AOE Range).
///
/// Statt einer einzelnen Pfuetze zerspringt das Glas in mehrere Scherben, die
/// jeweils eine eigene kleinere Marmeladenlache hinterlassen. Die Lachen
/// verlangsamen Gegner zusaetzlich - das macht aus der reinen Zufallswurf-
/// Waffe echte Zonenkontrolle.
///
/// cooldown    = Pause zwischen zwei Wuerfen
/// duration    = Lebensdauer einer Lache
/// damage      = Schaden pro Tick
/// range       = Radius einer einzelnen Lache
/// AttackSpeed = Abstand zwischen zwei Ticks
/// shots       = Anzahl Scherben (+ playerShots)
/// </summary>
public class StickyShatterEvo : Weapon
{
    [SerializeField] private GameObject jarPrefab;    // fliegendes Glas
    [SerializeField] private GameObject puddlePrefab; // Lache nach dem Aufprall

    [Tooltip("Wie weit die Scherben vom Aufschlagpunkt wegfliegen.")]
    [SerializeField] private float shardSpread = 2.5f;

    [Tooltip("Tempo der Gegner in der Lache (0.5 = halbe Geschwindigkeit).")]
    [Range(0.05f, 1f)]
    [SerializeField] private float slowMultiplier = 0.5f;

    [SerializeField] private float throwRange = 5f;

    private float spawnCounter;

    public float SlowMultiplier { get { return slowMultiplier; } }

    void Update()
    {
        if (!IsActive) return;

        // Die Evo hat nur eine Stufe: aktiv sein heisst, sie wurde erhalten.
        Achievements.Unlock(Ach.StickyShatterEvo);

        spawnCounter -= Time.deltaTime;
        if (spawnCounter <= 0f)
        {
            spawnCounter = CurrentCooldown;
            StartCoroutine(ThrowJar(RandomSpawnPoint()));
        }
    }

    private IEnumerator ThrowJar(Vector2 targetPos)
    {
        GameObject jar = Instantiate(jarPrefab, transform.position, transform.rotation);

        Scene gameScene = SceneManager.GetSceneByName("Game");
        if (gameScene.IsValid() && gameScene.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(jar, gameScene);
        }

        while (jar != null && Vector2.Distance(jar.transform.position, targetPos) > 0.05f)
        {
            jar.transform.position = Vector3.MoveTowards(
                jar.transform.position, targetPos, 10f * Time.deltaTime);
            yield return null;
        }

        if (jar != null) Destroy(jar);

        if (AudioController.Instance != null)
        {
            AudioController.Instance.PalySound(AudioController.Instance.JarJamBreakingGlass, 0.1f);
        }

        Shatter(targetPos, gameScene);
    }

    /// <summary>Erste Lache auf den Aufschlagpunkt, die restlichen im Kreis darum.</summary>
    private void Shatter(Vector2 center, Scene gameScene)
    {
        if (!IsActive) return;

        int shards = Mathf.Max(1, Mathf.RoundToInt(
            CurrentStats.shots + PlayerController.Instance.playerShots));

        for (int i = 0; i < shards; i++)
        {
            Vector2 pos = center;
            if (i > 0)
            {
                float angle = 360f / (shards - 1) * (i - 1);
                pos += new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad),
                    Mathf.Sin(angle * Mathf.Deg2Rad)) * shardSpread;
            }

            GameObject puddle = Instantiate(puddlePrefab, pos, Quaternion.identity);

            if (gameScene.IsValid() && gameScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(puddle, gameScene);
            }

            StickyShatterEvoPrefab script = puddle.GetComponent<StickyShatterEvoPrefab>();
            if (script != null) script.weapon = this;
        }
    }

    private Vector2 RandomSpawnPoint()
    {
        return (Vector2)transform.position + Random.insideUnitCircle * throwRange;
    }
}
