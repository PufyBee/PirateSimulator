using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Runtime Stats Overlay - Self-contained statistics display
/// 
/// Creates its own UI overlay showing live stats during simulation.
/// Works independently of any existing UI setup.
/// 
/// SETUP:
/// 1. Add this to any GameObject
/// 2. Assign the SimulationEngine reference
/// 3. That's it - creates its own Canvas and display
/// 
/// FEATURES:
/// - Shows active ship counts
/// - Shows outcomes (escaped, captured, defeated)
/// - Shows protection rate with color coding
/// - Shows current environment conditions
/// - Auto-hides during setup, shows during run
/// </summary>
public class RuntimeStatsOverlay : MonoBehaviour
{
    public static RuntimeStatsOverlay Instance { get; private set; }

    [Header("=== REFERENCES ===")]
    public SimulationEngine engine;

    [Header("=== POSITION ===")]
    [Tooltip("Where to anchor the overlay")]
    public OverlayPosition position = OverlayPosition.TopLeft;
    public Vector2 offset = new Vector2(10, -10);

    [Header("=== APPEARANCE ===")]
    public Color backgroundColor = new Color(0, 0, 0, 0.7f);
    public Color textColor = Color.white;
    public int fontSize = 16;

    public enum OverlayPosition
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    // Created UI elements
    private GameObject canvasObject;
    private GameObject panelObject;
    private TMP_Text statsText;
    private bool isShowing = false;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        CreateOverlay();
        Hide(); // Start hidden
    }

    void Update()
    {
        if (isShowing && statsText != null)
        {
            UpdateStats();
        }
    }

    /// <summary>
    /// Show the overlay (call when simulation starts)
    /// </summary>
    public void Show()
    {
        if (panelObject != null)
            panelObject.SetActive(true);
        isShowing = true;
    }

    /// <summary>
    /// Hide the overlay (call when returning to setup)
    /// </summary>
    public void Hide()
    {
        if (panelObject != null)
            panelObject.SetActive(false);
        isShowing = false;
    }

    void CreateOverlay()
    {
        // Create Canvas
        canvasObject = new GameObject("RuntimeStatsCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // On top of other UI

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObject.AddComponent<GraphicRaycaster>();

        // Create Panel
        panelObject = new GameObject("StatsPanel");
        panelObject.transform.SetParent(canvasObject.transform, false);

        RectTransform panelRect = panelObject.AddComponent<RectTransform>();
        SetAnchorPosition(panelRect);
        panelRect.sizeDelta = new Vector2(200, 220);  // Smaller panel

        Image panelBg = panelObject.AddComponent<Image>();
        panelBg.color = backgroundColor;

        // Add padding via VerticalLayoutGroup
        VerticalLayoutGroup layout = panelObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 10, 10);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;

        // Create stats text
        GameObject textObj = new GameObject("StatsText");
        textObj.transform.SetParent(panelObject.transform, false);

        statsText = textObj.AddComponent<TextMeshProUGUI>();
        statsText.fontSize = fontSize;
        statsText.color = textColor;
        statsText.alignment = TextAlignmentOptions.TopLeft;

        LayoutElement le = textObj.AddComponent<LayoutElement>();
        le.preferredHeight = 260;

        // Initial text
        statsText.text = "Initializing...";
    }

    void SetAnchorPosition(RectTransform rect)
    {
        switch (position)
        {
            case OverlayPosition.TopLeft:
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(Mathf.Abs(offset.x), -Mathf.Abs(offset.y));
                break;
            case OverlayPosition.TopRight:
                rect.anchorMin = new Vector2(1, 1);
                rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(1, 1);
                rect.anchoredPosition = new Vector2(-Mathf.Abs(offset.x), -Mathf.Abs(offset.y));
                break;
            case OverlayPosition.BottomLeft:
                rect.anchorMin = new Vector2(0, 0);
                rect.anchorMax = new Vector2(0, 0);
                rect.pivot = new Vector2(0, 0);
                rect.anchoredPosition = new Vector2(Mathf.Abs(offset.x), Mathf.Abs(offset.y));
                break;
            case OverlayPosition.BottomRight:
                rect.anchorMin = new Vector2(1, 0);
                rect.anchorMax = new Vector2(1, 0);
                rect.pivot = new Vector2(1, 0);
                rect.anchoredPosition = new Vector2(-Mathf.Abs(offset.x), Mathf.Abs(offset.y));
                break;
        }
    }

    void UpdateStats()
    {
        if (engine == null)
        {
            // Try to find engine
            engine = FindObjectOfType<SimulationEngine>();
            if (engine == null)
            {
                statsText.text = "No SimulationEngine found";
                return;
            }
        }

        // Get ship counts
        int merchants = 0, pirates = 0, security = 0;
        if (ShipSpawner.Instance != null)
        {
            foreach (var ship in ShipSpawner.Instance.GetActiveShips())
            {
                if (ship == null || ship.Data == null) continue;
                switch (ship.Data.type)
                {
                    case ShipType.Cargo: merchants++; break;
                    case ShipType.Pirate: pirates++; break;
                    case ShipType.Security: security++; break;
                }
            }
        }

        // Get outcomes
        int escaped = engine.GetMerchantsExited();
        int captured = engine.GetMerchantsCaptured();
        int defeated = engine.GetPiratesDefeated();

        // Get conditions
        string conditions = "Normal";
        string effects = "";
        if (EnvironmentSettings.Instance != null)
        {
            conditions = EnvironmentSettings.Instance.GetConditionsSummary();
            float detMult = EnvironmentSettings.Instance.DetectionMultiplier;
            float spdMult = EnvironmentSettings.Instance.SpeedMultiplier;
            if (detMult != 1f || spdMult != 1f)
            {
                effects = $"\n<size=12><color=#888888>Detection: {detMult:P0} | Speed: {spdMult:P0}</color></size>";
            }
        }

        // Get tick count
        int ticks = engine.GetTickCount();

        // Build display string - NO GRADE OR PROTECTION RATE
        statsText.text = 
            $"<b>═══ LIVE STATS ═══</b>\n" +
            $"<color=#AAAAAA>Tick: {ticks}</color>\n" +
            $"<color=#AAAAAA>{conditions}</color>{effects}\n" +
            $"\n" +
            $"<b>ACTIVE SHIPS</b>\n" +
            $"<color=#44AA44>Merchants: {merchants}</color>\n" +
            $"<color=#AA4444>Pirates: {pirates}</color>\n" +
            $"<color=#4444AA>Security: {security}</color>\n" +
            $"\n" +
            $"<b>OUTCOMES</b>\n" +
            $"<color=#44FF44>Escaped: {escaped}</color>\n" +
            $"<color=#FF4444>Captured: {captured}</color>\n" +
            $"<color=#44FFFF>Defeated: {defeated}</color>";
    }

    // Keep these for potential future use but they're not called anymore
    string GetGrade(float rate)
    {
        if (rate >= 90) return "A+";
        if (rate >= 80) return "A";
        if (rate >= 70) return "B";
        if (rate >= 60) return "C";
        if (rate >= 50) return "D";
        return "F";
    }

    string GetGradeColorHex(float rate)
    {
        if (rate >= 80) return "#44FF44"; // Green
        if (rate >= 70) return "#AAFF44"; // Yellow-green
        if (rate >= 60) return "#FFFF44"; // Yellow
        if (rate >= 50) return "#FFAA44"; // Orange
        return "#FF4444"; // Red
    }
}