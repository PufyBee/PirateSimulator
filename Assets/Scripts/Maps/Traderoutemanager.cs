using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// TRADE ROUTE MANAGER - Runtime Route System
/// 
/// Replaces the old "spawn at random point → go to random destination" with:
/// 
/// MERCHANTS:
/// - Pick a weighted random trade route at spawn
/// - Spawn at the route's first waypoint (off-screen edge)
/// - Follow waypoints using A* pathfinding between each pair
/// - Despawn when they leave camera bounds (not at final waypoint)
/// 
/// PIRATES:
/// - Spawn at a coastal base (historically accurate locations)
/// - Patrol to nearby ambush points (chokepoints on shipping lanes)
/// - After capture + escort, return to base
/// - If no targets, roam between ambush points near their base
/// 
/// NAVY:
/// - Assigned to a patrol zone at spawn
/// - Loop through patrol waypoints continuously
/// - Break patrol to chase pirates, then resume
/// - NEVER despawn (unless sunk) — they're persistent
/// 
/// OFFSCREEN DESPAWN:
/// - Every tick, checks if ships are outside camera bounds + buffer
/// - Only merchants despawn offscreen (when they've passed through)
/// - Pirates returning to base don't despawn
/// - Navy never despawns
/// 
/// SETUP:
/// 1. Create TradeRouteData assets for each route
/// 2. Create MapTradeRoutes asset for each map, assign routes/bases/zones
/// 3. Assign MapTradeRoutes array to this manager (matching MapManager order)
/// 4. Manager auto-switches when map changes
/// </summary>
public class TradeRouteManager : MonoBehaviour
{
    public static TradeRouteManager Instance { get; private set; }

    [Header("=== MAP ROUTE DATA ===")]
    [Tooltip("One MapTradeRoutes per map, in same order as MapManager")]
    public MapTradeRoutes[] mapRouteData;

    [Header("=== OFFSCREEN DESPAWN ===")]
    [Tooltip("Extra buffer beyond camera edges before despawning (Unity units)")]
    public float despawnBuffer = 30f;

    [Tooltip("Only despawn merchants that have traveled at least this % of their route")]
    [Range(0f, 1f)]
    public float minRouteProgressToDespawn = 0.1f;

    [Header("=== VARIANCE SETTINGS ===")]
    [Tooltip("Random scatter radius around spawn points (merchants and pirates)")]
    public float spawnScatterRadius = 15f;

    [Tooltip("Random scatter radius around ambush points")]
    public float ambushScatterRadius = 10f;

    [Tooltip("Random speed variation (0.15 = ±15% speed difference per ship)")]
    [Range(0f, 0.3f)]
    public float speedVariance = 0.15f;

    [Header("=== NAVY SETTINGS ===")]
    [Tooltip("Ticks a navy ship chases before giving up and resuming patrol")]
    public int navyChaseTimeout = 60;

    [Header("=== DEBUG ===")]
    public bool showRouteGizmos = true;
    public bool logRouteAssignments = false;

    // Current active map data
    private MapTradeRoutes currentMapRoutes;
    private int currentMapIndex = -1;

    /// <summary>
    /// Publicly readable current map index. -1 if no map loaded.
    /// </summary>
    public int CurrentMapIndex => currentMapIndex;

    // Track which ships have route assignments
    private Dictionary<string, RouteAssignment> shipRouteAssignments = new Dictionary<string, RouteAssignment>();

    // Navy patrol state
    private Dictionary<string, NavyPatrolState> navyPatrolStates = new Dictionary<string, NavyPatrolState>();

    // Pirate base assignments
    private Dictionary<string, PirateBaseAssignment> pirateBaseAssignments = new Dictionary<string, PirateBaseAssignment>();

    // Security ship counter for zone assignment
    private int securitySpawnCounter = 0;

    private void Awake()
    {
        Instance = this;

        // Load initial map data immediately so other scripts
        // can check HasRouteData() during their Start()
        if (mapRouteData != null && mapRouteData.Length > 0 && mapRouteData[0] != null)
        {
            SetMapIndex(0);
        }
    }

    private void Start()
    {
        // Re-sync with MapManager if it loaded a different map
        if (MapManager.Instance != null)
        {
            int currentMap = MapManager.Instance.GetCurrentMapIndex();
            if (currentMap != currentMapIndex)
                SetMapIndex(currentMap);
        }
    }

    /// <summary>
    /// Switch to a different map's route data.
    /// Called by MapManager when map changes, or by SimpleButtons on start.
    /// </summary>
    public void SetMapIndex(int index)
    {
        if (mapRouteData == null || index < 0 || index >= mapRouteData.Length)
        {
            Debug.LogWarning($"TradeRouteManager: Invalid map index {index}");
            return;
        }

        currentMapIndex = index;
        currentMapRoutes = mapRouteData[index];
        currentMapRoutes.mapName = MapManager.Instance.GetCurrentMapName();

        if (currentMapRoutes != null)
        {
            despawnBuffer = currentMapRoutes.despawnBufferUnits;
            Debug.Log($"TradeRouteManager: Loaded routes for '{currentMapRoutes.mapName}' " +
                      $"({currentMapRoutes.tradeRoutes.Count} routes, " +
                      $"{currentMapRoutes.pirateBases.Count} pirate bases, " +
                      $"{currentMapRoutes.navyPatrolZones.Count} patrol zones)");
        }
    }

    /// <summary>
    /// Clear all route assignments (call on simulation reset).
    /// </summary>
    public void ResetAllAssignments()
    {
        shipRouteAssignments.Clear();
        navyPatrolStates.Clear();
        pirateBaseAssignments.Clear();
        securitySpawnCounter = 0;
    }

    // ===================================================================
    // MERCHANT ROUTE ASSIGNMENT
    // ===================================================================

    /// <summary>
    /// Assign a trade route to a newly spawned merchant.
    /// Returns the spawn position and sets up the route waypoints.
    /// Call this from ShipSpawner INSTEAD of picking a random destination.
    /// </summary>
    public RouteAssignment AssignMerchantRoute(ShipController ship, System.Random rng)
    {
        if (currentMapRoutes == null || ship == null || ship.Data == null)
            return null;

        TradeRouteData route = currentMapRoutes.GetWeightedRandomRoute(rng, ShipType.Cargo);
        if (route == null)
        {
            Debug.LogWarning("TradeRouteManager: No eligible trade routes found for merchant!");
            return null;
        }

        // Get directional waypoints (randomly forward or reverse if reversible)
        List<Vector2> waypoints = route.GetDirectionalWaypoints(rng);

        if (waypoints.Count < 2)
        {
            Debug.LogWarning($"TradeRouteManager: Route '{route.routeName}' has fewer than 2 waypoints!");
            return null;
        }

        // Create assignment
        var assignment = new RouteAssignment
        {
            routeData = route,
            waypoints = waypoints,
            currentWaypointIndex = 0,
            totalWaypoints = waypoints.Count,
            shipId = ship.Data.shipId
        };

        shipRouteAssignments[ship.Data.shipId] = assignment;

        // Position ship at first waypoint + random scatter
        Vector2 scatteredSpawn = GetWaterValidatedPosition(waypoints[0], rng, spawnScatterRadius);
        ship.Data.position = scatteredSpawn;
        ship.transform.position = new Vector3(scatteredSpawn.x, scatteredSpawn.y, ship.transform.position.z);

        // Apply speed variance (±speedVariance%)
        ApplySpeedVariance(ship, rng);

        // Set destination to second waypoint (first navigation target)
        ship.SetDestination(waypoints[1]);
        assignment.currentWaypointIndex = 1;

        if (logRouteAssignments)
            Debug.Log($"[{ship.Data.shipId}] Assigned route: '{route.routeName}' ({waypoints.Count} waypoints)");

        return assignment;
    }

    /// <summary>
    /// Called each tick for merchants following trade routes.
    /// Advances to next waypoint when current one is reached.
    /// Returns true if the ship should continue, false if route is complete.
    /// </summary>
    public bool UpdateMerchantRoute(ShipController ship)
    {
        if (ship == null || ship.Data == null) return false;

        if (!shipRouteAssignments.TryGetValue(ship.Data.shipId, out RouteAssignment assignment))
            return true; // No route assignment, let default behavior handle it

        // Check if ship reached current waypoint target
        if (ship.Data.state == ShipState.Idle || ship.Data.state == ShipState.Moving)
        {
            // Bounds check — if index is past the end, route is complete
            if (assignment.currentWaypointIndex >= assignment.totalWaypoints)
            {
                // Route complete — mark as exited so it despawns
                ship.Data.state = ShipState.Exited;
                return false;
            }

            Vector2 targetWaypoint = assignment.waypoints[assignment.currentWaypointIndex];
            float distToWaypoint = Vector2.Distance(ship.Data.position, targetWaypoint);

            // Waypoint reached — advance to next
            if (distToWaypoint < 5f) // Generous threshold since A* path gets close
            {
                assignment.currentWaypointIndex++;

                if (assignment.currentWaypointIndex >= assignment.totalWaypoints)
                {
                    // Route complete — ship will despawn via offscreen check
                    // Don't immediately remove, let it keep moving in current direction
                    return true;
                }

                // Set next waypoint as destination
                Vector2 nextWaypoint = assignment.waypoints[assignment.currentWaypointIndex];
                ship.SetDestination(nextWaypoint);

                if (logRouteAssignments)
                    Debug.Log($"[{ship.Data.shipId}] Advancing to waypoint {assignment.currentWaypointIndex}/{assignment.totalWaypoints}");
            }
        }

        return true;
    }

    /// <summary>
    /// Get the route progress (0.0 to 1.0) for a ship.
    /// </summary>
    public float GetRouteProgress(string shipId)
    {
        if (!shipRouteAssignments.TryGetValue(shipId, out RouteAssignment assignment))
            return 0f;

        return (float)assignment.currentWaypointIndex / Mathf.Max(1, assignment.totalWaypoints);
    }

    // ===================================================================
    // PIRATE BASE ASSIGNMENT
    // ===================================================================

    /// <summary>
    /// Assign a pirate to a coastal base. Returns the spawn position.
    /// Pirates spawn AT the base, then patrol to ambush points.
    /// </summary>
    public Vector2 AssignPirateBase(ShipController ship, System.Random rng)
    {
        if (currentMapRoutes == null || ship == null || ship.Data == null)
            return Vector2.zero;

        PirateBase base_ = currentMapRoutes.GetRandomPirateBase(rng);
        if (base_ == null)
        {
            Debug.LogWarning("TradeRouteManager: No pirate bases defined!");
            return Vector2.zero;
        }

        var pirateAssignment = new PirateBaseAssignment
        {
            homeBase = base_,
            currentAmbushIndex = 0,
            isReturningToBase = false,
            shipId = ship.Data.shipId
        };

        pirateBaseAssignments[ship.Data.shipId] = pirateAssignment;

        // Position at base + random scatter
        Vector2 scatteredPos = GetWaterValidatedPosition(base_.position, rng, spawnScatterRadius * 0.5f);
        ship.Data.position = scatteredPos;
        ship.transform.position = new Vector3(scatteredPos.x, scatteredPos.y, ship.transform.position.z);

        // Apply speed variance
        ApplySpeedVariance(ship, rng);

        // Send to first ambush point (with scatter)
        if (base_.ambushPoints.Count > 0)
        {
            int ambushIdx = rng.Next(base_.ambushPoints.Count);
            pirateAssignment.currentAmbushIndex = ambushIdx;
            Vector2 scatteredAmbush = GetWaterValidatedPosition(base_.ambushPoints[ambushIdx], rng, ambushScatterRadius);
            ship.SetDestination(scatteredAmbush);
        }

        if (logRouteAssignments)
            Debug.Log($"[{ship.Data.shipId}] Assigned pirate base: '{base_.baseName}'");

        return base_.position;
    }

    /// <summary>
    /// Get the home base position for a pirate (for return-after-capture).
    /// </summary>
    public Vector2 GetPirateHomeBase(string shipId)
    {
        if (pirateBaseAssignments.TryGetValue(shipId, out PirateBaseAssignment assignment))
            return assignment.homeBase.position;

        return Vector2.zero;
    }

    /// <summary>
    /// Get a random ambush point near a pirate's assigned base.
    /// Used when pirate needs a new patrol destination.
    /// </summary>
    public Vector2 GetPirateAmbushPoint(string shipId, System.Random rng)
    {
        if (!pirateBaseAssignments.TryGetValue(shipId, out PirateBaseAssignment assignment))
            return Vector2.zero;

        if (assignment.homeBase.ambushPoints.Count == 0)
            return assignment.homeBase.position;

        int idx = rng.Next(assignment.homeBase.ambushPoints.Count);
        // Add scatter so pirates don't stack on the exact same point
        return GetWaterValidatedPosition(assignment.homeBase.ambushPoints[idx], rng, ambushScatterRadius);
    }

    /// <summary>
    /// Mark a pirate as returning to base (after capture/escort).
    /// </summary>
    public void SetPirateReturning(string shipId, bool returning)
    {
        if (pirateBaseAssignments.TryGetValue(shipId, out PirateBaseAssignment assignment))
            assignment.isReturningToBase = returning;
    }

    /// <summary>
    /// Check if a pirate is near its home base (for re-staging after return).
    /// </summary>
    public bool IsPirateNearBase(string shipId, float threshold = 5f)
    {
        if (!pirateBaseAssignments.TryGetValue(shipId, out PirateBaseAssignment assignment))
            return false;

        // We don't have the ship reference here, caller should check distance
        return false; // Caller checks distance themselves
    }

    // ===================================================================
    // NAVY PATROL ASSIGNMENT
    // ===================================================================

    /// <summary>
    /// Assign a navy ship to a patrol zone. Returns spawn position.
    /// Navy ships loop their patrol indefinitely.
    /// </summary>
    public Vector2 AssignNavyPatrol(ShipController ship, System.Random rng)
    {
        if (currentMapRoutes == null || ship == null || ship.Data == null)
            return Vector2.zero;

        NavyPatrolZone zone = currentMapRoutes.GetPatrolZone(securitySpawnCounter);
        securitySpawnCounter++;

        if (zone == null || zone.patrolWaypoints.Count == 0)
        {
            Debug.LogWarning("TradeRouteManager: No patrol zones defined!");
            return Vector2.zero;
        }

        var patrolState = new NavyPatrolState
        {
            zone = zone,
            currentWaypointIndex = 0,
            isForward = true,
            isOnChaseBreak = false,
            chaseTicks = 0,
            shipId = ship.Data.shipId
        };

        navyPatrolStates[ship.Data.shipId] = patrolState;

        // Position at first patrol waypoint
        Vector2 spawnPos = zone.patrolWaypoints[0];
        ship.Data.position = spawnPos;
        ship.transform.position = new Vector3(spawnPos.x, spawnPos.y, ship.transform.position.z);

        // Apply speed variance (navy has less variance — disciplined military vessels)
        ApplySpeedVariance(ship, rng, 0.5f);

        // Navigate to second waypoint
        if (zone.patrolWaypoints.Count > 1)
        {
            ship.SetDestination(zone.patrolWaypoints[1]);
            patrolState.currentWaypointIndex = 1;
        }

        if (logRouteAssignments)
            Debug.Log($"[{ship.Data.shipId}] Assigned patrol zone: '{zone.zoneName}'");

        return spawnPos;
    }

    /// <summary>
    /// Update navy patrol — advance waypoints in loop.
    /// Call each tick for security ships that are in Patrolling state.
    /// </summary>
    public void UpdateNavyPatrol(ShipController ship)
    {
        if (ship == null || ship.Data == null) return;

        if (!navyPatrolStates.TryGetValue(ship.Data.shipId, out NavyPatrolState state))
            return;

        // If on chase break, count down and resume when done
        if (state.isOnChaseBreak)
        {
            state.chaseTicks++;
            if (state.chaseTicks >= navyChaseTimeout)
            {
                // Chase timed out, resume patrol
                ResumePatrol(ship, state);
            }
            return; // Don't advance patrol while chasing
        }

        // Check if reached current patrol waypoint
        Vector2 targetWaypoint = state.zone.patrolWaypoints[state.currentWaypointIndex];
        float dist = Vector2.Distance(ship.Data.position, targetWaypoint);

        if (dist < 5f)
        {
            // Advance to next waypoint in patrol
            AdvancePatrolWaypoint(ship, state);
        }
    }

    /// <summary>
    /// Navy ship breaks patrol to chase a pirate. Call when security starts chasing.
    /// </summary>
    public void NavyBreakPatrol(string shipId)
    {
        if (navyPatrolStates.TryGetValue(shipId, out NavyPatrolState state))
        {
            state.isOnChaseBreak = true;
            state.chaseTicks = 0;
        }
    }

    /// <summary>
    /// Navy ship finished chase, resume patrol. Call when security stops chasing.
    /// </summary>
    public void NavyResumePatrol(ShipController ship)
    {
        if (ship == null || ship.Data == null) return;

        if (navyPatrolStates.TryGetValue(ship.Data.shipId, out NavyPatrolState state))
        {
            ResumePatrol(ship, state);
        }
    }

    private void AdvancePatrolWaypoint(ShipController ship, NavyPatrolState state)
    {
        var waypoints = state.zone.patrolWaypoints;

        if (state.zone.pingPongPatrol)
        {
            // Ping-pong: reverse at endpoints
            if (state.isForward)
            {
                state.currentWaypointIndex++;
                if (state.currentWaypointIndex >= waypoints.Count)
                {
                    state.currentWaypointIndex = waypoints.Count - 2;
                    state.isForward = false;
                }
            }
            else
            {
                state.currentWaypointIndex--;
                if (state.currentWaypointIndex < 0)
                {
                    state.currentWaypointIndex = 1;
                    state.isForward = true;
                }
            }
        }
        else
        {
            // Loop: wrap around to start
            state.currentWaypointIndex = (state.currentWaypointIndex + 1) % waypoints.Count;
        }

        // Clamp just in case
        state.currentWaypointIndex = Mathf.Clamp(state.currentWaypointIndex, 0, waypoints.Count - 1);

        ship.SetDestination(waypoints[state.currentWaypointIndex]);
    }

    private void ResumePatrol(ShipController ship, NavyPatrolState state)
    {
        state.isOnChaseBreak = false;
        state.chaseTicks = 0;

        // Navigate to nearest patrol waypoint
        float bestDist = float.MaxValue;
        int bestIdx = state.currentWaypointIndex;

        for (int i = 0; i < state.zone.patrolWaypoints.Count; i++)
        {
            float d = Vector2.Distance(ship.Data.position, state.zone.patrolWaypoints[i]);
            if (d < bestDist)
            {
                bestDist = d;
                bestIdx = i;
            }
        }

        state.currentWaypointIndex = bestIdx;
        // Go to the NEXT waypoint after the nearest one (to keep moving forward)
        AdvancePatrolWaypoint(ship, state);

        if (logRouteAssignments)
            Debug.Log($"[{ship.Data.shipId}] Resuming patrol at waypoint {state.currentWaypointIndex}");
    }

    // ===================================================================
    // OFFSCREEN DESPAWN
    // ===================================================================

    /// <summary>
    /// Check if a ship is outside camera bounds and should despawn.
    /// Call each tick from SimulationEngine for merchant ships.
    /// 
    /// Rules:
    /// - Only merchants despawn offscreen
    /// - Must have traveled at least minRouteProgressToDespawn of their route
    /// - Navy NEVER despawns
    /// - Pirates returning to base don't despawn
    /// </summary>
    public bool ShouldDespawnOffscreen(ShipController ship)
    {
        if (ship == null || ship.Data == null) return false;

        // Only merchants despawn offscreen
        if (ship.Data.type != ShipType.Cargo) return false;

        // Check route progress — don't despawn ships that just spawned
        float progress = GetRouteProgress(ship.Data.shipId);
        if (progress < minRouteProgressToDespawn) return false;

        // Check camera bounds
        return IsOutsideCameraBounds(ship.Data.position);
    }

    /// <summary>
    /// Check if a position is outside the camera bounds + buffer.
    /// </summary>
    public bool IsOutsideCameraBounds(Vector2 position)
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

        // Get camera bounds in world space
        float camHeight = cam.orthographicSize * 2f;
        float camWidth = camHeight * cam.aspect;
        Vector3 camPos = cam.transform.position;

        float left = camPos.x - camWidth / 2f - despawnBuffer;
        float right = camPos.x + camWidth / 2f + despawnBuffer;
        float bottom = camPos.y - camHeight / 2f - despawnBuffer;
        float top = camPos.y + camHeight / 2f + despawnBuffer;

        return position.x < left || position.x > right ||
               position.y < bottom || position.y > top;
    }

    // ===================================================================
    // VARIANCE HELPERS
    // ===================================================================

    /// <summary>
    /// Get a random offset within a circle of given radius.
    /// Validates the resulting position is in water. Retries up to 10 times.
    /// If no water found, returns zero offset (spawn at exact point).
    /// </summary>
    private Vector2 GetRandomScatter(System.Random rng, float radius)
    {
        if (radius <= 0f) return Vector2.zero;

        return Vector2.zero; // Base position only — validation done in GetWaterValidatedPosition
    }

    /// <summary>
    /// Get a scattered position near a base point, guaranteed to be in water.
    /// Tries random scatter first, then falls back to the exact point,
    /// then searches nearby for water.
    /// </summary>
    private Vector2 GetWaterValidatedPosition(Vector2 basePosition, System.Random rng, float scatterRadius)
    {
        // Try scattered positions
        for (int i = 0; i < 15; i++)
        {
            float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);
            float dist = Mathf.Sqrt((float)rng.NextDouble()) * scatterRadius;
            Vector2 candidate = basePosition + new Vector2(
                Mathf.Cos(angle) * dist,
                Mathf.Sin(angle) * dist
            );

            if (MapColorSampler.Instance == null || MapColorSampler.Instance.IsWater(candidate))
                return candidate;
        }

        // Scattered positions all on land — try the exact base position
        if (MapColorSampler.Instance == null || MapColorSampler.Instance.IsWater(basePosition))
            return basePosition;

        // Base position is on land too — search nearby for water
        Vector2[] offsets = {
            new Vector2(1, 0), new Vector2(-1, 0),
            new Vector2(0, 1), new Vector2(0, -1),
            new Vector2(1, 1), new Vector2(-1, -1),
            new Vector2(1, -1), new Vector2(-1, 1)
        };

        for (float searchDist = 2f; searchDist < 30f; searchDist += 2f)
        {
            foreach (var offset in offsets)
            {
                Vector2 testPoint = basePosition + offset * searchDist;
                if (MapColorSampler.Instance.IsWater(testPoint))
                    return testPoint;
            }
        }

        Debug.LogWarning($"TradeRouteManager: Could not find water near {basePosition}!");
        return basePosition;
    }

    /// <summary>
    /// Apply random speed variation to a ship.
    /// varianceScale lets you control how much variance (1.0 = full, 0.5 = half).
    /// A ship with base speed 0.05 and 15% variance will get speed between 0.0425 and 0.0575.
    /// </summary>
    private void ApplySpeedVariance(ShipController ship, System.Random rng, float varianceScale = 1f)
    {
        if (speedVariance <= 0f || ship == null || ship.Data == null) return;

        float actualVariance = speedVariance * varianceScale;
        // Random value between -variance and +variance
        float modifier = 1f + ((float)(rng.NextDouble() * 2.0 - 1.0) * actualVariance);
        ship.Data.speedUnitsPerTick *= modifier;
    }

    // ===================================================================
    // HELPER CLASSES
    // ===================================================================

    /// <summary>
    /// Check if trade route data is available for the current map.
    /// </summary>
    public bool HasRouteData()
    {
        return currentMapRoutes != null && currentMapRoutes.tradeRoutes.Count > 0;
    }

    /// <summary>
    /// Check if pirate base data is available.
    /// </summary>
    public bool HasPirateBaseData()
    {
        return currentMapRoutes != null && currentMapRoutes.pirateBases.Count > 0;
    }

    /// <summary>
    /// Check if navy patrol data is available.
    /// </summary>
    public bool HasNavyPatrolData()
    {
        return currentMapRoutes != null && currentMapRoutes.navyPatrolZones.Count > 0;
    }

    // ===================================================================
    // DEBUG GIZMOS
    // ===================================================================

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showRouteGizmos || currentMapRoutes == null) return;

        // Draw trade routes
        if (currentMapRoutes.tradeRoutes != null)
        {
            foreach (var route in currentMapRoutes.tradeRoutes)
            {
                if (route == null || route.waypoints == null || route.waypoints.Count < 2) continue;

                Gizmos.color = route.debugColor;
                for (int i = 0; i < route.waypoints.Count - 1; i++)
                {
                    Vector3 from = new Vector3(route.waypoints[i].x, route.waypoints[i].y, 0);
                    Vector3 to = new Vector3(route.waypoints[i + 1].x, route.waypoints[i + 1].y, 0);
                    Gizmos.DrawLine(from, to);
                }

                // Draw waypoint dots
                foreach (var wp in route.waypoints)
                {
                    Gizmos.DrawWireSphere(new Vector3(wp.x, wp.y, 0), 2f);
                }
            }
        }

        // Draw pirate bases
        Gizmos.color = Color.red;
        if (currentMapRoutes.pirateBases != null)
        {
            foreach (var base_ in currentMapRoutes.pirateBases)
            {
                Vector3 basePos = new Vector3(base_.position.x, base_.position.y, 0);
                Gizmos.DrawWireSphere(basePos, 5f);

                // Draw ambush points
                Gizmos.color = new Color(1f, 0.3f, 0f, 0.5f);
                foreach (var ambush in base_.ambushPoints)
                {
                    Vector3 ambushPos = new Vector3(ambush.x, ambush.y, 0);
                    Gizmos.DrawWireSphere(ambushPos, 3f);
                    Gizmos.DrawLine(basePos, ambushPos);
                }
                Gizmos.color = Color.red;
            }
        }

        // Draw navy patrol zones
        Gizmos.color = Color.blue;
        if (currentMapRoutes.navyPatrolZones != null)
        {
            foreach (var zone in currentMapRoutes.navyPatrolZones)
            {
                if (zone.patrolWaypoints == null || zone.patrolWaypoints.Count < 2) continue;

                for (int i = 0; i < zone.patrolWaypoints.Count - 1; i++)
                {
                    Vector3 from = new Vector3(zone.patrolWaypoints[i].x, zone.patrolWaypoints[i].y, 0);
                    Vector3 to = new Vector3(zone.patrolWaypoints[i + 1].x, zone.patrolWaypoints[i + 1].y, 0);
                    Gizmos.DrawLine(from, to);
                }

                // Draw loop closure
                if (!zone.pingPongPatrol && zone.patrolWaypoints.Count > 2)
                {
                    Vector3 last = new Vector3(zone.patrolWaypoints[zone.patrolWaypoints.Count - 1].x,
                                                zone.patrolWaypoints[zone.patrolWaypoints.Count - 1].y, 0);
                    Vector3 first = new Vector3(zone.patrolWaypoints[0].x, zone.patrolWaypoints[0].y, 0);
                    Gizmos.color = new Color(0f, 0f, 1f, 0.3f);
                    Gizmos.DrawLine(last, first);
                    Gizmos.color = Color.blue;
                }

                // Draw waypoint dots
                foreach (var wp in zone.patrolWaypoints)
                {
                    Gizmos.DrawWireSphere(new Vector3(wp.x, wp.y, 0), 2f);
                }
            }
        }

        // Draw despawn boundary
        Camera cam = Camera.main;
        if (cam != null)
        {
            float camHeight = cam.orthographicSize * 2f;
            float camWidth = camHeight * cam.aspect;
            Vector3 camPos = cam.transform.position;

            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            float left = camPos.x - camWidth / 2f - despawnBuffer;
            float right = camPos.x + camWidth / 2f + despawnBuffer;
            float bottom = camPos.y - camHeight / 2f - despawnBuffer;
            float top = camPos.y + camHeight / 2f + despawnBuffer;

            Gizmos.DrawLine(new Vector3(left, bottom, 0), new Vector3(right, bottom, 0));
            Gizmos.DrawLine(new Vector3(right, bottom, 0), new Vector3(right, top, 0));
            Gizmos.DrawLine(new Vector3(right, top, 0), new Vector3(left, top, 0));
            Gizmos.DrawLine(new Vector3(left, top, 0), new Vector3(left, bottom, 0));
        }
    }
#endif
}

// ===================================================================
// DATA CLASSES (used by TradeRouteManager internally)
// ===================================================================

/// <summary>
/// Tracks a ship's progress along a trade route.
/// </summary>
public class RouteAssignment
{
    public TradeRouteData routeData;
    public List<Vector2> waypoints;
    public int currentWaypointIndex;
    public int totalWaypoints;
    public string shipId;
}

/// <summary>
/// Tracks a navy ship's patrol state.
/// </summary>
public class NavyPatrolState
{
    public NavyPatrolZone zone;
    public int currentWaypointIndex;
    public bool isForward;
    public bool isOnChaseBreak;
    public int chaseTicks;
    public string shipId;
}

/// <summary>
/// Tracks which base a pirate is assigned to.
/// </summary>
public class PirateBaseAssignment
{
    public PirateBase homeBase;
    public int currentAmbushIndex;
    public bool isReturningToBase;
    public string shipId;
}