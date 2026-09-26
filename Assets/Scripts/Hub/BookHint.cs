using UnityEngine;

/// <summary>Buch im Hub: zeigt seine Seiten nacheinander in der Textbox.</summary>
public class BookHint : HubInteractable
{
    [Header("Seiten")]
    [Tooltip("Eine Seite pro Eintrag. Zeilenumbrueche macht TextMeshPro selbst. " +
             "<wave>Wort</wave> laesst ein Wort wellen.")]
    [TextArea(3, 10)]
    public string[] pages;

    [Header("Sprecher (optional)")]
    [Tooltip("Name auf dem Reiter ueber der Box. Leer = kein Reiter.")]
    public string speaker;
    [Tooltip("Portrait links in der Box. Leer = kein Portrait.")]
    public Sprite portrait;
    [Tooltip("Dasselbe Portrait mit offenem Mund - wechselt beim Tippen. Leer = nur wippen.")]
    public Sprite portraitTalking;

    protected override void OnInteract()
    {
        if (pages == null || pages.Length == 0)
        {
            Debug.LogWarning($"{name}: keine Seiten gesetzt.");
            return;
        }
        HubUI.Instance.ShowDialogue(pages, speaker, portrait, portraitTalking);
    }
}
