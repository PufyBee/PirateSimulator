using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI Controller - Updated to work with new UICanvas design
/// 
/// REQUIREMENTS COVERED:
/// - Rqmt04: Run Controls (Start/Pause/Resume/Reset)
/// - Rqmt17: Lock Config on Start
/// - Rqmt18: Single-Step Execution
/// - Rqmt19: Adjustable Speed (slider OR buttons)
/// - Rqmt25: Condition Indicator (runtime badge)
/// - Rqmt26: Prevent Invalid Actions (button states)
/// 
/// WIRING GUIDE (Inspector):
/// ─────────────────────────
/// startBtn          → UICanvas > Start Button (Root) > Start Button (Shadow) > Start Button (Button)
/// pauseBtn          → UICanvas > Main Controls (Root) > Pause Button (Button)
/// stepBtn           → UICanvas > Main Controls (Root) > Step Button (Button)
/// resetBtn          → UICanvas > Main Controls (Root) > Restart Button (Button)
/// 
/// speedSlider       → (Optional) Any UI Slider for speed control
/// speedUpBtn        → (Optional) Button to increase speed
/// speedDownBtn      → (Optional) Button to decrease speed
/// speedValueText    → (Optional) Text showing current speed
/// 
/// conditionBadge    → (Optional) Text element to show "Morning / Clear" during run
/// </summary>
public class SimpleButtons : MonoBehaviour
{
    [Header("=== BUTTONS ===")]
    public Button startBtn;
    public Button pauseBtn;
    public Button stepBtn;
    public Button resetBtn;

    [Header("=== PAUSE BUTTON TEXT ===")]
    public TMP_Text pauseButtonText;

    [Header("=== REFERENCES ===")]
    public SimulationEngine engine;

    [Header("=== LIVE STATS (Use one or the other) ===")]
    [Tooltip("Old style: Separate panel that replaces setup")]
    public LiveResultsPanel liveResultsPanel;
    [Tooltip("New style: Self-contained overlay (recommended)")]
    public RuntimeStatsOverlay runtimeStatsOverlay;

    [Header("=== SHIP COUNT INPUTS (Initial Ships) ===")]
    public TMP_InputField merchantCountInput;
    public TMP_InputField pirateCountInput;
    public TMP_InputField securityCountInput;

    [Header("=== SPAWN RATE INPUTS ===")]
    public TMP_InputField merchantSpawnInput;
    public TMP_InputField pirateSpawnInput;
    public TMP_InputField securitySpawnInput;

    [Header("=== RUN SETTINGS INPUTS ===")]
    public TMP_InputField durationInput;
    public TMP_InputField seedInput;

    [Header("=== TIME OF DAY BUTTONS (1-4) ===")]
    public Button[] todButtons = new Button[4];

    [Header("=== WEATHER BUTTONS (1-4) ===")]
    public Button[] weatherButtons = new Button[4];

    [Header("=== MAP SELECTION BUTTONS ===")]
    public Button[] mapButtons = new Button[3];

    [Header("=== UI PANELS ===")]
    [Tooltip("Setup Bar (Root) - the setup/config panel")]
    public GameObject setupPanel;
    [Tooltip("Main Controls (Root) - pause/step/restart panel")]
    public GameObject controlsPanel;
    [Tooltip("Start Button (Root) - hide after starting")]
    public GameObject startButtonRoot;

    [Header("=== DISPLAY TEXT (Optional) ===")]
    public TMP_Text statusText;
    public TMP_Text timeText;
    public TMP_Text statsText;

    [Header("=== SPEED CONTROL (Rqmt19) ===")]
    [Tooltip("Option A: Use a slider")]
    public Slider speedSlider;
    [Tooltip("Option B: Use buttons (if no slider)")]
    public Button speedUpBtn;
    public Button speedDownBtn;
    [Tooltip("Shows current speed value")]
    public TMP_Text speedValueText;

    [Header("=== CONDITION INDICATOR (Rqmt25) ===")]
    [Tooltip("Shows current conditions during run (e.g. 'Morning / Clear')")]
    public TMP_Text conditionBadge;

    [Header("=== CONFIRMATION DIALOG ===")]
    public ConfirmationDialog confirmationDialog;

    [Header("=== SPAWN ZONE CONFIGURATOR ===")]
    public SpawnZoneConfigurator spawnZoneConfigurator;

    [Header("=== COASTAL DEFENSE ===")]
    public CoastalDefenseManager coastalDefenseManager;

    // State
    private bool isPaused = false;
    private int selectedTimeOfDay = 0;
    private int selectedWeather = 0;
    private float currentSpeed = 4f;
    private const float MIN_SPEED = 0.5f;
    private const float MAX_SPEED = 20f;
    private const float SPEED_STEP = 1f;

    // Colors for button selection highlighting
    private Color selectedColor = new Color(0.8f, 0.65f, 0.3f);
    private Color unselectedColor = new Color(0.6f, 0.5f, 0.35f);

    void Start()
    {
        // Connect main buttons
        if (startBtn) startBtn.onClick.AddListener(OnStartClicked);
        if (pauseBtn) pauseBtn.onClick.AddListener(OnPauseClicked);
        if (stepBtn) stepBtn.onClick.AddListener(OnStepClicked);
        if (resetBtn) resetBtn.onClick.AddListener(OnResetClicked);

        // Connect Time of Day buttons
        for (int i = 0; i < todButtons.Length; i++)
        {
            if (todButtons[i] != null)
            {
                int index = i;
                todButtons[i].onClick.AddListener(() => OnTimeOfDaySelected(index));
            }
        }

        // Connect Weather buttons
        for (int i = 0; i < weatherButtons.Length; i++)
        {
            if (weatherButtons[i] != null)
            {
                int index = i;
                weatherButtons[i].onClick.AddListener(() => OnWeatherSelected(index));
            }
        }

        // Connect Map buttons
        for (int i = 0; i < mapButtons.Length; i++)
        {
            if (mapButtons[i] != null)
            {
                int index = i;
                mapButtons[i].onClick.AddListener(() => OnMapSelected(index));
            }
        }

        // === SPEED CONTROL (Rqmt19) ===
        // Option A: Slider
        if (speedSlider)
        {
            speedSlider.minValue = MIN_SPEED;
            speedSlider.maxValue = MAX_SPEED;
            speedSlider.value = currentSpeed;
            speedSlider.onValueChanged.AddListener(OnSpeedSliderChanged);
        }
        // Option B: Buttons (if no slider)
        if (speedUpBtn) speedUpBtn.onClick.AddListener(OnSpeedUp);
        if (speedDownBtn) speedDownBtn.onClick.AddListener(OnSpeedDown);
        
        // Initialize speed display
        UpdateSpeedDisplay();

        // Initial UI state: show setup, hide controls
        if (setupPanel) setupPanel.SetActive(true);
        if (controlsPanel) controlsPanel.SetActive(false);
        if (startButtonRoot) startButtonRoot.SetActive(true);

        // Hide condition badge during setup (Rqmt25)
        if (conditionBadge) conditionBadge.gameObject.SetActive(false);

        // Highlight default selections
        UpdateTodButtonVisuals();
        UpdateWeatherButtonVisuals();

        SetStatus("READY");
        UpdatePauseButtonText();

        // Show spawn zones for setup
        if (spawnZoneConfigurator != null)
            spawnZoneConfigurator.ShowForSetup();
    }

    void Update()
    {
        UpdateTimeDisplay();
        UpdateStatsDisplay();
        UpdateConditionBadge();
    }

    // ===== BUTTON HANDLERS =====

    void OnStartClicked()
    {
        Debug.Log("START clicked");

        ApplySettingsFromInputs();
        ApplyEnvironmentSettings();

        // Lock spawn zones
        if (spawnZoneConfigurator == null)
            spawnZoneConfigurator = FindObjectOfType<SpawnZoneConfigurator>();

        if (spawnZoneConfigurator != null)
        {
            spawnZoneConfigurator.SyncToSpawner();
            spawnZoneConfigurator.Lock();
        }

        // Lock coastal defenses
        if (coastalDefenseManager == null)
            coastalDefenseManager = FindObjectOfType<CoastalDefenseManager>();

        if (coastalDefenseManager != null)
            coastalDefenseManager.Lock();

        // DISABLE MAP BUTTONS during runtime
        SetMapButtonsInteractable(false);

        if (engine) engine.StartRun();

        isPaused = false;
        UpdatePauseButtonText();
        SetStatus("RUNNING");

        // Switch UI: hide setup, show controls
        if (setupPanel) setupPanel.SetActive(false);
        if (startButtonRoot) startButtonRoot.SetActive(false);
        if (controlsPanel) controlsPanel.SetActive(true);

        // Show condition badge during run (Rqmt25)
        if (conditionBadge) conditionBadge.gameObject.SetActive(true);

        // Show live stats (supports both old and new style)
        if (liveResultsPanel) liveResultsPanel.ShowLiveResults();
        if (runtimeStatsOverlay) runtimeStatsOverlay.Show();
        
        // Auto-find RuntimeStatsOverlay if not assigned
        if (runtimeStatsOverlay == null)
        {
            runtimeStatsOverlay = FindObjectOfType<RuntimeStatsOverlay>();
            if (runtimeStatsOverlay != null) runtimeStatsOverlay.Show();
        }
    }

    void OnPauseClicked()
    {
        Debug.Log("PAUSE/RESUME clicked");

        if (engine == null) return;

        if (isPaused)
        {
            engine.StartRun();
            isPaused = false;
            SetStatus("RUNNING");
        }
        else
        {
            engine.PauseRun();
            isPaused = true;
            SetStatus("PAUSED");
        }

        UpdatePauseButtonText();
    }

    void OnStepClicked()
    {
        Debug.Log("STEP clicked");

        // If first step, apply settings and switch to controls view
        if (engine != null && engine.GetTickCount() == 0)
        {
            ApplySettingsFromInputs();
            ApplyEnvironmentSettings();

            // Lock spawn zones
            if (spawnZoneConfigurator == null)
                spawnZoneConfigurator = FindObjectOfType<SpawnZoneConfigurator>();
            if (spawnZoneConfigurator != null)
            {
                spawnZoneConfigurator.SyncToSpawner();
                spawnZoneConfigurator.Lock();
            }

            // DISABLE MAP BUTTONS during runtime
            SetMapButtonsInteractable(false);

            // Switch UI
            if (setupPanel) setupPanel.SetActive(false);
            if (startButtonRoot) startButtonRoot.SetActive(false);
            if (controlsPanel) controlsPanel.SetActive(true);

            if (liveResultsPanel) liveResultsPanel.ShowLiveResults();
        }

        if (engine) engine.StepOnce();
        SetStatus("STEP");
    }

    void OnResetClicked()
    {
        Debug.Log("RESET clicked");

        bool needsWarning = engine != null && engine.GetTickCount() > 0;

        if (needsWarning && confirmationDialog != null)
        {
            confirmationDialog.Show(
                "Reset Simulation?",
                "This will lose your current run progress.",
                onConfirm: DoReset
            );
        }
        else
        {
            DoReset();
        }
    }

    void DoReset()
    {
        // Show end of run panel if simulation ran
        if (engine != null && engine.GetTickCount() > 0)
        {
            if (EndOfRunPanel.Instance != null)
                EndOfRunPanel.Instance.Show();
        }

        if (engine) engine.ResetToNewRun();

        isPaused = false;
        UpdatePauseButtonText();
        SetStatus("READY");

        // Unlock spawn zones
        if (spawnZoneConfigurator != null)
            spawnZoneConfigurator.ShowForSetup();

        // Unlock coastal defenses
        if (coastalDefenseManager != null)
            coastalDefenseManager.Unlock();

        // RE-ENABLE MAP BUTTONS
        SetMapButtonsInteractable(true);

        // Switch UI: show setup, hide controls
        if (setupPanel) setupPanel.SetActive(true);
        if (startButtonRoot) startButtonRoot.SetActive(true);
        if (controlsPanel) controlsPanel.SetActive(false);

        // Hide condition badge (Rqmt25 - only show during run)
        if (conditionBadge) conditionBadge.gameObject.SetActive(false);

        // Hide live stats (both old and new style)
        if (liveResultsPanel) liveResultsPanel.HideLiveResults();
        if (runtimeStatsOverlay) runtimeStatsOverlay.Hide();
        if (RuntimeStatsOverlay.Instance != null) RuntimeStatsOverlay.Instance.Hide();
    }

    // ===== TIME OF DAY / WEATHER / MAP BUTTONS =====

    void OnTimeOfDaySelected(int index)
    {
        selectedTimeOfDay = index;
        Debug.Log($"Time of Day: {GetTimeOfDayName(index)}");

        if (EnvironmentSettings.Instance != null)
            EnvironmentSettings.Instance.SetTimeOfDay(index);

        UpdateTodButtonVisuals();
    }

    void OnWeatherSelected(int index)
    {
        selectedWeather = index;
        Debug.Log($"Weather: {GetWeatherName(index)}");

        if (EnvironmentSettings.Instance != null)
            EnvironmentSettings.Instance.SetWeather(index);

        UpdateWeatherButtonVisuals();
    }

    void OnMapSelected(int index)
    {
        Debug.Log($"Map selected: {index}");

        if (MapManager.Instance != null)
            MapManager.Instance.LoadMap(index);

        if (spawnZoneConfigurator != null)
            spawnZoneConfigurator.ShowForSetup();
    }

    string GetTimeOfDayName(int index)
    {
        switch (index)
        {
            case 0: return "Morning";
            case 1: return "Afternoon";
            case 2: return "Evening";
            case 3: return "Night";
            default: return "Unknown";
        }
    }

    string GetWeatherName(int index)
    {
        switch (index)
        {
            case 0: return "Clear";
            case 1: return "Cloudy";
            case 2: return "Stormy";
            case 3: return "Foggy";
            default: return "Unknown";
        }
    }

    void UpdateTodButtonVisuals()
    {
        for (int i = 0; i < todButtons.Length; i++)
        {
            if (todButtons[i] == null) continue;

            Image img = todButtons[i].GetComponent<Image>();
            if (img != null)
                img.color = (i == selectedTimeOfDay) ? selectedColor : unselectedColor;

            TMP_Text txt = todButtons[i].GetComponentInChildren<TMP_Text>();
            if (txt != null)
                txt.fontStyle = (i == selectedTimeOfDay) ? FontStyles.Bold | FontStyles.Underline : FontStyles.Normal;
        }
    }

    void UpdateWeatherButtonVisuals()
    {
        for (int i = 0; i < weatherButtons.Length; i++)
        {
            if (weatherButtons[i] == null) continue;

            Image img = weatherButtons[i].GetComponent<Image>();
            if (img != null)
                img.color = (i == selectedWeather) ? selectedColor : unselectedColor;

            TMP_Text txt = weatherButtons[i].GetComponentInChildren<TMP_Text>();
            if (txt != null)
                txt.fontStyle = (i == selectedWeather) ? FontStyles.Bold | FontStyles.Underline : FontStyles.Normal;
        }
    }

    // === SPEED CONTROL (Rqmt19) ===

    void OnSpeedSliderChanged(float value)
    {
        currentSpeed = value;
        ApplySpeed();
        UpdateSpeedDisplay();
    }

    void OnSpeedUp()
    {
        currentSpeed = Mathf.Min(currentSpeed + SPEED_STEP, MAX_SPEED);
        if (speedSlider) speedSlider.value = currentSpeed;
        ApplySpeed();
        UpdateSpeedDisplay();
    }

    void OnSpeedDown()
    {
        currentSpeed = Mathf.Max(currentSpeed - SPEED_STEP, MIN_SPEED);
        if (speedSlider) speedSlider.value = currentSpeed;
        ApplySpeed();
        UpdateSpeedDisplay();
    }

    void ApplySpeed()
    {
        if (engine) engine.SetTickInterval(1f / currentSpeed);
    }

    void UpdateSpeedDisplay()
    {
        if (speedValueText)
            speedValueText.text = $"{currentSpeed:F1}x";
    }

    // === CONDITION BADGE (Rqmt25) ===

    void UpdateConditionBadge()
    {
        if (conditionBadge == null) return;
        
        if (EnvironmentSettings.Instance != null)
        {
            conditionBadge.text = EnvironmentSettings.Instance.GetConditionsSummary();
        }
        else
        {
            conditionBadge.text = $"{GetTimeOfDayName(selectedTimeOfDay)} / {GetWeatherName(selectedWeather)}";
        }
    }

    // ===== APPLY SETTINGS =====

    void ApplyEnvironmentSettings()
    {
        if (EnvironmentSettings.Instance == null) return;

        EnvironmentSettings.Instance.SetTimeOfDay(selectedTimeOfDay);
        EnvironmentSettings.Instance.SetWeather(selectedWeather);

        Debug.Log($"Environment: {EnvironmentSettings.Instance.GetConditionsSummary()}");
    }

    // === INPUT LIMITS (Crash Prevention) ===
    private const int MAX_INITIAL_SHIPS = 50;      // Per type
    private const int MAX_TOTAL_SHIPS = 100;       // All types combined
    private const int MIN_SPAWN_INTERVAL = 10;     // Prevent spawn flooding
    private const int MAX_DURATION = 100000;       // ~27 hours of sim time
    private const int EASTER_EGG_THRESHOLD = 500;  // Trigger funny message

    void ApplySettingsFromInputs()
    {
        if (engine == null) return;

        // Parse raw values first to check for easter egg
        int rawMerchants = ParseIntRaw(merchantCountInput, 2);
        int rawPirates = ParseIntRaw(pirateCountInput, 1);
        int rawSecurity = ParseIntRaw(securityCountInput, 1);
        int rawTotal = rawMerchants + rawPirates + rawSecurity;

        // Easter egg check
        if (rawTotal > EASTER_EGG_THRESHOLD)
        {
            StartCoroutine(ShowEasterEggWarning(rawTotal));
        }

        // Parse with limits
        int merchants = ParseIntClamped(merchantCountInput, 2, 0, MAX_INITIAL_SHIPS);
        int pirates = ParseIntClamped(pirateCountInput, 1, 0, MAX_INITIAL_SHIPS);
        int security = ParseIntClamped(securityCountInput, 1, 0, MAX_INITIAL_SHIPS);

        // Enforce total ship limit
        int totalShips = merchants + pirates + security;
        if (totalShips > MAX_TOTAL_SHIPS)
        {
            float scale = (float)MAX_TOTAL_SHIPS / totalShips;
            merchants = Mathf.FloorToInt(merchants * scale);
            pirates = Mathf.FloorToInt(pirates * scale);
            security = Mathf.FloorToInt(security * scale);
            Debug.LogWarning($"Total ships clamped to {MAX_TOTAL_SHIPS}. Adjusted: M={merchants}, P={pirates}, S={security}");
        }

        engine.initialMerchants = merchants;
        engine.initialPirates = pirates;
        engine.initialSecurity = security;

        // Spawn intervals (higher = less frequent, so enforce minimum)
        engine.merchantSpawnInterval = ParseIntClamped(merchantSpawnInput, 50, MIN_SPAWN_INTERVAL, 10000);
        engine.pirateSpawnInterval = ParseIntClamped(pirateSpawnInput, 80, MIN_SPAWN_INTERVAL, 10000);
        engine.securitySpawnInterval = ParseIntClamped(securitySpawnInput, 100, MIN_SPAWN_INTERVAL, 10000);

        // Duration and seed (seed can be anything)
        engine.maxTicks = ParseIntClamped(durationInput, 0, 0, MAX_DURATION);
        engine.runSeed = ParseInt(seedInput, 12345);

        Debug.Log($"Applied: M={engine.initialMerchants}, P={engine.initialPirates}, S={engine.initialSecurity}");
    }

    System.Collections.IEnumerator ShowEasterEggWarning(int attemptedTotal)
    {
        // Create canvas if needed
        GameObject canvasObj = new GameObject("EasterEggCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999; // On top of everything

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Create text
        GameObject textObj = new GameObject("WarningText");
        textObj.transform.SetParent(canvasObj.transform, false);

        TMPro.TMP_Text warningText = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        warningText.text = $"Nice try. Ships capped at {MAX_TOTAL_SHIPS}.\n<size=24>({attemptedTotal:N0} requested)</size>";
        warningText.fontSize = 48;
        warningText.color = Color.red;
        warningText.alignment = TMPro.TextAlignmentOptions.Center;
        warningText.fontStyle = TMPro.FontStyles.Bold;

        RectTransform rect = textObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(800, 200);

        // Fade in
        float fadeTime = 0.3f;
        float elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float alpha = elapsed / fadeTime;
            warningText.color = new Color(1f, 0f, 0f, alpha);
            yield return null;
        }

        // Hold
        yield return new WaitForSeconds(2f);

        // Fade out
        elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - (elapsed / fadeTime);
            warningText.color = new Color(1f, 0f, 0f, alpha);
            yield return null;
        }

        // Cleanup
        Destroy(canvasObj);
    }

    int ParseInt(TMP_InputField input, int defaultValue)
    {
        if (input == null) return defaultValue;
        if (string.IsNullOrEmpty(input.text)) return defaultValue;
        if (int.TryParse(input.text, out int result))
            return Mathf.Max(0, result);
        return defaultValue;
    }

    int ParseIntRaw(TMP_InputField input, int defaultValue)
    {
        if (input == null) return defaultValue;
        if (string.IsNullOrEmpty(input.text)) return defaultValue;
        if (int.TryParse(input.text, out int result))
            return Mathf.Max(0, result);
        return defaultValue;
    }

    int ParseIntClamped(TMP_InputField input, int defaultValue, int min, int max)
    {
        if (input == null) return defaultValue;
        if (string.IsNullOrEmpty(input.text)) return defaultValue;
        if (int.TryParse(input.text, out int result))
        {
            int clamped = Mathf.Clamp(result, min, max);
            if (clamped != result)
            {
                Debug.LogWarning($"Input '{input.name}' clamped: {result} → {clamped} (limits: {min}-{max})");
                input.text = clamped.ToString(); // Update the UI to show clamped value
            }
            return clamped;
        }
        return defaultValue;
    }

    // ===== UI UPDATES =====

    void UpdatePauseButtonText()
    {
        if (pauseButtonText)
            pauseButtonText.text = isPaused ? "Resume" : "Pause";
    }

    void SetStatus(string text)
    {
        if (statusText)
        {
            statusText.text = text;

            switch (text)
            {
                case "RUNNING": statusText.color = Color.green; break;
                case "PAUSED": statusText.color = Color.yellow; break;
                case "STOPPED": statusText.color = Color.red; break;
                default: statusText.color = Color.white; break;
            }
        }
    }

    void UpdateTimeDisplay()
    {
        if (timeText == null || engine == null) return;

        int ticks = engine.GetTickCount();
        int ticksPerHour = 60;
        int startHour = 6;

        if (EnvironmentSettings.Instance != null)
        {
            switch (EnvironmentSettings.Instance.timeOfDay)
            {
                case TimeOfDay.Morning: startHour = 6; break;
                case TimeOfDay.Noon: startHour = 12; break;
                case TimeOfDay.Evening: startHour = 18; break;
                case TimeOfDay.Night: startHour = 22; break;
            }
        }

        int totalMinutes = (ticks * 60) / Mathf.Max(1, ticksPerHour);
        int hours = (startHour + (totalMinutes / 60)) % 24;
        int minutes = totalMinutes % 60;
        int day = 1 + ((startHour + (totalMinutes / 60)) / 24);

        timeText.text = $"Day {day}\n{hours:D2}:{minutes:D2}\nTick: {ticks}";
    }

    void UpdateStatsDisplay()
    {
        if (statsText == null) return;

        int merchants = 0, pirates = 0, security = 0;

        if (ShipSpawner.Instance != null)
        {
            foreach (var ship in ShipSpawner.Instance.GetActiveShips())
            {
                if (ship == null || ship.Data == null) continue;
                switch (ship.Data.type)
                {
                    case ShipType.Cargo: merchants++; break;
                    case ShipType.Pirate: pirates++; break;
                    case ShipType.Security: security++; break;
                }
            }
        }

        int escaped = 0, captured = 0, defeated = 0;
        if (engine != null)
        {
            escaped = engine.GetMerchantsExited();
            captured = engine.GetMerchantsCaptured();
            defeated = engine.GetPiratesDefeated();
        }

        statsText.text = $"SHIPS\n" +
                        $"Merchants: {merchants}\n" +
                        $"Pirates: {pirates}\n" +
                        $"Security: {security}\n\n" +
                        $"OUTCOMES\n" +
                        $"Escaped: {escaped}\n" +
                        $"Captured: {captured}\n" +
                        $"Defeated: {defeated}";
    }

    // ===== MAP BUTTON LOCK =====

    void SetMapButtonsInteractable(bool interactable)
    {
        foreach (var btn in mapButtons)
        {
            if (btn != null)
                btn.interactable = interactable;
        }

        // Also disable time of day and weather buttons during runtime
        foreach (var btn in todButtons)
        {
            if (btn != null)
                btn.interactable = interactable;
        }

        foreach (var btn in weatherButtons)
        {
            if (btn != null)
                btn.interactable = interactable;
        }

        if (!interactable)
            Debug.Log("Map/Environment buttons LOCKED for runtime");
        else
            Debug.Log("Map/Environment buttons UNLOCKED for setup");
    }
}