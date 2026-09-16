using UnityEngine;

/// <summary>
/// Basis fuer alles im Hub, das man per Taste im Umkreis ausloest.
/// Sucht den Spieler ueber den Tag "Player" und meldet sich beim HubUI
/// fuer den [E]-Hinweis an, solange man nah genug steht.
/// </summary>
public abstract class HubInteractable : MonoBehaviour
{
    public enum InteractShape
    {
        /// <summary>Rund um den Mittelpunkt - passt zu NPCs und einzelnen Objekten.</summary>
        Kreis = 0,
        /// <summary>Achsenparalleles Rechteck - passt zu Regalen, Theken, Wandobjekten.</summary>
        Rechteck = 1,
    }

    [Header("Interaktionszone")]
    [SerializeField] protected InteractShape shape = InteractShape.Kreis;

    [Tooltip("Nur bei Kreis. Radius in Units, 1 Unit = eine 32x32-Kachel.")]
    [SerializeField] protected float interactRadius = 1.5f;

    [Tooltip("Nur bei Rechteck. Volle Breite und Hoehe in Units.")]
    [SerializeField] protected Vector2 interactSize = new Vector2(3f, 1.5f);

    [Tooltip("Verschiebt die Zone gegen den Objektmittelpunkt, z.B. vor ein Regal.")]
    [SerializeField] protected Vector2 interactOffset = Vector2.zero;

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

    /// <summary>Spielerposition in den lokalen Achsen der Zone, Offset bereits abgezogen.</summary>
    Vector2 ToZoneSpace(Vector3 worldPos)
    {
        // Ueber die Rotation zuruecktransformieren, damit ein gedrehtes Regal
        // auch eine gedrehte Zone hat. Skalierung bleibt bewusst aussen vor:
        // das hier ist ein Naeherungstest, kein Collider.
        Vector2 delta = Quaternion.Inverse(transform.rotation) * (worldPos - transform.position);
        return delta - interactOffset;
    }

    protected bool PlayerInRange
    {
        get
        {
            if (player == null) return false;
            Vector2 d = ToZoneSpace(player.position);

            if (shape == InteractShape.Rechteck)
                return Mathf.Abs(d.x) <= interactSize.x * 0.5f
                    && Mathf.Abs(d.y) <= interactSize.y * 0.5f;

            // sqrMagnitude spart die Wurzel, das laeuft jeden Frame
            return d.sqrMagnitude <= interactRadius * interactRadius;
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

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.35f, 0.85f, 1f, 0.7f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

        if (shape == InteractShape.Rechteck)
            Gizmos.DrawWireCube(interactOffset, new Vector3(interactSize.x, interactSize.y, 0.01f));
        else
            Gizmos.DrawWireSphere(interactOffset, interactRadius);

        Gizmos.matrix = Matrix4x4.identity;
    }
}
