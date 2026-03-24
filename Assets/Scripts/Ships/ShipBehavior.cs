using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// SMART SHIP BEHAVIOR - Advanced AI System (PERFORMANCE OPTIMIZED)
/// 
/// OPTIMIZATION CHANGES:
/// - Pathfinding throttle: Ships only call SetDestination() every repathIntervalTicks
///   OR when the target has moved more than repathDistanceThreshold.
///   This is the SINGLE BIGGEST performance win — A* was being called every tick per ship.
/// - Cached component references to avoid GetComponent() every tick
/// 
/// ALL EXISTING BEHAVIOR IS PRESERVED:
/// - Force assessment, pack hunting, distress calls, hotspots
/// - Escort capture system
/// - Visual effects (chase lines, color pulses)
/// 
/// The throttle is invisible to the player — ships still chase smoothly because
/// they follow existing waypoints between re-paths.
/// </summary>
public class ShipBehavior : MonoBehaviour
{
    [Header("=== DETECTION ===")]
    public float detectionRange = 8f;

    [Header("=== CAPTURE/COMBAT ===")]
    public float captureRange = 1.5f;
    public int captureTime = 5;

    [Header("=== FLEE SETTINGS ===")]
    public float fleeDistance = 3f;
    public float fleeSpeedBoost = 1.3f;

    [Header("=== SMART AI SETTINGS ===")]
    [Tooltip("How long pirates remember last seen position (in ticks)")]
    public int memoryDurationTicks = 50;
    
    [Tooltip("Range at which pirates detect security and retreat")]
    public float retreatRange = 6f;
    
    [Tooltip("Range at which distress calls are heard")]
    public float distressCallRange = 12f;
    
    [Tooltip("Range for pack hunting coordination")]
    public float packHuntingRange = 8f;

    [Header("=== FORCE ASSESSMENT ===")]
    [Tooltip("Range to count friendly/enemy ships for force assessment")]
    public float forceAssessmentRange = 10f;
    
    [Tooltip("Pirates need at least this ratio (pirates/security) to attack")]
    public float minimumForceRatio = 0.5f;
    
    [Tooltip("If true, pirates will escort captured merchants instead of destroying them")]
    public bool enableEscortCapture = true;

    [Header("=== AMBUSH BEHAVIOR ===")]
    [Tooltip("Radius around ambush point — pirate activates when a merchant enters this radius")]
    public float ambushTriggerRadius = 15f;
    [Tooltip("If true, pirates are invisible while lurking at ambush points")]
    public bool invisibleWhileLurking = true;

    [Header("=== PATHFINDING THROTTLE ===")]
    [Tooltip("Minimum ticks between SetDestination() calls (A* pathfinding)")]
    public int repathIntervalTicks = 10;
    
    [Tooltip("Re-path immediately if target moves more than this distance")]
    public float repathDistanceThreshold = 3f;
    
    [Tooltip("Minimum ticks between flee re-paths (merchants fleeing)")]
    public int fleeRepathInterval = 8;

    [Header("=== VISUAL EFFECTS ===")]
    public bool enableVisualEffects = true;
    public GameObject explosionPrefab;
    public GameObject captureEffectPrefab;

    [Header("=== DEBUG ===")]
    public bool showGizmos = true;
    public bool enableLogs = false;

    // Components (cached)
    private ShipController controller;
    private SpriteRenderer spriteRenderer;
    private LineRenderer chaseLineRenderer;

    // State
    private ShipController currentTarget;
    private int captureProgress = 0;
    private BehaviorState currentState = BehaviorState.Idle;
    private Vector2 originalDestination;
    private bool hasOriginalDestination = false;
    private Color originalColor;
    private float originalSpeed;

    // === PATHFINDING THROTTLE STATE ===
    private int ticksSinceLastRepath = 0;
    private Vector2 lastRepathTargetPos;
    private int ticksSinceLastFleeRepath = 0;

    // === SMART AI STATE ===
    private Vector2 lastKnownTargetPosition;
    private bool hasLastKnownPosition = false;
    private int ticksSinceLastSeen = 0;
    private bool isRespondingToDistress = false;
    private Vector2 distressPosition;

    // === LURKING/AMBUSH STATE ===
    private bool isLurking = false;
    private Vector2 currentAmbushPoint;
    private bool hasAmbushPoint = false;

    // === STATIC SHARED DATA ===
    private static List<Vector2> captureHotspots = new List<Vector2>();
    private static List<DistressCall> activeDistressCalls = new List<DistressCall>();
    private const int MAX_HOTSPOTS = 20;

    public enum BehaviorState
    {
        Idle,
        Patrolling,
        Chasing,
        Fleeing,
        Capturing,
        Retreating,
        Investigating,
        Responding,
        Escorting,
        Lurking
    }

    private class DistressCall
    {
        public Vector2 position;
        public ShipController merchant;
        public int ticksRemaining;
    }

    // Escort capture system
    private ShipController capturedMerchant;
    private Vector2 escortDestination;

    void Awake()
    {
        controller = GetComponent<ShipController>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;

        SetupChaseLineRenderer();
    }

    void SetupChaseLineRenderer()
    {
        chaseLineRenderer = gameObject.AddComponent<LineRenderer>();
        chaseLineRenderer.startWidth = 0.05f;
        chaseLineRenderer.endWidth = 0.05f;
        chaseLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        chaseLineRenderer.startColor = Color.red;
        chaseLineRenderer.endColor = Color.yellow;
        chaseLineRenderer.positionCount = 2;
        chaseLineRenderer.enabled = false;
    }

    void Start()
    {
        if (controller != null && controller.Data != null)
        {
            originalSpeed = controller.Data.speedUnitsPerTick;
        }
    }

    void Update()
    {
        UpdateChaseLine();
    }

    // ===== PATHFINDING THROTTLE HELPERS =====

    /// <summary>
    /// Only calls SetDestination if enough ticks have passed OR target moved significantly.
    /// Returns true if a repath actually happened.
    /// </summary>
    private bool ThrottledSetDestination(Vector2 destination, bool forceRepath = false)
    {
        ticksSinceLastRepath++;

        bool shouldRepath = forceRepath;

        // Time-based: enough ticks have passed
        if (ticksSinceLastRepath >= repathIntervalTicks)
            shouldRepath = true;

        // Distance-based: target moved significantly since last repath
        float targetMoved = Vector2.Distance(destination, lastRepathTargetPos);
        if (targetMoved >= repathDistanceThreshold)
            shouldRepath = true;

        // Don't repath if we don't need to
        if (!shouldRepath)
            return false;

        // Actually call pathfinding
        controller.SetDestination(destination);
        ticksSinceLastRepath = 0;
        lastRepathTargetPos = destination;
        return true;
    }

    /// <summary>
    /// Throttled flee repath - merchants don't need to recalculate A* every tick while fleeing.
    /// </summary>
    private bool ThrottledFleeDestination(Vector2 destination)
    {
        ticksSinceLastFleeRepath++;

        if (ticksSinceLastFleeRepath >= fleeRepathInterval)
        {
            controller.SetDestination(destination);
            ticksSinceLastFleeRepath = 0;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Force an immediate repath (e.g., when switching targets or states).
    /// </summary>
    private void ForceSetDestination(Vector2 destination)
    {
        controller.SetDestination(destination);
        ticksSinceLastRepath = 0;
        ticksSinceLastFleeRepath = 0;
        lastRepathTargetPos = destination;
    }

    // ===== MAIN BEHAVIOR TICK =====

    public void OnBehaviorTick(List<ShipController> allShips)
    {
        if (controller == null || controller.Data == null) return;

        if (controller.Data.state == ShipState.Captured ||
            controller.Data.state == ShipState.Sunk ||
            controller.Data.state == ShipState.Exited)
        {
            chaseLineRenderer.enabled = false;
            return;
        }

        UpdateDistressCalls();

        switch (controller.Data.type)
        {
            case ShipType.Pirate:
                ExecuteSmartPirateBehavior(allShips);
                break;
            case ShipType.Security:
                ExecuteSmartSecurityBehavior(allShips);
                break;
            case ShipType.Cargo:
                ExecuteSmartMerchantBehavior(allShips);
                break;
        }

        UpdateVisuals();
    }

    // ==================== SMART PIRATE BEHAVIOR ====================

    void ExecuteSmartPirateBehavior(List<ShipController> allShips)
    {
        // PRIORITY 0: If escorting a captured merchant, continue escort
        if (currentState == BehaviorState.Escorting && capturedMerchant != null)
        {
            ExecuteEscortBehavior(allShips);
            return;
        }

        // === LURKING AMBUSH SYSTEM ===
        bool useAmbushSystem = TradeRouteManager.Instance != null && 
                                TradeRouteManager.Instance.HasPirateBaseData();

        if (useAmbushSystem)
        {
            if (!hasAmbushPoint)
            {
                currentAmbushPoint = TradeRouteManager.Instance.GetPirateAmbushPoint(
                    controller.Data.shipId,
                    new System.Random(controller.Data.shipId.GetHashCode())
                );
                if (currentAmbushPoint != Vector2.zero)
                {
                    hasAmbushPoint = true;
                    ForceSetDestination(currentAmbushPoint);
                    currentState = BehaviorState.Patrolling;
                    SetPirateVisibility(false);
                }
            }

            if (isLurking)
            {
                ShipController nearestMerchant = FindNearestMerchantInRadius(allShips, ambushTriggerRadius);
                if (nearestMerchant != null)
                {
                    isLurking = false;
                    SetPirateVisibility(true);
                    controller.Data.state = ShipState.Moving;
                    if (enableLogs)
                        Debug.Log($"[{controller.Data.shipId}] AMBUSH! Attacking {nearestMerchant.Data.shipId}!");
                }
                else
                {
                    controller.Data.state = ShipState.Idle;
                    return;
                }
            }

            if (!isLurking && hasAmbushPoint && 
                (currentState == BehaviorState.Patrolling || currentState == BehaviorState.Idle))
            {
                float distToAmbush = Vector2.Distance(controller.Data.position, currentAmbushPoint);
                if (distToAmbush < 5f)
                {
                    isLurking = true;
                    SetPirateVisibility(false);
                    controller.Data.state = ShipState.Idle;
                    currentState = BehaviorState.Lurking;
                    if (enableLogs)
                        Debug.Log($"[{controller.Data.shipId}] Lurking at ambush point...");
                    return;
                }
            }
        }

        // PRIORITY 1: Check for nearby security - RETREAT if outgunned!
        int nearbyPirates = CountNearbyShipsOfType(allShips, controller.Data.position, forceAssessmentRange, ShipType.Pirate);
        int nearbySecurity = CountNearbyShipsOfType(allShips, controller.Data.position, forceAssessmentRange, ShipType.Security);
        
        bool shouldRetreat = false;
        if (nearbySecurity > 0)
        {
            float forceRatio = (float)nearbyPirates / nearbySecurity;
            shouldRetreat = forceRatio < minimumForceRatio;
            
            if (enableLogs && shouldRetreat)
                Debug.Log($"[{controller.Data.shipId}] OUTGUNNED! Pirates:{nearbyPirates} vs Security:{nearbySecurity} (ratio:{forceRatio:F1})");
        }

        ShipController nearestSecurity = FindNearestShipOfType(allShips, ShipType.Security);
        if (nearestSecurity != null && shouldRetreat)
        {
            float securityDist = Vector2.Distance(controller.Data.position, nearestSecurity.Data.position);
            if (securityDist <= retreatRange * 1.5f)
            {
                ExecuteRetreat(nearestSecurity);
                return;
            }
        }

        // PRIORITY 2: Find a target using threat assessment
        ShipController bestTarget = FindBestMerchantTarget(allShips);

        if (bestTarget != null)
        {
            float distance = Vector2.Distance(controller.Data.position, bestTarget.Data.position);

            if (distance <= detectionRange)
            {
                // FORCE CHECK before engaging
                int securityNearTarget = CountNearbyShipsOfType(allShips, bestTarget.Data.position, forceAssessmentRange, ShipType.Security);
                int piratesNearTarget = CountNearbyShipsOfType(allShips, bestTarget.Data.position, forceAssessmentRange, ShipType.Pirate);
                
                if (securityNearTarget > 0)
                {
                    float engageRatio = (float)piratesNearTarget / securityNearTarget;
                    if (engageRatio < minimumForceRatio)
                    {
                        if (enableLogs)
                            Debug.Log($"[{controller.Data.shipId}] Target too well guarded. Pirates:{piratesNearTarget} vs Security:{securityNearTarget}");
                        
                        currentTarget = null;
                        currentState = BehaviorState.Patrolling;
                        return;
                    }
                }

                // Track target - update memory
                lastKnownTargetPosition = bestTarget.Data.position;
                hasLastKnownPosition = true;
                ticksSinceLastSeen = 0;

                // Switch target? Force repath
                bool targetChanged = (currentTarget != bestTarget);
                currentTarget = bestTarget;

                // Broadcast to pack
                NotifyPackOfTarget(allShips, bestTarget);

                if (distance <= captureRange)
                {
                    // CAPTURING
                    currentState = BehaviorState.Capturing;
                    captureProgress++;

                    if (enableVisualEffects)
                        PulseCaptureEffect();

                    if (captureProgress >= captureTime)
                    {
                        if (enableEscortCapture)
                        {
                            StartEscortCapture(currentTarget);
                        }
                        else
                        {
                            CaptureMerchant(currentTarget);
                        }
                        
                        RecordCaptureHotspot(controller.Data.position);
                        captureProgress = 0;
                        currentTarget = null;
                        hasLastKnownPosition = false;
                    }
                }
                else
                {
                    // CHASING — THROTTLED PATHFINDING
                    currentState = BehaviorState.Chasing;
                    captureProgress = 0;

                    if (targetChanged)
                        ForceSetDestination(currentTarget.Data.position);
                    else
                        ThrottledSetDestination(currentTarget.Data.position);
                }
                return;
            }
        }

        // PRIORITY 3: Check memory - go to last known position
        if (hasLastKnownPosition)
        {
            ticksSinceLastSeen++;

            if (ticksSinceLastSeen < memoryDurationTicks)
            {
                float distToLastKnown = Vector2.Distance(controller.Data.position, lastKnownTargetPosition);

                if (distToLastKnown > 0.5f)
                {
                    currentState = BehaviorState.Investigating;
                    // Only repath if we don't already have a path to this location
                    ThrottledSetDestination(lastKnownTargetPosition);

                    if (enableLogs)
                        Debug.Log($"[{controller.Data.shipId}] Investigating last known position");
                    return;
                }
                else
                {
                    hasLastKnownPosition = false;
                }
            }
            else
            {
                hasLastKnownPosition = false;
            }
        }

        // PRIORITY 4: Return to ambush point or fallback patrol
        if (currentState != BehaviorState.Chasing)
        {
            if (useAmbushSystem && hasAmbushPoint)
            {
                ReturnToAmbushPoint();
                return;
            }

            if (captureHotspots.Count > 0)
            {
                Vector2 nearestHotspot = GetNearestHotspot();
                float distToHotspot = Vector2.Distance(controller.Data.position, nearestHotspot);

                if (distToHotspot > 2f)
                {
                    currentState = BehaviorState.Patrolling;
                    ThrottledSetDestination(nearestHotspot);
                    return;
                }
            }
        }

        // Default: patrol
        currentTarget = null;
        currentState = BehaviorState.Patrolling;
        captureProgress = 0;
    }

    void ExecuteRetreat(ShipController security)
    {
        currentState = BehaviorState.Retreating;
        currentTarget = null;
        captureProgress = 0;

        Vector2 awayDir = (controller.Data.position - security.Data.position).normalized;
        Vector2 retreatTarget = controller.Data.position + awayDir * fleeDistance * 2f;
        
        // Retreat is urgent — force repath, but still throttle subsequent calls
        ThrottledSetDestination(retreatTarget, forceRepath: true);

        if (enableLogs)
            Debug.Log($"[{controller.Data.shipId}] RETREATING from security!");
    }

    // ==================== AMBUSH/LURKING HELPERS ====================

    void SetPirateVisibility(bool visible)
    {
        if (!invisibleWhileLurking) return;
        if (controller == null || controller.Data == null) return;
        if (controller.Data.type != ShipType.Pirate) return;

        if (spriteRenderer != null)
            spriteRenderer.enabled = visible;

        if (chaseLineRenderer != null && !visible)
            chaseLineRenderer.enabled = false;

        var childRenderers = controller.GetComponentsInChildren<Renderer>();
        foreach (var r in childRenderers)
            r.enabled = visible;
    }

    ShipController FindNearestMerchantInRadius(List<ShipController> allShips, float radius)
    {
        ShipController nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var ship in allShips)
        {
            if (ship == null || ship.Data == null) continue;
            if (ship.Data.type != ShipType.Cargo) continue;
            if (ship.Data.state == ShipState.Captured ||
                ship.Data.state == ShipState.Sunk ||
                ship.Data.state == ShipState.Exited) continue;

            float dist = Vector2.Distance(controller.Data.position, ship.Data.position);
            if (dist <= radius && dist < nearestDist)
            {
                nearestDist = dist;
                nearest = ship;
            }
        }

        return nearest;
    }

    void ReturnToAmbushPoint()
    {
        if (!hasAmbushPoint) return;

        float distToAmbush = Vector2.Distance(controller.Data.position, currentAmbushPoint);
        
        if (distToAmbush < 5f)
        {
            isLurking = true;
            SetPirateVisibility(false);
            controller.Data.state = ShipState.Idle;
            currentState = BehaviorState.Lurking;
        }
        else
        {
            currentState = BehaviorState.Patrolling;
            SetPirateVisibility(true);
            ThrottledSetDestination(currentAmbushPoint);
        }
    }

    void StartEscortCapture(ShipController merchant)
    {
        if (merchant == null) return;

        capturedMerchant = merchant;
        currentState = BehaviorState.Escorting;

        merchant.Data.state = ShipState.Captured;
        merchant.SetState(ShipState.Captured);
        merchant.Data.speedUnitsPerTick = controller.Data.speedUnitsPerTick * 0.8f;

        if (TradeRouteManager.Instance != null)
        {
            Vector2 homeBase = TradeRouteManager.Instance.GetPirateHomeBase(controller.Data.shipId);
            if (homeBase != Vector2.zero)
            {
                escortDestination = homeBase;
                TradeRouteManager.Instance.SetPirateReturning(controller.Data.shipId, true);
            }
            else if (ShipSpawner.Instance != null)
                escortDestination = ShipSpawner.Instance.pirateSpawnCenter;
            else
                escortDestination = controller.Data.position + new Vector2(-20f, 0);
        }
        else if (ShipSpawner.Instance != null)
        {
            escortDestination = ShipSpawner.Instance.pirateSpawnCenter;
        }
        else
        {
            escortDestination = controller.Data.position + new Vector2(-20f, 0);
        }

        ForceSetDestination(escortDestination);

        if (enableLogs)
            Debug.Log($"[{controller.Data.shipId}] ESCORTING captured {merchant.Data.shipId} to base!");

        SpriteRenderer sr = merchant.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = new Color(0.5f, 0.5f, 0.5f, 0.8f);
        }
    }

    void ExecuteEscortBehavior(List<ShipController> allShips)
    {
        if (capturedMerchant == null || capturedMerchant.Data == null)
        {
            capturedMerchant = null;
            currentState = BehaviorState.Patrolling;
            return;
        }

        int nearbySecurity = CountNearbyShipsOfType(allShips, controller.Data.position, forceAssessmentRange, ShipType.Security);
        int nearbyPirates = CountNearbyShipsOfType(allShips, controller.Data.position, forceAssessmentRange, ShipType.Pirate);
        
        if (nearbySecurity > 0)
        {
            float forceRatio = (float)nearbyPirates / nearbySecurity;
            if (forceRatio < minimumForceRatio)
            {
                if (enableLogs)
                    Debug.Log($"[{controller.Data.shipId}] ABANDONING captured merchant - too much security!");
                
                capturedMerchant = null;
                
                ShipController nearestSecurity = FindNearestShipOfType(allShips, ShipType.Security);
                if (nearestSecurity != null)
                {
                    ExecuteRetreat(nearestSecurity);
                }
                else
                {
                    currentState = BehaviorState.Patrolling;
                }
                return;
            }
        }

        if (capturedMerchant != null && capturedMerchant.Data != null)
        {
            Vector2 followPos = controller.Data.position;
            capturedMerchant.Data.position = Vector2.Lerp(capturedMerchant.Data.position, followPos, 0.1f);
            capturedMerchant.transform.position = new Vector3(capturedMerchant.Data.position.x, capturedMerchant.Data.position.y, capturedMerchant.transform.position.z);
        }

        float distToBase = Vector2.Distance(controller.Data.position, escortDestination);
        if (distToBase < 2f)
        {
            if (enableLogs)
                Debug.Log($"[{controller.Data.shipId}] DELIVERED {capturedMerchant.Data.shipId} to pirate base!");

            if (capturedMerchant != null)
            {
                if (enableVisualEffects)
                {
                    StartCoroutine(CaptureVisualEffect(capturedMerchant));
                }
                else
                {
                    Destroy(capturedMerchant.gameObject);
                }
            }

            capturedMerchant = null;
            currentState = BehaviorState.Patrolling;
            return;
        }

        currentState = BehaviorState.Escorting;
        // Throttled repath toward base
        ThrottledSetDestination(escortDestination);
    }

    ShipController FindBestMerchantTarget(List<ShipController> allShips)
    {
        ShipController bestTarget = null;
        float bestScore = float.MinValue;

        foreach (var ship in allShips)
        {
            if (ship == null || ship.Data == null) continue;
            if (ship.Data.type != ShipType.Cargo) continue;
            if (ship.Data.state == ShipState.Captured ||
                ship.Data.state == ShipState.Sunk ||
                ship.Data.state == ShipState.Exited) continue;

            float distance = Vector2.Distance(controller.Data.position, ship.Data.position);
            if (distance > detectionRange * 1.5f) continue;

            float score = 100f;
            score -= distance * 5f;

            float nearestSecurityDist = GetDistanceToNearestSecurity(allShips, ship.Data.position);
            if (nearestSecurityDist < retreatRange)
            {
                score -= 200f;
            }
            else if (nearestSecurityDist < retreatRange * 2f)
            {
                score -= 50f;
            }

            int nearbyMerchants = CountNearbyShipsOfType(allShips, ship.Data.position, 3f, ShipType.Cargo);
            if (nearbyMerchants <= 1)
            {
                score += 30f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = ship;
            }
        }

        return bestScore > 0 ? bestTarget : null;
    }

    void NotifyPackOfTarget(List<ShipController> allShips, ShipController target)
    {
        foreach (var ship in allShips)
        {
            if (ship == null || ship == controller) continue;
            if (ship.Data == null || ship.Data.type != ShipType.Pirate) continue;
            if (ship.Data.state != ShipState.Moving && ship.Data.state != ShipState.Idle) continue;

            float dist = Vector2.Distance(controller.Data.position, ship.Data.position);
            if (dist <= packHuntingRange)
            {
                ShipBehavior otherBehavior = ship.GetComponent<ShipBehavior>();
                if (otherBehavior != null && otherBehavior.currentState == BehaviorState.Patrolling)
                {
                    otherBehavior.ReceivePackHuntingInfo(target.Data.position);
                }
            }
        }
    }

    public void ReceivePackHuntingInfo(Vector2 targetPosition)
    {
        if (!hasLastKnownPosition || currentState == BehaviorState.Patrolling)
        {
            lastKnownTargetPosition = targetPosition;
            hasLastKnownPosition = true;
            ticksSinceLastSeen = 0;

            if (enableLogs)
                Debug.Log($"[{controller.Data.shipId}] Received pack hunting info!");
        }
    }

    // ==================== SMART SECURITY BEHAVIOR ====================

    void ExecuteSmartSecurityBehavior(List<ShipController> allShips)
    {
        // PRIORITY 1: Respond to distress calls
        DistressCall nearestDistress = GetNearestDistressCall();
        if (nearestDistress != null)
        {
            float distToDistress = Vector2.Distance(controller.Data.position, nearestDistress.position);

            if (distToDistress <= distressCallRange)
            {
                currentState = BehaviorState.Responding;
                
                // Throttled repath toward distress location
                ThrottledSetDestination(nearestDistress.position);
                
                isRespondingToDistress = true;
                distressPosition = nearestDistress.position;

                if (enableLogs)
                    Debug.Log($"[{controller.Data.shipId}] Responding to distress call!");

                ShipController nearestPirate = FindNearestShipOfType(allShips, ShipType.Pirate);
                if (nearestPirate != null)
                {
                    float pirateDist = Vector2.Distance(controller.Data.position, nearestPirate.Data.position);
                    if (pirateDist <= captureRange)
                    {
                        DefeatPirate(nearestPirate);
                        return;
                    }
                }
                return;
            }
        }

        // PRIORITY 2: Find pirates - prioritize those actively chasing
        ShipController priorityTarget = FindPriorityPirateTarget(allShips);

        if (priorityTarget != null)
        {
            float distance = Vector2.Distance(controller.Data.position, priorityTarget.Data.position);

            if (distance <= detectionRange)
            {
                bool targetChanged = (currentTarget != priorityTarget);
                currentTarget = priorityTarget;

                if (distance <= captureRange)
                {
                    DefeatPirate(currentTarget);
                    currentTarget = null;
                    currentState = BehaviorState.Patrolling;
                    isRespondingToDistress = false;

                    if (TradeRouteManager.Instance != null)
                        TradeRouteManager.Instance.NavyResumePatrol(controller);
                }
                else
                {
                    currentState = BehaviorState.Chasing;

                    if (targetChanged)
                    {
                        if (TradeRouteManager.Instance != null)
                            TradeRouteManager.Instance.NavyBreakPatrol(controller.Data.shipId);
                        ForceSetDestination(currentTarget.Data.position);
                    }
                    else
                        ThrottledSetDestination(currentTarget.Data.position);
                }
                return;
            }
        }

        // PRIORITY 3: Route-based patrol loop or fallback
        if (TradeRouteManager.Instance != null && TradeRouteManager.Instance.HasNavyPatrolData())
        {
            TradeRouteManager.Instance.UpdateNavyPatrol(controller);
            currentState = BehaviorState.Patrolling;
            currentTarget = null;
            isRespondingToDistress = false;
            return;
        }

        if (captureHotspots.Count > 0)
        {
            Vector2 nearestHotspot = GetNearestHotspot();
            float distToHotspot = Vector2.Distance(controller.Data.position, nearestHotspot);

            if (distToHotspot > 3f)
            {
                currentState = BehaviorState.Patrolling;
                ThrottledSetDestination(nearestHotspot);
                return;
            }
        }

        currentTarget = null;
        currentState = BehaviorState.Patrolling;
        isRespondingToDistress = false;
    }

    ShipController FindPriorityPirateTarget(List<ShipController> allShips)
    {
        ShipController chasingPirate = null;
        ShipController anyPirate = null;
        float nearestChasingDist = float.MaxValue;
        float nearestAnyDist = float.MaxValue;

        foreach (var ship in allShips)
        {
            if (ship == null || ship.Data == null) continue;
            if (ship.Data.type != ShipType.Pirate) continue;
            if (ship.Data.state == ShipState.Sunk) continue;

            // Skip lurking pirates — they're hidden and undetectable by navy
            ShipBehavior pirateBehavior = ship.GetComponent<ShipBehavior>();
            if (pirateBehavior != null && pirateBehavior.currentState == BehaviorState.Lurking) continue;

            float dist = Vector2.Distance(controller.Data.position, ship.Data.position);

            bool isChasing = pirateBehavior != null &&
                (pirateBehavior.currentState == BehaviorState.Chasing ||
                 pirateBehavior.currentState == BehaviorState.Capturing);

            if (isChasing && dist < nearestChasingDist)
            {
                nearestChasingDist = dist;
                chasingPirate = ship;
            }

            if (dist < nearestAnyDist)
            {
                nearestAnyDist = dist;
                anyPirate = ship;
            }
        }

        return chasingPirate ?? anyPirate;
    }

    // ==================== SMART MERCHANT BEHAVIOR ====================

    void ExecuteSmartMerchantBehavior(List<ShipController> allShips)
    {
        if (controller.Data.state == ShipState.Captured)
        {
            currentState = BehaviorState.Idle;
            return;
        }

        ShipController nearestPirate = FindNearestShipOfType(allShips, ShipType.Pirate);

        if (nearestPirate == null)
        {
            if (currentState == BehaviorState.Fleeing)
            {
                controller.Data.speedUnitsPerTick = originalSpeed;
                ReturnToOriginalDestination();
            }
            currentState = BehaviorState.Patrolling;
            return;
        }

        float distance = Vector2.Distance(controller.Data.position, nearestPirate.Data.position);

        if (distance <= detectionRange)
        {
            if (currentState != BehaviorState.Fleeing)
            {
                SaveOriginalDestination();
                controller.Data.speedUnitsPerTick = originalSpeed * fleeSpeedBoost;
                BroadcastDistressCall();
                
                // First flee — force immediate repath
                FleeFrom(nearestPirate, forceRepath: true);
            }
            else
            {
                // Continuing to flee — throttled repath
                FleeFrom(nearestPirate, forceRepath: false);
            }

            currentState = BehaviorState.Fleeing;
        }
        else
        {
            if (currentState == BehaviorState.Fleeing)
            {
                controller.Data.speedUnitsPerTick = originalSpeed;
                ReturnToOriginalDestination();
            }
            currentState = BehaviorState.Patrolling;
        }
    }

    void BroadcastDistressCall()
    {
        foreach (var call in activeDistressCalls)
        {
            if (call.merchant == controller)
            {
                call.ticksRemaining = 30;
                call.position = controller.Data.position;
                return;
            }
        }

        activeDistressCalls.Add(new DistressCall
        {
            position = controller.Data.position,
            merchant = controller,
            ticksRemaining = 30
        });

        if (enableLogs)
            Debug.Log($"[{controller.Data.shipId}] DISTRESS CALL BROADCAST!");
    }

    // ==================== SHARED SYSTEMS ====================

    void UpdateDistressCalls()
    {
        for (int i = activeDistressCalls.Count - 1; i >= 0; i--)
        {
            activeDistressCalls[i].ticksRemaining--;
            if (activeDistressCalls[i].ticksRemaining <= 0)
            {
                activeDistressCalls.RemoveAt(i);
            }
        }
    }

    DistressCall GetNearestDistressCall()
    {
        DistressCall nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var call in activeDistressCalls)
        {
            float dist = Vector2.Distance(controller.Data.position, call.position);
            if (dist < nearestDist && dist <= distressCallRange)
            {
                nearestDist = dist;
                nearest = call;
            }
        }

        return nearest;
    }

    void RecordCaptureHotspot(Vector2 position)
    {
        captureHotspots.Add(position);
        if (captureHotspots.Count > MAX_HOTSPOTS)
        {
            captureHotspots.RemoveAt(0);
        }

        if (enableLogs)
            Debug.Log($"Capture hotspot recorded at {position}. Total hotspots: {captureHotspots.Count}");
    }

    Vector2 GetNearestHotspot()
    {
        Vector2 nearest = controller.Data.position;
        float nearestDist = float.MaxValue;

        foreach (var hotspot in captureHotspots)
        {
            float dist = Vector2.Distance(controller.Data.position, hotspot);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = hotspot;
            }
        }

        return nearest;
    }

    float GetDistanceToNearestSecurity(List<ShipController> allShips, Vector2 position)
    {
        float nearest = float.MaxValue;

        foreach (var ship in allShips)
        {
            if (ship == null || ship.Data == null) continue;
            if (ship.Data.type != ShipType.Security) continue;
            if (ship.Data.state == ShipState.Sunk) continue;

            float dist = Vector2.Distance(position, ship.Data.position);
            if (dist < nearest)
                nearest = dist;
        }

        return nearest;
    }

    int CountNearbyShipsOfType(List<ShipController> allShips, Vector2 position, float range, ShipType type)
    {
        int count = 0;
        foreach (var ship in allShips)
        {
            if (ship == null || ship.Data == null) continue;
            if (ship.Data.type != type) continue;
            if (ship.Data.state == ShipState.Captured ||
                ship.Data.state == ShipState.Sunk ||
                ship.Data.state == ShipState.Exited) continue;

            // Skip lurking pirates — they're hidden
            if (ship.Data.type == ShipType.Pirate)
            {
                ShipBehavior sb = ship.GetComponent<ShipBehavior>();
                if (sb != null && sb.currentState == BehaviorState.Lurking) continue;
            }

            if (Vector2.Distance(position, ship.Data.position) <= range)
                count++;
        }
        return count;
    }

    // ==================== ACTIONS ====================

    void CaptureMerchant(ShipController merchant)
    {
        if (merchant == null || merchant.Data == null) return;

        Debug.Log($"*** {controller.Data.shipId} CAPTURED {merchant.Data.shipId}! ***");

        merchant.Data.state = ShipState.Captured;
        merchant.SetState(ShipState.Captured);

        if (enableVisualEffects)
        {
            StartCoroutine(CaptureVisualEffect(merchant));
        }
    }

    void DefeatPirate(ShipController pirate)
    {
        if (pirate == null || pirate.Data == null) return;

        Debug.Log($"*** {controller.Data.shipId} DEFEATED {pirate.Data.shipId}! ***");

        pirate.Data.state = ShipState.Sunk;
        pirate.SetState(ShipState.Sunk);

        if (enableVisualEffects)
        {
            StartCoroutine(DefeatVisualEffect(pirate));
        }
    }

    // ==================== VISUAL EFFECTS ====================

    System.Collections.IEnumerator CaptureVisualEffect(ShipController target)
    {
        if (target == null) yield break;

        SpriteRenderer sr = target.GetComponent<SpriteRenderer>();
        if (sr == null) yield break;

        sr.color = Color.white;
        yield return new WaitForSeconds(0.1f);

        if (target == null || sr == null) yield break;
        sr.color = Color.red;
        yield return new WaitForSeconds(0.1f);

        float fadeTime = 0.3f;
        float elapsed = 0f;
        Color capturedColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);

        while (elapsed < fadeTime)
        {
            if (target == null || sr == null) yield break;
            elapsed += Time.deltaTime;
            sr.color = Color.Lerp(Color.red, capturedColor, elapsed / fadeTime);
            yield return null;
        }

        if (target == null || sr == null) yield break;
        sr.color = capturedColor;

        if (captureEffectPrefab != null && target != null)
        {
            Instantiate(captureEffectPrefab, target.transform.position, Quaternion.identity);
        }

        yield return new WaitForSeconds(2f);

        if (target == null || sr == null) yield break;

        float fadeOutTime = 1.5f;
        elapsed = 0f;
        Vector3 originalScale = target.transform.localScale;

        while (elapsed < fadeOutTime)
        {
            if (target == null || sr == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / fadeOutTime;
            Color c = sr.color;
            c.a = Mathf.Lerp(0.5f, 0f, t);
            sr.color = c;
            target.transform.localScale = Vector3.Lerp(originalScale, originalScale * 0.5f, t);
            yield return null;
        }

        if (target != null)
            Destroy(target.gameObject);
    }

    System.Collections.IEnumerator DefeatVisualEffect(ShipController target)
    {
        if (target == null) yield break;

        SpriteRenderer sr = target.GetComponent<SpriteRenderer>();
        if (sr == null) yield break;

        Vector3 originalScale = target.transform.localScale;

        if (target == null) yield break;
        target.transform.localScale = originalScale * 1.3f;

        if (target == null || sr == null) yield break;
        sr.color = Color.white;
        yield return new WaitForSeconds(0.05f);

        if (target == null || sr == null) yield break;
        sr.color = Color.cyan;
        yield return new WaitForSeconds(0.05f);

        if (target == null || sr == null) yield break;
        sr.color = new Color(1f, 0.5f, 0f);
        target.transform.localScale = originalScale * 1.5f;
        yield return new WaitForSeconds(0.05f);

        if (target == null || sr == null) yield break;
        sr.color = Color.yellow;
        yield return new WaitForSeconds(0.05f);

        if (target == null || sr == null) yield break;
        sr.color = Color.red;
        yield return new WaitForSeconds(0.05f);

        float fadeTime = 0.4f;
        float elapsed = 0f;

        while (elapsed < fadeTime)
        {
            if (target == null || sr == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / fadeTime;
            target.transform.localScale = Vector3.Lerp(originalScale * 1.5f, Vector3.zero, t);
            target.transform.Rotate(0, 0, 720 * Time.deltaTime);
            Color c = Color.Lerp(Color.red, new Color(0.2f, 0.2f, 0.2f, 0f), t);
            sr.color = c;
            yield return null;
        }

        if (explosionPrefab != null && target != null)
        {
            Instantiate(explosionPrefab, target.transform.position, Quaternion.identity);
        }

        if (target != null)
            Destroy(target.gameObject);
    }

    void PulseCaptureEffect()
    {
        if (spriteRenderer == null) return;
        float pulse = Mathf.Sin(Time.time * 15f);
        spriteRenderer.color = Color.Lerp(originalColor, Color.red, (pulse + 1f) / 2f);
    }

    void UpdateChaseLine()
    {
        if (chaseLineRenderer == null) return;

        bool showLine = currentTarget != null &&
            (currentState == BehaviorState.Chasing ||
             currentState == BehaviorState.Capturing ||
             currentState == BehaviorState.Responding);

        if (currentState == BehaviorState.Escorting && capturedMerchant != null)
        {
            chaseLineRenderer.enabled = true;
            chaseLineRenderer.SetPosition(0, transform.position);
            chaseLineRenderer.SetPosition(1, capturedMerchant.transform.position);
            chaseLineRenderer.startColor = new Color(0.5f, 0f, 0.5f);
            chaseLineRenderer.endColor = new Color(0.8f, 0.4f, 0.8f);
            return;
        }

        if (currentState == BehaviorState.Investigating && hasLastKnownPosition)
        {
            chaseLineRenderer.enabled = true;
            chaseLineRenderer.SetPosition(0, transform.position);
            chaseLineRenderer.SetPosition(1, new Vector3(lastKnownTargetPosition.x, lastKnownTargetPosition.y, 0));
            chaseLineRenderer.startColor = Color.gray;
            chaseLineRenderer.endColor = Color.yellow;
            return;
        }

        if (showLine && currentTarget != null)
        {
            chaseLineRenderer.enabled = true;
            chaseLineRenderer.SetPosition(0, transform.position);
            chaseLineRenderer.SetPosition(1, currentTarget.transform.position);

            if (controller.Data.type == ShipType.Security)
            {
                chaseLineRenderer.startColor = currentState == BehaviorState.Responding ? Color.green : Color.white;
                chaseLineRenderer.endColor = Color.cyan;
            }
            else
            {
                chaseLineRenderer.startColor = currentState == BehaviorState.Capturing ? Color.red : Color.yellow;
                chaseLineRenderer.endColor = Color.red;
            }
        }
        else
        {
            chaseLineRenderer.enabled = false;
        }
    }

    void UpdateVisuals()
    {
        if (spriteRenderer == null) return;

        switch (currentState)
        {
            case BehaviorState.Fleeing:
                float flash = Mathf.Sin(Time.time * 10f) > 0 ? 1f : 0.6f;
                spriteRenderer.color = originalColor * flash;
                break;
            case BehaviorState.Retreating:
                float retreatFlash = Mathf.Sin(Time.time * 8f) > 0 ? 0.8f : 0.5f;
                spriteRenderer.color = new Color(originalColor.r * retreatFlash, originalColor.g * 0.5f, originalColor.b * 0.5f);
                break;
            case BehaviorState.Capturing:
                break;
            case BehaviorState.Chasing:
                spriteRenderer.color = originalColor * 1.2f;
                break;
            case BehaviorState.Investigating:
                spriteRenderer.color = originalColor * 0.8f;
                break;
            case BehaviorState.Responding:
                float respondPulse = (Mathf.Sin(Time.time * 6f) + 1f) / 2f;
                spriteRenderer.color = Color.Lerp(originalColor, Color.green, respondPulse * 0.3f);
                break;
            case BehaviorState.Escorting:
                float escortPulse = (Mathf.Sin(Time.time * 4f) + 1f) / 2f;
                spriteRenderer.color = Color.Lerp(originalColor, new Color(0.8f, 0.2f, 0.8f), escortPulse * 0.4f);
                break;
            case BehaviorState.Lurking:
                break;
            default:
                spriteRenderer.color = originalColor;
                break;
        }
    }

    // ==================== HELPERS ====================

    ShipController FindNearestShipOfType(List<ShipController> allShips, ShipType targetType)
    {
        ShipController nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var ship in allShips)
        {
            if (ship == null || ship == controller) continue;
            if (ship.Data == null) continue;
            if (ship.Data.type != targetType) continue;
            if (ship.Data.state == ShipState.Captured ||
                ship.Data.state == ShipState.Sunk ||
                ship.Data.state == ShipState.Exited) continue;

            // Skip lurking pirates — they're hidden and undetectable
            if (ship.Data.type == ShipType.Pirate)
            {
                ShipBehavior sb = ship.GetComponent<ShipBehavior>();
                if (sb != null && sb.currentState == BehaviorState.Lurking) continue;
            }

            float dist = Vector2.Distance(controller.Data.position, ship.Data.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = ship;
            }
        }

        return nearest;
    }

    void FleeFrom(ShipController threat, bool forceRepath = false)
    {
        if (threat == null || threat.Data == null) return;

        Vector2 awayDir = (controller.Data.position - threat.Data.position).normalized;
        Vector2 fleeTarget = controller.Data.position + awayDir * fleeDistance;
        
        if (forceRepath)
        {
            ForceSetDestination(fleeTarget);
        }
        else
        {
            // Use flee-specific throttle (shorter interval since flee is urgent)
            ticksSinceLastFleeRepath++;
            if (ticksSinceLastFleeRepath >= fleeRepathInterval)
            {
                controller.SetDestination(fleeTarget);
                ticksSinceLastFleeRepath = 0;
            }
        }
    }

    void SaveOriginalDestination()
    {
        if (hasOriginalDestination) return;

        if (controller.Data.route != null &&
            controller.Data.route.waypoints != null &&
            controller.Data.route.waypoints.Count > 0)
        {
            originalDestination = controller.Data.route.waypoints[controller.Data.route.waypoints.Count - 1];
            hasOriginalDestination = true;
        }
    }

    void ReturnToOriginalDestination()
    {
        if (hasOriginalDestination)
        {
            ForceSetDestination(originalDestination);
        }
    }

    public static void ResetSharedData()
    {
        captureHotspots.Clear();
        activeDistressCalls.Clear();
    }

    // ==================== DEBUG GIZMOS ====================

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, captureRange);

        if (controller != null && controller.Data != null && controller.Data.type == ShipType.Pirate)
        {
            Gizmos.color = new Color(0f, 0f, 1f, 0.1f);
            Gizmos.DrawWireSphere(transform.position, retreatRange);
        }

        if (hasLastKnownPosition)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(new Vector3(lastKnownTargetPosition.x, lastKnownTargetPosition.y, 0), 0.3f);
            Gizmos.DrawLine(transform.position, new Vector3(lastKnownTargetPosition.x, lastKnownTargetPosition.y, 0));
        }

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        foreach (var hotspot in captureHotspots)
        {
            Gizmos.DrawWireSphere(new Vector3(hotspot.x, hotspot.y, 0), 0.5f);
        }
    }
#endif
}