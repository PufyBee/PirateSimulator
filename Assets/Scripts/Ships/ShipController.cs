using UnityEngine;

public class ShipController : MonoBehaviour
{
    public ShipData Data { get; private set; }

    private const float TargetEpsilon = 0.05f;

    [Header("Visual Smoothing (does not change simulation)")]
    public bool smoothVisuals = true;
    public float visualLerpSpeed = 12f;

    private Vector3 visualTargetPos;

    public void Initialize(ShipData data)
    {
        Data = data;
        visualTargetPos = new Vector3(Data.position.x, Data.position.y, transform.position.z);
        transform.position = visualTargetPos;
    }

    private void Update()
    {
        if (!smoothVisuals) return;
        transform.position = Vector3.Lerp(transform.position, visualTargetPos, Time.deltaTime * visualLerpSpeed);
    }

    // Called ONLY by SimulationEngine once per tick
    public void OnTick()
    {
        if (Data == null) return;
        if (Data.state != ShipState.Moving) return;

        if (Data.route == null || !Data.route.HasWaypoint)
        {
            Data.state = ShipState.Idle;
            return;
        }

        Vector2 target = Data.route.Current;
        Vector2 toTarget = target - Data.position;
        float dist = toTarget.magnitude;

        // If reached waypoint, advance
        if (dist <= TargetEpsilon)
        {
            Data.route.Advance();

            if (Data.route == null || !Data.route.HasWaypoint)
            {
                Data.state = ShipState.Exited;
                return;
            }

            target = Data.route.Current;
            toTarget = target - Data.position;
            dist = toTarget.magnitude;

            if (dist <= TargetEpsilon) return;
        }

        Vector2 dir = toTarget.normalized;
        Vector2 step = dir * Data.speedUnitsPerTick;

        // ---- MAP BLOCKING (pretty-map sampling) ----
        if (MapColorSampler.Instance != null)
        {
            Vector2 proposed = Data.position + step;

            // If next step is land, "slide" along coast (try X then Y)
            if (!MapColorSampler.Instance.IsWater(proposed))
            {
                Vector2 tryX = new Vector2(Data.position.x + step.x, Data.position.y);
                Vector2 tryY = new Vector2(Data.position.x, Data.position.y + step.y);

                if (MapColorSampler.Instance.IsWater(tryX))
                {
                    Data.position = tryX;
                }
                else if (MapColorSampler.Instance.IsWater(tryY))
                {
                    Data.position = tryY;
                }
                else
                {
                    // Completely blocked
                    Data.state = ShipState.Idle;
                    return;
                }
            }
            else
            {
                Data.position = proposed;
            }
        }
        else
        {
            // No map sampler in scene yet: move freely
            Data.position += step;
        }

        Data.velocityDir = dir;

        visualTargetPos = new Vector3(Data.position.x, Data.position.y, transform.position.z);

        FaceDirection(dir);

        if (!smoothVisuals)
            transform.position = visualTargetPos;
    }

    private void FaceDirection(Vector2 dir)
    {
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
    }

    public void SetState(ShipState state)
    {
        if (Data != null) Data.state = state;
    }
}
