using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Help Panel - Displays game instructions and information
/// 
/// Can be used in Main Menu or Simulation scene.
/// Call Show() to open, Hide() to close.
/// 
/// SETUP:
/// 1. Add this to a GameObject
/// 2. It auto-creates the UI, OR assign custom panel
/// 3. Add a button that calls HelpPanel.Instance.Toggle()
/// </summary>
public class HelpPanel : MonoBehaviour
{
    public static HelpPanel Instance { get; private set; }

    [Header("=== CUSTOM UI (Optional) ===")]
    public GameObject helpPanelObject;
    public Button closeButton;

    [Header("=== SETTINGS ===")]
    public KeyCode helpKey = KeyCode.F1;
    public bool closeOnClickOutside = true;

    // Internal
    private Canvas helpCanvas;
    private GameObject canvasObject;
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

        // Hide the entire canvas on start
        if (canvasObject != null)
        {
            canvasObject.SetActive(false);
        }
        else if (helpPanelObject != null)
        {
            helpPanelObject.SetActive(false);
        }
    }

    void Update()
    {
        // F1 or H to toggle help
        if (Input.GetKeyDown(helpKey) || Input.GetKeyDown(KeyCode.H))
        {
            Toggle();
        }

        // Escape to close
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

    void CreateHelpPanel()
    {
        // Create canvas
        canvasObject = new GameObject("HelpCanvas");
        helpCanvas = canvasObject.AddComponent<Canvas>();
        helpCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        helpCanvas.sortingOrder = 999;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObject.AddComponent<GraphicRaycaster>();

        // Dark background overlay (click to close)
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObject.transform, false);

        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0.7f);

        if (closeOnClickOutside)
        {
            Button bgButton = bgObj.AddComponent<Button>();
            bgButton.onClick.AddListener(Hide);
        }

        // Main panel
        helpPanelObject = new GameObject("HelpPanel");
        helpPanelObject.transform.SetParent(canvasObject.transform, false);

        RectTransform panelRect = helpPanelObject.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(600, 700);
        panelRect.anchoredPosition = Vector2.zero;

        Image panelBg = helpPanelObject.AddComponent<Image>();
        panelBg.color = new Color(0.12f, 0.14f, 0.18f, 0.98f);

        // Add outline
        Outline outline = helpPanelObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.3f, 0.5f, 0.7f, 1f);
        outline.effectDistance = new Vector2(3, -3);

        // Vertical layout
        VerticalLayoutGroup layout = helpPanelObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(25, 25, 20, 20);
        layout.spacing = 15;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        // === HEADER ===
        CreateHeader(helpPanelObject.transform);

        // === CONTENT SECTIONS ===
        CreateSection(helpPanelObject.transform, "ABOUT",
            "Maritime Piracy Simulation - CS 499 Senior Project\n" +
            "Simulates real-world piracy hotspots to analyze\n" +
            "maritime protection effectiveness.");

        CreateSection(helpPanelObject.transform, "CONTROLS",
            "• <b>Start</b> - Begin the simulation\n" +
            "• <b>Pause/Resume</b> - Freeze or continue time\n" +
            "• <b>Step</b> - Advance one simulation tick\n" +
            "• <b>Reset</b> - Clear and start over\n" +
            "• <b>Speed Slider</b> - Adjust simulation speed");

        CreateSection(helpPanelObject.transform, "SHIPS",
            "<color=#FFD700>● Merchant</color> - Transports valuable cargo\n" +
            "<color=#FF4444>● Pirate</color> - Hunts and captures merchants\n" +
            "<color=#4488FF>● Navy</color> - Patrols and defeats pirates\n\n" +
            "<i>Hover over ships to see detailed information!</i>");

        CreateSection(helpPanelObject.transform, "CONDITIONS",
            "<b>Time of Day:</b> Affects visibility & spawn rates\n" +
            "• Morning/Noon - Normal conditions\n" +
            "• Evening/Night - Pirates more active, lower visibility\n\n" +
            "<b>Weather:</b> Affects speed & detection\n" +
            "• Clear/Calm - Normal to good conditions\n" +
            "• Foggy - Reduced detection range\n" +
            "• Stormy - Slower ships, reduced visibility");

        CreateSection(helpPanelObject.transform, "REGIONS",
            "• <b>Strait of Malacca</b> - Singapore, Malaysia, Indonesia\n" +
            "• <b>Gulf of Aden</b> - Somalia, Yemen, Djibouti\n" +
            "• <b>Gulf of Guinea</b> - Nigeria, Cameroon, Gabon");

        // === CLOSE BUTTON ===
        CreateCloseButton(helpPanelObject.transform);

        // === HOTKEY HINT ===
        CreateHotkeyHint(helpPanelObject.transform);
    }

    void CreateHeader(Transform parent)
    {
        GameObject headerObj = new GameObject("Header");
        headerObj.transform.SetParent(parent, false);

        TextMeshProUGUI headerText = headerObj.AddComponent<TextMeshProUGUI>();
        headerText.text = "❓ HELP";
        headerText.fontSize = 32;
        headerText.fontStyle = FontStyles.Bold;
        headerText.color = new Color(0.4f, 0.7f, 1f);
        headerText.alignment = TextAlignmentOptions.Center;

        LayoutElement le = headerObj.AddComponent<LayoutElement>();
        le.preferredHeight = 50;
    }

    void CreateSection(Transform parent, string title, string content)
    {
        GameObject sectionObj = new GameObject($"Section_{title}");
        sectionObj.transform.SetParent(parent, false);

        VerticalLayoutGroup sectionLayout = sectionObj.AddComponent<VerticalLayoutGroup>();
        sectionLayout.spacing = 5;
        sectionLayout.childControlHeight = false;
        sectionLayout.childControlWidth = true;
        sectionLayout.childForceExpandHeight = false;

        // Title
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(sectionObj.transform, false);

        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = title;
        titleText.fontSize = 18;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = new Color(0.9f, 0.9f, 0.9f);

        LayoutElement titleLe = titleObj.AddComponent<LayoutElement>();
        titleLe.preferredHeight = 25;

        // Content
        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(sectionObj.transform, false);

        TextMeshProUGUI contentText = contentObj.AddComponent<TextMeshProUGUI>();
        contentText.text = content;
        contentText.fontSize = 14;
        contentText.color = new Color(0.8f, 0.8f, 0.8f);
        contentText.richText = true;
        contentText.enableWordWrapping = true;

        // Auto-size content height
        ContentSizeFitter fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    void CreateCloseButton(Transform parent)
    {
        GameObject buttonObj = new GameObject("CloseButton");
        buttonObj.transform.SetParent(parent, false);

        RectTransform btnRect = buttonObj.AddComponent<RectTransform>();
        btnRect.sizeDelta = new Vector2(150, 45);

        Image btnImage = buttonObj.AddComponent<Image>();
        btnImage.color = new Color(0.2f, 0.5f, 0.8f);

        closeButton = buttonObj.AddComponent<Button>();
        closeButton.onClick.AddListener(Hide);

        // Button hover effect
        ColorBlock colors = closeButton.colors;
        colors.highlightedColor = new Color(0.3f, 0.6f, 0.9f);
        colors.pressedColor = new Color(0.15f, 0.4f, 0.7f);
        closeButton.colors = colors;

        // Button text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI btnText = textObj.AddComponent<TextMeshProUGUI>();
        btnText.text = "Got it!";
        btnText.fontSize = 20;
        btnText.fontStyle = FontStyles.Bold;
        btnText.color = Color.white;
        btnText.alignment = TextAlignmentOptions.Center;

        LayoutElement le = buttonObj.AddComponent<LayoutElement>();
        le.preferredHeight = 50;
    }

    void CreateHotkeyHint(Transform parent)
    {
        GameObject hintObj = new GameObject("HotkeyHint");
        hintObj.transform.SetParent(parent, false);

        TextMeshProUGUI hintText = hintObj.AddComponent<TextMeshProUGUI>();
        hintText.text = "Press H or F1 to toggle help";
        hintText.fontSize = 12;
        hintText.fontStyle = FontStyles.Italic;
        hintText.color = new Color(0.5f, 0.5f, 0.5f);
        hintText.alignment = TextAlignmentOptions.Center;

        LayoutElement le = hintObj.AddComponent<LayoutElement>();
        le.preferredHeight = 20;
    }
}