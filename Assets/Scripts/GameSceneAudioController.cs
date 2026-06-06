using UnityEngine;

public class GameSceneAudioController : MonoBehaviour
{
    public AudioSource[] uiSounds; // hier klick-sounds etc reinziehen

    public void PlaySound(int index)
    {
        if (index < 0 || index >= uiSounds.Length) return;

        var src = uiSounds[index];
        // Lautstärke etc. aus globalem AudioController übernehmen:
        src.volume = AudioController.Instance.masterVolume;
        src.Play();
    }
}
