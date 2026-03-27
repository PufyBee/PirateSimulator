using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Ship Spawner - TRADE ROUTE INTEGRATED
/// 
/// WHAT CHANGED:
/// - SpawnShip() now checks TradeRouteManager for route data
/// - Merchants: assigned a weighted random trade route (spawn at route start)
/// - Pirates: assigned to a coastal base (spawn at base, patrol to ambush points)
/// - Security: assigned to a patrol zone (spawn at first patrol waypoint)
/// - If no TradeRouteManager or no route data → falls back to OLD zone-based spawning
/// 
/// ALL EXISTING INSPECTOR FIELDS ARE PRESERVED for fallback.
/// </summary>
public class ShipSpawner : MonoBehaviour
{
    public static ShipSpawner Instance { get; private set; }

    [Header("=== SHIP PREFABS ===")]
    public GameObject merchantShipPrefab;
    public GameObject pirateShipPrefab;
    public GameObject securityShipPrefab;

    [Header("=== MERCHANT SPAWN ZONE (Fallback) ===")]
    [Tooltip("Center of the spawn zone")]
    public Vector2 merchantSpawnCenter = new Vector2(-8, 0);
    [Tooltip("Size of spawn zone (ships spawn randomly within)")]
    public Vector2 merchantSpawnSize = new Vector2(2, 6);
    public Vector2 merchantDestination = new Vector2(8, 0);
    public float merchantSpeed = 0.05f;

    [Header("=== PIRATE SPAWN ZONE (Fallback) ===")]
    public Vector2 pirateSpawnCenter = new Vector2(0, 0);
    public Vector2 pirateSpawnSize = new Vector2(4, 4);
    public Vector2 piratePatrolPoint = new Vector2(0, -1);
    public float pirateSpeed = 0.07f;

    [Header("=== SECURITY SPAWN ZONE (Fallback) ===")]
    public Vector2 securitySpawnCenter = new Vector2(6, 0);
    public Vector2 securitySpawnSize = new Vector2(2, 4);
    public Vector2 securityPatrolPoint = new Vector2(-2, -1);
    public float securitySpeed = 0.06f;

    [Header("=== VISUAL EFFECTS ===")]
    [Tooltip("Add wake trails and radar ping to spawned ships")]
    public bool enableVisualEffects = true;

    [Header("=== DEBUG ===")]
    public bool showSpawnZones = true;

    // Tracking
    private List<ShipController> activeShips = new List<ShipController>();
    private int shipIdCounter = 0;

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// Spawn a ship of the specified type.
    /// If TradeRouteManager has data → uses trade routes/bases/patrols.
    /// Otherwise → falls back to old zone-based spawning.
    /// </summary>
    public ShipController SpawnShip(ShipType type, System.Random rng = null)
    {
        if (rng == null) rng = new System.Random();

        GameObject prefab = GetPrefab(type);
        if (prefab == null)
        {
            Debug.LogError($"ShipSpawner: No prefab assigned for {type}!");
            return null;
        }

        // Determine spawn position and speed
        // If trade routes are active, use a dummy position — TradeRouteManager will override it
        Vector2 spawnPoint;
        float speed = GetSpeed(type);

        bool hasTradeRoutes = TradeRouteManager.Instance != null && (
            (type == ShipType.Cargo && TradeRouteManager.Instance.HasRouteData()) ||
            (type == ShipType.Pirate && TradeRouteManager.Instance.HasPirateBaseData()) ||
            (type == ShipType.Security && TradeRouteManager.Instance.HasNavyPatrolData())
        );

        if (hasTradeRoutes)
        {
            // Use origin as placeholder — TradeRouteManager will set the real position
            spawnPoint = Vector2.zero;
        }
        else
        {
            // No trade routes for this type — use old zone-based spawn
            spawnPoint = GetRandomSpawnPoint(type, rng);
        }

        // Create the ship
        GameObject shipObj = Instantiate(prefab);
        shipObj.name = $"{type}_{shipIdCounter}";

        ShipController controller = shipObj.GetComponent<ShipController>();
        if (controller == null)
        {
            controller = shipObj.AddComponent<ShipController>();
        }

        // Create ship data
        ShipData data = new ShipData
        {
            shipId = $"{type}-{shipIdCounter}",
            type = type,
            state = ShipState.Moving,
            position = spawnPoint,
            speedUnitsPerTick = speed,
            velocityDir = Vector2.right,
            route = null
        };

        shipIdCounter++;

        // Initialize the ship
        controller.Initialize(data);

        // === TRADE ROUTE ASSIGNMENT ===
        bool routeAssigned = false;

        if (TradeRouteManager.Instance != null)
        {
            switch (type)
            {
                case ShipType.Cargo:
                    if (TradeRouteManager.Instance.HasRouteData())
                    {
                        var assignment = TradeRouteManager.Instance.AssignMerchantRoute(controller, rng);
                        if (assignment != null)
                            routeAssigned = true;
                    }
                    break;

                case ShipType.Pirate:
                    if (TradeRouteManager.Instance.HasPirateBaseData())
                    {
                        Vector2 basePos = TradeRouteManager.Instance.AssignPirateBase(controller, rng);
                        if (basePos != Vector2.zero)
                            routeAssigned = true;
                    }
                    break;

                case ShipType.Security:
                    if (TradeRouteManager.Instance.HasNavyPatrolData())
                    {
                        Vector2 patrolStart = TradeRouteManager.Instance.AssignNavyPatrol(controller, rng);
                        if (patrolStart != Vector2.zero)
                            routeAssigned = true;
                    }
                    break;
            }
        }

        // === FALLBACK: Old zone-based spawning if no route was assigned ===
        if (!routeAssigned)
        {
            Vector2 destination = GetDestination(type);
            controller.SetDestination(destination);
        }

        // Add visual effects (wake trail, radar ping)
        if (enableVisualEffects && shipObj.GetComponent<ShipVisualEffects>() == null)
        {
            shipObj.AddComponent<ShipVisualEffects>();
        }

        // Track this ship
        activeShips.Add(controller);

        return controller;
    }

    /// <summary>
    /// Get a random point within the spawn zone for this ship type.
    /// Validates the point is in water, retries up to maxAttempts times.
    /// </summary>
    private Vector2 GetRandomSpawnPoint(ShipType type, System.Random rng)
    {
        Vector2 center;
        Vector2 size;

        switch (type)
        {
            case ShipType.Cargo:
                center = merchantSpawnCenter;
                size = merchantSpawnSize;
                break;
            case ShipType.Pirate:
                center = pirateSpawnCenter;
                size = pirateSpawnSize;
                break;
            case ShipType.Security:
                center = securitySpawnCenter;
                size = securitySpawnSize;
                break;
            default:
                center = merchantSpawnCenter;
                size = merchantSpawnSize;
                break;
        }

        int maxAttempts = 20;
        for (int i = 0; i < maxAttempts; i++)
        {
            float x = center.x + (float)(rng.NextDouble() - 0.5) * size.x;
            float y = center.y + (float)(rng.NextDouble() - 0.5) * size.y;
            Vector2 point = new Vector2(x, y);

            if (MapColorSampler.Instance == null || MapColorSampler.Instance.IsWater(point))
            {
                return point;
            }
        }

        Debug.LogWarning($"ShipSpawner: Could not find water spawn point for {type} after {maxAttempts} attempts. Using center.");

        if (MapColorSampler.Instance != null && !MapColorSampler.Instance.IsWater(center))
        {
            Vector2[] offsets = {
                new Vector2(1, 0), new Vector2(-1, 0),
                new Vector2(0, 1), new Vector2(0, -1),
                new Vector2(1, 1), new Vector2(-1, -1),
                new Vector2(1, -1), new Vector2(-1, 1)
            };

            for (float dist = 0.5f; dist < 5f; dist += 0.5f)
            {
                foreach (var offset in offsets)
                {
                    Vector2 testPoint = center + offset * dist;
                    if (MapColorSampler.Instance.IsWater(testPoint))
                    {
                        return testPoint;
                    }
                }
            }
        }

        return center;
    }

    // ===== CONVENIENCE METHODS =====

    public ShipController SpawnCargo(System.Random rng, string idSuffix = "")
    {
        return SpawnShip(ShipType.Cargo, rng);
    }

    public ShipController SpawnPirate(System.Random rng = null)
    {
        return SpawnShip(ShipType.Pirate, rng);
    }

    public ShipController SpawnSecurity(System.Random rng = null)
    {
        return SpawnShip(ShipType.Security, rng);
    }

    // ===== TRACKING =====

    public List<ShipController> GetActiveShips()
    {
        activeShips.RemoveAll(s => s == null);
        return activeShips;
    }

    public List<ShipController> GetShipsOfType(ShipType type)
    {
        activeShips.RemoveAll(s => s == null);
        return activeShips.FindAll(s => s.Data != null && s.Data.type == type);
    }

    public void RemoveShip(ShipController ship)
    {
        activeShips.Remove(ship);
    }

    public void ClearAllShips()
    {
        foreach (var ship in activeShips)
        {
            if (ship != null)
                Destroy(ship.gameObject);
        }
        activeShips.Clear();
        shipIdCounter = 0;
    }

    // ===== HELPERS =====

    private GameObject GetPrefab(ShipType type)
    {
        switch (type)
        {
            case ShipType.Cargo: return merchantShipPrefab;
            case ShipType.Pirate: return pirateShipPrefab;
            case ShipType.Security: return securityShipPrefab;
            default: return merchantShipPrefab;
        }
    }

    private Vector2 GetDestination(ShipType type)
    {
        switch (type)
        {
            case ShipType.Cargo: return merchantDestination;
            case ShipType.Pirate: return piratePatrolPoint;
            case ShipType.Security: return securityPatrolPoint;
            default: return merchantDestination;
        }
    }

    private float GetSpeed(ShipType type)
    {
        switch (type)
        {
            case ShipType.Cargo: return merchantSpeed;
            case ShipType.Pirate: return pirateSpeed;
            case ShipType.Security: return securitySpeed;
            default: return merchantSpeed;
        }
    }

    // ===== DEBUG VISUALIZATION =====

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showSpawnZones) return;

        // Don't show old spawn zones if trade routes are configured
        if (TradeRouteManager.Instance != null && TradeRouteManager.Instance.HasRouteData())
            return;

        // Merchant zone - Green
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawCube(merchantSpawnCenter, new Vector3(merchantSpawnSize.x, merchantSpawnSize.y, 0.1f));
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(merchantSpawnCenter, new Vector3(merchantSpawnSize.x, merchantSpawnSize.y, 0.1f));

        // Pirate zone - Red
        Gizmos.color = new Color(1, 0, 0, 0.3f);
        Gizmos.DrawCube(pirateSpawnCenter, new Vector3(pirateSpawnSize.x, pirateSpawnSize.y, 0.1f));
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(pirateSpawnCenter, new Vector3(pirateSpawnSize.x, pirateSpawnSize.y, 0.1f));

        // Security zone - Blue
        Gizmos.color = new Color(0, 0, 1, 0.3f);
        Gizmos.DrawCube(securitySpawnCenter, new Vector3(securitySpawnSize.x, securitySpawnSize.y, 0.1f));
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(securitySpawnCenter, new Vector3(securitySpawnSize.x, securitySpawnSize.y, 0.1f));

        // Destinations - lines
        Gizmos.color = Color.green;
        Gizmos.DrawLine(merchantSpawnCenter, merchantDestination);
        Gizmos.color = Color.red;
        Gizmos.DrawLine(pirateSpawnCenter, piratePatrolPoint);
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(securitySpawnCenter, securityPatrolPoint);
    }
#endif
}