using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class CandyBombPrefab : MonoBehaviour
{
    // Bonbonbombe: faellt, federt, Zuendschnur brennt ab, blinkt - 18 Bilder
    // genau bis zum Knall. Fehlt der Streifen, spielt der alte Animator.
    private const string FuseFramesPath = "Weapons/candy_bomb_fuse";
    private const float ExplodeAfter = 1.8f;

    public CandyBomb weapon; // Referenz zur Waffe
    public List<Enemy> enemiesInRange = new List<Enemy>(); // Gegner im Explosionsradius
    [SerializeField] private GameObject explosionPrefab;   // alter Effekt, nur noch Ersatz + Sorting-Layer

    private SpriteRenderer sr;
    private Sprite[] fuseFrames;
    private float age;

    void Start()
    {
        weapon = WeaponFinder.Find<CandyBomb>("Candy Bomb");

        sr = GetComponent<SpriteRenderer>();
        fuseFrames = CandyBurst.Load(FuseFramesPath);
        if (sr != null && fuseFrames.Length > 0)
        {
            Animator anim = GetComponent<Animator>();
            if (anim != null) anim.enabled = false;
            transform.position = PixelPool.Snap(transform.position);
            sr.sprite = fuseFrames[0];
        }

        // Starte Explosion nach kurzer Verzögerung
        StartCoroutine(TriggerExplosionAfterDelay(ExplodeAfter));

        Destroy(gameObject, 2.1f);
    }

    void Update()
    {
        if (sr == null || fuseFrames == null || fuseFrames.Length == 0) return;
        age += Time.deltaTime;
        int f = Mathf.Min(fuseFrames.Length - 1, (int)(age / ExplodeAfter * fuseFrames.Length));
        sr.sprite = fuseFrames[f];
    }

    private IEnumerator TriggerExplosionAfterDelay(float delay)
    {
        // ⏳ kurz warten
        yield return new WaitForSeconds(delay);

        // Wenn keine Waffe oder ungültiges Level → trotzdem zerstören
        if (weapon == null || !weapon.IsActive)
        {
            Destroy(gameObject);
            yield break;
        }

        // 💥 Explosion erzeugen
        WeaponSounds.Play(WeaponSounds.CandyBoom);
        SpawnExplosion();

        // Kopie der Liste erstellen (damit Änderungen die Schleife nicht crashen)
        var enemiesCopy = new List<Enemy>(enemiesInRange);

        float damage = weapon.CurrentStats.damage;
        foreach (var enemy in enemiesCopy)
        {
            if (enemy != null)
                enemy.TakeDamage(damage);
        }

        // 🧹 Bombe löschen (Explosion bleibt)
        Destroy(gameObject);
    }

    private void SpawnExplosion()
    {
        // Bild so gross wie der echte Schadensradius (Kollider, von CandyBomb skaliert)
        CircleCollider2D col = GetComponent<CircleCollider2D>();
        float radius = col != null ? col.radius * transform.lossyScale.x : 2.5f;

        SpriteRenderer layerFrom = explosionPrefab != null ? explosionPrefab.GetComponentInChildren<SpriteRenderer>() : sr;
        int layer = layerFrom != null ? layerFrom.sortingLayerID : 0;
        int order = layerFrom != null ? layerFrom.sortingOrder : 0;

        if (CandyBurst.Spawn(CandyBurst.CandyPrefix, CandyBurst.CandySizes, transform.position, radius, layer, order) != null)
            return;

        // 💥 Ersatz: alter Effekt, skaliert mit Waffen-Range + Spieler-AOE
        if (explosionPrefab != null)
        {
            GameObject explosion = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            float totalScale = weapon.CurrentStats.range + PlayerController.Instance.AOERange + 1.5f;
            explosion.transform.localScale = Vector3.one * totalScale;
        }
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
        {
            var enemy = collider.GetComponent<Enemy>();
            if (enemy != null && !enemiesInRange.Contains(enemy))
                enemiesInRange.Add(enemy);
        }
    }

    private void OnTriggerExit2D(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
        {
            var enemy = collider.GetComponent<Enemy>();
            if (enemy != null)
                enemiesInRange.Remove(enemy);
        }
    }
}
