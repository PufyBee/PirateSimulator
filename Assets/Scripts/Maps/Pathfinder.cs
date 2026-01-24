using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A* Pathfinder that works with MapColorSampler.
/// Finds paths through water, avoiding land.
/// 
/// UPDATED: Now accounts for ship size when building grid,
/// so paths automatically stay away from coastlines.
/// 
/// SETUP:
/// 1. Create empty GameObject called "Pathfinder"
/// 2. Add this script to it
/// 3. That's it! Ships will automatically use it.
/// </summary>
public class Pathfinder : MonoBehaviour
{
    public static Pathfinder Instance { get; private set; }

    [Header("Grid Settings")]
    [Tooltip("How many cells to divide the map into. Higher = more accurate but slower. 100-150 is good.")]
    public int gridResolution = 120;

    [Header("Ship Safety Buffer")]
    [Tooltip("How far from land should paths stay? Increase if ships get stuck near coastlines.")]
    public float shipSafetyBuffer = 0.5f;

    [Header("Path Smoothing")]
    [Tooltip("Remove unnecessary waypoints for smoother paths")]
    public bool smoothPath = true;

    [Header("Performance")]
    [Tooltip("Max nodes to search before giving up (prevents freezing)")]
    public int maxSearchNodes = 8000;

    [Header("Debug")]
    public bool showDebugGrid = false;
    public bool logPathfinding = false;

    // Internal grid
    private bool[,] walkable;
    private int gridWidth;
    private int gridHeight;
    private float cellWidth;
    private float cellHeight;
    private Bounds mapBounds;
    private bool isInitialized = false;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // Delay initialization to ensure MapColorSampler is ready
        Invoke(nameof(BuildGrid), 0.1f);
    }

    /// <summary>
    /// Build the navigation grid from MapColorSampler.
    /// Call this if you change maps.
    /// </summary>
    public void BuildGrid()
    {
        if (MapColorSampler.Instance == null)
        {
            Debug.LogError("Pathfinder: MapColorSampler.Instance is null! Make sure it exists in the scene.");
            return;
        }

        if (MapColorSampler.Instance.mapRenderer == null)
        {
            Debug.LogError("Pathfinder: MapColorSampler has no mapRenderer assigned!");
            return;
        }

        mapBounds = MapColorSampler.Instance.mapRenderer.bounds;

        // Calculate grid dimensions based on map aspect ratio
        float aspect = mapBounds.size.x / mapBounds.size.y;

        if (aspect >= 1f)
        {
            gridWidth = gridResolution;
            gridHeight = Mathf.Max(1, Mathf.RoundToInt(gridResolution / aspect));
        }
        else
        {
            gridHeight = gridResolution;
            gridWidth = Mathf.Max(1, Mathf.RoundToInt(gridResolution * aspect));
        }

        cellWidth = mapBounds.size.x / gridWidth;
        cellHeight = mapBounds.size.y / gridHeight;

        // Build walkability grid
        walkable = new bool[gridWidth, gridHeight];

        // Temporarily disable MapColorSampler's buffer - we'll do our own
        bool originalBuffer = MapColorSampler.Instance.useCoastBuffer;
        MapColorSampler.Instance.useCoastBuffer = false;

        int waterCount = 0;
        int landCount = 0;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                Vector2 worldPos = GridToWorld(x, y);
                
                // Check if this cell is safe for a ship (with buffer)
                bool isSafe = IsSafeForShip(worldPos);
                walkable[x, y] = isSafe;

                if (isSafe) waterCount++;
                else landCount++;
            }
        }

        // Restore original settings
        MapColorSampler.Instance.useCoastBuffer = originalBuffer;

        isInitialized = true;
        Debug.Log($"Pathfinder: Grid built - {gridWidth}x{gridHeight} ({waterCount} water, {landCount} land cells) with {shipSafetyBuffer} buffer");
    }

    /// <summary>
    /// Check if a position is safe for a ship (water + buffer from land).
    /// </summary>
    private bool IsSafeForShip(Vector2 worldPos)
    {
        // Center must be water
        if (!MapColorSampler.Instance.IsWater(worldPos))
            return false;

        // If no buffer, just check center
        if (shipSafetyBuffer <= 0.01f)
            return true;

        // Check in 8 directions around the point
        float buffer = shipSafetyBuffer;
        
        // Cardinal directions
        if (!MapColorSampler.Instance.IsWater(worldPos + new Vector2(buffer, 0))) return false;
        if (!MapColorSampler.Instance.IsWater(worldPos + new Vector2(-buffer, 0))) return false;
        if (!MapColorSampler.Instance.IsWater(worldPos + new Vector2(0, buffer))) return false;
        if (!MapColorSampler.Instance.IsWater(worldPos + new Vector2(0, -buffer))) return false;

        // Diagonal directions (at ~70% distance for circle approximation)
        float diagBuffer = buffer * 0.7f;
        if (!MapColorSampler.Instance.IsWater(worldPos + new Vector2(diagBuffer, diagBuffer))) return false;
        if (!MapColorSampler.Instance.IsWater(worldPos + new Vector2(-diagBuffer, diagBuffer))) return false;
        if (!MapColorSampler.Instance.IsWater(worldPos + new Vector2(diagBuffer, -diagBuffer))) return false;
        if (!MapColorSampler.Instance.IsWater(worldPos + new Vector2(-diagBuffer, -diagBuffer))) return false;

        return true;
    }

    /// <summary>
    /// Find a path from start to end in world coordinates.
    /// Returns list of waypoints, or null if no path exists.
    /// </summary>
    public List<Vector2> FindPath(Vector2 startWorld, Vector2 endWorld)
    {
        if (!isInitialized)
        {
            Debug.LogWarning("Pathfinder: Not initialized yet!");
            BuildGrid();
            if (!isInitialized) return null;
        }

        Vector2Int startCell = WorldToGrid(startWorld);
        Vector2Int endCell = WorldToGrid(endWorld);

        if (logPathfinding)
        {
            Debug.Log($"Pathfinder: Finding path from {startWorld} (cell {startCell}) to {endWorld} (cell {endCell})");
        }

        // Validate start position
        if (!IsValidCell(startCell) || !IsWalkable(startCell))
        {
            if (logPathfinding) Debug.Log($"Pathfinder: Start position invalid, finding nearest walkable...");
            startCell = FindNearestWalkable(startCell);
            if (startCell.x < 0)
            {
                Debug.LogWarning("Pathfinder: Could not find walkable start position!");
                return null;
            }
        }

        // Validate end position
        if (!IsValidCell(endCell) || !IsWalkable(endCell))
        {
            if (logPathfinding) Debug.Log($"Pathfinder: End position invalid, finding nearest walkable...");
            endCell = FindNearestWalkable(endCell);
            if (endCell.x < 0)
            {
                Debug.LogWarning("Pathfinder: Could not find walkable end position!");
                return null;
            }
        }

        // Run A* search
        List<Vector2Int> cellPath = AStarSearch(startCell, endCell);

        if (cellPath == null || cellPath.Count == 0)
        {
            if (logPathfinding) Debug.Log("Pathfinder: No path found!");
            return null;
        }

        // Convert to world coordinates
        List<Vector2> worldPath = new List<Vector2>(cellPath.Count);
        foreach (var cell in cellPath)
        {
            worldPath.Add(GridToWorld(cell.x, cell.y));
        }

        // Use exact start and end positions
        worldPath[0] = startWorld;
        worldPath[worldPath.Count - 1] = endWorld;

        // Smooth the path
        if (smoothPath && worldPath.Count > 2)
        {
            worldPath = SmoothPath(worldPath);
        }

        if (logPathfinding)
        {
            Debug.Log($"Pathfinder: Found path with {worldPath.Count} waypoints");
        }

        return worldPath;
    }

    /// <summary>
    /// A* search algorithm.
    /// </summary>
    private List<Vector2Int> AStarSearch(Vector2Int start, Vector2Int end)
    {
        var openSet = new PriorityQueue();
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, float>();
        var closedSet = new HashSet<Vector2Int>();

        gScore[start] = 0;
        openSet.Enqueue(start, Heuristic(start, end));

        int nodesSearched = 0;

        while (openSet.Count > 0 && nodesSearched < maxSearchNodes)
        {
            Vector2Int current = openSet.Dequeue();
            nodesSearched++;

            if (current == end)
            {
                return ReconstructPath(cameFrom, end);
            }

            closedSet.Add(current);

            // Check all 8 neighbors
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;

                    Vector2Int neighbor = new Vector2Int(current.x + dx, current.y + dy);

                    if (!IsValidCell(neighbor) || !IsWalkable(neighbor) || closedSet.Contains(neighbor))
                        continue;

                    // Prevent diagonal movement through walls
                    if (dx != 0 && dy != 0)
                    {
                        if (!IsWalkable(new Vector2Int(current.x + dx, current.y)) ||
                            !IsWalkable(new Vector2Int(current.x, current.y + dy)))
                            continue;
                    }

                    float moveCost = (dx != 0 && dy != 0) ? 1.414f : 1f;
                    float tentativeG = gScore[current] + moveCost;

                    if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeG;
                        float f = tentativeG + Heuristic(neighbor, end);
                        openSet.Enqueue(neighbor, f);
                    }
                }
            }
        }

        if (nodesSearched >= maxSearchNodes)
        {
            Debug.LogWarning($"Pathfinder: Search limit reached ({maxSearchNodes} nodes)");
        }

        return null;
    }

    private float Heuristic(Vector2Int a, Vector2Int b)
    {
        // Octile distance (good for 8-directional movement)
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return Mathf.Max(dx, dy) + 0.414f * Mathf.Min(dx, dy);
    }

    private List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        var path = new List<Vector2Int> { current };

        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(current);
        }

        path.Reverse();
        return path;
    }

    private Vector2Int FindNearestWalkable(Vector2Int start)
    {
        // BFS to find nearest walkable cell
        var queue = new Queue<Vector2Int>();
        var visited = new HashSet<Vector2Int>();

        queue.Enqueue(start);
        visited.Add(start);

        int maxSearch = 500;
        int searched = 0;

        while (queue.Count > 0 && searched < maxSearch)
        {
            Vector2Int current = queue.Dequeue();
            searched++;

            if (IsValidCell(current) && IsWalkable(current))
            {
                return current;
            }

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;

                    Vector2Int neighbor = new Vector2Int(current.x + dx, current.y + dy);

                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        return new Vector2Int(-1, -1);
    }

    private List<Vector2> SmoothPath(List<Vector2> path)
    {
        if (path.Count <= 2) return path;

        var smoothed = new List<Vector2> { path[0] };
        int current = 0;

        while (current < path.Count - 1)
        {
            int farthest = current + 1;

            // Find farthest point with clear line of sight
            for (int i = current + 2; i < path.Count; i++)
            {
                if (HasLineOfSight(path[current], path[i]))
                {
                    farthest = i;
                }
            }

            smoothed.Add(path[farthest]);
            current = farthest;
        }

        return smoothed;
    }

    private bool HasLineOfSight(Vector2 from, Vector2 to)
    {
        float dist = Vector2.Distance(from, to);
        
        // More samples for longer distances - be very thorough
        int samples = Mathf.Max(10, Mathf.CeilToInt(dist / 0.1f));

        for (int i = 1; i < samples; i++)
        {
            float t = i / (float)samples;
            Vector2 point = Vector2.Lerp(from, to, t);

            // Check the grid directly - is this cell walkable?
            Vector2Int cell = WorldToGrid(point);
            if (!IsWalkable(cell))
            {
                return false;
            }
            
            // Also check with safety buffer
            if (!IsSafeForShip(point))
            {
                return false;
            }
        }

        return true;
    }

    // Grid helper methods
    private Vector2Int WorldToGrid(Vector2 world)
    {
        float nx = (world.x - mapBounds.min.x) / mapBounds.size.x;
        float ny = (world.y - mapBounds.min.y) / mapBounds.size.y;

        int gx = Mathf.Clamp(Mathf.FloorToInt(nx * gridWidth), 0, gridWidth - 1);
        int gy = Mathf.Clamp(Mathf.FloorToInt(ny * gridHeight), 0, gridHeight - 1);

        return new Vector2Int(gx, gy);
    }

    private Vector2 GridToWorld(int x, int y)
    {
        float wx = mapBounds.min.x + (x + 0.5f) * cellWidth;
        float wy = mapBounds.min.y + (y + 0.5f) * cellHeight;
        return new Vector2(wx, wy);
    }

    private bool IsValidCell(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < gridWidth && cell.y >= 0 && cell.y < gridHeight;
    }

    private bool IsWalkable(Vector2Int cell)
    {
        if (!IsValidCell(cell)) return false;
        return walkable[cell.x, cell.y];
    }

    // Simple priority queue for A*
    private class PriorityQueue
    {
        private List<(Vector2Int pos, float priority)> items = new List<(Vector2Int, float)>();

        public int Count => items.Count;

        public void Enqueue(Vector2Int pos, float priority)
        {
            items.Add((pos, priority));
        }

        public Vector2Int Dequeue()
        {
            int bestIndex = 0;
            float bestPriority = items[0].priority;

            for (int i = 1; i < items.Count; i++)
            {
                if (items[i].priority < bestPriority)
                {
                    bestPriority = items[i].priority;
                    bestIndex = i;
                }
            }

            Vector2Int result = items[bestIndex].pos;
            items.RemoveAt(bestIndex);
            return result;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebugGrid || walkable == null) return;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (!walkable[x, y])
                {
                    Gizmos.color = new Color(1, 0, 0, 0.3f);
                    Vector2 pos = GridToWorld(x, y);
                    Gizmos.DrawCube(new Vector3(pos.x, pos.y, 0), new Vector3(cellWidth * 0.9f, cellHeight * 0.9f, 0.1f));
                }
            }
        }
    }
#endif
}