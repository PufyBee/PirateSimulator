using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SIMULATION ENGINE - With Environment Effects Support
/// 
/// Applies Time of Day and Weather multipliers to:
/// - Ship spawn rates
/// - Ship speeds (via ShipSpawner)
/// - Detection ranges (via ShipBehavior)
/// 
/// Also resets shared AI data (hotspots, distress calls) on reset.
/// </summary>
public class SimulationEngine : MonoBehaviour
{
    [Header("=== REFERENCES ===")]
    public ShipSpawner shipSpawner;

    [Header("=== RUN SETTINGS ===")]
    public float tickInterval = 0.25f;
    public int maxTicks = 0;
    public int runSeed = 12345;

    [Header("=== INITIAL SHIPS (Base Values) ===")]
    public int initialMerchants = 2;
    public int initialPirates = 1;
    public int initialSecurity = 1;

    [Header("=== SPAWN INTERVALS (Base Values - Ticks) ===")]
    [Tooltip("Base ticks between merchant spawns (0 = disabled)")]
    public int merchantSpawnInterval = 50;
    [Tooltip("Base ticks between pirate spawns (0 = disabled)")]
    public int pirateSpawnInterval = 80;
    [Tooltip("Base ticks between security spawns (0 = disabled)")]
    public int securitySpawnInterval = 100;

    // Adjusted spawn intervals (after environment multipliers)
    private int adjustedMerchantInterval;
    private int adjustedPirateInterval;
    private int adjustedSecurityInterval;

    // Runtime state
    private List<ShipController> ships = new List<ShipController>();
    private Coroutine tickLoop;
    private System.Random rng;

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

    public void StartRun()
    {
        if (isRunning && !isPaused) return;

        if (rng == null)
        {
            rng = new System.Random(runSeed);
            
            // Apply environment multipliers at start of run
            ApplyEnvironmentMultipliers();
        }

        if (ships.Count == 0)
            SpawnInitialShips();

        isRunning = true;
        isPaused = false;

        if (tickLoop == null)
            tickLoop = StartCoroutine(TickLoop());

        Debug.Log("Simulation STARTED");
    }

    public void PauseRun()
    {
        if (!isRunning) return;
        
        isPaused = true;
        StopTickLoop();
        
        Debug.Log("Simulation PAUSED");
    }

    public void StepOnce()
    {
        if (rng == null)
        {
            rng = new System.Random(runSeed);
            ApplyEnvironmentMultipliers();
        }

        if (ships.Count == 0)
            SpawnInitialShips();

        DoOneTick();
        Debug.Log($"Simulation STEP - Tick {tickCount}");
    }

    public void EndRun()
    {
        StopTickLoop();
        isRunning = false;
        isPaused = false;
        
        Debug.Log("Simulation ENDED");
        PrintFinalStats();
    }

    public void ResetToNewRun()
    {
        StopTickLoop();
        
        isRunning = false;
        isPaused = false;
        tickCount = 0;
        rng = null;

        ticksSinceLastMerchant = 0;
        ticksSinceLastPirate = 0;
        ticksSinceLastSecurity = 0;

        merchantsExited = 0;
        merchantsCaptured = 0;
        piratesDefeated = 0;
        countedShipIds.Clear();

        foreach (var ship in ships)
        {
            if (ship != null)
                Destroy(ship.gameObject);
        }
        ships.Clear();

        if (shipSpawner != null)
            shipSpawner.ClearAllShips();

        // Reset shared AI data (hotspots, distress calls)
        ShipBehavior.ResetSharedData();

        // Reset coastal defenses
        CoastalDefense.ResetAllBatteries();

        Debug.Log("Simulation RESET");
    }

    public void SetTickInterval(float seconds)
    {
        tickInterval = Mathf.Max(0.01f, seconds);
        
        if (isRunning && !isPaused)
        {
            StopTickLoop();
            tickLoop = StartCoroutine(TickLoop());
        }
    }

    // ===== ENVIRONMENT MULTIPLIERS =====

    private void ApplyEnvironmentMultipliers()
    {
        // Get multipliers from EnvironmentSettings (if it exists)
        float merchantMult = 1f;
        float pirateMult = 1f;
        float securityMult = 1f;

        if (EnvironmentSettings.Instance != null)
        {
            // Recalculate multipliers based on current settings
            EnvironmentSettings.Instance.CalculateMultipliers();

            merchantMult = EnvironmentSettings.Instance.MerchantSpawnMultiplier;
            pirateMult = EnvironmentSettings.Instance.PirateSpawnMultiplier;
            securityMult = EnvironmentSettings.Instance.SecuritySpawnMultiplier;

            Debug.Log($"Environment multipliers applied - Merchant: {merchantMult:F2}, Pirate: {pirateMult:F2}, Security: {securityMult:F2}");
        }

        // Apply to spawn intervals (higher multiplier = more spawns = shorter interval)
        // Avoid division by zero
        adjustedMerchantInterval = merchantSpawnInterval > 0 
            ? Mathf.Max(1, Mathf.RoundToInt(merchantSpawnInterval / merchantMult)) 
            : 0;
        
        adjustedPirateInterval = pirateSpawnInterval > 0 
            ? Mathf.Max(1, Mathf.RoundToInt(pirateSpawnInterval / pirateMult)) 
            : 0;
        
        adjustedSecurityInterval = securitySpawnInterval > 0 
            ? Mathf.Max(1, Mathf.RoundToInt(securitySpawnInterval / securityMult)) 
            : 0;

        Debug.Log($"Adjusted spawn intervals - Merchant: {adjustedMerchantInterval}, Pirate: {adjustedPirateInterval}, Security: {adjustedSecurityInterval}");

        // Apply speed multiplier to ShipSpawner
        if (shipSpawner != null && EnvironmentSettings.Instance != null)
        {
            float speedMult = EnvironmentSettings.Instance.SpeedMultiplier;
            shipSpawner.merchantSpeed *= speedMult;
            shipSpawner.pirateSpeed *= speedMult;
            shipSpawner.securitySpeed *= speedMult;
        }
    }

    // ===== PUBLIC GETTERS =====

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
        {
            ships.Add(ship);

            // Apply detection multiplier to behavior
            if (EnvironmentSettings.Instance != null)
            {
                ShipBehavior behavior = ship.GetComponent<ShipBehavior>();
                if (behavior != null)
                {
                    behavior.detectionRange *= EnvironmentSettings.Instance.DetectionMultiplier;
                }
            }
        }
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

        CheckPeriodicSpawns();

        // Run behavior AI
        foreach (var ship in ships)
        {
            if (ship == null) continue;

            ShipBehavior behavior = ship.GetComponent<ShipBehavior>();
            if (behavior != null)
                behavior.OnBehaviorTick(ships);
        }

        // Move ships
        for (int i = ships.Count - 1; i >= 0; i--)
        {
            var ship = ships[i];
            if (ship == null)
            {
                ships.RemoveAt(i);
                continue;
            }

            ship.OnTick();

            if (ship.Data != null)
                HandleShipState(ship, i);
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
                // DON'T destroy - visual effect handles it
                // Just remove from active list so it doesn't get ticked
                ships.RemoveAt(index);
                break;

            case ShipState.Sunk:
                if (ship.Data.type == ShipType.Pirate && !countedShipIds.Contains(ship.Data.shipId))
                {
                    piratesDefeated++;
                    countedShipIds.Add(ship.Data.shipId);
                }
                // DON'T destroy - visual effect handles it
                // Just remove from active list so it doesn't get ticked
                ships.RemoveAt(index);
                break;
        }
    }

    private void CheckPeriodicSpawns()
    {
        // Use adjusted intervals (with environment multipliers applied)
        if (adjustedMerchantInterval > 0)
        {
            ticksSinceLastMerchant++;
            if (ticksSinceLastMerchant >= adjustedMerchantInterval)
            {
                SpawnShip(ShipType.Cargo);
                ticksSinceLastMerchant = 0;
            }
        }

        if (adjustedPirateInterval > 0)
        {
            ticksSinceLastPirate++;
            if (ticksSinceLastPirate >= adjustedPirateInterval)
            {
                SpawnShip(ShipType.Pirate);
                ticksSinceLastPirate = 0;
            }
        }

        if (adjustedSecurityInterval > 0)
        {
            ticksSinceLastSecurity++;
            if (ticksSinceLastSecurity >= adjustedSecurityInterval)
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
        
        if (EnvironmentSettings.Instance != null)
        {
            Debug.Log($"Conditions: {EnvironmentSettings.Instance.GetConditionsSummary()}");
        }
        
        Debug.Log("============================");
    }
}