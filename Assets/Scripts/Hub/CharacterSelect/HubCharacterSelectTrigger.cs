using UnityEngine;

/// <summary>
/// Oeffnet die Charakterauswahl im Hub. Haengt am Charakter_Selector-Objekt, die
/// Zone ist ein Rechteck - Groesse und Versatz stehen im Inspector, der Gizmo
/// zeigt sie in der Szene. Gleicher Aufbau wie der HubLevelSelectTrigger.
/// </summary>
public class HubCharacterSelectTrigger : HubInteractable
{
    [Header("Charakterauswahl")]
    [Tooltip("Leer lassen - dann wird die HubCharacterSelectUI auf diesem Objekt genommen.")]
    [SerializeField] private HubCharacterSelectUI characterSelect;

    void Reset()
    {
        shape = InteractShape.Rechteck;
        interactSize = new Vector2(1.5f, 2.5f);
        interactOffset = new Vector2(0f, -1.2f);
        promptText = "[E] Charakter";
    }

    protected override void Start()
    {
        base.Start();

        if (characterSelect == null) characterSelect = GetComponent<HubCharacterSelectUI>();
        if (characterSelect == null) characterSelect = gameObject.AddComponent<HubCharacterSelectUI>();
    }

    protected override void OnInteract()
    {
        if (characterSelect == null)
        {
            Debug.LogWarning($"{name}: keine HubCharacterSelectUI gefunden.");
            return;
        }
        characterSelect.Open();
    }
}
