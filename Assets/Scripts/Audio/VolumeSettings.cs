using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class VolumeSettings : MonoBehaviour
{
    [Header("=== SLIDERS ===")]
    public Slider masterSlider;
    public Slider sfxSlider;
    public Slider musicSlider;

    [Header("=== TEXT DISPLAYS ===")]
    public TMP_Text masterValueText;
    public TMP_Text sfxValueText;
    public TMP_Text musicValueText;

    void OnEnable()
    {
        if (AudioManager.Instance == null) return;

        // Set slider values to current volumes
        if (masterSlider != null)
        {
            masterSlider.value = PlayerPrefs.GetFloat("MasterVolume", 0.8f);
            masterSlider.onValueChanged.AddListener(OnMasterChanged);
        }

        if (sfxSlider != null)
        {
            sfxSlider.value = PlayerPrefs.GetFloat("SFXVolume", 0.8f);
            sfxSlider.onValueChanged.AddListener(OnSFXChanged);
        }

        if (musicSlider != null)
        {
            musicSlider.value = PlayerPrefs.GetFloat("MusicVolume", 0.6f);
            musicSlider.onValueChanged.AddListener(OnMusicChanged);
        }

        UpdateText();
    }

    void OnDisable()
    {
        if (masterSlider != null)
            masterSlider.onValueChanged.RemoveListener(OnMasterChanged);

        if (sfxSlider != null)
            sfxSlider.onValueChanged.RemoveListener(OnSFXChanged);

        if (musicSlider != null)
            musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
    }

    void OnMasterChanged(float value)
    {
        AudioManager.Instance.SetMasterVolume(value);
        UpdateText();

        // Play a test sound
        AudioManager.Instance.PlaySFX("ButtonClick");
    }

    void OnSFXChanged(float value)
    {
        AudioManager.Instance.SetSFXVolume(value);
        UpdateText();

        // Play a test sound
        AudioManager.Instance.PlaySFX("ButtonClick");
    }

    void OnMusicChanged(float value)
    {
        AudioManager.Instance.SetMusicVolume(value);
        UpdateText();
    }

    void UpdateText()
    {
        if (masterValueText != null && masterSlider != null)
            masterValueText.text = $"{Mathf.RoundToInt(masterSlider.value * 100)}%";

        if (sfxValueText != null && sfxSlider != null)
            sfxValueText.text = $"{Mathf.RoundToInt(sfxSlider.value * 100)}%";

        if (musicValueText != null && musicSlider != null)
            musicValueText.text = $"{Mathf.RoundToInt(musicSlider.value * 100)}%";
    }
}