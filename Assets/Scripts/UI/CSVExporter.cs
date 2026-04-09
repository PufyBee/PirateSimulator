using UnityEngine;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// CSV Exporter - Exports simulation results to downloadable CSV file
/// 
/// FIXED: Now reads cached final stats from EndOfRunPanel when available,
/// instead of pulling live engine values that have been zeroed out by reset.
/// 
/// Works in both WebGL (browser download) and Editor (saves to desktop)
/// </summary>
public class CSVExporter : MonoBehaviour
{
    public static CSVExporter Instance { get; private set; }

    [Header("=== EXPORT OPTIONS ===")]
    [Tooltip("Include individual ship events in export")]
    public bool includeDetailedEvents = true;

    [Tooltip("Include configuration settings in export")]
    public bool includeConfiguration = true;

    // Cached final-state values (set when simulation ends)
    // CSVExporter prefers these over live engine values which may have been reset
    public static int CachedEscaped;
    public static int CachedCaptured;
    public static int CachedDefeated;
    public static int CachedTicks;
    public static int CachedInitialMerchants;
    public static int CachedInitialPirates;
    public static int CachedInitialSecurity;
    public static int CachedMerchantSpawnInterval;
    public static int CachedPirateSpawnInterval;
    public static int CachedSecuritySpawnInterval;
    public static int CachedMaxTicks;
    public static int CachedRunSeed;
    public static string CachedMapName = "Unknown";
    public static string CachedTimeOfDay = "Unknown";
    public static string CachedWeather = "Unknown";
    public static float CachedDetectionMultiplier = 1f;
    public static float CachedSpeedMultiplier = 1f;
    public static bool HasCachedData = false;

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
    /// Capture simulation state before it gets reset.
    /// Call this from EndOfRunPanel.Show() or before engine.ResetToNewRun().
    /// </summary>
    public static void CacheFinalStats()
    {
        SimulationEngine engine = FindObjectOfType<SimulationEngine>();
        EnvironmentSettings env = EnvironmentSettings.Instance;
        if (env == null) env = FindObjectOfType<EnvironmentSettings>();

        if (engine != null)
        {
            CachedEscaped = engine.GetMerchantsExited();
            CachedCaptured = engine.GetMerchantsCaptured();
            CachedDefeated = engine.GetPiratesDefeated();
            CachedTicks = engine.GetTickCount();
            CachedInitialMerchants = engine.initialMerchants;
            CachedInitialPirates = engine.initialPirates;
            CachedInitialSecurity = engine.initialSecurity;
            CachedMerchantSpawnInterval = engine.merchantSpawnInterval;
            CachedPirateSpawnInterval = engine.pirateSpawnInterval;
            CachedSecuritySpawnInterval = engine.securitySpawnInterval;
            CachedMaxTicks = engine.maxTicks;
            CachedRunSeed = engine.runSeed;
        }

        if (env != null)
        {
            CachedTimeOfDay = env.timeOfDay.ToString();
            CachedWeather = env.weather.ToString();
            CachedDetectionMultiplier = env.DetectionMultiplier;
            CachedSpeedMultiplier = env.SpeedMultiplier;
        }

        if (MapManager.Instance != null)
        {
            CachedMapName = MapManager.Instance.GetCurrentMapName();
        }

        HasCachedData = true;
        Debug.Log($"CSVExporter: Cached final stats - {CachedEscaped} escaped, {CachedCaptured} captured, {CachedDefeated} defeated, {CachedTicks} ticks");
    }

    /// <summary>
    /// Clear cached stats (call when starting a new run)
    /// </summary>
    public static void ClearCache()
    {
        HasCachedData = false;
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

        // Determine source: cached values (preferred) or live engine
        int escaped, captured, defeated, ticks;
        int initialMerchants, initialPirates, initialSecurity;
        int merchantInterval, pirateInterval, securityInterval;
        int maxTicks, runSeed;
        string mapName, timeOfDay, weather;
        float detMult, spdMult;

        if (HasCachedData)
        {
            // Use cached final-state values
            escaped = CachedEscaped;
            captured = CachedCaptured;
            defeated = CachedDefeated;
            ticks = CachedTicks;
            initialMerchants = CachedInitialMerchants;
            initialPirates = CachedInitialPirates;
            initialSecurity = CachedInitialSecurity;
            merchantInterval = CachedMerchantSpawnInterval;
            pirateInterval = CachedPirateSpawnInterval;
            securityInterval = CachedSecuritySpawnInterval;
            maxTicks = CachedMaxTicks;
            runSeed = CachedRunSeed;
            mapName = CachedMapName;
            timeOfDay = CachedTimeOfDay;
            weather = CachedWeather;
            detMult = CachedDetectionMultiplier;
            spdMult = CachedSpeedMultiplier;
        }
        else
        {
            // Fall back to live engine values
            SimulationEngine engine = FindObjectOfType<SimulationEngine>();
            EnvironmentSettings env = EnvironmentSettings.Instance;
            if (env == null) env = FindObjectOfType<EnvironmentSettings>();

            escaped = engine != null ? engine.GetMerchantsExited() : 0;
            captured = engine != null ? engine.GetMerchantsCaptured() : 0;
            defeated = engine != null ? engine.GetPiratesDefeated() : 0;
            ticks = engine != null ? engine.GetTickCount() : 0;
            initialMerchants = engine != null ? engine.initialMerchants : 0;
            initialPirates = engine != null ? engine.initialPirates : 0;
            initialSecurity = engine != null ? engine.initialSecurity : 0;
            merchantInterval = engine != null ? engine.merchantSpawnInterval : 0;
            pirateInterval = engine != null ? engine.pirateSpawnInterval : 0;
            securityInterval = engine != null ? engine.securitySpawnInterval : 0;
            maxTicks = engine != null ? engine.maxTicks : 0;
            runSeed = engine != null ? engine.runSeed : 0;
            mapName = MapManager.Instance != null ? MapManager.Instance.GetCurrentMapName() : "Unknown";
            timeOfDay = env != null ? env.timeOfDay.ToString() : "Unknown";
            weather = env != null ? env.weather.ToString() : "Unknown";
            detMult = env != null ? env.DetectionMultiplier : 1f;
            spdMult = env != null ? env.SpeedMultiplier : 1f;
        }

        float protectionRate = (escaped + captured) > 0
            ? (float)escaped / (escaped + captured) * 100f
            : 100f;

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
        sb.AppendLine($"Total Ticks,{ticks}");
        sb.AppendLine($"Merchants Escaped,{escaped}");
        sb.AppendLine($"Merchants Captured,{captured}");
        sb.AppendLine($"Pirates Defeated,{defeated}");
        sb.AppendLine($"Protection Rate,{protectionRate:F1}%");
        sb.AppendLine($"Grade,{GetGrade(protectionRate)}");
        sb.AppendLine();

        // === CONFIGURATION ===
        if (includeConfiguration)
        {
            sb.AppendLine("CONFIGURATION");
            sb.AppendLine("Setting,Value");
            sb.AppendLine($"Initial Merchants,{initialMerchants}");
            sb.AppendLine($"Initial Pirates,{initialPirates}");
            sb.AppendLine($"Initial Security,{initialSecurity}");
            sb.AppendLine($"Merchant Spawn Interval,{merchantInterval}");
            sb.AppendLine($"Pirate Spawn Interval,{pirateInterval}");
            sb.AppendLine($"Security Spawn Interval,{securityInterval}");
            sb.AppendLine($"Max Ticks (Duration),{maxTicks}");
            sb.AppendLine($"Random Seed,{runSeed}");
            sb.AppendLine();
        }

        // === ENVIRONMENT CONDITIONS ===
        sb.AppendLine("ENVIRONMENT CONDITIONS");
        sb.AppendLine("Condition,Value");
        sb.AppendLine($"Time of Day,{timeOfDay}");
        sb.AppendLine($"Weather,{weather}");
        sb.AppendLine($"Detection Multiplier,{detMult:F2}");
        sb.AppendLine($"Speed Multiplier,{spdMult:F2}");
        sb.AppendLine();

        // === MAP INFO ===
        sb.AppendLine("MAP INFO");
        sb.AppendLine("Property,Value");
        sb.AppendLine($"Current Map,{mapName}");
        sb.AppendLine();

        sb.AppendLine("========================================");
        sb.AppendLine("END OF REPORT");
        sb.AppendLine("========================================");

        return sb.ToString();
    }

    string BuildSummaryCSV()
    {
        StringBuilder sb = new StringBuilder();

        int escaped, captured, defeated, ticks;
        string timeOfDay, weather;

        if (HasCachedData)
        {
            escaped = CachedEscaped;
            captured = CachedCaptured;
            defeated = CachedDefeated;
            ticks = CachedTicks;
            timeOfDay = CachedTimeOfDay;
            weather = CachedWeather;
        }
        else
        {
            SimulationEngine engine = FindObjectOfType<SimulationEngine>();
            EnvironmentSettings env = EnvironmentSettings.Instance;
            if (env == null) env = FindObjectOfType<EnvironmentSettings>();

            escaped = engine != null ? engine.GetMerchantsExited() : 0;
            captured = engine != null ? engine.GetMerchantsCaptured() : 0;
            defeated = engine != null ? engine.GetPiratesDefeated() : 0;
            ticks = engine != null ? engine.GetTickCount() : 0;
            timeOfDay = env != null ? env.timeOfDay.ToString() : "Unknown";
            weather = env != null ? env.weather.ToString() : "Unknown";
        }

        float protectionRate = (escaped + captured) > 0
            ? (float)escaped / (escaped + captured) * 100f
            : 100f;

        sb.AppendLine("Metric,Value");
        sb.AppendLine($"Ticks,{ticks}");
        sb.AppendLine($"Escaped,{escaped}");
        sb.AppendLine($"Captured,{captured}");
        sb.AppendLine($"Defeated,{defeated}");
        sb.AppendLine($"Protection Rate,{protectionRate:F1}%");
        sb.AppendLine($"Grade,{GetGrade(protectionRate)}");
        sb.AppendLine($"Time of Day,{timeOfDay}");
        sb.AppendLine($"Weather,{weather}");

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