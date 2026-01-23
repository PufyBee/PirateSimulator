using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Simple Button Controller - Fixed Version
/// - Pause toggles on/off
/// - Reset returns to setup screen
/// - Reads input fields and applies to SimulationEngine
/// </summary>
public class SimpleButtons : MonoBehaviour
{
    [Header("=== BUTTONS ===")]
    public Button startBtn;
    public Button pauseBtn;
    public Button stopBtn;
    public Button resetBtn;
    public Button stepBtn;

    [Header("=== BUTTON TEXT (to change Pause/Resume) ===")]
    public TMP_Text pauseButtonText;

    [Header("=== REFERENCES ===")]
    public SimulationEngine engine;
    public GameObject setupPanel;      // The setup screen to show on reset
    public GameObject sidebarPanel;    // The sidebar to hide on reset

    [Header("=== INPUT FIELDS (from Setup Panel) ===")]
    public TMP_InputField merchantCountInput;
    public TMP_InputField pirateCountInput;
    public TMP_InputField securityCountInput;
    public TMP_InputField merchantSpawnRateInput;
    public TMP_InputField pirateSpawnRateInput;
    public TMP_InputField securitySpawnRateInput;
    public TMP_InputField durationInput;
    public TMP_InputField seedInput;

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
        
        // Apply settings from input fields BEFORE starting
        ApplySettingsFromInputs();
        
        if (engine) engine.StartRun();
        
        isPaused = false;
        UpdatePauseButtonText();
        SetStatus("RUNNING");
    }

    void OnPauseClicked()
    {
        Debug.Log("PAUSE/RESUME clicked");
        
        if (engine == null) return;

        if (isPaused)
        {
            // Currently paused, so RESUME
            engine.StartRun();
            isPaused = false;
            SetStatus("RUNNING");
        }
        else
        {
            // Currently running, so PAUSE
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
    }

    void OnStepClicked()
    {
        Debug.Log("STEP clicked");
        
        // Apply settings if this is the first step
        if (engine != null && engine.GetTickCount() == 0)
        {
            ApplySettingsFromInputs();
        }
        
        if (engine) engine.StepOnce();
        SetStatus("STEP");
    }

    void OnSpeedChanged(float value)
    {
        if (engine) engine.SetTickInterval(1f / value);
    }

    // ===== APPLY SETTINGS FROM INPUT FIELDS =====

    void ApplySettingsFromInputs()
    {
        if (engine == null) return;

        // Initial ship counts
        engine.initialMerchants = ParseInt(merchantCountInput, 2);
        engine.initialPirates = ParseInt(pirateCountInput, 1);
        engine.initialSecurity = ParseInt(securityCountInput, 1);

        // Spawn intervals
        engine.merchantSpawnInterval = ParseInt(merchantSpawnRateInput, 50);
        engine.pirateSpawnInterval = ParseInt(pirateSpawnRateInput, 80);
        engine.securitySpawnInterval = ParseInt(securitySpawnRateInput, 100);

        // Run settings
        engine.maxTicks = ParseInt(durationInput, 0);
        engine.runSeed = ParseInt(seedInput, 12345);

        Debug.Log($"Applied settings: Merchants={engine.initialMerchants}, Pirates={engine.initialPirates}, Security={engine.initialSecurity}");
        Debug.Log($"Spawn rates: M={engine.merchantSpawnInterval}, P={engine.pirateSpawnInterval}, S={engine.securitySpawnInterval}");
        Debug.Log($"Duration={engine.maxTicks}, Seed={engine.runSeed}");
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
        {
            pauseButtonText.text = isPaused ? "RESUME" : "PAUSE";
        }
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