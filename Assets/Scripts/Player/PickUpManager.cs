using UnityEngine;

public class PickUpManager : MonoBehaviour
{
    public static PickUpManager Instance;
    public GameObject heart_PickUP;
    public GameObject magnet_PickUP;

    [Header("Goldenes Herz (Skill GoldenHeartChance)")]
    [Tooltip("Eigenes Prefab fuer das goldene Herz. Leer = normales Herz, golden eingefaerbt.")]
    public GameObject goldenHeart_PickUP;
    public Color goldenHeartTint = new Color(1f, 0.85f, 0.25f);

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>Legt ein Herz an - normal oder golden. Gibt null zurueck, wenn kein Prefab da ist.</summary>
    public GameObject SpawnHeart(Vector3 position, bool golden)
    {
        GameObject prefab = golden && goldenHeart_PickUP != null ? goldenHeart_PickUP : heart_PickUP;
        if (prefab == null) return null;

        GameObject heart = Instantiate(prefab, position, Quaternion.identity);
        if (!golden) return heart;

        PickUps pickup = heart.GetComponent<PickUps>();
        if (pickup != null) pickup.PickUp_id = PickUps.GoldenHeartId;

        if (goldenHeart_PickUP == null)
        {
            foreach (SpriteRenderer sr in heart.GetComponentsInChildren<SpriteRenderer>())
                sr.color = goldenHeartTint;
        }

        return heart;
    }
}
