using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class SimulationEngine : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text statusText;
    public TMP_Text tickText;

    [Header("Run Settings")]
    public float tickInterval = 0.25f;  // adjustable speed (Req 2.9)
    public int maxTicks = 0;            // 0 = endless, otherwise timed (Req 2.4)
    public int runSeed = 12345;         // determinism (Req 5.8 future)

    [Header("References")]
    public ShipSpawner shipSpawner;

    // runtime
    private readonly List<ShipController> ships = new();
    private Coroutine loop;
    private bool isRunning;
    private bool isPaused;
    private int tickCount;
    private System.Random rng;

    // ---- Public API: wire buttons to these ----

    // Start or Resume (Req 1.4)
    public void StartRun()
    {
        if (isRunning && !isPaused) return;

        if (rng == null) rng = new System.Random(runSeed);

        // Spawn at least one ship if none exist yet
        if (ships.Count == 0 && shipSpawner != null)
        {
            var cargo = shipSpawner.SpawnCargo(rng, "1");
            if (cargo != null) ships.Add(cargo);
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
            var cargo = shipSpawner.SpawnCargo(rng, "1");
            if (cargo != null) ships.Add(cargo);
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

        // Later: show ResultsPanel summary (Req 3.2)
    }

    // Reset / New Run workflow (Req 2.10)
    public void ResetToNewRun()
    {
        StopLoop();
        isRunning = false;
        isPaused = false;
        tickCount = 0;

        // destroy ships
        foreach (var s in ships)
            if (s != null) Destroy(s.gameObject);
        ships.Clear();

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

        // tick all ships
        for (int i = ships.Count - 1; i >= 0; i--)
        {
            var ship = ships[i];
            if (ship == null)
            {
                ships.RemoveAt(i);
                continue;
            }

            ship.OnTick();

            // remove ships that exited (optional cleanup)
            if (ship.Data != null && ship.Data.state == ShipState.Exited)
            {
                Destroy(ship.gameObject);
                ships.RemoveAt(i);
            }
        }
    }

    private void RefreshUI()
    {
        if (tickText != null)
        {
            string mode = (maxTicks > 0) ? $"{tickCount} / {maxTicks}" : $"{tickCount} (endless)";
            tickText.text = $"Tick: {mode}";
        }
    }

    private void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
        Debug.Log(msg);
    }
}
