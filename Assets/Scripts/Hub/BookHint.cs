using UnityEngine;

/// <summary>Buch im Hub: zeigt seine Seiten nacheinander in der Textbox.</summary>
public class BookHint : HubInteractable
{
    [Header("Seiten")]
    [Tooltip("Eine Seite pro Eintrag. Zeilenumbrueche macht TextMeshPro selbst. " +
             "<wave>Wort</wave> laesst ein Wort wellen.")]
    [TextArea(3, 10)]
    public string[] pages;

    [Tooltip("Uebersetzung: Seite i steht in den Sprachdateien unter hub.book.<locKey>.<i>. " +
             "Fehlt ein Eintrag, gilt die Seite oben. Leer = Seiten unuebersetzt.")]
    public string locKey;

    [Header("Sprecher (optional)")]
    [Tooltip("Name auf dem Reiter ueber der Box. Leer = kein Reiter.")]
    public string speaker;
    [Tooltip("Portrait links in der Box. Leer = kein Portrait.")]
    public Sprite portrait;
    [Tooltip("Dasselbe Portrait mit offenem Mund - wechselt beim Tippen. Leer = nur wippen.")]
    public Sprite portraitTalking;

    protected override string PromptKey => "hub.prompt.read";

    protected override void OnInteract()
    {
        if (pages == null || pages.Length == 0)
        {
            Debug.LogWarning($"{name}: keine Seiten gesetzt.");
            return;
        }
        HubUI.Instance.ShowDialogue(LocalizedPages(), speaker, portrait, portraitTalking);
    }

    string[] LocalizedPages()
    {
        if (string.IsNullOrEmpty(locKey)) return pages;

        var shown = new string[pages.Length];
        for (int i = 0; i < pages.Length; i++)
            shown[i] = Loc.Get($"hub.book.{locKey}.{i}", pages[i]);
        return shown;
    }
}
