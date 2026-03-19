using UnityEngine;
using UnityEngine.UI;
using TMPro;
using static MapManager;


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

    [Header("=== REAL WORLD DATA ===")]
    public bool showRealWorldFacts = true;
    public Color factColor = new Color(0.7f, 0.9f, 1f); // Light blue
    public Color merchantFactColor = new Color(0.7f, 0.9f, 1f);
    public Color pirateFactColor = new Color(1f, 0.7f, 0.7f);
    public Color navyFactColor = new Color(0.7f, 0.8f, 1f);

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
                routeText.text = identity.GetDetailedRouteString();
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

        // REAL WORLD FACTS - Replace the old extra info
        if (extraText != null && showRealWorldFacts)
        {
            extraText.text = GetRealWorldFact(data, identity);
            extraText.color = GetFactColor(data.type);
        }
    }

    private Color GetFactColor(ShipType type)
    {
        return type switch
        {
            ShipType.Cargo => merchantFactColor,
            ShipType.Pirate => pirateFactColor,
            ShipType.Security => navyFactColor,
            _ => Color.white
        };
    }

    private string GetRealWorldFact(ShipData data, ShipIdentity identity)
    {
        if (identity == null) return "";

        MapManager.MapRegion region = MapManager.MapRegion.StraitOfMalacca; // Default

        if (MapManager.Instance != null)
        {
            region = MapManager.Instance.CurrentRegion;
        }

        switch (data.type)
        {
            // ========== CARGO SHIPS ==========
            case ShipType.Cargo:
                switch (region)
                {
                    case MapManager.MapRegion.StraitOfMalacca:
                        return $"{RegionalData.Malacca.Stats.GetTrafficString()} • {RegionalData.Malacca.Stats.GetTradeString()}\n" +
                               $"Departing from: {identity.regionalPortName}\n" +
                               $"Carrying: {identity.regionalCargoType}\n" +
                               $"{RegionalData.Malacca.Stats.GetValueString()} annual trade";

                    case MapManager.MapRegion.GulfOfAden:
                        return $"{RegionalData.Aden.Stats.GetTrafficString()} • {RegionalData.Aden.Stats.GetTradeString()}\n" +
                               $"Departing from: {identity.regionalPortName}\n" +
                               $"Current risk: {RegionalData.Aden.Stats.currentRiskLevel}\n" +
                               $"NATO/EU patrols active";

                    case MapManager.MapRegion.GulfOfGuinea:
                        return $"{RegionalData.Guinea.Stats.GetTrafficString()} • {RegionalData.Guinea.Stats.GetTradeString()}\n" +
                               $"Departing from: {identity.regionalPortName}\n" +
                               $"HIGH RISK - Kidnapping hotspot\n" +
                               $"Carrying: {identity.regionalCargoType}";

                    default:
                        return "";
                }

            // ========== PIRATE SHIPS ==========
            case ShipType.Pirate:
                switch (region)
                {
                    case MapManager.MapRegion.StraitOfMalacca:
                        return $"Peak activity: 2004 ({RegionalData.Malacca.Stats.peakIncidents} attacks)\n" +
                               $"Operating near: {identity.regionalHotspotName}\n" +
                               $"{identity.regionalHotspotDescription}\n" +
                               $"Now suppressed by {RegionalData.Malacca.NavalForces[0].name}";

                    case MapManager.MapRegion.GulfOfAden:
                        return $"Peak Somali piracy: 2011 ({RegionalData.Aden.Stats.peakIncidents} attacks)\n" +
                               $"Base: {identity.regionalHotspotName}\n" +
                               $"{identity.regionalHotspotDescription}\n" +
                               $"Tactic: {identity.regionalTacticName}\n" +
                               $"$160M ransoms paid 2008-2012";

                    case MapManager.MapRegion.GulfOfGuinea:
                        return $"CURRENT GLOBAL HOTSPOT\n" +
                               $"Operating near: {identity.regionalHotspotName}\n" +
                               $"{identity.regionalHotspotDescription}\n" +
                               $"Tactic: {identity.regionalTacticName}\n" +
                               $"Kidnapping for ransom - 130+ crew in 2020";

                    default:
                        return "";
                }

            // ========== NAVY SHIPS ==========
            case ShipType.Security:
                switch (region)
                {
                    case MapManager.MapRegion.StraitOfMalacca:
                        return $"{identity.regionalNavyForce1}\n" +
                               $"{identity.regionalNavyForce2}\n" +
                               $"Protecting ports: {identity.regionalProtectedPort1}, {identity.regionalProtectedPort2}\n" +
                               $"3 nations cooperating";

                    case MapManager.MapRegion.GulfOfAden:
                        return $"{identity.regionalNavyForce1}\n" +
                               $"{identity.regionalNavyForce2}\n" +
                               $"25+ nations patrolling\n" +
                               $"Protecting {identity.regionalProtectedPort1}, {identity.regionalProtectedPort2} corridor";

                    case MapManager.MapRegion.GulfOfGuinea:
                        return $"{identity.regionalNavyForce1}\n" +
                               $"{identity.regionalNavyForce2}\n" +
                               $"Protecting {identity.regionalProtectedPort1}, {identity.regionalProtectedPort2}\n" +
                               $"HIGH THREAT AREA";

                    default:
                        return "";
                }

            default:
                return "";
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