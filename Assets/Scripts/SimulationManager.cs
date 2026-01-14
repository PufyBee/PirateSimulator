using UnityEngine;
using TMPro;

public class SimulationManager : MonoBehaviour
{
    public TMP_Text statusText;

    public GameObject cargoPrefab;

    private GameObject cargoInstance;
    private bool running = false;

    void Update()
    {
        if (running)
        {
            Tick();
            running = false; // one tick per frame for now (simple)
        }
    }

    public void StartRun()
    {
        SetStatus("RUNNING");

        if (cargoInstance == null)
            SpawnCargo();

        running = true;
    }

    public void PauseRun()
    {
        SetStatus("PAUSED");
        running = false;
    }

    public void StepOnce()
    {
        if (cargoInstance == null)
            SpawnCargo();

        Tick();
        SetStatus("STEP");
    }

    public void EndRun()
    {
        SetStatus("ENDED");
        running = false;
    }

    void SpawnCargo()
    {
        cargoInstance = Instantiate(cargoPrefab);
        var mover = cargoInstance.GetComponent<GridMover>();
        mover.SetPosition(new Vector2Int(0, 0));
    }

    void Tick()
    {
        var mover = cargoInstance.GetComponent<GridMover>();
        mover.Move(Vector2Int.right);
    }

    void SetStatus(string msg)
    {
        statusText.text = msg;
        Debug.Log(msg);
    }
}
