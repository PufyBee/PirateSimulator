using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Main Menu Controller
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("=== BUTTONS ===")]
    public Button startButton;
    public Button settingsButton;
    public Button quitButton;

    [Header("=== PANELS ===")]
    public GameObject settingsPanel;  // Optional: if you have a settings panel

    [Header("=== SETTINGS ===")]
    public string simulationSceneName = "ShipTestScene";

    void Start()
    {
        if (startButton)
            startButton.onClick.AddListener(OnStartClicked);
        
        if (settingsButton)
            settingsButton.onClick.AddListener(OnSettingsClicked);
        
        if (quitButton)
            quitButton.onClick.AddListener(OnQuitClicked);

        // Hide settings panel at start
        if (settingsPanel)
            settingsPanel.SetActive(false);
    }

    void OnStartClicked()
    {
        Debug.Log($"Loading scene: {simulationSceneName}");
        SceneManager.LoadScene(simulationSceneName);
    }

    void OnSettingsClicked()
    {
        Debug.Log("Settings clicked");
        
        // Toggle settings panel if you have one
        if (settingsPanel)
            settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    void OnQuitClicked()
    {
        Debug.Log("Quitting...");
        Application.Quit();
        
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}