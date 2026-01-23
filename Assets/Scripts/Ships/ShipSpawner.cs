using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Ship Spawner with ZONE-BASED spawning
/// Ships spawn randomly within a rectangular area, not a single point.
/// </summary>
public class ShipSpawner : MonoBehaviour
{
    public static ShipSpawner Instance { get; private set; }

    [Header("=== SHIP PREFABS ===")]
    public GameObject merchantShipPrefab;
    public GameObject pirateShipPrefab;
    public GameObject securityShipPrefab;

    [Header("=== MERCHANT SPAWN ZONE ===")]
    [Tooltip("Center of the spawn zone")]
    public Vector2 merchantSpawnCenter = new Vector2(-8, 0);
    [Tooltip("Size of spawn zone (ships spawn randomly within)")]
    public Vector2 merchantSpawnSize = new Vector2(2, 6);
    public Vector2 merchantDestination = new Vector2(8, 0);
    public float merchantSpeed = 0.05f;

    [Header("=== PIRATE SPAWN ZONE ===")]
    public Vector2 pirateSpawnCenter = new Vector2(0, 0);
    public Vector2 pirateSpawnSize = new Vector2(4, 4);
    public Vector2 piratePatrolPoint = new Vector2(0, -1);
    public float pirateSpeed = 0.07f;

    [Header("=== SECURITY SPAWN ZONE ===")]
    public Vector2 securitySpawnCenter = new Vector2(6, 0);
    public Vector2 securitySpawnSize = new Vector2(2, 4);
    public Vector2 securityPatrolPoint = new Vector2(-2, -1);
    public float securitySpeed = 0.06f;

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
    /// Spawn a ship of the specified type at a random position within its zone.
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

        // Get random position within spawn zone
        Vector2 spawnPoint = GetRandomSpawnPoint(type, rng);
        Vector2 destination = GetDestination(type);
        float speed = GetSpeed(type);

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

        // Initialize and set destination
        controller.Initialize(data);
        controller.SetDestination(destination);

        // Track this ship
        activeShips.Add(controller);

        Debug.Log($"Spawned {type} at {spawnPoint}");

        return controller;
    }

    /// <summary>
    /// Get a random point within the spawn zone for this ship type.
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

        // Random point within rectangle
        float x = center.x + (float)(rng.NextDouble() - 0.5) * size.x;
        float y = center.y + (float)(rng.NextDouble() - 0.5) * size.y;

        return new Vector2(x, y);
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