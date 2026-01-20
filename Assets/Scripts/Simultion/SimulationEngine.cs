using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class SimulationEngine : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text statusText;
    public TMP_Text tickText;
    public TMP_Text statsText;  // NEW: for showing ship counts

    [Header("Run Settings")]
    public float tickInterval = 0.25f;  // adjustable speed (Req 2.9)
    public int maxTicks = 0;            // 0 = endless, otherwise timed (Req 2.4)
    public int runSeed = 12345;         // determinism (Req 5.8 future)

    [Header("Spawn Settings")]
    [Tooltip("Ticks between merchant spawns (0 = only spawn at start)")]
    public int merchantSpawnInterval = 50;
    [Tooltip("Ticks between pirate spawns (0 = only spawn at start)")]
    public int pirateSpawnInterval = 80;
    [Tooltip("Ticks between security spawns (0 = only spawn at start)")]
    public int securitySpawnInterval = 100;

    [Header("Initial Ships")]
    public int initialMerchants = 1;
    public int initialPirates = 1;
    public int initialSecurity = 1;

    [Header("References")]
    public ShipSpawner shipSpawner;

    // runtime
    private readonly List<ShipController> ships = new();
    private Coroutine loop;
    private bool isRunning;
    private bool isPaused;
    private int tickCount;
    private System.Random rng;

    // Spawn timers
    private int ticksSinceLastMerchant = 0;
    private int ticksSinceLastPirate = 0;
    private int ticksSinceLastSecurity = 0;

    // Statistics (Req 3.1)
    private int totalMerchantsSpawned = 0;
    private int totalPiratesSpawned = 0;
    private int totalSecuritySpawned = 0;
    private int merchantsExited = 0;
    private int merchantsCaptured = 0;
    private int piratesDefeated = 0;

    // ---- Public API: wire buttons to these ----

    // Start or Resume (Req 1.4)
    public void StartRun()
    {
        if (isRunning && !isPaused) return;

        if (rng == null) rng = new System.Random(runSeed);

        // Spawn initial ships if none exist yet
        if (ships.Count == 0 && shipSpawner != null)
        {
            SpawnInitialShips();
        }

        isRunning = true;
        isPaused = false;
        SetStatus("RUNNING");
        StartLoop();
        RefreshUI();
    }

    // Pause (Req 1.4)
    public void PauseRun()
    {
        if (!isRunning) return;
        isPaused = true;
        SetStatus("PAUSED");
        StopLoop();
        RefreshUI();
    }

    // Step (Req 2.8)
    public void StepOnce()
    {
        // stepping should work even if paused/not running
        if (rng == null) rng = new System.Random(runSeed);

        if (ships.Count == 0 && shipSpawner != null)
        {
            SpawnInitialShips();
        }

        AdvanceTick();
        SetStatus("STEP");
        RefreshUI();
    }

    // End (Req 1.10)
    public void EndRun()
    {
        StopLoop();
        isRunning = false;
        isPaused = false;
        SetStatus("ENDED");
        RefreshUI();
        PrintFinalStats();
    }

    // Reset / New Run workflow (Req 2.10)
    public void ResetToNewRun()
    {
        StopLoop();
        isRunning = false;
        isPaused = false;
        tickCount = 0;

        // Reset spawn timers
        ticksSinceLastMerchant = 0;
        ticksSinceLastPirate = 0;
        ticksSinceLastSecurity = 0;

        // Reset statistics
        totalMerchantsSpawned = 0;
        totalPiratesSpawned = 0;
        totalSecuritySpawned = 0;
        merchantsExited = 0;
        merchantsCaptured = 0;
        piratesDefeated = 0;
        countedShipIds.Clear();

        // Destroy ships
        foreach (var s in ships)
            if (s != null) Destroy(s.gameObject);
        ships.Clear();

        // Also clear from spawner if it tracks them
        if (shipSpawner != null)
            shipSpawner.ClearAllShips();

        rng = null;
        SetStatus("SETUP");
        RefreshUI();
    }

    // Optional: speed slider hook (Req 2.9)
    public void SetTickInterval(float seconds)
    {
        tickInterval = Mathf.Max(0.01f, seconds);
        if (isRunning && !isPaused)
        {
            StopLoop();
            StartLoop();
        }
    }

    // Optional: allow UI input to set max ticks (0=endless)
    public void SetMaxTicks(int ticks)
    {
        maxTicks = Mathf.Max(0, ticks);
        RefreshUI();
    }

    public void SetSeed(int seed)
    {
        runSeed = seed;
        // don't recreate rng mid-run; apply on next Reset/Start
    }

    // ---- Spawning ----

    private void SpawnInitialShips()
    {
        // Spawn initial merchants
        for (int i = 0; i < initialMerchants; i++)
        {
            SpawnMerchant();
        }

        // Spawn initial pirates
        for (int i = 0; i < initialPirates; i++)
        {
            SpawnPirate();
        }

        // Spawn initial security
        for (int i = 0; i < initialSecurity; i++)
        {
            SpawnSecurity();
        }
    }

    private void SpawnMerchant()
    {
        var ship = shipSpawner.SpawnShip(ShipType.Cargo, rng);
        if (ship != null)
        {
            ships.Add(ship);
            totalMerchantsSpawned++;
        }
    }

    private void SpawnPirate()
    {
        var ship = shipSpawner.SpawnShip(ShipType.Pirate, rng);
        if (ship != null)
        {
            ships.Add(ship);
            totalPiratesSpawned++;
        }
    }

    private void SpawnSecurity()
    {
        var ship = shipSpawner.SpawnShip(ShipType.Security, rng);
        if (ship != null)
        {
            ships.Add(ship);
            totalSecuritySpawned++;
        }
    }

    private void CheckPeriodicSpawns()
    {
        // Merchant spawning
        if (merchantSpawnInterval > 0)
        {
            ticksSinceLastMerchant++;
            if (ticksSinceLastMerchant >= merchantSpawnInterval)
            {
                SpawnMerchant();
                ticksSinceLastMerchant = 0;
            }
        }

        // Pirate spawning
        if (pirateSpawnInterval > 0)
        {
            ticksSinceLastPirate++;
            if (ticksSinceLastPirate >= pirateSpawnInterval)
            {
                SpawnPirate();
                ticksSinceLastPirate = 0;
            }
        }

        // Security spawning
        if (securitySpawnInterval > 0)
        {
            ticksSinceLastSecurity++;
            if (ticksSinceLastSecurity >= securitySpawnInterval)
            {
                SpawnSecurity();
                ticksSinceLastSecurity = 0;
            }
        }
    }

    // ---- loop ----

    private void StartLoop()
    {
        if (loop != null) return;
        loop = StartCoroutine(TickLoop());
    }

    private void StopLoop()
    {
        if (loop != null)
        {
            StopCoroutine(loop);
            loop = null;
        }
    }

    private IEnumerator TickLoop()
    {
        while (isRunning && !isPaused)
        {
            AdvanceTick();
            RefreshUI();

            // If timed mode: stop after maxTicks
            if (maxTicks > 0 && tickCount >= maxTicks)
            {
                SetStatus("COMPLETED");
                isRunning = false;
                isPaused = false;
                PrintFinalStats();
                loop = null;
                yield break;
            }

            yield return new WaitForSeconds(tickInterval);
        }

        loop = null;
    }

    private void AdvanceTick()
    {
        tickCount++;

        // Check if we should spawn new ships
        CheckPeriodicSpawns();

        // First: Run behavior AI for all ships (detection, chasing, fleeing)
        foreach (var ship in ships)
        {
            if (ship == null) continue;

            ShipBehavior behavior = ship.GetComponent<ShipBehavior>();
            if (behavior != null)
            {
                behavior.OnBehaviorTick(ships);
            }
        }

        // Second: Tick all ships (movement)
        for (int i = ships.Count - 1; i >= 0; i--)
        {
            var ship = ships[i];
            if (ship == null)
            {
                ships.RemoveAt(i);
                continue;
            }

            ship.OnTick();

            // Handle ships based on their state
            if (ship.Data != null)
            {
                switch (ship.Data.state)
                {
                    case ShipState.Exited:
                        if (ship.Data.type == ShipType.Cargo)
                            merchantsExited++;
                        Destroy(ship.gameObject);
                        ships.RemoveAt(i);
                        break;

                    case ShipState.Captured:
                        // Only count once (check if not already counted)
                        if (ship.Data.type == ShipType.Cargo && !IsShipAlreadyCounted(ship))
                        {
                            merchantsCaptured++;
                            MarkShipCounted(ship);
                        }
                        // Don't destroy - captured ships might be rescued
                        break;

                    case ShipState.Sunk:
                        if (ship.Data.type == ShipType.Pirate)
                            piratesDefeated++;
                        Destroy(ship.gameObject);
                        ships.RemoveAt(i);
                        break;
                }
            }
        }
    }

    // Track which ships have been counted to avoid double-counting
    private HashSet<string> countedShipIds = new HashSet<string>();

    private bool IsShipAlreadyCounted(ShipController ship)
    {
        return countedShipIds.Contains(ship.Data.shipId);
    }

    private void MarkShipCounted(ShipController ship)
    {
        countedShipIds.Add(ship.Data.shipId);
    }

    private void RefreshUI()
    {
        if (tickText != null)
        {
            string mode = (maxTicks > 0) ? $"{tickCount} / {maxTicks}" : $"{tickCount} (endless)";
            tickText.text = $"Tick: {mode}";
        }

        if (statsText != null)
        {
            int activeMerchants = CountShipsOfType(ShipType.Cargo);
            int activePirates = CountShipsOfType(ShipType.Pirate);
            int activeSecurity = CountShipsOfType(ShipType.Security);

            statsText.text = $"Merchants: {activeMerchants} | Pirates: {activePirates} | Security: {activeSecurity}\n" +
                           $"Exited: {merchantsExited} | Captured: {merchantsCaptured} | Pirates Defeated: {piratesDefeated}";
        }
    }

    private int CountShipsOfType(ShipType type)
    {
        int count = 0;
        foreach (var ship in ships)
        {
            if (ship != null && ship.Data != null && ship.Data.type == type)
                count++;
        }
        return count;
    }

    private void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
        Debug.Log(msg);
    }

    private void PrintFinalStats()
    {
        Debug.Log("=== SIMULATION RESULTS ===");
        Debug.Log($"Total Ticks: {tickCount}");
        Debug.Log($"Merchants Spawned: {totalMerchantsSpawned}");
        Debug.Log($"Merchants Exited Safely: {merchantsExited}");
        Debug.Log($"Merchants Captured: {merchantsCaptured}");
        Debug.Log($"Pirates Spawned: {totalPiratesSpawned}");
        Debug.Log($"Pirates Defeated: {piratesDefeated}");
        Debug.Log($"Security Spawned: {totalSecuritySpawned}");
        Debug.Log("==========================");
    }

    // ---- Public Getters for UI/Statistics ----

    public int GetTickCount() => tickCount;
    public int GetMerchantsExited() => merchantsExited;
    public int GetMerchantsCaptured() => merchantsCaptured;
    public int GetPiratesDefeated() => piratesDefeated;
    public bool IsRunning() => isRunning;
    public bool IsPaused() => isPaused;
}