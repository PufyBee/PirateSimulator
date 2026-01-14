using UnityEngine;

public class GridMover : MonoBehaviour
{
    public Vector2Int gridPos;

    public void SetPosition(Vector2Int pos)
    {
        gridPos = pos;
        UpdateWorld();
    }

    public void Move(Vector2Int delta)
    {
        gridPos += delta;
        UpdateWorld();
    }

    void UpdateWorld()
    {
        transform.position = new Vector3(gridPos.x, gridPos.y, 0);
    }
}
