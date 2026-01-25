using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Environment Visual Overlay
/// 
/// Creates visual effects for Time of Day and Weather that work
/// independently of the map. Uses a full-screen overlay image.
/// 
/// SETUP:
/// 1. Create a Canvas set to "Screen Space - Camera" 
/// 2. Set Sort Order to 1 (above map, below UI)
/// 3. Create an Image that stretches to fill the canvas
/// 4. Add this script to the Image
/// 5. Reference it from EnvironmentSettings
/// 
/// OR use the auto-setup: Just add this script to any GameObject
/// and check "Auto Create Overlay"
/// </summary>
public class EnvironmentVisualOverlay : MonoBehaviour
{
    public static EnvironmentVisualOverlay Instance { get; private set; }

    [Header("=== AUTO SETUP ===")]
    [Tooltip("Automatically create the overlay canvas and image")]
    public bool autoCreateOverlay = true;

    [Header("=== MANUAL REFERENCES ===")]
    [Tooltip("If not auto-creating, assign the overlay image here")]
    public Image overlayImage;

    [Header("=== TIME OF DAY COLORS ===")]
    public Color morningTint = new Color(1f, 0.92f, 0.8f, 0.03f);      // Barely visible warm
    public Color noonTint = new Color(1f, 1f, 0.95f, 0.01f);           // Almost invisible
    public Color eveningTint = new Color(1f, 0.7f, 0.5f, 0.06f);       // Subtle sunset
    public Color nightTint = new Color(0.5f, 0.5f, 0.7f, 0.08f);       // VERY subtle night

    [Header("=== WEATHER COLORS ===")]
    public Color clearTint = new Color(1f, 1f, 1f, 0f);                // No effect
    public Color foggyTint = new Color(0.9f, 0.9f, 0.95f, 0.08f);      // Very subtle fog
    public Color stormyTint = new Color(0.6f, 0.6f, 0.65f, 0.08f);     // Very subtle storm
    public Color calmTint = new Color(1f, 0.97f, 0.85f, 0.02f);        // Barely visible golden

    [Header("=== TRANSITION ===")]
    public float transitionSpeed = 2f;

    [Header("=== PARTICLE EFFECTS ===")]
    public bool enableRainEffect = true;
    public GameObject rainParticlePrefab;
    private GameObject activeRainEffect;

    [Header("=== VIGNETTE (NIGHT) ===")]
    public bool enableNightVignette = false;  // Disabled by default - too dark
    public Image vignetteImage;

    // Current state
    private Color currentOverlayColor;
    private Color targetOverlayColor;
    private TimeOfDay currentTimeOfDay = TimeOfDay.Morning;
    private Weather currentWeather = Weather.Clear;

    private void Awake()
    {
        Instance = this;

        if (autoCreateOverlay && overlayImage == null)
        {
            CreateOverlayCanvas();
        }
    }

    private void Start()
    {
        // Initialize to current settings
        if (EnvironmentSettings.Instance != null)
        {
            SetTimeOfDay(EnvironmentSettings.Instance.timeOfDay);
            SetWeather(EnvironmentSettings.Instance.weather);
        }

        currentOverlayColor = targetOverlayColor;
        ApplyOverlay();
    }

    private void Update()
    {
        // Smooth transition to target color
        if (currentOverlayColor != targetOverlayColor)
        {
            currentOverlayColor = Color.Lerp(currentOverlayColor, targetOverlayColor, Time.deltaTime * transitionSpeed);
            ApplyOverlay();
        }
    }

    /// <summary>
    /// Auto-create the overlay canvas and image
    /// </summary>
    private void CreateOverlayCanvas()
    {
        // Create Canvas
        GameObject canvasObj = new GameObject("EnvironmentOverlayCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = Camera.main;
        canvas.sortingOrder = 1; // Above map (0), below UI (10+)
        canvas.planeDistance = 5f;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Create overlay image
        GameObject imageObj = new GameObject("OverlayImage");
        imageObj.transform.SetParent(canvasObj.transform, false);

        overlayImage = imageObj.AddComponent<Image>();
        overlayImage.color = Color.clear;
        overlayImage.raycastTarget = false; // Don't block clicks!

        // Stretch to fill
        RectTransform rt = overlayImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Create vignette for night effect
        if (enableNightVignette)
        {
            CreateVignette(canvasObj.transform);
        }

        Debug.Log("EnvironmentVisualOverlay: Auto-created overlay canvas");
    }

    /// <summary>
    /// Create a vignette effect for night time
    /// </summary>
    private void CreateVignette(Transform parent)
    {
        GameObject vignetteObj = new GameObject("Vignette");
        vignetteObj.transform.SetParent(parent, false);

        vignetteImage = vignetteObj.AddComponent<Image>();
        vignetteImage.raycastTarget = false;

        // Create a radial gradient texture for vignette
        Texture2D vignetteTex = CreateVignetteTexture(256);
        vignetteImage.sprite = Sprite.Create(vignetteTex, new Rect(0, 0, 256, 256), new Vector2(0.5f, 0.5f));
        vignetteImage.color = new Color(0, 0, 0, 0); // Start invisible

        // Stretch to fill
        RectTransform rt = vignetteImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private Texture2D CreateVignetteTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float maxDist = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float t = Mathf.Clamp01(dist / maxDist);
                
                // Smooth falloff - more transparent in center, darker at edges
                float alpha = Mathf.Pow(t, 2f); // Quadratic falloff
                tex.SetPixel(x, y, new Color(0, 0, 0, alpha));
            }
        }

        tex.Apply();
        return tex;
    }

    /// <summary>
    /// Set the time of day visual effect
    /// </summary>
    public void SetTimeOfDay(TimeOfDay time)
    {
        currentTimeOfDay = time;
        UpdateTargetColor();

        // Update vignette for night
        if (vignetteImage != null)
        {
            float vignetteAlpha = (time == TimeOfDay.Night) ? 0.6f : 
                                  (time == TimeOfDay.Evening) ? 0.2f : 0f;
            vignetteImage.color = new Color(0, 0, 0, vignetteAlpha);
        }

        Debug.Log($"EnvironmentVisualOverlay: Time set to {time}");
    }

    /// <summary>
    /// Set the weather visual effect
    /// </summary>
    public void SetWeather(Weather weather)
    {
        currentWeather = weather;
        UpdateTargetColor();

        // Handle rain particles
        if (enableRainEffect)
        {
            if (weather == Weather.Stormy)
            {
                StartRain();
            }
            else
            {
                StopRain();
            }
        }

        Debug.Log($"EnvironmentVisualOverlay: Weather set to {weather}");
    }

    /// <summary>
    /// Calculate the combined overlay color from time + weather
    /// </summary>
    private void UpdateTargetColor()
    {
        // Get base colors
        Color timeColor = GetTimeOfDayColor(currentTimeOfDay);
        Color weatherColor = GetWeatherColor(currentWeather);

        // Blend them together
        // Use additive blending for the RGB, average for alpha
        targetOverlayColor = new Color(
            Mathf.Clamp01(timeColor.r * weatherColor.r),
            Mathf.Clamp01(timeColor.g * weatherColor.g),
            Mathf.Clamp01(timeColor.b * weatherColor.b),
            Mathf.Clamp01(timeColor.a + weatherColor.a)
        );
    }

    private Color GetTimeOfDayColor(TimeOfDay time)
    {
        switch (time)
        {
            case TimeOfDay.Morning: return morningTint;
            case TimeOfDay.Noon: return noonTint;
            case TimeOfDay.Evening: return eveningTint;
            case TimeOfDay.Night: return nightTint;
            default: return clearTint;
        }
    }

    private Color GetWeatherColor(Weather weather)
    {
        switch (weather)
        {
            case Weather.Clear: return clearTint;
            case Weather.Foggy: return foggyTint;
            case Weather.Stormy: return stormyTint;
            case Weather.Calm: return calmTint;
            default: return clearTint;
        }
    }

    private void ApplyOverlay()
    {
        if (overlayImage != null)
        {
            overlayImage.color = currentOverlayColor;
        }
    }

    // ===== RAIN EFFECT =====

    private void StartRain()
    {
        if (activeRainEffect != null) return;

        if (rainParticlePrefab != null)
        {
            activeRainEffect = Instantiate(rainParticlePrefab);
        }
        else
        {
            // Create simple rain effect procedurally
            activeRainEffect = CreateProceduralRain();
        }
    }

    private void StopRain()
    {
        if (activeRainEffect != null)
        {
            Destroy(activeRainEffect);
            activeRainEffect = null;
        }
    }

    private GameObject CreateProceduralRain()
    {
        GameObject rainObj = new GameObject("RainEffect");
        
        // Position at Z = -5 so it's in front of the map
        rainObj.transform.position = new Vector3(0, 0, -5);

        ParticleSystem ps = rainObj.AddComponent<ParticleSystem>();
        
        // Get camera size to scale rain appropriately
        float camHeight = 250f;  // Approximate for ortho size ~123
        float camWidth = camHeight * 1.7f;  // Approximate aspect ratio
        
        if (Camera.main != null && Camera.main.orthographic)
        {
            camHeight = Camera.main.orthographicSize * 2f;
            camWidth = camHeight * Camera.main.aspect;
        }
        
        // Main module - rain drops for 2D game
        var main = ps.main;
        main.loop = true;
        main.startLifetime = 1.5f;
        main.startSpeed = 0f;  // We use velocity instead
        main.startSize3D = false;
        main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3f);  // MUCH bigger rain drops
        main.startColor = new Color(0.6f, 0.7f, 0.9f, 0.5f);  // Blue-ish rain
        main.maxParticles = 5000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;

        // Emission - lots of rain
        var emission = ps.emission;
        emission.rateOverTime = 3000f;

        // Shape - cover the entire visible camera area
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Rectangle;
        shape.scale = new Vector3(camWidth + 50f, camHeight + 50f, 1f);  // Cover full view + buffer
        shape.rotation = new Vector3(0, 0, 0);

        // Velocity - rain falls DOWN in screen space (negative Y)
        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = 15f;     // Wind - scaled up
        velocity.y = -150f;   // Fall down FAST - scaled for large view
        velocity.z = 0f;

        // Make particles stretch based on velocity (looks like rain streaks)
        var renderer = rainObj.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = 100;  // In front of everything except UI
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 0f;
        renderer.velocityScale = 0.05f;  // Stretch based on speed
        
        // Material
        Material rainMat = new Material(Shader.Find("Sprites/Default"));
        rainMat.color = new Color(0.7f, 0.8f, 1f, 0.4f);
        renderer.material = rainMat;

        // Make rain follow camera
        RainFollowCamera followScript = rainObj.AddComponent<RainFollowCamera>();
        
        Debug.Log($"Rain created - Coverage: {camWidth + 50f} x {camHeight + 50f}");
        
        return rainObj;
    }

    private void CreateRainSplashes(GameObject parent)
    {
        // Skip splashes for 2D - they don't make sense in top-down view
    }

    // ===== PUBLIC UTILITIES =====

    /// <summary>
    /// Get current visibility multiplier (for fog of war effects)
    /// </summary>
    public float GetVisibilityMultiplier()
    {
        float visibility = 1f;

        // Time affects visibility
        switch (currentTimeOfDay)
        {
            case TimeOfDay.Night: visibility *= 0.5f; break;
            case TimeOfDay.Evening: visibility *= 0.8f; break;
        }

        // Weather affects visibility
        switch (currentWeather)
        {
            case Weather.Foggy: visibility *= 0.5f; break;
            case Weather.Stormy: visibility *= 0.7f; break;
        }

        return visibility;
    }

    /// <summary>
    /// Force immediate update (no transition)
    /// </summary>
    public void ForceUpdate()
    {
        if (EnvironmentSettings.Instance != null)
        {
            SetTimeOfDay(EnvironmentSettings.Instance.timeOfDay);
            SetWeather(EnvironmentSettings.Instance.weather);
        }
        currentOverlayColor = targetOverlayColor;
        ApplyOverlay();
    }
}

/// <summary>
/// Helper script to make rain follow the camera (2D version)
/// </summary>
public class RainFollowCamera : MonoBehaviour
{
    private Camera mainCam;

    void Start()
    {
        mainCam = Camera.main;
    }

    void Update()
    {
        if (mainCam != null)
        {
            Vector3 camPos = mainCam.transform.position;
            // Keep rain at camera position but in front (negative Z)
            transform.position = new Vector3(camPos.x, camPos.y, -5f);
        }
    }
}