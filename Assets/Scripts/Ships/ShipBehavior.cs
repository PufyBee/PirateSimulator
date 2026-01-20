using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// DEBUG VERSION - Has lots of logging to diagnose issues
/// </summary>
public class ShipBehavior : MonoBehaviour
{
    [Header("Detection")]
    public float detectionRange = 8f;
    public int scanInterval = 2;

    [Header("Capture/Combat")]
    public float captureRange = 0.5f;
    public int captureTime = 10;

    [Header("Flee Behavior")]
    public float fleeDistance = 4f;

    [Header("Debug")]
    public bool showDetectionGizmos = true;
    public bool enableDebugLogs = true;

    private ShipController controller;
    private ShipController currentTarget;
    private int ticksSinceLastScan = 0;
    private int captureProgress = 0;
    private Vector2 originalDestination;
    private bool hasOriginalDestination = false;
    private BehaviorState behaviorState = BehaviorState.Normal;

    private enum BehaviorState
    {
        Normal,
        Chasing,
        Fleeing,
        Capturing
    }

    private void Awake()
    {
        controller = GetComponent<ShipController>();
        if (enableDebugLogs)
            Debug.Log($"ShipBehavior Awake on {gameObject.name}");
    }

    private void Start()
    {
        if (enableDebugLogs)
            Debug.Log($"ShipBehavior Start on {gameObject.name}, controller={(controller != null ? "OK" : "NULL")}");
    }

    public void OnBehaviorTick(List<ShipController> allShips)
    {
        if (controller == null)
        {
            if (enableDebugLogs) Debug.LogWarning($"ShipBehavior: controller is null on {gameObject.name}");
            return;
        }

        if (controller.Data == null)
        {
            if (enableDebugLogs) Debug.LogWarning($"ShipBehavior: controller.Data is null on {gameObject.name}");
            return;
        }

        if (controller.Data.state != ShipState.Moving && controller.Data.state != ShipState.Idle)
        {
            return;
        }

        // Log every scan to show behavior is running
        ticksSinceLastScan++;
        if (ticksSinceLastScan >= scanInterval)
        {
            if (enableDebugLogs)
                Debug.Log($"[{controller.Data.shipId}] Behavior tick - Type: {controller.Data.type}, State: {behaviorState}, AllShips: {allShips.Count}");
            
            ticksSinceLastScan = 0;
        }

        switch (controller.Data.type)
        {
            case ShipType.Pirate:
                ExecutePirateBehavior(allShips);
                break;
            case ShipType.Security:
                ExecuteSecurityBehavior(allShips);
                break;
            case ShipType.Cargo:
                ExecuteMerchantBehavior(allShips);
                break;
        }
    }

    private void ExecutePirateBehavior(List<ShipController> allShips)
    {
        // Find nearest merchant
        ShipController nearestMerchant = FindNearestShipOfType(allShips, ShipType.Cargo);

        if (nearestMerchant != null)
        {
            float distance = Vector2.Distance(controller.Data.position, nearestMerchant.Data.position);

            if (enableDebugLogs)
                Debug.Log($"[{controller.Data.shipId}] Found merchant {nearestMerchant.Data.shipId} at distance {distance:F2} (detection range: {detectionRange})");

            if (distance <= detectionRange)
            {
                currentTarget = nearestMerchant;

                if (distance <= captureRange)
                {
                    behaviorState = BehaviorState.Capturing;
                    captureProgress++;

                    if (enableDebugLogs)
                        Debug.Log($"[{controller.Data.shipId}] CAPTURING {currentTarget.Data.shipId}... {captureProgress}/{captureTime}");

                    if (captureProgress >= captureTime)
                    {
                        CaptureTarget(currentTarget);
                        captureProgress = 0;
                        currentTarget = null;
                        behaviorState = BehaviorState.Normal;
                    }
                }
                else
                {
                    behaviorState = BehaviorState.Chasing;
                    captureProgress = 0;

                    if (enableDebugLogs)
                        Debug.Log($"[{controller.Data.shipId}] CHASING {currentTarget.Data.shipId}!");

                    ChaseTarget(currentTarget);
                }
                return;
            }
        }
        else
        {
            if (enableDebugLogs && ticksSinceLastScan == 0)
                Debug.Log($"[{controller.Data.shipId}] No merchants found in {allShips.Count} ships");
        }

        currentTarget = null;
        behaviorState = BehaviorState.Normal;
        captureProgress = 0;
    }

    private void ExecuteSecurityBehavior(List<ShipController> allShips)
    {
        ShipController nearestPirate = FindNearestShipOfType(allShips, ShipType.Pirate);

        if (nearestPirate != null)
        {
            float distance = Vector2.Distance(controller.Data.position, nearestPirate.Data.position);

            if (distance <= detectionRange)
            {
                currentTarget = nearestPirate;

                if (distance <= captureRange)
                {
                    DefeatTarget(currentTarget);
                    currentTarget = null;
                    behaviorState = BehaviorState.Normal;
                }
                else
                {
                    behaviorState = BehaviorState.Chasing;
                    if (enableDebugLogs)
                        Debug.Log($"[{controller.Data.shipId}] CHASING pirate {currentTarget.Data.shipId}!");
                    ChaseTarget(currentTarget);
                }
                return;
            }
        }

        currentTarget = null;
        behaviorState = BehaviorState.Normal;
    }

    private void ExecuteMerchantBehavior(List<ShipController> allShips)
    {
        ShipController nearestPirate = FindNearestShipOfType(allShips, ShipType.Pirate);

        if (nearestPirate != null)
        {
            float distance = Vector2.Distance(controller.Data.position, nearestPirate.Data.position);

            if (distance <= detectionRange)
            {
                if (behaviorState != BehaviorState.Fleeing)
                {
                    SaveOriginalDestination();
                    if (enableDebugLogs)
                        Debug.Log($"[{controller.Data.shipId}] FLEEING from pirate {nearestPirate.Data.shipId}!");
                }

                behaviorState = BehaviorState.Fleeing;
                FleeFrom(nearestPirate);
                return;
            }
        }

        if (behaviorState == BehaviorState.Fleeing)
        {
            if (enableDebugLogs)
                Debug.Log($"[{controller.Data.shipId}] Safe now, returning to route");
            ReturnToOriginalDestination();
            behaviorState = BehaviorState.Normal;
        }
    }

    private ShipController FindNearestShipOfType(List<ShipController> allShips, ShipType targetType)
    {
        ShipController nearest = null;
        float nearestDist = float.MaxValue;
        int countOfType = 0;

        foreach (var ship in allShips)
        {
            if (ship == null || ship == controller) continue;
            if (ship.Data == null) continue;
            if (ship.Data.type != targetType) continue;
            if (ship.Data.state == ShipState.Captured ||
                ship.Data.state == ShipState.Sunk ||
                ship.Data.state == ShipState.Exited) continue;

            countOfType++;
            float dist = Vector2.Distance(controller.Data.position, ship.Data.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = ship;
            }
        }

        if (enableDebugLogs && ticksSinceLastScan == 0 && nearest == null)
        {
            Debug.Log($"[{controller.Data.shipId}] Searching for {targetType}: found {countOfType} valid ships");
        }

        return nearest;
    }

    private void ChaseTarget(ShipController target)
    {
        if (target == null || target.Data == null) return;
        controller.SetDestination(target.Data.position);
    }

    private void FleeFrom(ShipController threat)
    {
        if (threat == null || threat.Data == null) return;

        Vector2 awayDir = (controller.Data.position - threat.Data.position).normalized;
        Vector2 fleeTarget = controller.Data.position + awayDir * fleeDistance;
        controller.SetDestination(fleeTarget);
    }

    private void SaveOriginalDestination()
    {
        if (!hasOriginalDestination && controller.Data.route != null &&
            controller.Data.route.waypoints != null &&
            controller.Data.route.waypoints.Count > 0)
        {
            originalDestination = controller.Data.route.waypoints[controller.Data.route.waypoints.Count - 1];
            hasOriginalDestination = true;
        }
    }

    private void ReturnToOriginalDestination()
    {
        if (hasOriginalDestination)
        {
            controller.SetDestination(originalDestination);
        }
    }

    private void CaptureTarget(ShipController target)
    {
        if (target == null || target.Data == null) return;

        target.Data.state = ShipState.Captured;
        Debug.Log($"*** {controller.Data.shipId} CAPTURED {target.Data.shipId}! ***");
        target.SetState(ShipState.Captured);
    }

    private void DefeatTarget(ShipController target)
    {
        if (target == null || target.Data == null) return;

        target.Data.state = ShipState.Sunk;
        Debug.Log($"*** {controller.Data.shipId} DEFEATED {target.Data.shipId}! ***");
        target.SetState(ShipState.Sunk);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDetectionGizmos) return;

        // Always draw detection range (not just when selected)
        Gizmos.color = new Color(1, 1, 0, 0.2f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = new Color(1, 0, 0, 0.3f);
        Gizmos.DrawWireSphere(transform.position, captureRange);

        if (currentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, currentTarget.transform.position);
        }
    }
#endif
}