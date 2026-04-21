using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI Controller - Updated for High-Speed Multi-Tick Engine
/// 
/// CHANGES FROM ORIGINAL:
/// 1. Speed slider now goes 0.5x to 500x (was 0.5 to 20)
/// 2. Uses logarithmic slider scale for fine control at low speeds, big jumps at high speeds
/// 3. Calls SetSpeedMultiplier() instead of SetTickInterval()
/// 4. Time display shows simulated days/hours based on real-time correlation
/// 5. Added performance indicator (ticks/sec)
/// 
/// ALL OTHER FUNCTIONALITY IS IDENTICAL.
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

    [Header("=== WAKE TRAIL TOGGLE ===")]
    public Button wakeTrailToggleBtn;
    public TMP_Text wakeTrailButtonText;
    private bool wakeTrailsEnabled = true;

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

    // === SPEED SETTINGS (UPDATED) ===
    // Slider uses logarithmic scale: 0.0 to 1.0 maps to MIN_SPEED to MAX_SPEED
    private float currentSpeed = 4f;
    private const float MIN_SPEED = 0.5f;
    private const float MAX_SPEED = 500f;

    // Speed presets for button stepping
    private static readonly float[] SPEED_PRESETS = {
        0.5f, 1f, 2f, 4f, 8f, 16f, 32f, 50f, 100f, 200f, 500f
    };
    private int currentPresetIndex = 3; // starts at 4x

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
            // Logarithmic slider: 0.0 to 1.0 (maps to MIN_SPEED..MAX_SPEED)
            speedSlider.minValue = 0f;
            speedSlider.maxValue = 1f;
            speedSlider.value = SpeedToSlider(currentSpeed);
            speedSlider.onValueChanged.AddListener(OnSpeedSliderChanged);
        }
        if (speedUpBtn) speedUpBtn.onClick.AddListener(OnSpeedUp);
        if (speedDownBtn) speedDownBtn.onClick.AddListener(OnSpeedDown);
        if (wakeTrailToggleBtn) wakeTrailToggleBtn.onClick.AddListener(OnWakeTrailToggle);
        
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
        {
            // Hide spawn zone markers if trade routes are configured for this map
            if (TradeRouteManager.Instance != null && TradeRouteManager.Instance.HasRouteData())
                spawnZoneConfigurator.gameObject.SetActive(false);
            else
                spawnZoneConfigurator.ShowForSetup();
        }

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

        // Keyboard support
        if (engine != null && engine.GetTickCount() > 0){
            if (Input.GetKeyDown(KeyCode.Space)) {
                OnPauseClicked();
            }
            if (Input.GetKeyDown(KeyCode.RightArrow)) {
                OnStepClicked();
            }
            if (Input.GetKeyDown(KeyCode.R)) {
                DoReset();
            }
            if (Input.GetKeyDown(KeyCode.UpArrow)) {
                OnSpeedUp();
            }
            if (Input.GetKeyDown(KeyCode.DownArrow)) {
                OnSpeedDown();
            }        
        }

        // Continuously enforce wake trail toggle on all ships (catches newly spawned ones)
        if (!wakeTrailsEnabled)
        {
            var allEffects = FindObjectsOfType<ShipVisualEffects>();
            foreach (var fx in allEffects)
            {
                if (fx.enabled)
                {
                    fx.enabled = false;
                    var trails = fx.GetComponentsInChildren<TrailRenderer>();
                    foreach (var trail in trails)
                        trail.enabled = false;
                    var lines = fx.GetComponentsInChildren<LineRenderer>();
                    foreach (var line in lines)
                        line.enabled = false;
                }
            }
        }
    }

    // ===== LOGARITHMIC SPEED SLIDER =====
    // This gives fine control at low speeds (0.5-10x) and big jumps at high speeds (100-500x)

    /// <summary>
    /// Convert speed multiplier to slider position (0..1) using log scale.
    /// </summary>
    float SpeedToSlider(float speed)
    {
        // log mapping: slider = log(speed/MIN) / log(MAX/MIN)
        float logMin = Mathf.Log(MIN_SPEED);
        float logMax = Mathf.Log(MAX_SPEED);
        float logSpeed = Mathf.Log(Mathf.Clamp(speed, MIN_SPEED, MAX_SPEED));
        return (logSpeed - logMin) / (logMax - logMin);
    }

    /// <summary>
    /// Convert slider position (0..1) to speed multiplier using log scale.
    /// </summary>
    float SliderToSpeed(float sliderValue)
    {
        float logMin = Mathf.Log(MIN_SPEED);
        float logMax = Mathf.Log(MAX_SPEED);
        float logSpeed = logMin + sliderValue * (logMax - logMin);
        return Mathf.Exp(logSpeed);
    }

    // ===== BUTTON HANDLERS =====

    void OnStartClicked()
    {
        Debug.Log("START clicked");

        ApplySettingsFromInputs();
        ApplyEnvironmentSettings();

        AudioManager.Instance?.PlaySFX(AudioClipNames.SFX.StartSim); 
        AudioManager.Instance?.StopMusic(0.5f); 
        AudioManager.Instance?.PlayMusic(AudioClipNames.Music.Simulation, 1f);
        AudioManager.Instance?.PlaySFX("ButtonClick");

        if (spawnZoneConfigurator == null)
            spawnZoneConfigurator = FindObjectOfType<SpawnZoneConfigurator>();
        if (spawnZoneConfigurator != null)
        {
            // Only sync old spawn zones if NO trade routes are configured
            if (TradeRouteManager.Instance == null || !TradeRouteManager.Instance.HasRouteData())
            {
                spawnZoneConfigurator.SyncToSpawner();
            }
            spawnZoneConfigurator.Lock();
        }

        if (coastalDefenseManager == null)
            coastalDefenseManager = FindObjectOfType<CoastalDefenseManager>();
        if (coastalDefenseManager != null)
            coastalDefenseManager.Lock();

        // Hide route weight panel during simulation
        if (RouteWeightPanel.Instance != null)
            RouteWeightPanel.Instance.Hide();

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

        // Hide persistent help button during simulation to avoid blocking live stats
        if (HelpPanel.Instance != null)
            HelpPanel.Instance.HidePersistentButton();
    }

    void OnPauseClicked()
    {
        if (engine == null) return;

        AudioManager.Instance?.PlaySFX("ButtonClick");

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
        AudioManager.Instance?.PlaySFX("ButtonClick");

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
        AudioManager.Instance?.PlaySFX("ButtonClick");

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
        AudioManager.Instance?.PlaySFX(AudioClipNames.SFX.ResetSim);
        AudioManager.Instance?.StopMusic(0.5f);
        AudioManager.Instance?.PlayMusic(AudioClipNames.Music.Setup, 1f);
        if (engine != null && engine.GetTickCount() > 0)
        {
            if (EndOfRunPanel.Instance != null)
                EndOfRunPanel.Instance.Show();
        }

        if (engine) engine.ResetToNewRun();

        // Reset trade route assignments
        if (TradeRouteManager.Instance != null)
            TradeRouteManager.Instance.ResetAllAssignments();

        isPaused = false;
        UpdatePauseButtonText();
        SetStatus("READY");

        if (spawnZoneConfigurator != null)
        {
            if (TradeRouteManager.Instance != null && TradeRouteManager.Instance.HasRouteData())
                spawnZoneConfigurator.gameObject.SetActive(false);
            else
            {
                spawnZoneConfigurator.gameObject.SetActive(true);
                spawnZoneConfigurator.ShowForSetup();
            }
        }
        if (coastalDefenseManager != null)
            coastalDefenseManager.Unlock();

        // Restore route weights and show panel
        if (RouteWeightPanel.Instance != null)
        {
            RouteWeightPanel.Instance.RestoreOriginalWeights();
            RouteWeightPanel.Instance.RefreshForCurrentMap();
            RouteWeightPanel.Instance.Show();
        }

        SetRuntimeUIState(false);

        if (setupPanel) setupPanel.SetActive(true);
        if (startButtonRoot) startButtonRoot.SetActive(true);
        if (controlsPanel) controlsPanel.SetActive(false);
        if (conditionBadge) conditionBadge.gameObject.SetActive(false);

        if (liveResultsPanel) liveResultsPanel.HideLiveResults();
        if (runtimeStatsOverlay) runtimeStatsOverlay.Hide();
        if (RuntimeStatsOverlay.Instance != null) RuntimeStatsOverlay.Instance.Hide();

        // Show persistent help button again after returning to setup
        if (HelpPanel.Instance != null)
            HelpPanel.Instance.ShowPersistentButton();
    }

    void SetRuntimeUIState(bool isRunning)
    {
        if (mapSelectionPanel != null)
        {
            mapSelectionPanel.SetActive(!isRunning);
        }

        if (rightSidePanel != null)
        {
            rightSidePanel.SetActive(!isRunning);
        }

        SetMapButtonsInteractable(!isRunning);
    }

    void OnTimeOfDaySelected(int index)
    {
        AudioManager.Instance?.PlaySFX("ButtonClick");

        selectedTimeOfDay = index;
        if (EnvironmentSettings.Instance != null)
            EnvironmentSettings.Instance.SetTimeOfDay(index);
        UpdateTodButtonVisuals();
    }

    void OnWeatherSelected(int index)
    {
        AudioManager.Instance?.PlaySFX("ButtonClick");

        selectedWeather = index;
        if (EnvironmentSettings.Instance != null)
            EnvironmentSettings.Instance.SetWeather(index);
        UpdateWeatherButtonVisuals();
    }

    void OnMapSelected(int index)
    {
        if (MapManager.Instance != null)
            MapManager.Instance.LoadMap(index);

        // Switch trade route data to match new map
        if (TradeRouteManager.Instance != null)
            TradeRouteManager.Instance.SetMapIndex(index);

        // Show/hide spawn zone markers based on whether this map has trade routes
        if (spawnZoneConfigurator != null)
        {
            if (TradeRouteManager.Instance != null && TradeRouteManager.Instance.HasRouteData())
                spawnZoneConfigurator.gameObject.SetActive(false);
            else
            {
                spawnZoneConfigurator.gameObject.SetActive(true);
                spawnZoneConfigurator.ShowForSetup();
            }
            
        }
                // Refresh route weight sliders for new map
        if (RouteWeightPanel.Instance != null)
            RouteWeightPanel.Instance.RefreshForCurrentMap();
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

    // ===== SPEED CONTROL (UPDATED) =====

    void OnSpeedSliderChanged(float sliderValue)
    {
        currentSpeed = SliderToSpeed(sliderValue);

        // Snap to nearest preset for display cleanliness
        currentSpeed = SnapToNearestPreset(currentSpeed);

        ApplySpeed();
        UpdateSpeedDisplay();
    }

    void OnSpeedUp()
    {
        // Jump to next preset
        currentPresetIndex = Mathf.Min(currentPresetIndex + 1, SPEED_PRESETS.Length - 1);
        currentSpeed = SPEED_PRESETS[currentPresetIndex];
        if (speedSlider) speedSlider.value = SpeedToSlider(currentSpeed);
        ApplySpeed();
        UpdateSpeedDisplay();
    }

    void OnSpeedDown()
    {
        // Jump to previous preset
        currentPresetIndex = Mathf.Max(currentPresetIndex - 1, 0);
        currentSpeed = SPEED_PRESETS[currentPresetIndex];
        if (speedSlider) speedSlider.value = SpeedToSlider(currentSpeed);
        ApplySpeed();
        UpdateSpeedDisplay();
    }

    /// <summary>
    /// Snap to nearest preset if very close (for clean display values).
    /// </summary>
    float SnapToNearestPreset(float speed)
    {
        float bestDist = float.MaxValue;
        float bestPreset = speed;
        int bestIndex = currentPresetIndex;

        for (int i = 0; i < SPEED_PRESETS.Length; i++)
        {
            // Use ratio distance for log-scale snapping
            float ratio = speed / SPEED_PRESETS[i];
            float dist = Mathf.Abs(Mathf.Log(ratio));

            if (dist < bestDist)
            {
                bestDist = dist;
                bestPreset = SPEED_PRESETS[i];
                bestIndex = i;
            }
        }

        // Only snap if very close (within ~15% on log scale)
        if (bestDist < 0.15f)
        {
            currentPresetIndex = bestIndex;
            return bestPreset;
        }

        return speed;
    }

    void ApplySpeed()
    {
        if (engine) engine.SetSpeedMultiplier(currentSpeed);
    }

    void UpdateSpeedDisplay()
    {
        if (speedValueText)
        {
            if (currentSpeed >= 100f)
                speedValueText.text = $"{currentSpeed:F0}x";
            else if (currentSpeed >= 10f)
                speedValueText.text = $"{currentSpeed:F0}x";
            else
                speedValueText.text = $"{currentSpeed:F1}x";
        }
    }

    // ===== WAKE TRAIL TOGGLE =====

    void OnWakeTrailToggle()
    {
        wakeTrailsEnabled = !wakeTrailsEnabled;

        // Toggle all existing ShipVisualEffects components
        var allEffects = FindObjectsOfType<ShipVisualEffects>();
        foreach (var fx in allEffects)
        {
            fx.enabled = wakeTrailsEnabled;
            // Also hide existing trail renderers
            var trails = fx.GetComponentsInChildren<TrailRenderer>();
            foreach (var trail in trails)
                trail.enabled = wakeTrailsEnabled;
            var lines = fx.GetComponentsInChildren<LineRenderer>();
            foreach (var line in lines)
                line.enabled = wakeTrailsEnabled;
        }

        // Update ShipSpawner so new ships respect the setting
        if (ShipSpawner.Instance != null)
            ShipSpawner.Instance.enableVisualEffects = wakeTrailsEnabled;

        // Update button text
        if (wakeTrailButtonText)
            wakeTrailButtonText.text = wakeTrailsEnabled ? "Trails: ON" : "Trails: OFF";
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

    // ===== TIME DISPLAY (UPDATED - Real Time Correlation) =====

    void UpdateTimeDisplay()
    {
        if (timeText == null || engine == null) return;

        int ticks = engine.GetTickCount();
        float simHours = engine.GetSimulatedHours();
        float simDays = engine.GetSimulatedDays();

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

        int displayTicks = Mathf.Max(0, engine.GetTickCount() - 1);
        int day = (displayTicks / 96);
        if (engine.maxTicks > 0 && engine.GetTickCount() >= engine.maxTicks)
            day = engine.maxTicks / 96;

        float totalHours = startHour + simHours;
        int hours = Mathf.FloorToInt(totalHours) % 24;
        int minutes = Mathf.FloorToInt((totalHours - Mathf.Floor(totalHours)) * 60f);
        Debug.Log($"ticks={ticks}, simHours={simHours}, day={day}, maxTicks={engine.maxTicks}, maxDays={engine.maxTicks/96}");
        // Show effective ticks per second for performance monitoring
        float effectiveTps = engine.GetEffectiveTicksPerSecond();

        timeText.text = $"Day {day}  {hours:D2}:{minutes:D2}\nTick: {ticks}  ({currentSpeed:F0}x)\n{effectiveTps:F0} ticks/s";
    }

    // ===== SETTINGS =====

    private const int MAX_INITIAL_SHIPS = 50;
    private const int MAX_TOTAL_SHIPS = 100;
    private const int MAX_DURATION = 24000;
    private const int SPAWN_RATE_BASE = 300;

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

        int merchantRate = ParseIntClamped(merchantSpawnInput, 5, 0, 10);
        int pirateRate = ParseIntClamped(pirateSpawnInput, 3, 0, 10);
        int securityRate = ParseIntClamped(securitySpawnInput, 3, 0, 10);

        engine.merchantSpawnInterval = ConvertRateToInterval(merchantRate);
        engine.pirateSpawnInterval = ConvertRateToInterval(pirateRate);
        engine.securitySpawnInterval = ConvertRateToInterval(securityRate);

        int durationDays = ParseIntClamped(durationInput, 0, 0, 250);
        engine.maxTicks = durationDays * 96; // 1 day = 96 ticks (15 min/tick)
        engine.runSeed = ParseInt(seedInput, 12345);

        Debug.Log($"=== SETTINGS APPLIED ===");
        Debug.Log($"Initial: M={engine.initialMerchants}, P={engine.initialPirates}, S={engine.initialSecurity}");
        Debug.Log($"Spawn Rates: M={merchantRate}->{engine.merchantSpawnInterval}t, P={pirateRate}->{engine.pirateSpawnInterval}t, S={securityRate}->{engine.securitySpawnInterval}t");
    }

    int ConvertRateToInterval(int rate)
    {
        if (rate <= 0) return 0;
        return Mathf.Max(5, SPAWN_RATE_BASE / rate);
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
        {
            input.text = clamped.ToString();
            ToastNotification.Show($"Value adjusted to {clamped}. Allowed range: {min} - {max}");
        }
        return clamped;
    }
    ToastNotification.Show($"Invalid input. Using default value: {defaultValue}");
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

    void UpdateStatsDisplay()
    {
        if (statsText == null) return;

        int merchants = 0, pirates = 0, security = 0;
        if (engine != null)
        {
            foreach (var ship in engine.GetActiveShips())
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