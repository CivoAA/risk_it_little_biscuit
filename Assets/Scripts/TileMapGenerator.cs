using UnityEngine;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class TilemapGenerator : MonoBehaviour
{
    [Header("🔹 Tilemap Setup")]
    public Tilemap tilemap;
    public TileBase[] tiles; // z. B. 3 verschiedene Tiles

    [Header("🔹 Generation Bounds")]
    public int left = 25;
    public int right = 27;
    public int down = 21;
    public int up = 19;

    [Header("🔹 Noise Settings")]
    [Range(0.01f, 0.3f)]
    public float noiseScale = 0.1f;

#if UNITY_EDITOR
    [ContextMenu("🧩 Generate Tilemap")]
    public void GenerateTilemap()
    {
        if (tilemap == null || tiles.Length == 0)
        {
            Debug.LogWarning("⚠️ Tilemap oder Tiles fehlen!");
            return;
        }

        Undo.RegisterCompleteObjectUndo(tilemap, "Generate Random Tiles");

        float offsetX = Random.Range(0f, 100f);
        float offsetY = Random.Range(0f, 100f);

        for (int x = -left; x <= right; x++)
        {
            for (int y = -down; y <= up; y++)
            {
                float noiseValue = Mathf.PerlinNoise(x * noiseScale + offsetX, y * noiseScale + offsetY);

                int index;
                if (noiseValue < 0.33f) index = 0;
                else if (noiseValue < 0.66f) index = 1;
                else index = 2;

                tilemap.SetTile(new Vector3Int(x, y, 0), tiles[index]);
            }
        }

        // Szene als geändert markieren
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(tilemap.gameObject.scene);
        Debug.Log($"✅ Tilemap generiert! Bereich: ←{left} →{right} ↓{down} ↑{up}");
    }
#endif
}
