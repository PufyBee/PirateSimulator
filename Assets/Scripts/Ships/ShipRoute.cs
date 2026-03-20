using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ShipRoute
{
    public List<Vector2> waypoints = new();
    public int currentIndex = 0;

    public bool HasWaypoint => waypoints != null && waypoints.Count > 0 && currentIndex < waypoints.Count;
    public Vector2 Current => waypoints[currentIndex];

    public void Advance()
    {
        currentIndex++;
    }
}
