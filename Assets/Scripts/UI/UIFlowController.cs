using UnityEngine;
using UnityEngine.UI;

public class UIFlowController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject setupPanel;
    public GameObject speedPanel;
    public GameObject statsPanel;
    public GameObject timePanel;

    [Header("Buttons")]
    public Button startButton;
    public Button stepButton;
    public Button pauseButton;
    public Button restartButton;

    private bool runActive = false;

    void Start()
    {
        EnterSetupMode();
    }

    public void EnterSetupMode()
    {
        runActive = false;

        if (setupPanel) setupPanel.SetActive(true);
        if (speedPanel) speedPanel.SetActive(false);
        if (statsPanel) statsPanel.SetActive(false);
        if (timePanel) timePanel.SetActive(false);

        // Only Start is meaningful before run begins
        if (startButton) startButton.interactable = true;
        if (stepButton) stepButton.interactable = false;
        if (pauseButton) pauseButton.interactable = false;

        // Restart becomes "New Run" vibe (optional)
        if (restartButton) restartButton.interactable = true;
    }

    public void EnterRunMode()
    {
        runActive = true;

        if (setupPanel) setupPanel.SetActive(false);
        if (speedPanel) speedPanel.SetActive(true);
        if (statsPanel) statsPanel.SetActive(true);
        if (timePanel) timePanel.SetActive(true);

        if (startButton) startButton.interactable = false; // start already used
        if (stepButton) stepButton.interactable = true;
        if (pauseButton) pauseButton.interactable = true;
        if (restartButton) restartButton.interactable = true;
    }

    // Hook this to your Start button OnClick
    public void OnStartPressed()
    {
        // You will also call SimulationEngine.StartRun() here later.
        EnterRunMode();
    }

    // Hook this to your Restart/NewRun button OnClick
    public void OnNewRunPressed()
    {
        // You will also call SimulationEngine.ResetRun() here later.
        EnterSetupMode();
    }
}
