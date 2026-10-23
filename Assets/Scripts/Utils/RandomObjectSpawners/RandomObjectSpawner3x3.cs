using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class RandomObjectSpawner3x3 : MonoBehaviour
{
    [Header("Objekte, die gespawnt werden können")]
    public GameObject[] spawnablePrefabs;

    [Header("Einstellungen")]
    public Transform player;
    public float blockSize = 20f;
    public int spawnRadiusInBlocks = 5;
    public int maxObjectsPerBlock = 1;
    [Tooltip("Luecke zwischen zwei Mixer-Feldern, in Feld-Durchmessern (1 = ein ganzes Feld dazwischen).")]
    public float mixerGap = 1f;

    [Header("No-Spawn-Zone um Kamera")]
    [Tooltip("Linke untere Ecke der No-Spawn-Zone (z. B. Kamerarand unten links)")]
    public Transform bottomLeftNoSpawnPoint;
    [Tooltip("Rechte obere Ecke der No-Spawn-Zone (z. B. Kamerarand oben rechts)")]
    public Transform topRightNoSpawnPoint;

    private HashSet<Vector2Int> generatedBlocks = new HashSet<Vector2Int>();
    private GameObject[] mixerPrefabs;

    // Alle laufenden Spawner - der Koop fragt sie, wo Mixer stehen werden (ChunkPropRandomizer).
    private static readonly List<RandomObjectSpawner3x3> active = new List<RandomObjectSpawner3x3>();
    private void OnEnable() => active.Add(this);
    private void OnDisable() => active.Remove(this);

    private void Update()
    {
        // Koop: Bloecke um alle Spieler herum, gewuerfelt nach Ort statt nach Reihenfolge.
        if (NetWorld.Deterministic)
        {
            foreach (Vector2 anchor in NetWorld.Anchors()) GenerateAround(anchor);
            return;
        }

        // Die Inspector-Referenz gilt, solange Spieler und Spawner in derselben
        // Szene liegen. Sobald die Maps in eigene Szenen wandern, faellt sie weg
        // (szenenuebergreifende Referenzen speichert Unity nicht) - dann kommt
        // der Spieler hier ueber sein Singleton.
        if (player == null && PlayerController.Instance != null)
            player = PlayerController.Instance.transform;

        if (player == null) return;
        GenerateAround(player.position);
    }

    private void GenerateAround(Vector2 position)
    {
        int playerBlockX = Mathf.FloorToInt(position.x / blockSize);
        int playerBlockY = Mathf.FloorToInt(position.y / blockSize);

        for (int x = playerBlockX - spawnRadiusInBlocks; x <= playerBlockX + spawnRadiusInBlocks; x++)
        {
            for (int y = playerBlockY - spawnRadiusInBlocks; y <= playerBlockY + spawnRadiusInBlocks; y++)
            {
                Vector2Int block = new Vector2Int(x, y);
                if (!generatedBlocks.Contains(block))
                {
                    GenerateBlock(block);
                    generatedBlocks.Add(block);
                }
            }
        }
    }

    private void GenerateBlock(Vector2Int block)
    {
        if (spawnablePrefabs.Length == 0) return;

        if (NetWorld.Deterministic)
        {
            GenerateBlockDeterministic(block);
            return;
        }

        float blockStartX = block.x * blockSize;
        float blockEndX = blockStartX + blockSize;
        float blockStartY = block.y * blockSize;
        float blockEndY = blockStartY + blockSize;

        int baseCount = Random.Range(0, maxObjectsPerBlock + 1);

        // Skilltree "MoreMixers": zusaetzliche Objekte sind immer Mixer.
        if (mixerPrefabs == null) mixerPrefabs = System.Array.FindAll(spawnablePrefabs, MixerObject.IsMixer);
        int objectCount = mixerPrefabs.Length > 0 ? MixerObject.ScaleSpawnCount(baseCount) : baseCount;

        for (int i = 0; i < objectCount; i++)
        {
            GameObject prefab = i < baseCount
                ? spawnablePrefabs[Random.Range(0, spawnablePrefabs.Length)]
                : mixerPrefabs[Random.Range(0, mixerPrefabs.Length)];

            // Mixer wuerfeln neu, bis ihr Feld frei steht - normale Objekte
            // haben wie bisher einen Versuch.
            bool isMixer = System.Array.IndexOf(mixerPrefabs, prefab) >= 0;
            int attempts = isMixer ? MixerPlacement.Attempts : 1;
            bool found = false;
            float randomX = 0f, randomY = 0f;

            for (int a = 0; a < attempts && !found; a++)
            {
                randomX = Random.Range(blockStartX, blockEndX);
                randomY = Random.Range(blockStartY, blockEndY);

                // 🔹 Überspringen, falls in der No-Spawn-Zone
                if (IsInNoSpawnZone(randomX, randomY))
                    continue;

                found = !isMixer || MixerPlacement.IsFree(prefab, new Vector2(randomX, randomY), mixerGap);
            }

            if (!found)
                continue;

            GameObject spawnedObject = Instantiate(prefab, new Vector3(randomX, randomY, 0f), Quaternion.identity);

            Scene gameScene = RunScene.Current;
            if (gameScene.IsValid() && gameScene.isLoaded)
            {
                RunScene.Place(spawnedObject, isMixer ? "Mixer" : "Map-Objekte");
            }
            else
            {
                Debug.LogWarning("⚠️ Keine Lauf-Szene gefunden! Objekt bleibt in aktueller Scene.");
            }
        }
    }

    // ------------------------------------------------------------------ Koop

    private struct Planned
    {
        public int Prefab;
        public Vector2 Position;
        public bool Mixer;
    }

    private static readonly List<Planned> plan = new List<Planned>();

    private int Salt => NetWorld.Hash(gameObject.scene.name + "/" + gameObject.name);

    /// <summary>
    /// Was in diesem Block stehen wird - nur aus Seed und Block gerechnet, bei
    /// allen Spielern gleich, egal wann wer hier vorbeikommt. Mixer bleiben
    /// so weit vom Blockrand weg, dass sie nie einem Mixer im Nachbarblock zu
    /// nahe kommen (sonst haenge das Ergebnis von der Reihenfolge ab).
    /// "MoreMixers" aus dem Skilltree wirkt im Koop nicht - die Welt ist fuer alle dieselbe.
    /// </summary>
    private void PlanBlock(Vector2Int block, List<Planned> into)
    {
        into.Clear();
        if (spawnablePrefabs.Length == 0) return;
        if (mixerPrefabs == null) mixerPrefabs = System.Array.FindAll(spawnablePrefabs, MixerObject.IsMixer);

        System.Random rng = NetWorld.RandomFor(Salt, block.x, block.y);
        float x0 = block.x * blockSize, y0 = block.y * blockSize;
        int count = rng.Next(0, maxObjectsPerBlock + 1);

        for (int i = 0; i < count; i++)
        {
            int index = rng.Next(0, spawnablePrefabs.Length);
            GameObject prefab = spawnablePrefabs[index];
            bool isMixer = System.Array.IndexOf(mixerPrefabs, prefab) >= 0;
            float radius = isMixer ? MixerPlacement.FieldRadius(prefab) : 0f;
            float margin = isMixer ? Mathf.Min(blockSize * 0.45f, radius * (1f + mixerGap)) : 0f;
            int attempts = isMixer ? MixerPlacement.Attempts : 1;

            for (int a = 0; a < attempts; a++)
            {
                var pos = new Vector2(x0 + margin + (float)rng.NextDouble() * (blockSize - 2f * margin),
                                      y0 + margin + (float)rng.NextDouble() * (blockSize - 2f * margin));
                if (NetWorld.NearStart(pos)) continue;

                bool free = true;
                if (isMixer)
                {
                    foreach (Planned other in into)
                    {
                        if (!other.Mixer) continue;
                        float min = radius * 2f * (1f + mixerGap);
                        if ((other.Position - pos).sqrMagnitude < min * min) { free = false; break; }
                    }
                }
                if (!free) continue;

                into.Add(new Planned { Prefab = index, Position = pos, Mixer = isMixer });
                break;
            }
        }
    }

    private void GenerateBlockDeterministic(Vector2Int block)
    {
        PlanBlock(block, plan);
        foreach (Planned p in plan)
        {
            GameObject spawned = Instantiate(spawnablePrefabs[p.Prefab], new Vector3(p.Position.x, p.Position.y, 0f), Quaternion.identity);
            RunScene.Place(spawned, p.Mixer ? "Mixer" : "Map-Objekte");
            if (p.Mixer && NetWorld.MixerAlreadyUsed(p.Position))
            {
                MixerObject mixer = spawned.GetComponentInChildren<MixerObject>();
                if (mixer != null) mixer.MarkUsedByTeammate();
            }
        }
    }

    private static readonly List<Planned> query = new List<Planned>();

    /// <summary>
    /// Koop: Mixer-Felder, die in diesem Rechteck stehen werden (auch wenn der
    /// Block noch nicht gebaut ist). Damit weichen Baeume Mixern aus, ohne
    /// dass es darauf ankommt, was zuerst entstand.
    /// </summary>
    public static void PlannedMixerFields(Rect area, List<(Vector2 center, float radius)> into)
    {
        into.Clear();
        foreach (RandomObjectSpawner3x3 s in active)
        {
            if (s == null || s.spawnablePrefabs == null || s.spawnablePrefabs.Length == 0) continue;
            int bx0 = Mathf.FloorToInt(area.xMin / s.blockSize), bx1 = Mathf.FloorToInt(area.xMax / s.blockSize);
            int by0 = Mathf.FloorToInt(area.yMin / s.blockSize), by1 = Mathf.FloorToInt(area.yMax / s.blockSize);
            for (int x = bx0; x <= bx1; x++)
            {
                for (int y = by0; y <= by1; y++)
                {
                    s.PlanBlock(new Vector2Int(x, y), query);
                    foreach (Planned p in query)
                    {
                        if (!p.Mixer) continue;
                        GameObject prefab = s.spawnablePrefabs[p.Prefab];
                        into.Add((MixerPlacement.FieldCenter(prefab, p.Position), MixerPlacement.FieldRadius(prefab)));
                    }
                }
            }
        }
    }

    private bool IsInNoSpawnZone(float x, float y)
    {
        if (bottomLeftNoSpawnPoint != null && topRightNoSpawnPoint != null)
        {
            float left = bottomLeftNoSpawnPoint.position.x;
            float right = topRightNoSpawnPoint.position.x;
            float bottom = bottomLeftNoSpawnPoint.position.y;
            float top = topRightNoSpawnPoint.position.y;

            return (x >= left && x <= right && y >= bottom && y <= top);
        }

        // Die beiden Ecken hingen am Kamerarand und stehen seit dem
        // Szenen-Split in GameCore - die Referenz von hier aus ist damit leer.
        // Ohne Ersatz waere die Antwort "nein, spawn ruhig", und dann poppen
        // Props mitten im Bild neben dem Spieler auf.
        if (ViewBounds.TryGetWorldRect(out Rect view))
        {
            return view.Contains(new Vector2(x, y));
        }

        return false;
    }
}
