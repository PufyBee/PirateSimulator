using UnityEngine;

/// <summary>
/// Samples the map texture to determine if a position is water or land.
/// 
/// UPDATED: Now handles dark coastline outlines by treating dark pixels as land.
/// 
/// Logic:
/// 1. If pixel is too dark overall → LAND (catches dark outlines)
/// 2. If green is dominant → LAND (main landmass)
/// 3. If blue is dominant and bright enough → WATER
/// 4. Otherwise → LAND (safe default)
/// </summary>
public class MapColorSampler : MonoBehaviour
{
    public static MapColorSampler Instance { get; private set; }

    [Header("Assign the SpriteRenderer of your map")]
    public SpriteRenderer mapRenderer;

    [Header("=== WATER DETECTION ===")]
    [Tooltip("Water if Blue channel is greater than Green + this offset")]
    public float blueDominanceOffset = 0.05f;

    [Tooltip("Minimum blue brightness to count as water (0-1)")]
    [Range(0f, 1f)]
    public float minBlue = 0.25f;

    [Header("=== LAND DETECTION ===")]
    [Tooltip("If green is dominant by this amount, it's land")]
    public float greenDominanceOffset = 0.03f;

    [Tooltip("Pixels darker than this (overall brightness) are treated as LAND. This catches dark coastline outlines!")]
    [Range(0f, 0.5f)]
    public float darkPixelThreshold = 0.15f;

    [Header("=== COAST BUFFER (Anti-scraping) ===")]
    [Tooltip("Check surrounding points to prevent ships from touching coastlines")]
    public bool useCoastBuffer = true;

    [Tooltip("How far around each point to check (world units). Increase if ships still scrape coasts.")]
    public float coastBufferWorld = 0.20f;

    [Header("=== DEBUG ===")]
    [Tooltip("Log color values when sampling (turn off for performance)")]
    public bool debugLogging = false;

    private Texture2D tex;
    private Bounds worldBounds;
    private bool isInitialized = false;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (mapRenderer == null || mapRenderer.sprite == null)
        {
            Debug.LogError("MapColorSampler: Please assign a SpriteRenderer with a sprite!");
            enabled = false;
            return;
        }

        tex = mapRenderer.sprite.texture;
        worldBounds = mapRenderer.bounds;

        // Check if texture is readable
        try
        {
            tex.GetPixel(0, 0);
        }
        catch (UnityException)
        {
            Debug.LogError("MapColorSampler: Texture is not readable! Go to texture import settings and enable 'Read/Write Enabled'");
            enabled = false;
            return;
        }

        isInitialized = true;
        Debug.Log($"MapColorSampler initialized. Map bounds: {worldBounds}");
    }

    /// <summary>
    /// Check if a world position is navigable water.
    /// Returns true if the ship can sail there.
    /// </summary>
    public bool IsWater(Vector2 worldPos)
    {
        if (!isInitialized || tex == null) return true;

        if (!useCoastBuffer)
            return IsWaterPoint(worldPos);

        // Check center point
        if (!IsWaterPoint(worldPos)) return false;

        // Check surrounding points (prevents coast scraping)
        float r = Mathf.Max(0f, coastBufferWorld);
        if (r <= 0.001f) return true;

        // Cardinal directions
        if (!IsWaterPoint(worldPos + new Vector2(r, 0))) return false;
        if (!IsWaterPoint(worldPos + new Vector2(-r, 0))) return false;
        if (!IsWaterPoint(worldPos + new Vector2(0, r))) return false;
        if (!IsWaterPoint(worldPos + new Vector2(0, -r))) return false;

        return true;
    }

    /// <summary>
    /// Check a single point (no buffer).
    /// </summary>
    private bool IsWaterPoint(Vector2 worldPos)
    {
        // Convert world position to UV coordinates (0-1)
        float u = Mathf.InverseLerp(worldBounds.min.x, worldBounds.max.x, worldPos.x);
        float v = Mathf.InverseLerp(worldBounds.min.y, worldBounds.max.y, worldPos.y);

        // Outside map bounds = treat as water (allows ships to exit)
        if (u < 0f || u > 1f || v < 0f || v > 1f)
            return true;

        // Sample the texture
        Color c = tex.GetPixelBilinear(u, v);

        // === DETECTION LOGIC ===

        // 1. DARK PIXEL CHECK (catches coastline outlines!)
        float brightness = (c.r + c.g + c.b) / 3f;
        if (brightness < darkPixelThreshold)
        {
            if (debugLogging)
                Debug.Log($"LAND (dark): pos={worldPos}, brightness={brightness:F2}, color={c}");
            return false; // Dark = land
        }

        // 2. GREEN DOMINANT = LAND
        bool greenDominant = (c.g > c.b + greenDominanceOffset) && (c.g > c.r + greenDominanceOffset);
        if (greenDominant)
        {
            if (debugLogging)
                Debug.Log($"LAND (green): pos={worldPos}, color={c}");
            return false;
        }

        // 3. BLUE DOMINANT AND BRIGHT ENOUGH = WATER
        bool blueDominant = (c.b > c.g + blueDominanceOffset) && (c.b > c.r + blueDominanceOffset);
        bool blueEnough = c.b >= minBlue;

        if (blueDominant && blueEnough)
        {
            if (debugLogging)
                Debug.Log($"WATER: pos={worldPos}, color={c}");
            return true;
        }

        // 4. DEFAULT = LAND (safer than assuming water)
        if (debugLogging)
            Debug.Log($"LAND (default): pos={worldPos}, color={c}");
        return false;
    }

    /// <summary>
    /// Get the world bounds of the map.
    /// </summary>
    public Bounds GetWorldBounds()
    {
        return worldBounds;
    }

    /// <summary>
    /// Debug helper: Get the color at a world position.
    /// </summary>
    public Color GetColorAtPosition(Vector2 worldPos)
    {
        if (tex == null) return Color.black;

        float u = Mathf.InverseLerp(worldBounds.min.x, worldBounds.max.x, worldPos.x);
        float v = Mathf.InverseLerp(worldBounds.min.y, worldBounds.max.y, worldPos.y);

        if (u < 0f || u > 1f || v < 0f || v > 1f)
            return Color.black;

        return tex.GetPixelBilinear(u, v);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor tool: Click in scene view while holding Shift to test water detection.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!isInitialized) return;

        // Draw map bounds
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(worldBounds.center, worldBounds.size);
    }
#endif
}