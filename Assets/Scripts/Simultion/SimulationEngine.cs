using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SIMULATION ENGINE - Clean Version
/// 
/// This script ONLY handles simulation logic.
/// It does NOT touch any UI elements directly.
/// UI scripts read from this using the public getter methods.
/// 
/// SETUP:
/// 1. Add to a GameObject called "SimulationEngine"
/// 2. Drag ShipSpawner reference in Inspector
/// 3. That's it - UI scripts will connect to this
/// </summary>
public class SimulationEngine : MonoBehaviour
{
    [Header("=== REFERENCES ===")]
    public ShipSpawner shipSpawner;

    [Header("=== RUN SETTINGS ===")]
    [Tooltip("Seconds between simulation ticks")]
    public float tickInterval = 0.25f;
    
    [Tooltip("Maximum ticks before auto-stop (0 = endless)")]
    public int maxTicks = 0;
    
    [Tooltip("Random seed for reproducibility")]
    public int runSeed = 12345;

    [Header("=== SPAWN SETTINGS ===")]
    public int initialMerchants = 2;
    public int initialPirates = 1;
    public int initialSecurity = 1;

    [Tooltip("Ticks between merchant spawns (0 = disabled)")]
    public int merchantSpawnInterval = 50;
    
    [Tooltip("Ticks between pirate spawns (0 = disabled)")]
    public int pirateSpawnInterval = 80;
    
    [Tooltip("Ticks between security spawns (0 = disabled)")]
    public int securitySpawnInterval = 100;

    // ===== PRIVATE STATE =====
    private List<ShipController> ships = new List<ShipController>();
    private Coroutine tickLoop;
    private System.Random rng;

    // State flags
    private bool isRunning = false;
    private bool isPaused = false;
    private int tickCount = 0;

    // Spawn timers
    private int ticksSinceLastMerchant = 0;
    private int ticksSinceLastPirate = 0;
    private int ticksSinceLastSecurity = 0;

    // Statistics
    private int merchantsExited = 0;
    private int merchantsCaptured = 0;
    private int piratesDefeated = 0;
    private HashSet<string> countedShipIds = new HashSet<string>();

    // ===== PUBLIC CONTROL METHODS =====
    // UI scripts call these

    /// <summary>
    /// Start or resume the simulation
    /// </summary>
    public void StartRun()
    {
        // Don't restart if already running
        if (isRunning && !isPaused) return;

        // Initialize RNG if needed
        if (rng == null)
            rng = new System.Random(runSeed);

        // Spawn initial ships if this is a fresh start
        if (ships.Count == 0)
            SpawnInitialShips();

        isRunning = true;
        isPaused = false;

        // Start the tick loop
        if (tickLoop == null)
            tickLoop = StartCoroutine(TickLoop());

        Debug.Log("Simulation STARTED");
    }

    /// <summary>
    /// Pause the simulation
    /// </summary>
    public void PauseRun()
    {
        if (!isRunning) return;
        
        isPaused = true;
        StopTickLoop();
        
        Debug.Log("Simulation PAUSED");
    }

    /// <summary>
    /// Advance one tick (for step-through debugging)
    /// </summary>
    public void StepOnce()
    {
        if (rng == null)
            rng = new System.Random(runSeed);

        if (ships.Count == 0)
            SpawnInitialShips();

        DoOneTick();
        
        Debug.Log($"Simulation STEP - Tick {tickCount}");
    }

    /// <summary>
    /// Stop the simulation completely
    /// </summary>
    public void EndRun()
    {
        StopTickLoop();
        isRunning = false;
        isPaused = false;
        
        Debug.Log("Simulation ENDED");
        PrintFinalStats();
    }

    /// <summary>
    /// Reset everything for a new run
    /// </summary>
    public void ResetToNewRun()
    {
        StopTickLoop();
        
        // Reset state
        isRunning = false;
        isPaused = false;
        tickCount = 0;
        rng = null;

        // Reset spawn timers
        ticksSinceLastMerchant = 0;
        ticksSinceLastPirate = 0;
        ticksSinceLastSecurity = 0;

        // Reset statistics
        merchantsExited = 0;
        merchantsCaptured = 0;
        piratesDefeated = 0;
        countedShipIds.Clear();

        // Destroy all ships
        foreach (var ship in ships)
        {
            if (ship != null)
                Destroy(ship.gameObject);
        }
        ships.Clear();

        // Clear spawner tracking too
        if (shipSpawner != null)
            shipSpawner.ClearAllShips();

        Debug.Log("Simulation RESET");
    }

    /// <summary>
    /// Change simulation speed
    /// </summary>
    public void SetTickInterval(float seconds)
    {
        tickInterval = Mathf.Max(0.01f, seconds);
        
        // Restart loop if running to apply new speed
        if (isRunning && !isPaused)
        {
            StopTickLoop();
            tickLoop = StartCoroutine(TickLoop());
        }
    }

    // ===== PUBLIC GETTERS =====
    // UI scripts read these to display info

    public bool IsRunning() => isRunning;
    public bool IsPaused() => isPaused;
    public int GetTickCount() => tickCount;
    public int GetMerchantsExited() => merchantsExited;
    public int GetMerchantsCaptured() => merchantsCaptured;
    public int GetPiratesDefeated() => piratesDefeated;
    public int GetActiveShipCount() => ships.Count;

    // ===== PRIVATE METHODS =====

    private void SpawnInitialShips()
    {
        if (shipSpawner == null)
        {
            Debug.LogError("SimulationEngine: No ShipSpawner assigned!");
            return;
        }

        for (int i = 0; i < initialMerchants; i++)
            SpawnShip(ShipType.Cargo);

        for (int i = 0; i < initialPirates; i++)
            SpawnShip(ShipType.Pirate);

        for (int i = 0; i < initialSecurity; i++)
            SpawnShip(ShipType.Security);

        Debug.Log($"Spawned initial ships: {initialMerchants} merchants, {initialPirates} pirates, {initialSecurity} security");
    }

    private void SpawnShip(ShipType type)
    {
        var ship = shipSpawner.SpawnShip(type, rng);
        if (ship != null)
            ships.Add(ship);
    }

    private void StopTickLoop()
    {
        if (tickLoop != null)
        {
            StopCoroutine(tickLoop);
            tickLoop = null;
        }
    }

    private IEnumerator TickLoop()
    {
        while (isRunning && !isPaused)
        {
            DoOneTick();

            // Check for max ticks
            if (maxTicks > 0 && tickCount >= maxTicks)
            {
                Debug.Log("Simulation COMPLETED - max ticks reached");
                isRunning = false;
                PrintFinalStats();
                yield break;
            }

            yield return new WaitForSeconds(tickInterval);
        }

        tickLoop = null;
    }

    private void DoOneTick()
    {
        tickCount++;

        // Periodic spawning
        CheckPeriodicSpawns();

        // Run behavior AI for all ships
        foreach (var ship in ships)
        {
            if (ship == null) continue;

            ShipBehavior behavior = ship.GetComponent<ShipBehavior>();
            if (behavior != null)
                behavior.OnBehaviorTick(ships);
        }

        // Move all ships
        for (int i = ships.Count - 1; i >= 0; i--)
        {
            var ship = ships[i];
            if (ship == null)
            {
                ships.RemoveAt(i);
                continue;
            }

            ship.OnTick();

            // Handle state changes
            if (ship.Data != null)
            {
                HandleShipState(ship, i);
            }
        }
    }

    private void HandleShipState(ShipController ship, int index)
    {
        switch (ship.Data.state)
        {
            case ShipState.Exited:
                if (ship.Data.type == ShipType.Cargo)
                    merchantsExited++;
                Destroy(ship.gameObject);
                ships.RemoveAt(index);
                break;

            case ShipState.Captured:
                if (ship.Data.type == ShipType.Cargo && !countedShipIds.Contains(ship.Data.shipId))
                {
                    merchantsCaptured++;
                    countedShipIds.Add(ship.Data.shipId);
                }
                break;

            case ShipState.Sunk:
                if (ship.Data.type == ShipType.Pirate)
                    piratesDefeated++;
                Destroy(ship.gameObject);
                ships.RemoveAt(index);
                break;
        }
    }

    private void CheckPeriodicSpawns()
    {
        if (merchantSpawnInterval > 0)
        {
            ticksSinceLastMerchant++;
            if (ticksSinceLastMerchant >= merchantSpawnInterval)
            {
                SpawnShip(ShipType.Cargo);
                ticksSinceLastMerchant = 0;
            }
        }

        if (pirateSpawnInterval > 0)
        {
            ticksSinceLastPirate++;
            if (ticksSinceLastPirate >= pirateSpawnInterval)
            {
                SpawnShip(ShipType.Pirate);
                ticksSinceLastPirate = 0;
            }
        }

        if (securitySpawnInterval > 0)
        {
            ticksSinceLastSecurity++;
            if (ticksSinceLastSecurity >= securitySpawnInterval)
            {
                SpawnShip(ShipType.Security);
                ticksSinceLastSecurity = 0;
            }
        }
    }

    private void PrintFinalStats()
    {
        Debug.Log("===== FINAL STATISTICS =====");
        Debug.Log($"Total Ticks: {tickCount}");
        Debug.Log($"Merchants Escaped: {merchantsExited}");
        Debug.Log($"Merchants Captured: {merchantsCaptured}");
        Debug.Log($"Pirates Defeated: {piratesDefeated}");
        Debug.Log("============================");
    }
}