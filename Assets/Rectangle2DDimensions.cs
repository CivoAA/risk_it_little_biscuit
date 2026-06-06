using UnityEngine;

public class Rectangle2DDimensions : MonoBehaviour
{
    void Start()
    {
        // Variante 1: SpriteRenderer (für normale Sprites)
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Vector2 size = sr.bounds.size; // Weltgröße
            float width = size.x;
            float height = size.y;

            Debug.Log($"Width: {width}, Height: {height}");
            Debug.Log($"Seitenverhältnis (Width:Height) = {width / height:F2}");
            return;
        }

        // Variante 2: RectTransform (für UI-Objekte)
        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null)
        {
            float width = rt.rect.width;
            float height = rt.rect.height;

            Debug.Log($"Width: {width}, Height: {height}");
            Debug.Log($"Seitenverhältnis (Width:Height) = {width / height:F2}");
            return;
        }

        Debug.LogWarning("Kein SpriteRenderer oder RectTransform gefunden!");
    }
}