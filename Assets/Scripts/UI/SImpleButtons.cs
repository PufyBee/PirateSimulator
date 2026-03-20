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
    public LiveResultsPanel liveResultsPanel;
    public RuntimeStatsOverlay runtimeStatsOverlay;

    [Header("=== SHIP COUNT INPUTS (Initial Ships) ===")]
    public TMP_InputField merchantCountInput;
    public TMP_InputField pirateCountInput;
    public TMP_InputField securityCountInput;

    [Header("=== SPAWN RATE INPUTS ===")]
    [Tooltip("Ticks between merchant spawns (higher = slower spawning)")]
    public TMP_InputField merchantSpawnInput;
    [Tooltip("Ticks between pirate spawns (higher = slower spawning)")]
    public TMP_InputField pirateSpawnInput;
    [Tooltip("Ticks between security spawns (higher = slower spawning)")]
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
    [Tooltip("Setup Bar (Root) - the setup/config panel on LEFT side")]
    public GameObject setupPanel;
    [Tooltip("Main Controls (Root) - pause/step/restart panel")]
    public GameObject controlsPanel;
    [Tooltip("Start Button (Root) - hide after starting")]
    public GameObject startButtonRoot;
    [Tooltip("Map Selection Panel - HIDE completely during runtime")]
    public GameObject mapSelectionPanel;
    [Tooltip("Right side panel - HIDE during runtime if assigned")]
    public GameObject rightSidePanel;

    [Header("=== DISPLAY TEXT (Optional) ===")]
    public TMP_Text statusText;
    public TMP_Text timeText;
    public TMP_Text statsText;

    [Header("=== SPEED CONTROL (Rqmt19) ===")]
    public Slider speedSlider;
    public Button speedUpBtn;
    public Button speedDownBtn;
    public TMP_Text speedValueText;

    [Header("=== CONDITION INDICATOR (Rqmt25) ===")]
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

    private Color selectedColor = new Color(0.8f, 0.65f, 0.3f);
    private Color unselectedColor = new Color(0.6f, 0.5f, 0.35f);

    void Start()
    {
        if (startBtn) startBtn.onClick.AddListener(OnStartClicked);
        if (pauseBtn) pauseBtn.onClick.AddListener(OnPauseClicked);
        if (stepBtn) stepBtn.onClick.AddListener(OnStepClicked);
        if (resetBtn) resetBtn.onClick.AddListener(OnResetClicked);

        for (int i = 0; i < todButtons.Length; i++)
        {
            if (todButtons[i] != null)
            {
                int index = i;
                todButtons[i].onClick.AddListener(() => OnTimeOfDaySelected(index));
            }
        }

        for (int i = 0; i < weatherButtons.Length; i++)
        {
            if (weatherButtons[i] != null)
            {
                int index = i;
                weatherButtons[i].onClick.AddListener(() => OnWeatherSelected(index));
            }
        }

        for (int i = 0; i < mapButtons.Length; i++)
        {
            if (mapButtons[i] != null)
            {
                int index = i;
                mapButtons[i].onClick.AddListener(() => OnMapSelected(index));
            }
        }

        if (speedSlider)
        {
            speedSlider.minValue = MIN_SPEED;
            speedSlider.maxValue = MAX_SPEED;
            speedSlider.value = currentSpeed;
            speedSlider.onValueChanged.AddListener(OnSpeedSliderChanged);
        }
        if (speedUpBtn) speedUpBtn.onClick.AddListener(OnSpeedUp);
        if (speedDownBtn) speedDownBtn.onClick.AddListener(OnSpeedDown);
        
        UpdateSpeedDisplay();

        if (setupPanel) setupPanel.SetActive(true);
        if (controlsPanel) controlsPanel.SetActive(false);
        if (startButtonRoot) startButtonRoot.SetActive(true);
        if (mapSelectionPanel) mapSelectionPanel.SetActive(true);
        if (rightSidePanel) rightSidePanel.SetActive(true);
        if (conditionBadge) conditionBadge.gameObject.SetActive(false);

        UpdateTodButtonVisuals();
        UpdateWeatherButtonVisuals();
        SetStatus("READY");
        UpdatePauseButtonText();

        if (spawnZoneConfigurator != null)
            spawnZoneConfigurator.ShowForSetup();

        LogInputConnections();
    }

    void LogInputConnections()
    {
        Debug.Log("=== SimpleButtons Input Connections ===");
        Debug.Log($"merchantCountInput: {(merchantCountInput != null ? "OK" : "NULL")}");
        Debug.Log($"pirateCountInput: {(pirateCountInput != null ? "OK" : "NULL")}");
        Debug.Log($"securityCountInput: {(securityCountInput != null ? "OK" : "NULL")}");
        Debug.Log($"merchantSpawnInput: {(merchantSpawnInput != null ? "OK" : "NULL")}");
        Debug.Log($"pirateSpawnInput: {(pirateSpawnInput != null ? "OK" : "NULL")}");
        Debug.Log($"securitySpawnInput: {(securitySpawnInput != null ? "OK" : "NULL")}");
    }

    void Update()
    {
        UpdateTimeDisplay();
        UpdateStatsDisplay();
        UpdateConditionBadge();
    }

    void OnStartClicked()
    {
        Debug.Log("START clicked");

        ApplySettingsFromInputs();
        ApplyEnvironmentSettings();
        //AudioManager.Instance.PlayMusic("Simulation");

        if (spawnZoneConfigurator == null)
            spawnZoneConfigurator = FindObjectOfType<SpawnZoneConfigurator>();
        if (spawnZoneConfigurator != null)
        {
            spawnZoneConfigurator.SyncToSpawner();
            spawnZoneConfigurator.Lock();
        }

        if (coastalDefenseManager == null)
            coastalDefenseManager = FindObjectOfType<CoastalDefenseManager>();
        if (coastalDefenseManager != null)
            coastalDefenseManager.Lock();

        SetRuntimeUIState(true);

        if (engine) engine.StartRun();

        isPaused = false;
        UpdatePauseButtonText();
        SetStatus("RUNNING");

        if (setupPanel) setupPanel.SetActive(false);
        if (startButtonRoot) startButtonRoot.SetActive(false);
        if (controlsPanel) controlsPanel.SetActive(true);
        if (conditionBadge) conditionBadge.gameObject.SetActive(true);

        if (liveResultsPanel) liveResultsPanel.ShowLiveResults();
        if (runtimeStatsOverlay) runtimeStatsOverlay.Show();
        
        if (runtimeStatsOverlay == null)
        {
            runtimeStatsOverlay = FindObjectOfType<RuntimeStatsOverlay>();
            if (runtimeStatsOverlay != null) runtimeStatsOverlay.Show();
        }
    }

    void OnPauseClicked()
    {
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
        if (engine != null && engine.GetTickCount() == 0)
        {
            ApplySettingsFromInputs();
            ApplyEnvironmentSettings();

            if (spawnZoneConfigurator == null)
                spawnZoneConfigurator = FindObjectOfType<SpawnZoneConfigurator>();
            if (spawnZoneConfigurator != null)
            {
                spawnZoneConfigurator.SyncToSpawner();
                spawnZoneConfigurator.Lock();
            }

            SetRuntimeUIState(true);

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
        if (engine != null && engine.GetTickCount() > 0)
        {
            if (EndOfRunPanel.Instance != null)
                EndOfRunPanel.Instance.Show();
        }

        if (engine) engine.ResetToNewRun();

        isPaused = false;
        UpdatePauseButtonText();
        SetStatus("READY");

        if (spawnZoneConfigurator != null)
            spawnZoneConfigurator.ShowForSetup();
        if (coastalDefenseManager != null)
            coastalDefenseManager.Unlock();

        SetRuntimeUIState(false);

        if (setupPanel) setupPanel.SetActive(true);
        if (startButtonRoot) startButtonRoot.SetActive(true);
        if (controlsPanel) controlsPanel.SetActive(false);
        if (conditionBadge) conditionBadge.gameObject.SetActive(false);

        if (liveResultsPanel) liveResultsPanel.HideLiveResults();
        if (runtimeStatsOverlay) runtimeStatsOverlay.Hide();
        if (RuntimeStatsOverlay.Instance != null) RuntimeStatsOverlay.Instance.Hide();
    }

    void SetRuntimeUIState(bool isRunning)
    {
        // COMPLETELY HIDE map selection panel during runtime
        if (mapSelectionPanel != null)
        {
            mapSelectionPanel.SetActive(!isRunning);
        }

        // COMPLETELY HIDE right side panel during runtime
        if (rightSidePanel != null)
        {
            rightSidePanel.SetActive(!isRunning);
        }

        SetMapButtonsInteractable(!isRunning);
    }

    void OnTimeOfDaySelected(int index)
    {
        selectedTimeOfDay = index;
        if (EnvironmentSettings.Instance != null)
            EnvironmentSettings.Instance.SetTimeOfDay(index);
        UpdateTodButtonVisuals();
    }

    void OnWeatherSelected(int index)
    {
        selectedWeather = index;
        if (EnvironmentSettings.Instance != null)
            EnvironmentSettings.Instance.SetWeather(index);
        UpdateWeatherButtonVisuals();
    }

    void OnMapSelected(int index)
    {
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

    void UpdateConditionBadge()
    {
        if (conditionBadge == null) return;
        if (EnvironmentSettings.Instance != null)
            conditionBadge.text = EnvironmentSettings.Instance.GetConditionsSummary();
        else
            conditionBadge.text = $"{GetTimeOfDayName(selectedTimeOfDay)} / {GetWeatherName(selectedWeather)}";
    }

    void ApplyEnvironmentSettings()
    {
        if (EnvironmentSettings.Instance == null) return;
        EnvironmentSettings.Instance.SetTimeOfDay(selectedTimeOfDay);
        EnvironmentSettings.Instance.SetWeather(selectedWeather);
    }

    private const int MAX_INITIAL_SHIPS = 50;
    private const int MAX_TOTAL_SHIPS = 100;
    private const int MAX_DURATION = 100000;
    
    // Spawn rate conversion: user enters 1-10, we convert to ticks
    // Higher number = faster spawning
    private const int SPAWN_RATE_BASE = 100; // At rate 1, spawn every 100 ticks

    void ApplySettingsFromInputs()
    {
        if (engine == null) return;

        int merchants = ParseIntClamped(merchantCountInput, 2, 0, MAX_INITIAL_SHIPS);
        int pirates = ParseIntClamped(pirateCountInput, 1, 0, MAX_INITIAL_SHIPS);
        int security = ParseIntClamped(securityCountInput, 1, 0, MAX_INITIAL_SHIPS);

        int totalShips = merchants + pirates + security;
        if (totalShips > MAX_TOTAL_SHIPS)
        {
            float scale = (float)MAX_TOTAL_SHIPS / totalShips;
            merchants = Mathf.FloorToInt(merchants * scale);
            pirates = Mathf.FloorToInt(pirates * scale);
            security = Mathf.FloorToInt(security * scale);
        }

        engine.initialMerchants = merchants;
        engine.initialPirates = pirates;
        engine.initialSecurity = security;

        // Convert spawn RATE (1-10, higher=faster) to spawn INTERVAL (ticks between spawns)
        // Rate 0 = disabled (set to very high interval)
        // Rate 1 = slow (100 ticks between spawns)
        // Rate 5 = medium (20 ticks between spawns)
        // Rate 10 = fast (10 ticks between spawns)
        int merchantRate = ParseIntClamped(merchantSpawnInput, 5, 0, 10);
        int pirateRate = ParseIntClamped(pirateSpawnInput, 3, 0, 10);
        int securityRate = ParseIntClamped(securitySpawnInput, 3, 0, 10);

        engine.merchantSpawnInterval = ConvertRateToInterval(merchantRate);
        engine.pirateSpawnInterval = ConvertRateToInterval(pirateRate);
        engine.securitySpawnInterval = ConvertRateToInterval(securityRate);

        engine.maxTicks = ParseIntClamped(durationInput, 0, 0, MAX_DURATION);
        engine.runSeed = ParseInt(seedInput, 12345);

        Debug.Log($"=== SETTINGS APPLIED ===");
        Debug.Log($"Initial: M={engine.initialMerchants}, P={engine.initialPirates}, S={engine.initialSecurity}");
        Debug.Log($"Spawn Rates: M={merchantRate}→{engine.merchantSpawnInterval}t, P={pirateRate}→{engine.pirateSpawnInterval}t, S={securityRate}→{engine.securitySpawnInterval}t");
    }

    /// <summary>
    /// Convert user-friendly rate (0-10) to tick interval.
    /// 0 = disabled (99999 ticks)
    /// 1 = slowest (100 ticks)
    /// 10 = fastest (10 ticks)
    /// </summary>
    int ConvertRateToInterval(int rate)
    {
        if (rate <= 0) return 99999; // Effectively disabled
        return Mathf.Max(10, SPAWN_RATE_BASE / rate);
    }

    int ParseInt(TMP_InputField input, int defaultValue)
    {
        if (input == null) return defaultValue;
        if (string.IsNullOrEmpty(input.text)) return defaultValue;
        if (int.TryParse(input.text, out int result))
            return Mathf.Max(0, result);
        return defaultValue;
    }

    int ParseIntClamped(TMP_InputField input, int defaultValue, int min, int max)
    {
        if (input == null) 
        {
            Debug.LogWarning($"Input field NULL, using default: {defaultValue}");
            return defaultValue;
        }
        if (string.IsNullOrEmpty(input.text)) return defaultValue;
        if (int.TryParse(input.text, out int result))
        {
            int clamped = Mathf.Clamp(result, min, max);
            if (clamped != result)
                input.text = clamped.ToString();
            return clamped;
        }
        return defaultValue;
    }

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

        statsText.text = $"SHIPS\nMerchants: {merchants}\nPirates: {pirates}\nSecurity: {security}\n\nOUTCOMES\nEscaped: {escaped}\nCaptured: {captured}\nDefeated: {defeated}";
    }

    void SetMapButtonsInteractable(bool interactable)
    {
        foreach (var btn in mapButtons)
            if (btn != null) btn.interactable = interactable;
        foreach (var btn in todButtons)
            if (btn != null) btn.interactable = interactable;
        foreach (var btn in weatherButtons)
            if (btn != null) btn.interactable = interactable;
    }
}