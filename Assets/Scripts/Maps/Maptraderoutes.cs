using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// MAP TRADE ROUTES - ScriptableObject
/// 
/// Contains ALL route/behavior data for a single map:
/// - Trade routes (merchant shipping lanes)
/// - Pirate staging bases (coastal spawn points + ambush zones)
/// - Navy patrol zones (looping patrol waypoints)
/// 
/// CREATE IN UNITY: Right-click in Project → Create → Piracy Sim → Map Trade Routes
/// Create one per map (Malacca, Aden, Guinea).
/// 
/// USAGE: Assigned to TradeRouteManager which reads this at runtime.
/// </summary>
[CreateAssetMenu(fileName = "NewMapTradeRoutes", menuName = "Piracy Sim/Map Trade Routes")]
public class MapTradeRoutes : ScriptableObject
{
    [Header("=== MAP IDENTITY ===")]
    public string mapName = "Unnamed Map";

    [Tooltip("Index matching MapManager's map list (0=Malacca, 1=Aden, 2=Guinea)")]
    public int mapIndex = 0;

    [Header("=== TRADE ROUTES ===")]
    [Tooltip("All merchant shipping routes for this map")]
    public List<TradeRouteData> tradeRoutes = new List<TradeRouteData>();

    [Header("=== PIRATE BASES ===")]
    [Tooltip("Coastal staging points where pirates spawn and return to after captures")]
    public List<PirateBase> pirateBases = new List<PirateBase>();

    [Header("=== NAVY PATROL ZONES ===")]
    [Tooltip("Patrol routes that navy ships loop through continuously")]
    public List<NavyPatrolZone> navyPatrolZones = new List<NavyPatrolZone>();

    [Header("=== MAP BOUNDS (for offscreen despawn) ===")]
    [Tooltip("Ships beyond this boundary are considered offscreen and can despawn")]
    public float despawnBufferUnits = 30f;

    /// <summary>
    /// Pick a random trade route weighted by traffic weight.
    /// </summary>
    public TradeRouteData GetWeightedRandomRoute(System.Random rng, ShipType shipType)
    {
        if (tradeRoutes == null || tradeRoutes.Count == 0) return null;

        // Filter by allowed ship type
        List<TradeRouteData> eligible = new List<TradeRouteData>();
        int totalWeight = 0;

        foreach (var route in tradeRoutes)
        {
            if (route != null && route.IsShipTypeAllowed(shipType))
            {
                eligible.Add(route);
                totalWeight += route.trafficWeight;
            }
        }

        if (eligible.Count == 0) return null;
        if (eligible.Count == 1) return eligible[0];

        // Weighted random selection
        int roll = rng.Next(totalWeight);
        int cumulative = 0;

        foreach (var route in eligible)
        {
            cumulative += route.trafficWeight;
            if (roll < cumulative)
                return route;
        }

        return eligible[eligible.Count - 1]; // Fallback
    }

    /// <summary>
    /// Get a random pirate base for spawning.
    /// </summary>
    public PirateBase GetRandomPirateBase(System.Random rng)
    {
        if (pirateBases == null || pirateBases.Count == 0) return null;
        return pirateBases[rng.Next(pirateBases.Count)];
    }

    /// <summary>
    /// Get a navy patrol zone (round-robin or random assignment).
    /// </summary>
    public NavyPatrolZone GetPatrolZone(int securityIndex)
    {
        if (navyPatrolZones == null || navyPatrolZones.Count == 0) return null;
        return navyPatrolZones[securityIndex % navyPatrolZones.Count];
    }
}

/// <summary>
/// A coastal point where pirates spawn and stage attacks from.
/// Pirates launch from here, ambush at nearby chokepoints, and return after captures.
/// </summary>
[System.Serializable]
public class PirateBase
{
    [Tooltip("Name for debug/display")]
    public string baseName = "Pirate Base";

    [Tooltip("World position of the base (near coastline)")]
    public Vector2 position;

    [Tooltip("Ambush points pirates patrol between (chokepoints near shipping lanes)")]
    public List<Vector2> ambushPoints = new List<Vector2>();

    [Tooltip("How far from the base pirates are willing to roam")]
    public float maxRoamDistance = 80f;

    [Tooltip("Spawn weight relative to other bases (higher = more pirates spawn here)")]
    [Range(1, 100)]
    public int spawnWeight = 50;
}

/// <summary>
/// A patrol zone that navy ships loop through continuously.
/// Navy ships cycle through these waypoints and never despawn (unless sunk).
/// </summary>
[System.Serializable]
public class NavyPatrolZone
{
    [Tooltip("Name for debug/display")]
    public string zoneName = "Patrol Zone";

    [Tooltip("Waypoints the navy ship patrols through in a loop")]
    public List<Vector2> patrolWaypoints = new List<Vector2>();

    [Tooltip("If true, navy reverses direction at endpoints instead of looping")]
    public bool pingPongPatrol = false;

    [Tooltip("How many security ships are assigned to this zone")]
    [Range(1, 5)]
    public int assignedShips = 1;
}