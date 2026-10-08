using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Haelt den Spieler fuer eine Zwischensequenz fest: keine Eingabe, kein
/// Schaden, keine Physik (<see cref="PlayerController.Captured"/>, der Koerper
/// wird kinematisch). Der Spieler selbst wird unsichtbar - an seiner Stelle
/// zeigt eine Puppe sein aktuelles Bild, die sich drehen, schrumpfen und
/// stauchen laesst, ohne dass Waffen oder Aufsammelradius mitskalieren.
///
/// Immer mit <see cref="Release"/> wieder freigeben.
/// </summary>
public class PlayerPuppet
{
    /// <summary>Koerpermitte ueber dem Pivot (der liegt an den Fuessen). Um sie dreht sich die Puppe.</summary>
    private const float BodyCenter = 0.5f;

    private readonly PlayerController player;
    private readonly Rigidbody2D rb;
    private readonly RigidbodyType2D oldType;
    private readonly RigidbodyInterpolation2D oldInterpolation;
    private readonly SpriteRenderer source;
    private readonly List<Renderer> hidden = new List<Renderer>();

    private readonly Transform pivot;
    private readonly SpriteRenderer proxy;

    public bool Released { get; private set; }

    public PlayerPuppet(PlayerController player)
    {
        this.player = player;
        player.Captured = true;

        rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            oldType = rb.bodyType;
            oldInterpolation = rb.interpolation;
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.None;
        }

        PlayerHitFeedback feedback = player.GetComponent<PlayerHitFeedback>();
        source = feedback != null && feedback.playerSpriteRenderer != null
            ? feedback.playerSpriteRenderer
            : player.GetComponent<SpriteRenderer>();

        var go = new GameObject("SpielerPuppe");
        RunScene.Place(go, "Effekte");
        pivot = go.transform;
        var body = new GameObject("Bild");
        body.transform.SetParent(pivot, false);
        body.transform.localPosition = new Vector3(0f, -BodyCenter, 0f);
        proxy = body.AddComponent<SpriteRenderer>();
        if (source != null)
        {
            proxy.sharedMaterial = source.sharedMaterial;
            proxy.sortingLayerID = source.sortingLayerID;
            proxy.sortingOrder = source.sortingOrder + 20;
        }
        proxy.enabled = false;
        pivot.position = Position;
    }

    /// <summary>Wo der Spieler steht (Fuesse).</summary>
    public Vector2 Position
    {
        get { return player != null ? (Vector2)player.transform.position : Vector2.zero; }
    }

    /// <summary>Spieler unsichtbar machen und die Puppe zeigen (oder umgekehrt).</summary>
    public void UsePuppet(bool on)
    {
        if (player == null) return;
        if (on)
        {
            if (hidden.Count == 0)
            {
                foreach (Renderer r in player.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled) continue;
                    r.enabled = false;
                    hidden.Add(r);
                }
            }
        }
        else
        {
            foreach (Renderer r in hidden) if (r != null) r.enabled = true;
            hidden.Clear();
        }
        proxy.enabled = on;
        Sync();
    }

    /// <summary>Bild der Puppe vom (unsichtbaren) Spieler uebernehmen - der Animator laeuft weiter.</summary>
    public void Sync()
    {
        if (source != null && proxy != null)
        {
            proxy.sprite = source.sprite;
            proxy.flipX = source.flipX;
        }
    }

    /// <summary>
    /// Setzt den Spieler (Fuesse) an eine Stelle. Kamera und Begleiter folgen.
    /// Die Puppe zieht mit, solange ihr niemand eine eigene Stelle gibt.
    /// </summary>
    public void MovePlayer(Vector2 feet, bool carryCompanions = false)
    {
        if (player == null) return;
        Vector2 before = player.transform.position;
        Vector3 p = player.transform.position;
        player.transform.position = new Vector3(feet.x, feet.y, p.z);
        if (rb != null) rb.position = feet;

        if (carryCompanions)
        {
            Vector3 delta = (Vector3)(feet - before);
            foreach (Companion c in Object.FindObjectsByType<Companion>())
                c.transform.position += delta;
        }
    }

    /// <summary>Puppe: Fuesse bei <paramref name="feet"/>, Groesse, Drehung (Grad), Stauchung (x, y).</summary>
    public void Pose(Vector2 feet, float scale, float angle, Vector2 squash)
    {
        if (pivot == null) return;
        Sync();
        Vector2 center = feet + Vector2.up * (BodyCenter * scale * squash.y);
        pivot.position = VerkohlterArt.Snap(center);
        pivot.rotation = Quaternion.Euler(0f, 0f, angle);
        pivot.localScale = new Vector3(scale * squash.x, scale * squash.y, 1f);
    }

    /// <summary>Spieler wieder freigeben (sichtbar, steuerbar, Physik an).</summary>
    public void Release()
    {
        if (Released) return;
        Released = true;
        UsePuppet(false);
        if (pivot != null) Object.Destroy(pivot.gameObject);
        if (player == null) return;
        if (rb != null)
        {
            rb.bodyType = oldType;
            rb.interpolation = oldInterpolation;
            rb.linearVelocity = Vector2.zero;
        }
        player.Captured = false;
    }
}
