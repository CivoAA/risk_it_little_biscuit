using UnityEngine;

/// <summary>
/// Das Chili-Regal unten rechts in Ricos Laden: [E] in der Naehe oeffnet die
/// Schwierigkeitswahl (<see cref="DifficultyPanel"/>).
///
/// Gleicher Aufbau wie <see cref="WorkbenchTrigger"/>. Das Regal steht frei
/// im Raum statt an der Wand, die Zone sitzt darum ohne Versatz drumherum.
/// </summary>
public class DifficultyTrigger : HubInteractable
{
    protected override string PromptKey => "hub.prompt.difficulty";

    private void Reset()
    {
        shape = InteractShape.Kreis;
        interactRadius = 1.3f;
        interactOffset = new Vector2(0f, 0.3f);
        promptText = "[E] Schwierigkeit";
        showOutline = true;
        outlineMode = OutlineMode.NurInReichweite;
    }

    protected override void OnInteract()
    {
        DifficultyPanel.Open();
    }
}
