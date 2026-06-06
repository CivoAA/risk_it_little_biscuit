using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class RepeatingImageScroller : MonoBehaviour
{
    [Header("📜 Scroll Settings")]
    [Tooltip("Direction to scroll the pattern (e.g., -1,0 for left, 0,1 for up)")]
    public Vector2 scrollDirection = new Vector2(-1f, 0f);

    [Tooltip("Speed of the scrolling effect")]
    public float scrollSpeed = 1f;

    [Header("📐 Tiling Settings")]
    [Tooltip("How many times to tile the pattern")]
    public Vector2 tiling = new Vector2(1f, 1f);

    private RawImage targetImage;
    private Vector2 currentOffset;

    void Start()
    {
        targetImage = GetComponent<RawImage>();

        if (targetImage == null)
        {
            Debug.LogError("RepeatingImageScroller requires a RawImage component!");
            enabled = false;
            return;
        }

        if (targetImage.texture != null)
        {
            targetImage.texture.wrapMode = TextureWrapMode.Repeat;
        }

        targetImage.uvRect = new Rect(0, 0, tiling.x, tiling.y);
        currentOffset = Vector2.zero;
    }

    void Update()
    {
        if (targetImage == null || targetImage.texture == null) return;

        currentOffset += scrollDirection.normalized * scrollSpeed * Time.unscaledDeltaTime;

        targetImage.uvRect = new Rect(currentOffset.x, currentOffset.y, tiling.x, tiling.y);
    }

    public void SetScrollDirection(Vector2 direction)
    {
        scrollDirection = direction;
    }

    public void SetScrollSpeed(float speed)
    {
        scrollSpeed = speed;
    }

    public void SetTiling(Vector2 newTiling)
    {
        tiling = newTiling;
        if (targetImage != null)
        {
            targetImage.uvRect = new Rect(currentOffset.x, currentOffset.y, tiling.x, tiling.y);
        }
    }
}
