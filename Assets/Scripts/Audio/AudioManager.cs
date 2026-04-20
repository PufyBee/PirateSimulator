using UnityEngine;
using UnityEngine.Audio;
using System;
using System.Collections;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("=== AUDIO MIXER (Optional) ===")]
    public AudioMixer audioMixer;
    public string sfxVolumeParameter = "SFXVolume";
    public string musicVolumeParameter = "MusicVolume";
    public string masterVolumeParameter = "MasterVolume";

    [Header("=== AUDIO SOURCES ===")]
    public AudioSource musicSource;
    public AudioSource musicSource2; // For crossfading
    public AudioSource ambientSource;

    [Header("=== SFX POOLING ===")]
    public int sfxPoolSize = 10;
    public GameObject sfxSourcePrefab;

    [Header("=== DEFAULT VOLUMES ===")]
    [Range(0f, 1f)]
    public float defaultMasterVolume = 0.8f;
    [Range(0f, 1f)]
    public float defaultSFXVolume = 0.8f;
    [Range(0f, 1f)]
    public float defaultMusicVolume = 0.6f;

    [Header("=== AUDIO CLIPS ===")]
    public SoundClip[] soundEffects;
    public SoundClip[] musicTracks;
    public SoundClip[] ambientSounds;

    [Header("=== DEBUG ===")]
    public bool debugMode = true;

    // Runtime
    private AudioSource[] sfxPool;
    private int currentSFXIndex = 0;
    private AudioSource currentMusicSource;
    private bool isCrossfading = false;
    private float masterVolume = 0.8f;
    private float sfxVolume = 0.8f;
    private float musicVolume = 0.6f;

    [System.Serializable]
    public class SoundClip
    {
        public string name;
        public AudioClip clip;
        [Range(0f, 1f)]
        public float volume = 1f;
        [Range(0.5f, 2f)]
        public float pitchMin = 1f;
        [Range(0.5f, 2f)]
        public float pitchMax = 1f;
        public bool loop = false;
        public float spatialBlend = 0f; // 0 = 2D, 1 = 3D
    }

    void Awake()
    {
        // Singleton setup
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializeAudioSystem();
    }

    void Start()
    {
        LoadVolumePreferences();
        PlayMusic("Setup", 0f);
    }

    void InitializeAudioSystem()
    {
        // Create audio sources if not assigned
        if (musicSource == null)
        {
            GameObject musicObj = new GameObject("MusicSource");
            musicObj.transform.SetParent(transform);
            musicSource = musicObj.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f; // 2D
        }

        if (musicSource2 == null)
        {
            GameObject musicObj2 = new GameObject("MusicSource2");
            musicObj2.transform.SetParent(transform);
            musicSource2 = musicObj2.AddComponent<AudioSource>();
            musicSource2.loop = true;
            musicSource2.playOnAwake = false;
            musicSource2.spatialBlend = 0f; // 2D
        }

        if (ambientSource == null)
        {
            GameObject ambientObj = new GameObject("AmbientSource");
            ambientObj.transform.SetParent(transform);
            ambientSource = ambientObj.AddComponent<AudioSource>();
            ambientSource.loop = true;
            ambientSource.playOnAwake = false;
            ambientSource.spatialBlend = 0f; // 2D
        }

        // Create SFX pool
        sfxPool = new AudioSource[sfxPoolSize];

        for (int i = 0; i < sfxPoolSize; i++)
        {
            if (sfxSourcePrefab != null)
            {
                GameObject poolObj = Instantiate(sfxSourcePrefab, transform);
                poolObj.name = $"SFXSource_{i}";
                sfxPool[i] = poolObj.GetComponent<AudioSource>();
                if (sfxPool[i] == null)
                    sfxPool[i] = poolObj.AddComponent<AudioSource>();
            }
            else
            {
                GameObject poolObj = new GameObject($"SFXSource_{i}");
                poolObj.transform.SetParent(transform);
                sfxPool[i] = poolObj.AddComponent<AudioSource>();
            }

            sfxPool[i].playOnAwake = false;
            sfxPool[i].spatialBlend = 0f; // Default 2D
        }

        currentMusicSource = musicSource;

        if (debugMode)
            Debug.Log($"AudioManager initialized with {sfxPoolSize} SFX sources");
    }
    public void PlaySFX(string soundName)
    {
        PlaySFX(soundName, Vector3.zero, false);
    }
    public void PlaySFX(string soundName, Vector3 position)
    {
        PlaySFX(soundName, position, true);
    }

    private void PlaySFX(string soundName, Vector3 position, bool usePosition)
    {
        SoundClip clipData = Array.Find(soundEffects, s => s.name == soundName);

        if (clipData == null || clipData.clip == null)
        {
            if (debugMode)
                Debug.LogWarning($"SFX '{soundName}' not found!");
            return;
        }

        AudioSource source = GetAvailableSFXSource();

        if (source == null)
        {
            if (debugMode)
                Debug.LogWarning("No available SFX sources!");
            return;
        }

        // Configure the source
        source.clip = clipData.clip;
        source.volume = clipData.volume * sfxVolume * masterVolume;
        source.pitch = UnityEngine.Random.Range(clipData.pitchMin, clipData.pitchMax);
        source.loop = clipData.loop;
        source.spatialBlend = clipData.spatialBlend;

        // Position for 3D sounds
        if (usePosition && clipData.spatialBlend > 0f)
        {
            source.transform.position = position;
        }
        else
        {
            source.transform.position = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        }

        source.Play();

        if (debugMode)
            Debug.Log($"Playing SFX: {soundName} at volume {source.volume:F2}");
    }

    public void PlayOneShot(string soundName)
    {
        PlayOneShot(soundName, Vector3.zero, false);
    }
    public void PlayOneShot(string soundName, Vector3 position)
    {
        PlayOneShot(soundName, position, true);
    }

    private void PlayOneShot(string soundName, Vector3 position, bool usePosition)
    {
        SoundClip clipData = Array.Find(soundEffects, s => s.name == soundName);

        if (clipData == null || clipData.clip == null)
        {
            if (debugMode)
                Debug.LogWarning($"SFX '{soundName}' not found!");
            return;
        }

        AudioSource source = GetAvailableSFXSource();

        if (source == null) return;

        float volume = clipData.volume * sfxVolume * masterVolume;
        float pitch = UnityEngine.Random.Range(clipData.pitchMin, clipData.pitchMax);

        source.pitch = pitch;
        source.spatialBlend = clipData.spatialBlend;

        if (usePosition && clipData.spatialBlend > 0f)
        {
            source.transform.position = position;
            source.PlayOneShot(clipData.clip, volume);
        }
        else
        {
            source.PlayOneShot(clipData.clip, volume);
        }

        if (debugMode)
            Debug.Log($"Playing one-shot SFX: {soundName}");
    }

    private AudioSource GetAvailableSFXSource()
    {
        // Simple round-robin pool
        for (int i = 0; i < sfxPoolSize; i++)
        {
            int index = (currentSFXIndex + i) % sfxPoolSize;
            if (!sfxPool[index].isPlaying)
            {
                currentSFXIndex = (index + 1) % sfxPoolSize;
                return sfxPool[index];
            }
        }

        // If all are playing, use the next one anyway (will cut off)
        currentSFXIndex = (currentSFXIndex + 1) % sfxPoolSize;
        return sfxPool[currentSFXIndex];
    }

    public void PlayMusic(string musicName, float crossfadeDuration = 1f)
    {
        SoundClip clipData = Array.Find(musicTracks, m => m.name == musicName);

        if (clipData == null || clipData.clip == null)
        {
            if (debugMode)
                Debug.LogWarning($"Music '{musicName}' not found!");
            return;
        }

        // Don't restart if already playing
        if (currentMusicSource.isPlaying && currentMusicSource.clip == clipData.clip)
            return;

        StopAllCoroutines();
        StartCoroutine(CrossfadeMusic(clipData, crossfadeDuration));
    }

    private IEnumerator CrossfadeMusic(SoundClip newClip, float duration)
    {
        isCrossfading = true;

        // Determine which source to use for new music
        AudioSource newSource = (currentMusicSource == musicSource) ? musicSource2 : musicSource;
        AudioSource oldSource = currentMusicSource;

        // Configure new source
        newSource.clip = newClip.clip;
        newSource.volume = 0f;
        newSource.loop = newClip.loop;
        newSource.pitch = 1f;
        newSource.Play();

        float targetVolume = newClip.volume * musicVolume * masterVolume;

        // Crossfade
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Smooth fade
            float fadeIn = Mathf.SmoothStep(0f, targetVolume, t);
            float fadeOut = Mathf.SmoothStep(oldSource.volume, 0f, t);

            newSource.volume = fadeIn;
            oldSource.volume = fadeOut;

            yield return null;
        }

        // Cleanup
        oldSource.Stop();
        oldSource.volume = 0f;
        newSource.volume = targetVolume;
        currentMusicSource = newSource;
        isCrossfading = false;

        if (debugMode)
            Debug.Log($"Now playing music: {newClip.name}");
    }

    public void StopMusic(float fadeDuration = 1f)
    {
        StopAllCoroutines();
        StartCoroutine(FadeOutMusic(fadeDuration));
    }

    private IEnumerator FadeOutMusic(float duration)
    {
        float startVolume = currentMusicSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            currentMusicSource.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        currentMusicSource.Stop();
        currentMusicSource.volume = startVolume;
    }

    public void PlayAmbient(string ambientName)
    {
        SoundClip clipData = Array.Find(ambientSounds, a => a.name == ambientName);

        if (clipData == null || clipData.clip == null)
        {
            if (debugMode)
                Debug.LogWarning($"Ambient '{ambientName}' not found!");
            return;
        }

        ambientSource.clip = clipData.clip;
        ambientSource.volume = clipData.volume * musicVolume * masterVolume;
        ambientSource.loop = true;
        ambientSource.Play();

        if (debugMode)
            Debug.Log($"Playing ambient: {ambientName}");
    }

    public void StopAmbient(float fadeDuration = 1f)
    {
        StartCoroutine(FadeOutAmbient(fadeDuration));
    }

    private IEnumerator FadeOutAmbient(float duration)
    {
        float startVolume = ambientSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            ambientSource.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        ambientSource.Stop();
        ambientSource.volume = startVolume;
    }

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);

        if (audioMixer != null)
        {
            // Convert to decibels (logarithmic)
            float db = volume > 0.001f ? Mathf.Log10(volume) * 20 : -80f;
            audioMixer.SetFloat(masterVolumeParameter, db);
        }

        ApplyVolumes();
        SaveVolumePreferences();
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);

        if (audioMixer != null)
        {
            float db = volume > 0.001f ? Mathf.Log10(volume) * 20 : -80f;
            audioMixer.SetFloat(sfxVolumeParameter, db);
        }

        SaveVolumePreferences();
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);

        if (audioMixer != null)
        {
            float db = volume > 0.001f ? Mathf.Log10(volume) * 20 : -80f;
            audioMixer.SetFloat(musicVolumeParameter, db);
        }

        if (!isCrossfading)
            currentMusicSource.volume = GetCurrentMusicVolume();

        ambientSource.volume = GetCurrentAmbientVolume();
        SaveVolumePreferences();
    }

    private void ApplyVolumes()
    {
        if (!isCrossfading)
            currentMusicSource.volume = GetCurrentMusicVolume();

        ambientSource.volume = GetCurrentAmbientVolume();
    }

    private float GetCurrentMusicVolume()
    {
        SoundClip currentClip = Array.Find(musicTracks, m => m.clip == currentMusicSource.clip);
        float clipVolume = currentClip?.volume ?? 1f;
        return clipVolume * musicVolume * masterVolume;
    }

    private float GetCurrentAmbientVolume()
    {
        SoundClip currentClip = Array.Find(ambientSounds, a => a.clip == ambientSource.clip);
        float clipVolume = currentClip?.volume ?? 1f;
        return clipVolume * musicVolume * masterVolume;
    }
    private void LoadVolumePreferences()
    {
        masterVolume = PlayerPrefs.GetFloat("MasterVolume", defaultMasterVolume);
        sfxVolume = PlayerPrefs.GetFloat("SFXVolume", defaultSFXVolume);
        musicVolume = PlayerPrefs.GetFloat("MusicVolume", defaultMusicVolume);

        SetMasterVolume(masterVolume);
        SetSFXVolume(sfxVolume);
        SetMusicVolume(musicVolume);

        if (debugMode)
            Debug.Log($"Loaded volume preferences: Master={masterVolume:F2}, SFX={sfxVolume:F2}, Music={musicVolume:F2}");
    }

    private void SaveVolumePreferences()
    {
        PlayerPrefs.SetFloat("MasterVolume", masterVolume);
        PlayerPrefs.SetFloat("SFXVolume", sfxVolume);
        PlayerPrefs.SetFloat("MusicVolume", musicVolume);
        PlayerPrefs.Save();

        if (debugMode)
            Debug.Log($"Saved volume preferences");
    }

    // ========== UTILITY METHODS ==========

    /// <summary>
    /// Play a random sound from a list
    /// </summary>
    public void PlayRandomSFX(string[] soundNames)
    {
        if (soundNames == null || soundNames.Length == 0) return;
        string randomName = soundNames[UnityEngine.Random.Range(0, soundNames.Length)];
        PlaySFX(randomName);
    }

    /// <summary>
    /// Check if a sound exists
    /// </summary>
    public bool SoundExists(string soundName)
    {
        return Array.Exists(soundEffects, s => s.name == soundName);
    }

    /// <summary>
    /// Stop all sounds
    /// </summary>
    public void StopAllSounds()
    {
        // Stop music
        currentMusicSource.Stop();
        musicSource2.Stop();

        // Stop ambient
        ambientSource.Stop();

        // Stop all SFX
        foreach (var source in sfxPool)
        {
            if (source.isPlaying)
                source.Stop();
        }
    }

    // Called when the object is destroyed
    void OnDestroy()
    {
        SaveVolumePreferences();
    }
}