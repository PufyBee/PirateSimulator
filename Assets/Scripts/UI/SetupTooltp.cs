using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// SETUP TOOLTIP - Hover over any UI element to show a label
/// 
/// Attach this to any setup icon (ship sprites, weather icons, etc.)
/// Set the tooltipText in the Inspector.
/// Shows a small label near the mouse on hover.
/// 
/// SETUP:
/// 1. Add this component to a UI image/button
/// 2. Set tooltipText to e.g. "Merchant Ship" or "Pirate Ship"
/// 3. Done - hover shows the label
/// </summary>
public class SetupTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("=== TOOLTIP TEXT ===")]
    [Tooltip("Text shown on hover")]
    public string tooltipText = "Label";

    private static GameObject tooltipObject;
    private static TMP_Text tooltipLabel;
    private static Canvas tooltipCanvas;
    private static bool isInitialized = false;

    void Start()
    {
        if (!isInitialized)
        {
            CreateSharedTooltip();
            isInitialized = true;
        }
    }

    static void CreateSharedTooltip()
    {
        // Canvas
        GameObject canvasObj = new GameObject("SetupTooltipCanvas");
        tooltipCanvas = canvasObj.AddComponent<Canvas>();
        tooltipCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        tooltipCanvas.sortingOrder = 1001;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();

        // Tooltip panel
        tooltipObject = new GameObject("Tooltip");
        tooltipObject.transform.SetParent(canvasObj.transform, false);

        RectTransform tipRect = tooltipObject.AddComponent<RectTransform>();
        tipRect.pivot = new Vector2(0.5f, 0);
        tipRect.sizeDelta = new Vector2(160, 32);

        Image bg = tooltipObject.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.1f, 0.14f, 0.95f);
        bg.raycastTarget = false;

        Outline outline = tooltipObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.83f, 0.64f, 0.3f, 0.7f);
        outline.effectDistance = new Vector2(1, -1);

        // Text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(tooltipObject.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8, 2);
        textRect.offsetMax = new Vector2(-8, -2);

        tooltipLabel = textObj.AddComponent<TextMeshProUGUI>();
        tooltipLabel.fontSize = 14;
        tooltipLabel.fontStyle = FontStyles.Bold;
        tooltipLabel.color = new Color(0.9f, 0.85f, 0.7f);
        tooltipLabel.alignment = TextAlignmentOptions.Center;
        tooltipLabel.raycastTarget = false;
        tooltipLabel.enableAutoSizing = false;

        tooltipObject.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (tooltipObject == null) return;

        tooltipLabel.text = tooltipText;

        // Size to fit text
        float textWidth = tooltipLabel.GetPreferredValues(tooltipText).x + 20f;
        tooltipObject.GetComponent<RectTransform>().sizeDelta = new Vector2(Mathf.Max(80, textWidth), 32);

        tooltipObject.SetActive(true);
        UpdatePosition(eventData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltipObject != null)
            tooltipObject.SetActive(false);
    }

    void Update()
    {
        if (tooltipObject != null && tooltipObject.activeSelf)
        {
            // Follow mouse
            RectTransform tipRect = tooltipObject.GetComponent<RectTransform>();
            Vector2 mousePos = Input.mousePosition;

            // Convert to canvas space
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                tooltipCanvas.GetComponent<RectTransform>(),
                mousePos,
                null,
                out Vector2 localPoint
            );

            tipRect.anchoredPosition = localPoint + new Vector2(0, 30);
        }
    }

    void UpdatePosition(PointerEventData eventData)
    {
        if (tooltipObject == null || tooltipCanvas == null) return;

        RectTransform tipRect = tooltipObject.GetComponent<RectTransform>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            tooltipCanvas.GetComponent<RectTransform>(),
            eventData.position,
            null,
            out Vector2 localPoint
        );

        tipRect.anchoredPosition = localPoint + new Vector2(0, 30);
    }

    void OnDestroy()
    {
        if (tooltipObject != null && !tooltipObject.activeSelf)
        {
            // Don't destroy shared tooltip unless scene is unloading
        }
    }
}