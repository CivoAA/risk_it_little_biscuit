using UnityEngine;
using UnityEngine.SceneManagement;

public class BombSawEvoPrefab : MonoBehaviour
{
    public BombSawEvo weapon;
    [SerializeField] private GameObject prefab;   // alter Explosions-Prefab: Groesse + Sorting-Layer, sonst Ersatz

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        weapon = WeaponFinder.Find<BombSawEvo>("Bomb Saw Evo");
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (weapon == null || !weapon.IsActive) return;
        if (collider.CompareTag("Enemy"))
        {
            WeaponSounds.Play(WeaponSounds.SawBoom);
            float grow = weapon.CurrentStats.range * (PlayerController.Instance.AOERange * 0.4f);
            if (SpawnCandyBurst(grow)) return;

            GameObject fireBallExplosion = Instantiate(prefab, transform.position, transform.rotation);
            fireBallExplosion.transform.localScale *= grow;
            Scene gameScene = RunScene.Current;
            if (gameScene.IsValid() && gameScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(fireBallExplosion, gameScene);
            }
        }
    }

    /// <summary>
    /// Pixelgenaue Bonbon-Explosion statt des skalierten Effekts. Radius wie
    /// bisher: Prefab-Massstab x Kolliderradius x Wachstum. Der Schaden kommt
    /// weiter aus BombSawEvoExplosionPrefab auf einem Trigger dieser Groesse.
    /// </summary>
    private bool SpawnCandyBurst(float grow)
    {
        float baseRadius = 1f;
        int layer = 0, order = 0;
        if (prefab != null)
        {
            CircleCollider2D old = prefab.GetComponent<CircleCollider2D>();
            baseRadius = prefab.transform.localScale.x * (old != null ? old.radius : 1f);
            SpriteRenderer oldSr = prefab.GetComponentInChildren<SpriteRenderer>();
            if (oldSr != null)
            {
                layer = oldSr.sortingLayerID;
                order = oldSr.sortingOrder;
            }
        }
        float radius = baseRadius * grow;

        GameObject burst = CandyBurst.Spawn(CandyBurst.SawPrefix, CandyBurst.SawSizes, transform.position,
                                            radius, layer, order);
        if (burst == null) return false;

        CircleCollider2D hit = burst.AddComponent<CircleCollider2D>();
        hit.isTrigger = true;
        hit.radius = radius / Mathf.Max(0.0001f, burst.transform.localScale.x);
        burst.AddComponent<BombSawEvoExplosionPrefab>();
        return true;
    }
}
