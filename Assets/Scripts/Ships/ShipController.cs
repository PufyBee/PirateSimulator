using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Ship controller with A* pathfinding.
/// 
/// HOW IT WORKS:
/// 1. Ship is given a destination
/// 2. Pathfinder calculates a route around obstacles
/// 3. Ship follows the waypoints
/// 4. If stuck, ship requests a new path
/// 
/// REPLACES: Your old ShipController.cs
/// WORKS WITH: Your existing ShipData.cs and ShipRoute.cs (no changes needed)
/// </summary>
public class ShipController : MonoBehaviour
{
    public ShipData Data { get; private set; }

    [Header("Movement")]
    [Tooltip("How close to a waypoint before moving to the next one")]
    public float waypointReachDistance = 0.15f;

    [Tooltip("Buffer distance from coastlines")]
    public float coastBuffer = 0.1f;

    [Header("Visual Smoothing")]
    [Tooltip("Smoothly interpolate position (visual only, doesn't affect simulation)")]
    public bool smoothVisuals = true;

    [Tooltip("How fast to smooth visual position")]
    public float visualSmoothSpeed = 12f;

    [Header("Stuck Detection")]
    [Tooltip("Minimum movement per tick to count as 'moving'")]
    public float minMoveDistance = 0.001f;

    [Tooltip("Ticks without movement before requesting new path")]
    public int stuckTicksBeforeRepath = 20;

    [Header("Debug")]
    public bool showPathGizmos = false;
    public bool logMovement = false;

    // Internal state
    private Vector3 visualPosition;
    private int stuckTicks = 0;
    private int repathAttempts = 0;
    private const int MaxRepathAttempts = 3;
    private Vector2 lastPosition;

    /// <summary>
    /// Initialize the ship with its data.
    /// Call this right after instantiating the ship.
    /// </summary>
    public void Initialize(ShipData data)
    {
        Data = data;
        visualPosition = new Vector3(data.position.x, data.position.y, transform.position.z);
        transform.position = visualPosition;
        lastPosition = data.position;
        stuckTicks = 0;
        repathAttempts = 0;

        if (logMovement)
        {
            Debug.Log($"Ship {data.shipId} initialized at {data.position}");
        }
    }

    /// <summary>
    /// Set a destination and calculate path to it.
    /// </summary>
    public void SetDestination(Vector2 destination)
    {
        if (Data == null)
        {
            Debug.LogError("ShipController: Cannot set destination - Data is null!");
            return;
        }

        if (Pathfinder.Instance == null)
        {
            Debug.LogError("ShipController: Cannot set destination - No Pathfinder in scene!");
            // Fallback: direct path
            SetDirectPath(destination);
            return;
        }

        // Calculate path using A*
        List<Vector2> path = Pathfinder.Instance.FindPath(Data.position, destination);

        if (path != null && path.Count > 0)
        {
            // Store in ShipRoute
            Data.route = new ShipRoute();
            Data.route.waypoints = path;
            Data.route.currentIndex = 0;
            Data.state = ShipState.Moving;
            repathAttempts = 0;

            if (logMovement)
            {
                Debug.Log($"Ship {Data.shipId}: Path calculated with {path.Count} waypoints");
            }
        }
        else
        {
            Debug.LogWarning($"Ship {Data.shipId}: No path found to {destination}");
            repathAttempts++;

            if (repathAttempts >= MaxRepathAttempts)
            {
                Debug.LogWarning($"Ship {Data.shipId}: Giving up on pathfinding");
                Data.state = ShipState.Idle;
            }
            else
            {
                // Try direct path as fallback
                SetDirectPath(destination);
            }
        }
    }

    /// <summary>
    /// Fallback: Set a direct path (no pathfinding).
    /// </summary>
    private void SetDirectPath(Vector2 destination)
    {
        Data.route = new ShipRoute();
        Data.route.waypoints = new List<Vector2> { Data.position, destination };
        Data.route.currentIndex = 0;
        Data.state = ShipState.Moving;
    }

    /// <summary>
    /// Visual update (runs every frame).
    /// </summary>
    private void Update()
    {
        if (!smoothVisuals || Data == null) return;

        // Smoothly move visual representation toward actual position
        Vector3 targetPos = new Vector3(Data.position.x, Data.position.y, transform.position.z);
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * visualSmoothSpeed);
    }

    /// <summary>
    /// Simulation tick (called by your SimulationEngine).
    /// This is where the actual movement happens.
    /// </summary>
    public void OnTick()
    {
        if (Data == null) return;

        // Only process moving ships
        if (Data.state != ShipState.Moving) return;

        // Check if we have a route
        if (Data.route == null || !Data.route.HasWaypoint)
        {
            Data.state = ShipState.Idle;
            return;
        }

        // Get current waypoint
        Vector2 target = Data.route.Current;
        Vector2 toTarget = target - Data.position;
        float distanceToTarget = toTarget.magnitude;

        // Check if we've reached the waypoint
        if (distanceToTarget <= waypointReachDistance)
        {
            Data.route.Advance();

            if (!Data.route.HasWaypoint)
            {
                // Reached final destination!
                Data.state = ShipState.Exited;
                if (logMovement) Debug.Log($"Ship {Data.shipId}: Reached destination!");
                return;
            }

            // Move to next waypoint
            target = Data.route.Current;
            toTarget = target - Data.position;
            distanceToTarget = toTarget.magnitude;
        }

        // Calculate movement direction
        Vector2 moveDir = toTarget.normalized;

        // Calculate how far we can move this tick
        float moveDistance = Mathf.Min(Data.speedUnitsPerTick, distanceToTarget);

        // Calculate new position
        Vector2 newPosition = Data.position + moveDir * moveDistance;

        // Check if the move is valid (not into land)
        if (MapColorSampler.Instance != null)
        {
            if (!IsMoveValid(Data.position, newPosition))
            {
                // Can't move to desired position - try to find a valid position
                newPosition = FindValidMovePosition(Data.position, moveDir, moveDistance);
            }
        }

        // Track movement for stuck detection
        float actualMove = Vector2.Distance(lastPosition, newPosition);

        if (actualMove < minMoveDistance)
        {
            stuckTicks++;

            if (stuckTicks >= stuckTicksBeforeRepath)
            {
                HandleStuck();
            }
        }
        else
        {
            stuckTicks = 0;
        }

        // Apply movement
        lastPosition = Data.position;
        Data.position = newPosition;
        Data.velocityDir = moveDir;

        // Update visual position (instant if smoothing is off)
        if (!smoothVisuals)
        {
            transform.position = new Vector3(Data.position.x, Data.position.y, transform.position.z);
        }

        // Rotate to face movement direction
        //FaceDirection(moveDir);
    }

    /// <summary>
    /// Check if a move from start to end is valid (doesn't cross land).
    /// </summary>
    private bool IsMoveValid(Vector2 start, Vector2 end)
    {
        // Sample points along the path
        int samples = Mathf.Max(3, Mathf.CeilToInt(Vector2.Distance(start, end) / 0.05f));

        for (int i = 1; i <= samples; i++)
        {
            float t = i / (float)samples;
            Vector2 point = Vector2.Lerp(start, end, t);

            if (!MapColorSampler.Instance.IsWater(point))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Find a valid position to move to (binary search for farthest safe point).
    /// </summary>
    private Vector2 FindValidMovePosition(Vector2 start, Vector2 direction, float maxDistance)
    {
        float lo = 0f;
        float hi = maxDistance;

        // Binary search for farthest valid point
        for (int i = 0; i < 8; i++)
        {
            float mid = (lo + hi) / 2f;
            Vector2 testPos = start + direction * mid;

            if (IsMoveValid(start, testPos))
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }

        return start + direction * lo;
    }

    /// <summary>
    /// Handle being stuck - request a new path.
    /// </summary>
    private void HandleStuck()
    {
        if (logMovement)
        {
            Debug.Log($"Ship {Data.shipId}: Stuck! Requesting new path...");
        }

        stuckTicks = 0;
        repathAttempts++;

        if (repathAttempts >= MaxRepathAttempts)
        {
            Debug.LogWarning($"Ship {Data.shipId}: Stuck and out of repath attempts. Setting to Idle.");
            Data.state = ShipState.Idle;
            return;
        }

        // Get final destination and request new path
        if (Data.route != null && Data.route.waypoints != null && Data.route.waypoints.Count > 0)
        {
            Vector2 finalDestination = Data.route.waypoints[Data.route.waypoints.Count - 1];
            SetDestination(finalDestination);
        }
        else
        {
            Data.state = ShipState.Idle;
        }
    }

    /// <summary>
    /// Rotate the ship to face a direction.
    /// </summary>
    private void FaceDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        // Subtract 90 because ship sprites typically point "up"
        transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
    }

    /// <summary>
    /// Set the ship's state directly.
    /// </summary>
    public void SetState(ShipState state)
    {
        if (Data != null)
        {
            Data.state = state;
        }
    }

    /// <summary>
    /// Get the remaining waypoints in the current route.
    /// </summary>
    public List<Vector2> GetRemainingPath()
    {
        if (Data?.route?.waypoints == null) return null;

        var remaining = new List<Vector2>();
        for (int i = Data.route.currentIndex; i < Data.route.waypoints.Count; i++)
        {
            remaining.Add(Data.route.waypoints[i]);
        }
        return remaining;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showPathGizmos || Data?.route?.waypoints == null) return;

        // Draw the full path
        Gizmos.color = Color.cyan;
        for (int i = 0; i < Data.route.waypoints.Count - 1; i++)
        {
            Vector3 from = new Vector3(Data.route.waypoints[i].x, Data.route.waypoints[i].y, 0);
            Vector3 to = new Vector3(Data.route.waypoints[i + 1].x, Data.route.waypoints[i + 1].y, 0);
            Gizmos.DrawLine(from, to);
        }

        // Draw remaining path in different color
        Gizmos.color = Color.yellow;
        for (int i = Data.route.currentIndex; i < Data.route.waypoints.Count - 1; i++)
        {
            Vector3 from = new Vector3(Data.route.waypoints[i].x, Data.route.waypoints[i].y, 0);
            Vector3 to = new Vector3(Data.route.waypoints[i + 1].x, Data.route.waypoints[i + 1].y, 0);
            Gizmos.DrawLine(from, to);
        }

        // Draw current waypoint
        if (Data.route.HasWaypoint)
        {
            Gizmos.color = Color.green;
            Vector2 wp = Data.route.Current;
            Gizmos.DrawWireSphere(new Vector3(wp.x, wp.y, 0), 0.1f);
        }

        // Draw final destination
        if (Data.route.waypoints.Count > 0)
        {
            Gizmos.color = Color.red;
            Vector2 dest = Data.route.waypoints[Data.route.waypoints.Count - 1];
            Gizmos.DrawWireSphere(new Vector3(dest.x, dest.y, 0), 0.15f);
        }
    }
#endif
}