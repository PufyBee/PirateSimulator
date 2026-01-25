using UnityEngine;

/// <summary>
/// Coordinate Helper - Click anywhere to see world coordinates
/// 
/// USE:
/// 1. Add to any GameObject
/// 2. Enter Play mode
/// 3. Click on the map
/// 4. Coordinates appear in Console AND on screen
/// 
/// DELETE THIS after you're done setting up spawn zones!
/// </summary>
public class CoordinateHelper : MonoBehaviour
{
    [Header("=== LAST CLICKED POSITION ===")]
    public Vector2 lastClickedPosition;
    
    [Header("=== SETTINGS ===")]
    public bool showOnScreen = true;
    public KeyCode copyKey = KeyCode.C;  // Press C to copy to clipboard
    
    private string displayText = "Click on map to get coordinates";
    private GUIStyle guiStyle;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mousePos = Input.mousePosition;
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(mousePos);
            worldPos.z = 0;
            
            lastClickedPosition = new Vector2(worldPos.x, worldPos.y);
            
            displayText = $"Clicked: ({worldPos.x:F1}, {worldPos.y:F1})";
            Debug.Log($"=== COORDINATES === X: {worldPos.x:F1}, Y: {worldPos.y:F1}");
        }
        
        // Press C to copy coordinates
        if (Input.GetKeyDown(copyKey) && lastClickedPosition != Vector2.zero)
        {
            string coords = $"new Vector2({lastClickedPosition.x:F1}f, {lastClickedPosition.y:F1}f)";
            GUIUtility.systemCopyBuffer = coords;
            Debug.Log($"Copied to clipboard: {coords}");
            displayText = "Copied to clipboard!";
        }
    }

    void OnGUI()
    {
        if (!showOnScreen) return;
        
        if (guiStyle == null)
        {
            guiStyle = new GUIStyle(GUI.skin.box);
            guiStyle.fontSize = 18;
            guiStyle.alignment = TextAnchor.MiddleCenter;
            guiStyle.normal.textColor = Color.white;
        }
        
        // Display box at top of screen
        GUI.Box(new Rect(10, 10, 350, 60), 
            $"{displayText}\n[Press C to copy as Vector2]", 
            guiStyle);
    }

    // Draw crosshair at last clicked position
    void OnDrawGizmos()
    {
        if (lastClickedPosition == Vector2.zero) return;
        
        Gizmos.color = Color.yellow;
        float size = 5f;
        
        // Draw crosshair
        Gizmos.DrawLine(
            new Vector3(lastClickedPosition.x - size, lastClickedPosition.y, 0),
            new Vector3(lastClickedPosition.x + size, lastClickedPosition.y, 0)
        );
        Gizmos.DrawLine(
            new Vector3(lastClickedPosition.x, lastClickedPosition.y - size, 0),
            new Vector3(lastClickedPosition.x, lastClickedPosition.y + size, 0)
        );
    }
}