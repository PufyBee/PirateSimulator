using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// COASTAL DEFENSE SYSTEM - Land-based missile batteries
/// 
/// Features:
/// - Auto-targets pirates within range
/// - Visual missile with trail
/// - Lock-on warning indicator
/// - Explosion on impact
/// - Cooldown between shots
/// - Multiple batteries supported
/// 
/// SETUP:
/// 1. Create empty GameObject on coastline
/// 2. Add this component
/// 3. Adjust range and cooldown
/// 4. Assign ShipSpawner reference (or leave empty for auto-find)
/// </summary>
public class CoastalDefense : MonoBehaviour
{
    [Header("=== TARGETING ===")]
    [Tooltip("Range at which battery can detect and fire at pirates")]
    public float firingRange = 150f;
    
    [Tooltip("Time between shots in seconds")]
    public float cooldownTime = 3f;
    
    [Tooltip("Time to lock on before firing")]
    public float lockOnTime = 0.8f;

    [Header("=== MISSILE SETTINGS ===")]
    [Tooltip("How fast the missile travels")]
    public float missileSpeed = 20f;
    
    [Tooltip("Missile size")]
    public float missileScale = 0.5f;

    [Header("=== VISUALS ===")]
    [Tooltip("Color of the missile")]
    public Color missileColor = new Color(1f, 0.3f, 0f); // Orange
    
    [Tooltip("Color of the trail")]
    public Color trailColor = new Color(1f, 0.6f, 0.2f); // Light orange
    
    [Tooltip("Color of lock-on indicator")]
    public Color lockOnColor = Color.red;

    [Header("=== REFERENCES ===")]
    public ShipSpawner shipSpawner;

    [Header("=== DEBUG ===")]
    public bool showRangeGizmo = true;
    public bool enableLogs = true;  // ON by default now

    // State
    private float cooldownRemaining = 0f;
    private ShipController currentTarget;
    private bool isLockedOn = false;
    private float lockOnProgress = 0f;
    private GameObject lockOnIndicator;
    private LineRenderer lockOnLine;

    // Static tracking for all batteries
    private static List<CoastalDefense> allBatteries = new List<CoastalDefense>();

    void Awake()
    {
        if (shipSpawner == null)
            shipSpawner = FindObjectOfType<ShipSpawner>();

        SetupLockOnVisuals();
        allBatteries.Add(this);
    }

    void OnDestroy()
    {
        allBatteries.Remove(this);
    }

    void SetupLockOnVisuals()
    {
        // Create lock-on line renderer
        GameObject lineObj = new GameObject("LockOnLine");
        lineObj.transform.SetParent(transform);
        lockOnLine = lineObj.AddComponent<LineRenderer>();
        lockOnLine.startWidth = 0.05f;
        lockOnLine.endWidth = 0.02f;
        lockOnLine.material = new Material(Shader.Find("Sprites/Default"));
        lockOnLine.startColor = lockOnColor;
        lockOnLine.endColor = new Color(lockOnColor.r, lockOnColor.g, lockOnColor.b, 0.3f);
        lockOnLine.positionCount = 2;
        lockOnLine.enabled = false;

        // Create lock-on indicator (pulsing circle on target)
        lockOnIndicator = new GameObject("LockOnIndicator");
        lockOnIndicator.transform.SetParent(transform);
        SpriteRenderer sr = lockOnIndicator.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color = lockOnColor;
        sr.sortingOrder = 100;
        lockOnIndicator.transform.localScale = Vector3.one * 0.5f;
        lockOnIndicator.SetActive(false);
    }

    void Update()
    {
        // Debug - check if this is running
        if (enableLogs && Time.frameCount % 120 == 0)
        {
            Debug.Log($"CoastalDefense UPDATE running. Cooldown: {cooldownRemaining:F1}, Target: {(currentTarget != null ? currentTarget.Data?.shipId : "none")}");
        }

        // Cooldown
        if (cooldownRemaining > 0)
        {
            cooldownRemaining -= Time.deltaTime;
        }

        // Find target if we don't have one
        if (currentTarget == null || !IsValidTarget(currentTarget))
        {
            currentTarget = FindBestTarget();
            isLockedOn = false;
            lockOnProgress = 0f;
            if (lockOnLine != null) lockOnLine.enabled = false;
            if (lockOnIndicator != null) lockOnIndicator.SetActive(false);
        }

        // Lock-on process
        if (currentTarget != null && cooldownRemaining <= 0)
        {
            if (enableLogs)
                Debug.Log($"CoastalDefense: LOCKING ON to {currentTarget.Data.shipId}");
            UpdateLockOn();
        }
    }

    ShipController FindBestTarget()
    {
        // Auto-find shipSpawner if not set
        if (shipSpawner == null)
        {
            shipSpawner = ShipSpawner.Instance;
            if (shipSpawner == null)
                shipSpawner = FindObjectOfType<ShipSpawner>();
        }

        if (shipSpawner == null)
        {
            if (enableLogs)
                Debug.LogWarning("CoastalDefense: No ShipSpawner found!");
            return null;
        }

        ShipController nearest = null;
        float nearestDist = float.MaxValue;

        var activeShips = shipSpawner.GetActiveShips();
        
        int pirateCount = 0;
        foreach (var ship in activeShips)
        {
            if (ship == null || ship.Data == null) continue;
            
            // Log ALL ships
            float dist = Vector2.Distance(transform.position, ship.Data.position);
            
            if (ship.Data.type == ShipType.Pirate)
            {
                pirateCount++;
                Debug.Log($"CoastalDefense: PIRATE '{ship.Data.shipId}' at dist {dist:F1}, state={ship.Data.state}, range={firingRange}, inRange={dist <= firingRange}");
            }

            if (!IsValidTarget(ship)) continue;

            if (dist <= firingRange && dist < nearestDist)
            {
                // Check if another battery is already targeting this pirate
                if (!IsTargetedByOtherBattery(ship))
                {
                    nearestDist = dist;
                    nearest = ship;
                }
            }
        }
        
        if (enableLogs)
            Debug.Log($"CoastalDefense: Found {pirateCount} pirates out of {activeShips.Count} ships. Best target: {(nearest != null ? nearest.Data.shipId : "NONE")}");

        return nearest;
    }

    bool IsValidTarget(ShipController ship)
    {
        if (ship == null || ship.Data == null) return false;
        if (ship.Data.type != ShipType.Pirate) return false;
        if (ship.Data.state == ShipState.Sunk || ship.Data.state == ShipState.Captured) return false;
        
        float dist = Vector2.Distance(transform.position, ship.Data.position);
        return dist <= firingRange;
    }

    bool IsTargetedByOtherBattery(ShipController ship)
    {
        foreach (var battery in allBatteries)
        {
            if (battery == this) continue;
            if (battery.currentTarget == ship && battery.isLockedOn) return true;
        }
        return false;
    }

    void UpdateLockOn()
    {
        // Show lock-on line
        lockOnLine.enabled = true;
        lockOnLine.SetPosition(0, transform.position);
        lockOnLine.SetPosition(1, currentTarget.transform.position);

        // Show and pulse lock-on indicator
        lockOnIndicator.SetActive(true);
        lockOnIndicator.transform.position = currentTarget.transform.position;
        
        // Pulse effect
        float pulse = 1f + Mathf.Sin(Time.time * 15f) * 0.3f;
        lockOnIndicator.transform.localScale = Vector3.one * 0.5f * pulse;
        
        // Flash the indicator color
        SpriteRenderer sr = lockOnIndicator.GetComponent<SpriteRenderer>();
        sr.color = Color.Lerp(lockOnColor, Color.white, (Mathf.Sin(Time.time * 20f) + 1f) / 2f);

        // Progress lock-on
        lockOnProgress += Time.deltaTime;

        // Blink the lock-on line faster as we get closer to firing
        float blinkRate = Mathf.Lerp(5f, 30f, lockOnProgress / lockOnTime);
        lockOnLine.enabled = Mathf.Sin(Time.time * blinkRate) > 0;

        if (lockOnProgress >= lockOnTime)
        {
            // FIRE!
            FireMissile(currentTarget);
            cooldownRemaining = cooldownTime;
            isLockedOn = false;
            lockOnProgress = 0f;
            currentTarget = null;
            lockOnLine.enabled = false;
            lockOnIndicator.SetActive(false);
        }
        else
        {
            isLockedOn = true;
        }
    }

    void FireMissile(ShipController target)
    {
        if (enableLogs)
            Debug.Log($"COASTAL DEFENSE: Firing missile at {target.Data.shipId}!");

        StartCoroutine(MissileSequence(target));
    }

    IEnumerator MissileSequence(ShipController target)
    {
        // Create missile
        GameObject missile = new GameObject("Missile");
        missile.transform.position = transform.position;

        // Missile sprite (triangle/arrow shape)
        SpriteRenderer missileSR = missile.AddComponent<SpriteRenderer>();
        missileSR.sprite = CreateMissileSprite();
        missileSR.color = missileColor;
        missileSR.sortingOrder = 150;
        missile.transform.localScale = Vector3.one * missileScale;

        // Missile trail
        TrailRenderer trail = missile.AddComponent<TrailRenderer>();
        trail.time = 0.3f;
        trail.startWidth = 0.15f;
        trail.endWidth = 0f;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.startColor = trailColor;
        trail.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);

        // Muzzle flash at battery
        StartCoroutine(MuzzleFlash());

        // Fly toward target - track continuously
        float maxFlightTime = 10f; // Safety timeout
        float flightTime = 0f;

        while (flightTime < maxFlightTime)
        {
            flightTime += Time.deltaTime;

            // Get current target position
            Vector3 targetPos;
            if (target != null && target.Data != null && target.Data.state != ShipState.Sunk)
            {
                targetPos = target.transform.position;
            }
            else
            {
                // Target gone, just explode where we are
                break;
            }

            // Move missile toward target
            Vector3 direction = (targetPos - missile.transform.position).normalized;
            missile.transform.position += direction * missileSpeed * Time.deltaTime;
            
            // Rotate to face direction
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            missile.transform.rotation = Quaternion.Euler(0, 0, angle - 90f);

            // Check if we've reached target - use smaller threshold
            float distanceToTarget = Vector3.Distance(missile.transform.position, targetPos);
            if (distanceToTarget < 0.5f)
            {
                // HIT!
                break;
            }

            yield return null;
        }

        // IMPACT!
        Vector3 impactPos = missile.transform.position;
        Destroy(missile);

        // IMMEDIATELY disable the pirate so it stops moving
        if (target != null && target.Data != null && target.Data.state != ShipState.Sunk)
        {
            // Stop it NOW
            target.Data.state = ShipState.Sunk;
            target.SetState(ShipState.Sunk);
            
            if (enableLogs)
                Debug.Log($"COASTAL DEFENSE: Pirate {target.Data.shipId} HIT! Stopping movement.");
        }

        // Explosion effect (plays while pirate is already stopped)
        yield return StartCoroutine(ExplosionEffect(impactPos));

        // Now do the visual death effect on the stopped pirate
        if (target != null)
        {
            StartCoroutine(DefeatEffect(target));
        }
    }

    IEnumerator MuzzleFlash()
    {
        GameObject flash = new GameObject("MuzzleFlash");
        flash.transform.position = transform.position;

        SpriteRenderer sr = flash.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color = Color.yellow;
        sr.sortingOrder = 140;
        flash.transform.localScale = Vector3.one * 0.8f;

        // Quick expand and fade
        float duration = 0.15f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            flash.transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.5f, t);
            sr.color = new Color(1f, 1f, 0f, 1f - t);

            yield return null;
        }

        Destroy(flash);
    }

    IEnumerator ExplosionEffect(Vector3 position)
    {
        // Create multiple explosion layers for epic effect

        // Layer 1: White flash
        GameObject flash = CreateExplosionLayer(position, Color.white, 0.5f, 145);
        
        // Layer 2: Yellow core
        GameObject core = CreateExplosionLayer(position, Color.yellow, 0.3f, 146);
        
        // Layer 3: Orange expansion
        GameObject orange = CreateExplosionLayer(position, new Color(1f, 0.5f, 0f), 0.4f, 147);
        
        // Layer 4: Red outer
        GameObject red = CreateExplosionLayer(position, Color.red, 0.2f, 148);

        // Animate explosion
        float duration = 0.4f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Expand all layers at different rates
            if (flash != null)
            {
                flash.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 2f, t);
                flash.GetComponent<SpriteRenderer>().color = new Color(1f, 1f, 1f, 1f - t);
            }
            if (core != null)
            {
                core.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.5f, t);
                core.GetComponent<SpriteRenderer>().color = new Color(1f, 1f, 0f, 1f - t * 0.8f);
            }
            if (orange != null)
            {
                orange.transform.localScale = Vector3.one * Mathf.Lerp(0.4f, 2.5f, t);
                orange.GetComponent<SpriteRenderer>().color = new Color(1f, 0.5f, 0f, 1f - t);
            }
            if (red != null)
            {
                red.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 3f, t);
                red.GetComponent<SpriteRenderer>().color = new Color(1f, 0f, 0f, 0.8f - t);
            }

            yield return null;
        }

        // Cleanup
        if (flash != null) Destroy(flash);
        if (core != null) Destroy(core);
        if (orange != null) Destroy(orange);
        if (red != null) Destroy(red);

        // Smoke ring
        yield return StartCoroutine(SmokeRingEffect(position));
    }

    IEnumerator SmokeRingEffect(Vector3 position)
    {
        GameObject smoke = new GameObject("SmokeRing");
        smoke.transform.position = position;

        SpriteRenderer sr = smoke.AddComponent<SpriteRenderer>();
        sr.sprite = CreateRingSprite();
        sr.color = new Color(0.3f, 0.3f, 0.3f, 0.6f);
        sr.sortingOrder = 144;
        smoke.transform.localScale = Vector3.one * 0.5f;

        float duration = 0.8f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            smoke.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 3f, t);
            sr.color = new Color(0.3f, 0.3f, 0.3f, 0.6f * (1f - t));

            yield return null;
        }

        Destroy(smoke);
    }

    IEnumerator DefeatEffect(ShipController target)
    {
        if (target == null) yield break;

        SpriteRenderer sr = target.GetComponent<SpriteRenderer>();
        if (sr == null) yield break;

        Vector3 originalScale = target.transform.localScale;

        // Quick flash sequence
        sr.color = Color.white;
        yield return new WaitForSeconds(0.05f);

        if (target == null) yield break;
        sr.color = Color.yellow;
        target.transform.localScale = originalScale * 1.3f;
        yield return new WaitForSeconds(0.05f);

        if (target == null) yield break;
        sr.color = new Color(1f, 0.5f, 0f);
        target.transform.localScale = originalScale * 1.5f;
        yield return new WaitForSeconds(0.05f);

        // Shrink and fade
        float fadeTime = 0.3f;
        float elapsed = 0f;

        while (elapsed < fadeTime)
        {
            if (target == null) yield break;

            elapsed += Time.deltaTime;
            float t = elapsed / fadeTime;

            target.transform.localScale = Vector3.Lerp(originalScale * 1.5f, Vector3.zero, t);
            sr.color = Color.Lerp(Color.red, new Color(0.2f, 0.2f, 0.2f, 0f), t);

            yield return null;
        }

        if (target != null)
            Destroy(target.gameObject);
    }

    GameObject CreateExplosionLayer(Vector3 position, Color color, float scale, int sortOrder)
    {
        GameObject obj = new GameObject("ExplosionLayer");
        obj.transform.position = position;
        obj.transform.localScale = Vector3.one * scale;

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color = color;
        sr.sortingOrder = sortOrder;

        return obj;
    }

    // ===== SPRITE GENERATION =====

    Sprite CreateCircleSprite()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];

        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f - 1;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist <= radius)
                {
                    float alpha = 1f - (dist / radius) * 0.3f; // Slight gradient
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
                else
                {
                    pixels[y * size + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    Sprite CreateMissileSprite()
    {
        int width = 16;
        int height = 32;
        Texture2D tex = new Texture2D(width, height);
        Color[] pixels = new Color[width * height];

        // Clear
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.clear;

        // Draw triangle (pointing up)
        for (int y = 0; y < height; y++)
        {
            float widthAtY = (1f - (float)y / height) * width;
            int startX = (int)((width - widthAtY) / 2);
            int endX = (int)((width + widthAtY) / 2);

            for (int x = startX; x < endX; x++)
            {
                if (x >= 0 && x < width)
                    pixels[y * width + x] = Color.white;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), height);
    }

    Sprite CreateRingSprite()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];

        Vector2 center = new Vector2(size / 2f, size / 2f);
        float outerRadius = size / 2f - 1;
        float innerRadius = outerRadius * 0.7f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist <= outerRadius && dist >= innerRadius)
                {
                    pixels[y * size + x] = Color.white;
                }
                else
                {
                    pixels[y * size + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    // ===== PUBLIC METHODS =====

    /// <summary>
    /// Reset all batteries (call on simulation reset)
    /// </summary>
    public static void ResetAllBatteries()
    {
        foreach (var battery in allBatteries)
        {
            battery.cooldownRemaining = 0f;
            battery.currentTarget = null;
            battery.isLockedOn = false;
            battery.lockOnProgress = 0f;
            if (battery.lockOnLine != null)
                battery.lockOnLine.enabled = false;
            if (battery.lockOnIndicator != null)
                battery.lockOnIndicator.SetActive(false);
        }
    }

    // ===== GIZMOS =====

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showRangeGizmo) return;

        // Firing range
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, firingRange);

        // Battery position indicator
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.8f);
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.3f);

        // Draw line to current target
        if (currentTarget != null)
        {
            Gizmos.color = isLockedOn ? Color.red : Color.yellow;
            Gizmos.DrawLine(transform.position, currentTarget.transform.position);
        }
    }

    void OnDrawGizmosSelected()
    {
        // Show range more prominently when selected
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.4f);
        Gizmos.DrawSphere(transform.position, firingRange);
    }
#endif
}