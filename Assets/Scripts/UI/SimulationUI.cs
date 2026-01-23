using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Simple Simulation UI - All buttons always visible
/// No automatic hiding/disabling of buttons
/// </summary>
public class SimulationUI : MonoBehaviour
{
    [Header("=== REFERENCES ===")]
    public SimulationEngine simulationEngine;

    [Header("=== BUTTONS ===")]
    public Button startButton;
    public Button pauseButton;
    public Button stopButton;
    public Button resetButton;
    public Button stepButton;

    [Header("=== TEXT DISPLAYS ===")]
    public TMP_Text statusText;
    public TMP_Text timeText;
    public TMP_Text statsText;

    [Header("=== SPEED CONTROL ===")]
    public Slider speedSlider;
    public TMP_Text speedLabel;

    [Header("=== SETTINGS ===")]
    public int ticksPerHour = 60;
    public int startHour = 6;

    void Start()
    {
        // Connect buttons - simple click handlers
        if (startButton) startButton.onClick.AddListener(OnStart);
        if (pauseButton) pauseButton.onClick.AddListener(OnPause);
        if (stopButton) stopButton.onClick.AddListener(OnStop);
        if (resetButton) resetButton.onClick.AddListener(OnReset);
        if (stepButton) stepButton.onClick.AddListener(OnStep);

        // Speed slider
        if (speedSlider)
        {
            speedSlider.minValue = 1f;
            speedSlider.maxValue = 20f;
            speedSlider.value = 4f;
            speedSlider.onValueChanged.AddListener(OnSpeedChanged);
        }

        SetStatus("READY");
    }

    void Update()
    {
        // Just update displays, don't touch button states
        UpdateTimeDisplay();
        UpdateStatsDisplay();
        UpdateSpeedLabel();
    }

    // ===== BUTTON HANDLERS =====

    void OnStart()
    {
        Debug.Log("Start clicked");
        if (simulationEngine == null)
        {
            Debug.LogError("SimulationEngine not connected!");
            return;
        }
        simulationEngine.StartRun();
        SetStatus("RUNNING");
    }

    void OnPause()
    {
        Debug.Log("Pause clicked");
        if (simulationEngine == null) return;
        
        // Toggle pause/resume
        if (simulationEngine.IsPaused())
        {
            simulationEngine.StartRun();  // Resume
            SetStatus("RUNNING");
        }
        else
        {
            simulationEngine.PauseRun();
            SetStatus("PAUSED");
        }
    }

    void OnStop()
    {
        Debug.Log("Stop clicked");
        if (simulationEngine == null) return;
        simulationEngine.EndRun();
        SetStatus("STOPPED");
    }

    void OnReset()
    {
        Debug.Log("Reset clicked");
        if (simulationEngine == null) return;
        simulationEngine.ResetToNewRun();
        SetStatus("READY");
    }

    void OnStep()
    {
        Debug.Log("Step clicked");
        if (simulationEngine == null) return;
        simulationEngine.StepOnce();
        SetStatus("STEP");
    }

    void OnSpeedChanged(float value)
    {
        if (simulationEngine == null) return;
        simulationEngine.SetTickInterval(1f / value);
    }

    // ===== DISPLAY UPDATES =====

    void SetStatus(string text)
    {
        if (statusText)
        {
            statusText.text = text;
            
            // Color based on status
            if (text == "RUNNING") statusText.color = Color.green;
            else if (text == "PAUSED") statusText.color = Color.yellow;
            else if (text == "STOPPED") statusText.color = Color.red;
            else statusText.color = Color.white;
        }
    }

    void UpdateSpeedLabel()
    {
        if (speedLabel && speedSlider)
            speedLabel.text = $"Speed: {speedSlider.value:F1}x";
    }

    void UpdateTimeDisplay()
    {
        if (timeText == null || simulationEngine == null) return;

        int ticks = simulationEngine.GetTickCount();
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
        if (simulationEngine != null)
        {
            escaped = simulationEngine.GetMerchantsExited();
            captured = simulationEngine.GetMerchantsCaptured();
            defeated = simulationEngine.GetPiratesDefeated();
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