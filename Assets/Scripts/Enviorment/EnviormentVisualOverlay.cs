using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Environment Visual Overlay
/// 
/// Creates visual effects for Time of Day and Weather that work
/// independently of the map. Uses a full-screen overlay image.
/// 
/// WEATHER CONDITIONS:
/// - Clear: No effects
/// - Foggy: Wind particles, slight haze
/// - Stormy: Rain particles, dark overlay
/// - Thunderstorm: Heavy rain + Lightning flashes
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
    public Color morningTint = new Color(1f, 0.92f, 0.8f, 0.03f);
    public Color noonTint = new Color(1f, 1f, 0.95f, 0.01f);
    public Color eveningTint = new Color(1f, 0.7f, 0.5f, 0.06f);
    public Color nightTint = new Color(0.5f, 0.5f, 0.7f, 0.08f);

    [Header("=== WEATHER COLORS ===")]
    public Color clearTint = new Color(1f, 1f, 1f, 0f);
    public Color foggyTint = new Color(0.9f, 0.9f, 0.95f, 0.08f);
    public Color stormyTint = new Color(0.6f, 0.6f, 0.65f, 0.08f);
    public Color thunderstormTint = new Color(0.4f, 0.4f, 0.5f, 0.12f);

    [Header("=== TRANSITION ===")]
    public float transitionSpeed = 2f;

    [Header("=== PARTICLE EFFECTS ===")]
    public bool enableRainEffect = true;
    public bool enableWindEffect = true;
    public GameObject rainParticlePrefab;
    public GameObject windParticlePrefab;
    private GameObject activeRainEffect;
    private GameObject activeWindEffect;

    [Header("=== LIGHTNING (Thunderstorm) ===")]
    public bool enableLightning = true;
    public float lightningMinInterval = 2f;
    public float lightningMaxInterval = 6f;
    public float lightningIntensity = 0.7f;
    private float nextLightningTime;
    private bool isThunderstorm = false;

    [Header("=== VIGNETTE (NIGHT) ===")]
    public bool enableNightVignette = false;
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
        if (EnvironmentSettings.Instance != null)
        {
            SetTimeOfDay(EnvironmentSettings.Instance.timeOfDay);
            SetWeather(EnvironmentSettings.Instance.weather);
        }

        currentOverlayColor = targetOverlayColor;
        ApplyOverlay();
        
        nextLightningTime = Time.time + Random.Range(lightningMinInterval, lightningMaxInterval);
    }

    private void Update()
    {
        if (currentOverlayColor != targetOverlayColor)
        {
            currentOverlayColor = Color.Lerp(currentOverlayColor, targetOverlayColor, Time.deltaTime * transitionSpeed);
            ApplyOverlay();
        }
        
        if (isThunderstorm && enableLightning)
        {
            UpdateLightning();
        }
    }

    private void UpdateLightning()
    {
        if (Time.time >= nextLightningTime)
        {
            StartCoroutine(LightningFlash());
            nextLightningTime = Time.time + Random.Range(lightningMinInterval, lightningMaxInterval);
        }
    }

    private System.Collections.IEnumerator LightningFlash()
    {
        if (overlayImage == null) yield break;
        
        Color originalColor = currentOverlayColor;
        Color flashColor = new Color(1f, 1f, 1f, lightningIntensity);
        
        // Flash
        overlayImage.color = flashColor;
        yield return new WaitForSeconds(0.05f);
        
        // Fade back
        float fadeTime = 0.15f;
        float elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            overlayImage.color = Color.Lerp(flashColor, originalColor, elapsed / fadeTime);
            yield return null;
        }
        
        // Sometimes double flash
        if (Random.value < 0.3f)
        {
            yield return new WaitForSeconds(0.1f);
            overlayImage.color = new Color(1f, 1f, 1f, lightningIntensity * 0.6f);
            yield return new WaitForSeconds(0.03f);
            
            elapsed = 0f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                overlayImage.color = Color.Lerp(flashColor, originalColor, elapsed / fadeTime);
                yield return null;
            }
        }
        
        overlayImage.color = originalColor;
    }

    private void CreateOverlayCanvas()
    {
        GameObject canvasObj = new GameObject("EnvironmentOverlayCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = Camera.main;
        canvas.sortingOrder = 1;
        canvas.planeDistance = 5f;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject imageObj = new GameObject("OverlayImage");
        imageObj.transform.SetParent(canvasObj.transform, false);

        overlayImage = imageObj.AddComponent<Image>();
        overlayImage.color = Color.clear;
        overlayImage.raycastTarget = false;

        RectTransform rt = overlayImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        if (enableNightVignette)
        {
            CreateVignette(canvasObj.transform);
        }

        Debug.Log("EnvironmentVisualOverlay: Auto-created overlay canvas");
    }

    private void CreateVignette(Transform parent)
    {
        GameObject vignetteObj = new GameObject("Vignette");
        vignetteObj.transform.SetParent(parent, false);

        vignetteImage = vignetteObj.AddComponent<Image>();
        vignetteImage.raycastTarget = false;

        Texture2D vignetteTex = CreateVignetteTexture(256);
        vignetteImage.sprite = Sprite.Create(vignetteTex, new Rect(0, 0, 256, 256), new Vector2(0.5f, 0.5f));
        vignetteImage.color = new Color(0, 0, 0, 0);

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
                float alpha = Mathf.Pow(t, 2f);
                tex.SetPixel(x, y, new Color(0, 0, 0, alpha));
            }
        }

        tex.Apply();
        return tex;
    }

    public void SetTimeOfDay(TimeOfDay time)
    {
        currentTimeOfDay = time;
        UpdateTargetColor();

        if (vignetteImage != null)
        {
            float vignetteAlpha = (time == TimeOfDay.Night) ? 0.3f : 
                                  (time == TimeOfDay.Evening) ? 0.2f : 0f;
            vignetteImage.color = new Color(0, 0, 0, vignetteAlpha);
        }

        Debug.Log($"EnvironmentVisualOverlay: Time set to {time}");
    }

    public void SetWeather(Weather weather)
    {
        currentWeather = weather;
        UpdateTargetColor();

        isThunderstorm = (weather == Weather.Thunderstorm);

        // Wind for Foggy
        if (enableWindEffect)
        {
            if (weather == Weather.Foggy)
                StartWind();
            else
                StopWind();
        }

        // Rain for Stormy and Thunderstorm
        if (enableRainEffect)
        {
            if (weather == Weather.Stormy || weather == Weather.Thunderstorm)
                StartRain();
            else
                StopRain();
        }

        Debug.Log($"EnvironmentVisualOverlay: Weather set to {weather}");
    }

    private void UpdateTargetColor()
    {
        Color timeColor = GetTimeOfDayColor(currentTimeOfDay);
        Color weatherColor = GetWeatherColor(currentWeather);

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
            case Weather.Thunderstorm: return thunderstormTint;
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

    private void StartWind()
    {
        if (activeWindEffect != null) return;

        if (windParticlePrefab != null)
            activeWindEffect = Instantiate(windParticlePrefab);
        else
            activeWindEffect = CreateProceduralWind();
    }

    private void StopWind()
    {
        if (activeWindEffect != null)
        {
            Destroy(activeWindEffect);
            activeWindEffect = null;
        }   
    }
    
    private GameObject CreateProceduralWind()
    {
        GameObject windObj = new GameObject("WindEffect");
        windObj.transform.position = new Vector3(0, 0, -5);

        ParticleSystem ps = windObj.AddComponent<ParticleSystem>();
        
        float camHeight = 250f;
        float camWidth = camHeight * 1.7f;
        
        if (Camera.main != null && Camera.main.orthographic)
        {
            camHeight = Camera.main.orthographicSize * 2f;
            camWidth = camHeight * Camera.main.aspect;
        }
        
        var main = ps.main;
        main.loop = true;
        main.startLifetime = 3f;
        main.startSpeed = 0f;
        main.startSize3D = false;
        main.startSize = 0.5f;
        main.startColor = new Color(0.99f, 0.99f, 0.99f, 0.5f);
        main.maxParticles = 5000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.rateOverTime = 30f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Rectangle;
        shape.position = new Vector3(-400f, 25f, 0f);
        shape.scale = new Vector3(camWidth / 4f, camHeight + 50f, 1f);
        shape.rotation = new Vector3(0, 0, 0);

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(200f, 250f);
        velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);   
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f); 
        velocity.orbitalZ = 0.14f;
        velocity.speedModifier = 1.6f;

        var renderer = windObj.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = 101;
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 0f;
        renderer.velocityScale = 0.1f;
        
        Material windMat = new Material(Shader.Find("Sprites/Default"));
        windMat.color = new Color(1f, 1f, 1f, 0.4f);
        renderer.material = windMat;

        windObj.AddComponent<RainFollowCamera>();
        
        return windObj;
    }

    private void StartRain()
    {
        if (activeRainEffect != null) return;

        if (rainParticlePrefab != null)
            activeRainEffect = Instantiate(rainParticlePrefab);
        else
            activeRainEffect = CreateProceduralRain();
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
        rainObj.transform.position = new Vector3(0, 0, -5);

        ParticleSystem ps = rainObj.AddComponent<ParticleSystem>();
        
        float camHeight = 250f;
        float camWidth = camHeight * 1.7f;
        
        if (Camera.main != null && Camera.main.orthographic)
        {
            camHeight = Camera.main.orthographicSize * 2f;
            camWidth = camHeight * Camera.main.aspect;
        }
        
        bool isHeavyRain = (currentWeather == Weather.Thunderstorm);
        
        var main = ps.main;
        main.loop = true;
        main.startLifetime = isHeavyRain ? 0.3f : 0.4f;
        main.startSpeed = 0f;
        main.startSize3D = false;
        main.startSize = new ParticleSystem.MinMaxCurve(isHeavyRain ? 1f : 0.5f, isHeavyRain ? 4f : 3f);
        main.startColor = new Color(0.68f, 0.83f, 0.82f, isHeavyRain ? 0.3f : 0.2f);
        main.maxParticles = isHeavyRain ? 8000 : 5000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.rateOverTime = isHeavyRain ? 300f : 150f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Rectangle;
        shape.position = new Vector3(0f, 65f, 0f);
        shape.scale = new Vector3(camWidth + 50f, camHeight + 50f, 1f);
        shape.rotation = new Vector3(0, 0, 0);

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = isHeavyRain ? 25f : 15f;
        velocity.y = isHeavyRain ? -280f : -200f;
        velocity.z = 0f;
        velocity.speedModifier = 1.6f;

        var renderer = rainObj.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = 100;
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 0f;
        renderer.velocityScale = 0.1f;
        
        Material rainMat = new Material(Shader.Find("Sprites/Default"));
        rainMat.color = new Color(0.7f, 0.8f, 1f, 0.4f);
        renderer.material = rainMat;

        rainObj.AddComponent<RainFollowCamera>();
        
        Debug.Log($"Rain created - Heavy: {isHeavyRain}");
        
        return rainObj;
    }

    public float GetVisibilityMultiplier()
    {
        float visibility = 1f;

        switch (currentTimeOfDay)
        {
            case TimeOfDay.Night: visibility *= 0.5f; break;
            case TimeOfDay.Evening: visibility *= 0.8f; break;
        }

        switch (currentWeather)
        {
            case Weather.Foggy: visibility *= 0.5f; break;
            case Weather.Stormy: visibility *= 0.7f; break;
            case Weather.Thunderstorm: visibility *= 0.6f; break;
        }

        return visibility;
    }

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
            transform.position = new Vector3(camPos.x, camPos.y, -5f);
        }
    }
}