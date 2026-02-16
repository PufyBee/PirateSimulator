using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// End of Run Panel - Shows simulation results summary
/// 
/// Displays:
/// - Final statistics (escaped, captured, defeated)
/// - Protection rate with grade
/// - Conditions summary
/// - Run duration
/// 
/// SETUP:
/// 1. Add this to a GameObject in your scene
/// 2. Call Show() when simulation ends or reset is clicked
/// 3. Connect the OnClose event to return to setup
/// </summary>
public class EndOfRunPanel : MonoBehaviour
{
    public static EndOfRunPanel Instance { get; private set; }

    [Header("=== CUSTOM UI (Optional) ===")]
    public GameObject panelObject;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI gradeText;
    public TextMeshProUGUI protectionRateText;
    public TextMeshProUGUI merchantsEscapedText;
    public TextMeshProUGUI merchantsCapturedText;
    public TextMeshProUGUI piratesDefeatedText;
    public TextMeshProUGUI conditionsText;
    public TextMeshProUGUI durationText;
    public Button newRunButton;
    public Button closeButton;

    [Header("=== GRADE COLORS ===")]
    public Color gradeA = new Color(0.2f, 0.9f, 0.3f);   // Green
    public Color gradeB = new Color(0.6f, 0.9f, 0.2f);   // Yellow-green
    public Color gradeC = new Color(0.9f, 0.9f, 0.2f);   // Yellow
    public Color gradeD = new Color(0.9f, 0.6f, 0.2f);   // Orange
    public Color gradeF = new Color(0.9f, 0.2f, 0.2f);   // Red

    [Header("=== EVENTS ===")]
    public UnityEngine.Events.UnityEvent OnNewRun;
    public UnityEngine.Events.UnityEvent OnClose;

    [Header("=== EXPORT (Optional) ===")]
    public Button exportButton;

    // Internal
    private GameObject canvasObject;
    private bool isShowing = false;

    // Cached results
    private int lastEscaped;
    private int lastCaptured;
    private int lastDefeated;
    private int lastTicks;
    private float lastProtectionRate;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (panelObject == null)
        {
            CreateEndPanel();
        }

        if (canvasObject != null)
            canvasObject.SetActive(false);
        else if (panelObject != null)
            panelObject.SetActive(false);
    }

    /// <summary>
    /// Show the end panel with current simulation results
    /// </summary>
    public void Show()
    {
        // Gather stats from SimulationEngine
        GatherStats();
        UpdateDisplay();

        if (canvasObject != null)
            canvasObject.SetActive(true);
        else if (panelObject != null)
            panelObject.SetActive(true);

        isShowing = true;
    }

    /// <summary>
    /// Show with manually provided stats
    /// </summary>
    public void Show(int escaped, int captured, int defeated, int ticks)
    {
        lastEscaped = escaped;
        lastCaptured = captured;
        lastDefeated = defeated;
        lastTicks = ticks;
        
        // Calculate protection rate
        int totalMerchants = escaped + captured;
        lastProtectionRate = totalMerchants > 0 ? (float)escaped / totalMerchants * 100f : 100f;

        UpdateDisplay();

        if (canvasObject != null)
            canvasObject.SetActive(true);
        else if (panelObject != null)
            panelObject.SetActive(true);

        isShowing = true;
    }

    public void Hide()
    {
        if (canvasObject != null)
            canvasObject.SetActive(false);
        else if (panelObject != null)
            panelObject.SetActive(false);

        isShowing = false;
    }

    void GatherStats()
    {
        // Try to get stats from SimulationEngine
        SimulationEngine engine = FindObjectOfType<SimulationEngine>();
        
        if (engine != null)
        {
            lastEscaped = engine.GetMerchantsExited();
            lastCaptured = engine.GetMerchantsCaptured();
            lastDefeated = engine.GetPiratesDefeated();
            lastTicks = engine.GetTickCount();
        }
        else
        {
            lastEscaped = 0;
            lastCaptured = 0;
            lastDefeated = 0;
            lastTicks = 0;
        }

        // Calculate protection rate
        int totalMerchants = lastEscaped + lastCaptured;
        lastProtectionRate = totalMerchants > 0 ? (float)lastEscaped / totalMerchants * 100f : 100f;
    }

    void UpdateDisplay()
    {
        // Title
        if (titleText != null)
        {
            titleText.text = "SIMULATION COMPLETE";
        }

        // Grade
        if (gradeText != null)
        {
            string grade = GetGrade(lastProtectionRate);
            gradeText.text = grade;
            gradeText.color = GetGradeColor(lastProtectionRate);
        }

        // Protection rate
        if (protectionRateText != null)
        {
            protectionRateText.text = $"Protection Rate: {lastProtectionRate:F1}%";
        }

        // Stats
        if (merchantsEscapedText != null)
            merchantsEscapedText.text = $"Merchants Escaped: {lastEscaped}";

        if (merchantsCapturedText != null)
            merchantsCapturedText.text = $"Merchants Captured: {lastCaptured}";

        if (piratesDefeatedText != null)
            piratesDefeatedText.text = $"Pirates Defeated: {lastDefeated}";

        // Conditions
        if (conditionsText != null && EnvironmentSettings.Instance != null)
        {
            conditionsText.text = $"Conditions: {EnvironmentSettings.Instance.GetConditionsSummary()}";
        }

        // Duration
        if (durationText != null)
        {
            int days = lastTicks / 1440;  // Assuming 1440 ticks per day
            int hours = (lastTicks % 1440) / 60;
            durationText.text = $"Duration: Day {days + 1}, {hours:D2}:00 ({lastTicks} ticks)";
        }
    }

    string GetGrade(float protectionRate)
    {
        if (protectionRate >= 90f) return "A+";
        if (protectionRate >= 80f) return "A";
        if (protectionRate >= 70f) return "B";
        if (protectionRate >= 60f) return "C";
        if (protectionRate >= 50f) return "D";
        return "F";
    }

    Color GetGradeColor(float protectionRate)
    {
        if (protectionRate >= 80f) return gradeA;
        if (protectionRate >= 70f) return gradeB;
        if (protectionRate >= 60f) return gradeC;
        if (protectionRate >= 50f) return gradeD;
        return gradeF;
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

    void CreateEndPanel()
    {
        // Create canvas
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
        panelRect.sizeDelta = new Vector2(450, 590);
        panelRect.anchoredPosition = Vector2.zero;

        Image panelBg = panelObject.AddComponent<Image>();
        panelBg.color = new Color(0.1f, 0.12f, 0.16f, 0.98f);

        Outline outline = panelObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.3f, 0.6f, 0.4f, 1f);
        outline.effectDistance = new Vector2(3, -3);

        // Layout
        VerticalLayoutGroup layout = panelObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(30, 30, 25, 25);
        layout.spacing = 12;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        // === TITLE ===
        titleText = CreateText(panelObject.transform, "SIMULATION COMPLETE", 28, FontStyles.Bold, new Color(0.4f, 0.8f, 0.5f));
        titleText.GetComponent<LayoutElement>().preferredHeight = 45;

        // === BIG GRADE ===
        gradeText = CreateText(panelObject.transform, "A+", 72, FontStyles.Bold, gradeA);
        gradeText.GetComponent<LayoutElement>().preferredHeight = 90;

        // === PROTECTION RATE ===
        protectionRateText = CreateText(panelObject.transform, "Protection Rate: 85.0%", 24, FontStyles.Normal, Color.white);
        protectionRateText.GetComponent<LayoutElement>().preferredHeight = 35;

        // === STATS ===
        merchantsEscapedText = CreateText(panelObject.transform, "Merchants Escaped: 0", 18, FontStyles.Normal, new Color(0.3f, 0.9f, 0.4f));
        merchantsEscapedText.GetComponent<LayoutElement>().preferredHeight = 28;

        merchantsCapturedText = CreateText(panelObject.transform, "Merchants Captured: 0", 18, FontStyles.Normal, new Color(0.9f, 0.4f, 0.3f));
        merchantsCapturedText.GetComponent<LayoutElement>().preferredHeight = 28;

        piratesDefeatedText = CreateText(panelObject.transform, "Pirates Defeated: 0", 18, FontStyles.Normal, new Color(0.4f, 0.6f, 0.9f));
        piratesDefeatedText.GetComponent<LayoutElement>().preferredHeight = 28;

        // === CONDITIONS & DURATION ===
        conditionsText = CreateText(panelObject.transform, "Conditions: Morning, Clear", 14, FontStyles.Italic, new Color(0.7f, 0.7f, 0.7f));
        conditionsText.GetComponent<LayoutElement>().preferredHeight = 22;

        durationText = CreateText(panelObject.transform, "Duration: Day 1, 06:00 (360 ticks)", 14, FontStyles.Italic, new Color(0.7f, 0.7f, 0.7f));
        durationText.GetComponent<LayoutElement>().preferredHeight = 22;

        // === BUTTONS ===
        // Export button
        exportButton = CreateSimpleButton(panelObject.transform, "Export CSV", new Color(0.3f, 0.4f, 0.6f, 1f), OnExportClicked);

        // New Run button
        newRunButton = CreateSimpleButton(panelObject.transform, "New Run", new Color(0.2f, 0.55f, 0.3f, 1f), OnNewRunClicked);

        // Close button  
        closeButton = CreateSimpleButton(panelObject.transform, "Close", new Color(0.45f, 0.45f, 0.5f, 1f), OnCloseClicked);
    }

    void OnExportClicked()
    {
        if (CSVExporter.Instance != null)
        {
            CSVExporter.Instance.ExportResults();
        }
        else
        {
            // Try to find it
            var exporter = FindObjectOfType<CSVExporter>();
            if (exporter != null)
            {
                exporter.ExportResults();
            }
            else
            {
                Debug.LogWarning("CSVExporter not found in scene. Add CSVExporter component to export results.");
            }
        }
    }

    Button CreateSimpleButton(Transform parent, string text, Color bgColor, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject($"Button_{text}");
        btnObj.transform.SetParent(parent, false);

        Image btnImage = btnObj.AddComponent<Image>();
        btnImage.color = bgColor;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnImage;
        btn.onClick.AddListener(onClick);

        LayoutElement le = btnObj.AddComponent<LayoutElement>();
        le.preferredHeight = 45;
        le.preferredWidth = 200;

        // Text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI btnText = textObj.AddComponent<TextMeshProUGUI>();
        btnText.text = text;
        btnText.fontSize = 20;
        btnText.fontStyle = FontStyles.Bold;
        btnText.color = Color.white;
        btnText.alignment = TextAlignmentOptions.Center;

        return btn;
    }

    TextMeshProUGUI CreateText(Transform parent, string text, int fontSize, FontStyles style, Color color)
    {
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;

        LayoutElement le = textObj.AddComponent<LayoutElement>();
        le.preferredHeight = fontSize + 10;

        return tmp;
    }

    void CreateDivider(Transform parent)
    {
        // Not used anymore
    }
}