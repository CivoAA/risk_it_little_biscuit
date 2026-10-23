using System.Collections.Generic;
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

    /// <summary>Die laufende 3x3-Map, falls eine geladen ist.</summary>
    public static WorldManager3x3 Current { get; private set; }

    /// <summary>Nach dieser Strecke wiederholt sich der Inhalt der Chunks.</summary>
    public Vector2 WrapPeriod => new Vector2(chunkWidth * 3f, chunkHeight * 3f);

    void OnEnable()
    {
        Current = this;
    }

    void OnDisable()
    {
        if (Current == this) Current = null;
    }

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

        if (NetWorld.Deterministic) InitNet();
    }

    void Update()
    {
        if (netMode)
        {
            UpdateNet();
            return;
        }

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

    // ------------------------------------------------------------------ Koop
    //
    // Allein wandern neun Chunks mit dem Spieler. Im Koop gibt es mehrere
    // Spieler - jeder braucht seine drei mal drei Zellen. Jede Zelle hat einen
    // festen Inhalt (welcher der neun Chunks dort liegt, ergibt sich aus der
    // Zelle modulo 3 - genau wie beim Wandern allein), und fuer jeden Inhalt
    // gibt es so viele Kopien wie gerade gebraucht. Wo sich die Bereiche
    // zweier Spieler ueberlappen, liegt eine Zelle nur einmal.

    private bool netMode;
    private Vector2 netOrigin;
    private readonly GameObject[,] originals = new GameObject[3, 3];
    private readonly List<GameObject>[,] pool = new List<GameObject>[3, 3];
    private readonly Dictionary<Vector2Int, GameObject> placed = new Dictionary<Vector2Int, GameObject>();
    private readonly HashSet<Vector2Int> needed = new HashSet<Vector2Int>();
    private readonly List<Vector2Int> releasing = new List<Vector2Int>();

    private void InitNet()
    {
        netMode = true;
        netOrigin = centerWorldPos;
        for (int x = 0; x < 3; x++)
        {
            for (int y = 0; y < 3; y++)
            {
                originals[x, y] = chunks[x, y];
                pool[x, y] = new List<GameObject>();
                if (chunks[x, y] == null) continue;
                var cell = new Vector2Int(x - 1, y - 1);
                placed[cell] = chunks[x, y];
                // Auch die Ausgangslage nach Seed + Zelle - sonst stuenden bei
                // einem Spieler die Baeume aus dem Editor, beim anderen gewuerfelte.
                Reshuffle(chunks[x, y]);
            }
        }
    }

    private Vector2Int CellOf(Vector2 position)
    {
        return new Vector2Int(Mathf.RoundToInt((position.x - netOrigin.x) / chunkWidth),
                              Mathf.RoundToInt((position.y - netOrigin.y) / chunkHeight));
    }

    private Vector3 CellPosition(Vector2Int cell, float z)
    {
        return new Vector3(netOrigin.x + cell.x * chunkWidth, netOrigin.y + cell.y * chunkHeight, z);
    }

    private static int Mod3(int v) => ((v % 3) + 3) % 3;

    private void UpdateNet()
    {
        needed.Clear();
        foreach (Vector2 anchor in NetWorld.Anchors())
        {
            Vector2Int c = CellOf(anchor);
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    needed.Add(new Vector2Int(c.x + dx, c.y + dy));
        }
        if (needed.Count == 0) return;

        // Zellen, die keiner mehr braucht, geben ihren Chunk frei.
        releasing.Clear();
        foreach (var kv in placed)
            if (!needed.Contains(kv.Key)) releasing.Add(kv.Key);
        foreach (Vector2Int cell in releasing)
        {
            GameObject go = placed[cell];
            placed.Remove(cell);
            if (go != null) pool[Mod3(cell.x + 1), Mod3(cell.y + 1)].Add(go);
        }

        foreach (Vector2Int cell in needed)
        {
            if (placed.ContainsKey(cell)) continue;
            int kx = Mod3(cell.x + 1), ky = Mod3(cell.y + 1);
            GameObject original = originals[kx, ky];
            if (original == null) continue;

            List<GameObject> free = pool[kx, ky];
            GameObject go = null;
            while (free.Count > 0 && go == null)
            {
                go = free[free.Count - 1];
                free.RemoveAt(free.Count - 1);
            }

            bool fresh = go == null;
            if (fresh) go = Instantiate(original, original.transform.parent);

            go.transform.position = CellPosition(cell, original.transform.position.z);
            placed[cell] = go;
            // Kopien bringen die Baeume des Originals mit - neu wuerfeln. Wer
            // nur umzieht, wuerfelt selbst (der Sprung ist immer drei Zellen gross).
            if (fresh) Reshuffle(go);
        }
    }

    private static void Reshuffle(GameObject chunk)
    {
        foreach (ChunkPropRandomizer r in chunk.GetComponentsInChildren<ChunkPropRandomizer>(true)) r.Reshuffle();
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
