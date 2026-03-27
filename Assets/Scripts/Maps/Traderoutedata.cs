using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// TRADE ROUTE DATA - ScriptableObject
/// 
/// Defines a single named shipping route as an ordered list of waypoints.
/// Merchants pick a route at spawn and follow it waypoint-to-waypoint,
/// using A* pathfinding between each pair (so routes auto-navigate around land).
/// 
/// CREATE IN UNITY: Right-click in Project → Create → Piracy Sim → Trade Route
/// 
/// FIELDS:
/// - routeName: Display name (e.g., "Indian Ocean → Singapore")
/// - waypoints: Ordered list of world-space positions defining the route
/// - trafficWeight: Relative likelihood of this route being chosen (higher = more ships)
/// - allowedShipTypes: Which ship types can use this route (default: Cargo only)
/// - isReversible: If true, ships can travel the route in either direction
/// - description: For display in UI or tooltips
/// </summary>
[CreateAssetMenu(fileName = "NewTradeRoute", menuName = "Piracy Sim/Trade Route")]
public class TradeRouteData : ScriptableObject
{
    [Header("=== ROUTE IDENTITY ===")]
    [Tooltip("Display name for this route")]
    public string routeName = "Unnamed Route";

    [Tooltip("Description for UI/tooltips")]
    [TextArea(2, 4)]
    public string description = "";

    [Header("=== WAYPOINTS ===")]
    [Tooltip("Ordered waypoints defining this route. Ships pathfind between consecutive points.")]
    public List<Vector2> waypoints = new List<Vector2>();

    [Tooltip("If true, ships can spawn at either end and travel in reverse")]
    public bool isReversible = true;

    [Header("=== TRAFFIC ===")]
    [Tooltip("Relative weight for route selection. Higher = more merchant traffic on this route.")]
    [Range(1, 100)]
    public int trafficWeight = 50;

    [Tooltip("Which ship types are allowed on this route")]
    public List<ShipType> allowedShipTypes = new List<ShipType> { ShipType.Cargo };

    [Header("=== VISUAL (Optional) ===")]
    [Tooltip("Color for debug visualization of this route")]
    public Color debugColor = Color.cyan;

    /// <summary>
    /// Get waypoints in forward order (index 0 → last)
    /// </summary>
    public List<Vector2> GetForwardWaypoints()
    {
        return new List<Vector2>(waypoints);
    }

    /// <summary>
    /// Get waypoints in reverse order (last → index 0)
    /// </summary>
    public List<Vector2> GetReverseWaypoints()
    {
        var reversed = new List<Vector2>(waypoints);
        reversed.Reverse();
        return reversed;
    }

    /// <summary>
    /// Get waypoints in a random direction (if reversible) using the provided RNG.
    /// </summary>
    public List<Vector2> GetDirectionalWaypoints(System.Random rng)
    {
        if (isReversible && rng.NextDouble() < 0.5)
            return GetReverseWaypoints();
        return GetForwardWaypoints();
    }

    /// <summary>
    /// Get the spawn position (first waypoint of the chosen direction)
    /// </summary>
    public Vector2 GetSpawnPosition(bool reverse)
    {
        if (waypoints == null || waypoints.Count == 0)
            return Vector2.zero;

        return reverse ? waypoints[waypoints.Count - 1] : waypoints[0];
    }

    /// <summary>
    /// Check if a given ship type is allowed on this route
    /// </summary>
    public bool IsShipTypeAllowed(ShipType type)
    {
        if (allowedShipTypes == null || allowedShipTypes.Count == 0)
            return true; // No restrictions = all allowed
        return allowedShipTypes.Contains(type);
    }

    /// <summary>
    /// Get total approximate route length (sum of distances between waypoints)
    /// </summary>
    public float GetApproximateLength()
    {
        if (waypoints == null || waypoints.Count < 2) return 0f;

        float total = 0f;
        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            total += Vector2.Distance(waypoints[i], waypoints[i + 1]);
        }
        return total;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Draw route in Scene view when selected
    /// </summary>
    private void OnValidate()
    {
        // Ensure at least 2 waypoints
        if (waypoints != null && waypoints.Count == 1)
        {
            Debug.LogWarning($"Trade Route '{routeName}' has only 1 waypoint. Routes need at least 2.");
        }
    }
#endif
}