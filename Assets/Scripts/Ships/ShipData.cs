using UnityEngine;

public enum ShipType
{
    Cargo,
    Pirate,
    Security
}

public enum ShipState
{
    Idle,
    Moving,
    Attacked,
    Captured,
    Escaped,
    Sunk,
    Exited
}

[System.Serializable]
public class ShipData
{
    public string shipId;
    public ShipType type;
    public ShipState state;

    public Vector2 position;
    public Vector2 velocityDir;
    public float speedUnitsPerTick;

    public ShipRoute route;
}
