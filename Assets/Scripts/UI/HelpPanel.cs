using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Help Panel - Displays game instructions and information
/// 
/// REWRITTEN: Larger panel, properly scrollable, persistent help button always visible.
/// Updated content for trade route system and coastal missile drag-and-drop.
/// Preserves RegionalData integration for real-world statistics section.
/// 
/// SETUP:
/// 1. Add this component to any GameObject in your scene
/// 2. It auto-creates the help canvas + persistent "?" button in top-right
/// 3. Press F1 or H to toggle, ESC to close, click outside to dismiss
/// </summary>
public class HelpPanel : MonoBehaviour
{
    public static HelpPanel Instance { get; private set; }

    [Header("=== CUSTOM UI (Optional) ===")]
    public GameObject helpPanelObject;
    public Button closeButton;
    
    public TMP_FontAsset pirateFont;

    public Sprite helpButton;

    [Header("=== SETTINGS ===")]
    public KeyCode helpKey = KeyCode.F1;
    public bool closeOnClickOutside = true;
    public bool showPersistentHelpButton = true;

    // Internal references
    private Canvas helpCanvas;
    private GameObject canvasObject;
    private GameObject persistentButtonObject;
    private Transform contentParent;
    private bool isShowing = false;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (helpPanelObject == null)
        {
            CreateHelpPanel();
        }

        if (canvasObject != null)
        {
            canvasObject.SetActive(false);
        }
        else if (helpPanelObject != null)
        {
            helpPanelObject.SetActive(false);
        }

        if (showPersistentHelpButton)
        {
            CreatePersistentHelpButton();
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(helpKey) || Input.GetKeyDown(KeyCode.H))
        {
            Toggle();
        }

        if (isShowing && Input.GetKeyDown(KeyCode.Escape))
        {
            Hide();
        }
    }

    public void Toggle()
    {
        if (isShowing)
            Hide();
        else
            Show();
    }

    public void Show()
    {
        if (canvasObject != null)
            canvasObject.SetActive(true);
        else if (helpPanelObject != null)
            helpPanelObject.SetActive(true);

        isShowing = true;
    }

    public void Hide()
    {
        if (canvasObject != null)
            canvasObject.SetActive(false);
        else if (helpPanelObject != null)
            helpPanelObject.SetActive(false);

        isShowing = false;
    }

    /// <summary>
    /// Hide the persistent "?" button (call when simulation starts to free up screen space)
    /// </summary>
    public void HidePersistentButton()
    {
        if (persistentButtonObject != null)
            persistentButtonObject.transform.parent.gameObject.SetActive(false);
    }

    /// <summary>
    /// Show the persistent "?" button (call when returning to setup)
    /// </summary>
    public void ShowPersistentButton()
    {
        if (persistentButtonObject != null)
            persistentButtonObject.transform.parent.gameObject.SetActive(true);
    }

    void CreateHelpPanel()
    {
        // === MAIN CANVAS ===
        canvasObject = new GameObject("HelpCanvas");
        helpCanvas = canvasObject.AddComponent<Canvas>();
        helpCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        helpCanvas.sortingOrder = 999;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObject.AddComponent<GraphicRaycaster>();

        // === DARK BACKGROUND OVERLAY ===
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObject.transform, false);

        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0.75f);

        if (closeOnClickOutside)
        {
            Button bgButton = bgObj.AddComponent<Button>();
            bgButton.onClick.AddListener(Hide);
        }

        // === MAIN PANEL (LARGER) ===
        helpPanelObject = new GameObject("HelpPanel");
        helpPanelObject.transform.SetParent(canvasObject.transform, false);

        RectTransform panelRect = helpPanelObject.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(800, 900);
        panelRect.anchoredPosition = Vector2.zero;

        Image panelBg = helpPanelObject.AddComponent<Image>();
        panelBg.color = new Color(0.10f, 0.12f, 0.16f, 0.98f);

        Outline outline = helpPanelObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.3f, 0.5f, 0.7f, 1f);
        outline.effectDistance = new Vector2(3, -3);

        // === HEADER (FIXED AT TOP) ===
        GameObject headerObj = new GameObject("Header");
        headerObj.transform.SetParent(helpPanelObject.transform, false);

        RectTransform headerRect = headerObj.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0, 1);
        headerRect.anchorMax = new Vector2(1, 1);
        headerRect.pivot = new Vector2(0.5f, 1);
        headerRect.anchoredPosition = new Vector2(0, -20);
        headerRect.sizeDelta = new Vector2(-40, 60);

        TextMeshProUGUI headerText = headerObj.AddComponent<TextMeshProUGUI>();
        headerText.text = "HELP & GUIDE";
        headerText.fontSize = 36;
        headerText.fontStyle = FontStyles.Bold;
        headerText.color = new Color(0.4f, 0.7f, 1f);
        headerText.alignment = TextAlignmentOptions.Center;

        // === CLOSE X BUTTON (TOP-RIGHT) ===
        GameObject closeBtnObj = new GameObject("CloseX");
        closeBtnObj.transform.SetParent(helpPanelObject.transform, false);

        RectTransform closeBtnRect = closeBtnObj.AddComponent<RectTransform>();
        closeBtnRect.anchorMin = new Vector2(1, 1);
        closeBtnRect.anchorMax = new Vector2(1, 1);
        closeBtnRect.pivot = new Vector2(1, 1);
        closeBtnRect.anchoredPosition = new Vector2(-15, -15);
        closeBtnRect.sizeDelta = new Vector2(40, 40);

        Image closeBtnImg = closeBtnObj.AddComponent<Image>();
        closeBtnImg.color = new Color(0.7f, 0.2f, 0.2f);

        Button closeXButton = closeBtnObj.AddComponent<Button>();
        closeXButton.onClick.AddListener(Hide);

        GameObject closeXTextObj = new GameObject("X");
        closeXTextObj.transform.SetParent(closeBtnObj.transform, false);
        RectTransform closeXTextRect = closeXTextObj.AddComponent<RectTransform>();
        closeXTextRect.anchorMin = Vector2.zero;
        closeXTextRect.anchorMax = Vector2.one;
        closeXTextRect.offsetMin = Vector2.zero;
        closeXTextRect.offsetMax = Vector2.zero;

        TextMeshProUGUI closeXText = closeXTextObj.AddComponent<TextMeshProUGUI>();
        closeXText.text = "X";
        closeXText.fontSize = 24;
        closeXText.fontStyle = FontStyles.Bold;
        closeXText.color = Color.white;
        closeXText.alignment = TextAlignmentOptions.Center;

        // === SCROLL VIEW ===
        GameObject scrollObj = new GameObject("ScrollView");
        scrollObj.transform.SetParent(helpPanelObject.transform, false);

        RectTransform scrollRect = scrollObj.AddComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0, 0);
        scrollRect.anchorMax = new Vector2(1, 1);
        scrollRect.offsetMin = new Vector2(20, 80);
        scrollRect.offsetMax = new Vector2(-20, -90);

        Image scrollBg = scrollObj.AddComponent<Image>();
        scrollBg.color = new Color(0.08f, 0.10f, 0.14f, 1f);

        ScrollRect sr = scrollObj.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.scrollSensitivity = 30;

        RectMask2D mask = scrollObj.AddComponent<RectMask2D>();

        // === SCROLL CONTENT ===
        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(scrollObj.transform, false);

        RectTransform contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0, 0);

        VerticalLayoutGroup contentLayout = contentObj.AddComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(20, 20, 20, 20);
        contentLayout.spacing = 18;
        contentLayout.childAlignment = TextAnchor.UpperLeft;
        contentLayout.childControlHeight = true;
        contentLayout.childControlWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.childForceExpandWidth = true;

        ContentSizeFitter contentFitter = contentObj.AddComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.content = contentRect;
        sr.viewport = scrollRect;

        contentParent = contentObj.transform;

        // === SECTIONS ===
        CreateSection(contentParent, "ABOUT",
            "Maritime Piracy Simulation - CS 499 Senior Project\n" +
            "Simulates real-world piracy hotspots to analyze maritime protection effectiveness across three major shipping regions.");

        CreateSection(contentParent, "CONTROLS",
            "<b>Start</b> - Begin the simulation\n" +
            "<b>Pause/Resume</b> - Freeze or continue time\n" +
            "<b>Step</b> - Advance one simulation tick\n" +
            "<b>Reset</b> - Clear and start over\n" +
            "<b>Speed Slider</b> - Adjust simulation speed (0.5x - 500x)\n" +
            "<b>Trail Toggle</b> - Show or hide ship wake trails");

        CreateSection(contentParent, "SHIPS",
            "<color=#FFD700>● Merchant</color> - Transports valuable cargo along trade routes\n" +
            "<color=#FF4444>● Pirate</color> - Hunts and captures merchants from coastal hideouts\n" +
            "<color=#4488FF>● Navy</color> - Patrols shipping lanes and intercepts pirates\n\n" +
            "<i>Hover over any ship to see its current status and details.</i>");

        CreateSection(contentParent, "SETUP",
            "<b>Initial Ships</b> - Number of each type at simulation start\n" +
            "<b>Spawn Rates</b> - How frequently new ships appear (1-10)\n" +
            "<b>Map</b> - Choose a region using the map selector\n" +
            "<b>Time of Day</b> - Affects detection range and visibility\n" +
            "<b>Weather</b> - Affects ship speed and detection\n" +
            "<b>Duration</b> - Run length in ticks (0 = unlimited)\n" +
            "<b>Seed</b> - Same seed produces identical runs");

        CreateSection(contentParent, "COASTAL DEFENSE",
            "Before starting a simulation, <b>click and drag coastal defense missiles</b> from the panel onto land near pirate hotspots. " +
            "Once placed, missiles automatically engage pirates that come within range, providing static defense for vulnerable shipping lanes.\n\n" +
            "Place missiles strategically near choke points and known pirate areas for maximum effectiveness.");

        CreateSection(contentParent, "CONDITIONS",
            "<b>Time of Day</b> affects pirate activity:\n" +
            "• Morning/Afternoon - Normal visibility\n" +
            "• Evening/Night - Reduced detection, more pirate activity\n\n" +
            "<b>Weather</b> affects ship behavior:\n" +
            "• Clear - Normal conditions\n" +
            "• Cloudy/Foggy - Reduced detection range\n" +
            "• Stormy - Slower ships, lower visibility");

        CreateSection(contentParent, "REGIONS",
            "Three real-world piracy hotspots are simulated, each with historically accurate trade routes and pirate base locations:\n\n" +
            "<b>Strait of Malacca</b> - Singapore/Malaysia/Indonesia\n" +
            "Major shipping artery between Indian Ocean and East Asia.\n\n" +
            "<b>Gulf of Aden</b> - Somalia/Yemen/Djibouti\n" +
            "Critical chokepoint for Persian Gulf and Suez traffic.\n\n" +
            "<b>Gulf of Guinea</b> - Nigeria/Cameroon/Gabon\n" +
            "West African coast with active piracy near oil ports.");

        AddRegionalStatsSection();

        CreateSection(contentParent, "KEYBINDS",
            "<b>F1 or H</b> - Toggle this help panel\n" +
            "<b>ESC</b> - Close panels");

        // === HOTKEY HINT (FIXED AT BOTTOM) ===
        GameObject hintObj = new GameObject("HotkeyHint");
        hintObj.transform.SetParent(helpPanelObject.transform, false);

        RectTransform hintRect = hintObj.AddComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0, 0);
        hintRect.anchorMax = new Vector2(1, 0);
        hintRect.pivot = new Vector2(0.5f, 0);
        hintRect.anchoredPosition = new Vector2(0, 20);
        hintRect.sizeDelta = new Vector2(-40, 30);

        TextMeshProUGUI hintText = hintObj.AddComponent<TextMeshProUGUI>();
        hintText.text = "Press H or F1 to toggle help  •  ESC to close  •  Click outside to dismiss";
        hintText.fontSize = 14;
        hintText.fontStyle = FontStyles.Italic;
        hintText.color = new Color(0.5f, 0.5f, 0.5f);
        hintText.alignment = TextAlignmentOptions.Center;
    }

    void CreateSection(Transform parent, string title, string content)
    {
        GameObject sectionObj = new GameObject($"Section_{title}");
        sectionObj.transform.SetParent(parent, false);

        VerticalLayoutGroup sectionLayout = sectionObj.AddComponent<VerticalLayoutGroup>();
        sectionLayout.spacing = 8;
        sectionLayout.childControlHeight = true;
        sectionLayout.childControlWidth = true;
        sectionLayout.childForceExpandHeight = false;
        sectionLayout.childForceExpandWidth = true;

        ContentSizeFitter sectionFitter = sectionObj.AddComponent<ContentSizeFitter>();
        sectionFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Title
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(sectionObj.transform, false);

        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = title;
        titleText.fontSize = 20;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = new Color(0.4f, 0.7f, 1f);

        ContentSizeFitter titleFitter = titleObj.AddComponent<ContentSizeFitter>();
        titleFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        LayoutElement titleLe = titleObj.AddComponent<LayoutElement>();
        titleLe.minHeight = 28;

        // Content
        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(sectionObj.transform, false);

        TextMeshProUGUI contentText = contentObj.AddComponent<TextMeshProUGUI>();
        contentText.text = content;
        contentText.fontSize = 16;
        contentText.color = new Color(0.85f, 0.85f, 0.85f);
        contentText.richText = true;
        contentText.enableWordWrapping = true;
        contentText.lineSpacing = 5;

        ContentSizeFitter fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void AddRegionalStatsSection()
    {
        var malacca = RegionalData.Malacca.Stats;
        var aden = RegionalData.Aden.Stats;
        var guinea = RegionalData.Guinea.Stats;

        string content =
            $"<b>STRAIT OF MALACCA</b>\n" +
            $"• Traffic: {malacca.GetTrafficString()}\n" +
            $"• Trade: {malacca.GetTradeString()}\n" +
            $"• Peak piracy: {malacca.peakPiracyYear} ({malacca.peakIncidents} incidents)\n" +
            $"• Current risk: {malacca.currentRiskLevel}\n\n" +

            $"<b>GULF OF ADEN</b>\n" +
            $"• Traffic: {aden.GetTrafficString()}\n" +
            $"• Trade: {aden.GetTradeString()}\n" +
            $"• Peak piracy: {aden.peakPiracyYear} ({aden.peakIncidents} attacks)\n" +
            $"• Current risk: {aden.currentRiskLevel}\n\n" +

            $"<b>GULF OF GUINEA</b>\n" +
            $"• Traffic: {guinea.GetTrafficString()}\n" +
            $"• Trade: {guinea.GetTradeString()}\n" +
            $"• Peak piracy: {guinea.peakPiracyYear} (kidnappings)\n" +
            $"• Current risk: {guinea.currentRiskLevel}\n" +
            $"• Last: {guinea.lastMajorIncident}";

        CreateSection(contentParent, "REAL-WORLD STATISTICS", content);
    }

    void CreatePersistentHelpButton()
    {
        // Create a separate canvas for the persistent button so it stays on top
        GameObject btnCanvasObj = new GameObject("HelpButtonCanvas");
        Canvas btnCanvas = btnCanvasObj.AddComponent<Canvas>();
        btnCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        btnCanvas.sortingOrder = 998;

        CanvasScaler btnScaler = btnCanvasObj.AddComponent<CanvasScaler>();
        btnScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        btnScaler.referenceResolution = new Vector2(1920, 1080);

        btnCanvasObj.AddComponent<GraphicRaycaster>();

        // Help button in top-right corner
        persistentButtonObject = new GameObject("HelpButton");
        persistentButtonObject.transform.SetParent(btnCanvasObj.transform, false);

        RectTransform btnRect = persistentButtonObject.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0, 1);
        btnRect.anchorMax = new Vector2(0, 1);
        btnRect.pivot = new Vector2(0, 1);
        btnRect.anchoredPosition = new Vector2(20, -20);
        btnRect.sizeDelta = new Vector2(45, 45);

        Image btnImg = persistentButtonObject.AddComponent<Image>();
        btnImg.sprite = helpButton;
        //btnImg.color = new Color(0.2f, 0.5f, 0.8f, 0.9f);

        Button helpBtn = persistentButtonObject.AddComponent<Button>();
        helpBtn.onClick.AddListener(Toggle);

        ColorBlock colors = helpBtn.colors;
        colors.normalColor = new Color(0.8f, 0.8f, 0.8f, 0.9f);
        colors.highlightedColor = new Color(0.3f, 0.6f, 0.9f, 1f);
        colors.pressedColor = new Color(0.15f, 0.4f, 0.7f, 1f);
        helpBtn.colors = colors;

        Outline btnOutline = persistentButtonObject.AddComponent<Outline>();
        btnOutline.effectColor = new Color(1, 1, 1, 0.6f);
        btnOutline.effectDistance = new Vector2(2, -2);

        // Question mark text
        GameObject btnTextObj = new GameObject("Text");
        btnTextObj.transform.SetParent(persistentButtonObject.transform, false);

        RectTransform btnTextRect = btnTextObj.AddComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.offsetMin = Vector2.zero;
        btnTextRect.offsetMax = Vector2.zero;

        TextMeshProUGUI btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
        btnText.text = "H";
        btnText.font = pirateFont;
        btnText.fontSize = 15;
        //btnText.fontStyle = FontStyles.Bold;
        btnText.color = Color.white;
        btnText.alignment = TextAlignmentOptions.Center;
    }
}