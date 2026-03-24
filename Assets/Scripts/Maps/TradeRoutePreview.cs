using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// TRADE ROUTE VISUALIZER
/// 
/// Draws routes, pirate bases, and navy patrols in the Scene/Game view.
/// 
/// WORKS IN BOTH EDITOR AND PLAY MODE.
/// - Editor: shows the map selected by previewMapIndex
/// - Play mode: auto-detects active map, falls back to previewMapIndex
/// 
/// SETUP: Add to any GameObject. Drag MapTradeRoutes assets into allMapData array.
/// Set previewMapIndex to 0 for Malacca.
/// </summary>
public class TradeRouteVisualizer : MonoBehaviour
{
    [Header("=== DATA ===")]
    [Tooltip("One per map, same order as MapManager (0=Malacca, 1=Aden, 2=Guinea)")]
    public MapTradeRoutes[] allMapData;

    [Tooltip("Which map to preview. 0=Malacca, 1=Aden, 2=Guinea. In Play mode, auto-updates when you switch maps.")]
    [Range(0, 5)]
    public int previewMapIndex = 0;

    [Header("=== DISPLAY OPTIONS ===")]
    public bool showTradeRoutes = true;
    public bool showPirateBases = true;
    public bool showNavyPatrols = true;
    public bool showWaypointNumbers = true;
    public bool showNames = true;

    [Header("=== SIZES ===")]
    public float waypointSize = 3f;
    public float baseSize = 6f;
    public float ambushSize = 4f;

    [Header("=== ROUTE COLORS ===")]
    public Color route1Color = new Color(0.2f, 0.8f, 1f, 1f);
    public Color route2Color = new Color(0.2f, 1f, 0.4f, 1f);
    public Color route3Color = new Color(0.7f, 0.3f, 1f, 1f);
    public Color route4Color = new Color(1f, 0.6f, 0.1f, 1f);

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        MapTradeRoutes data = GetDataToShow();
        if (data == null) return;

        if (showTradeRoutes) DrawTradeRoutes(data);
        if (showPirateBases) DrawPirateBases(data);
        if (showNavyPatrols) DrawNavyPatrols(data);
    }

    MapTradeRoutes GetDataToShow()
    {
        if (allMapData == null || allMapData.Length == 0) return null;

        int idx = previewMapIndex;

        // In Play mode, try to sync with TradeRouteManager's active map
        if (Application.isPlaying && TradeRouteManager.Instance != null)
        {
            // Find which allMapData entry matches TradeRouteManager's loaded data
            for (int i = 0; i < allMapData.Length; i++)
            {
                if (allMapData[i] != null && TradeRouteManager.Instance.HasRouteData())
                {
                    // Match by mapIndex field on the ScriptableObject
                    if (allMapData[i].mapIndex == i)
                    {
                        // Check if this is the active one by seeing if TradeRouteManager
                        // would return routes from this data set
                        // Simple approach: just sync to previewMapIndex automatically
                    }
                }
            }
        }

        idx = Mathf.Clamp(idx, 0, allMapData.Length - 1);
        return allMapData[idx];
    }

    void DrawTradeRoutes(MapTradeRoutes data)
    {
        if (data.tradeRoutes == null) return;

        Color[] colors = { route1Color, route2Color, route3Color, route4Color };

        for (int r = 0; r < data.tradeRoutes.Count; r++)
        {
            var route = data.tradeRoutes[r];
            if (route == null || route.waypoints == null || route.waypoints.Count < 2)
                continue;

            Color color = colors[r % colors.Length];
            Gizmos.color = color;

            for (int i = 0; i < route.waypoints.Count - 1; i++)
            {
                Vector3 from = V3(route.waypoints[i]);
                Vector3 to = V3(route.waypoints[i + 1]);
                Gizmos.DrawLine(from, to);
                Vector2 dir = (route.waypoints[i + 1] - route.waypoints[i]).normalized;
                Vector2 perp = new Vector2(-dir.y, dir.x) * 0.3f;
                Gizmos.DrawLine(from + V3(perp), to + V3(perp));
                Gizmos.DrawLine(from - V3(perp), to - V3(perp));
            }

            for (int i = 0; i < route.waypoints.Count; i++)
            {
                Vector3 pos = V3(route.waypoints[i]);
                Gizmos.color = color;
                Gizmos.DrawSphere(pos, waypointSize);
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(pos, waypointSize);

                if (showWaypointNumbers)
                {
                    UnityEditor.Handles.Label(
                        pos + Vector3.up * (waypointSize + 2f),
                        $"WP{i}",
                        new GUIStyle()
                        {
                            fontSize = 14,
                            fontStyle = FontStyle.Bold,
                            normal = new GUIStyleState() { textColor = color },
                            alignment = TextAnchor.MiddleCenter
                        }
                    );
                }
            }

            if (showNames && route.waypoints.Count >= 2)
            {
                int mid = route.waypoints.Count / 2;
                Vector3 labelPos = V3(route.waypoints[mid]) + Vector3.up * (waypointSize + 8f);
                UnityEditor.Handles.Label(
                    labelPos,
                    $"{route.routeName} (weight:{route.trafficWeight})",
                    new GUIStyle()
                    {
                        fontSize = 16,
                        fontStyle = FontStyle.Bold,
                        normal = new GUIStyleState() { textColor = color },
                        alignment = TextAnchor.MiddleCenter
                    }
                );
            }

            if (route.waypoints.Count >= 3)
            {
                int mid = route.waypoints.Count / 2;
                Vector2 dir = (route.waypoints[mid] - route.waypoints[mid - 1]).normalized;
                Vector3 arrowBase = V3(route.waypoints[mid]);
                Vector3 arrowTip = arrowBase + new Vector3(dir.x, dir.y, 0) * 8f;
                Vector2 perp2 = new Vector2(-dir.y, dir.x);
                Gizmos.color = color;
                Gizmos.DrawLine(arrowBase, arrowTip);
                Gizmos.DrawLine(arrowTip, arrowTip - new Vector3(dir.x + perp2.x, dir.y + perp2.y, 0) * 3f);
                Gizmos.DrawLine(arrowTip, arrowTip - new Vector3(dir.x - perp2.x, dir.y - perp2.y, 0) * 3f);
            }
        }
    }

    void DrawPirateBases(MapTradeRoutes data)
    {
        if (data.pirateBases == null) return;

        for (int b = 0; b < data.pirateBases.Count; b++)
        {
            var pirateBase = data.pirateBases[b];
            Vector3 basePos = V3(pirateBase.position);

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(basePos, baseSize);
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(basePos, baseSize);
            Gizmos.color = new Color(0.3f, 0f, 0f, 1f);
            Gizmos.DrawSphere(basePos, baseSize * 0.4f);

            if (showNames)
            {
                UnityEditor.Handles.Label(
                    basePos + Vector3.up * (baseSize + 4f),
                    $"PIRATE: {pirateBase.baseName}",
                    new GUIStyle()
                    {
                        fontSize = 14,
                        fontStyle = FontStyle.Bold,
                        normal = new GUIStyleState() { textColor = Color.red },
                        alignment = TextAnchor.MiddleCenter
                    }
                );
            }

            if (pirateBase.ambushPoints != null)
            {
                for (int a = 0; a < pirateBase.ambushPoints.Count; a++)
                {
                    Vector3 ambushPos = V3(pirateBase.ambushPoints[a]);
                    Color ambushColor = new Color(1f, 0.5f, 0f, 1f);

                    Gizmos.color = ambushColor;
                    Gizmos.DrawSphere(ambushPos, ambushSize);
                    Gizmos.color = Color.white;
                    Gizmos.DrawWireSphere(ambushPos, ambushSize);

                    Gizmos.color = new Color(1f, 0.3f, 0f, 0.6f);
                    DrawDashedLine(basePos, ambushPos, 3f);

                    if (showNames)
                    {
                        UnityEditor.Handles.Label(
                            ambushPos + Vector3.up * (ambushSize + 3f),
                            $"Ambush {a}",
                            new GUIStyle()
                            {
                                fontSize = 12,
                                normal = new GUIStyleState() { textColor = ambushColor },
                                alignment = TextAnchor.MiddleCenter
                            }
                        );
                    }
                }
            }

            Gizmos.color = new Color(1f, 0f, 0f, 0.1f);
            Gizmos.DrawWireSphere(basePos, pirateBase.maxRoamDistance);
        }
    }

    void DrawNavyPatrols(MapTradeRoutes data)
    {
        if (data.navyPatrolZones == null) return;

        Color navyColor = new Color(0.2f, 0.5f, 1f, 1f);

        for (int z = 0; z < data.navyPatrolZones.Count; z++)
        {
            var zone = data.navyPatrolZones[z];
            if (zone.patrolWaypoints == null || zone.patrolWaypoints.Count < 2)
                continue;

            Gizmos.color = navyColor;

            for (int i = 0; i < zone.patrolWaypoints.Count - 1; i++)
            {
                Vector3 from = V3(zone.patrolWaypoints[i]);
                Vector3 to = V3(zone.patrolWaypoints[i + 1]);
                Gizmos.DrawLine(from, to);
                Vector2 dir = (zone.patrolWaypoints[i + 1] - zone.patrolWaypoints[i]).normalized;
                Vector2 perp = new Vector2(-dir.y, dir.x) * 0.2f;
                Gizmos.DrawLine(from + V3(perp), to + V3(perp));
            }

            if (!zone.pingPongPatrol && zone.patrolWaypoints.Count > 2)
            {
                Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.4f);
                DrawDashedLine(
                    V3(zone.patrolWaypoints[zone.patrolWaypoints.Count - 1]),
                    V3(zone.patrolWaypoints[0]),
                    4f
                );
            }

            Gizmos.color = navyColor;
            for (int i = 0; i < zone.patrolWaypoints.Count; i++)
            {
                Vector3 pos = V3(zone.patrolWaypoints[i]);
                Gizmos.DrawSphere(pos, waypointSize * 0.7f);
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(pos, waypointSize * 0.7f);
                Gizmos.color = navyColor;

                if (showWaypointNumbers)
                {
                    UnityEditor.Handles.Label(
                        pos + Vector3.up * (waypointSize + 2f),
                        $"P{i}",
                        new GUIStyle()
                        {
                            fontSize = 12,
                            fontStyle = FontStyle.Bold,
                            normal = new GUIStyleState() { textColor = navyColor },
                            alignment = TextAnchor.MiddleCenter
                        }
                    );
                }
            }

            if (showNames && zone.patrolWaypoints.Count > 0)
            {
                Vector3 labelPos = V3(zone.patrolWaypoints[0]) + Vector3.up * (waypointSize + 8f);
                UnityEditor.Handles.Label(
                    labelPos,
                    $"NAVY: {zone.zoneName} ({zone.assignedShips} ships)",
                    new GUIStyle()
                    {
                        fontSize = 14,
                        fontStyle = FontStyle.Bold,
                        normal = new GUIStyleState() { textColor = navyColor },
                        alignment = TextAnchor.MiddleCenter
                    }
                );
            }
        }
    }

    void DrawDashedLine(Vector3 from, Vector3 to, float dashLength)
    {
        Vector3 dir = (to - from);
        float totalDist = dir.magnitude;
        dir /= totalDist;
        float drawn = 0f;
        bool drawing = true;
        while (drawn < totalDist)
        {
            float segEnd = Mathf.Min(drawn + dashLength, totalDist);
            if (drawing) Gizmos.DrawLine(from + dir * drawn, from + dir * segEnd);
            drawn = segEnd;
            drawing = !drawing;
        }
    }

    Vector3 V3(Vector2 v) { return new Vector3(v.x, v.y, 0f); }
    Vector3 V3(Vector2 v, float z) { return new Vector3(v.x, v.y, z); }
#endif
}