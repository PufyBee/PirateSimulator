using UnityEngine;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// CSV Exporter - Exports simulation results to downloadable CSV file
/// 
/// Works in both WebGL (browser download) and Editor (saves to desktop)
/// 
/// SETUP:
/// 1. Add this script to any GameObject (or ButtonControls)
/// 2. Add the FileDownload.jslib to Assets/Plugins/WebGL/
/// 3. Wire a button to call ExportResults()
/// 
/// OUTPUT FORMAT:
/// - Summary stats (escaped, captured, defeated, protection rate)
/// - Configuration used (ship counts, spawn rates, duration)
/// - Environment conditions
/// - Timestamp
/// </summary>
public class CSVExporter : MonoBehaviour
{
    public static CSVExporter Instance { get; private set; }

    [Header("=== EXPORT OPTIONS ===")]
    [Tooltip("Include individual ship events in export")]
    public bool includeDetailedEvents = true;

    [Tooltip("Include configuration settings in export")]
    public bool includeConfiguration = true;

    // JavaScript interop for WebGL
    #if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void DownloadFile(string filename, string content);
    #endif

    void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// Export current simulation results to CSV
    /// Call this from a UI button
    /// </summary>
    public void ExportResults()
    {
        string csv = BuildFullCSV();
        string filename = $"pirate_sim_results_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv";

        #if UNITY_WEBGL && !UNITY_EDITOR
            DownloadFile(filename, csv);
            Debug.Log($"CSV download triggered: {filename}");
        #else
            // Editor/Standalone fallback - save to desktop
            string desktopPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
            string fullPath = System.IO.Path.Combine(desktopPath, filename);
            System.IO.File.WriteAllText(fullPath, csv);
            Debug.Log($"CSV saved to: {fullPath}");
        #endif
    }

    /// <summary>
    /// Export just the summary (smaller file)
    /// </summary>
    public void ExportSummaryOnly()
    {
        string csv = BuildSummaryCSV();
        string filename = $"pirate_sim_summary_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv";

        #if UNITY_WEBGL && !UNITY_EDITOR
            DownloadFile(filename, csv);
        #else
            string desktopPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
            string fullPath = System.IO.Path.Combine(desktopPath, filename);
            System.IO.File.WriteAllText(fullPath, csv);
            Debug.Log($"CSV saved to: {fullPath}");
        #endif
    }

    string BuildFullCSV()
    {
        StringBuilder sb = new StringBuilder();

        // Find references
        SimulationEngine engine = FindObjectOfType<SimulationEngine>();
        EnvironmentSettings env = EnvironmentSettings.Instance;
        if (env == null) env = FindObjectOfType<EnvironmentSettings>();

        // === HEADER ===
        sb.AppendLine("========================================");
        sb.AppendLine("PIRATE SIMULATION - RESULTS EXPORT");
        sb.AppendLine("========================================");
        sb.AppendLine();

        // === TIMESTAMP ===
        sb.AppendLine("EXPORT INFO");
        sb.AppendLine("Field,Value");
        sb.AppendLine($"Export Date,{System.DateTime.Now:yyyy-MM-dd}");
        sb.AppendLine($"Export Time,{System.DateTime.Now:HH:mm:ss}");
        sb.AppendLine();

        // === RESULTS SUMMARY ===
        sb.AppendLine("SIMULATION RESULTS");
        sb.AppendLine("Metric,Value");

        if (engine != null)
        {
            int escaped = engine.GetMerchantsExited();
            int captured = engine.GetMerchantsCaptured();
            int defeated = engine.GetPiratesDefeated();
            int ticks = engine.GetTickCount();

            float protectionRate = (escaped + captured) > 0
                ? (float)escaped / (escaped + captured) * 100f
                : 100f;

            string grade = GetGrade(protectionRate);

            sb.AppendLine($"Total Ticks,{ticks}");
            sb.AppendLine($"Merchants Escaped,{escaped}");
            sb.AppendLine($"Merchants Captured,{captured}");
            sb.AppendLine($"Pirates Defeated,{defeated}");
            sb.AppendLine($"Protection Rate,{protectionRate:F1}%");
            sb.AppendLine($"Grade,{grade}");
        }
        else
        {
            sb.AppendLine("No simulation data available,N/A");
        }

        sb.AppendLine();

        // === CONFIGURATION ===
        if (includeConfiguration)
        {
            sb.AppendLine("CONFIGURATION");
            sb.AppendLine("Setting,Value");

            if (engine != null)
            {
                sb.AppendLine($"Initial Merchants,{engine.initialMerchants}");
                sb.AppendLine($"Initial Pirates,{engine.initialPirates}");
                sb.AppendLine($"Initial Security,{engine.initialSecurity}");
                sb.AppendLine($"Merchant Spawn Interval,{engine.merchantSpawnInterval}");
                sb.AppendLine($"Pirate Spawn Interval,{engine.pirateSpawnInterval}");
                sb.AppendLine($"Security Spawn Interval,{engine.securitySpawnInterval}");
                sb.AppendLine($"Max Ticks (Duration),{engine.maxTicks}");
                sb.AppendLine($"Random Seed,{engine.runSeed}");
            }

            sb.AppendLine();
        }

        // === ENVIRONMENT CONDITIONS ===
        sb.AppendLine("ENVIRONMENT CONDITIONS");
        sb.AppendLine("Condition,Value");

        if (env != null)
        {
            sb.AppendLine($"Time of Day,{env.timeOfDay}");
            sb.AppendLine($"Weather,{env.weather}");
            sb.AppendLine($"Detection Multiplier,{env.DetectionMultiplier:F2}");
            sb.AppendLine($"Speed Multiplier,{env.SpeedMultiplier:F2}");
        }
        else
        {
            sb.AppendLine("Environment settings,Not available");
        }

        sb.AppendLine();

        // === MAP INFO ===
        sb.AppendLine("MAP INFO");
        sb.AppendLine("Property,Value");

        if (MapManager.Instance != null)
        {
            sb.AppendLine($"Current Map,{MapManager.Instance.GetCurrentMapName()}");
        }
        else
        {
            sb.AppendLine("Map,Unknown");
        }

        sb.AppendLine();

        // === ACTIVE SHIPS AT END ===
        sb.AppendLine("SHIPS AT END OF SIMULATION");
        sb.AppendLine("Type,Count");

        if (ShipSpawner.Instance != null)
        {
            int merchants = 0, pirates = 0, security = 0;

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

            sb.AppendLine($"Merchants,{merchants}");
            sb.AppendLine($"Pirates,{pirates}");
            sb.AppendLine($"Security,{security}");
        }

        sb.AppendLine();
        sb.AppendLine("========================================");
        sb.AppendLine("END OF REPORT");
        sb.AppendLine("========================================");

        return sb.ToString();
    }

    string BuildSummaryCSV()
    {
        StringBuilder sb = new StringBuilder();

        SimulationEngine engine = FindObjectOfType<SimulationEngine>();
        EnvironmentSettings env = EnvironmentSettings.Instance;
        if (env == null) env = FindObjectOfType<EnvironmentSettings>();

        sb.AppendLine("Metric,Value");

        if (engine != null)
        {
            int escaped = engine.GetMerchantsExited();
            int captured = engine.GetMerchantsCaptured();
            int defeated = engine.GetPiratesDefeated();
            int ticks = engine.GetTickCount();

            float protectionRate = (escaped + captured) > 0
                ? (float)escaped / (escaped + captured) * 100f
                : 100f;

            sb.AppendLine($"Ticks,{ticks}");
            sb.AppendLine($"Escaped,{escaped}");
            sb.AppendLine($"Captured,{captured}");
            sb.AppendLine($"Defeated,{defeated}");
            sb.AppendLine($"Protection Rate,{protectionRate:F1}%");
            sb.AppendLine($"Grade,{GetGrade(protectionRate)}");
        }

        if (env != null)
        {
            sb.AppendLine($"Time of Day,{env.timeOfDay}");
            sb.AppendLine($"Weather,{env.weather}");
        }

        return sb.ToString();
    }

    string GetGrade(float protectionRate)
    {
        if (protectionRate >= 90f) return "A+";
        if (protectionRate >= 80f) return "A";
        if (protectionRate >= 70f) return "B";
        if (protectionRate >= 60f) return "C";
        if (protectionRate >= 50f) return "D";
        return "F";
    }
}