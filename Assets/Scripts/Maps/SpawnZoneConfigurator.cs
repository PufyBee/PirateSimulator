using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// RUNTIME SPAWN ZONE CONFIGURATOR
/// 
/// Allows users to drag spawn zone markers on the map during setup!
/// 
/// FEATURES:
/// - Draggable markers for each ship type
/// - Visual zone rectangles showing spawn areas
/// - Arrow lines to destinations
/// - Snaps to valid water positions
/// - Presets load defaults, users can customize
/// - Locks when simulation starts
/// 
/// SETUP:
/// 1. Add this script to a GameObject in your scene
/// 2. It auto-creates all UI markers
/// 3. Assign to your setup flow
/// </summary>
public class SpawnZoneConfigurator : MonoBehaviour
{
    public static SpawnZoneConfigurator Instance { get; private set; }

    [Header("=== REFERENCES ===")]
    public ShipSpawner shipSpawner;
    public Camera mainCamera;
    public Canvas targetCanvas;

    [Header("=== MARKER SETTINGS ===")]
    public float markerSize = 40f;
    public float zoneOpacity = 0.3f;
    public bool showZoneRectangles = true;
    public bool showConnectionLines = true;
    public bool validateWaterOnly = true;

    [Header("=== COLORS ===")]
    public Color merchantColor = new Color(1f, 0.85f, 0.2f);
    public Color pirateColor = new Color(0.9f, 0.2f, 0.2f);
    public Color securityColor = new Color(0.2f, 0.6f, 1f);
    public Color destinationColor = new Color(0.2f, 0.9f, 0.3f);
    public Color invalidColor = new Color(1f, 0f, 0f, 0.5f);

    [Header("=== STATE ===")]
    public bool isLocked = false;

    private bool shouldBeVisible = true;
    private Dictionary<string, DraggableZoneMarker> markers = new Dictionary<string, DraggableZoneMarker>();
    private Dictionary<string, GameObject> zoneVisuals = new Dictionary<string, GameObject>();
    private Dictionary<string, LineRenderer> connectionLines = new Dictionary<string, LineRenderer>();
    private GameObject markerContainer;
    private GameObject zoneContainer;
    private bool isInitialized = false;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        Initialize();
        StartCoroutine(DelayedShowForSetup());
    }

    IEnumerator DelayedShowForSetup()
    {
        yield return null;
        
        float timeout = 2f;
        float elapsed = 0f;
        while (MapColorSampler.Instance == null && elapsed < timeout)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }
        
        ShowForSetup();
    }

    public void ShowForSetup()
    {
        Debug.Log("SpawnZoneConfigurator: ShowForSetup called");
        
        if (!isInitialized) Initialize();
        
        SyncFromSpawner();
        ValidateAllMarkerPositions();
        
        isLocked = false;
        SetVisible(true);
        SetMarkersInteractable(true);
        
        Debug.Log("SpawnZoneConfigurator: Zones now VISIBLE");
    }

    void ValidateAllMarkerPositions()
    {
        if (MapColorSampler.Instance == null)
        {
            Debug.LogWarning("SpawnZoneConfigurator: MapColorSampler not ready, skipping validation");
            return;
        }

        foreach (var kvp in markers)
        {
            string id = kvp.Key;
            DraggableZoneMarker marker = kvp.Value;
            
            if (marker == null) continue;
            
            Vector3 pos = marker.transform.position;
            
            if (!MapColorSampler.Instance.IsWater(pos))
            {
                Vector3 validPos = FindNearestWater(pos);
                marker.transform.position = validPos;
                Debug.Log($"SpawnZoneConfigurator: Moved {id} from land to water");
            }
        }
        
        SyncToSpawner();
    }

    void Initialize()
    {
        if (isInitialized) return;

        if (shipSpawner == null)
            shipSpawner = FindObjectOfType<ShipSpawner>();
        if (mainCamera == null)
            mainCamera = Camera.main;
        if (targetCanvas == null)
            targetCanvas = FindObjectOfType<Canvas>();

        markerContainer = new GameObject("SpawnZoneMarkers");
        markerContainer.transform.SetParent(transform);

        zoneContainer = new GameObject("SpawnZoneVisuals");
        zoneContainer.transform.SetParent(transform);

        CreateMarkerSet();
        CreateZoneVisuals();
        CreateConnectionLines();
        SyncFromSpawner();

        isInitialized = true;
    }

    void Update()
    {
        if (!isInitialized || !shouldBeVisible) return;
        UpdateZoneVisuals();
        UpdateConnectionLines();
    }

    #region === MARKER CREATION ===

    void CreateMarkerSet()
    {
        CreateMarker("MerchantSpawn", "M", merchantColor, "Merchant Spawn");
        CreateMarker("MerchantDest", "M→", destinationColor, "Merchant Destination");
        CreateMarker("PirateSpawn", "P", pirateColor, "Pirate Spawn");
        CreateMarker("PiratePatrol", "P⚔", new Color(pirateColor.r, pirateColor.g, pirateColor.b, 0.7f), "Pirate Patrol");
        CreateMarker("SecuritySpawn", "S", securityColor, "Security Spawn");
        CreateMarker("SecurityPatrol", "S⚔", new Color(securityColor.r, securityColor.g, securityColor.b, 0.7f), "Security Patrol");
    }

    void CreateMarker(string id, string label, Color color, string tooltip)
    {
        GameObject markerObj = new GameObject($"Marker_{id}");
        markerObj.transform.SetParent(markerContainer.transform);

        SpriteRenderer sr = markerObj.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite(64);
        sr.color = color;
        sr.sortingOrder = 100;

        float worldSize = markerSize / 10f;
        markerObj.transform.localScale = Vector3.one * worldSize;

        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(markerObj.transform);
        labelObj.transform.localPosition = Vector3.zero;

        TextMesh textMesh = labelObj.AddComponent<TextMesh>();
        textMesh.text = label;
        textMesh.fontSize = 32;
        textMesh.fontStyle = FontStyle.Bold;
        textMesh.color = Color.white;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        labelObj.transform.localScale = Vector3.one * 0.3f;

        MeshRenderer mr = labelObj.GetComponent<MeshRenderer>();
        mr.sortingOrder = 101;

        CircleCollider2D collider = markerObj.AddComponent<CircleCollider2D>();
        collider.radius = 0.5f;

        DraggableZoneMarker draggable = markerObj.AddComponent<DraggableZoneMarker>();
        draggable.markerId = id;
        draggable.configurator = this;
        draggable.tooltipText = tooltip;

        GameObject ring = new GameObject("Ring");
        ring.transform.SetParent(markerObj.transform);
        ring.transform.localPosition = Vector3.zero;
        ring.transform.localScale = Vector3.one * 1.2f;

        SpriteRenderer ringSr = ring.AddComponent<SpriteRenderer>();
        ringSr.sprite = CreateRingSprite(64);
        ringSr.color = Color.white;
        ringSr.sortingOrder = 99;

        markers[id] = draggable;
    }

    Sprite CreateCircleSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f - 2;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist <= radius)
                    tex.SetPixel(x, y, Color.white);
                else
                    tex.SetPixel(x, y, Color.clear);
            }
        }

        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    Sprite CreateRingSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float outerRadius = size / 2f - 2;
        float innerRadius = outerRadius - 3;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist <= outerRadius && dist >= innerRadius)
                    tex.SetPixel(x, y, Color.white);
                else
                    tex.SetPixel(x, y, Color.clear);
            }
        }

        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    #endregion

    #region === ZONE VISUALS ===

    void CreateZoneVisuals()
    {
        if (!showZoneRectangles) return;

        CreateZoneRect("MerchantSpawn", merchantColor);
        CreateZoneRect("PirateSpawn", pirateColor);
        CreateZoneRect("SecuritySpawn", securityColor);
        CreateZoneRect("MerchantDest", destinationColor);
        CreateZoneRect("PiratePatrol", pirateColor);
        CreateZoneRect("SecurityPatrol", securityColor);
    }

    void CreateZoneRect(string id, Color color)
    {
        GameObject zoneObj = new GameObject($"Zone_{id}");
        zoneObj.transform.SetParent(zoneContainer.transform);

        SpriteRenderer sr = zoneObj.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSquareSprite(32);
        sr.color = new Color(color.r, color.g, color.b, zoneOpacity);
        sr.sortingOrder = 50;
        sr.drawMode = SpriteDrawMode.Sliced;

        zoneVisuals[id] = zoneObj;
    }

    Sprite CreateSquareSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (x < 2 || x >= size - 2 || y < 2 || y >= size - 2)
                    tex.SetPixel(x, y, Color.white);
                else
                    tex.SetPixel(x, y, new Color(1, 1, 1, 0.5f));
            }
        }

        tex.Apply();
        tex.filterMode = FilterMode.Point;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect, new Vector4(4, 4, 4, 4));
    }

    void UpdateZoneVisuals()
    {
        if (!showZoneRectangles) return;

        if (markers.ContainsKey("MerchantSpawn") && zoneVisuals.ContainsKey("MerchantSpawn"))
            UpdateZoneRect("MerchantSpawn", markers["MerchantSpawn"].transform.position, GetZoneSize("MerchantSpawn"));

        if (markers.ContainsKey("PirateSpawn") && zoneVisuals.ContainsKey("PirateSpawn"))
            UpdateZoneRect("PirateSpawn", markers["PirateSpawn"].transform.position, GetZoneSize("PirateSpawn"));

        if (markers.ContainsKey("SecuritySpawn") && zoneVisuals.ContainsKey("SecuritySpawn"))
            UpdateZoneRect("SecuritySpawn", markers["SecuritySpawn"].transform.position, GetZoneSize("SecuritySpawn"));

        if (markers.ContainsKey("MerchantDest") && zoneVisuals.ContainsKey("MerchantDest"))
            UpdateZoneRect("MerchantDest", markers["MerchantDest"].transform.position, GetZoneSize("MerchantDest"));

        if (markers.ContainsKey("PiratePatrol") && zoneVisuals.ContainsKey("PiratePatrol"))
            UpdateZoneRect("PiratePatrol", markers["PiratePatrol"].transform.position, GetZoneSize("PiratePatrol"));

        if (markers.ContainsKey("SecurityPatrol") && zoneVisuals.ContainsKey("SecurityPatrol"))
            UpdateZoneRect("SecurityPatrol", markers["SecurityPatrol"].transform.position, GetZoneSize("SecurityPatrol"));
    }

    void UpdateZoneRect(string id, Vector3 center, Vector2 size)
    {
        if (!zoneVisuals.ContainsKey(id)) return;
        GameObject zone = zoneVisuals[id];
        zone.transform.position = new Vector3(center.x, center.y, 1);
        zone.transform.localScale = new Vector3(size.x, size.y, 1);
    }

    Vector2 GetZoneSize(string id)
    {
        if (shipSpawner == null) return new Vector2(20, 20);

        switch (id)
        {
            case "MerchantSpawn": return shipSpawner.merchantSpawnSize;
            case "PirateSpawn": return shipSpawner.pirateSpawnSize;
            case "SecuritySpawn": return shipSpawner.securitySpawnSize;
            case "MerchantDest": return new Vector2(10, 10);
            case "PiratePatrol": return new Vector2(30, 30);
            case "SecurityPatrol": return new Vector2(30, 30);
            default: return new Vector2(20, 20);
        }
    }

    #endregion

    #region === CONNECTION LINES ===

    void CreateConnectionLines()
    {
        if (!showConnectionLines) return;
        CreateLine("MerchantPath", merchantColor);
        CreateLine("PiratePath", pirateColor);
        CreateLine("SecurityPath", securityColor);
    }

    void CreateLine(string id, Color color)
    {
        GameObject lineObj = new GameObject($"Line_{id}");
        lineObj.transform.SetParent(zoneContainer.transform);

        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = color;
        lr.endColor = new Color(color.r, color.g, color.b, 0.3f);
        lr.startWidth = 1f;
        lr.endWidth = 0.5f;
        lr.positionCount = 2;
        lr.sortingOrder = 60;
        lr.useWorldSpace = true;

        connectionLines[id] = lr;
    }

    void UpdateConnectionLines()
    {
        if (!showConnectionLines) return;

        if (connectionLines.ContainsKey("MerchantPath") && markers.ContainsKey("MerchantSpawn") && markers.ContainsKey("MerchantDest"))
        {
            LineRenderer lr = connectionLines["MerchantPath"];
            lr.SetPosition(0, markers["MerchantSpawn"].transform.position);
            lr.SetPosition(1, markers["MerchantDest"].transform.position);
        }

        if (connectionLines.ContainsKey("PiratePath") && markers.ContainsKey("PirateSpawn") && markers.ContainsKey("PiratePatrol"))
        {
            LineRenderer lr = connectionLines["PiratePath"];
            lr.SetPosition(0, markers["PirateSpawn"].transform.position);
            lr.SetPosition(1, markers["PiratePatrol"].transform.position);
        }

        if (connectionLines.ContainsKey("SecurityPath") && markers.ContainsKey("SecuritySpawn") && markers.ContainsKey("SecurityPatrol"))
        {
            LineRenderer lr = connectionLines["SecurityPath"];
            lr.SetPosition(0, markers["SecuritySpawn"].transform.position);
            lr.SetPosition(1, markers["SecurityPatrol"].transform.position);
        }
    }

    #endregion

    #region === SYNC WITH SPAWNER ===

    public void SyncFromSpawner()
    {
        if (shipSpawner == null) return;

        SetMarkerPosition("MerchantSpawn", ClampToReasonableBounds(shipSpawner.merchantSpawnCenter));
        SetMarkerPosition("MerchantDest", ClampToReasonableBounds(shipSpawner.merchantDestination));
        SetMarkerPosition("PirateSpawn", ClampToReasonableBounds(shipSpawner.pirateSpawnCenter));
        SetMarkerPosition("PiratePatrol", ClampToReasonableBounds(shipSpawner.piratePatrolPoint));
        SetMarkerPosition("SecuritySpawn", ClampToReasonableBounds(shipSpawner.securitySpawnCenter));
        SetMarkerPosition("SecurityPatrol", ClampToReasonableBounds(shipSpawner.securityPatrolPoint));
    }

    Vector2 ClampToReasonableBounds(Vector2 pos)
    {
        float maxDist = 200f;
        
        if (mainCamera != null)
            maxDist = mainCamera.orthographicSize * 2f;
        
        if (pos.magnitude > maxDist * 2f)
        {
            Debug.LogWarning($"SpawnZoneConfigurator: Position {pos} too far, resetting to origin");
            return Vector2.zero;
        }
        
        pos.x = Mathf.Clamp(pos.x, -maxDist, maxDist);
        pos.y = Mathf.Clamp(pos.y, -maxDist, maxDist);
        
        return pos;
    }

    public void SyncToSpawner()
    {
        if (shipSpawner == null) return;

        if (markers.ContainsKey("MerchantSpawn"))
            shipSpawner.merchantSpawnCenter = (Vector2)markers["MerchantSpawn"].transform.position;
        if (markers.ContainsKey("MerchantDest"))
            shipSpawner.merchantDestination = (Vector2)markers["MerchantDest"].transform.position;
        if (markers.ContainsKey("PirateSpawn"))
            shipSpawner.pirateSpawnCenter = (Vector2)markers["PirateSpawn"].transform.position;
        if (markers.ContainsKey("PiratePatrol"))
            shipSpawner.piratePatrolPoint = (Vector2)markers["PiratePatrol"].transform.position;
        if (markers.ContainsKey("SecuritySpawn"))
            shipSpawner.securitySpawnCenter = (Vector2)markers["SecuritySpawn"].transform.position;
        if (markers.ContainsKey("SecurityPatrol"))
            shipSpawner.securityPatrolPoint = (Vector2)markers["SecurityPatrol"].transform.position;

        Debug.Log("SpawnZoneConfigurator: Synced to spawner");
    }

    void SetMarkerPosition(string id, Vector2 position)
    {
        if (markers.ContainsKey(id))
            markers[id].transform.position = new Vector3(position.x, position.y, 0);
    }

    #endregion

    #region === PUBLIC API ===

    public void OnMarkerMoved(string markerId, Vector3 newPosition)
    {
        if (isLocked) return;

        if (validateWaterOnly && MapColorSampler.Instance != null)
        {
            bool isValid = MapColorSampler.Instance.IsWater(newPosition);
            
            if (!isValid)
            {
                Vector3 validPos = FindNearestWater(newPosition);
                
                if (markers.ContainsKey(markerId))
                {
                    markers[markerId].transform.position = validPos;
                    StartCoroutine(FlashInvalid(markers[markerId]));
                }
                
                Debug.Log($"Cannot place {markerId} on land! Snapped to water.");
            }
        }

        SyncToSpawner();
    }

    Vector3 FindNearestWater(Vector3 landPos)
    {
        if (MapColorSampler.Instance == null) return landPos;

        float searchRadius = 5f;
        float maxRadius = 100f;
        int samples = 16;

        while (searchRadius < maxRadius)
        {
            for (int i = 0; i < samples; i++)
            {
                float angle = (i / (float)samples) * 360f * Mathf.Deg2Rad;
                Vector3 testPos = landPos + new Vector3(
                    Mathf.Cos(angle) * searchRadius,
                    Mathf.Sin(angle) * searchRadius,
                    0
                );

                if (MapColorSampler.Instance.IsWater(testPos))
                    return testPos;
            }
            searchRadius += 5f;
        }

        return landPos;
    }

    IEnumerator FlashInvalid(DraggableZoneMarker marker)
    {
        SpriteRenderer sr = marker.GetComponent<SpriteRenderer>();
        if (sr == null) yield break;

        Color originalColor = GetMarkerColor(marker.markerId);
        
        sr.color = invalidColor;
        yield return new WaitForSeconds(0.15f);
        sr.color = originalColor;
        yield return new WaitForSeconds(0.1f);
        sr.color = invalidColor;
        yield return new WaitForSeconds(0.15f);
        sr.color = originalColor;
    }

    Color GetMarkerColor(string id)
    {
        if (id.Contains("Merchant"))
            return id.Contains("Dest") ? destinationColor : merchantColor;
        if (id.Contains("Pirate"))
            return pirateColor;
        if (id.Contains("Security"))
            return securityColor;
        return Color.white;
    }

    public void Lock()
    {
        Debug.Log("SpawnZoneConfigurator: LOCK - Hiding zones");
        isLocked = true;
        SetVisible(false);
    }

    public void Unlock()
    {
        Debug.Log("SpawnZoneConfigurator: UNLOCK - Showing zones");
        isLocked = false;
        SetVisible(true);
        SetMarkersInteractable(true);
    }

    public void SetVisible(bool visible)
    {
        shouldBeVisible = visible;
        
        if (markerContainer != null)
            markerContainer.SetActive(visible);
        if (zoneContainer != null)
            zoneContainer.SetActive(visible);
        
        Debug.Log($"SpawnZoneConfigurator: SetVisible({visible})");
    }

    public void SetMinimalMode(bool minimal)
    {
        if (zoneContainer != null)
            zoneContainer.SetActive(!minimal);
    }

    void SetMarkersInteractable(bool interactable)
    {
        foreach (var marker in markers.Values)
        {
            marker.isInteractable = interactable;
            
            SpriteRenderer sr = marker.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Color c = sr.color;
                c.a = interactable ? 1f : 0.5f;
                sr.color = c;
            }
        }
    }

    public void ResetToDefaults()
    {
        var presets = FindObjectOfType<MapSpawnPresets>();
        if (presets != null && MapManager.Instance != null)
            presets.ApplyPreset(MapManager.Instance.GetCurrentMapIndex());

        SyncFromSpawner();
        Debug.Log("SpawnZoneConfigurator: Reset to defaults");
    }

    #endregion
}

/// <summary>
/// Makes a marker draggable with mouse
/// </summary>
public class DraggableZoneMarker : MonoBehaviour
{
    public string markerId;
    public SpawnZoneConfigurator configurator;
    public string tooltipText;
    public bool isInteractable = true;

    private bool isDragging = false;
    private Vector3 offset;
    private Camera cam;
    private SpriteRenderer spriteRenderer;
    private Vector3 originalScale;

    void Start()
    {
        cam = Camera.main;
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalScale = transform.localScale;
    }

    void OnMouseEnter()
    {
        if (!isInteractable) return;
        transform.localScale = originalScale * 1.2f;
    }

    void OnMouseExit()
    {
        if (!isDragging)
            transform.localScale = originalScale;
    }

    void OnMouseDown()
    {
        if (!isInteractable || configurator == null || configurator.isLocked) return;

        isDragging = true;
        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0;
        offset = transform.position - mouseWorld;
        transform.localScale = originalScale * 1.3f;
    }

    void OnMouseDrag()
    {
        if (!isDragging || !isInteractable) return;

        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0;
        transform.position = mouseWorld + offset;
    }

    void OnMouseUp()
    {
        if (!isDragging) return;

        isDragging = false;
        transform.localScale = originalScale;

        if (configurator != null)
            configurator.OnMarkerMoved(markerId, transform.position);
    }
}