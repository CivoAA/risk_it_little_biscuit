using UnityEngine;

/// <summary>
/// Oeffnet die Waffenkunde (<see cref="WeaponGuidePanel"/>) mit [E].
///
/// Vorerst ein leeres Objekt an der rechten Wand in Ricos Laden - der NPC,
/// der hier erklaert, kommt spaeter. Bekommt das Objekt (oder ein Kind) dann
/// einen SpriteRenderer, einfach showOutline anschalten.
/// </summary>
public class WeaponGuideTrigger : HubInteractable
{
    protected override string PromptKey => "hub.prompt.guide";

    private void Reset()
    {
        // Steht an der Wand, der Spieler kommt von links - Zone darum etwas nach links.
        shape = InteractShape.Kreis;
        interactRadius = 1.3f;
        interactOffset = new Vector2(-0.5f, 0f);
        promptText = "[E] Waffenkunde";
        showOutline = false;
        outlineMode = OutlineMode.NurInReichweite;
    }

    protected override void OnInteract()
    {
        WeaponGuidePanel.Open();
    }
}
