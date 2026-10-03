using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class ShuriBlastEvo : Weapon
{
    [SerializeField] private GameObject prefab;
    public List<Enemy> enemiesInRange = new List<Enemy>();
    public Vector2 target;
    private float spawnCounter;
    private bool shooting = false;

    void Update()
    {
        if (weaponLevel >= 0)
        {
            UpdateTarget();
            spawnCounter -= Time.deltaTime;
            if (spawnCounter <= 0 && !shooting)
            {
                // Achievment Unlocken
                Achievements.Unlock(Ach.ShuriBlastEvo);
                StartCoroutine(SpawnShuriken());
            }
        }
    }

    IEnumerator SpawnShuriken()
    {
        shooting = true;

        int shots = Mathf.RoundToInt(stats[weaponLevel].shots + PlayerController.Instance.ExtraShots);

        for (int i = 0; i < shots; i++)
        {
            if (target == Vector2.zero) break;

            AudioController.Instance.PalySound(AudioController.Instance.BOBA);

            foreach (Vector2 dir in ThrowDirections(target)) SpawnOne(dir);

            yield return new WaitForSeconds(0.3f);
        }

        spawnCounter = CurrentCooldown;
        shooting = false;
    }

    /// <summary>
    /// Wohin geworfen wird: aufs Ziel - mit dem Skilltree "Vier Richtungen"
    /// zusaetzlich im rechten Winkel dazu und nach hinten (wie beim Shurikookie).
    /// Die Laenge ist die Flugstrecke: bis zum Ziel und 10 Einheiten weiter.
    /// </summary>
    IEnumerable<Vector2> ThrowDirections(Vector2 Target)
    {
        Vector2 toTarget = Target - (Vector2)transform.position;
        float distance = toTarget.magnitude + 10f;
        Vector2 dir = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector2.left;

        yield return dir * distance;

        if (!Skills.HasGrant(SkillGrants.ShurikookieVierRichtungen)) yield break;

        yield return new Vector2(-dir.y, dir.x) * distance;
        yield return -dir * distance;
        yield return new Vector2(dir.y, -dir.x) * distance;
    }

    void SpawnOne(Vector2 path)
    {
        GameObject BOBA = Instantiate(prefab, transform.position, transform.rotation);
        Scene gameScene = RunScene.Current;
        if (gameScene.IsValid() && gameScene.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(BOBA, gameScene);
        }
        StartCoroutine(MoveAndDestroy(BOBA, path));
    }

    /// <param name="path">Richtung mal Flugstrecke ab dem Spieler.</param>
    IEnumerator MoveAndDestroy(GameObject BOBA, Vector2 path)
    {
        float moveSpeed = 10f;

        Vector2 targetPos = (Vector2)transform.position + path;

        // Skilltree "Abpraller": am Ende der Strecke nicht verschwinden, sondern
        // zum naechsten Gegner abprallen. Der Stern durchdringt ohnehin alles -
        // darum prallt er hier erst am Ende ab statt beim Treffer.
        int bouncesLeft = Ricochet.BouncesForThrow();

        // Nicht in Flugrichtung drehen: die Drehung steckt in den Bildern
        // (SpriteFlipbook), das Gesicht in der Mitte bleibt aufrecht.
        while (BOBA != null)
        {
            while (BOBA != null && Vector3.Distance(BOBA.transform.position, targetPos) > 0.001f)
            {
                BOBA.transform.position = Vector3.MoveTowards(
                    BOBA.transform.position,
                    targetPos,
                    moveSpeed * Time.deltaTime);
                yield return null;
            }

            if (BOBA == null || bouncesLeft <= 0) break;

            Enemy next = Ricochet.Nearest(BOBA.transform.position, null);
            if (next == null) break;

            Vector2 to = (Vector2)next.transform.position - (Vector2)BOBA.transform.position;
            if (to.sqrMagnitude < 0.0001f) break;

            bouncesLeft--;
            targetPos = (Vector2)next.transform.position + to.normalized * RicochetOvershoot;
        }

        if (BOBA != null)
        {
            Destroy(BOBA);
            target = Vector2.zero;
        }
    }

    /// <summary>So weit fliegt der Stern nach einem Abpraller hinter seinem neuen Ziel weiter.</summary>
    private const float RicochetOvershoot = 4f;

    private void UpdateTarget()
    {
        if (enemiesInRange == null || enemiesInRange.Count == 0)
            return;

        Enemy closestEnemy = null;
        float closestDistance = Mathf.Infinity;
        Vector2 myPos = transform.position;

        foreach (Enemy enemy in enemiesInRange)
        {
            if (enemy == null) continue;

            float distance = Vector2.Distance(myPos, enemy.transform.position);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestEnemy = enemy;
            }
        }

        if (closestEnemy != null)
        {
            target = closestEnemy.transform.position;
        }
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
        {
            enemiesInRange.Add(collider.GetComponent<Enemy>());
        }
    }

    private void OnTriggerExit2D(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
        {
            enemiesInRange.Remove(collider.GetComponent<Enemy>());
        }
    }
}
