using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class RouteWeightPanel : MonoBehaviour
{
    public static RouteWeightPanel Instance { get; private set; }

    private GameObject canvasObject;
    private GameObject toggleButton;
    private GameObject expandedPanel;
    private TMP_Text toggleText;
    private bool isExpanded = false;
    private bool hasPopulated = false;
    private List<SliderEntry> sliderEntries = new List<SliderEntry>();
    private Dictionary<TradeRouteData, int> originalWeights = new Dictionary<TradeRouteData, int>();
    private List<GameObject> routeLineObjects = new List<GameObject>();

    private const float ROW_HEIGHT = 40f;
    private const float PANEL_WIDTH = 260f;
    private const float SLIDER_H = 16f;
    private const float BUTTON_X = 75f;
    private const float BUTTON_Y = -20f;
    private const float PANEL_TOP_Y = -58f;

    private static readonly Color[] ROUTE_COLORS = new Color[]
    {
        new Color(0.2f, 0.8f, 0.9f),
        new Color(0.9f, 0.4f, 0.2f),
        new Color(0.3f, 0.9f, 0.4f),
        new Color(0.9f, 0.9f, 0.2f),
        new Color(0.9f, 0.2f, 0.6f),
        new Color(0.5f, 0.4f, 0.9f),
        new Color(0.2f, 0.6f, 0.9f),
        new Color(0.9f, 0.7f, 0.2f),
        new Color(0.4f, 0.9f, 0.8f),
        new Color(0.9f, 0.3f, 0.3f),
        new Color(0.6f, 0.9f, 0.2f),
        new Color(0.8f, 0.5f, 0.9f),
    };

    private class SliderEntry
    {
        public TradeRouteData routeData;
        public Slider slider;
        public TMP_Text valueLabel;
        public Image colorDot;
        public GameObject row;
        public GameObject lineObject;
        public LineRenderer lineRenderer;
        public Color routeColor;
    }

    void Awake() { Instance = this; }
    void Start() { CreateToggleButton(); }

    void Update()
    {
        if (!hasPopulated && TradeRouteManager.Instance != null && TradeRouteManager.Instance.CurrentMapIndex >= 0)
        {
            RefreshForCurrentMap();
            hasPopulated = true;
        }
    }

    public void RefreshForCurrentMap()
    {
        MapTradeRoutes mapRoutes = GetCurrentMapRoutes();
        if (mapRoutes == null) return;

        ClearAll();

        foreach (var route in mapRoutes.tradeRoutes)
        {
            if (route != null && !originalWeights.ContainsKey(route))
                originalWeights[route] = route.trafficWeight;
        }

        if (expandedPanel != null)
            Destroy(expandedPanel);

        int routeCount = 0;
        foreach (var route in mapRoutes.tradeRoutes)
            if (route != null) routeCount++;

        float panelHeight = Mathf.Min(routeCount * ROW_HEIGHT + 10f, 450f);

        expandedPanel = new GameObject("ExpandedPanel");
        expandedPanel.transform.SetParent(canvasObject.transform, false);

        RectTransform panelRect = expandedPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 1);
        panelRect.anchorMax = new Vector2(0, 1);
        panelRect.pivot = new Vector2(0, 1);
        panelRect.anchoredPosition = new Vector2(BUTTON_X, PANEL_TOP_Y);
        panelRect.sizeDelta = new Vector2(PANEL_WIDTH, panelHeight);

        Image panelBg = expandedPanel.AddComponent<Image>();
        panelBg.color = new Color(0.06f, 0.08f, 0.12f, 0.95f);

        Outline panelOutline = expandedPanel.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.11f, 0.45f, 0.58f, 0.3f);
        panelOutline.effectDistance = new Vector2(1, -1);

        int index = 0;
        foreach (var route in mapRoutes.tradeRoutes)
        {
            if (route == null) continue;
            Color routeColor = GetRouteColor(route, index);
            CreateRow(route, index, routeColor);
            CreateRouteLine(route, index, routeColor);
            index++;
        }

        expandedPanel.SetActive(false);
        HideAllRouteLines();

        if (isExpanded)
        {
            isExpanded = false;
            toggleText.text = "> Trade Routes";
        }
    }

    public void Show()
    {
        if (canvasObject != null) canvasObject.SetActive(true);
    }

    public void Hide()
    {
        if (canvasObject != null) canvasObject.SetActive(false);
        if (isExpanded)
        {
            isExpanded = false;
            HideAllRouteLines();
        }
    }

    public void RestoreOriginalWeights()
    {
        foreach (var kvp in originalWeights)
            if (kvp.Key != null) kvp.Key.trafficWeight = kvp.Value;

        foreach (var entry in sliderEntries)
        {
            if (entry.routeData != null && entry.slider != null)
            {
                entry.slider.value = entry.routeData.trafficWeight;
                UpdateValueDisplay(entry);
            }
        }
    }

    private void CreateToggleButton()
    {
        canvasObject = new GameObject("RouteWeightCanvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 55;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObject.AddComponent<GraphicRaycaster>();

        toggleButton = new GameObject("ToggleButton");
        toggleButton.transform.SetParent(canvasObject.transform, false);

        RectTransform toggleRect = toggleButton.AddComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(0, 1);
        toggleRect.anchorMax = new Vector2(0, 1);
        toggleRect.pivot = new Vector2(0, 1);
        toggleRect.anchoredPosition = new Vector2(BUTTON_X, BUTTON_Y);
        toggleRect.sizeDelta = new Vector2(200, 36);

        Image toggleBg = toggleButton.AddComponent<Image>();
        toggleBg.color = new Color(0.12f, 0.18f, 0.25f, 0.95f);

        Button toggleBtn = toggleButton.AddComponent<Button>();
        toggleBtn.targetGraphic = toggleBg;
        toggleBtn.onClick.AddListener(TogglePanel);

        Outline btnOutline = toggleButton.AddComponent<Outline>();
        btnOutline.effectColor = new Color(0.11f, 0.45f, 0.58f, 0.5f);
        btnOutline.effectDistance = new Vector2(1, -1);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(toggleButton.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10, 0);
        textRect.offsetMax = new Vector2(-10, 0);

        toggleText = textObj.AddComponent<TextMeshProUGUI>();
        toggleText.text = "> Trade Routes";
        toggleText.fontSize = 14;
        toggleText.fontStyle = FontStyles.Bold;
        toggleText.color = new Color(0.11f, 0.45f, 0.58f);
        toggleText.alignment = TextAlignmentOptions.Left;
    }

    private void CreateRouteLine(TradeRouteData route, int index, Color color)
    {
        if (route.waypoints == null || route.waypoints.Count < 2) return;

        GameObject lineObj = new GameObject($"RouteLine_{route.routeName}");
        lineObj.transform.SetParent(transform, false);

        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.positionCount = route.waypoints.Count;
        lr.startWidth = 3f;
        lr.endWidth = 3f;
        lr.useWorldSpace = true;
        lr.sortingOrder = 5;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = color;
        lr.endColor = color;

        for (int i = 0; i < route.waypoints.Count; i++)
        {
            Vector2 wp = route.waypoints[i];
            lr.SetPosition(i, new Vector3(wp.x, wp.y, -1f));
        }

        lineObj.SetActive(false);
        routeLineObjects.Add(lineObj);

        if (index < sliderEntries.Count)
        {
            sliderEntries[index].lineObject = lineObj;
            sliderEntries[index].lineRenderer = lr;
        }
    }

    private Color GetRouteColor(TradeRouteData route, int index)
    {
        if (route.debugColor != Color.cyan)
            return route.debugColor;
        return ROUTE_COLORS[index % ROUTE_COLORS.Length];
    }

    private void ShowAllRouteLines()
    {
        foreach (var entry in sliderEntries)
        {
            if (entry.lineObject != null)
            {
                entry.lineObject.SetActive(true);
                UpdateRouteLine(entry);
            }
        }
    }

    private void HideAllRouteLines()
    {
        foreach (var obj in routeLineObjects)
            if (obj != null) obj.SetActive(false);
    }

    private void UpdateRouteLine(SliderEntry entry)
    {
        if (entry.lineObject == null || entry.lineRenderer == null) return;

        if (entry.routeData.trafficWeight == 0)
        {
            entry.lineObject.SetActive(true);
            Color c = entry.routeColor;
            c.a = 0.1f;
            entry.lineRenderer.startColor = c;
            entry.lineRenderer.endColor = c;
            entry.lineRenderer.startWidth = 1.5f;
            entry.lineRenderer.endWidth = 1.5f;
        }
        else
        {
            entry.lineObject.SetActive(true);
            Color c = entry.routeColor;
            c.a = Mathf.Lerp(0.4f, 1f, entry.routeData.trafficWeight / 100f);
            entry.lineRenderer.startColor = c;
            entry.lineRenderer.endColor = c;
            float width = Mathf.Lerp(2f, 5f, entry.routeData.trafficWeight / 100f);
            entry.lineRenderer.startWidth = width;
            entry.lineRenderer.endWidth = width;
        }
    }

    private void CreateRow(TradeRouteData route, int index, Color routeColor)
    {
        float yPos = -5f - (index * ROW_HEIGHT);
        float rowWidth = PANEL_WIDTH - 12f;

        GameObject row = new GameObject($"Row_{route.routeName}");
        row.transform.SetParent(expandedPanel.transform, false);

        RectTransform rowRect = row.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0, 1);
        rowRect.anchorMax = new Vector2(0, 1);
        rowRect.pivot = new Vector2(0, 1);
        rowRect.anchoredPosition = new Vector2(6, yPos);
        rowRect.sizeDelta = new Vector2(rowWidth, ROW_HEIGHT - 3f);

        Image rowBg = row.AddComponent<Image>();
        rowBg.color = new Color(0.09f, 0.11f, 0.15f, 0.8f);

        // Color dot (left side)
        GameObject dotObj = new GameObject("ColorDot");
        dotObj.transform.SetParent(row.transform, false);

        RectTransform dotRect = dotObj.AddComponent<RectTransform>();
        dotRect.anchorMin = new Vector2(0, 0.5f);
        dotRect.anchorMax = new Vector2(0, 0.5f);
        dotRect.pivot = new Vector2(0, 0.5f);
        dotRect.anchoredPosition = new Vector2(6, 4);
        dotRect.sizeDelta = new Vector2(12, 12);

        Image dotImg = dotObj.AddComponent<Image>();
        dotImg.color = routeColor;

        // Value (right side)
        GameObject valObj = new GameObject("Value");
        valObj.transform.SetParent(row.transform, false);

        RectTransform valRect = valObj.AddComponent<RectTransform>();
        valRect.anchorMin = new Vector2(1, 0.5f);
        valRect.anchorMax = new Vector2(1, 1f);
        valRect.pivot = new Vector2(1, 1);
        valRect.anchoredPosition = new Vector2(-8, 0);
        valRect.sizeDelta = new Vector2(40, 18);

        TMP_Text valText = valObj.AddComponent<TextMeshProUGUI>();
        valText.text = route.trafficWeight.ToString();
        valText.fontSize = 13;
        valText.fontStyle = FontStyles.Bold;
        valText.color = new Color(0.83f, 0.64f, 0.3f);
        valText.alignment = TextAlignmentOptions.Right;
        valText.enableAutoSizing = false;
        valText.raycastTarget = false;

        // Slider (bottom portion of row)
        GameObject sliderBg = new GameObject("SliderBg");
        sliderBg.transform.SetParent(row.transform, false);

        RectTransform sliderBgRect = sliderBg.AddComponent<RectTransform>();
        sliderBgRect.anchorMin = new Vector2(0, 0);
        sliderBgRect.anchorMax = new Vector2(1, 0);
        sliderBgRect.pivot = new Vector2(0, 0);
        sliderBgRect.anchoredPosition = new Vector2(24, 4);
        sliderBgRect.sizeDelta = new Vector2(rowWidth - 40f, SLIDER_H);

        Image sliderBgImg = sliderBg.AddComponent<Image>();
        sliderBgImg.color = new Color(0.15f, 0.18f, 0.22f);

        Slider slider = sliderBg.AddComponent<Slider>();
        slider.minValue = 0;
        slider.maxValue = 100;
        slider.wholeNumbers = true;
        slider.value = route.trafficWeight;
        slider.direction = Slider.Direction.LeftToRight;

        // Fill
        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(sliderBg.transform, false);

        RectTransform fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0, 1);
        fillRect.pivot = new Vector2(0, 0.5f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = routeColor;

        slider.fillRect = fillRect;

        // Handle area
        GameObject handleArea = new GameObject("HandleArea");
        handleArea.transform.SetParent(sliderBg.transform, false);

        RectTransform handleAreaRect = handleArea.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = Vector2.zero;
        handleAreaRect.offsetMax = Vector2.zero;

        // Handle
        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(handleArea.transform, false);

        RectTransform handleRect = handle.AddComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(12, SLIDER_H + 4);

        Image handleImg = handle.AddComponent<Image>();
        handleImg.color = new Color(0.83f, 0.64f, 0.3f);

        slider.handleRect = handleRect;
        slider.targetGraphic = handleImg;

        SliderEntry entry = new SliderEntry
        {
            routeData = route,
            slider = slider,
            valueLabel = valText,
            colorDot = dotImg,
            row = row,
            routeColor = routeColor
        };
        sliderEntries.Add(entry);

        slider.onValueChanged.AddListener((value) =>
        {
            int intVal = Mathf.RoundToInt(value);
            route.trafficWeight = intVal;
            UpdateValueDisplay(entry);
            UpdateRouteLine(entry);
        });
    }

    private void TogglePanel()
    {
        isExpanded = !isExpanded;

        if (expandedPanel != null)
            expandedPanel.SetActive(isExpanded);

        if (toggleText != null)
            toggleText.text = isExpanded ? "v Trade Routes" : "> Trade Routes";

        if (isExpanded)
            ShowAllRouteLines();
        else
            HideAllRouteLines();
    }

    private void UpdateValueDisplay(SliderEntry entry)
    {
        if (entry.valueLabel == null) return;
        int val = Mathf.RoundToInt(entry.slider.value);

        if (val == 0)
        {
            entry.valueLabel.text = "OFF";
            entry.valueLabel.color = new Color(0.8f, 0.3f, 0.3f);
            if (entry.colorDot != null)
            {
                Color dimmed = entry.routeColor;
                dimmed.a = 0.2f;
                entry.colorDot.color = dimmed;
            }
        }
        else
        {
            entry.valueLabel.text = val.ToString();
            entry.valueLabel.color = new Color(0.83f, 0.64f, 0.3f);
            if (entry.colorDot != null)
                entry.colorDot.color = entry.routeColor;
        }
    }

    private void ClearAll()
    {
        foreach (var entry in sliderEntries)
            if (entry.row != null) Destroy(entry.row);
        sliderEntries.Clear();

        foreach (var obj in routeLineObjects)
            if (obj != null) Destroy(obj);
        routeLineObjects.Clear();
    }

    private MapTradeRoutes GetCurrentMapRoutes()
    {
        if (TradeRouteManager.Instance == null) return null;
        int mapIndex = TradeRouteManager.Instance.CurrentMapIndex;
        if (mapIndex < 0) return null;
        var mapRouteData = TradeRouteManager.Instance.mapRouteData;
        if (mapRouteData == null || mapIndex >= mapRouteData.Length) return null;
        return mapRouteData[mapIndex];
    }
}