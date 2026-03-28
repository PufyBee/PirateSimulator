using UnityEngine;

/// <summary>
/// WAYPOINT PLACEMENT TOOL
/// 
/// Click on the map to add waypoints to a selected TradeRouteData asset.
/// 
/// USAGE:
/// 1. Add this to any GameObject (e.g., TradeRouteManager)
/// 2. Drag a TradeRouteData asset into "Target Route"
/// 3. Check "Placement Mode" to enable
/// 4. Click on the map — waypoints are added in order
/// 5. Uncheck "Placement Mode" when done
/// 6. Click "Clear All Waypoints" to start over
/// 
/// Also works for pirate bases and ambush points:
/// - Set Mode to "PirateBase" to place a base position
/// - Set Mode to "AmbushPoint" to add ambush points to the selected base
/// 
/// Press Z to undo last placed waypoint.
/// </summary>
public class WaypointPlacementTool : MonoBehaviour
{
    public enum PlacementMode
    {
        RouteWaypoint,
        PirateBase,
        AmbushPoint,
        NavyPatrolPoint
    }

    [Header("=== PLACEMENT ===")]
    [Tooltip("Enable to start placing waypoints by clicking")]
    public bool placementEnabled = false;

    [Tooltip("What are we placing?")]
    public PlacementMode mode = PlacementMode.RouteWaypoint;

    [Header("=== TARGET (drag asset here) ===")]
    [Tooltip("The trade route to add waypoints to")]
    public TradeRouteData targetRoute;

    [Tooltip("The map routes asset (for pirate bases and navy patrols)")]
    public MapTradeRoutes targetMapRoutes;

    [Tooltip("Which pirate base index to add ambush points to")]
    public int pirateBaseIndex = 0;

    [Header("=== VISUAL FEEDBACK ===")]
    [Tooltip("Color of placed waypoint markers")]
    public Color waypointColor = Color.yellow;

    [Tooltip("Size of waypoint markers")]
    public float markerSize = 4f;

    [Tooltip("Show coordinates on screen when placing")]
    public bool showCoordinateLabel = true;

    [Header("=== INFO (Read Only) ===")]
    [Tooltip("Number of waypoints in the current target route")]
    public int currentWaypointCount = 0;

    [Tooltip("Last placed position")]
    public Vector2 lastPlacedPosition;

    private Camera mainCam;
    private string lastActionLog = "";

    void Start()
    {
        mainCam = Camera.main;
    }

    void Update()
    {
        if (!placementEnabled) return;
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return;

        // Hold SHIFT + Left Click to place waypoint
        // Shift bypasses all UI blocking issues
        if (Input.GetMouseButtonDown(0) && Input.GetKey(KeyCode.LeftShift))
        {
            Vector3 worldPos = mainCam.ScreenToWorldPoint(Input.mousePosition);
            Vector2 pos = new Vector2(worldPos.x, worldPos.y);

            Debug.Log($"[WaypointTool] CLICK at ({pos.x:F1}, {pos.y:F1})");
            PlacePoint(pos);
        }

        // Z to undo last
        if (Input.GetKeyDown(KeyCode.Z))
        {
            UndoLastPoint();
        }

        // Update info
        if (targetRoute != null)
            currentWaypointCount = targetRoute.waypoints != null ? targetRoute.waypoints.Count : 0;
    }

    void PlacePoint(Vector2 pos)
    {
        switch (mode)
        {
            case PlacementMode.RouteWaypoint:
                PlaceRouteWaypoint(pos);
                break;
            case PlacementMode.PirateBase:
                PlacePirateBase(pos);
                break;
            case PlacementMode.AmbushPoint:
                PlaceAmbushPoint(pos);
                break;
            case PlacementMode.NavyPatrolPoint:
                PlaceNavyPatrolPoint(pos);
                break;
        }

        lastPlacedPosition = pos;
    }

    void PlaceRouteWaypoint(Vector2 pos)
    {
        if (targetRoute == null)
        {
            Debug.LogWarning("WaypointTool: No target route assigned!");
            return;
        }

        if (targetRoute.waypoints == null)
            targetRoute.waypoints = new System.Collections.Generic.List<Vector2>();

        targetRoute.waypoints.Add(pos);
        lastActionLog = $"Added WP{targetRoute.waypoints.Count - 1} to '{targetRoute.routeName}' at ({pos.x:F1}, {pos.y:F1})";
        Debug.Log($"[WaypointTool] {lastActionLog}");

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(targetRoute);
#endif
    }

    void PlacePirateBase(Vector2 pos)
    {
        if (targetMapRoutes == null)
        {
            Debug.LogWarning("WaypointTool: No target map routes assigned!");
            return;
        }

        var newBase = new PirateBase
        {
            baseName = $"Base {targetMapRoutes.pirateBases.Count + 1}",
            position = pos,
            ambushPoints = new System.Collections.Generic.List<Vector2>(),
            maxRoamDistance = 50f,
            spawnWeight = 50
        };

        targetMapRoutes.pirateBases.Add(newBase);
        pirateBaseIndex = targetMapRoutes.pirateBases.Count - 1;
        lastActionLog = $"Added pirate base '{newBase.baseName}' at ({pos.x:F1}, {pos.y:F1})";
        Debug.Log($"[WaypointTool] {lastActionLog}");

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(targetMapRoutes);
#endif
    }

    void PlaceAmbushPoint(Vector2 pos)
    {
        if (targetMapRoutes == null || targetMapRoutes.pirateBases.Count == 0)
        {
            Debug.LogWarning("WaypointTool: No pirate bases to add ambush points to!");
            return;
        }

        int idx = Mathf.Clamp(pirateBaseIndex, 0, targetMapRoutes.pirateBases.Count - 1);
        var base_ = targetMapRoutes.pirateBases[idx];

        if (base_.ambushPoints == null)
            base_.ambushPoints = new System.Collections.Generic.List<Vector2>();

        base_.ambushPoints.Add(pos);
        lastActionLog = $"Added ambush point to '{base_.baseName}' at ({pos.x:F1}, {pos.y:F1})";
        Debug.Log($"[WaypointTool] {lastActionLog}");

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(targetMapRoutes);
#endif
    }

    void PlaceNavyPatrolPoint(Vector2 pos)
    {
        if (targetMapRoutes == null)
        {
            Debug.LogWarning("WaypointTool: No target map routes assigned!");
            return;
        }

        // Add to last patrol zone, or create one if none exist
        if (targetMapRoutes.navyPatrolZones.Count == 0)
        {
            targetMapRoutes.navyPatrolZones.Add(new NavyPatrolZone
            {
                zoneName = "Patrol Zone 1",
                patrolWaypoints = new System.Collections.Generic.List<Vector2>(),
                assignedShips = 1
            });
        }

        var zone = targetMapRoutes.navyPatrolZones[targetMapRoutes.navyPatrolZones.Count - 1];
        if (zone.patrolWaypoints == null)
            zone.patrolWaypoints = new System.Collections.Generic.List<Vector2>();

        zone.patrolWaypoints.Add(pos);
        lastActionLog = $"Added patrol point P{zone.patrolWaypoints.Count - 1} to '{zone.zoneName}' at ({pos.x:F1}, {pos.y:F1})";
        Debug.Log($"[WaypointTool] {lastActionLog}");

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(targetMapRoutes);
#endif
    }

    void UndoLastPoint()
    {
        switch (mode)
        {
            case PlacementMode.RouteWaypoint:
                if (targetRoute != null && targetRoute.waypoints != null && targetRoute.waypoints.Count > 0)
                {
                    targetRoute.waypoints.RemoveAt(targetRoute.waypoints.Count - 1);
                    lastActionLog = $"Undid last waypoint. {targetRoute.waypoints.Count} remaining.";
                    Debug.Log($"[WaypointTool] {lastActionLog}");
#if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(targetRoute);
#endif
                }
                break;

            case PlacementMode.AmbushPoint:
                if (targetMapRoutes != null && targetMapRoutes.pirateBases.Count > 0)
                {
                    int idx = Mathf.Clamp(pirateBaseIndex, 0, targetMapRoutes.pirateBases.Count - 1);
                    var base_ = targetMapRoutes.pirateBases[idx];
                    if (base_.ambushPoints != null && base_.ambushPoints.Count > 0)
                    {
                        base_.ambushPoints.RemoveAt(base_.ambushPoints.Count - 1);
                        lastActionLog = "Undid last ambush point.";
                        Debug.Log($"[WaypointTool] {lastActionLog}");
#if UNITY_EDITOR
                        UnityEditor.EditorUtility.SetDirty(targetMapRoutes);
#endif
                    }
                }
                break;

            case PlacementMode.NavyPatrolPoint:
                if (targetMapRoutes != null && targetMapRoutes.navyPatrolZones.Count > 0)
                {
                    var zone = targetMapRoutes.navyPatrolZones[targetMapRoutes.navyPatrolZones.Count - 1];
                    if (zone.patrolWaypoints != null && zone.patrolWaypoints.Count > 0)
                    {
                        zone.patrolWaypoints.RemoveAt(zone.patrolWaypoints.Count - 1);
                        lastActionLog = "Undid last patrol point.";
                        Debug.Log($"[WaypointTool] {lastActionLog}");
#if UNITY_EDITOR
                        UnityEditor.EditorUtility.SetDirty(targetMapRoutes);
#endif
                    }
                }
                break;
        }
    }

    /// <summary>
    /// Clear all waypoints from the target route. Use context menu.
    /// </summary>
    [ContextMenu("Clear All Waypoints From Target Route")]
    public void ClearTargetRoute()
    {
        if (targetRoute != null && targetRoute.waypoints != null)
        {
            targetRoute.waypoints.Clear();
            Debug.Log($"[WaypointTool] Cleared all waypoints from '{targetRoute.routeName}'");
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(targetRoute);
#endif
        }
    }

    // ===== ON-SCREEN DISPLAY =====

    void OnGUI()
    {
        if (!placementEnabled) return;

        // Mode indicator
        string modeText = mode.ToString();
        string targetText = "";

        switch (mode)
        {
            case PlacementMode.RouteWaypoint:
                targetText = targetRoute != null ? targetRoute.routeName : "NO ROUTE SELECTED";
                break;
            case PlacementMode.PirateBase:
                targetText = "Click to place new pirate base";
                break;
            case PlacementMode.AmbushPoint:
                if (targetMapRoutes != null && pirateBaseIndex < targetMapRoutes.pirateBases.Count)
                    targetText = $"Adding to: {targetMapRoutes.pirateBases[pirateBaseIndex].baseName}";
                else
                    targetText = "NO BASE SELECTED";
                break;
            case PlacementMode.NavyPatrolPoint:
                targetText = "Click to add patrol waypoints";
                break;
        }

        // Draw info box
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.fontSize = 14;
        boxStyle.alignment = TextAnchor.UpperLeft;
        boxStyle.normal.textColor = Color.white;

        string info = $"PLACEMENT MODE: {modeText}\n" +
                      $"Target: {targetText}\n" +
                      $"Waypoints: {currentWaypointCount}\n" +
                      $"Last: {lastActionLog}\n" +
                      $"Click = place | Z = undo";

        GUI.Box(new Rect(10, 10, 350, 100), info, boxStyle);

        // Show crosshair at mouse position
        if (showCoordinateLabel && mainCam != null)
        {
            Vector3 worldPos = mainCam.ScreenToWorldPoint(Input.mousePosition);
            Vector2 screenPos = Input.mousePosition;
            screenPos.y = Screen.height - screenPos.y; // Flip Y for GUI

            GUIStyle coordStyle = new GUIStyle(GUI.skin.label);
            coordStyle.fontSize = 12;
            coordStyle.normal.textColor = Color.yellow;

            GUI.Label(new Rect(screenPos.x + 15, screenPos.y - 10, 200, 25),
                $"({worldPos.x:F1}, {worldPos.y:F1})", coordStyle);
        }
    }

    // ===== GIZMOS =====

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!placementEnabled) return;

        // Draw placed waypoints for current target
        if (mode == PlacementMode.RouteWaypoint && targetRoute != null && targetRoute.waypoints != null)
        {
            Gizmos.color = waypointColor;
            for (int i = 0; i < targetRoute.waypoints.Count; i++)
            {
                Vector3 pos = new Vector3(targetRoute.waypoints[i].x, targetRoute.waypoints[i].y, 0);
                Gizmos.DrawSphere(pos, markerSize);

                // Draw line between waypoints
                if (i > 0)
                {
                    Vector3 prev = new Vector3(targetRoute.waypoints[i - 1].x, targetRoute.waypoints[i - 1].y, 0);
                    Gizmos.DrawLine(prev, pos);
                }

                // Label
                UnityEditor.Handles.color = waypointColor;
                UnityEditor.Handles.Label(pos + Vector3.up * (markerSize + 2),
                    $"WP{i}",
                    new GUIStyle()
                    {
                        fontSize = 12,
                        fontStyle = FontStyle.Bold,
                        normal = new GUIStyleState() { textColor = waypointColor }
                    });
            }
        }
    }
#endif
}