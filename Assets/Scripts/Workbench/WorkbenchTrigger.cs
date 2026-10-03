using UnityEngine;

/// <summary>
/// Die Werkbank im Hub: [E] in der Naehe oeffnet den Verteiler-Bildschirm
/// (<see cref="WorkbenchPanel"/>).
///
/// Gleicher Aufbau wie <see cref="HubSkilltreeTrigger"/> und
/// <see cref="AchievementsBookTrigger"/> - Zone und Hinweistext stehen im
/// Inspector, der Gizmo zeigt die Zone in der Szene.
///
/// Das Objekt legt man am schnellsten ueber
/// <c>Tools ▸ Werkbank ▸ Werkbank in Szene setzen</c> an; das setzt Sprite,
/// Zone und Sortierung gleich mit.
/// </summary>
public class WorkbenchTrigger : HubInteractable
{
    private void Reset()
    {
        // Die Zone sitzt vor dem Moebel: die Werkbank steht an der Wand, der
        // Spieler laeuft davor. Gleiche Aufteilung wie beim Erfolge-Buch.
        shape = InteractShape.Rechteck;
        interactSize = new Vector2(3f, 2.5f);
        interactOffset = new Vector2(0f, -1.2f);
        promptText = "[E] Werkbank";
        showOutline = true;
        outlineMode = OutlineMode.NurInReichweite;
    }

    protected override void OnInteract()
    {
        WorkbenchPanel.Open();
    }
}
