using UnityEngine;

public class MapData : MonoBehaviour
{
    public SpriteRenderer mapRenderer;

    private Texture2D mapTexture;
    private Bounds bounds;

    // Define what counts as water
    [Header("Water Detection")]
    public float waterBlueThreshold = 0.4f;
    public float landGreenThreshold = 0.3f;

    void Awake()
    {
        mapTexture = mapRenderer.sprite.texture;
        bounds = mapRenderer.bounds;
    }

    public bool IsWaterWorldPosition(Vector2 worldPos)
    {
        // Convert world → local → UV
        Vector2 localPos = worldPos - (Vector2)bounds.min;
        Vector2 size = bounds.size;

        float u = localPos.x / size.x;
        float v = localPos.y / size.y;

        // Outside map = invalid
        if (u < 0 || u > 1 || v < 0 || v > 1)
            return false;

        int x = Mathf.FloorToInt(u * mapTexture.width);
        int y = Mathf.FloorToInt(v * mapTexture.height);

        Color c = mapTexture.GetPixel(x, y);

        // Simple rule: blue = water, green = land
        return c.b > waterBlueThreshold && c.g < landGreenThreshold;
    }
}
