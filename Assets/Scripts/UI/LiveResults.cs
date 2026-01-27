using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Live Results Panel - Shows real-time statistics during simulation
/// Replaces the setup panel area when simulation is running
/// 
/// SETUP:
/// 1. Create a panel in same position as Setup Panel
/// 2. Add text elements for each stat
/// 3. This script shows/hides based on simulation state
/// </summary>
public class LiveResultsPanel : MonoBehaviour
{
    [Header("=== PANEL REFERENCES ===")]
    public GameObject liveResultsPanel;  // This panel
    public GameObject setupPanel;         // Setup panel to hide when this shows

    [Header("=== SIMULATION REFERENCE ===")]
    public SimulationEngine engine;

    [Header("=== HEADER DISPLAY ===")]
    public TMP_Text titleText;
    public TMP_Text conditionsText;      // "Evening, Foggy"
    public TMP_Text timeElapsedText;     // "Day 1 - 14:32"

    [Header("=== SHIP COUNTS ===")]
    public TMP_Text activeMerchantsText;
    public TMP_Text activePiratesText;
    public TMP_Text activeSecurityText;

    [Header("=== OUTCOME STATS ===")]
    public TMP_Text merchantsEscapedText;
    public TMP_Text merchantsCapturedText;
    public TMP_Text piratesDefeatedText;

    [Header("=== CALCULATED METRICS ===")]
    public TMP_Text protectionRateText;   // The key metric!
    public TMP_Text pirateSuccessText;
    public Slider protectionBar;          // Visual bar for protection rate

    [Header("=== ACTIVITY LOG ===")]
    public TMP_Text activityLogText;      // Shows recent events
    private string[] recentEvents = new string[5];
    private int eventIndex = 0;

    [Header("=== SETTINGS ===")]
    public int ticksPerHour = 60;

    // Track previous values to detect changes
    private int lastEscaped = 0;
    private int lastCaptured = 0;
    private int lastDefeated = 0;

    void Start()
    {
        // Start hidden
        if (liveResultsPanel)
            liveResultsPanel.SetActive(false);

        // Initialize event log
        for (int i = 0; i < recentEvents.Length; i++)
            recentEvents[i] = "";
    }

    void Update()
    {
        if (engine == null) return;

        // Only update when simulation is running
        if (engine.IsRunning() || engine.GetTickCount() > 0)
        {
            UpdateAllDisplays();
            CheckForNewEvents();
        }
    }

    /// <summary>
    /// Call this when simulation starts
    /// </summary>
    public void ShowLiveResults()
    {
        if (liveResultsPanel) liveResultsPanel.SetActive(true);
        if (setupPanel) setupPanel.SetActive(false);

        // Reset tracking
        lastEscaped = 0;
        lastCaptured = 0;
        lastDefeated = 0;
        eventIndex = 0;
        for (int i = 0; i < recentEvents.Length; i++)
            recentEvents[i] = "";

        if (titleText)
            titleText.text = "SIMULATION RUNNING";
    }

    /// <summary>
    /// Call this when simulation ends
    /// </summary>
    public void ShowFinalResults()
    {
        if (titleText)
            titleText.text = "SIMULATION COMPLETE";

        // Keep panel visible to show final stats
        UpdateAllDisplays();
        AddEvent("Simulation ended");
    }

    /// <summary>
    /// Call this when resetting
    /// </summary>
    public void HideLiveResults()
    {
        if (liveResultsPanel) liveResultsPanel.SetActive(false);
        if (setupPanel) setupPanel.SetActive(true);
    }

    void UpdateAllDisplays()
    {
        UpdateTimeDisplay();
        UpdateConditionsDisplay();
        UpdateShipCounts();
        UpdateOutcomes();
        UpdateMetrics();
        UpdateActivityLog();
    }

    void UpdateTimeDisplay()
    {
        if (timeElapsedText == null || engine == null) return;

        int ticks = engine.GetTickCount();
        
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

        timeElapsedText.text = $"Day {day} - {hours:D2}:{minutes:D2}  (Tick {ticks})";
    }

    void UpdateConditionsDisplay()
    {
        if (conditionsText == null) return;

        if (EnvironmentSettings.Instance != null)
        {
            conditionsText.text = EnvironmentSettings.Instance.GetConditionsSummary();
        }
        else
        {
            conditionsText.text = "Normal Conditions";
        }
    }

    void UpdateShipCounts()
    {
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

        if (activeMerchantsText) activeMerchantsText.text = $"Merchants Active: {merchants}";
        if (activePiratesText) activePiratesText.text = $"Pirates Active: {pirates}";
        if (activeSecurityText) activeSecurityText.text = $"Security Active: {security}";
    }

    void UpdateOutcomes()
    {
        if (engine == null) return;

        int escaped = engine.GetMerchantsExited();
        int captured = engine.GetMerchantsCaptured();
        int defeated = engine.GetPiratesDefeated();

        if (merchantsEscapedText)
        {
            merchantsEscapedText.text = $"✓ Escaped Safely: {escaped}";
            merchantsEscapedText.color = Color.green;
        }

        if (merchantsCapturedText)
        {
            merchantsCapturedText.text = $"✗ Captured: {captured}";
            merchantsCapturedText.color = Color.red;
        }

        if (piratesDefeatedText)
        {
            piratesDefeatedText.text = $"Pirates Defeated: {defeated}";
            piratesDefeatedText.color = Color.cyan;
        }
    }

    void UpdateMetrics()
    {
        if (engine == null) return;

        int escaped = engine.GetMerchantsExited();
        int captured = engine.GetMerchantsCaptured();
        int total = escaped + captured;

        // Protection Rate
        float protectionRate = 0f;
        if (total > 0)
        {
            protectionRate = (float)escaped / total * 100f;
        }

        if (protectionRateText)
        {
            protectionRateText.text = $"Protection Rate: {protectionRate:F1}%";
            
            // Color based on performance
            if (protectionRate >= 70)
                protectionRateText.color = Color.green;
            else if (protectionRate >= 40)
                protectionRateText.color = Color.yellow;
            else
                protectionRateText.color = Color.red;
        }

        if (protectionBar)
        {
            protectionBar.value = protectionRate / 100f;
        }

        // Pirate success rate (inverse)
        if (pirateSuccessText)
        {
            float pirateSuccess = total > 0 ? (float)captured / total * 100f : 0f;
            pirateSuccessText.text = $"Pirate Success: {pirateSuccess:F1}%";
        }
    }

    void CheckForNewEvents()
    {
        if (engine == null) return;

        int escaped = engine.GetMerchantsExited();
        int captured = engine.GetMerchantsCaptured();
        int defeated = engine.GetPiratesDefeated();

        // Check for new escapes
        if (escaped > lastEscaped)
        {
            int diff = escaped - lastEscaped;
            AddEvent($"✓ {diff} merchant(s) escaped!");
            lastEscaped = escaped;
        }

        // Check for new captures
        if (captured > lastCaptured)
        {
            int diff = captured - lastCaptured;
            AddEvent($"✗ {diff} merchant(s) captured!");
            lastCaptured = captured;
        }

        // Check for defeated pirates
        if (defeated > lastDefeated)
        {
            int diff = defeated - lastDefeated;
            AddEvent($"⚔ {diff} pirate(s) defeated!");
            lastDefeated = defeated;
        }
    }

    void AddEvent(string message)
    {
        // Shift events down
        for (int i = recentEvents.Length - 1; i > 0; i--)
        {
            recentEvents[i] = recentEvents[i - 1];
        }
        
        // Add new event at top with timestamp
        int ticks = engine != null ? engine.GetTickCount() : 0;
        recentEvents[0] = $"[{ticks}] {message}";
        
        UpdateActivityLog();
    }

    void UpdateActivityLog()
    {
        if (activityLogText == null) return;

        string log = "<b>RECENT ACTIVITY</b>\n";
        foreach (var evt in recentEvents)
        {
            if (!string.IsNullOrEmpty(evt))
                log += evt + "\n";
        }

        activityLogText.text = log;
    }
}