using UnityEngine;
using UnityEngine.SceneManagement;

public class BombSawEvoPrefab : MonoBehaviour
{
    public BombSawEvo weapon;
    [SerializeField] private GameObject prefab;

    private float counter;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        weapon = GameObject.Find("Bomb Saw Evo").GetComponent<BombSawEvo>();
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
        {
            GameObject fireBallExplosion = Instantiate(prefab, transform.position, transform.rotation);
            fireBallExplosion.transform.localScale *= weapon.stats[weapon.weaponLevel].range * (PlayerController.Instance.AOERange * 0.7f);
            Scene gameScene = SceneManager.GetSceneByName("Game");
            if (gameScene.IsValid() && gameScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(fireBallExplosion, gameScene);
            }
        }
    }
}
