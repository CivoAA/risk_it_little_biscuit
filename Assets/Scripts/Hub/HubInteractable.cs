using UnityEngine;

/// <summary>
/// Basis fuer alles im Hub, das man per Taste im Umkreis ausloest.
/// Sucht den Spieler ueber den Tag "Player" und meldet sich beim HubUI
/// fuer den [E]-Hinweis an, solange man nah genug steht.
/// </summary>
public abstract class HubInteractable : MonoBehaviour
{
    [Header("Interaktion")]
    [Tooltip("Radius in Units, in dem die Taste wirkt. 1 Unit = eine 32x32-Kachel.")]
    [SerializeField] protected float interactRadius = 1.5f;
    [SerializeField] protected KeyCode interactKey = KeyCode.E;

    [Header("Hinweis")]
    [SerializeField] protected bool showPrompt = true;
    [SerializeField] protected string promptText = "[E] Lesen";

    protected Transform player;

    protected virtual void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
        else Debug.LogWarning($"{name}: kein GameObject mit Tag 'Player' gefunden - Interaktion bleibt aus.");
    }

    protected bool PlayerInRange
    {
        get
        {
            if (player == null) return false;
            // sqrMagnitude spart die Wurzel, das laeuft jeden Frame
            return ((Vector2)(player.position - transform.position)).sqrMagnitude
                   <= interactRadius * interactRadius;
        }
    }

    protected virtual void Update()
    {
        // Waehrend ein Dialog offen ist, reagiert nichts im Hub
        bool active = PlayerInRange && !HubUI.DialogueOpen;

        if (showPrompt && active) HubUI.Instance.RequestPrompt(promptText);
        if (active && Input.GetKeyDown(interactKey)) OnInteract();
    }

    protected abstract void OnInteract();

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.35f, 0.85f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}
