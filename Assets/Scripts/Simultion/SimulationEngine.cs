using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SIMULATION ENGINE - High-Performance Multi-Tick System
/// 
/// KEY CHANGES FROM ORIGINAL:
/// 1. Update()-based tick loop instead of coroutine (no WaitForSeconds ceiling)
/// 2. Runs MULTIPLE ticks per frame at high speeds (200x+ supported)
/// 3. Real-time correlation: 1 tick = 15 sim-minutes by default
/// 4. Time budget per frame prevents freezing at extreme speeds
/// 5. Applies environment multipliers to spawn rates, speeds, detection
/// 
/// REAL-WORLD TIME SCALE:
/// - 1 tick = 15 simulated minutes (configurable via simMinutesPerTick)
/// - At 1x speed: ~4 ticks/sec = 1 sim-hour per second
/// - At 200x: ~800 ticks/sec = 200 sim-hours per second
/// 
/// MAP SCALE (Malacca baseline):
/// - 1 Unity unit ≈ 1.44 km
/// - Cargo ship (13 kn): ~4.2 units/tick
/// - Pirate skiff (28 kn): ~9.0 units/tick  
/// - Naval patrol (20 kn): ~6.4 units/tick
/// </summary>
public class SimulationEngine : MonoBehaviour
{
    [Header("=== REFERENCES ===")]
    public ShipSpawner shipSpawner;

    [Header("=== TIME SCALE ===")]
    [Tooltip("Simulated minutes per tick. 15 = each tick is 15 minutes of real time.")]
    public float simMinutesPerTick = 15f;

    [Tooltip("Target ticks per second at 1x speed")]
    public float baseTicksPerSecond = 4f;

    [Tooltip("Current speed multiplier (1x, 10x, 200x, etc.)")]
    public float speedMultiplier = 1f;

    [Tooltip("Max milliseconds to spend on ticks per frame (prevents freezing)")]
    public float maxTickBudgetMs = 12f; // ~12ms leaves room for rendering at 60fps

    [Header("=== RUN SETTINGS ===")]
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
    private System.Random rng;

    private bool isRunning = false;
    private bool isPaused = false;
    private int tickCount = 0;

    // Multi-tick accumulator
    private float tickAccumulator = 0f;

    // Spawn timers
    private int ticksSinceLastMerchant = 0;
    private int ticksSinceLastPirate = 0;
    private int ticksSinceLastSecurity = 0;

    // Statistics
    private int merchantsExited = 0;
    private int merchantsCaptured = 0;
    private int piratesDefeated = 0;
    private HashSet<string> countedShipIds = new HashSet<string>();

    // Performance monitoring
    private int ticksThisFrame = 0;
    private int lastFrameTickCount = 0;

    // ===== PUBLIC CONTROL METHODS =====

    public void StartRun()
    {
        if (isRunning && !isPaused) return;

        if (rng == null)
        {
            rng = new System.Random(runSeed);
            ApplyEnvironmentMultipliers();
        }

        if (ships.Count == 0)
            SpawnInitialShips();

        isRunning = true;
        isPaused = false;
        tickAccumulator = 0f;

        Debug.Log($"Simulation STARTED (speed: {speedMultiplier}x, {simMinutesPerTick} min/tick)");
    }

    public void PauseRun()
    {
        if (!isRunning) return;
        isPaused = true;
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
        isRunning = false;
        isPaused = false;
        Debug.Log("Simulation ENDED");
        PrintFinalStats();
    }

    public void ResetToNewRun()
    {
        isRunning = false;
        isPaused = false;
        tickCount = 0;
        rng = null;
        tickAccumulator = 0f;

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

        ShipBehavior.ResetSharedData();
        CoastalDefense.ResetAllBatteries();

        Debug.Log("Simulation RESET");
    }

    /// <summary>
    /// Set speed multiplier directly (replaces SetTickInterval).
    /// 1 = real-time-ish, 100 = 100x faster, 200 = 200x faster.
    /// </summary>
    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = Mathf.Max(0.1f, multiplier);
    }

    /// <summary>
    /// LEGACY COMPATIBILITY: SetTickInterval still works.
    /// Converts interval to speed multiplier internally.
    /// tickInterval of 0.25 = 4 ticks/sec = 1x speed (at baseTicksPerSecond=4)
    /// </summary>
    public void SetTickInterval(float seconds)
    {
        float ticksPerSec = 1f / Mathf.Max(0.001f, seconds);
        speedMultiplier = ticksPerSec / baseTicksPerSecond;
        speedMultiplier = Mathf.Max(0.1f, speedMultiplier);
    }

    // ===== UPDATE-BASED TICK LOOP =====

    private void Update()
    {
        if (!isRunning || isPaused) return;

        // Calculate how many ticks should happen this frame
        float targetTicksPerSecond = baseTicksPerSecond * speedMultiplier;
        tickAccumulator += targetTicksPerSecond * Time.deltaTime;

        // Clamp accumulator to prevent spiral of death after lag spikes
        // At 200x with base 4 tps = 800 tps target. At 60fps that's ~13 ticks/frame.
        // Cap at something reasonable to prevent freezing.
        float maxTicksPerFrame = targetTicksPerSecond / 30f; // assume minimum 30fps
        maxTicksPerFrame = Mathf.Max(maxTicksPerFrame, 1f);   // always at least 1
        tickAccumulator = Mathf.Min(tickAccumulator, maxTicksPerFrame * 2f); // allow 2x burst

        // Run ticks with time budget
        ticksThisFrame = 0;
        float frameStartTime = Time.realtimeSinceStartup * 1000f; // ms

        while (tickAccumulator >= 1f)
        {
            DoOneTick();
            tickAccumulator -= 1f;
            ticksThisFrame++;

            // Check max ticks
            if (maxTicks > 0 && tickCount >= maxTicks)
            {
                Debug.Log("Simulation COMPLETED - max ticks reached");
                isRunning = false;
                PrintFinalStats();
                return;
            }

            // Time budget check - don't freeze the game
            float elapsed = (Time.realtimeSinceStartup * 1000f) - frameStartTime;
            if (elapsed >= maxTickBudgetMs)
            {
                // We've used our budget. Remaining ticks carry over to next frame.
                break;
            }
        }

        lastFrameTickCount = ticksThisFrame;
    }

    // ===== ENVIRONMENT MULTIPLIERS =====

    private void ApplyEnvironmentMultipliers()
    {
        float merchantMult = 1f;
        float pirateMult = 1f;
        float securityMult = 1f;

        if (EnvironmentSettings.Instance != null)
        {
            EnvironmentSettings.Instance.CalculateMultipliers();

            merchantMult = EnvironmentSettings.Instance.MerchantSpawnMultiplier;
            pirateMult = EnvironmentSettings.Instance.PirateSpawnMultiplier;
            securityMult = EnvironmentSettings.Instance.SecuritySpawnMultiplier;

            Debug.Log($"Environment multipliers applied - Merchant: {merchantMult:F2}, Pirate: {pirateMult:F2}, Security: {securityMult:F2}");
        }

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
    public float GetSpeedMultiplier() => speedMultiplier;
    public int GetTicksLastFrame() => lastFrameTickCount;

    /// <summary>
    /// Get the current simulation time as a formatted string.
    /// Based on simMinutesPerTick and total ticks elapsed.
    /// </summary>
    public float GetSimulatedHours()
    {
        return (tickCount * simMinutesPerTick) / 60f;
    }

    public float GetSimulatedDays()
    {
        return GetSimulatedHours() / 24f;
    }

    /// <summary>
    /// Get effective ticks per second (actual measured rate).
    /// Useful for performance monitoring.
    /// </summary>
    public float GetEffectiveTicksPerSecond()
    {
        if (!isRunning || isPaused) return 0f;
        return lastFrameTickCount / Mathf.Max(Time.deltaTime, 0.001f);
    }

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
                ships.RemoveAt(index);
                break;

            case ShipState.Sunk:
                if (ship.Data.type == ShipType.Pirate && !countedShipIds.Contains(ship.Data.shipId))
                {
                    piratesDefeated++;
                    countedShipIds.Add(ship.Data.shipId);
                }
                ships.RemoveAt(index);
                break;
        }
    }

    private void CheckPeriodicSpawns()
    {
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
        Debug.Log($"Simulated Time: {GetSimulatedHours():F1} hours ({GetSimulatedDays():F1} days)");
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