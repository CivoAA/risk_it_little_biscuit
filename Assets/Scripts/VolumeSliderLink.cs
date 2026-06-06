using UnityEngine;
using UnityEngine.UI;

public class VolumeSlider : MonoBehaviour
{
    public enum VolumeType { Master, Music, Effects }
    public VolumeType volumeType;

    private Slider slider;

    void Awake()
    {
        slider = GetComponent<Slider>();
        slider.onValueChanged.AddListener(OnSliderChanged);
    }

    void Start()
    {
        if (AudioSettingsManager.Instance != null)
        {
            switch (volumeType)
            {
                case VolumeType.Master: slider.value = AudioSettingsManager.Instance.currentSettings.masterVolume; break;
                case VolumeType.Music:  slider.value = AudioSettingsManager.Instance.currentSettings.musicVolume; break;
                case VolumeType.Effects:slider.value = AudioSettingsManager.Instance.currentSettings.effectsVolume; break;
            }
        }
    }

    private void OnSliderChanged(float value)
    {
        if (AudioSettingsManager.Instance != null)
        {
            switch (volumeType)
            {
                case VolumeType.Master: AudioSettingsManager.Instance.SetMasterVolume(value); break;
                case VolumeType.Music:  AudioSettingsManager.Instance.SetMusicVolume(value); break;
                case VolumeType.Effects:AudioSettingsManager.Instance.SetEffectsVolume(value); break;
            }
        }
    }

    void OnDestroy()
    {
        slider.onValueChanged.RemoveListener(OnSliderChanged);
    }
    
    public void RefreshFromSettings()
    {
        if (AudioSettingsManager.Instance == null) return;

        switch (volumeType)
        {
            case VolumeType.Master:
                slider.value = AudioSettingsManager.Instance.currentSettings.masterVolume;
                break;
            case VolumeType.Music:
                slider.value = AudioSettingsManager.Instance.currentSettings.musicVolume;
                break;
            case VolumeType.Effects:
                slider.value = AudioSettingsManager.Instance.currentSettings.effectsVolume;
                break;
        }
    }
}