using System.Collections;
using UnityEngine;
using TMPro;

public class SimulationManager : MonoBehaviour
{
    // =========================
    // UI REFERENCES (DRAG/DROP)
    // =========================
    [Header("UI Text")]
    public TMP_Text statusText;
    public TMP_Text tickText;

    [Header("UI Panels")]
    public GameObject setupPanel;
    public GameObject runPanel;
    public GameObject resultsPanel;

    [Header("Setup Inputs (TMP Input Fields)")]
    public TMP_InputField durationInput;   // e.g., "200"
    public TMP_InputField seedInput;       // e.g., "12345" (optional)

    [Header("Setup / Error UI")]
    public TMP_Text setupErrorText;        // optional, can be null

    // =========================
    // PREFABS
    // =========================
    [Header("Prefabs")]
    public GameObject cargoPrefab;

    // =========================
    // RUN SETTINGS (DEFAULTS)
    // =========================
    [Header("Run Settings (defaults if setup not used yet)")]
    public int durationTicks = 50;          // Req 2.4 / 1.7
    public float tickInterval = 0.5f;       // Req 2.9 (speed)
    public bool autoSpawnCargo = true;

    // =========================
    // INTERNAL STATE
    // =========================
    private GameObject cargoInstance;
    private Coroutine runCoroutine;

    private int tickCount = 0;
    private bool isRunning = false;
    private bool isPaused = false;

    // Determinism / reproducibility groundwork (Req 5.8 later)
    private int runSeed = 0;
    private bool hasLockedConfig = false;

    // Simple “end state” flag (Req 1.10)
    private bool runEnded = false;

    // =========================
    // UNITY LIFECYCLE
    // =========================
    private void Start()
    {
        // Start in setup mode (Req 1.3)
        ShowSetup();
        SetStatus("SETUP");
        RefreshTickUI();
        ClearSetupError();
    }

    // =========================
    // PUBLIC BUTTON API
    // =========================

    // Called by SetupPanel "Begin Run" button
    public void ConfirmSetupAndStart()
    {
        ClearSetupError();

        // Validate duration
        if (durationInput == null)
        {
            ShowSetupError("Duration input field is not assigned on SimulationManager.");
            return;
        }

        if (!int.TryParse(durationInput.text, out int dur))
        {
            ShowSetupError("Duration must be a whole number (ticks). Example: 200");
            return;
        }

        // Supported bounds (set conservative bounds so you don't freeze WebGL later)
        // You can adjust these later.
        const int MIN_DUR = 1;
        const int MAX_DUR = 5000;

        if (dur < MIN_DUR || dur > MAX_DUR)
        {
            ShowSetupError($"Duration must be between {MIN_DUR} and {MAX_DUR} ticks.");
            return;
        }

        // Seed is optional
        int seed = 0;
        if (seedInput != null && !string.IsNullOrWhiteSpace(seedInput.text))
        {
            if (!int.TryParse(seedInput.text, out seed))
            {
                ShowSetupError("Seed must be a whole number. Example: 12345 (or leave blank).");
                return;
            }
        }
        else
        {
            // If blank, pick a deterministic-ish seed from time (still displayed later if you add a label)
            seed = System.Environment.TickCount;
        }

        // Apply + "lock" config for this run (Req 2.7)
        durationTicks = dur;
        RefreshTickUI(); // Ensures TickText immediately shows the new duration
        runSeed = seed;
        hasLockedConfig = true;

        // Reset run state before beginning a configured run
        ResetRunState(keepCargo: false);
        runEnded = false;

        // Switch to Run UI and start
        ShowRun();
        StartRun();
    }

    // Start OR Resume. (Req 1.4)
    public void StartRun()
    {
        // If already running, do nothing.
        if (isRunning && !isPaused) return;

        // If user terminated/completed, require "New Run" (prevents confusing state)
        if (runEnded)
        {
            // Keep it simple: push them to setup again
            SetStatus("ENDED - NEW RUN REQUIRED");
            ShowResults(); // results viewable end state
            return;
        }

        if (autoSpawnCargo && cargoInstance == null)
            SpawnCargo();

        // If run ended previously by reaching duration, treat Start as invalid until reset
        if (tickCount >= durationTicks)
        {
            CompleteRun();
            return;
        }

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
        // Step is for inspection/testing, so it should work even if paused.
        if (runEnded)
        {
            SetStatus("ENDED - NEW RUN REQUIRED");
            RefreshTickUI();
            return;
        }

        if (autoSpawnCargo && cargoInstance == null)
            SpawnCargo();

        if (tickCount >= durationTicks)
        {
            CompleteRun();
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

        runEnded = true;

        SetStatus("ENDED");
        RefreshTickUI();

        // Show a reviewable end state (Req 1.10)
        ShowResults();
        // Later: populate ResultsPanel summary (Req 3.2)
    }

    // Reset / New Run workflow (Req 2.10)
    public void ResetToNewRun()
    {
        StopTickLoop();
        ResetRunState(keepCargo: false);

        runEnded = false;
        hasLockedConfig = false;

        SetStatus("SETUP");
        RefreshTickUI();
        ClearSetupError();

        ShowSetup();
    }

    // Optional: Hook to a UI slider later (Req 2.9)
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

    // Optional: "Run Again with same settings" (Req 5.8) – stubbed for later UI
    public void RunAgainSameSettings()
    {
        if (!hasLockedConfig)
        {
            // No locked config exists yet; send them to setup.
            ResetToNewRun();
            return;
        }

        StopTickLoop();
        ResetRunState(keepCargo: false);

        runEnded = false;

        ShowRun();
        StartRun();
    }

    // =========================
    // CORE SIM LOOP (DETERMINISTIC STRUCTURE)
    // =========================
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
        tickCount++;

        // Canonical 1-tick behavior (currently just cargo moves right)
        var mover = cargoInstance.GetComponent<GridMover>();
        mover.Move(Vector2Int.right);

        // Later: update live metrics here (Req 3.1)
        // Later: move all entities here (pirates/security/merchants)
        // Later: handle captures/exits here
    }

    private void CompleteRun()
    {
        StopTickLoop();
        isRunning = false;
        isPaused = false;

        runEnded = true;

        SetStatus("COMPLETED");
        RefreshTickUI();

        ShowResults();
        // Later: ResultsPanel summary (Req 3.2)
    }

    // =========================
    // SPAWN / RESET
    // =========================
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

    // =========================
    // UI HELPERS
    // =========================
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

    private void ShowSetup()
    {
        if (setupPanel != null) setupPanel.SetActive(true);
        if (runPanel != null) runPanel.SetActive(false);
        if (resultsPanel != null) resultsPanel.SetActive(false);
    }

    private void ShowRun()
    {
        if (setupPanel != null) setupPanel.SetActive(false);
        if (runPanel != null) runPanel.SetActive(true);
        if (resultsPanel != null) resultsPanel.SetActive(false);
    }

    private void ShowResults()
    {
        if (setupPanel != null) setupPanel.SetActive(false);
        if (runPanel != null) runPanel.SetActive(false);
        if (resultsPanel != null) resultsPanel.SetActive(true);
    }

    private void ShowSetupError(string msg)
    {
        Debug.LogWarning(msg);
        if (setupErrorText != null)
            setupErrorText.text = msg;
    }

    private void ClearSetupError()
    {
        if (setupErrorText != null)
            setupErrorText.text = "";
    }
}
