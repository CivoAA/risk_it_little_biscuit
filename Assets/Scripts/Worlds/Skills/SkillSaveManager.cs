using UnityEngine;

/// <summary>
/// Nur noch ein Durchreicher. Der Skilltree liegt jetzt im Katalog
/// <see cref="SkillTrees"/>, der Fortschritt in <see cref="Skills"/>.
///
/// Diese Komponente bleibt, weil in der World Map ein paar Knöpfe im Inspector
/// auf ihre Methoden zeigen. Daten hat sie keine mehr - wer im Code etwas vom
/// Skilltree will, spricht direkt Skills an.
/// </summary>
public class SkillSaveManager : MonoBehaviour
{
    public static SkillSaveManager Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------------------------------------------------------- Knöpfe

    /// <summary>Setzt den aktiven Baum zurück und erstattet die Punkte.</summary>
    public void ResetAllSkills() => Skills.ResetActiveTree();

    /// <summary>Charakter-Level des gewählten Charakters auf 0.</summary>
    public void ResetSkillCurrency()
    {
        Skills.SetXp(Skills.ActiveTree, 0);
        WM_UIController.Instance?.UpdateSkillCurrencyText();
    }

    /// <summary>Debug-Knopf: ein Charakter-Level mehr für den gewählten Charakter.</summary>
    public void GainSkillCurrency()
    {
        Skills.SetXp(Skills.ActiveTree, CharLevel.XpForLevel(CharLevel.LevelFor(Skills.Xp) + 1));
        WM_UIController.Instance?.UpdateSkillCurrencyText();
    }

    /// <summary>
    /// Achievements, Unlocks, Skills und Skillpunkte auf null - der harte Reset.
    ///
    /// Shop-Käufe und Währung bleiben absichtlich stehen; dafür gibt es den
    /// eigenen Knopf (<see cref="WM_GameManager.OnResetButtonPressed"/>).
    /// </summary>
    public void ResetAllProgress()
    {
        Achievements.ResetAll();
        Unlocks.ResetAll();
        Skills.ResetEverything();
        Bestiary.ResetAll();

        WM_UIController.Instance?.UpdateSkillCurrencyText();
        UnlockUIManager.Instance?.RefreshUI();
        Achievement_UI_Manager.Instance?.RefreshProgressUI();

        Debug.Log("[Skills] Alles zurückgesetzt: Achievements, Unlocks, Skills und Skillpunkte.");
    }

    // ------------------------------------------------- Alte Aufrufwege

    /// <summary>
    /// Früher wurden hier die Szenen-Knoten auf den Spielstand gesetzt. Den Baum
    /// baut jetzt <see cref="SkillTreeView"/> selbst - der Aufruf bleibt nur, damit
    /// alte Knöpfe nichts kaputt machen.
    /// </summary>
    public void ApplyToScene() { }
}
