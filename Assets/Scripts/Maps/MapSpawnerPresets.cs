using UnityEngine;

/// <summary>
/// Map Spawn Presets - Stores spawn zone positions for each map
/// 
/// Each map has different water locations, so spawn zones must change.
/// When you switch maps, zones automatically reposition.
/// 
/// SETUP:
/// 1. Add this component to your MapManager object
/// 2. Assign your ShipSpawner
/// 3. Switch to a map, position zones in ShipSpawner Inspector
/// 4. Right-click this component → "Save Current as [Map] Preset"
/// 5. Repeat for each map
/// </summary>
public class MapSpawnPresets : MonoBehaviour
{
    public static MapSpawnPresets Instance { get; private set; }

    [Header("=== REFERENCE ===")]
    public ShipSpawner shipSpawner;

    [Header("=== GUINEA PRESET ===")]
    public Vector2 guineaMerchantCenter = new Vector2(-50, -50);
    public Vector2 guineaMerchantSize = new Vector2(30, 30);
    public Vector2 guineaMerchantDest = new Vector2(50, -80);
    
    public Vector2 guineaPirateCenter = new Vector2(0, -60);
    public Vector2 guineaPirateSize = new Vector2(20, 20);
    public Vector2 guineaPiratePatrol = new Vector2(-20, -70);
    
    public Vector2 guineaSecurityCenter = new Vector2(30, -50);
    public Vector2 guineaSecuritySize = new Vector2(20, 20);
    public Vector2 guineaSecurityPatrol = new Vector2(-10, -60);

    [Header("=== MALACCA PRESET ===")]
    public Vector2 malaccaMerchantCenter = new Vector2(-60, 0);
    public Vector2 malaccaMerchantSize = new Vector2(20, 40);
    public Vector2 malaccaMerchantDest = new Vector2(40, -40);
    
    public Vector2 malaccaPirateCenter = new Vector2(-20, -20);
    public Vector2 malaccaPirateSize = new Vector2(30, 20);
    public Vector2 malaccaPiratePatrol = new Vector2(0, -30);
    
    public Vector2 malaccaSecurityCenter = new Vector2(20, -30);
    public Vector2 malaccaSecuritySize = new Vector2(20, 20);
    public Vector2 malaccaSecurityPatrol = new Vector2(-30, -10);

    [Header("=== ADEN PRESET ===")]
    public Vector2 adenMerchantCenter = new Vector2(-60, 30);
    public Vector2 adenMerchantSize = new Vector2(20, 30);
    public Vector2 adenMerchantDest = new Vector2(60, 0);
    
    public Vector2 adenPirateCenter = new Vector2(0, 20);
    public Vector2 adenPirateSize = new Vector2(40, 20);
    public Vector2 adenPiratePatrol = new Vector2(-20, 10);
    
    public Vector2 adenSecurityCenter = new Vector2(40, 10);
    public Vector2 adenSecuritySize = new Vector2(20, 20);
    public Vector2 adenSecurityPatrol = new Vector2(-10, 20);

    [Header("=== DEBUG ===")]
    public bool logChanges = true;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // Auto-find ShipSpawner if not assigned
        if (shipSpawner == null)
        {
            shipSpawner = FindObjectOfType<ShipSpawner>();
        }

        // Subscribe to map changes
        if (MapManager.Instance != null)
        {
            MapManager.Instance.OnMapChanged += OnMapChanged;
            // Apply initial
            OnMapChanged(MapManager.Instance.GetCurrentMapIndex());
        }
    }

    void OnDestroy()
    {
        if (MapManager.Instance != null)
        {
            MapManager.Instance.OnMapChanged -= OnMapChanged;
        }
    }

    void OnMapChanged(int mapIndex)
    {
        ApplyPreset(mapIndex);
    }

    public void ApplyPreset(int mapIndex)
    {
        if (shipSpawner == null)
        {
            Debug.LogWarning("MapSpawnPresets: No ShipSpawner assigned!");
            return;
        }

        switch (mapIndex)
        {
            case 0: // Guinea
                ApplyGuineaPreset();
                break;
            case 1: // Malacca
                ApplyMalaccaPreset();
                break;
            case 2: // Aden
                ApplyAdenPreset();
                break;
            default:
                Debug.LogWarning($"MapSpawnPresets: Unknown map index {mapIndex}");
                break;
        }
    }

    void ApplyGuineaPreset()
    {
        shipSpawner.merchantSpawnCenter = guineaMerchantCenter;
        shipSpawner.merchantSpawnSize = guineaMerchantSize;
        shipSpawner.merchantDestination = guineaMerchantDest;
        
        shipSpawner.pirateSpawnCenter = guineaPirateCenter;
        shipSpawner.pirateSpawnSize = guineaPirateSize;
        shipSpawner.piratePatrolPoint = guineaPiratePatrol;
        
        shipSpawner.securitySpawnCenter = guineaSecurityCenter;
        shipSpawner.securitySpawnSize = guineaSecuritySize;
        shipSpawner.securityPatrolPoint = guineaSecurityPatrol;
        
        if (logChanges) Debug.Log("MapSpawnPresets: Applied Guinea preset");
    }

    void ApplyMalaccaPreset()
    {
        shipSpawner.merchantSpawnCenter = malaccaMerchantCenter;
        shipSpawner.merchantSpawnSize = malaccaMerchantSize;
        shipSpawner.merchantDestination = malaccaMerchantDest;
        
        shipSpawner.pirateSpawnCenter = malaccaPirateCenter;
        shipSpawner.pirateSpawnSize = malaccaPirateSize;
        shipSpawner.piratePatrolPoint = malaccaPiratePatrol;
        
        shipSpawner.securitySpawnCenter = malaccaSecurityCenter;
        shipSpawner.securitySpawnSize = malaccaSecuritySize;
        shipSpawner.securityPatrolPoint = malaccaSecurityPatrol;
        
        if (logChanges) Debug.Log("MapSpawnPresets: Applied Malacca preset");
    }

    void ApplyAdenPreset()
    {
        shipSpawner.merchantSpawnCenter = adenMerchantCenter;
        shipSpawner.merchantSpawnSize = adenMerchantSize;
        shipSpawner.merchantDestination = adenMerchantDest;
        
        shipSpawner.pirateSpawnCenter = adenPirateCenter;
        shipSpawner.pirateSpawnSize = adenPirateSize;
        shipSpawner.piratePatrolPoint = adenPiratePatrol;
        
        shipSpawner.securitySpawnCenter = adenSecurityCenter;
        shipSpawner.securitySpawnSize = adenSecuritySize;
        shipSpawner.securityPatrolPoint = adenSecurityPatrol;
        
        if (logChanges) Debug.Log("MapSpawnPresets: Applied Aden preset");
    }

    // ============ CONTEXT MENU - Right-click component header ============

    [ContextMenu("Save Current as Guinea Preset")]
    public void SaveAsGuinea()
    {
        if (shipSpawner == null) return;
        
        guineaMerchantCenter = shipSpawner.merchantSpawnCenter;
        guineaMerchantSize = shipSpawner.merchantSpawnSize;
        guineaMerchantDest = shipSpawner.merchantDestination;
        
        guineaPirateCenter = shipSpawner.pirateSpawnCenter;
        guineaPirateSize = shipSpawner.pirateSpawnSize;
        guineaPiratePatrol = shipSpawner.piratePatrolPoint;
        
        guineaSecurityCenter = shipSpawner.securitySpawnCenter;
        guineaSecuritySize = shipSpawner.securitySpawnSize;
        guineaSecurityPatrol = shipSpawner.securityPatrolPoint;
        
        Debug.Log("Saved current zones to Guinea preset!");
    }

    [ContextMenu("Save Current as Malacca Preset")]
    public void SaveAsMalacca()
    {
        if (shipSpawner == null) return;
        
        malaccaMerchantCenter = shipSpawner.merchantSpawnCenter;
        malaccaMerchantSize = shipSpawner.merchantSpawnSize;
        malaccaMerchantDest = shipSpawner.merchantDestination;
        
        malaccaPirateCenter = shipSpawner.pirateSpawnCenter;
        malaccaPirateSize = shipSpawner.pirateSpawnSize;
        malaccaPiratePatrol = shipSpawner.piratePatrolPoint;
        
        malaccaSecurityCenter = shipSpawner.securitySpawnCenter;
        malaccaSecuritySize = shipSpawner.securitySpawnSize;
        malaccaSecurityPatrol = shipSpawner.securityPatrolPoint;
        
        Debug.Log("Saved current zones to Malacca preset!");
    }

    [ContextMenu("Save Current as Aden Preset")]
    public void SaveAsAden()
    {
        if (shipSpawner == null) return;
        
        adenMerchantCenter = shipSpawner.merchantSpawnCenter;
        adenMerchantSize = shipSpawner.merchantSpawnSize;
        adenMerchantDest = shipSpawner.merchantDestination;
        
        adenPirateCenter = shipSpawner.pirateSpawnCenter;
        adenPirateSize = shipSpawner.pirateSpawnSize;
        adenPiratePatrol = shipSpawner.piratePatrolPoint;
        
        adenSecurityCenter = shipSpawner.securitySpawnCenter;
        adenSecuritySize = shipSpawner.securitySpawnSize;
        adenSecurityPatrol = shipSpawner.securityPatrolPoint;
        
        Debug.Log("Saved current zones to Aden preset!");
    }
}