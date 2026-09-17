using UnityEngine;

/// <summary>
/// Oeffnet die Levelauswahl im Hub. Haengt am LevelSelectUI-Objekt, die Zone ist
/// ein Rechteck - Groesse und Versatz stehen im Inspector, der Gizmo zeigt sie
/// in der Szene. Gleicher Aufbau wie der HubShopTrigger.
/// </summary>
public class HubLevelSelectTrigger : HubInteractable
{
    [Header("Levelauswahl")]
    [Tooltip("Leer lassen - dann wird die HubLevelSelectUI auf diesem Objekt genommen.")]
    [SerializeField] private HubLevelSelectUI levelSelect;

    void Reset()
    {
        shape = InteractShape.Rechteck;
        interactSize = new Vector2(3f, 2.5f);
        promptText = "[E] Levelauswahl";
    }

    protected override void Start()
    {
        base.Start();

        if (levelSelect == null) levelSelect = GetComponent<HubLevelSelectUI>();
        if (levelSelect == null) levelSelect = gameObject.AddComponent<HubLevelSelectUI>();
    }

    protected override void OnInteract()
    {
        if (levelSelect == null)
        {
            Debug.LogWarning($"{name}: keine HubLevelSelectUI gefunden.");
            return;
        }
        levelSelect.Open();
    }
}
