using UnityEngine;

/// <summary>
/// Nur noch ein Durchreicher fuer den Reset-Knopf im Skilltree-Panel.
///
/// Symbole und Beschreibungstexte lagen frueher hier als Inspector-Liste. Die
/// Symbole kommen jetzt ueber <see cref="SkillIcons"/> aus
/// Assets/Resources/Skills/, die Texte aus <see cref="SkillText"/> und den
/// Sprachdateien.
/// </summary>
public class SkillManager : MonoBehaviour
{
    public static SkillManager Instance;

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

    public void OnResetSkillsButton()
    {
        Skills.ResetActiveTree();
        AudioController ac = AudioController.Instance;
        if (ac != null) ac.PalySound(ac.MenuClick);
    }
}
