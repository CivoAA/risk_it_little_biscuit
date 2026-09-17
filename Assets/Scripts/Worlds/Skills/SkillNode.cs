using UnityEngine;

/// <summary>
/// VERALTET - nur noch ein Platzhalter.
///
/// Skill-Knoten stehen jetzt im Katalog <see cref="SkillTrees"/> und werden von
/// <see cref="SkillTreeView"/> zur Laufzeit erzeugt. In der Szene liegt keiner
/// mehr.
///
/// Diese Komponente existiert, damit die 142 alten Knoten-Objekte in
/// "World Map.unity" kein fehlendes Skript anzeigen. Sie raeumen sich beim Start
/// selbst weg, damit sie dem neu gebauten Baum nicht im Weg liegen.
///
/// ZU TUN (einmalig, in Unity): unter SkillTreeCanvas die alten Knoten-Objekte
/// loeschen - den Scroll-View-Baum komplett, und in den vier Tree Panels die
/// Kinder von "Button Root". Danach kann diese Datei weg.
/// </summary>
[System.Obsolete("Skills stehen jetzt in SkillTrees.cs. Alte Knoten-Objekte aus der Szene loeschen.")]
[AddComponentMenu("")]
public class SkillNode : MonoBehaviour
{
    private static bool warned;
    private static int removed;

    private void Awake()
    {
        removed++;

        if (!warned)
        {
            warned = true;
            Debug.LogWarning(
                "[Skills] Alte Skill-Knoten in der Szene gefunden. Sie werden beim Start entfernt, " +
                "der Baum wird aus SkillTrees.cs neu gebaut. Die Objekte duerfen unter " +
                "SkillTreeCanvas geloescht werden.");
        }

        Destroy(gameObject);
    }

    /// <summary>Wie viele alte Knoten dieser Start weggeraeumt hat - fuer die Konsole.</summary>
    public static int RemovedCount => removed;
}
