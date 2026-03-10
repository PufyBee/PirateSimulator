using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// DEBUG PATH VISUALIZER
/// 
/// Draws all ship paths in the Scene view so you can see exactly
/// where ships are trying to go and where paths fail.
/// 
/// SETUP:
/// 1. Add this to any GameObject
/// 2. Open Scene view (not Game view)
/// 3. Run the simulation
/// 4. See all paths drawn
/// 
/// COLOR CODE:
/// - GREEN: Merchant paths
/// - RED: Pirate paths  
/// - BLUE: Security paths
/// - YELLOW: Current waypoint
/// - MAGENTA: Ship facing direction
/// - WHITE: Direct line to final destination
/// </summary>
public class PathDebugVisualizer : MonoBehaviour
{
    [Header("=== SETTINGS ===")]
    public bool enabled = true;
    public bool showPaths = true;
    public bool showWaypoints = true;
    public bool showDestinationLine = true;
    public bool showFacingDirection = true;
    public bool showShipInfo = true;

    [Header("=== SIZES ===")]
    public float waypointSize = 0.3f;
    public float currentWaypointSize = 0.5f;
    public float pathWidth = 0.1f;

    [Header("=== REFERENCES ===")]
    public ShipSpawner shipSpawner;

    void Start()
    {
        if (shipSpawner == null)
            shipSpawner = FindObjectOfType<ShipSpawner>();
    }

    void OnDrawGizmos()
    {
        if (!enabled || !Application.isPlaying) return;
        if (shipSpawner == null) return;

        var ships = shipSpawner.GetActiveShips();
        if (ships == null) return;

        foreach (var ship in ships)
        {
            if (ship == null || ship.Data == null) continue;
            DrawShipPath(ship);
        }
    }

    void DrawShipPath(ShipController ship)
    {
        Vector3 shipPos = new Vector3(ship.Data.position.x, ship.Data.position.y, 0);

        // Set color based on ship type
        Color pathColor = GetShipColor(ship.Data.type);
        Gizmos.color = pathColor;

        // Draw ship position
        Gizmos.DrawWireSphere(shipPos, 0.4f);

        // Draw route if exists
        if (showPaths && ship.Data.route != null && ship.Data.route.waypoints != null)
        {
            var waypoints = ship.Data.route.waypoints;
            int currentIdx = ship.Data.route.currentIndex;

            // Draw completed path (dimmer)
            Gizmos.color = new Color(pathColor.r, pathColor.g, pathColor.b, 0.3f);
            for (int i = 0; i < currentIdx && i < waypoints.Count - 1; i++)
            {
                Vector3 from = new Vector3(waypoints[i].x, waypoints[i].y, 0);
                Vector3 to = new Vector3(waypoints[i + 1].x, waypoints[i + 1].y, 0);
                Gizmos.DrawLine(from, to);
            }

            // Draw remaining path (bright)
            Gizmos.color = pathColor;
            for (int i = currentIdx; i < waypoints.Count - 1; i++)
            {
                Vector3 from = new Vector3(waypoints[i].x, waypoints[i].y, 0);
                Vector3 to = new Vector3(waypoints[i + 1].x, waypoints[i + 1].y, 0);
                Gizmos.DrawLine(from, to);
            }

            // Draw waypoints
            if (showWaypoints)
            {
                for (int i = 0; i < waypoints.Count; i++)
                {
                    Vector3 wp = new Vector3(waypoints[i].x, waypoints[i].y, 0);

                    if (i == currentIdx)
                    {
                        // Current waypoint - yellow and bigger
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawWireSphere(wp, currentWaypointSize);
                    }
                    else if (i > currentIdx)
                    {
                        // Future waypoint
                        Gizmos.color = pathColor;
                        Gizmos.DrawWireSphere(wp, waypointSize);
                    }
                }
            }

            // Draw final destination marker
            if (waypoints.Count > 0)
            {
                Vector3 finalDest = new Vector3(waypoints[waypoints.Count - 1].x, waypoints[waypoints.Count - 1].y, 0);
                Gizmos.color = Color.white;
                Gizmos.DrawWireCube(finalDest, Vector3.one * 0.6f);

                // Draw direct line from ship to final destination (white dotted conceptually)
                if (showDestinationLine)
                {
                    Gizmos.color = new Color(1, 1, 1, 0.2f);
                    Gizmos.DrawLine(shipPos, finalDest);
                }
            }
        }
        else
        {
            // No route - draw X
            Gizmos.color = Color.red;
            float x = 0.5f;
            Gizmos.DrawLine(shipPos + new Vector3(-x, -x, 0), shipPos + new Vector3(x, x, 0));
            Gizmos.DrawLine(shipPos + new Vector3(-x, x, 0), shipPos + new Vector3(x, -x, 0));
        }

        // Draw facing direction
        if (showFacingDirection)
        {
            Gizmos.color = Color.magenta;
            Vector3 facingDir = new Vector3(ship.Data.velocityDir.x, ship.Data.velocityDir.y, 0).normalized;
            Gizmos.DrawLine(shipPos, shipPos + facingDir * 1f);
        }
    }

    Color GetShipColor(ShipType type)
    {
        switch (type)
        {
            case ShipType.Cargo: return Color.green;
            case ShipType.Pirate: return Color.red;
            case ShipType.Security: return Color.cyan;
            default: return Color.white;
        }
    }

#if UNITY_EDITOR
    // Also draw in editor when selected
    void OnDrawGizmosSelected()
    {
        // Draw legend
        Vector3 legendPos = Camera.main != null 
            ? Camera.main.ViewportToWorldPoint(new Vector3(0.02f, 0.98f, 10f))
            : new Vector3(-10, 10, 0);

        UnityEditor.Handles.Label(legendPos, 
            "PATH DEBUG:\n" +
            "GREEN = Merchant\n" +
            "RED = Pirate\n" +
            "CYAN = Security\n" +
            "YELLOW = Current waypoint\n" +
            "WHITE BOX = Destination\n" +
            "RED X = No route");
    }
#endif
}