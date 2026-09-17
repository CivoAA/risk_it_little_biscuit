using UnityEngine;

/// <summary>Buch im Hub: zeigt seine Seiten nacheinander in der Textbox.</summary>
public class BookHint : HubInteractable
{
    [Header("Seiten")]
    [Tooltip("Eine Seite pro Eintrag. Zeilenumbrueche macht TextMeshPro selbst.")]
    [TextArea(3, 10)]
    public string[] pages;

    protected override void OnInteract()
    {
        if (pages == null || pages.Length == 0)
        {
            Debug.LogWarning($"{name}: keine Seiten gesetzt.");
            return;
        }
        HubUI.Instance.ShowDialogue(pages);
    }
}
