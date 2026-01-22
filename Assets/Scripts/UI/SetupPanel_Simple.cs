using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Setup Panel Controller
/// Handles the pre-simulation configuration screen.
/// </summary>
public class SetupPanel : MonoBehaviour
{
    [Header("=== REFERENCES ===")]
    public SimulationEngine simulationEngine;
    public ShipSpawner shipSpawner;
    
    [Header("=== PANELS ===")]
    public GameObject setupPanel;   // This panel (to hide when sim starts)
    public GameObject sidebar;      // The sidebar (to show when sim starts)

    [Header("=== SHIP COUNT INPUTS ===")]
    public TMP_InputField merchantCountInput;
    public TMP_InputField pirateCountInput;
    public TMP_InputField securityCountInput;

    [Header("=== SPAWN RATE INPUTS ===")]
    public TMP_InputField merchantSpawnInput;
    public TMP_InputField pirateSpawnInput;
    public TMP_InputField securitySpawnInput;

    [Header("=== RUN SETTINGS ===")]
    public TMP_InputField durationInput;
    public TMP_InputField seedInput;
    public TMP_Dropdown timeOfDayDropdown;

    [Header("=== BUTTONS ===")]
    public Button startSimButton;
    public Button randomizeButton;

    void Start()
    {
        // Connect buttons
        if (startSimButton)
            startSimButton.onClick.AddListener(OnStartSimulation);
        
        if (randomizeButton)
            randomizeButton.onClick.AddListener(OnRandomizeSeed);

        // Set default values
        SetDefaults();

        // Show setup panel, hide sidebar
        if (setupPanel) setupPanel.SetActive(true);
        if (sidebar) sidebar.SetActive(false);
    }

    void SetDefaults()
    {
        if (merchantCountInput) merchantCountInput.text = "2";
        if (pirateCountInput) pirateCountInput.text = "1";
        if (securityCountInput) securityCountInput.text = "1";

        if (merchantSpawnInput) merchantSpawnInput.text = "50";
        if (pirateSpawnInput) pirateSpawnInput.text = "80";
        if (securitySpawnInput) securitySpawnInput.text = "100";

        if (durationInput) durationInput.text = "500";
        if (seedInput) seedInput.text = Random.Range(10000, 99999).ToString();
    }

    void OnRandomizeSeed()
    {
        if (seedInput)
            seedInput.text = Random.Range(10000, 99999).ToString();
    }

    void OnStartSimulation()
    {
        // Parse all inputs
        int merchantCount = ParseInt(merchantCountInput, 2);
        int pirateCount = ParseInt(pirateCountInput, 1);
        int securityCount = ParseInt(securityCountInput, 1);

        int merchantSpawn = ParseInt(merchantSpawnInput, 50);
        int pirateSpawn = ParseInt(pirateSpawnInput, 80);
        int securitySpawn = ParseInt(securitySpawnInput, 100);

        int duration = ParseInt(durationInput, 500);
        int seed = ParseInt(seedInput, 12345);

        int startHour = 6; // Default morning
        if (timeOfDayDropdown)
        {
            switch (timeOfDayDropdown.value)
            {
                case 0: startHour = 6; break;   // Morning
                case 1: startHour = 12; break;  // Noon
                case 2: startHour = 18; break;  // Evening
                case 3: startHour = 22; break;  // Night
            }
        }

        // Apply to SimulationEngine
        if (simulationEngine)
        {
            simulationEngine.initialMerchants = merchantCount;
            simulationEngine.initialPirates = pirateCount;
            simulationEngine.initialSecurity = securityCount;

            simulationEngine.merchantSpawnInterval = merchantSpawn;
            simulationEngine.pirateSpawnInterval = pirateSpawn;
            simulationEngine.securitySpawnInterval = securitySpawn;

            simulationEngine.maxTicks = duration;
            simulationEngine.runSeed = seed;
        }

        Debug.Log($"Starting simulation: {merchantCount} merchants, {pirateCount} pirates, {securityCount} security");
        Debug.Log($"Spawn rates: {merchantSpawn}/{pirateSpawn}/{securitySpawn}, Duration: {duration}, Seed: {seed}, Hour: {startHour}");

        // Hide setup panel, show sidebar
        if (setupPanel) setupPanel.SetActive(false);
        if (sidebar) sidebar.SetActive(true);

        // Start the simulation
        if (simulationEngine)
            simulationEngine.StartRun();
    }

    int ParseInt(TMP_InputField input, int defaultValue)
    {
        if (input == null) return defaultValue;
        if (int.TryParse(input.text, out int result))
            return Mathf.Max(0, result);
        return defaultValue;
    }

    /// <summary>
    /// Call this to show setup panel again (e.g., after reset)
    /// </summary>
    public void ShowSetup()
    {
        if (setupPanel) setupPanel.SetActive(true);
        if (sidebar) sidebar.SetActive(false);
    }
}