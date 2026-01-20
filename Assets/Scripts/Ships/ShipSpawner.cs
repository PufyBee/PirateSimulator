using UnityEngine;

public class ShipSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject cargoShipPrefab;

    [Header("Spawn Point (where ships start)")]
    public Vector2 cargoSpawnPoint = new Vector2(-4, 0);

    [Tooltip("If true, adds a small deterministic Y jitter so seed visibly matters.")]
    public bool jitterSpawnY = true;

    [Header("Destination (where ships try to reach)")]
    [Tooltip("The exit point ships will navigate toward")]
    public Vector2 cargoDestination = new Vector2(4, 0);

    [Header("Defaults")]
    public float cargoSpeedUnitsPerTick = 0.05f;

    // Called by SimulationEngine at setup/start
    public ShipController SpawnCargo(System.Random rng, string idSuffix = "1")
    {
        if (cargoShipPrefab == null)
        {
            Debug.LogError("ShipSpawner: cargoShipPrefab is not assigned.");
            return null;
        }

        if (rng == null) rng = new System.Random(12345);

        // Deterministic tiny jitter so seed matters (optional)
        float jitterY = 0f;
        if (jitterSpawnY)
            jitterY = (float)(rng.NextDouble() * 0.6 - 0.3); // [-0.3, 0.3]

        Vector2 spawn = new Vector2(cargoSpawnPoint.x, cargoSpawnPoint.y + jitterY);

        GameObject go = Instantiate(cargoShipPrefab);
        ShipController controller = go.GetComponent<ShipController>();
        if (controller == null) controller = go.AddComponent<ShipController>();

        // Create ship data (NO route yet - pathfinder will calculate it)
        ShipData data = new ShipData
        {
            shipId = $"Cargo-{idSuffix}",
            type = ShipType.Cargo,
            state = ShipState.Moving,
            position = spawn,
            speedUnitsPerTick = cargoSpeedUnitsPerTick,
            velocityDir = Vector2.right,
            route = null  // Will be set by SetDestination
        };

        // Initialize the ship
        controller.Initialize(data);

        // NOW use pathfinding to calculate route to destination
        controller.SetDestination(cargoDestination);

        return controller;
    }
}