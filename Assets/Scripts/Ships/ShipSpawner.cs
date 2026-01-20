using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Spawns and manages all ship types in the simulation.
/// 
/// SETUP:
/// 1. Assign prefabs for each ship type
/// 2. Set spawn points and destinations
/// 3. Call SpawnShip() from your SimulationEngine
/// </summary>
public class ShipSpawner : MonoBehaviour
{
    public static ShipSpawner Instance { get; private set; }

    [Header("Ship Prefabs")]
    public GameObject merchantShipPrefab;
    public GameObject pirateShipPrefab;
    public GameObject securityShipPrefab;

    [Header("Merchant Settings")]
    public Vector2 merchantSpawnPoint = new Vector2(-8, -2);
    public Vector2 merchantDestination = new Vector2(8, -2);
    public float merchantSpeed = 0.05f;

    [Header("Pirate Settings")]
    public Vector2 pirateSpawnPoint = new Vector2(0, 2);
    public Vector2 piratePatrolPoint = new Vector2(0, -1);
    public float pirateSpeed = 0.07f;  // Faster than merchants

    [Header("Security Settings")]
    public Vector2 securitySpawnPoint = new Vector2(6, -2);
    public Vector2 securityPatrolPoint = new Vector2(-2, -1);
    public float securitySpeed = 0.06f;  // Medium speed

    [Header("Spawn Variance")]
    [Tooltip("Add random offset to spawn positions")]
    public bool useSpawnJitter = true;
    public float spawnJitterAmount = 0.5f;

    // Track all active ships
    private List<ShipController> activeShips = new List<ShipController>();
    private int shipIdCounter = 0;

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// Spawn a ship of the specified type.
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

        // Get spawn point and destination based on type
        Vector2 spawnPoint = GetSpawnPoint(type);
        Vector2 destination = GetDestination(type);
        float speed = GetSpeed(type);

        // Add jitter for variety
        if (useSpawnJitter)
        {
            float jitterX = (float)(rng.NextDouble() * 2 - 1) * spawnJitterAmount;
            float jitterY = (float)(rng.NextDouble() * 2 - 1) * spawnJitterAmount;
            spawnPoint += new Vector2(jitterX, jitterY);
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

        // Initialize and set destination
        controller.Initialize(data);
        controller.SetDestination(destination);

        // Track this ship
        activeShips.Add(controller);

        Debug.Log($"Spawned {type} at {spawnPoint} heading to {destination}");

        return controller;
    }

    /// <summary>
    /// Spawn a merchant ship (convenience method for backwards compatibility).
    /// </summary>
    public ShipController SpawnCargo(System.Random rng, string idSuffix = "")
    {
        return SpawnShip(ShipType.Cargo, rng);
    }

    /// <summary>
    /// Spawn a pirate ship.
    /// </summary>
    public ShipController SpawnPirate(System.Random rng = null)
    {
        return SpawnShip(ShipType.Pirate, rng);
    }

    /// <summary>
    /// Spawn a security ship.
    /// </summary>
    public ShipController SpawnSecurity(System.Random rng = null)
    {
        return SpawnShip(ShipType.Security, rng);
    }

    /// <summary>
    /// Get all active ships.
    /// </summary>
    public List<ShipController> GetActiveShips()
    {
        // Clean up any null references (destroyed ships)
        activeShips.RemoveAll(s => s == null);
        return activeShips;
    }

    /// <summary>
    /// Get all active ships of a specific type.
    /// </summary>
    public List<ShipController> GetShipsOfType(ShipType type)
    {
        activeShips.RemoveAll(s => s == null);
        return activeShips.FindAll(s => s.Data != null && s.Data.type == type);
    }

    /// <summary>
    /// Remove a ship from tracking (call when ship exits or is destroyed).
    /// </summary>
    public void RemoveShip(ShipController ship)
    {
        activeShips.Remove(ship);
    }

    /// <summary>
    /// Destroy all active ships (for reset).
    /// </summary>
    public void ClearAllShips()
    {
        foreach (var ship in activeShips)
        {
            if (ship != null)
            {
                Destroy(ship.gameObject);
            }
        }
        activeShips.Clear();
        shipIdCounter = 0;
    }

    // Helper methods
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

    private Vector2 GetSpawnPoint(ShipType type)
    {
        switch (type)
        {
            case ShipType.Cargo: return merchantSpawnPoint;
            case ShipType.Pirate: return pirateSpawnPoint;
            case ShipType.Security: return securitySpawnPoint;
            default: return merchantSpawnPoint;
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
}