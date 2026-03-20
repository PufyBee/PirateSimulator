using UnityEngine;

/// <summary>
/// SHIP VISUAL EFFECTS - Wakes and Radar Ping
/// 
/// Adds:
/// - Wake trail behind moving ships (fading white line)
/// - Radar ping effect when detecting targets
/// 
/// SETUP:
/// 1. Add this component to your ship prefab(s)
/// 2. Or add via code in ShipSpawner after instantiating
/// 
/// Works automatically - no configuration needed.
/// </summary>
public class ShipVisualEffects : MonoBehaviour
{
    [Header("=== WAKE SETTINGS ===")]
    public bool enableWake = true;
    public float wakeTime = 3f;
    public float wakeStartWidth = 0.25f;
    public float wakeEndWidth = 0.02f;
    public Color wakeColor = new Color(1f, 1f, 1f, 0.6f);

    [Header("=== RADAR PING SETTINGS ===")]
    public bool enableRadarPing = true;
    public float pingCooldown = 2f;
    public float pingExpandTime = 0.5f;
    public float pingMaxRadius = 2f;
    public Color pingColor = new Color(1f, 1f, 0f, 0.5f);

    // Components
    private TrailRenderer wakeTrail;
    private ShipController controller;
    private ShipBehavior behavior;

    // Ping state
    private float lastPingTime = -999f;
    private bool wasDetecting = false;

    void Awake()
    {
        controller = GetComponent<ShipController>();
        behavior = GetComponent<ShipBehavior>();
    }

    void Start()
    {
        if (enableWake)
            SetupWakeTrail();
    }

    void Update()
    {
        if (enableRadarPing)
            CheckForDetectionPing();
    }

    // ==================== WAKE TRAIL ====================

    void SetupWakeTrail()
    {
        // Create wake trail
        wakeTrail = gameObject.AddComponent<TrailRenderer>();
        wakeTrail.time = wakeTime;
        wakeTrail.startWidth = wakeStartWidth;
        wakeTrail.endWidth = wakeEndWidth;
        wakeTrail.material = new Material(Shader.Find("Sprites/Default"));
        wakeTrail.sortingOrder = -1; // Behind ships
        
        // Set wake color based on ship type - MORE VISIBLE
        Color startColor = wakeColor;
        Color endColor = new Color(wakeColor.r, wakeColor.g, wakeColor.b, 0f);

        if (controller != null && controller.Data != null)
        {
            switch (controller.Data.type)
            {
                case ShipType.Cargo:
                    // Bright white/cyan wake for merchants
                    startColor = new Color(0.9f, 0.95f, 1f, 0.7f);
                    break;
                case ShipType.Pirate:
                    // Dark red/gray wake for pirates
                    startColor = new Color(0.5f, 0.3f, 0.3f, 0.6f);
                    break;
                case ShipType.Security:
                    // Bright blue wake for security
                    startColor = new Color(0.5f, 0.7f, 1f, 0.7f);
                    break;
            }
            endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);
        }

        wakeTrail.startColor = startColor;
        wakeTrail.endColor = endColor;

        // Make sure sorting order is visible
        wakeTrail.sortingOrder = 10;

        // Curve for nice fade
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0f, 1f);
        curve.AddKey(1f, 0f);
        wakeTrail.widthCurve = curve;
    }

    // ==================== RADAR PING ====================

    void CheckForDetectionPing()
    {
        if (behavior == null || controller == null || controller.Data == null) return;

        // Only pirates and security do radar pings
        if (controller.Data.type == ShipType.Cargo) return;

        // Check if currently detecting/chasing
        bool isDetecting = behavior.enabled && 
            (GetBehaviorState() == "Chasing" || 
             GetBehaviorState() == "Capturing" ||
             GetBehaviorState() == "Responding");

        // Trigger ping on first detection
        if (isDetecting && !wasDetecting)
        {
            if (Time.time - lastPingTime > pingCooldown)
            {
                TriggerRadarPing();
                lastPingTime = Time.time;
            }
        }

        wasDetecting = isDetecting;
    }

    string GetBehaviorState()
    {
        // Use reflection or just check the behavior's public state
        // For simplicity, we'll trigger ping when behavior changes target
        try
        {
            var field = typeof(ShipBehavior).GetField("currentState", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                var state = field.GetValue(behavior);
                return state.ToString();
            }
        }
        catch { }
        return "";
    }

    void TriggerRadarPing()
    {
        StartCoroutine(RadarPingEffect());
    }

    System.Collections.IEnumerator RadarPingEffect()
    {
        // Create ping object
        GameObject pingObj = new GameObject("RadarPing");
        pingObj.transform.position = transform.position;

        // Create expanding circle sprite
        SpriteRenderer sr = pingObj.AddComponent<SpriteRenderer>();
        sr.sprite = CreateRingSprite();
        sr.sortingOrder = 5;

        // Set color based on ship type
        Color baseColor = pingColor;
        if (controller != null && controller.Data != null)
        {
            switch (controller.Data.type)
            {
                case ShipType.Pirate:
                    baseColor = new Color(1f, 0.3f, 0.3f, 0.6f); // Red for pirates
                    break;
                case ShipType.Security:
                    baseColor = new Color(0.3f, 1f, 0.5f, 0.6f); // Green for security
                    break;
            }
        }
        sr.color = baseColor;

        // Animate expansion
        float elapsed = 0f;
        Vector3 startScale = Vector3.one * 0.1f;
        Vector3 endScale = Vector3.one * pingMaxRadius * 2f;

        while (elapsed < pingExpandTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / pingExpandTime;

            // Ease out
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            pingObj.transform.localScale = Vector3.Lerp(startScale, endScale, eased);
            pingObj.transform.position = transform.position; // Follow ship

            // Fade out
            Color c = baseColor;
            c.a = baseColor.a * (1f - t);
            sr.color = c;

            yield return null;
        }

        Destroy(pingObj);
    }

    // ==================== SPRITE GENERATION ====================

    Sprite CreateRingSprite()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];

        Vector2 center = new Vector2(size / 2f, size / 2f);
        float outerRadius = size / 2f - 1;
        float innerRadius = outerRadius - 3;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist <= outerRadius && dist >= innerRadius)
                {
                    // Soft edge
                    float edgeDist = Mathf.Min(dist - innerRadius, outerRadius - dist);
                    float alpha = Mathf.Clamp01(edgeDist / 1.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
                else
                {
                    pixels[y * size + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    // ==================== PUBLIC METHODS ====================

    /// <summary>
    /// Manually trigger a radar ping (e.g., when detecting a target)
    /// </summary>
    public void DoPing()
    {
        if (enableRadarPing && Time.time - lastPingTime > pingCooldown)
        {
            TriggerRadarPing();
            lastPingTime = Time.time;
        }
    }

    /// <summary>
    /// Clear the wake trail (e.g., on teleport)
    /// </summary>
    public void ClearWake()
    {
        if (wakeTrail != null)
            wakeTrail.Clear();
    }
}