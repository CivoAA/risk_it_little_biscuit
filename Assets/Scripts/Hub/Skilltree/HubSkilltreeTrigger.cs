using UnityEngine;

/// <summary>
/// Oeffnet den Skilltree im Hub. Haengt am Skilltree-Objekt, die Zone ist ein
/// Rechteck - Groesse und Versatz stehen im Inspector, der Gizmo zeigt sie in
/// der Szene. Gleicher Aufbau wie der HubLevelSelectTrigger.
/// </summary>
public class HubSkilltreeTrigger : HubInteractable
{
    [Header("Skilltree")]
    [Tooltip("Leer lassen - dann wird die HubSkilltreeUI auf diesem Objekt genommen.")]
    [SerializeField] private HubSkilltreeUI skilltree;

    void Reset()
    {
        shape = InteractShape.Rechteck;
        interactSize = new Vector2(3f, 2.5f);
        promptText = "[E] Skilltree";
    }

    protected override void Start()
    {
        base.Start();

        if (skilltree == null) skilltree = GetComponent<HubSkilltreeUI>();
        if (skilltree == null) skilltree = gameObject.AddComponent<HubSkilltreeUI>();
    }

    protected override void OnInteract()
    {
        if (skilltree == null)
        {
            Debug.LogWarning($"{name}: keine HubSkilltreeUI gefunden.");
            return;
        }
        skilltree.Open();
    }
}
