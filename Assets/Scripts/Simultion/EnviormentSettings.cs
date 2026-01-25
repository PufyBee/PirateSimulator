using UnityEngine;

/// <summary>
/// Environment Settings - Handles Time of Day and Weather effects
/// 
/// Apply modifiers to simulation based on conditions:
/// - Detection range multipliers
/// - Ship speed multipliers  
/// - Spawn rate multipliers
/// 
/// SETUP:
/// 1. Add this to a GameObject called "EnvironmentManager"
/// 2. Reference it from your SimulationEngine or SetupPanel
/// 3. Call ApplySettings() before simulation starts
/// </summary>
public class EnvironmentSettings : MonoBehaviour
{
    public static EnvironmentSettings Instance { get; private set; }

    [Header("=== CURRENT CONDITIONS ===")]
    public TimeOfDay timeOfDay = TimeOfDay.Morning;
    public Weather weather = Weather.Clear;

    [Header("=== CALCULATED MULTIPLIERS (Read Only) ===")]
    [SerializeField] private float detectionMultiplier = 1f;
    [SerializeField] private float speedMultiplier = 1f;
    [SerializeField] private float merchantSpawnMultiplier = 1f;
    [SerializeField] private float pirateSpawnMultiplier = 1f;
    [SerializeField] private float securitySpawnMultiplier = 1f;

    // Public access to multipliers
    public float DetectionMultiplier => detectionMultiplier;
    public float SpeedMultiplier => speedMultiplier;
    public float MerchantSpawnMultiplier => merchantSpawnMultiplier;
    public float PirateSpawnMultiplier => pirateSpawnMultiplier;
    public float SecuritySpawnMultiplier => securitySpawnMultiplier;

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// Set the time of day (call from UI dropdown)
    /// </summary>
    public void SetTimeOfDay(int index)
    {
        timeOfDay = (TimeOfDay)index;
        CalculateMultipliers();
        
        // Update visual overlay
        if (EnvironmentVisualOverlay.Instance != null)
        {
            EnvironmentVisualOverlay.Instance.SetTimeOfDay(timeOfDay);
        }
        
        Debug.Log($"Time of Day set to: {timeOfDay}");
    }

    /// <summary>
    /// Set the weather (call from UI dropdown)
    /// </summary>
    public void SetWeather(int index)
    {
        weather = (Weather)index;
        CalculateMultipliers();
        
        // Update visual overlay
        if (EnvironmentVisualOverlay.Instance != null)
        {
            EnvironmentVisualOverlay.Instance.SetWeather(weather);
        }
        
        Debug.Log($"Weather set to: {weather}");
    }

    /// <summary>
    /// Calculate all multipliers based on current conditions
    /// </summary>
    public void CalculateMultipliers()
    {
        // Start with base values
        detectionMultiplier = 1f;
        speedMultiplier = 1f;
        merchantSpawnMultiplier = 1f;
        pirateSpawnMultiplier = 1f;
        securitySpawnMultiplier = 1f;

        // Apply Time of Day effects
        ApplyTimeOfDayEffects();

        // Apply Weather effects
        ApplyWeatherEffects();

        Debug.Log($"Environment Multipliers - Detection: {detectionMultiplier:F2}, Speed: {speedMultiplier:F2}, " +
                  $"Merchant Spawn: {merchantSpawnMultiplier:F2}, Pirate Spawn: {pirateSpawnMultiplier:F2}");
    }

    private void ApplyTimeOfDayEffects()
    {
        switch (timeOfDay)
        {
            case TimeOfDay.Morning:
                // Normal conditions - baseline
                // No changes
                break;

            case TimeOfDay.Noon:
                // Peak shipping hours
                merchantSpawnMultiplier *= 1.5f;    // 50% more merchants
                securitySpawnMultiplier *= 1.25f;   // 25% more security patrols
                break;

            case TimeOfDay.Evening:
                // Pirates become more active
                pirateSpawnMultiplier *= 1.5f;      // 50% more pirates
                detectionMultiplier *= 0.8f;        // 20% reduced visibility
                break;

            case TimeOfDay.Night:
                // Hard to see, dangerous waters
                detectionMultiplier *= 0.5f;        // 50% reduced visibility
                speedMultiplier *= 0.85f;           // 15% slower (cautious navigation)
                pirateSpawnMultiplier *= 1.75f;     // 75% more pirates (cover of darkness)
                merchantSpawnMultiplier *= 0.6f;    // 40% fewer merchants (avoid night travel)
                securitySpawnMultiplier *= 0.75f;   // 25% fewer security (skeleton crew)
                break;
        }
    }

    private void ApplyWeatherEffects()
    {
        switch (weather)
        {
            case Weather.Clear:
                // Perfect conditions - baseline
                // No changes
                break;

            case Weather.Foggy:
                // Reduced visibility
                detectionMultiplier *= 0.5f;        // 50% reduced detection
                speedMultiplier *= 0.9f;            // 10% slower (careful navigation)
                pirateSpawnMultiplier *= 1.3f;      // 30% more pirates (use fog as cover)
                break;

            case Weather.Stormy:
                // Dangerous conditions
                speedMultiplier *= 0.7f;            // 30% slower
                detectionMultiplier *= 0.7f;        // 30% reduced visibility
                pirateSpawnMultiplier *= 0.5f;      // 50% fewer pirates (too dangerous)
                merchantSpawnMultiplier *= 0.7f;    // 30% fewer merchants (delayed shipments)
                break;

            case Weather.Calm:
                // Ideal sailing conditions
                speedMultiplier *= 1.2f;            // 20% faster
                merchantSpawnMultiplier *= 1.25f;   // 25% more merchants (good shipping day)
                break;
        }
    }

    /// <summary>
    /// Get a summary string for UI display
    /// </summary>
    public string GetConditionsSummary()
    {
        return $"{timeOfDay}, {weather}";
    }

    /// <summary>
    /// Get detailed effects string for UI display
    /// </summary>
    public string GetEffectsSummary()
    {
        string effects = "";
        
        if (detectionMultiplier != 1f)
            effects += $"Detection: {detectionMultiplier:P0}\n";
        if (speedMultiplier != 1f)
            effects += $"Speed: {speedMultiplier:P0}\n";
        if (merchantSpawnMultiplier != 1f)
            effects += $"Merchant Spawns: {merchantSpawnMultiplier:P0}\n";
        if (pirateSpawnMultiplier != 1f)
            effects += $"Pirate Spawns: {pirateSpawnMultiplier:P0}\n";
        if (securitySpawnMultiplier != 1f)
            effects += $"Security Spawns: {securitySpawnMultiplier:P0}\n";

        if (string.IsNullOrEmpty(effects))
            effects = "Normal conditions";

        return effects;
    }
}

/// <summary>
/// Time of day options
/// </summary>
public enum TimeOfDay
{
    Morning = 0,    // 6:00 - Normal
    Noon = 1,       // 12:00 - Peak merchant activity
    Evening = 2,    // 18:00 - Pirates more active
    Night = 3       // 22:00 - Low visibility, dangerous
}

/// <summary>
/// Weather options
/// </summary>
public enum Weather
{
    Clear = 0,      // Normal
    Foggy = 1,      // Low visibility
    Stormy = 2,     // Slow, dangerous
    Calm = 3        // Fast, good conditions
}