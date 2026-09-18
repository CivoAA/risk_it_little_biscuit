using UnityEngine;
using UnityEngine.SceneManagement;

public class ExplosiveStarEvoPrefab : MonoBehaviour
{
    public ExplosiveStarEvo weapon;
    [SerializeField] private GameObject prefab;

    private float counter;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        weapon = WeaponFinder.Find<ExplosiveStarEvo>("Explosive Star Evo");
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (weapon == null || !weapon.IsActive) return;
        if (collider.CompareTag("Enemy"))
        {
            GameObject fireBallExplosion = Instantiate(prefab, transform.position, transform.rotation);
            fireBallExplosion.transform.localScale *= weapon.CurrentStats.range * (PlayerController.Instance.AOERange * 0.7f);
            Scene gameScene = RunScene.Current;
            if (gameScene.IsValid() && gameScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(fireBallExplosion, gameScene);
            }
        }
    }
}
