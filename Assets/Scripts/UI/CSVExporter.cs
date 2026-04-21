using UnityEngine;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// CSV Exporter - Comprehensive simulation results export
/// 
/// Caches all stats before engine reset, exports to browser download or desktop file.
/// Matches EndOfRunPanel data output.
/// </summary>
public class CSVExporter : MonoBehaviour
{
    public static CSVExporter Instance { get; private set; }

    // JavaScript interop for WebGL
    #if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void DownloadFile(string filename, string content);
    #endif

    // Cached final-state values
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
    public static int CachedTotalMerchantsSpawned;
    public static int CachedTotalPiratesSpawned;
    public static int CachedTotalSecuritySpawned;
    public static int CachedPeakActiveShips;
    public static int CachedPeakActivePirates;
    public static int CachedPeakActiveMerchants;
    public static float CachedSimHours;
    public static float CachedSimDays;
    public static string CachedMapName = "Unknown";
    public static string CachedTimeOfDay = "Unknown";
    public static string CachedWeather = "Unknown";
    public static float CachedDetectionMultiplier = 1f;
    public static float CachedSpeedMultiplier = 1f;
    public static float CachedMerchantSpawnMultiplier = 1f;
    public static float CachedPirateSpawnMultiplier = 1f;
    public static float CachedSecuritySpawnMultiplier = 1f;
    public static bool HasCachedData = false;

    void Awake()
    {
        Instance = this;
    }

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
            CachedTotalMerchantsSpawned = engine.GetTotalMerchantsSpawned();
            CachedTotalPiratesSpawned = engine.GetTotalPiratesSpawned();
            CachedTotalSecuritySpawned = engine.GetTotalSecuritySpawned();
            CachedPeakActiveShips = engine.GetPeakActiveShips();
            CachedPeakActivePirates = engine.GetPeakActivePirates();
            CachedPeakActiveMerchants = engine.GetPeakActiveMerchants();
            CachedSimHours = engine.GetSimulatedHours();
            CachedSimDays = engine.GetSimulatedDays();
        }

        if (env != null)
        {
            CachedTimeOfDay = env.timeOfDay.ToString();
            CachedWeather = env.weather.ToString();
            CachedDetectionMultiplier = env.DetectionMultiplier;
            CachedSpeedMultiplier = env.SpeedMultiplier;
            CachedMerchantSpawnMultiplier = env.MerchantSpawnMultiplier;
            CachedPirateSpawnMultiplier = env.PirateSpawnMultiplier;
            CachedSecuritySpawnMultiplier = env.SecuritySpawnMultiplier;
        }

        if (MapManager.Instance != null)
            CachedMapName = MapManager.Instance.GetCurrentMapName();

        HasCachedData = true;
    }

    public static void ClearCache()
    {
        HasCachedData = false;
    }

    public void ExportResults()
    {
        string csv = BuildCSV();
        string filename = $"pirate_sim_results_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv";

        #if UNITY_WEBGL && !UNITY_EDITOR
            DownloadFile(filename, csv);
            Debug.Log($"CSV download triggered: {filename}");
        #else
            string desktopPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
            string fullPath = System.IO.Path.Combine(desktopPath, filename);
            System.IO.File.WriteAllText(fullPath, csv);
            Debug.Log($"CSV saved to: {fullPath}");
        #endif
    }

    string BuildCSV()
    {
        StringBuilder sb = new StringBuilder();

        int escaped, captured, defeated, ticks;
        int initialM, initialP, initialS;
        int spawnIntervalM, spawnIntervalP, spawnIntervalS;
        int maxTicks, seed;
        int totalSpawnedM, totalSpawnedP, totalSpawnedS;
        int peakShips, peakPirates, peakMerchants;
        float simHours, simDays;
        string mapName, tod, weather;
        float detMult, spdMult, mSpawnMult, pSpawnMult, sSpawnMult;

        if (HasCachedData)
        {
            escaped = CachedEscaped;
            captured = CachedCaptured;
            defeated = CachedDefeated;
            ticks = CachedTicks;
            initialM = CachedInitialMerchants;
            initialP = CachedInitialPirates;
            initialS = CachedInitialSecurity;
            spawnIntervalM = CachedMerchantSpawnInterval;
            spawnIntervalP = CachedPirateSpawnInterval;
            spawnIntervalS = CachedSecuritySpawnInterval;
            maxTicks = CachedMaxTicks;
            seed = CachedRunSeed;
            totalSpawnedM = CachedTotalMerchantsSpawned;
            totalSpawnedP = CachedTotalPiratesSpawned;
            totalSpawnedS = CachedTotalSecuritySpawned;
            peakShips = CachedPeakActiveShips;
            peakPirates = CachedPeakActivePirates;
            peakMerchants = CachedPeakActiveMerchants;
            simHours = CachedSimHours;
            simDays = CachedSimDays;
            mapName = CachedMapName;
            tod = CachedTimeOfDay;
            weather = CachedWeather;
            detMult = CachedDetectionMultiplier;
            spdMult = CachedSpeedMultiplier;
            mSpawnMult = CachedMerchantSpawnMultiplier;
            pSpawnMult = CachedPirateSpawnMultiplier;
            sSpawnMult = CachedSecuritySpawnMultiplier;
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
            initialM = engine != null ? engine.initialMerchants : 0;
            initialP = engine != null ? engine.initialPirates : 0;
            initialS = engine != null ? engine.initialSecurity : 0;
            spawnIntervalM = engine != null ? engine.merchantSpawnInterval : 0;
            spawnIntervalP = engine != null ? engine.pirateSpawnInterval : 0;
            spawnIntervalS = engine != null ? engine.securitySpawnInterval : 0;
            maxTicks = engine != null ? engine.maxTicks : 0;
            seed = engine != null ? engine.runSeed : 0;
            totalSpawnedM = engine != null ? engine.GetTotalMerchantsSpawned() : 0;
            totalSpawnedP = engine != null ? engine.GetTotalPiratesSpawned() : 0;
            totalSpawnedS = engine != null ? engine.GetTotalSecuritySpawned() : 0;
            peakShips = engine != null ? engine.GetPeakActiveShips() : 0;
            peakPirates = engine != null ? engine.GetPeakActivePirates() : 0;
            peakMerchants = engine != null ? engine.GetPeakActiveMerchants() : 0;
            simHours = engine != null ? engine.GetSimulatedHours() : 0;
            simDays = engine != null ? engine.GetSimulatedDays() : 0;
            mapName = MapManager.Instance != null ? MapManager.Instance.GetCurrentMapName() : "Unknown";
            tod = env != null ? env.timeOfDay.ToString() : "Unknown";
            weather = env != null ? env.weather.ToString() : "Unknown";
            detMult = env != null ? env.DetectionMultiplier : 1f;
            spdMult = env != null ? env.SpeedMultiplier : 1f;
            mSpawnMult = env != null ? env.MerchantSpawnMultiplier : 1f;
            pSpawnMult = env != null ? env.PirateSpawnMultiplier : 1f;
            sSpawnMult = env != null ? env.SecuritySpawnMultiplier : 1f;
        }

        // Derived stats
        int totalMerchants = escaped + captured;
        float protectionRate = totalMerchants > 0 ? (float)escaped / totalMerchants * 100f : 100f;
        float captureRate = totalMerchants > 0 ? (float)captured / totalMerchants * 100f : 0f;
        float capturesPerDay = simDays > 0 ? captured / simDays : 0f;
        float defeatsPerDay = simDays > 0 ? defeated / simDays : 0f;
        float merchantsPerDay = simDays > 0 ? totalMerchants / simDays : 0f;
        float pirateEfficiency = totalSpawnedP > 0 ? (float)captured / totalSpawnedP * 100f : 0f;
        float navyEfficiency = totalSpawnedS > 0 ? (float)defeated / totalSpawnedS * 100f : 0f;
        int totalShipsSpawned = totalSpawnedM + totalSpawnedP + totalSpawnedS;

        // Build CSV
        sb.AppendLine("PIRATE SIMULATION - RESULTS EXPORT");
        sb.AppendLine();

        sb.AppendLine("EXPORT INFO");
        sb.AppendLine("Field,Value");
        sb.AppendLine($"Export Date,{System.DateTime.Now:yyyy-MM-dd}");
        sb.AppendLine($"Export Time,{System.DateTime.Now:HH:mm:ss}");
        sb.AppendLine();

        sb.AppendLine("RUN INFO");
        sb.AppendLine("Field,Value");
        sb.AppendLine($"Map,{mapName}");
        sb.AppendLine($"Total Ticks,{ticks}");
        sb.AppendLine($"Simulated Hours,{simHours:F1}");
        sb.AppendLine($"Simulated Days,{simDays:F1}");
        sb.AppendLine($"Random Seed,{seed}");
        sb.AppendLine($"Max Duration (ticks),{maxTicks}");
        sb.AppendLine();

        sb.AppendLine("CONFIGURATION");
        sb.AppendLine("Field,Value");
        sb.AppendLine($"Initial Merchants,{initialM}");
        sb.AppendLine($"Initial Pirates,{initialP}");
        sb.AppendLine($"Initial Security,{initialS}");
        sb.AppendLine($"Merchant Spawn Interval,{spawnIntervalM}");
        sb.AppendLine($"Pirate Spawn Interval,{spawnIntervalP}");
        sb.AppendLine($"Security Spawn Interval,{spawnIntervalS}");
        sb.AppendLine();

        sb.AppendLine("ENVIRONMENT CONDITIONS");
        sb.AppendLine("Field,Value");
        sb.AppendLine($"Time of Day,{tod}");
        sb.AppendLine($"Weather,{weather}");
        sb.AppendLine($"Detection Multiplier,{detMult:F2}");
        sb.AppendLine($"Speed Multiplier,{spdMult:F2}");
        sb.AppendLine($"Merchant Spawn Multiplier,{mSpawnMult:F2}");
        sb.AppendLine($"Pirate Spawn Multiplier,{pSpawnMult:F2}");
        sb.AppendLine($"Security Spawn Multiplier,{sSpawnMult:F2}");
        sb.AppendLine();

        sb.AppendLine("OUTCOMES");
        sb.AppendLine("Field,Value");
        sb.AppendLine($"Merchants Escaped,{escaped}");
        sb.AppendLine($"Merchants Captured,{captured}");
        sb.AppendLine($"Pirates Defeated,{defeated}");
        sb.AppendLine($"Total Merchants Processed,{totalMerchants}");
        sb.AppendLine();

        sb.AppendLine("ANALYSIS");
        sb.AppendLine("Field,Value");
        sb.AppendLine($"Protection Rate,{protectionRate:F1}%");
        sb.AppendLine($"Capture Rate,{captureRate:F1}%");
        sb.AppendLine($"Captures Per Day,{capturesPerDay:F1}");
        sb.AppendLine($"Defeats Per Day,{defeatsPerDay:F1}");
        sb.AppendLine($"Merchants Per Day,{merchantsPerDay:F1}");
        sb.AppendLine($"Pirate Efficiency,{pirateEfficiency:F1}%");
        sb.AppendLine($"Navy Efficiency,{navyEfficiency:F1}%");
        sb.AppendLine();

        sb.AppendLine("SPAWN TOTALS");
        sb.AppendLine("Field,Value");
        sb.AppendLine($"Total Merchants Spawned,{totalSpawnedM}");
        sb.AppendLine($"Total Pirates Spawned,{totalSpawnedP}");
        sb.AppendLine($"Total Security Spawned,{totalSpawnedS}");
        sb.AppendLine($"Total Ships Spawned,{totalShipsSpawned}");
        sb.AppendLine();

        sb.AppendLine("PEAK ACTIVITY");
        sb.AppendLine("Field,Value");
        sb.AppendLine($"Peak Active Ships,{peakShips}");
        sb.AppendLine($"Peak Active Merchants,{peakMerchants}");
        sb.AppendLine($"Peak Active Pirates,{peakPirates}");
        sb.AppendLine();

        sb.AppendLine("END OF REPORT");

        return sb.ToString();
    }
}