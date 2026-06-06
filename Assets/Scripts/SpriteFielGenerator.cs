using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class SpriteFieldGenerator : MonoBehaviour
{
    [Header("🌸 Sprite-Objekte (z. B. Blumen)")]
    public Sprite[] flowerSprites;

    [Header("🌳 Prefabs (z. B. Bäume mit Collider)")]
    public GameObject[] treePrefabs;

    [Header("📦 Ziel-Elternobjekt für alles (optional)")]
    public Transform targetParent;

    [Header("🧭 Generation Bounds")]
    public int left = 25;
    public int right = 25;
    public int down = 20;
    public int up = 20;
    public Vector2Int offset = Vector2Int.zero;

    [Header("⚙️ Platzierungseinstellungen")]
    public float spacing = 1f;
    public int spriteCount = 100;
    public int prefabCount = 20;

    [Header("🌈 Noise Settings")]
    public bool usePerlinNoise = true;
    [Range(0.01f, 0.3f)] public float noiseScale = 0.1f;
    [Range(0f, 1f)] public float placementThreshold = 0.5f;

#if UNITY_EDITOR
    [ContextMenu("🌿 Generate Field")]
    public void GenerateField()
    {
        if (flowerSprites.Length == 0 && treePrefabs.Length == 0)
        {
            Debug.LogWarning("⚠️ Keine Sprites oder Prefabs zugewiesen!");
            return;
        }

        if (targetParent == null)
            targetParent = transform;

        // Vorher alte Inhalte löschen
        for (int i = targetParent.childCount - 1; i >= 0; i--)
            DestroyImmediate(targetParent.GetChild(i).gameObject);

        Undo.RegisterFullObjectHierarchyUndo(targetParent, "Generate Field");

        float offsetX = Random.Range(0f, 100f);
        float offsetY = Random.Range(0f, 100f);

        // Grenzen bestimmen
        int width = left + right;
        int height = up + down;

        // Zufällige Positionen für Prefabs und Sprites berechnen
        for (int i = 0; i < prefabCount; i++)
        {
            Vector2 pos = GetRandomPosition(offsetX, offsetY, width, height);
            GameObject prefab = treePrefabs[Random.Range(0, treePrefabs.Length)];
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            obj.transform.position = pos;
            obj.transform.SetParent(targetParent);
        }

        for (int i = 0; i < spriteCount; i++)
        {
            Vector2 pos = GetRandomPosition(offsetX, offsetY, width, height);
            Sprite sprite = flowerSprites[Random.Range(0, flowerSprites.Length)];
            GameObject go = new GameObject($"Sprite_{i}");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            go.transform.position = pos;
            go.transform.SetParent(targetParent);
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log($"✅ Feld generiert: {prefabCount} Prefabs, {spriteCount} Sprites. Offset: {offset}");
    }

    private Vector2 GetRandomPosition(float noiseOffsetX, float noiseOffsetY, int width, int height)
    {
        float x = Random.Range(-left, right) * spacing + offset.x;
        float y = Random.Range(-down, up) * spacing + offset.y;

        if (usePerlinNoise)
        {
            float noise = Mathf.PerlinNoise(x * noiseScale + noiseOffsetX, y * noiseScale + noiseOffsetY);
            if (noise < placementThreshold)
                return GetRandomPosition(noiseOffsetX, noiseOffsetY, width, height); // neu probieren
        }

        return new Vector2(x, y);
    }
#endif
}
