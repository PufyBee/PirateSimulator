using UnityEngine;

/// <summary>
/// Ship Light (Sprite-based) - Fresh version with safety limits
/// </summary>
public class ShipLight : MonoBehaviour
{
    [Header("=== LIGHT APPEARANCE ===")]
    public Color merchantLightColor = new Color(1f, 0.85f, 0.4f, 0.8f);
    public Color pirateLightColor = new Color(1f, 0.2f, 0.1f, 0.8f);
    public Color securityLightColor = new Color(0.2f, 0.4f, 0.8f, 0.6f);

    [Header("=== SIZE (will be clamped for safety) ===")]
    [Range(1f, 50f)]
    public float glowSize = 15f;
    [Range(1f, 20f)]
    public float innerGlowSize = 6f;

    [Header("=== FLICKER EFFECT ===")]
    public bool enableFlicker = true;
    public float flickerSpeed = 5f;
    [Range(0f, 0.5f)]
    public float flickerAmount = 0.2f;

    // Internal
    private ShipController shipController;
    private GameObject glowObject;
    private GameObject innerGlowObject;
    private SpriteRenderer glowRenderer;
    private SpriteRenderer innerGlowRenderer;
    private bool isLightOn = false;
    private bool isInitialized = false;
    private float flickerOffset;
    private float scaleCompensation = 1f;
    private Color baseColor;

    void Start()
    {
        shipController = GetComponent<ShipController>();
        flickerOffset = Random.Range(0f, 100f);
        
        // Delay to ensure ship data is ready
        Invoke(nameof(Initialize), 0.15f);
    }

    void Initialize()
    {
        if (isInitialized) return;
        
        if (shipController == null || shipController.Data == null)
        {
            // Try again
            Invoke(nameof(Initialize), 0.1f);
            return;
        }

        // Safety clamp sizes
        glowSize = Mathf.Clamp(glowSize, 1f, 50f);
        innerGlowSize = Mathf.Clamp(innerGlowSize, 1f, 20f);

        CreateGlowSprites();
        SetLightColor();
        isInitialized = true;
        
        // Initial state check
        UpdateLightState();
    }

    void Update()
    {
        if (!isInitialized) return;
        
        UpdateLightState();

        if (isLightOn && enableFlicker)
        {
            ApplyFlicker();
        }
    }

    void CreateGlowSprites()
    {
        // Get parent scale to compensate
        Vector3 parentScale = transform.lossyScale;
        float scaleCompensation = 1f / Mathf.Max(parentScale.x, parentScale.y, 0.001f);
        
        // Outer glow
        glowObject = new GameObject("ShipGlow");
        glowObject.transform.SetParent(transform);
        glowObject.transform.localPosition = Vector3.zero;

        glowRenderer = glowObject.AddComponent<SpriteRenderer>();
        glowRenderer.sprite = CreateCircleSprite(64);
        glowRenderer.sortingOrder = 10;
        glowRenderer.color = Color.clear;  // Start invisible
        
        // Apply size WITH scale compensation so it's consistent regardless of ship size
        glowObject.transform.localScale = Vector3.one * glowSize * scaleCompensation;
        glowObject.SetActive(false);

        // Inner core
        innerGlowObject = new GameObject("ShipGlowCore");
        innerGlowObject.transform.SetParent(transform);
        innerGlowObject.transform.localPosition = Vector3.zero;

        innerGlowRenderer = innerGlowObject.AddComponent<SpriteRenderer>();
        innerGlowRenderer.sprite = CreateCircleSprite(32);
        innerGlowRenderer.sortingOrder = 11;
        innerGlowRenderer.color = Color.clear;  // Start invisible
        
        // Apply size WITH scale compensation
        innerGlowObject.transform.localScale = Vector3.one * innerGlowSize * scaleCompensation;
        innerGlowObject.SetActive(false);
        
        // Store compensation for flicker
        this.scaleCompensation = scaleCompensation;
    }

    Sprite CreateCircleSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float t = dist / radius;
                
                // Soft circle falloff
                float alpha = Mathf.Clamp01(1f - t);
                alpha = alpha * alpha;  // Quadratic falloff
                
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * 0.7f));
            }
        }

        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    void SetLightColor()
    {
        if (shipController == null || shipController.Data == null)
        {
            baseColor = merchantLightColor;
            return;
        }

        switch (shipController.Data.type)
        {
            case ShipType.Cargo:
                baseColor = merchantLightColor;
                break;
            case ShipType.Pirate:
                baseColor = pirateLightColor;
                break;
            case ShipType.Security:
                baseColor = securityLightColor;
                break;
            default:
                baseColor = merchantLightColor;
                break;
        }

        ApplyColors();
    }

    void ApplyColors()
    {
        if (glowRenderer != null)
            glowRenderer.color = baseColor;
            
        if (innerGlowRenderer != null)
        {
            // Core uses SAME color and alpha as outer glow, not separate
            innerGlowRenderer.color = baseColor;
        }
    }

    void UpdateLightState()
    {
        bool shouldBeOn = ShouldLightBeOn();

        if (shouldBeOn != isLightOn)
        {
            isLightOn = shouldBeOn;
            
            if (glowObject != null)
                glowObject.SetActive(isLightOn);
            if (innerGlowObject != null)
                innerGlowObject.SetActive(isLightOn);

            if (isLightOn)
                ApplyColors();
        }
    }

    void ApplyFlicker()
    {
        if (glowRenderer == null || !isLightOn) return;

        float noise = Mathf.PerlinNoise(Time.time * flickerSpeed + flickerOffset, flickerOffset);
        float multiplier = 1f + (noise - 0.5f) * flickerAmount;

        Color flicker = baseColor;
        flicker.a = Mathf.Clamp01(baseColor.a * multiplier);
        glowRenderer.color = flicker;
    }

    bool ShouldLightBeOn()
    {
        if (EnvironmentSettings.Instance == null)
            return false;

        var time = EnvironmentSettings.Instance.timeOfDay;
        var weather = EnvironmentSettings.Instance.weather;

        return time == TimeOfDay.Night || 
               time == TimeOfDay.Evening || 
               weather == Weather.Stormy || 
               weather == Weather.Foggy;
    }

    void OnDestroy()
    {
        // Cleanup textures
        if (glowRenderer != null && glowRenderer.sprite != null)
        {
            if (glowRenderer.sprite.texture != null)
                Destroy(glowRenderer.sprite.texture);
            Destroy(glowRenderer.sprite);
        }
        if (innerGlowRenderer != null && innerGlowRenderer.sprite != null)
        {
            if (innerGlowRenderer.sprite.texture != null)
                Destroy(innerGlowRenderer.sprite.texture);
            Destroy(innerGlowRenderer.sprite);
        }
    }
}