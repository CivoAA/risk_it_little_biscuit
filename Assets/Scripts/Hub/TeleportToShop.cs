using UnityEngine;

/// <summary>
/// Versetzt den Spieler um eine feste Anzahl Kacheln nach oben.
/// Dort entsteht spaeter der Shop.
/// </summary>
public class TeleportToShop : HubInteractable
{
    [Header("Ziel")]
    [Tooltip("Anzahl Kacheln nach oben.")]
    [SerializeField] private int tilesUp = 20;
    [Tooltip("Weltgroesse einer Kachel. 32 px bei 32 PPU = 1 Unit.")]
    [SerializeField] private float unitsPerTile = 1f;

    public Vector3 TargetPosition => player != null
        ? player.position + Vector3.up * (tilesUp * unitsPerTile)
        : transform.position + Vector3.up * (tilesUp * unitsPerTile);

    protected override void OnInteract()
    {
        if (player == null) return;

        Vector3 target = player.position + Vector3.up * (tilesUp * unitsPerTile);

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

    void OnDrawGizmosSelected()
    {
        // Sprungweite im Editor sichtbar machen
        Vector3 from = transform.position;
        Vector3 to = from + Vector3.up * (tilesUp * unitsPerTile);
        Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.9f);
        Gizmos.DrawLine(from, to);
        Gizmos.DrawWireCube(to, new Vector3(1f, 1f, 0f));
    }
}
