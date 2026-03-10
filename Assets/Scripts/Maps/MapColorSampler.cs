using UnityEngine;

/// <summary>
/// Samples the MASK texture to determine if a position is water or land.
/// 
/// MASK SYSTEM (Simple & Reliable):
/// - White pixels = WATER (ships can go here)
/// - Black pixels = LAND (ships cannot go here)
/// 
/// Masks should be named: Malacca_12-9_mask, Guinea_12-9_mask, Aden_12-9_mask
/// and placed in Assets/Maps/ (or a Resources folder)
/// </summary>
public class MapColorSampler : MonoBehaviour
{
    public static MapColorSampler Instance { get; private set; }

    [Header("=== MAP RENDERER ===")]
    public SpriteRenderer mapRenderer;

    [Header("=== MASK TEXTURES ===")]
    [Tooltip("Assign masks in order: Malacca, Aden, Guinea")]
    public Texture2D[] maskTextures;

    [Header("=== MASK SETTINGS ===")]
    [Tooltip("Threshold for water detection (pixels brighter than this = water)")]
    [Range(0f, 1f)]
    public float waterThreshold = 0.5f;

    [Header("=== COAST BUFFER ===")]
    [Tooltip("Check surrounding points to prevent ships from touching coastlines")]
    public bool useCoastBuffer = true;

    [Tooltip("How far around each point to check (world units)")]
    public float coastBufferWorld = 0.5f;

    [Header("=== DEBUG ===")]
    public bool debugLogging = false;

    private Texture2D currentMask;
    private Bounds worldBounds;
    private bool isInitialized = false;
    private int currentMapIndex = 0;

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

        worldBounds = mapRenderer.bounds;

        // Get current map index from MapManager
        if (MapManager.Instance != null)
        {
            currentMapIndex = MapManager.Instance.GetCurrentMapIndex();
        }

        // Load the appropriate mask
        LoadMaskForCurrentMap();

        isInitialized = true;
        Debug.Log($"MapColorSampler initialized with mask. Map bounds: {worldBounds}");
    }

    /// <summary>
    /// Load the mask texture for the current map
    /// </summary>
    public void LoadMaskForCurrentMap()
    {
        if (MapManager.Instance != null)
        {
            currentMapIndex = MapManager.Instance.GetCurrentMapIndex();
        }

        // Try to get mask from array
        if (maskTextures != null && currentMapIndex < maskTextures.Length && maskTextures[currentMapIndex] != null)
        {
            currentMask = maskTextures[currentMapIndex];
            Debug.Log($"MapColorSampler: Loaded mask from array for map index {currentMapIndex}");
        }
        else
        {
            // Fallback: try to load from Resources
            string[] maskNames = { "Malacca_12-9_mask", "Aden_12-9_mask", "Guinea_12-9_mask" };
            if (currentMapIndex < maskNames.Length)
            {
                currentMask = Resources.Load<Texture2D>($"Maps/{maskNames[currentMapIndex]}");
                if (currentMask == null)
                {
                    currentMask = Resources.Load<Texture2D>(maskNames[currentMapIndex]);
                }
            }

            if (currentMask != null)
            {
                Debug.Log($"MapColorSampler: Loaded mask '{maskNames[currentMapIndex]}' from Resources");
            }
            else
            {
                Debug.LogError($"MapColorSampler: No mask found for map index {currentMapIndex}! Assign masks in Inspector or place in Resources folder.");
            }
        }

        // Check if mask is readable
        if (currentMask != null)
        {
            try
            {
                currentMask.GetPixel(0, 0);
            }
            catch (UnityException)
            {
                Debug.LogError("MapColorSampler: Mask texture is not readable! Go to texture import settings and enable 'Read/Write Enabled'");
                currentMask = null;
            }
        }
    }

    /// <summary>
    /// Check if a world position is navigable water.
    /// </summary>
    public bool IsWater(Vector2 worldPos)
    {
        if (!isInitialized || currentMask == null) return true;

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
    /// Check a single point using the MASK (no buffer).
    /// White = water, Black = land
    /// </summary>
    private bool IsWaterPoint(Vector2 worldPos)
    {
        // Convert world position to UV coordinates (0-1)
        float u = Mathf.InverseLerp(worldBounds.min.x, worldBounds.max.x, worldPos.x);
        float v = Mathf.InverseLerp(worldBounds.min.y, worldBounds.max.y, worldPos.y);

        // Outside map bounds = treat as water (allows ships to exit)
        if (u < 0f || u > 1f || v < 0f || v > 1f)
            return true;

        // Sample the MASK texture
        Color c = currentMask.GetPixelBilinear(u, v);

        // Simple check: brightness > threshold = water
        float brightness = (c.r + c.g + c.b) / 3f;
        bool isWater = brightness > waterThreshold;

        if (debugLogging)
        {
            Debug.Log($"IsWaterPoint: pos={worldPos}, brightness={brightness:F2}, isWater={isWater}");
        }

        return isWater;
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
        if (currentMask == null) return Color.black;

        float u = Mathf.InverseLerp(worldBounds.min.x, worldBounds.max.x, worldPos.x);
        float v = Mathf.InverseLerp(worldBounds.min.y, worldBounds.max.y, worldPos.y);

        if (u < 0f || u > 1f || v < 0f || v > 1f)
            return Color.black;

        return currentMask.GetPixelBilinear(u, v);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!isInitialized) return;

        // Draw map bounds
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(worldBounds.center, worldBounds.size);
    }

    private void OnDrawGizmos()
    {
        if (currentMask == null) return;

        // Show current mask name in scene
        UnityEditor.Handles.Label(
            worldBounds.center + Vector3.up * (worldBounds.size.y * 0.5f + 5f),
            $"MASK: {currentMask.name}\nIndex: {currentMapIndex}"
        );

        // Draw a grid of water/land points for visual debugging
        if (debugLogging && isInitialized)
        {
            int gridSize = 20;
            float stepX = worldBounds.size.x / gridSize;
            float stepY = worldBounds.size.y / gridSize;

            for (int x = 0; x <= gridSize; x++)
            {
                for (int y = 0; y <= gridSize; y++)
                {
                    Vector2 worldPos = new Vector2(
                        worldBounds.min.x + x * stepX,
                        worldBounds.min.y + y * stepY
                    );

                    bool water = IsWaterPoint(worldPos);
                    Gizmos.color = water ? new Color(0, 0, 1, 0.3f) : new Color(1, 0, 0, 0.3f);
                    Gizmos.DrawCube(new Vector3(worldPos.x, worldPos.y, 0), new Vector3(stepX * 0.8f, stepY * 0.8f, 0.1f));
                }
            }
        }
    }
#endif
}