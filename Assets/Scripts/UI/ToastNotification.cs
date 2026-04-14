using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// TOAST NOTIFICATION SYSTEM
/// 
/// Displays brief, non-blocking messages to inform the user about
/// actions taken by the system (input clamping, invalid actions, etc.)
/// 
/// Satisfies Req 5.4 by providing actionable error messages in response
/// to invalid user input.
/// 
/// USAGE:
/// - Add this component to any GameObject in the scene
/// - Call ToastNotification.Show("message") from anywhere
/// - Toast fades in, displays for ~2.5 seconds, fades out
/// 
/// EXAMPLES:
/// - ToastNotification.Show("Value capped at 50");
/// - ToastNotification.Show("Letters not allowed — integers only");
/// </summary>
public class ToastNotification : MonoBehaviour
{
    public static ToastNotification Instance { get; private set; }

    [Header("=== APPEARANCE ===")]
    public Color backgroundColor = new Color(0.1f, 0.15f, 0.2f, 0.95f);
    public Color textColor = new Color(1f, 0.85f, 0.3f); // Amber warning color
    public int fontSize = 20;

    [Header("=== TIMING ===")]
    public float displayDuration = 2.5f;
    public float fadeInDuration = 0.2f;
    public float fadeOutDuration = 0.4f;

    private GameObject canvasObject;
    private GameObject toastPanel;
    private CanvasGroup canvasGroup;
    private TMP_Text messageText;
    private Coroutine activeToast;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        CreateToastUI();
    }

    void CreateToastUI()
    {
        // Create canvas
        canvasObject = new GameObject("ToastCanvas");
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // Very high, above everything

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObject.AddComponent<GraphicRaycaster>();

        // Create toast panel (anchored bottom-center)
        toastPanel = new GameObject("ToastPanel");
        toastPanel.transform.SetParent(canvasObject.transform, false);

        RectTransform panelRect = toastPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0);
        panelRect.anchorMax = new Vector2(0.5f, 0);
        panelRect.pivot = new Vector2(0.5f, 0);
        panelRect.anchoredPosition = new Vector2(0, 80); // 80px from bottom
        panelRect.sizeDelta = new Vector2(500, 60);

        Image bg = toastPanel.AddComponent<Image>();
        bg.color = backgroundColor;

        // Add border/outline
        Outline outline = toastPanel.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.85f, 0.3f, 0.8f);
        outline.effectDistance = new Vector2(2, -2);

        // Canvas group for fade animation
        canvasGroup = toastPanel.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        // Create text
        GameObject textObj = new GameObject("Message");
        textObj.transform.SetParent(toastPanel.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(15, 5);
        textRect.offsetMax = new Vector2(-15, -5);

        messageText = textObj.AddComponent<TextMeshProUGUI>();
        messageText.text = "";
        messageText.fontSize = fontSize;
        messageText.fontStyle = FontStyles.Bold;
        messageText.color = textColor;
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.enableWordWrapping = true;
    }

    /// <summary>
    /// Display a toast notification with the given message.
    /// Can be called from anywhere via ToastNotification.Show(...).
    /// </summary>
    public static void Show(string message)
    {
        if (Instance == null)
        {
            // Auto-create if missing
            GameObject go = new GameObject("ToastNotification");
            Instance = go.AddComponent<ToastNotification>();
        }
        Instance.DisplayToast(message);
    }

    public void DisplayToast(string message)
    {
        if (messageText == null) return;

        messageText.text = message;

        // Stop any existing animation and restart
        if (activeToast != null)
            StopCoroutine(activeToast);

        activeToast = StartCoroutine(ToastAnimation());
    }

    IEnumerator ToastAnimation()
    {
        // Fade in
        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, t / fadeInDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;

        // Hold
        yield return new WaitForSecondsRealtime(displayDuration);

        // Fade out
        t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, t / fadeOutDuration);
            yield return null;
        }
        canvasGroup.alpha = 0f;

        activeToast = null;
    }
}