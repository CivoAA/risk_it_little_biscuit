using UnityEngine;

/// <summary>
/// Die Figur eines Mitspielers in der eigenen Welt. Aussehen und Bewegung
/// liefert <see cref="NetVisuals"/> (wie bei allem, was von anderen kommt),
/// den Zustand (Leben, niedergegangen, raus) <see cref="NetRun"/>.
///
/// Fuer die eigenen Gegner ist sie ein Ziel wie der eigene Spieler
/// (<see cref="NetPlayers"/>). Der Trigger-Collider ist fuer Boss-Attacken
/// mit Trefferflaeche da: die fragen <see cref="NetPlayers.TryGetTarget"/>
/// und schicken den Schaden dann an den Besitzer. Getaggt ist die Figur
/// bewusst NICHT als "Player" - sonst wuerden alte Stellen, die bei
/// CompareTag("Player") den eigenen Spieler treffen, ihn fuer die Figur
/// eines anderen bestrafen.
/// </summary>
public class NetAvatar : MonoBehaviour
{
    public int Slot { get; private set; }

    public NetRun.PeerState State => NetRun.Active != null ? NetRun.Active.StateOf(Slot) : NetRun.PeerState.Left;

    /// <summary>Steht und kann angegriffen werden.</summary>
    public bool Targetable => isActiveAndEnabled && State == NetRun.PeerState.Alive;

    private CircleCollider2D hitbox;

    public void Init(int slot)
    {
        Slot = slot;
        gameObject.name = "Mitspieler " + (slot + 1);

        PlayerController local = PlayerController.Instance;
        if (local != null) gameObject.layer = local.gameObject.layer;

        Rigidbody2D rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        rb.interpolation = RigidbodyInterpolation2D.None;

        hitbox = gameObject.AddComponent<CircleCollider2D>();
        hitbox.isTrigger = true;
        hitbox.radius = 0.32f;
        hitbox.offset = new Vector2(0f, 0.35f);

        // So gross wie der Koerper des eigenen Spielers - alle Figuren sind gleich gebaut.
        Collider2D body = local != null ? local.GetComponent<Collider2D>() : null;
        if (body != null)
        {
            Vector3 scale = local.transform.lossyScale;
            Vector2 size = body.bounds.size;
            hitbox.radius = Mathf.Max(0.15f, Mathf.Min(size.x, size.y) * 0.5f / Mathf.Max(0.01f, Mathf.Abs(scale.x)));
            hitbox.offset = body.offset;
        }

        NetPlayers.Register(this);
    }

    private void OnDestroy()
    {
        NetPlayers.Unregister(this);
    }
}
