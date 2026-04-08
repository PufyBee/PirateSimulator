using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Runtime Stats Overlay - Self-contained statistics display
/// 
/// FIXED: Uses engine.GetActiveShips() instead of ShipSpawner.GetActiveShips()
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
        Hide();
    }

    void Update()
    {
        if (isShowing && statsText != null)
        {
            UpdateStats();
        }
    }

    public void Show()
    {
        if (panelObject != null)
            panelObject.SetActive(true);
        isShowing = true;
    }

    public void Hide()
    {
        if (panelObject != null)
            panelObject.SetActive(false);
        isShowing = false;
    }

    void CreateOverlay()
    {
        canvasObject = new GameObject("RuntimeStatsCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObject.AddComponent<GraphicRaycaster>();

        panelObject = new GameObject("StatsPanel");
        panelObject.transform.SetParent(canvasObject.transform, false);

        RectTransform panelRect = panelObject.AddComponent<RectTransform>();
        SetAnchorPosition(panelRect);
        panelRect.sizeDelta = new Vector2(200, 290);

        Image panelBg = panelObject.AddComponent<Image>();
        panelBg.color = backgroundColor;

        VerticalLayoutGroup layout = panelObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 10, 10);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;

        GameObject textObj = new GameObject("StatsText");
        textObj.transform.SetParent(panelObject.transform, false);

        statsText = textObj.AddComponent<TextMeshProUGUI>();
        statsText.fontSize = fontSize;
        statsText.color = textColor;
        statsText.alignment = TextAlignmentOptions.TopLeft;

        LayoutElement le = textObj.AddComponent<LayoutElement>();
        le.preferredHeight = 350;

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
            engine = FindObjectOfType<SimulationEngine>();
            if (engine == null)
            {
                statsText.text = "No SimulationEngine found";
                return;
            }
        }

        // FIXED: Use engine.GetActiveShips() instead of ShipSpawner
        int merchants = 0, pirates = 0, security = 0;
        if (engine != null)
        {
            foreach (var ship in engine.GetActiveShips())
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

        int escaped = engine.GetMerchantsExited();
        int captured = engine.GetMerchantsCaptured();
        int defeated = engine.GetPiratesDefeated();

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

        int ticks = engine.GetTickCount();
        float simHours = engine.GetSimulatedHours();
        int simDays = Mathf.FloorToInt(simHours / 24f);
        int displayHours = Mathf.FloorToInt(simHours % 24f);

        statsText.text = 
            $"<b>═══ LIVE STATS ═══</b>\n" +
            $"<color=#AAAAAA>Tick: {ticks}</color>\n" +
            $"<color=#AAAAAA>Elapsed: Day {simDays + 1}, {displayHours}h</color>\n" +
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
        if (rate >= 80) return "#44FF44";
        if (rate >= 70) return "#AAFF44";
        if (rate >= 60) return "#FFFF44";
        if (rate >= 50) return "#FFAA44";
        return "#FF4444";
    }
}