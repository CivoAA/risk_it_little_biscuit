using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Wuerfelt die Props (Baeume) eines Chunks neu, sobald der
/// <see cref="WorldManager3x3"/> den Chunk auf die andere Seite des 3x3-Rasters
/// umsetzt. Dadurch wiederholt sich die Welt nicht sichtbar, obwohl immer nur
/// dieselben 9 Tilemaps im Kreis geschoben werden.
///
/// Es wird nichts erzeugt oder zerstoert - die vorhandenen Kinder des
/// Prop-Containers werden nur neu positioniert, skaliert und teilweise
/// ausgeblendet. Die Werte setzt normalerweise der Welt-Generator
/// (Tools -> Map -> Welt-Generator) beim Verteilen der Props.
/// </summary>
[DisallowMultipleComponent]
public class ChunkPropRandomizer : MonoBehaviour
{
    [Header("Quelle")]
    [Tooltip("Objekt, dessen Kinder umgewuerfelt werden. Leer = dieser Chunk selbst.")]
    public Transform propContainer;

    [Header("Chunk-Groesse (wie im WorldManager3x3)")]
    public float chunkWidth = 52f;
    public float chunkHeight = 40f;

    [Header("Verteilung")]
    public float edgeMargin = 1f;
    public float minDistance = 3f;
    [Range(0f, 1f)] public float clumping = 0.4f;
    public float clumpSize = 14f;

    [Header("Aussehen")]
    public Vector2 scaleRange = new Vector2(0.9f, 1.15f);
    public bool randomFlipX = true;

    [Tooltip("Anteil der Props, der nach dem Umsetzen zufaellig ausgeblendet wird.")]
    [Range(0f, 0.9f)] public float countJitter = 0.3f;

    [Tooltip("Mittlere Skalierung, die der Generator bereits in die Props geschrieben hat. " +
             "Wird herausgerechnet, damit die Baeume ueber viele Durchlaeufe nicht wachsen oder schrumpfen.")]
    public float editorScaleMid = 1f;

    private Transform[] props;
    private Vector3[] baseScales;
    private Vector3 lastPosition;
    private int shuffleCount;

    private readonly List<Vector2> placed = new List<Vector2>();

    private void Awake()
    {
        if (propContainer == null) propContainer = transform;

        int count = propContainer.childCount;
        props = new Transform[count];
        baseScales = new Vector3[count];

        float divisor = Mathf.Max(0.0001f, editorScaleMid);
        for (int i = 0; i < count; i++)
        {
            props[i] = propContainer.GetChild(i);
            baseScales[i] = props[i].localScale / divisor;
        }

        lastPosition = transform.position;
    }

    private void LateUpdate()
    {
        if (props == null || props.Length == 0) return;

        Vector3 position = transform.position;
        Vector3 delta = position - lastPosition;
        lastPosition = position;

        // Der WorldManager3x3 richtet die Chunks jeden Frame aus - das sind
        // Mini-Korrekturen. Ein echtes Umsetzen springt dagegen ueber das ganze
        // Raster, also mindestens eine volle Chunk-Breite bzw. -Hoehe.
        bool moved = Mathf.Abs(delta.x) > chunkWidth * 1.5f || Mathf.Abs(delta.y) > chunkHeight * 1.5f;
        if (moved) Reshuffle();
    }

    /// <summary>Verteilt alle Props des Containers neu innerhalb der Chunk-Grenzen.</summary>
    public void Reshuffle()
    {
        shuffleCount++;
        var rng = new System.Random(transform.GetSiblingIndex() * 7919 + shuffleCount * 83492791);

        float halfW = Mathf.Max(1f, chunkWidth * 0.5f - edgeMargin);
        float halfH = Mathf.Max(1f, chunkHeight * 0.5f - edgeMargin);
        float noiseOffsetX = (float)rng.NextDouble() * 500f;
        float noiseOffsetY = (float)rng.NextDouble() * 500f;
        float minSqr = minDistance * minDistance;

        // Wie viele Props diesmal ueberhaupt sichtbar sind.
        int visible = Mathf.RoundToInt(props.Length * (1f - (float)rng.NextDouble() * countJitter));
        visible = Mathf.Clamp(visible, 1, props.Length);

        placed.Clear();

        for (int i = 0; i < props.Length; i++)
        {
            Transform prop = props[i];
            if (prop == null) continue;

            if (i >= visible)
            {
                prop.gameObject.SetActive(false);
                continue;
            }

            Vector2 local = Vector2.zero;
            bool found = false;

            for (int attempt = 0; attempt < 24; attempt++)
            {
                local = new Vector2(
                    Mathf.Lerp(-halfW, halfW, (float)rng.NextDouble()),
                    Mathf.Lerp(-halfH, halfH, (float)rng.NextDouble()));

                if (clumping > 0f)
                {
                    float size = Mathf.Max(1f, clumpSize);
                    float n = Mathf.PerlinNoise((local.x + noiseOffsetX) / size, (local.y + noiseOffsetY) / size);
                    if (n < clumping * 0.6f) continue;
                }

                bool tooClose = false;
                for (int k = 0; k < placed.Count; k++)
                {
                    if ((placed[k] - local).sqrMagnitude < minSqr) { tooClose = true; break; }
                }
                if (tooClose) continue;

                found = true;
                break;
            }

            if (!found)
            {
                prop.gameObject.SetActive(false);
                continue;
            }

            placed.Add(local);
            prop.gameObject.SetActive(true);
            prop.localPosition = new Vector3(local.x, local.y, prop.localPosition.z);

            float scale = Mathf.Lerp(scaleRange.x, scaleRange.y, (float)rng.NextDouble());
            prop.localScale = new Vector3(baseScales[i].x * scale, baseScales[i].y * scale, baseScales[i].z);

            if (randomFlipX)
            {
                var renderer = prop.GetComponentInChildren<SpriteRenderer>();
                if (renderer != null) renderer.flipX = rng.Next(2) == 0;
            }
        }
    }
}
