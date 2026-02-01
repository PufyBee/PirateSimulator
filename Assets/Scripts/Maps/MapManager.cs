using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Map Manager - Handles multiple map regions
/// 
/// Manages:
/// - Switching between maps
/// - Updating MapColorSampler reference
/// - Rebuilding pathfinding grid
/// - Setting correct region for ship identities
/// 
/// SETUP:
/// 1. Add this to a GameObject (e.g., "MapManager")
/// 2. Assign your map sprites
/// 3. Connect to a dropdown in your UI
/// </summary>
public class MapManager : MonoBehaviour
{
    public static MapManager Instance { get; private set; }

    [Header("=== MAP SPRITES ===")]
    [Tooltip("Assign in order: Malacca, Aden, Guinea")]
    public Sprite[] mapSprites;

    [Header("=== MAP NAMES ===")]
    public string[] mapNames = { 
        "Strait of Malacca", 
        "Gulf of Aden", 
        "Gulf of Guinea" 
    };

    [Header("=== REFERENCES ===")]
    public SpriteRenderer mapRenderer;
    public TMP_Dropdown mapDropdown;

    [Header("=== CURRENT MAP ===")]
    [SerializeField] private int currentMapIndex = 0;
    public MapRegion CurrentRegion => (MapRegion)currentMapIndex;

    public enum MapRegion
    {
        StraitOfMalacca = 0,
        GulfOfAden = 1,
        GulfOfGuinea = 2
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // Setup dropdown if assigned
        if (mapDropdown != null)
        {
            SetupDropdown();
        }

        // Load initial map
        if (mapSprites != null && mapSprites.Length > 0)
        {
            LoadMap(currentMapIndex);
        }
    }

    void SetupDropdown()
    {
        mapDropdown.ClearOptions();
        
        var options = new System.Collections.Generic.List<TMP_Dropdown.OptionData>();
        foreach (string name in mapNames)
        {
            options.Add(new TMP_Dropdown.OptionData(name));
        }
        
        mapDropdown.AddOptions(options);
        mapDropdown.value = currentMapIndex;
        mapDropdown.onValueChanged.AddListener(OnMapSelected);
    }

    /// <summary>
    /// Called when user selects a map from dropdown
    /// </summary>
    public void OnMapSelected(int index)
    {
        LoadMap(index);
    }

    /// <summary>
    /// Load a specific map by index
    /// </summary>
    public void LoadMap(int index)
    {
        if (mapSprites == null || index < 0 || index >= mapSprites.Length)
        {
            Debug.LogError($"MapManager: Invalid map index {index}");
            return;
        }

        if (mapSprites[index] == null)
        {
            Debug.LogError($"MapManager: Map sprite at index {index} is null!");
            return;
        }

        currentMapIndex = index;
        
        // Update the map sprite
        if (mapRenderer != null)
        {
            mapRenderer.sprite = mapSprites[index];
            Debug.Log($"MapManager: Loaded map '{mapNames[index]}'");
        }
        else
        {
            Debug.LogError("MapManager: No mapRenderer assigned!");
            return;
        }

        // Update MapColorSampler
        UpdateMapColorSampler();

        // Rebuild pathfinding grid
        RebuildPathfinding();

        // Update ship identity region
        UpdateShipIdentityRegion();

        // Fire event for other systems
        OnMapChanged?.Invoke(currentMapIndex);

        // Sync spawn zone configurator to show new preset positions
        if (SpawnZoneConfigurator.Instance != null)
        {
            SpawnZoneConfigurator.Instance.SyncFromSpawner();
        }
    }

    void UpdateMapColorSampler()
    {
        if (MapColorSampler.Instance != null)
        {
            // MapColorSampler should already reference the mapRenderer
            // Just re-initialize it to update bounds
            MapColorSampler.Instance.Initialize();
            Debug.Log("MapManager: MapColorSampler updated");
        }
    }

    void RebuildPathfinding()
    {
        if (Pathfinder.Instance != null)
        {
            Pathfinder.Instance.BuildGrid();
            Debug.Log("MapManager: Pathfinding grid rebuilt");
        }
    }

    void UpdateShipIdentityRegion()
    {
        // Update the static region for new ships
        ShipIdentity.CurrentRegion = (ShipIdentity.MapRegion)currentMapIndex;
        Debug.Log($"MapManager: Ship identity region set to {ShipIdentity.CurrentRegion}");
    }

    /// <summary>
    /// Get the current map name
    /// </summary>
    public string GetCurrentMapName()
    {
        if (currentMapIndex >= 0 && currentMapIndex < mapNames.Length)
            return mapNames[currentMapIndex];
        return "Unknown";
    }

    /// <summary>
    /// Get current map index
    /// </summary>
    public int GetCurrentMapIndex()
    {
        return currentMapIndex;
    }

    // Event for other systems to listen to
    public System.Action<int> OnMapChanged;

    /// <summary>
    /// Cycle to next map (useful for testing)
    /// </summary>
    public void NextMap()
    {
        int next = (currentMapIndex + 1) % mapSprites.Length;
        LoadMap(next);
        
        if (mapDropdown != null)
            mapDropdown.value = next;
    }

    /// <summary>
    /// Load map by name
    /// </summary>
    public void LoadMap(string mapName)
    {
        for (int i = 0; i < mapNames.Length; i++)
        {
            if (mapNames[i].ToLower().Contains(mapName.ToLower()))
            {
                LoadMap(i);
                return;
            }
        }
        Debug.LogWarning($"MapManager: Map '{mapName}' not found");
    }
}