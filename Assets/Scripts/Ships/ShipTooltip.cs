using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Ship Tooltip - Shows ship information on hover
/// 
/// Creates a tooltip that follows the mouse and displays
/// ship details when hovering over a ship.
/// 
/// SETUP:
/// 1. Add this to a GameObject in your scene
/// 2. It will auto-create the tooltip UI, OR
/// 3. Assign your own tooltip panel for custom styling
/// </summary>
public class ShipTooltip : MonoBehaviour
{
    public static ShipTooltip Instance { get; private set; }

    [Header("=== SETTINGS ===")]
    public Vector2 tooltipOffset = new Vector2(20f, -20f);
    public float hoverDelay = 0.1f;  // Seconds before tooltip appears
    public float hoverDistance = 3f;  // How close mouse must be to ship (world units)
    
    [Header("=== CUSTOM UI (Optional) ===")]
    public GameObject tooltipPanel;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI routeText;
    public TextMeshProUGUI stateText;
    public TextMeshProUGUI speedText;
    public TextMeshProUGUI extraText;
    public Image shipTypeIcon;

    [Header("=== COLORS ===")]
    public Color merchantColor = new Color(0.2f, 0.8f, 0.2f);   // Green
    public Color pirateColor = new Color(0.9f, 0.2f, 0.2f);     // Red
    public Color navyColor = new Color(0.2f, 0.5f, 0.9f);       // Blue
    public Color capturedColor = new Color(0.5f, 0.5f, 0.5f);   // Gray

    // Internal
    private Canvas tooltipCanvas;
    private RectTransform tooltipRect;
    private ShipController hoveredShip;
    private float hoverTimer = 0f;
    private bool isShowing = false;
    private Camera mainCam;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        mainCam = Camera.main;

        if (tooltipPanel == null)
        {
            CreateTooltipUI();
        }

        tooltipRect = tooltipPanel.GetComponent<RectTransform>();
        tooltipPanel.SetActive(false);
    }

    void Update()
    {
        CheckForShipUnderMouse();
        UpdateTooltipPosition();
        
        if (isShowing && hoveredShip != null)
        {
            UpdateTooltipContent();
        }
    }

    void CheckForShipUnderMouse()
    {
        Vector2 mouseWorldPos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        
        // Find nearest ship within hover distance
        ShipController nearestShip = null;
        float nearestDist = hoverDistance;

        ShipController[] allShips = FindObjectsOfType<ShipController>();
        
        foreach (var ship in allShips)
        {
            if (ship == null || ship.Data == null) continue;
            
            // Skip dead/exited ships
            if (ship.Data.state == ShipState.Sunk || 
                ship.Data.state == ShipState.Exited) continue;

            float dist = Vector2.Distance(mouseWorldPos, ship.Data.position);
            
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearestShip = ship;
            }
        }

        // Changed what we're hovering over
        if (nearestShip != hoveredShip)
        {
            hoveredShip = nearestShip;
            hoverTimer = 0f;

            if (hoveredShip == null)
            {
                HideTooltip();
            }
        }

        // Show tooltip after delay
        if (hoveredShip != null && !isShowing)
        {
            hoverTimer += Time.deltaTime;
            if (hoverTimer >= hoverDelay)
            {
                ShowTooltip();
            }
        }
    }

    void ShowTooltip()
    {
        if (hoveredShip == null) return;

        isShowing = true;
        tooltipPanel.SetActive(true);
        UpdateTooltipContent();
    }

    void HideTooltip()
    {
        isShowing = false;
        tooltipPanel.SetActive(false);
    }

    void UpdateTooltipPosition()
    {
        if (!isShowing || tooltipRect == null) return;

        Vector2 mousePos = Input.mousePosition;
        Vector2 tooltipPos = mousePos + tooltipOffset;

        // Keep tooltip on screen
        float rightEdge = tooltipPos.x + tooltipRect.sizeDelta.x;
        float bottomEdge = tooltipPos.y - tooltipRect.sizeDelta.y;

        if (rightEdge > Screen.width)
            tooltipPos.x = mousePos.x - tooltipOffset.x - tooltipRect.sizeDelta.x;
        
        if (bottomEdge < 0)
            tooltipPos.y = mousePos.y - tooltipOffset.y + tooltipRect.sizeDelta.y;

        tooltipRect.position = tooltipPos;
    }

    void UpdateTooltipContent()
    {
        if (hoveredShip == null || hoveredShip.Data == null) return;

        var data = hoveredShip.Data;
        ShipIdentity identity = hoveredShip.GetComponent<ShipIdentity>();
        ShipBehavior behavior = hoveredShip.GetComponent<ShipBehavior>();

        // Title - ship name or ID
        if (titleText != null)
        {
            if (identity != null && !string.IsNullOrEmpty(identity.fullTitle))
            {
                titleText.text = identity.fullTitle;
            }
            else
            {
                titleText.text = data.shipId;
            }

            // Color by type
            titleText.color = GetShipColor(data.type, data.state);
        }

        // Route
        if (routeText != null)
        {
            if (identity != null)
            {
                routeText.text = identity.GetRouteString();
            }
            else
            {
                routeText.text = GetShipTypeName(data.type);
            }
        }

        // State
        if (stateText != null)
        {
            string stateStr = GetStateString(data.state, behavior);
            stateText.text = $"Status: {stateStr}";
        }

        // Speed
        if (speedText != null)
        {
            speedText.text = $"Speed: {data.speedUnitsPerTick:F3} u/tick";
        }

        // Extra info based on situation
        if (extraText != null)
        {
            extraText.text = GetExtraInfo(data, behavior);
        }
    }

    string GetShipTypeName(ShipType type)
    {
        switch (type)
        {
            case ShipType.Cargo: return "Merchant Vessel";
            case ShipType.Pirate: return "Pirate Vessel";
            case ShipType.Security: return "Naval Patrol";
            default: return "Unknown";
        }
    }

    string GetStateString(ShipState state, ShipBehavior behavior)
    {
        switch (state)
        {
            case ShipState.Idle: return "Idle";
            case ShipState.Moving: return "Underway";
            case ShipState.Captured: return "⚠ CAPTURED";
            case ShipState.Sunk: return "✖ DESTROYED";
            case ShipState.Exited: return "✓ Escaped";
            default: return state.ToString();
        }
    }

    string GetExtraInfo(ShipData data, ShipBehavior behavior)
    {
        // Could show chase target, capture progress, etc.
        if (data.state == ShipState.Captured)
        {
            return "Boarded by pirates";
        }

        if (data.type == ShipType.Cargo)
        {
            return "Carrying valuable cargo";
        }
        else if (data.type == ShipType.Pirate)
        {
            return "Hostile - hunting merchants";
        }
        else if (data.type == ShipType.Security)
        {
            return "Protecting shipping lanes";
        }

        return "";
    }

    Color GetShipColor(ShipType type, ShipState state)
    {
        if (state == ShipState.Captured || state == ShipState.Sunk)
            return capturedColor;

        switch (type)
        {
            case ShipType.Cargo: return merchantColor;
            case ShipType.Pirate: return pirateColor;
            case ShipType.Security: return navyColor;
            default: return Color.white;
        }
    }

    void CreateTooltipUI()
    {
        // Create canvas for tooltip
        GameObject canvasObj = new GameObject("TooltipCanvas");
        tooltipCanvas = canvasObj.AddComponent<Canvas>();
        tooltipCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        tooltipCanvas.sortingOrder = 1000;  // On top of everything

        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // Create tooltip panel
        tooltipPanel = new GameObject("TooltipPanel");
        tooltipPanel.transform.SetParent(canvasObj.transform, false);

        RectTransform panelRect = tooltipPanel.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(280, 140);
        panelRect.pivot = new Vector2(0, 1);  // Top-left pivot

        // Background
        Image bg = tooltipPanel.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

        // Add outline
        Outline outline = tooltipPanel.AddComponent<Outline>();
        outline.effectColor = new Color(0.3f, 0.3f, 0.4f, 1f);
        outline.effectDistance = new Vector2(2, -2);

        // Vertical layout
        VerticalLayoutGroup layout = tooltipPanel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 10, 10);
        layout.spacing = 4;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        // Content size fitter
        ContentSizeFitter fitter = tooltipPanel.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Create text elements
        titleText = CreateTextElement(tooltipPanel.transform, "TitleText", 18, FontStyles.Bold);
        routeText = CreateTextElement(tooltipPanel.transform, "RouteText", 14, FontStyles.Italic);
        stateText = CreateTextElement(tooltipPanel.transform, "StateText", 14, FontStyles.Normal);
        speedText = CreateTextElement(tooltipPanel.transform, "SpeedText", 12, FontStyles.Normal);
        extraText = CreateTextElement(tooltipPanel.transform, "ExtraText", 11, FontStyles.Italic);
        extraText.color = new Color(0.7f, 0.7f, 0.7f);
    }

    TextMeshProUGUI CreateTextElement(Transform parent, string name, int fontSize, FontStyles style)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = Color.white;
        tmp.enableWordWrapping = true;

        // Set preferred height based on font size
        LayoutElement layoutElement = textObj.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = fontSize + 6;

        return tmp;
    }
}