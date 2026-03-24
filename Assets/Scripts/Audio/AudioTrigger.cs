using UnityEngine;

public class AudioTrigger : MonoBehaviour
{
    [Header("=== TRIGGER SETTINGS ===")]
    public TriggerType triggerType = TriggerType.OnEnable;
    public string[] soundNames;
    public bool randomizeSound = true;
    public float minDelay = 0f;
    public float maxDelay = 0f;

    [Header("=== 3D SOUND ===")]
    public bool use3D = false;
    public float spatialBlend = 1f;

    public enum TriggerType
    {
        OnEnable,
        OnDisable,
        OnStart,
        OnDestroy,
        OnMouseDown,
        OnMouseEnter,
        Manual
    }

    void Start()
    {
        if (triggerType == TriggerType.OnStart)
            Play();
    }

    void OnEnable()
    {
        if (triggerType == TriggerType.OnEnable)
            Play();
    }

    void OnDisable()
    {
        if (triggerType == TriggerType.OnDisable)
            Play();
    }

    void OnDestroy()
    {
        if (triggerType == TriggerType.OnDestroy && AudioManager.Instance != null)
            Play();
    }

    void OnMouseDown()
    {
        if (triggerType == TriggerType.OnMouseDown)
            Play();
    }

    void OnMouseEnter()
    {
        if (triggerType == TriggerType.OnMouseEnter)
            Play();
    }

    public void Play()
    {
        if (AudioManager.Instance == null || soundNames == null || soundNames.Length == 0)
            return;

        if (maxDelay > 0f)
        {
            float delay = Random.Range(minDelay, maxDelay);
            Invoke(nameof(PlayDelayed), delay);
            return;
        }

        PlayDelayed();
    }

    private void PlayDelayed()
    {
        if (randomizeSound)
        {
            string randomName = soundNames[Random.Range(0, soundNames.Length)];

            if (use3D)
                AudioManager.Instance.PlayOneShot(randomName, transform.position);
            else
                AudioManager.Instance.PlayOneShot(randomName);
        }
        else
        {
            foreach (string name in soundNames)
            {
                if (use3D)
                    AudioManager.Instance.PlayOneShot(name, transform.position);
                else
                    AudioManager.Instance.PlayOneShot(name);
            }
        }
    }
}
