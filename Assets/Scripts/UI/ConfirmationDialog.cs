using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// Confirmation Dialog - Shows "Are you sure?" prompts
/// 
/// USE:
///   ConfirmationDialog.Instance.Show(
///       "Reset Simulation?",
///       "This will lose your current run progress.",
///       onConfirm: () => DoReset(),
///       onCancel: () => { } // optional
///   );
/// 
/// SETUP:
/// 1. Add this to a GameObject in your scene
/// 2. It auto-creates the UI
/// 3. Call Show() whenever you need confirmation
/// 
/// Your UI teammate can restyle the visuals later!
/// </summary>
public class ConfirmationDialog : MonoBehaviour
{
    public static ConfirmationDialog Instance { get; private set; }

    [Header("=== STYLING (Optional) ===")]
    public Color backgroundColor = new Color(0, 0, 0, 0.7f);
    public Color panelColor = new Color(0.15f, 0.15f, 0.2f, 0.95f);
    public Color confirmButtonColor = new Color(0.2f, 0.5f, 0.3f);
    public Color cancelButtonColor = new Color(0.5f, 0.3f, 0.3f);

    // UI Elements (auto-created or assign custom)
    private GameObject canvasObject;
    private GameObject panelObject;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI messageText;
    private Button confirmButton;
    private Button cancelButton;

    // Callbacks
    private Action onConfirmCallback;
    private Action onCancelCallback;

    void Awake()
    {
        Instance = this;
        Debug.Log("=== ConfirmationDialog Awake - Instance set! ===");
    }

    void Start()
    {
        CreateDialogUI();
        Hide();
    }

    void Update()
    {
        // ESC to cancel
        if (canvasObject != null && canvasObject.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                OnCancelClicked();
            }
            // Enter to confirm
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                OnConfirmClicked();
            }
        }
    }

    /// <summary>
    /// Show confirmation dialog
    /// </summary>
    /// <param name="title">Dialog title (e.g., "Reset Simulation?")</param>
    /// <param name="message">Explanation (e.g., "This will lose current progress.")</param>
    /// <param name="onConfirm">Called if user clicks Yes/Confirm</param>
    /// <param name="onCancel">Called if user clicks No/Cancel (optional)</param>
    /// <param name="confirmText">Text for confirm button (default: "Yes")</param>
    /// <param name="cancelText">Text for cancel button (default: "No")</param>
    public void Show(string title, string message, Action onConfirm, Action onCancel = null, 
                     string confirmText = "Yes", string cancelText = "No")
    {
        onConfirmCallback = onConfirm;
        onCancelCallback = onCancel;

        if (titleText != null) titleText.text = title;
        if (messageText != null) messageText.text = message;

        // Update button text
        if (confirmButton != null)
        {
            var btnText = confirmButton.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null) btnText.text = confirmText;
        }
        if (cancelButton != null)
        {
            var btnText = cancelButton.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null) btnText.text = cancelText;
        }

        if (canvasObject != null)
            canvasObject.SetActive(true);
    }

    /// <summary>
    /// Quick show with just message
    /// </summary>
    public void Show(string message, Action onConfirm)
    {
        Show("Confirm", message, onConfirm);
    }

    public void Hide()
    {
        if (canvasObject != null)
            canvasObject.SetActive(false);
    }

    public bool IsShowing()
    {
        return canvasObject != null && canvasObject.activeSelf;
    }

    void OnConfirmClicked()
    {
        Hide();
        onConfirmCallback?.Invoke();
    }

    void OnCancelClicked()
    {
        Hide();
        onCancelCallback?.Invoke();
    }

    void CreateDialogUI()
    {
        // Canvas
        canvasObject = new GameObject("ConfirmationDialogCanvas");
        canvasObject.transform.SetParent(transform);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // Above everything

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObject.AddComponent<GraphicRaycaster>();

        // Dark background (blocks clicks)
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObject.transform, false);

        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = backgroundColor;

        // Click background to cancel
        Button bgButton = bgObj.AddComponent<Button>();
        bgButton.onClick.AddListener(OnCancelClicked);
        ColorBlock bgColors = bgButton.colors;
        bgColors.highlightedColor = backgroundColor;
        bgColors.pressedColor = backgroundColor;
        bgButton.colors = bgColors;

        // Panel
        panelObject = new GameObject("Panel");
        panelObject.transform.SetParent(canvasObject.transform, false);

        RectTransform panelRect = panelObject.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(400, 200);
        panelRect.anchoredPosition = Vector2.zero;

        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = panelColor;

        // Add outline
        Outline outline = panelObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.4f, 0.4f, 0.5f, 1f);
        outline.effectDistance = new Vector2(2, -2);

        // Layout
        VerticalLayoutGroup layout = panelObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 20, 20);
        layout.spacing = 15;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        // Title
        titleText = CreateText(panelObject.transform, "Confirm Action", 24, FontStyles.Bold, Color.white);
        titleText.GetComponent<LayoutElement>().preferredHeight = 35;

        // Message
        messageText = CreateText(panelObject.transform, "Are you sure?", 18, FontStyles.Normal, new Color(0.8f, 0.8f, 0.8f));
        messageText.GetComponent<LayoutElement>().preferredHeight = 50;

        // Button row
        GameObject buttonRow = new GameObject("Buttons");
        buttonRow.transform.SetParent(panelObject.transform, false);

        HorizontalLayoutGroup buttonLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
        buttonLayout.spacing = 20;
        buttonLayout.childAlignment = TextAnchor.MiddleCenter;
        buttonLayout.childControlHeight = true;
        buttonLayout.childControlWidth = true;
        buttonLayout.childForceExpandHeight = false;
        buttonLayout.childForceExpandWidth = false;

        LayoutElement buttonRowLE = buttonRow.AddComponent<LayoutElement>();
        buttonRowLE.preferredHeight = 45;

        // Confirm button
        confirmButton = CreateButton(buttonRow.transform, "Yes", confirmButtonColor, OnConfirmClicked);

        // Cancel button
        cancelButton = CreateButton(buttonRow.transform, "No", cancelButtonColor, OnCancelClicked);
    }

    TextMeshProUGUI CreateText(Transform parent, string text, int fontSize, FontStyles style, Color color)
    {
        GameObject obj = new GameObject("Text");
        obj.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;

        obj.AddComponent<LayoutElement>();

        return tmp;
    }

    Button CreateButton(Transform parent, string text, Color bgColor, Action onClick)
    {
        GameObject btnObj = new GameObject($"Button_{text}");
        btnObj.transform.SetParent(parent, false);

        Image btnImage = btnObj.AddComponent<Image>();
        btnImage.color = bgColor;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnImage;
        btn.onClick.AddListener(() => onClick());

        LayoutElement le = btnObj.AddComponent<LayoutElement>();
        le.preferredWidth = 100;
        le.preferredHeight = 40;

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
        btnText.fontSize = 18;
        btnText.fontStyle = FontStyles.Bold;
        btnText.color = Color.white;
        btnText.alignment = TextAlignmentOptions.Center;

        return btn;
    }
}