using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI Controller - Updated to work with new UICanvas design
/// 
/// Supports:
/// - Button-based Time of Day selection (1-4 buttons)
/// - Button-based Weather selection (1-4 buttons)
/// - Separate input fields for ship counts and spawn rates
/// - Start/Pause/Step/Restart buttons
/// - Map selection buttons
/// - Setup Panel ↔ Controls Panel switching
/// 
/// WIRING GUIDE (Inspector):
/// ─────────────────────────
/// startBtn          → UICanvas > Start Button (Root) > Start Button (Shadow) > Start Button (Button)
/// pauseBtn          → UICanvas > Main Controls (Root) > Pause Button (Button)
/// stepBtn           → UICanvas > Main Controls (Root) > Step Button (Button)
/// resetBtn          → UICanvas > Main Controls (Root) > Restart Button (Button)
/// 
/// merchantCountInput → UICanvas > Setup Bar > Config Panel > Initial Ships (Root) > Merchant Ship (Input)
/// pirateCountInput   → UICanvas > Setup Bar > Config Panel > Initial Ships (Root) > Pirate Ship (Input)
/// securityCountInput → UICanvas > Setup Bar > Config Panel > Initial Ships (Root) > Military Ship (Input)
/// 
/// merchantSpawnInput → UICanvas > Setup Bar > Config Panel > Spawn Rates (Root) > Merchant Ship (Input) 2
/// pirateSpawnInput   → UICanvas > Setup Bar > Config Panel > Spawn Rates (Root) > Pirate Ship (Input) 2
/// securitySpawnInput → UICanvas > Setup Bar > Config Panel > Spawn Rates (Root) > Military Ship (Input) 2
/// 
/// durationInput      → UICanvas > Setup Bar > Config Panel > Duration (Root) > Duration (Input)
/// seedInput          → UICanvas > Setup Bar > Config Panel > Seed (Root) > Seed (Input)
/// 
/// todButtons[0-3]    → UICanvas > Setup Bar > Config Panel > Time of Day (Root) > Time of Day Button 1-4
/// weatherButtons[0-3] → UICanvas > Setup Bar > Config Panel > Weather (Root) > Weather Button 1-4
/// 
/// setupPanel         → UICanvas > Setup Bar (Root)
/// controlsPanel      → UICanvas > Main Controls (Root)
/// startButtonRoot    → UICanvas > Start Button (Root)
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

    [Header("=== LIVE RESULTS ===")]
    public LiveResultsPanel liveResultsPanel;

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

    [Header("=== SPEED (Optional) ===")]
    public Slider speedSlider;
    public TMP_Text speedLabel;

    [Header("=== CONFIRMATION DIALOG ===")]
    public ConfirmationDialog confirmationDialog;

    [Header("=== SPAWN ZONE CONFIGURATOR ===")]
    public SpawnZoneConfigurator spawnZoneConfigurator;

    // State
    private bool isPaused = false;
    private int selectedTimeOfDay = 0;
    private int selectedWeather = 0;

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

        // Speed slider
        if (speedSlider)
        {
            speedSlider.minValue = 1f;
            speedSlider.maxValue = 20f;
            speedSlider.value = 4f;
            speedSlider.onValueChanged.AddListener(OnSpeedChanged);
        }

        // Initial UI state: show setup, hide controls
        if (setupPanel) setupPanel.SetActive(true);
        if (controlsPanel) controlsPanel.SetActive(false);
        if (startButtonRoot) startButtonRoot.SetActive(true);

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
        UpdateSpeedLabel();
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

        if (engine) engine.StartRun();

        isPaused = false;
        UpdatePauseButtonText();
        SetStatus("RUNNING");

        // Switch UI: hide setup, show controls
        if (setupPanel) setupPanel.SetActive(false);
        if (startButtonRoot) startButtonRoot.SetActive(false);
        if (controlsPanel) controlsPanel.SetActive(true);

        // Show live results panel
        if (liveResultsPanel) liveResultsPanel.ShowLiveResults();
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

        // Switch UI: show setup, hide controls
        if (setupPanel) setupPanel.SetActive(true);
        if (startButtonRoot) startButtonRoot.SetActive(true);
        if (controlsPanel) controlsPanel.SetActive(false);

        // Hide live results
        if (liveResultsPanel) liveResultsPanel.HideLiveResults();
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

    void OnSpeedChanged(float value)
    {
        if (engine) engine.SetTickInterval(1f / value);
    }

    // ===== APPLY SETTINGS =====

    void ApplyEnvironmentSettings()
    {
        if (EnvironmentSettings.Instance == null) return;

        EnvironmentSettings.Instance.SetTimeOfDay(selectedTimeOfDay);
        EnvironmentSettings.Instance.SetWeather(selectedWeather);

        Debug.Log($"Environment: {EnvironmentSettings.Instance.GetConditionsSummary()}");
    }

    void ApplySettingsFromInputs()
    {
        if (engine == null) return;

        engine.initialMerchants = ParseInt(merchantCountInput, 2);
        engine.initialPirates = ParseInt(pirateCountInput, 1);
        engine.initialSecurity = ParseInt(securityCountInput, 1);

        engine.merchantSpawnInterval = ParseInt(merchantSpawnInput, 50);
        engine.pirateSpawnInterval = ParseInt(pirateSpawnInput, 80);
        engine.securitySpawnInterval = ParseInt(securitySpawnInput, 100);

        engine.maxTicks = ParseInt(durationInput, 0);
        engine.runSeed = ParseInt(seedInput, 12345);

        Debug.Log($"Applied: M={engine.initialMerchants}, P={engine.initialPirates}, S={engine.initialSecurity}");
    }

    int ParseInt(TMP_InputField input, int defaultValue)
    {
        if (input == null) return defaultValue;
        if (string.IsNullOrEmpty(input.text)) return defaultValue;
        if (int.TryParse(input.text, out int result))
            return Mathf.Max(0, result);
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

    void UpdateSpeedLabel()
    {
        if (speedLabel && speedSlider)
            speedLabel.text = $"Speed: {speedSlider.value:F1}x";
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
}