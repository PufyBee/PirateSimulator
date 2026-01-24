using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI Controller with LiveResultsPanel integration
/// </summary>
public class SimpleButtons : MonoBehaviour
{
    [Header("=== BUTTONS ===")]
    public Button startBtn;
    public Button pauseBtn;
    public Button stopBtn;
    public Button resetBtn;
    public Button stepBtn;

    [Header("=== BUTTON TEXT ===")]
    public TMP_Text pauseButtonText;

    [Header("=== REFERENCES ===")]
    public SimulationEngine engine;
    public GameObject setupPanel;
    public GameObject sidebarPanel;

    [Header("=== LIVE RESULTS ===")]
    public LiveResultsPanel liveResultsPanel;

    [Header("=== SHIP COUNT INPUTS ===")]
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

    [Header("=== ENVIRONMENT DROPDOWNS ===")]
    public TMP_Dropdown timeOfDayDropdown;
    public TMP_Dropdown weatherDropdown;

    [Header("=== DISPLAY TEXT ===")]
    public TMP_Text statusText;
    public TMP_Text timeText;
    public TMP_Text statsText;

    [Header("=== SPEED ===")]
    public Slider speedSlider;
    public TMP_Text speedLabel;

    private bool isPaused = false;

    void Start()
    {
        // Connect buttons
        if (startBtn) startBtn.onClick.AddListener(OnStartClicked);
        if (pauseBtn) pauseBtn.onClick.AddListener(OnPauseClicked);
        if (stopBtn) stopBtn.onClick.AddListener(OnStopClicked);
        if (resetBtn) resetBtn.onClick.AddListener(OnResetClicked);
        if (stepBtn) stepBtn.onClick.AddListener(OnStepClicked);

        // Connect environment dropdowns
        if (timeOfDayDropdown)
            timeOfDayDropdown.onValueChanged.AddListener(OnTimeOfDayChanged);
        
        if (weatherDropdown)
            weatherDropdown.onValueChanged.AddListener(OnWeatherChanged);

        // Speed slider
        if (speedSlider)
        {
            speedSlider.minValue = 1f;
            speedSlider.maxValue = 20f;
            speedSlider.value = 4f;
            speedSlider.onValueChanged.AddListener(OnSpeedChanged);
        }

        SetStatus("READY");
        UpdatePauseButtonText();
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
        
        if (engine) engine.StartRun();
        
        isPaused = false;
        UpdatePauseButtonText();
        SetStatus("RUNNING");

        // Hide setup, show sidebar
        if (setupPanel) setupPanel.SetActive(false);
        if (sidebarPanel) sidebarPanel.SetActive(true);

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

    void OnStopClicked()
    {
        Debug.Log("STOP clicked");
        if (engine) engine.EndRun();
        isPaused = false;
        UpdatePauseButtonText();
        SetStatus("STOPPED");

        // Show final results
        if (liveResultsPanel) liveResultsPanel.ShowFinalResults();
    }

    void OnResetClicked()
    {
        Debug.Log("RESET clicked");
        
        if (engine) engine.ResetToNewRun();
        
        isPaused = false;
        UpdatePauseButtonText();
        SetStatus("READY");

        // Show setup panel, hide sidebar
        if (setupPanel) setupPanel.SetActive(true);
        if (sidebarPanel) sidebarPanel.SetActive(false);

        // Hide live results, show setup
        if (liveResultsPanel) liveResultsPanel.HideLiveResults();
    }

    void OnStepClicked()
    {
        Debug.Log("STEP clicked");
        
        if (engine != null && engine.GetTickCount() == 0)
        {
            ApplySettingsFromInputs();
            ApplyEnvironmentSettings();
            
            // Show live results on first step too
            if (liveResultsPanel) liveResultsPanel.ShowLiveResults();
        }
        
        if (engine) engine.StepOnce();
        SetStatus("STEP");
    }

    void OnSpeedChanged(float value)
    {
        if (engine) engine.SetTickInterval(1f / value);
    }

    // ===== ENVIRONMENT HANDLERS =====

    void OnTimeOfDayChanged(int index)
    {
        if (EnvironmentSettings.Instance != null)
            EnvironmentSettings.Instance.SetTimeOfDay(index);
    }

    void OnWeatherChanged(int index)
    {
        if (EnvironmentSettings.Instance != null)
            EnvironmentSettings.Instance.SetWeather(index);
    }

    void ApplyEnvironmentSettings()
    {
        if (EnvironmentSettings.Instance == null) return;

        if (timeOfDayDropdown)
            EnvironmentSettings.Instance.SetTimeOfDay(timeOfDayDropdown.value);
        
        if (weatherDropdown)
            EnvironmentSettings.Instance.SetWeather(weatherDropdown.value);

        Debug.Log($"Environment: {EnvironmentSettings.Instance.GetConditionsSummary()}");
    }

    // ===== APPLY SETTINGS =====

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

        Debug.Log($"Applied: Merchants={engine.initialMerchants}, Pirates={engine.initialPirates}, Security={engine.initialSecurity}");
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
            pauseButtonText.text = isPaused ? "RESUME" : "PAUSE";
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