using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Ship Behavior with Visual Effects
/// 
/// IMPROVEMENTS:
/// - Visual feedback for captures (flash, explosion effect)
/// - Easier capture mechanics
/// - Sound effect hooks (for future)
/// - Chase lines visible in game (not just editor)
/// </summary>
public class ShipBehavior : MonoBehaviour
{
    [Header("=== DETECTION ===")]
    public float detectionRange = 8f;

    [Header("=== CAPTURE/COMBAT ===")]
    public float captureRange = 1.5f;      // Increased from 0.5
    public int captureTime = 5;             // Reduced from 10 (faster captures)

    [Header("=== FLEE SETTINGS ===")]
    public float fleeDistance = 3f;
    public float fleeSpeedBoost = 1.3f;     // Merchants move faster when fleeing

    [Header("=== VISUAL EFFECTS ===")]
    public bool enableVisualEffects = true;
    public GameObject explosionPrefab;       // Optional: assign explosion particle
    public GameObject captureEffectPrefab;   // Optional: assign capture effect

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

    public enum BehaviorState
    {
        Idle,
        Patrolling,
        Chasing,
        Fleeing,
        Capturing
    }

    void Awake()
    {
        controller = GetComponent<ShipController>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;

        // Create line renderer for chase visualization
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
        // Update chase line in real-time
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

        UpdateVisuals();
    }

    // ===== PIRATE BEHAVIOR =====

    void ExecutePirateBehavior(List<ShipController> allShips)
    {
        ShipController nearestMerchant = FindNearestShipOfType(allShips, ShipType.Cargo);

        if (nearestMerchant == null)
        {
            currentState = BehaviorState.Patrolling;
            currentTarget = null;
            captureProgress = 0;
            return;
        }

        float distance = Vector2.Distance(controller.Data.position, nearestMerchant.Data.position);

        if (enableLogs)
            Debug.Log($"[{controller.Data.shipId}] Merchant at distance {distance:F2} (capture range: {captureRange})");

        if (distance <= detectionRange)
        {
            currentTarget = nearestMerchant;

            if (distance <= captureRange)
            {
                // IN CAPTURE RANGE!
                currentState = BehaviorState.Capturing;
                captureProgress++;

                // Visual pulse effect during capture
                if (enableVisualEffects)
                    PulseCaptureEffect();

                if (enableLogs)
                    Debug.Log($"[{controller.Data.shipId}] CAPTURING {currentTarget.Data.shipId}... {captureProgress}/{captureTime}");

                if (captureProgress >= captureTime)
                {
                    CaptureMerchant(currentTarget);
                    captureProgress = 0;
                    currentTarget = null;
                    currentState = BehaviorState.Patrolling;
                }
            }
            else
            {
                // Chase
                currentState = BehaviorState.Chasing;
                captureProgress = 0; // Reset if they get away
                controller.SetDestination(currentTarget.Data.position);
            }
        }
        else
        {
            currentTarget = null;
            currentState = BehaviorState.Patrolling;
            captureProgress = 0;
        }
    }

    // ===== SECURITY BEHAVIOR =====

    void ExecuteSecurityBehavior(List<ShipController> allShips)
    {
        ShipController nearestPirate = FindNearestShipOfType(allShips, ShipType.Pirate);

        if (nearestPirate == null)
        {
            currentState = BehaviorState.Patrolling;
            currentTarget = null;
            return;
        }

        float distance = Vector2.Distance(controller.Data.position, nearestPirate.Data.position);

        if (distance <= detectionRange)
        {
            currentTarget = nearestPirate;

            if (distance <= captureRange)
            {
                // Instant defeat
                DefeatPirate(currentTarget);
                currentTarget = null;
                currentState = BehaviorState.Patrolling;
            }
            else
            {
                currentState = BehaviorState.Chasing;
                controller.SetDestination(currentTarget.Data.position);
            }
        }
        else
        {
            currentTarget = null;
            currentState = BehaviorState.Patrolling;
        }
    }

    // ===== MERCHANT BEHAVIOR =====

    void ExecuteMerchantBehavior(List<ShipController> allShips)
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
                // Restore normal speed
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
                // Speed boost when fleeing!
                controller.Data.speedUnitsPerTick = originalSpeed * fleeSpeedBoost;
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

    // ===== ACTIONS =====

    void CaptureMerchant(ShipController merchant)
    {
        if (merchant == null || merchant.Data == null) return;

        Debug.Log($"*** {controller.Data.shipId} CAPTURED {merchant.Data.shipId}! ***");

        merchant.Data.state = ShipState.Captured;
        merchant.SetState(ShipState.Captured);

        // VISUAL EFFECT: Flash and fade
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

        // VISUAL EFFECT: Explosion
        if (enableVisualEffects)
        {
            StartCoroutine(DefeatVisualEffect(pirate));
        }
    }

    // ===== VISUAL EFFECTS =====

    System.Collections.IEnumerator CaptureVisualEffect(ShipController target)
    {
        if (target == null) yield break;
        
        SpriteRenderer sr = target.GetComponent<SpriteRenderer>();
        if (sr == null) yield break;

        // Flash white
        if (target == null || sr == null) yield break;
        sr.color = Color.white;
        yield return new WaitForSeconds(0.1f);

        // Flash red
        if (target == null || sr == null) yield break;
        sr.color = Color.red;
        yield return new WaitForSeconds(0.1f);

        // Fade to gray
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

        // Optional: Spawn capture effect particle
        if (captureEffectPrefab != null && target != null)
        {
            Instantiate(captureEffectPrefab, target.transform.position, Quaternion.identity);
        }

        // FADE OUT completely over time
        yield return new WaitForSeconds(2f); // Wait a bit before fading

        if (target == null || sr == null) yield break;

        float fadeOutTime = 1.5f;
        elapsed = 0f;
        Vector3 originalScale = target.transform.localScale;

        while (elapsed < fadeOutTime)
        {
            if (target == null || sr == null) yield break;
            
            elapsed += Time.deltaTime;
            float t = elapsed / fadeOutTime;

            // Fade alpha
            Color c = sr.color;
            c.a = Mathf.Lerp(0.5f, 0f, t);
            sr.color = c;

            // Slight shrink
            target.transform.localScale = Vector3.Lerp(originalScale, originalScale * 0.5f, t);

            yield return null;
        }

        // Destroy the captured ship after fade
        if (target != null)
            Destroy(target.gameObject);
    }

    System.Collections.IEnumerator DefeatVisualEffect(ShipController target)
    {
        if (target == null) yield break;
        
        SpriteRenderer sr = target.GetComponent<SpriteRenderer>();
        if (sr == null) yield break;

        Vector3 originalScale = target.transform.localScale;

        // Quick expand (impact)
        if (target == null) yield break;
        target.transform.localScale = originalScale * 1.3f;

        // Flash white
        if (target == null || sr == null) yield break;
        sr.color = Color.white;
        yield return new WaitForSeconds(0.05f);

        // Flash cyan (navy color)
        if (target == null || sr == null) yield break;
        sr.color = Color.cyan;
        yield return new WaitForSeconds(0.05f);

        // Flash orange (explosion)
        if (target == null || sr == null) yield break;
        sr.color = new Color(1f, 0.5f, 0f);
        target.transform.localScale = originalScale * 1.5f;
        yield return new WaitForSeconds(0.05f);

        // Flash bright yellow
        if (target == null || sr == null) yield break;
        sr.color = Color.yellow;
        yield return new WaitForSeconds(0.05f);

        // Flash red
        if (target == null || sr == null) yield break;
        sr.color = Color.red;
        yield return new WaitForSeconds(0.05f);

        // Shrink and fade rapidly
        float fadeTime = 0.4f;
        float elapsed = 0f;

        while (elapsed < fadeTime)
        {
            if (target == null || sr == null) yield break;
            
            elapsed += Time.deltaTime;
            float t = elapsed / fadeTime;

            // Shrink with slight spin
            target.transform.localScale = Vector3.Lerp(originalScale * 1.5f, Vector3.zero, t);
            target.transform.Rotate(0, 0, 720 * Time.deltaTime); // Spin as it shrinks

            // Fade through colors
            Color c = Color.Lerp(Color.red, new Color(0.2f, 0.2f, 0.2f, 0f), t);
            sr.color = c;

            yield return null;
        }

        // Optional: Spawn explosion particle
        if (explosionPrefab != null && target != null)
        {
            Instantiate(explosionPrefab, target.transform.position, Quaternion.identity);
        }

        // Destroy the defeated ship
        if (target != null)
            Destroy(target.gameObject);
    }

    void PulseCaptureEffect()
    {
        if (spriteRenderer == null) return;

        // Quick pulse effect
        float pulse = Mathf.Sin(Time.time * 15f);
        spriteRenderer.color = Color.Lerp(originalColor, Color.red, (pulse + 1f) / 2f);
    }

    void UpdateChaseLine()
    {
        if (chaseLineRenderer == null) return;

        if (currentTarget != null && (currentState == BehaviorState.Chasing || currentState == BehaviorState.Capturing))
        {
            chaseLineRenderer.enabled = true;
            chaseLineRenderer.SetPosition(0, transform.position);
            chaseLineRenderer.SetPosition(1, currentTarget.transform.position);

            // Color based on ship type and state
            if (controller.Data.type == ShipType.Security)
            {
                // Navy uses white/cyan lines
                if (currentState == BehaviorState.Capturing)
                {
                    chaseLineRenderer.startColor = Color.cyan;
                    chaseLineRenderer.endColor = Color.cyan;
                }
                else
                {
                    chaseLineRenderer.startColor = Color.white;
                    chaseLineRenderer.endColor = Color.cyan;
                }
            }
            else
            {
                // Pirates use yellow/red lines
                if (currentState == BehaviorState.Capturing)
                {
                    chaseLineRenderer.startColor = Color.red;
                    chaseLineRenderer.endColor = Color.red;
                }
                else
                {
                    chaseLineRenderer.startColor = Color.yellow;
                    chaseLineRenderer.endColor = Color.red;
                }
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
                // Flash when fleeing
                float flash = Mathf.Sin(Time.time * 10f) > 0 ? 1f : 0.6f;
                spriteRenderer.color = originalColor * flash;
                break;
            case BehaviorState.Capturing:
                // Handled by PulseCaptureEffect
                break;
            case BehaviorState.Chasing:
                spriteRenderer.color = originalColor * 1.2f;
                break;
            default:
                spriteRenderer.color = originalColor;
                break;
        }
    }

    // ===== HELPERS =====

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

    // ===== DEBUG GIZMOS =====

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
    }
#endif
}