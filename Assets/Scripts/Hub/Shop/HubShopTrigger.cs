using UnityEngine;

/// <summary>
/// Oeffnet den Shop im Hub. Haengt am ShopUI-Objekt, die Zone ist ein Rechteck -
/// Groesse und Versatz stehen im Inspector, der Gizmo zeigt sie in der Szene.
/// </summary>
public class HubShopTrigger : HubInteractable
{
    [Header("Shop")]
    [Tooltip("Leer lassen - dann wird die HubShopUI auf diesem Objekt genommen.")]
    [SerializeField] private HubShopUI shop;

    void Reset()
    {
        shape = InteractShape.Rechteck;
        interactSize = new Vector2(3f, 2f);
        promptText = "[E] Shop";
    }

    protected override void Start()
    {
        base.Start();

        if (shop == null) shop = GetComponent<HubShopUI>();
        if (shop == null) shop = gameObject.AddComponent<HubShopUI>();
    }

    protected override void OnInteract()
    {
        if (shop == null)
        {
            Debug.LogWarning($"{name}: keine HubShopUI gefunden.");
            return;
        }
        shop.Open();
    }
}
