using UnityEngine;

/// <summary>
/// Teleporter, der paarweise arbeitet: zwei dieser Platten zeigen aufeinander
/// und schicken den Spieler jeweils zur anderen - hoch zum Shop und wieder
/// zurueck. Das Ziel ist eine feste Position, keine Verschiebung relativ zum
/// Spieler, sonst wandert man mit jeder Fahrt ein Stueck weiter weg.
/// </summary>
public class TeleportToShop : HubInteractable
{
    [Header("Ziel")]
    [Tooltip("Die Gegenstelle. Leer lassen geht, solange genau zwei Teleporter " +
             "in der Szene stehen - dann finden sie sich beim Start selbst.")]
    [SerializeField] private TeleportToShop partner;

    [Tooltip("Versatz gegen die Gegenstelle. 0,0 setzt den Spieler genau auf die " +
             "Platte, ein weiterer Druck bringt ihn also sofort wieder zurueck.")]
    [SerializeField] private Vector2 exitOffset = Vector2.zero;

    [Header("Sound")]
    [SerializeField] private AudioClip teleportClip;
    [Tooltip("Leer lassen - wird beim Start automatisch geholt oder angelegt.")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    protected override void Start()
    {
        base.Start();

        if (partner == null) partner = FindPartner();
        if (partner == null)
            Debug.LogWarning($"{name}: keine Gegenstelle gefunden - trag sie unter 'Partner' ein.");
        else if (partner.partner == null)
            partner.partner = this;   // Rueckweg gleich mitsetzen

        if (sfxSource == null) sfxSource = GetComponent<AudioSource>();
        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
    }

    /// <summary>Der einzige andere Teleporter in der Szene, sonst null.</summary>
    TeleportToShop FindPartner()
    {
        TeleportToShop[] all = FindObjectsByType<TeleportToShop>(FindObjectsSortMode.None);
        TeleportToShop found = null;

        foreach (TeleportToShop t in all)
        {
            if (t == this) continue;
            if (found != null) return null;   // mehr als einer - hier muss man von Hand ran
            found = t;
        }
        return found;
    }

    public Vector3 TargetPosition => partner != null
        ? partner.transform.position + (Vector3)exitOffset
        : transform.position;

    protected override void OnInteract()
    {
        if (player == null || partner == null) return;

        // Der Klang haengt an diesem Objekt und bleibt hier stehen, waehrend der
        // Spieler wegspringt. Deshalb ist die Quelle in der Szene auf 2D gestellt -
        // sonst reisst der Ton ab, sobald die Kamera 20 Units weiter oben sitzt.
        if (teleportClip != null && sfxSource != null)
            sfxSource.PlayOneShot(teleportClip, sfxVolume);

        Vector3 target = TargetPosition;
        target.z = player.position.z;

        // Rigidbody2D mitziehen, sonst zieht die Interpolation einen Schmierer
        // von der alten zur neuen Position ueber den Bildschirm.
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.position = target;
        }
        player.position = target;
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();   // Interaktionszone

        if (partner == null) return;

        // Sprungweite im Editor sichtbar machen
        Vector3 to = partner.transform.position + (Vector3)exitOffset;
        Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.9f);
        Gizmos.DrawLine(transform.position, to);
        Gizmos.DrawWireCube(to, new Vector3(1f, 1f, 0.01f));
    }
}
