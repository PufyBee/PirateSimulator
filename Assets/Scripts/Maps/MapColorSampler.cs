using UnityEngine;

public class MapColorSampler : MonoBehaviour
{
    public static MapColorSampler Instance { get; private set; }

    [Header("Assign the SpriteRenderer of your pretty map")]
    public SpriteRenderer mapRenderer;

    [Header("Water detection (tweak if needed)")]
    [Tooltip("Water if Blue is greater than Green+offset and Red+offset.")]
    public float blueDominanceOffset = 0.05f;

    [Tooltip("Optional: require a minimum blue brightness.")]
    public float minBlue = 0.20f;

    private Texture2D tex;
    private Bounds worldBounds;
    private int width;
    private int height;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (mapRenderer == null || mapRenderer.sprite == null)
        {
            Debug.LogError("MapColorSampler: assign mapRenderer with a sprite.");
            enabled = false;
            return;
        }

        tex = mapRenderer.sprite.texture;
        width = tex.width;
        height = tex.height;
        worldBounds = mapRenderer.bounds;
    }

    public bool IsWater(Vector2 worldPos)
    {
        if (tex == null) return true;

        float u = Mathf.InverseLerp(worldBounds.min.x, worldBounds.max.x, worldPos.x);
        float v = Mathf.InverseLerp(worldBounds.min.y, worldBounds.max.y, worldPos.y);

        // Outside map = treat as water (so ships can "exit")
        if (u < 0f || u > 1f || v < 0f || v > 1f)
            return true;

        int x = Mathf.Clamp(Mathf.RoundToInt(u * (width - 1)), 0, width - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(v * (height - 1)), 0, height - 1);

        Color c = tex.GetPixel(x, y);

        // "Pretty map" rule: water looks blue
        bool blueDominant = (c.b > c.g + blueDominanceOffset) && (c.b > c.r + blueDominanceOffset);
        bool blueEnough = c.b >= minBlue;

        return blueDominant && blueEnough;
    }
}
