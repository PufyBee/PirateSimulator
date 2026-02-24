using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// COASTAL DEFENSE MANAGER - Draggable battery placement system
/// 
/// Users drag battery icons onto land during setup.
/// Batteries lock when simulation starts.
/// 
/// SETUP:
/// 1. Create empty GameObject named "CoastalDefenseManager"
/// 2. Add this component
/// 3. Set maxBatteries (default 3)
/// 4. Assign MapColorSampler reference (or leave empty for auto-find)
/// 5. Batteries auto-create on Start
/// 
/// INTEGRATION:
/// - Call Lock() when simulation starts
/// - Call Unlock() when simulation resets
/// - Call ResetBatteries() to remove all and recreate
/// </summary>
public class CoastalDefenseManager : MonoBehaviour
{
    [Header("=== BATTERY SETTINGS ===")]
    [Tooltip("Maximum number of coastal defense batteries")]
    public int maxBatteries = 3;

    [Tooltip("Firing range for each battery")]
    public float batteryRange = 8f;

    [Tooltip("Cooldown between shots")]
    public float batteryCooldown = 3f;

    [Header("=== VISUAL SETTINGS ===")]
    [Tooltip("Color of battery icon during setup")]
    public Color batteryColor = new Color(1f, 0.4f, 0f); // Orange

    [Tooltip("Color when hovering valid position (land)")]
    public Color validColor = new Color(0.2f, 0.8f, 0.2f); // Green

    [Tooltip("Color when hovering invalid position (water)")]
    public Color invalidColor = new Color(0.8f, 0.2f, 0.2f); // Red

    [Tooltip("Range circle color")]
    public Color rangeColor = new Color(1f, 0.4f, 0f, 0.3f);

    [Header("=== STARTING POSITIONS ===")]
    [Tooltip("Where batteries start before being placed")]
    public Vector2 startingAreaCenter = new Vector2(-8f, 0f);
    public float startingSpacing = 1.5f;

    [Header("=== REFERENCES ===")]
    public MapColorSampler mapColorSampler;
    public ShipSpawner shipSpawner;

    [Header("=== DEBUG ===")]
    public bool showDebugLogs = false;

    // Runtime
    private List<DraggableBattery> batteries = new List<DraggableBattery>();
    private bool isLocked = false;

    public static CoastalDefenseManager Instance { get; private set; }

    void Awake()
    {
        Instance = this;

        if (mapColorSampler == null)
            mapColorSampler = FindObjectOfType<MapColorSampler>();

        if (shipSpawner == null)
            shipSpawner = FindObjectOfType<ShipSpawner>();
    }

    void Start()
    {
        CreateBatteries();
    }

    void CreateBatteries()
    {
        // Clear existing
        foreach (var battery in batteries)
        {
            if (battery != null && battery.gameObject != null)
                Destroy(battery.gameObject);
        }
        batteries.Clear();

        // Create new batteries
        for (int i = 0; i < maxBatteries; i++)
        {
            Vector2 startPos = startingAreaCenter + new Vector2(0, (i - (maxBatteries - 1) / 2f) * startingSpacing);
            GameObject batteryObj = CreateBatteryObject(i, startPos);
            DraggableBattery db = batteryObj.GetComponent<DraggableBattery>();
            batteries.Add(db);
        }

        if (showDebugLogs)
            Debug.Log($"CoastalDefenseManager: Created {maxBatteries} batteries");
    }

    GameObject CreateBatteryObject(int index, Vector2 position)
    {
        GameObject obj = new GameObject($"CoastalBattery_{index + 1}");
        obj.transform.position = new Vector3(position.x, position.y, -1f);

        // Add draggable component
        DraggableBattery db = obj.AddComponent<DraggableBattery>();
        db.manager = this;
        db.batteryIndex = index;
        db.firingRange = batteryRange;
        db.cooldownTime = batteryCooldown;

        // Main battery sprite (square/tower icon)
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = CreateBatterySprite();
        sr.color = batteryColor;
        sr.sortingOrder = 50;

        // Range indicator (child object)
        GameObject rangeObj = new GameObject("RangeIndicator");
        rangeObj.transform.SetParent(obj.transform);
        rangeObj.transform.localPosition = Vector3.zero;

        SpriteRenderer rangeSR = rangeObj.AddComponent<SpriteRenderer>();
        rangeSR.sprite = CreateCircleSprite();
        rangeSR.color = rangeColor;
        rangeSR.sortingOrder = 49;
        rangeObj.transform.localScale = Vector3.one * batteryRange * 2f;

        db.rangeIndicator = rangeObj;
        db.spriteRenderer = sr;
        db.rangeRenderer = rangeSR;

        // Add collider for clicking - MASSIVE radius for easy dragging
        CircleCollider2D collider = obj.AddComponent<CircleCollider2D>();
        collider.radius = 5f;

        // Also make the visual sprite bigger
        obj.transform.localScale = Vector3.one * 3f;

        return obj;
    }

    // ===== PUBLIC METHODS =====

    /// <summary>
    /// Lock all batteries in place - call when simulation starts
    /// </summary>
    public void Lock()
    {
        isLocked = true;

        foreach (var battery in batteries)
        {
            if (battery != null)
            {
                battery.Lock();
            }
        }

        if (showDebugLogs)
            Debug.Log("CoastalDefenseManager: Batteries LOCKED");
    }

    /// <summary>
    /// Unlock batteries for repositioning - call on reset
    /// </summary>
    public void Unlock()
    {
        isLocked = false;

        foreach (var battery in batteries)
        {
            if (battery != null)
            {
                battery.Unlock();
            }
        }

        // Reset battery states
        CoastalDefense.ResetAllBatteries();

        if (showDebugLogs)
            Debug.Log("CoastalDefenseManager: Batteries UNLOCKED");
    }

    /// <summary>
    /// Reset all batteries to starting positions
    /// </summary>
    public void ResetBatteries()
    {
        CreateBatteries();
        isLocked = false;
    }

    /// <summary>
    /// Check if a position is valid for battery placement (must be on land)
    /// </summary>
    public bool IsValidPosition(Vector2 position)
    {
        if (mapColorSampler == null) return true;

        // Batteries go on LAND (opposite of ships)
        return !mapColorSampler.IsWater(position);
    }

    /// <summary>
    /// Get nearest valid land position
    /// </summary>
    public Vector2 GetNearestLandPosition(Vector2 position)
    {
        if (mapColorSampler == null) return position;

        // If already on land, return as-is
        if (!mapColorSampler.IsWater(position))
            return position;

        // Search in expanding circles for land
        for (float radius = 0.5f; radius < 10f; radius += 0.5f)
        {
            for (int angle = 0; angle < 360; angle += 15)
            {
                float rad = angle * Mathf.Deg2Rad;
                Vector2 testPos = position + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;

                if (!mapColorSampler.IsWater(testPos))
                    return testPos;
            }
        }

        return position;
    }

    public bool IsLocked => isLocked;

    // ===== SPRITE GENERATION =====

    Sprite CreateBatterySprite()
    {
        int size = 32;
        Texture2D tex = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];

        // Clear
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.clear;

        // Draw a fort/tower shape
        int baseWidth = 24;
        int baseHeight = 8;
        int towerWidth = 12;
        int towerHeight = 20;

        // Base
        int baseStartX = (size - baseWidth) / 2;
        int baseStartY = 2;
        for (int y = baseStartY; y < baseStartY + baseHeight; y++)
        {
            for (int x = baseStartX; x < baseStartX + baseWidth; x++)
            {
                pixels[y * size + x] = Color.white;
            }
        }

        // Tower
        int towerStartX = (size - towerWidth) / 2;
        int towerStartY = baseStartY + baseHeight;
        for (int y = towerStartY; y < towerStartY + towerHeight; y++)
        {
            for (int x = towerStartX; x < towerStartX + towerWidth; x++)
            {
                pixels[y * size + x] = Color.white;
            }
        }

        // Battlements (top)
        int battlementY = towerStartY + towerHeight;
        for (int i = 0; i < 3; i++)
        {
            int bx = towerStartX + i * 5;
            for (int y = battlementY; y < battlementY + 4; y++)
            {
                for (int x = bx; x < bx + 3 && x < size; x++)
                {
                    if (y < size)
                        pixels[y * size + x] = Color.white;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    Sprite CreateCircleSprite()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];

        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f - 1;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist <= radius)
                {
                    // Filled circle with slight edge fade
                    float alpha = dist > radius - 2 ? (radius - dist) / 2f : 1f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha * 0.5f);
                }
                else
                {
                    pixels[y * size + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}

/// <summary>
/// Individual draggable battery component
/// </summary>
public class DraggableBattery : MonoBehaviour
{
    [HideInInspector] public CoastalDefenseManager manager;
    [HideInInspector] public int batteryIndex;
    [HideInInspector] public float firingRange;
    [HideInInspector] public float cooldownTime;
    [HideInInspector] public GameObject rangeIndicator;
    [HideInInspector] public SpriteRenderer spriteRenderer;
    [HideInInspector] public SpriteRenderer rangeRenderer;

    private bool isDragging = false;
    private bool isLocked = false;
    private bool isPlacedOnLand = false;
    private Vector3 dragOffset;
    private CoastalDefense defenseComponent;

    private Color originalColor;
    private Color originalRangeColor;

    void Start()
    {
        originalColor = spriteRenderer.color;
        originalRangeColor = rangeRenderer.color;
    }

    void Update()
    {
        if (isLocked) return;

        // Handle dragging
        if (isDragging)
        {
            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = transform.position.z;
            transform.position = mouseWorld + dragOffset;

            // Update color based on validity
            Vector2 pos = new Vector2(transform.position.x, transform.position.y);
            bool isValid = manager.IsValidPosition(pos);

            spriteRenderer.color = isValid ? manager.validColor : manager.invalidColor;
            rangeRenderer.color = isValid 
                ? new Color(manager.validColor.r, manager.validColor.g, manager.validColor.b, 0.3f)
                : new Color(manager.invalidColor.r, manager.invalidColor.g, manager.invalidColor.b, 0.3f);
        }

        // Pulse effect when not placed
        if (!isPlacedOnLand && !isDragging)
        {
            float pulse = (Mathf.Sin(Time.time * 3f) + 1f) / 2f;
            spriteRenderer.color = Color.Lerp(originalColor, originalColor * 1.3f, pulse);
        }
    }

    void OnMouseDown()
    {
        if (isLocked) return;

        isDragging = true;
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        dragOffset = transform.position - mouseWorld;
        dragOffset.z = 0;

        // Bring to front
        spriteRenderer.sortingOrder = 60;
        rangeRenderer.sortingOrder = 59;

        // Show range indicator prominently
        rangeIndicator.SetActive(true);
        rangeIndicator.transform.localScale = Vector3.one * firingRange * 2f;
    }

    void OnMouseUp()
    {
        if (isLocked) return;

        isDragging = false;

        // Reset sorting order
        spriteRenderer.sortingOrder = 50;
        rangeRenderer.sortingOrder = 49;

        // Snap to valid position
        Vector2 pos = new Vector2(transform.position.x, transform.position.y);
        
        if (!manager.IsValidPosition(pos))
        {
            // Find nearest land
            pos = manager.GetNearestLandPosition(pos);
            transform.position = new Vector3(pos.x, pos.y, transform.position.z);
        }

        // Check if now on land
        isPlacedOnLand = manager.IsValidPosition(pos);

        // Reset colors
        spriteRenderer.color = originalColor;
        rangeRenderer.color = originalRangeColor;

        if (manager.showDebugLogs)
            Debug.Log($"Battery {batteryIndex + 1} placed at {pos}, on land: {isPlacedOnLand}");
    }

    /// <summary>
    /// Lock battery in place and activate defense component
    /// </summary>
    public void Lock()
    {
        isLocked = true;
        isDragging = false;

        Vector2 pos = new Vector2(transform.position.x, transform.position.y);
        
        Debug.Log($"DraggableBattery {batteryIndex}: Lock() called at position {pos}");
        Debug.Log($"DraggableBattery {batteryIndex}: IsValidPosition = {manager.IsValidPosition(pos)}");
        
        // Only activate if placed on land
        if (manager.IsValidPosition(pos))
        {
            // Add the actual defense component
            if (defenseComponent == null)
            {
                defenseComponent = gameObject.AddComponent<CoastalDefense>();
                defenseComponent.firingRange = firingRange;
                defenseComponent.cooldownTime = cooldownTime;
                defenseComponent.shipSpawner = manager.shipSpawner;
                defenseComponent.showRangeGizmo = false; // We have our own
                defenseComponent.enableLogs = true;  // Enable logs!
                
                Debug.Log($"DraggableBattery {batteryIndex}: CoastalDefense component ADDED with range {firingRange}");
            }
            defenseComponent.enabled = true;
            Debug.Log($"DraggableBattery {batteryIndex}: CoastalDefense ENABLED");

            // Dim the range indicator during runtime
            rangeRenderer.color = new Color(originalRangeColor.r, originalRangeColor.g, originalRangeColor.b, 0.15f);

            if (manager.showDebugLogs)
                Debug.Log($"Battery {batteryIndex + 1} ACTIVATED at {pos}");
        }
        else
        {
            Debug.LogWarning($"DraggableBattery {batteryIndex}: NOT on land, will not activate");
            
            // Not on land - hide this battery
            spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0.3f);
            rangeIndicator.SetActive(false);

            if (manager.showDebugLogs)
                Debug.Log($"Battery {batteryIndex + 1} NOT ACTIVATED (not on land)");
        }
    }

    /// <summary>
    /// Unlock battery for repositioning
    /// </summary>
    public void Unlock()
    {
        isLocked = false;

        // Disable defense component
        if (defenseComponent != null)
        {
            defenseComponent.enabled = false;
        }

        // Restore visuals
        spriteRenderer.color = originalColor;
        rangeRenderer.color = originalRangeColor;
        rangeIndicator.SetActive(true);
        rangeIndicator.transform.localScale = Vector3.one * firingRange * 2f;
    }
}