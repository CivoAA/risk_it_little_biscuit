using UnityEngine;

/// <summary>
/// Das Leuchten der Elite-Gegner: ein pulsierender Umriss in
/// <see cref="EnemyCatalog.EliteGlow"/>, pixelgenau statt weichgezeichnet.
///
/// Gebaut aus Kopien des Sprites als einfarbige Silhouette (Shader
/// Risk/SpriteSilhouette), hinter dem Gegner und um genau einen Bildpixel
/// versetzt - vier Kopien fuer den harten Rand, vier weitere zwei Pixel weit
/// draussen fuer den schwachen Schein. Die Kopien folgen in LateUpdate dem
/// aktuellen Animationsbild und der Blickrichtung.
///
/// Haengt <see cref="Enemy"/> selbst an, sobald die Rolle Elite ist - am Prefab
/// muss dafuer nichts eingestellt werden.
/// </summary>
public class EliteGlow : MonoBehaviour
{
    private const string ShaderPath = "Shaders/SpriteSilhouette";

    private static readonly Vector2Int[] Inner = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
    private static readonly Vector2Int[] Outer = { new Vector2Int(2, 0), new Vector2Int(-2, 0), new Vector2Int(0, 2), new Vector2Int(0, -2) };

    private const float InnerAlphaMin = 0.55f, InnerAlphaMax = 1f;
    private const float OuterAlphaMin = 0.10f, OuterAlphaMax = 0.35f;
    private const float PulseSpeed = 4f;

    private static Material material;
    private static bool materialMissing;

    private SpriteRenderer source;
    private SpriteRenderer[] copies;
    private Vector2Int[] offsets;
    private Sprite lastSprite;
    private float phase;

    /// <summary>Haengt das Leuchten an. Doppelt aufgerufen passiert nichts.</summary>
    public static void Attach(SpriteRenderer source)
    {
        if (source == null || source.GetComponent<EliteGlow>() != null) return;

        Material mat = SharedMaterial();
        if (mat == null) return;

        EliteGlow glow = source.gameObject.AddComponent<EliteGlow>();
        glow.Build(source, mat);
    }

    private static Material SharedMaterial()
    {
        if (material != null || materialMissing) return material;

        Shader shader = Resources.Load<Shader>(ShaderPath);
        if (shader == null)
        {
            materialMissing = true;
            Debug.LogWarning($"[EliteGlow] Shader Resources/{ShaderPath} fehlt - Elites leuchten nicht.");
            return null;
        }

        material = new Material(shader) { name = "EliteGlow" };
        return material;
    }

    private void Build(SpriteRenderer src, Material mat)
    {
        source = src;
        phase = Random.value * Mathf.PI * 2f;

        offsets = new Vector2Int[Inner.Length + Outer.Length];
        Inner.CopyTo(offsets, 0);
        Outer.CopyTo(offsets, Inner.Length);

        copies = new SpriteRenderer[offsets.Length];
        for (int i = 0; i < offsets.Length; i++)
        {
            var go = new GameObject("EliteGlow");
            go.transform.SetParent(src.transform, false);

            SpriteRenderer r = go.AddComponent<SpriteRenderer>();
            r.sharedMaterial = mat;
            copies[i] = r;
        }

        Sync(true);
    }

    private void LateUpdate()
    {
        if (source == null) return;
        Sync(false);
    }

    private void Sync(bool force)
    {
        Sprite sprite = source.sprite;

        // Position haengt am Pixelmass des Bildes - nur neu setzen, wenn das
        // Bild wechselt (Animation mit anderem Pixelmass ist selten, aber moeglich).
        if (force || sprite != lastSprite)
        {
            lastSprite = sprite;
            float unit = sprite != null && sprite.pixelsPerUnit > 0f ? 1f / sprite.pixelsPerUnit : 1f / EnemyCatalog.PixelsPerUnit;

            for (int i = 0; i < copies.Length; i++)
            {
                copies[i].transform.localPosition = new Vector3(offsets[i].x * unit, offsets[i].y * unit, 0f);
                copies[i].sprite = sprite;
            }
        }

        float k = 0.5f + 0.5f * Mathf.Sin(Time.time * PulseSpeed + phase);
        Color inner = EnemyCatalog.EliteGlow; inner.a = Mathf.Lerp(InnerAlphaMin, InnerAlphaMax, k);
        Color outer = EnemyCatalog.EliteGlow; outer.a = Mathf.Lerp(OuterAlphaMin, OuterAlphaMax, k);
        bool show = source.enabled && sprite != null;

        for (int i = 0; i < copies.Length; i++)
        {
            SpriteRenderer r = copies[i];
            r.enabled = show;
            if (!show) continue;

            r.flipX = source.flipX;
            r.flipY = source.flipY;
            r.sortingLayerID = source.sortingLayerID;
            r.sortingOrder = source.sortingOrder - 1;
            r.color = i < Inner.Length ? inner : outer;
        }
    }
}
