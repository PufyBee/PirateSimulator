using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// END OF RUN PANEL - Comprehensive simulation results display
/// 
/// Shows all tracked and derived statistics when a simulation ends.
/// Self-creating UI with scrollable results panel.
/// </summary>
public class EndOfRunPanel : MonoBehaviour
{
    public static EndOfRunPanel Instance { get; private set; }

    [Header("=== EXPORT (Optional) ===")]
    public Button exportButton;

    [Header("=== EVENTS ===")]
    public UnityEngine.Events.UnityEvent OnNewRun;
    public UnityEngine.Events.UnityEvent OnClose;

    // Internal
    private GameObject canvasObject;
    private GameObject panelObject;
    private TMP_Text resultsText;
    private bool isShowing = false;

    // Cached results for CSV export
    private int lastEscaped;
    private int lastCaptured;
    private int lastDefeated;
    private int lastTicks;
    private int lastTotalMerchantsSpawned;
    private int lastTotalPiratesSpawned;
    private int lastTotalSecuritySpawned;
    private int lastPeakActiveShips;
    private int lastPeakActivePirates;
    private int lastPeakActiveMerchants;
    private float lastSimHours;
    private float lastSimDays;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        CreateEndPanel();
        if (canvasObject != null)
            canvasObject.SetActive(false);
    }

    public void Show()
    {
        GatherStats();
        UpdateDisplay();

        if (canvasObject != null)
            canvasObject.SetActive(true);

        isShowing = true;
    }

    public void Show(int escaped, int captured, int defeated, int ticks)
    {
        lastEscaped = escaped;
        lastCaptured = captured;
        lastDefeated = defeated;
        lastTicks = ticks;
        UpdateDisplay();

        if (canvasObject != null)
            canvasObject.SetActive(true);

        isShowing = true;
    }

    public void Hide()
    {
        if (canvasObject != null)
            canvasObject.SetActive(false);
        isShowing = false;
    }

    void GatherStats()
    {
        SimulationEngine engine = FindObjectOfType<SimulationEngine>();

        if (engine != null)
        {
            lastEscaped = engine.GetMerchantsExited();
            lastCaptured = engine.GetMerchantsCaptured();
            lastDefeated = engine.GetPiratesDefeated();
            lastTicks = engine.GetTickCount();
            lastTotalMerchantsSpawned = engine.GetTotalMerchantsSpawned();
            lastTotalPiratesSpawned = engine.GetTotalPiratesSpawned();
            lastTotalSecuritySpawned = engine.GetTotalSecuritySpawned();
            lastPeakActiveShips = engine.GetPeakActiveShips();
            lastPeakActivePirates = engine.GetPeakActivePirates();
            lastPeakActiveMerchants = engine.GetPeakActiveMerchants();
            lastSimHours = engine.GetSimulatedHours();
            lastSimDays = engine.GetSimulatedDays();
        }
        else
        {
            lastEscaped = 0;
            lastCaptured = 0;
            lastDefeated = 0;
            lastTicks = 0;
            lastTotalMerchantsSpawned = 0;
            lastTotalPiratesSpawned = 0;
            lastTotalSecuritySpawned = 0;
            lastPeakActiveShips = 0;
            lastPeakActivePirates = 0;
            lastPeakActiveMerchants = 0;
            lastSimHours = 0;
            lastSimDays = 0;
        }

        // Cache for CSV export
        CSVExporter.CacheFinalStats();
    }

    void UpdateDisplay()
    {
        if (resultsText == null) return;

        // Derived statistics
        int totalMerchants = lastEscaped + lastCaptured;
        float protectionRate = totalMerchants > 0 ? (float)lastEscaped / totalMerchants * 100f : 100f;
        float captureRate = totalMerchants > 0 ? (float)lastCaptured / totalMerchants * 100f : 0f;
        float capturesPerDay = lastSimDays > 0 ? lastCaptured / lastSimDays : 0f;
        float defeatsPerDay = lastSimDays > 0 ? lastDefeated / lastSimDays : 0f;
        float merchantsPerDay = lastSimDays > 0 ? totalMerchants / lastSimDays : 0f;
        float pirateEfficiency = lastTotalPiratesSpawned > 0 ? (float)lastCaptured / lastTotalPiratesSpawned * 100f : 0f;
        float navyEfficiency = lastTotalSecuritySpawned > 0 ? (float)lastDefeated / lastTotalSecuritySpawned * 100f : 0f;

        int days = Mathf.FloorToInt(lastSimHours / 24f);
        int hours = Mathf.FloorToInt(lastSimHours % 24f);

        // Conditions
        string conditions = "N/A";
        string detMult = "N/A";
        string spdMult = "N/A";
        if (EnvironmentSettings.Instance != null)
        {
            conditions = EnvironmentSettings.Instance.GetConditionsSummary();
            detMult = $"{EnvironmentSettings.Instance.DetectionMultiplier:P0}";
            spdMult = $"{EnvironmentSettings.Instance.SpeedMultiplier:P0}";
        }

        // Map
        string mapName = "Unknown";
        if (MapManager.Instance != null)
            mapName = MapManager.Instance.GetCurrentMapName();

        // Build display
        string text = "";

        // Header
        text += $"<color=#66BB77><b><size=26>SIMULATION COMPLETE</size></b></color>\n\n";

        // Run Info
        text += $"<color=#AA8855><b>RUN INFO</b></color>\n";
        text += $"<color=#AAAAAA>Duration: Day {days}, {hours}h ({lastTicks} ticks)</color>\n";
        text += $"<color=#AAAAAA>Map: {mapName}</color>\n";
        text += $"<color=#AAAAAA>Conditions: {conditions}</color>\n";
        text += $"<color=#AAAAAA>Detection: {detMult} | Speed: {spdMult}</color>\n\n";

        // Core Outcomes
        text += $"<color=#AA8855><b>OUTCOMES</b></color>\n";
        text += $"<color=#44FF44>Merchants Escaped: {lastEscaped}</color>\n";
        text += $"<color=#FF4444>Merchants Captured: {lastCaptured}</color>\n";
        text += $"<color=#44FFFF>Pirates Defeated: {lastDefeated}</color>\n\n";

        // Rates
        text += $"<color=#AA8855><b>ANALYSIS</b></color>\n";
        text += $"<color=#DDDDDD>Protection Rate: {protectionRate:F1}%</color>\n";
        text += $"<color=#DDDDDD>Capture Rate: {captureRate:F1}%</color>\n";
        text += $"<color=#DDDDDD>Captures / Day: {capturesPerDay:F1}</color>\n";
        text += $"<color=#DDDDDD>Defeats / Day: {defeatsPerDay:F1}</color>\n";
        text += $"<color=#DDDDDD>Merchants / Day: {merchantsPerDay:F1}</color>\n\n";

        // Spawn Totals
        text += $"<color=#AA8855><b>SPAWN TOTALS</b></color>\n";
        text += $"<color=#DDDDDD>Total Merchants Spawned: {lastTotalMerchantsSpawned}</color>\n";
        text += $"<color=#DDDDDD>Total Pirates Spawned: {lastTotalPiratesSpawned}</color>\n";
        text += $"<color=#DDDDDD>Total Navy Spawned: {lastTotalSecuritySpawned}</color>\n";
        text += $"<color=#DDDDDD>Total Ships Spawned: {lastTotalMerchantsSpawned + lastTotalPiratesSpawned + lastTotalSecuritySpawned}</color>\n\n";

        // Peaks
        text += $"<color=#AA8855><b>PEAK ACTIVITY</b></color>\n";
        text += $"<color=#DDDDDD>Peak Active Ships: {lastPeakActiveShips}</color>\n";
        text += $"<color=#DDDDDD>Peak Active Merchants: {lastPeakActiveMerchants}</color>\n";
        text += $"<color=#DDDDDD>Peak Active Pirates: {lastPeakActivePirates}</color>\n\n";

        // Efficiency
        text += $"<color=#AA8855><b>EFFICIENCY</b></color>\n";
        text += $"<color=#DDDDDD>Pirate Efficiency: {pirateEfficiency:F1}% (captures per pirate spawned)</color>\n";
        text += $"<color=#DDDDDD>Navy Efficiency: {navyEfficiency:F1}% (defeats per navy spawned)</color>\n";
        text += $"<color=#DDDDDD>Total Merchants Processed: {totalMerchants}</color>\n";

        resultsText.text = text;
    }

    void CreateEndPanel()
    {
        // Canvas
        canvasObject = new GameObject("EndOfRunCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 998;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObject.AddComponent<GraphicRaycaster>();

        // Dark background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObject.transform, false);

        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0.8f);

        // Main panel
        panelObject = new GameObject("EndPanel");
        panelObject.transform.SetParent(canvasObject.transform, false);

        RectTransform panelRect = panelObject.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(450, 750);
        panelRect.anchoredPosition = Vector2.zero;

        Image panelBg = panelObject.AddComponent<Image>();
        panelBg.color = new Color(0.1f, 0.12f, 0.16f, 0.98f);

        Outline outline = panelObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.3f, 0.6f, 0.4f, 1f);
        outline.effectDistance = new Vector2(3, -3);

        // Scroll view
        GameObject scrollObj = new GameObject("ScrollView");
        scrollObj.transform.SetParent(panelObject.transform, false);

        RectTransform scrollRect = scrollObj.AddComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0, 0);
        scrollRect.anchorMax = new Vector2(1, 1);
        scrollRect.offsetMin = new Vector2(10, 70);
        scrollRect.offsetMax = new Vector2(-10, -10);

        ScrollRect sr = scrollObj.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.scrollSensitivity = 25f;
        sr.movementType = ScrollRect.MovementType.Clamped;

        Image scrollBg = scrollObj.AddComponent<Image>();
        scrollBg.color = new Color(0, 0, 0, 0);

        RectMask2D mask = scrollObj.AddComponent<RectMask2D>();

        // Content
        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(scrollObj.transform, false);

        RectTransform contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0, 0);

        ContentSizeFitter fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.content = contentRect;
        sr.viewport = scrollRect;

        // Results text
        GameObject textObj = new GameObject("ResultsText");
        textObj.transform.SetParent(contentObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0, 1);
        textRect.anchorMax = new Vector2(1, 1);
        textRect.pivot = new Vector2(0.5f, 1);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = new Vector2(-20, 0);

        resultsText = textObj.AddComponent<TextMeshProUGUI>();
        resultsText.fontSize = 16;
        resultsText.color = Color.white;
        resultsText.alignment = TextAlignmentOptions.TopLeft;
        resultsText.enableWordWrapping = true;
        resultsText.richText = true;
        resultsText.lineSpacing = 3;

        ContentSizeFitter textFitter = textObj.AddComponent<ContentSizeFitter>();
        textFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Button row at the bottom
        GameObject buttonRow = new GameObject("ButtonRow");
        buttonRow.transform.SetParent(panelObject.transform, false);

        RectTransform buttonRowRect = buttonRow.AddComponent<RectTransform>();
        buttonRowRect.anchorMin = new Vector2(0, 0);
        buttonRowRect.anchorMax = new Vector2(1, 0);
        buttonRowRect.pivot = new Vector2(0.5f, 0);
        buttonRowRect.anchoredPosition = new Vector2(0, 10);
        buttonRowRect.sizeDelta = new Vector2(-20, 50);

        HorizontalLayoutGroup buttonLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
        buttonLayout.spacing = 8;
        buttonLayout.childControlWidth = true;
        buttonLayout.childControlHeight = true;
        buttonLayout.childForceExpandWidth = true;
        buttonLayout.childForceExpandHeight = true;

        // Export CSV button
        CreateButton(buttonRow.transform, "Export CSV", new Color(0.3f, 0.4f, 0.6f), OnExportClicked);

        // New Run button
        CreateButton(buttonRow.transform, "New Run", new Color(0.2f, 0.55f, 0.3f), OnNewRunClicked);
    }

    void CreateButton(Transform parent, string label, Color color, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject($"Btn_{label}");
        btnObj.transform.SetParent(parent, false);

        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = color;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(onClick);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TMP_Text btnText = textObj.AddComponent<TextMeshProUGUI>();
        btnText.text = label;
        btnText.fontSize = 16;
        btnText.fontStyle = FontStyles.Bold;
        btnText.color = Color.white;
        btnText.alignment = TextAlignmentOptions.Center;
    }

    void OnExportClicked()
    {
        if (CSVExporter.Instance != null)
            CSVExporter.Instance.ExportResults();
        else
        {
            var exporter = FindObjectOfType<CSVExporter>();
            if (exporter != null)
                exporter.ExportResults();
            else
                Debug.LogWarning("CSVExporter not found in scene.");
        }
    }

    void OnNewRunClicked()
    {
        Hide();
        OnNewRun?.Invoke();
    }

    void OnCloseClicked()
    {
        Hide();
        OnClose?.Invoke();
    }
}