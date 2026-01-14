using System.Collections;
using UnityEngine;
using TMPro;

public class SimulationManager : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text statusText;
    public TMP_Text tickText;

    [Header("Prefabs")]
    public GameObject cargoPrefab;

    [Header("Run Settings (temporary defaults)")]
    public int durationTicks = 50;        // Req 2.4 / 1.7
    public float tickInterval = 0.5f;     // Req 2.9 (speed)
    public bool autoSpawnCargo = true;

    // Internal state
    private GameObject cargoInstance;
    private Coroutine runCoroutine;

    private int tickCount = 0;
    private bool isRunning = false;
    private bool isPaused = false;

    // ---- Public API (wired to buttons) ----

    // Start OR Resume. (Req 1.4)
    public void StartRun()
    {
        // If already running, do nothing.
        if (isRunning && !isPaused) return;

        if (autoSpawnCargo && cargoInstance == null)
            SpawnCargo();

        // If run ended previously, restarting should be a "new run" in the future.
        // For now, if tickCount >= durationTicks, reset.
        if (tickCount >= durationTicks)
            ResetRunState(keepCargo: false);

        isRunning = true;
        isPaused = false;
        SetStatus("RUNNING");

        StartTickLoop();
        RefreshTickUI();
    }

    // Pause. (Req 1.4)
    public void PauseRun()
    {
        if (!isRunning) return;

        isPaused = true;
        SetStatus("PAUSED");
        StopTickLoop();
        RefreshTickUI();
    }

    // Single-step exactly one tick. (Req 2.8)
    public void StepOnce()
    {
        // Step should not be blocked by pause/running logic — it’s inspection.
        if (autoSpawnCargo && cargoInstance == null)
            SpawnCargo();

        // If run already completed, stepping does nothing (or you could reset).
        if (tickCount >= durationTicks)
        {
            SetStatus("COMPLETED");
            RefreshTickUI();
            return;
        }

        AdvanceTick();
        SetStatus("STEP");
        RefreshTickUI();
    }

    // Terminate / End (Req 1.4 / 1.10)
    public void EndRun()
    {
        StopTickLoop();
        isRunning = false;
        isPaused = false;

        SetStatus("ENDED");
        RefreshTickUI();

        // Later: show ResultsPanel here (Req 3.2 / 1.10)
    }

    // Reset/New Run workflow (Req 2.10)
    public void ResetToNewRun()
    {
        StopTickLoop();
        ResetRunState(keepCargo: false);
        SetStatus("SETUP");
        RefreshTickUI();
    }

    // Optional: Hook this to a UI slider later (Req 2.9)
    public void SetTickInterval(float seconds)
    {
        tickInterval = Mathf.Max(0.01f, seconds);

        // If currently running, restart loop to apply new speed immediately.
        if (isRunning && !isPaused)
        {
            StopTickLoop();
            StartTickLoop();
        }
    }

    // ---- Core simulation loop (deterministic) ----

    private void StartTickLoop()
    {
        if (runCoroutine != null) return;
        runCoroutine = StartCoroutine(RunLoop());
    }

    private void StopTickLoop()
    {
        if (runCoroutine != null)
        {
            StopCoroutine(runCoroutine);
            runCoroutine = null;
        }
    }

    private IEnumerator RunLoop()
    {
        while (isRunning && !isPaused)
        {
            // Stop when completed
            if (tickCount >= durationTicks)
            {
                CompleteRun();
                yield break;
            }

            AdvanceTick();
            RefreshTickUI();

            yield return new WaitForSeconds(tickInterval);
        }

        runCoroutine = null;
    }

    private void AdvanceTick()
    {
        // Canonical “one tick” function.
        tickCount++;

        var mover = cargoInstance.GetComponent<GridMover>();
        mover.Move(Vector2Int.right);

        // Later: update metrics here (Req 3.1)
        // Later: move pirates/security/merchants here
        // Later: handle captures/exits here
    }

    private void CompleteRun()
    {
        StopTickLoop();
        isRunning = false;
        isPaused = false;
        SetStatus("COMPLETED"); // Req 1.10 end state
        RefreshTickUI();

        // Later: ResultsPanel summary (Req 3.2)
    }

    // ---- Spawning / Reset ----

    private void SpawnCargo()
    {
        cargoInstance = Instantiate(cargoPrefab);
        var mover = cargoInstance.GetComponent<GridMover>();
        mover.SetPosition(new Vector2Int(0, 0));
    }

    private void ResetRunState(bool keepCargo)
    {
        tickCount = 0;
        isRunning = false;
        isPaused = false;

        if (!keepCargo && cargoInstance != null)
        {
            Destroy(cargoInstance);
            cargoInstance = null;
        }
    }

    // ---- UI helpers ----

    private void RefreshTickUI()
    {
        if (tickText != null)
            tickText.text = $"Tick: {tickCount} / {durationTicks}";
    }

    private void SetStatus(string msg)
    {
        if (statusText != null)
            statusText.text = msg;

        Debug.Log(msg);
    }
}
