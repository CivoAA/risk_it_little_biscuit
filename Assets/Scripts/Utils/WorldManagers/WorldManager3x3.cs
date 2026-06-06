using UnityEngine;

public class WorldManager3x3 : MonoBehaviour
{
    [Header("Chunks (3x3) – im Inspector in Telefon-Layout füllen:")]
    [Tooltip("Index 0..8 = oben links → unten rechts (0 1 2 / 3 4 5 / 6 7 8)")]
    public GameObject[] chunksFlat = new GameObject[9];

    [Header("Größe eines Chunks")]
    public float chunkWidth = 52f;
    public float chunkHeight = 40f;

    // internes 3x3 Grid (x: links→rechts 0..2, y: unten→oben 0..2)
    private GameObject[,] chunks = new GameObject[3, 3];

    // logisches Zentrum (Weltposition des mittleren Chunks [1,1])
    private Vector2 centerWorldPos;
    private Vector2Int centerIndex = new Vector2Int(1, 1);

    void Awake()
    {
        // 1D (Telefon-Layout, oben→unten) in 2D (unten=0 → oben=2) umsetzen
        // flat(yFlat=0..2, x=0..2) -> chunks[x, y=2-yFlat]
        for (int yFlat = 0; yFlat < 3; yFlat++)
        {
            for (int x = 0; x < 3; x++)
            {
                int idx = yFlat * 3 + x;          // 0..8
                int y = 2 - yFlat;                // invertiert: unten=0
                if (idx < chunksFlat.Length)
                    chunks[x, y] = chunksFlat[idx];
            }
        }
    }

    void Start()
    {
        // Startzentrum = aktuelle Position des mittleren Chunks, falls vorhanden
        if (chunks[1, 1] != null)
            centerWorldPos = chunks[1, 1].transform.position;
        else
            centerWorldPos = transform.position;

        SnapGridToCenter(); // alle 9 Chunks exakt ausrichten
    }

    void Update()
    {
        if (PlayerController.Instance == null) return;

        Vector3 playerPos = PlayerController.Instance.transform.position;

        // Distanz des Spielers relativ zum logischen Zentrum
        float dx = playerPos.x - centerWorldPos.x;
        float dy = playerPos.y - centerWorldPos.y;

        // Mehrfach shiften, falls der Spieler weit über den Rand kommt
        while (dx >  +chunkWidth  * 0.5f) { Shift(Vector2Int.right); centerWorldPos.x += chunkWidth;  dx -= chunkWidth; }
        while (dx <  -chunkWidth  * 0.5f) { Shift(Vector2Int.left ); centerWorldPos.x -= chunkWidth;  dx += chunkWidth; }
        while (dy >  +chunkHeight * 0.5f) { Shift(Vector2Int.up   ); centerWorldPos.y += chunkHeight; dy -= chunkHeight; }
        while (dy <  -chunkHeight * 0.5f) { Shift(Vector2Int.down ); centerWorldPos.y -= chunkHeight; dy += chunkHeight; }

        // nach möglichen Shifts die Positionen absolut setzen
        SnapGridToCenter();
    }

    // rotiert das Grid (Referenzen) in die gewünschte Richtung
    private void Shift(Vector2Int dir)
    {
        GameObject[,] newGrid = new GameObject[3, 3];

        for (int x = 0; x < 3; x++)
        {
            for (int y = 0; y < 3; y++)
            {
                int newX = (x - dir.x + 3) % 3;
                int newY = (y - dir.y + 3) % 3;
                newGrid[newX, newY] = chunks[x, y];
            }
        }

        chunks = newGrid;
        centerIndex = new Vector2Int((centerIndex.x - dir.x + 3) % 3, (centerIndex.y - dir.y + 3) % 3);
    }

    // setzt ALLE Chunk-Positionen absolut relativ zum logischen Zentrum
    private void SnapGridToCenter()
    {
        for (int x = 0; x < 3; x++)
        {
            for (int y = 0; y < 3; y++)
            {
                GameObject go = chunks[x, y];
                if (go == null) continue;

                float offsetX = (x - 1) * chunkWidth;
                float offsetY = (y - 1) * chunkHeight;
                go.transform.position = new Vector3(centerWorldPos.x + offsetX, centerWorldPos.y + offsetY, go.transform.position.z);
            }
        }
    }
}
