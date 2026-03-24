using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Map Manager - Handles multiple map regions
/// 
/// UPDATED: Guards SpawnZoneConfigurator sync when trade routes exist.
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
        if (mapDropdown != null)
        {
            SetupDropdown();
        }

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

    public void OnMapSelected(int index)
    {
        LoadMap(index);
    }

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

        UpdateMapColorSampler();
        RebuildPathfinding();
        UpdateShipIdentityRegion();

        // Fire event for other systems (MapSpawnPresets listens to this)
        OnMapChanged?.Invoke(currentMapIndex);

        // === UPDATED: Only sync spawn zone configurator if NO trade routes for this map ===
        bool tradeRoutesActive = false;
        if (TradeRouteManager.Instance != null)
        {
            TradeRouteManager.Instance.SetMapIndex(currentMapIndex);
            tradeRoutesActive = TradeRouteManager.Instance.HasRouteData();
        }

        if (!tradeRoutesActive && SpawnZoneConfigurator.Instance != null)
        {
            SpawnZoneConfigurator.Instance.SyncFromSpawner();
        }
    }

    void UpdateMapColorSampler()
    {
        if (MapColorSampler.Instance != null)
        {
            MapColorSampler.Instance.Initialize();
            MapColorSampler.Instance.LoadMaskForCurrentMap();
            Debug.Log("MapManager: MapColorSampler updated with new mask");
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
        ShipIdentity.CurrentRegion = (ShipIdentity.MapRegion)currentMapIndex;
        Debug.Log($"MapManager: Ship identity region set to {ShipIdentity.CurrentRegion}");
    }

    public string GetCurrentMapName()
    {
        if (currentMapIndex >= 0 && currentMapIndex < mapNames.Length)
            return mapNames[currentMapIndex];
        return "Unknown";
    }

    public int GetCurrentMapIndex()
    {
        return currentMapIndex;
    }

    public System.Action<int> OnMapChanged;

    public void NextMap()
    {
        int next = (currentMapIndex + 1) % mapSprites.Length;
        LoadMap(next);
        
        if (mapDropdown != null)
            mapDropdown.value = next;
    }

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