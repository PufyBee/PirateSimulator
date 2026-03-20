using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// SMART SHIP BEHAVIOR - Advanced AI System
/// 
/// PIRATE INTELLIGENCE:
/// - Memory: Remembers last seen merchant position, pursues even after losing sight
/// - Retreat: Flees when security is nearby instead of suiciding
/// - Learning Hotspots: Gravitates toward areas where captures happened
/// - Pack Hunting: Nearby pirates join active chases
/// - Threat Assessment: Avoids guarded merchants, targets isolated ones
/// 
/// MERCHANT INTELLIGENCE:
/// - Distress Calls: Broadcasts position when chased, security responds
/// - Route Adaptation: Avoids areas where captures occurred
/// 
/// SECURITY INTELLIGENCE:
/// - Priority Targeting: Prioritizes pirates actively chasing merchants
/// - Distress Response: Responds to merchant distress calls
/// - Patrol Optimization: Patrols high-risk areas
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

    [Header("=== VISUAL EFFECTS ===")]
    public bool enableVisualEffects = true;
    public GameObject explosionPrefab;
    public GameObject captureEffectPrefab;

    [Header("=== DEBUG ===")]
    public bool showGizmos = true;
    public bool enableLogs = false;

    // Components
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

    // === SMART AI STATE ===
    private Vector2 lastKnownTargetPosition;
    private bool hasLastKnownPosition = false;
    private int ticksSinceLastSeen = 0;
    private bool isRespondingToDistress = false;
    private Vector2 distressPosition;

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
        Retreating,      // Pirate fleeing from security
        Investigating,   // Going to last known position
        Responding,      // Security responding to distress
        Escorting        // NEW: Pirate escorting captured merchant
    }

    // Distress call data structure
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

    /// <summary>
    /// Called every simulation tick by SimulationEngine
    /// </summary>
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

        // Update distress calls (decay over time)
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

        // PRIORITY 1: Check for nearby security - RETREAT if outgunned!
        int nearbyPirates = CountNearbyShipsOfType(allShips, controller.Data.position, forceAssessmentRange, ShipType.Pirate);
        int nearbySecurity = CountNearbyShipsOfType(allShips, controller.Data.position, forceAssessmentRange, ShipType.Security);
        
        // Force assessment - retreat if outgunned
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
            if (securityDist <= retreatRange * 1.5f) // Wider retreat range when outgunned
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
                // Count security near the TARGET (not near us)
                int securityNearTarget = CountNearbyShipsOfType(allShips, bestTarget.Data.position, forceAssessmentRange, ShipType.Security);
                int piratesNearTarget = CountNearbyShipsOfType(allShips, bestTarget.Data.position, forceAssessmentRange, ShipType.Pirate);
                
                if (securityNearTarget > 0)
                {
                    float engageRatio = (float)piratesNearTarget / securityNearTarget;
                    if (engageRatio < minimumForceRatio)
                    {
                        // Too dangerous - don't engage, patrol instead
                        if (enableLogs)
                            Debug.Log($"[{controller.Data.shipId}] Target too well guarded. Pirates:{piratesNearTarget} vs Security:{securityNearTarget}");
                        
                        currentTarget = null;
                        currentState = BehaviorState.Patrolling;
                        return;
                    }
                }

                // Safe to engage - update memory
                lastKnownTargetPosition = bestTarget.Data.position;
                hasLastKnownPosition = true;
                ticksSinceLastSeen = 0;
                currentTarget = bestTarget;

                // Broadcast to pack - other pirates join the hunt
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
                        // CAPTURE SUCCESS!
                        if (enableEscortCapture)
                        {
                            // Start escorting the merchant instead of destroying
                            StartEscortCapture(currentTarget);
                        }
                        else
                        {
                            // Old behavior - destroy merchant
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
                    // CHASING
                    currentState = BehaviorState.Chasing;
                    captureProgress = 0;
                    controller.SetDestination(currentTarget.Data.position);
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
                    controller.SetDestination(lastKnownTargetPosition);

                    if (enableLogs)
                        Debug.Log($"[{controller.Data.shipId}] Investigating last known position");
                    return;
                }
                else
                {
                    // Reached last known position, target not there
                    hasLastKnownPosition = false;
                }
            }
            else
            {
                // Memory expired
                hasLastKnownPosition = false;
            }
        }

        // PRIORITY 4: Patrol toward hotspots
        if (captureHotspots.Count > 0 && currentState != BehaviorState.Chasing)
        {
            Vector2 nearestHotspot = GetNearestHotspot();
            float distToHotspot = Vector2.Distance(controller.Data.position, nearestHotspot);

            if (distToHotspot > 2f)
            {
                currentState = BehaviorState.Patrolling;
                controller.SetDestination(nearestHotspot);
                return;
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

        // Run away from security
        Vector2 awayDir = (controller.Data.position - security.Data.position).normalized;
        Vector2 retreatTarget = controller.Data.position + awayDir * fleeDistance * 2f;
        controller.SetDestination(retreatTarget);

        if (enableLogs)
            Debug.Log($"[{controller.Data.shipId}] RETREATING from security!");
    }

    /// <summary>
    /// Start escorting a captured merchant back to pirate base
    /// </summary>
    void StartEscortCapture(ShipController merchant)
    {
        if (merchant == null) return;

        capturedMerchant = merchant;
        currentState = BehaviorState.Escorting;

        // Mark merchant as captured but don't destroy
        merchant.Data.state = ShipState.Captured;
        merchant.SetState(ShipState.Captured);

        // Slow down the merchant (it's being towed)
        merchant.Data.speedUnitsPerTick = controller.Data.speedUnitsPerTick * 0.8f;

        // Set destination to pirate spawn area (their "base")
        if (ShipSpawner.Instance != null)
        {
            escortDestination = ShipSpawner.Instance.pirateSpawnCenter;
        }
        else
        {
            // Fallback - just go off screen
            escortDestination = controller.Data.position + new Vector2(-20f, 0);
        }

        controller.SetDestination(escortDestination);

        if (enableLogs)
            Debug.Log($"[{controller.Data.shipId}] ESCORTING captured {merchant.Data.shipId} to base!");

        // Visual feedback - dim the merchant
        SpriteRenderer sr = merchant.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = new Color(0.5f, 0.5f, 0.5f, 0.8f);
        }
    }

    /// <summary>
    /// Execute escort behavior - pirate leading captured merchant to base
    /// </summary>
    void ExecuteEscortBehavior(List<ShipController> allShips)
    {
        // Check if merchant is still valid
        if (capturedMerchant == null || capturedMerchant.Data == null)
        {
            // Lost the merchant somehow
            capturedMerchant = null;
            currentState = BehaviorState.Patrolling;
            return;
        }

        // Check for nearby security - might need to abandon capture and flee
        int nearbySecurity = CountNearbyShipsOfType(allShips, controller.Data.position, forceAssessmentRange, ShipType.Security);
        int nearbyPirates = CountNearbyShipsOfType(allShips, controller.Data.position, forceAssessmentRange, ShipType.Pirate);
        
        if (nearbySecurity > 0)
        {
            float forceRatio = (float)nearbyPirates / nearbySecurity;
            if (forceRatio < minimumForceRatio)
            {
                // Outgunned! Abandon the merchant and flee
                if (enableLogs)
                    Debug.Log($"[{controller.Data.shipId}] ABANDONING captured merchant - too much security!");
                
                // Release merchant (it's still captured but stationary)
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

        // Make merchant follow pirate
        if (capturedMerchant != null && capturedMerchant.Data != null)
        {
            // Merchant follows slightly behind pirate
            Vector2 followPos = controller.Data.position;
            capturedMerchant.Data.position = Vector2.Lerp(capturedMerchant.Data.position, followPos, 0.1f);
            capturedMerchant.transform.position = new Vector3(capturedMerchant.Data.position.x, capturedMerchant.Data.position.y, capturedMerchant.transform.position.z);
        }

        // Check if we've reached the base
        float distToBase = Vector2.Distance(controller.Data.position, escortDestination);
        if (distToBase < 2f)
        {
            // Successfully delivered merchant to pirate base!
            if (enableLogs)
                Debug.Log($"[{controller.Data.shipId}] DELIVERED {capturedMerchant.Data.shipId} to pirate base!");

            // Now actually "process" the merchant (destroy with effect)
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

        // Continue toward base
        currentState = BehaviorState.Escorting;
    }

    /// <summary>
    /// Find best merchant target using threat assessment
    /// Prefers isolated merchants, avoids guarded ones
    /// </summary>
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
            if (distance > detectionRange * 1.5f) continue; // Extended range for assessment

            // Calculate threat score
            float score = 100f;

            // Closer is better
            score -= distance * 5f;

            // Check for nearby security (BAD)
            float nearestSecurityDist = GetDistanceToNearestSecurity(allShips, ship.Data.position);
            if (nearestSecurityDist < retreatRange)
            {
                score -= 200f; // Heavily penalize guarded merchants
            }
            else if (nearestSecurityDist < retreatRange * 2f)
            {
                score -= 50f; // Somewhat risky
            }

            // Isolated merchants are better targets
            int nearbyMerchants = CountNearbyShipsOfType(allShips, ship.Data.position, 3f, ShipType.Cargo);
            if (nearbyMerchants <= 1)
            {
                score += 30f; // Isolated = easy target
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
                    // Share target info with pack member
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
                controller.SetDestination(nearestDistress.position);
                isRespondingToDistress = true;
                distressPosition = nearestDistress.position;

                if (enableLogs)
                    Debug.Log($"[{controller.Data.shipId}] Responding to distress call!");

                // Continue to check for pirates en route
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
                currentTarget = priorityTarget;

                if (distance <= captureRange)
                {
                    DefeatPirate(currentTarget);
                    currentTarget = null;
                    currentState = BehaviorState.Patrolling;
                    isRespondingToDistress = false;
                }
                else
                {
                    currentState = BehaviorState.Chasing;
                    controller.SetDestination(currentTarget.Data.position);
                }
                return;
            }
        }

        // PRIORITY 3: Patrol hotspots (where captures happened)
        if (captureHotspots.Count > 0)
        {
            Vector2 nearestHotspot = GetNearestHotspot();
            float distToHotspot = Vector2.Distance(controller.Data.position, nearestHotspot);

            if (distToHotspot > 3f)
            {
                currentState = BehaviorState.Patrolling;
                controller.SetDestination(nearestHotspot);
                return;
            }
        }

        currentTarget = null;
        currentState = BehaviorState.Patrolling;
        isRespondingToDistress = false;
    }

    /// <summary>
    /// Find pirate to target - prioritizes pirates actively chasing merchants
    /// </summary>
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

            float dist = Vector2.Distance(controller.Data.position, ship.Data.position);

            ShipBehavior pirateBehavior = ship.GetComponent<ShipBehavior>();
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

        // Prioritize chasing pirates
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

                // BROADCAST DISTRESS CALL
                BroadcastDistressCall();
            }

            currentState = BehaviorState.Fleeing;
            FleeFrom(nearestPirate);
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
        // Check if we already have an active distress call
        foreach (var call in activeDistressCalls)
        {
            if (call.merchant == controller)
            {
                call.ticksRemaining = 30; // Refresh
                call.position = controller.Data.position;
                return;
            }
        }

        // Create new distress call
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
        // Add to hotspots, limit size
        captureHotspots.Add(position);
        if (captureHotspots.Count > MAX_HOTSPOTS)
        {
            captureHotspots.RemoveAt(0); // Remove oldest
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

        // Show line when escorting captured merchant
        if (currentState == BehaviorState.Escorting && capturedMerchant != null)
        {
            chaseLineRenderer.enabled = true;
            chaseLineRenderer.SetPosition(0, transform.position);
            chaseLineRenderer.SetPosition(1, capturedMerchant.transform.position);
            chaseLineRenderer.startColor = new Color(0.5f, 0f, 0.5f); // Purple
            chaseLineRenderer.endColor = new Color(0.8f, 0.4f, 0.8f);
            return;
        }

        // Also show line when investigating
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
                // Pirates retreating flash differently
                float retreatFlash = Mathf.Sin(Time.time * 8f) > 0 ? 0.8f : 0.5f;
                spriteRenderer.color = new Color(originalColor.r * retreatFlash, originalColor.g * 0.5f, originalColor.b * 0.5f);
                break;
            case BehaviorState.Capturing:
                break; // Handled by PulseCaptureEffect
            case BehaviorState.Chasing:
                spriteRenderer.color = originalColor * 1.2f;
                break;
            case BehaviorState.Investigating:
                // Dim color when investigating
                spriteRenderer.color = originalColor * 0.8f;
                break;
            case BehaviorState.Responding:
                // Security responding pulses green
                float respondPulse = (Mathf.Sin(Time.time * 6f) + 1f) / 2f;
                spriteRenderer.color = Color.Lerp(originalColor, Color.green, respondPulse * 0.3f);
                break;
            case BehaviorState.Escorting:
                // Pirate escorting pulses purple (success!)
                float escortPulse = (Mathf.Sin(Time.time * 4f) + 1f) / 2f;
                spriteRenderer.color = Color.Lerp(originalColor, new Color(0.8f, 0.2f, 0.8f), escortPulse * 0.4f);
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

            float dist = Vector2.Distance(controller.Data.position, ship.Data.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = ship;
            }
        }

        return nearest;
    }

    void FleeFrom(ShipController threat)
    {
        if (threat == null || threat.Data == null) return;

        Vector2 awayDir = (controller.Data.position - threat.Data.position).normalized;
        Vector2 fleeTarget = controller.Data.position + awayDir * fleeDistance;
        controller.SetDestination(fleeTarget);
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
            controller.SetDestination(originalDestination);
        }
    }

    /// <summary>
    /// Clear all static data (call on simulation reset)
    /// </summary>
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

        // Detection range
        Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Capture range
        Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, captureRange);

        // Retreat range (pirates only)
        if (controller != null && controller.Data != null && controller.Data.type == ShipType.Pirate)
        {
            Gizmos.color = new Color(0f, 0f, 1f, 0.1f);
            Gizmos.DrawWireSphere(transform.position, retreatRange);
        }

        // Last known position
        if (hasLastKnownPosition)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(new Vector3(lastKnownTargetPosition.x, lastKnownTargetPosition.y, 0), 0.3f);
            Gizmos.DrawLine(transform.position, new Vector3(lastKnownTargetPosition.x, lastKnownTargetPosition.y, 0));
        }

        // Draw hotspots
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        foreach (var hotspot in captureHotspots)
        {
            Gizmos.DrawWireSphere(new Vector3(hotspot.x, hotspot.y, 0), 0.5f);
        }
    }
#endif
}