using UnityEngine;

/// <summary>
/// Die gelbe Kapsel mit dem Kochbuch: [E] in der Naehe oeffnet das
/// Erfolge-/Unlocks-Buch (<see cref="AchievementsBookPanel"/>).
///
/// Gleicher Aufbau wie <see cref="HubSkilltreeTrigger"/> und
/// <see cref="HubShopTrigger"/> - Zone und Hinweistext stehen im Inspector, der
/// Gizmo zeigt die Zone in der Szene.
///
/// Das Objekt legt man am schnellsten ueber
/// <c>Tools ▸ Achievements ▸ Buch-Objekt in Szene setzen</c> an; das setzt
/// Sprite, Zone und Sortierung gleich mit.
/// </summary>
public class AchievementsBookTrigger : HubInteractable
{
    [Header("Buch")]
    [Tooltip("Reiter, der beim Oeffnen vorne liegt: 0 = Erfolge, 1 = Unlocks.")]
    [SerializeField] private int startTab = 0;

    private void Reset()
    {
        // Die Zone sitzt unter dem Objekt: die Kapsel steht an der Wand, der
        // Spieler laeuft davor. Gleiche Aufteilung wie beim Skilltree im Hub.
        shape = InteractShape.Rechteck;
        interactSize = new Vector2(2f, 2.5f);
        interactOffset = new Vector2(0f, -1.2f);
        promptText = "[E] Erfolge";
        showOutline = true;
        outlineMode = OutlineMode.NurInReichweite;
    }

    protected override void OnInteract()
    {
        AchievementsBookPanel.Open(startTab);
    }
}
