using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Die eigentliche Arbeit hinter Tools -> Map -> Welt-Generator: fuellt die
/// neun Chunks einer Welt mit zufaelligen Tiles und verteilt Props darauf.
///
/// Das 3x3-Raster (<see cref="WorldManager3x3"/>) schiebt die neun Tilemaps im
/// Kreis, sobald der Spieler ueber eine Chunk-Grenze laeuft. Damit das endlos
/// wirkt, muss jeder Chunk an jeden anderen passen - deshalb gibt es genau zwei
/// Modi: gestreute Tiles (voellig zusammenhanglos, damit immer nahtlos) oder
/// zusammenhaengende Flecken, die dann aber in allen neun Chunks gleich sind.
/// </summary>
public static class WorldGenerator
{
    public const string PropContainerName = "Props";
    private const string UndoName = "Welt generieren";

    // ------------------------------------------------------------------ Chunks

    /// <summary>
    /// Liefert die neun Chunks in Rasterreihenfolge (0 = oben links, 8 = unten
    /// rechts). Bevorzugt die Inspector-Liste des Managers, faellt sonst auf die
    /// Kinder zurueck.
    /// </summary>
    public static List<Transform> CollectChunks(WorldManager3x3 world)
    {
        var chunks = new List<Transform>();
        if (world == null) return chunks;

        if (world.chunksFlat != null)
        {
            for (int i = 0; i < world.chunksFlat.Length; i++)
                if (world.chunksFlat[i] != null) chunks.Add(world.chunksFlat[i].transform);
        }

        if (chunks.Count < 9)
        {
            chunks.Clear();
            for (int i = 0; i < world.transform.childCount && chunks.Count < 9; i++)
            {
                Transform child = world.transform.GetChild(i);
                if (child.GetComponent<Tilemap>() != null) chunks.Add(child);
            }
        }

        return chunks;
    }

    /// <summary>
    /// Setzt die neun Chunks sauber auf ihre Rasterposition und traegt sie in
    /// der richtigen Reihenfolge in den WorldManager3x3 ein.
    /// </summary>
    public static void AlignChunks(WorldManager3x3 world)
    {
        var chunks = CollectChunks(world);
        if (chunks.Count < 9)
        {
            Debug.LogWarning($"{world.name}: nur {chunks.Count} Chunks gefunden - es werden 9 gebraucht.");
            return;
        }

        Undo.RegisterCompleteObjectUndo(world, UndoName);

        var flat = new GameObject[9];
        for (int i = 0; i < 9; i++)
        {
            Transform chunk = chunks[i];
            int column = i % 3;          // 0 = links
            int row = i / 3;             // 0 = oben
            Vector3 position = new Vector3(
                (column - 1) * world.chunkWidth,
                (1 - row) * world.chunkHeight,
                chunk.localPosition.z);

            Undo.RecordObject(chunk, UndoName);
            chunk.localPosition = position;
            chunk.localScale = Vector3.one;
            chunk.localRotation = Quaternion.identity;
            flat[i] = chunk.gameObject;
        }

        world.chunksFlat = flat;
        EditorUtility.SetDirty(world);
        MarkDirty(world);
    }

    // ------------------------------------------------------------------- Tiles

    /// <summary>Fuellt alle neun Chunks neu. Gibt die Anzahl gesetzter Tiles zurueck.</summary>
    public static int FillTiles(WorldManager3x3 world, WorldGenPreset preset)
    {
        var chunks = CollectChunks(world);
        if (chunks.Count == 0) return 0;

        var background = Usable(preset.backgroundTiles);
        var groups = UsableGroups(preset.patchGroups);

        if (background.Count == 0)
        {
            Debug.LogWarning("Keine Untergrund-Tiles mit Gewicht groesser 0 im Preset - es gibt nichts zu malen.");
            return 0;
        }

        int width = Mathf.Max(1, Mathf.RoundToInt(world.chunkWidth));
        int height = Mathf.Max(1, Mathf.RoundToInt(world.chunkHeight));
        int cellCount = width * height;
        var bounds = new BoundsInt(-width / 2, -height / 2, 0, width, height, 1);

        int written = 0;
        for (int chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
        {
            var tilemap = chunks[chunkIndex].GetComponent<Tilemap>();
            if (tilemap == null) continue;

            var rng = new System.Random(preset.seed * 7919 + chunkIndex * 104729);
            var cells = new TileBase[cellCount];

            // 1. Untergrund (Gras und blank) ueberall.
            for (int i = 0; i < cellCount; i++) cells[i] = PickTile(background, rng);

            // 2. Farbflecken daraufmalen.
            ScatterPatches(cells, width, height, groups, preset, rng);

            Undo.RegisterCompleteObjectUndo(tilemap, UndoName);
            tilemap.ClearAllTiles();
            tilemap.SetTilesBlock(bounds, cells);
            tilemap.CompressBounds();
            EditorUtility.SetDirty(tilemap);
            written += cellCount;
        }

        MarkDirty(world);
        return written;
    }

    /// <summary>Wie viele Farbflecken bei den aktuellen Reglern auf einem Chunk landen.</summary>
    public static int EstimatePatchCount(WorldGenPreset preset, float chunkWidth, float chunkHeight)
    {
        float spacing = PatchSpacingInTiles(preset);
        return Mathf.Max(1, Mathf.RoundToInt(chunkWidth * chunkHeight / (spacing * spacing)));
    }

    /// <summary>Fleckendurchmesser in Tiles, gegen Werte ausserhalb der Regler abgesichert.</summary>
    private static int PatchSize(WorldGenPreset preset) => Mathf.Clamp(preset.patchSize, 2, 30);

    /// <summary>Mittlerer Abstand zwischen zwei Fleckenmitten, in Tiles.</summary>
    private static float PatchSpacingInTiles(WorldGenPreset preset)
    {
        return Mathf.Max(2f, PatchSize(preset) * (1f + Mathf.Clamp01(preset.patchSpacing) * 3f));
    }

    /// <summary>
    /// Verteilt die Farbflecken ueber den Chunk. Die Mittelpunkte halten
    /// Abstand voneinander (Regler 2), gezeichnet wird ueber den Chunkrand
    /// hinaus rundherum weiter - dadurch bleibt jeder Chunk in sich kachelbar
    /// und passt an jeden anderen, egal wie der WorldManager3x3 sie umsortiert.
    /// </summary>
    private static void ScatterPatches(TileBase[] cells, int width, int height,
        List<WorldGenPreset.TileGroup> groups, WorldGenPreset preset, System.Random rng)
    {
        if (groups.Count == 0) return;

        float spacing = PatchSpacingInTiles(preset);
        int target = Mathf.Max(1, Mathf.RoundToInt(width * height / (spacing * spacing)));
        float minDistance = spacing * 0.75f;
        float minSqr = minDistance * minDistance;

        var centers = new List<Vector2>(target);
        int attempts = target * 12;

        for (int i = 0; i < attempts && centers.Count < target; i++)
        {
            var candidate = new Vector2(
                (float)rng.NextDouble() * width,
                (float)rng.NextDouble() * height);

            bool tooClose = false;
            for (int k = 0; k < centers.Count; k++)
            {
                // Abstand ueber den Rand hinweg messen, sonst draengeln sich die
                // Flecken an den Chunkkanten.
                float dx = Mathf.Abs(centers[k].x - candidate.x);
                float dy = Mathf.Abs(centers[k].y - candidate.y);
                dx = Mathf.Min(dx, width - dx);
                dy = Mathf.Min(dy, height - dy);
                if (dx * dx + dy * dy < minSqr) { tooClose = true; break; }
            }
            if (tooClose) continue;

            centers.Add(candidate);
            DrawPatch(cells, width, height,
                Mathf.FloorToInt(candidate.x), Mathf.FloorToInt(candidate.y),
                PickGroup(groups, rng), PatchSize(preset), rng);
        }
    }

    /// <summary>Malt einen einzelnen Fleck mit ausgefranstem Rand.</summary>
    private static void DrawPatch(TileBase[] cells, int width, int height, int centerX, int centerY,
        WorldGenPreset.TileGroup group, int patchSize, System.Random rng)
    {
        var tiles = Usable(group.tiles);
        if (tiles.Count == 0) return;

        float radius = Mathf.Max(1f, patchSize * 0.5f);
        int reach = Mathf.CeilToInt(radius * 1.4f);
        float noiseOffsetX = (float)rng.NextDouble() * 200f;
        float noiseOffsetY = (float)rng.NextDouble() * 200f;

        for (int dy = -reach; dy <= reach; dy++)
        {
            for (int dx = -reach; dx <= reach; dx++)
            {
                float distance = Mathf.Sqrt(dx * dx + dy * dy) / radius;

                // Perlin verbeult den Rand, damit keine Kreise entstehen.
                float wobble = Mathf.PerlinNoise(
                    (centerX + dx) * 0.22f + noiseOffsetX,
                    (centerY + dy) * 0.22f + noiseOffsetY);

                float edge = 1f - distance + (wobble - 0.5f) * 0.7f;
                if (edge <= 0f) continue;

                // Zur Mitte hin dichter, zum Rand hin loechrig.
                float chance = Mathf.Clamp01(edge * 1.6f) * group.density;
                if (rng.NextDouble() > chance) continue;

                int x = Mod(centerX + dx, width);
                int y = Mod(centerY + dy, height);
                cells[y * width + x] = PickTile(tiles, rng);
            }
        }
    }

    private static List<WorldGenPreset.TileEntry> Usable(List<WorldGenPreset.TileEntry> entries)
    {
        var result = new List<WorldGenPreset.TileEntry>();
        if (entries == null) return result;

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry != null && entry.tile != null && entry.weight > 0f) result.Add(entry);
        }
        return result;
    }

    private static List<WorldGenPreset.TileGroup> UsableGroups(List<WorldGenPreset.TileGroup> groups)
    {
        var result = new List<WorldGenPreset.TileGroup>();
        if (groups == null) return result;

        for (int i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            if (group != null && group.weight > 0f && Usable(group.tiles).Count > 0) result.Add(group);
        }
        return result;
    }

    private static WorldGenPreset.TileGroup PickGroup(List<WorldGenPreset.TileGroup> groups, System.Random rng)
    {
        float total = 0f;
        for (int i = 0; i < groups.Count; i++) total += groups[i].weight;

        float pick = (float)rng.NextDouble() * total;
        for (int i = 0; i < groups.Count; i++)
        {
            pick -= groups[i].weight;
            if (pick <= 0f) return groups[i];
        }
        return groups[groups.Count - 1];
    }

    public static void ClearTiles(WorldManager3x3 world)
    {
        var chunks = CollectChunks(world);
        for (int i = 0; i < chunks.Count; i++)
        {
            var tilemap = chunks[i].GetComponent<Tilemap>();
            if (tilemap == null) continue;

            Undo.RegisterCompleteObjectUndo(tilemap, "Tiles loeschen");
            tilemap.ClearAllTiles();
            tilemap.CompressBounds();
            EditorUtility.SetDirty(tilemap);
        }
        MarkDirty(world);
    }

    private static TileBase PickTile(List<WorldGenPreset.TileEntry> entries, System.Random rng)
    {
        float total = 0f;
        for (int i = 0; i < entries.Count; i++) total += entries[i].weight;

        float pick = (float)rng.NextDouble() * total;
        for (int i = 0; i < entries.Count; i++)
        {
            pick -= entries[i].weight;
            if (pick <= 0f) return entries[i].tile;
        }
        return entries[entries.Count - 1].tile;
    }

    // ------------------------------------------------------------------- Props

    /// <summary>Verteilt die Props neu. Gibt die Anzahl platzierter Objekte zurueck.</summary>
    public static int PlaceProps(WorldManager3x3 world, WorldGenPreset preset)
    {
        var chunks = CollectChunks(world);
        if (chunks.Count == 0) return 0;

        var entries = new List<WorldGenPreset.PropEntry>();
        for (int i = 0; i < preset.props.Count; i++)
        {
            var entry = preset.props[i];
            if (entry != null && entry.prefab != null && entry.weight > 0f) entries.Add(entry);
        }

        ClearProps(world);
        if (entries.Count == 0 || preset.propsPerChunk <= 0)
        {
            MarkDirty(world);
            return 0;
        }

        float scaleMid = Mathf.Max(0.0001f, (preset.propScaleRange.x + preset.propScaleRange.y) * 0.5f);
        float halfWidth = Mathf.Max(1f, world.chunkWidth * 0.5f - preset.propEdgeMargin);
        float halfHeight = Mathf.Max(1f, world.chunkHeight * 0.5f - preset.propEdgeMargin);
        float minSqr = preset.propMinDistance * preset.propMinDistance;

        int placedTotal = 0;
        var placed = new List<Vector2>();

        for (int chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
        {
            Transform chunk = chunks[chunkIndex];
            var container = new GameObject(PropContainerName);
            Undo.RegisterCreatedObjectUndo(container, UndoName);
            container.transform.SetParent(chunk, false);
            container.transform.localPosition = Vector3.zero;

            var rng = new System.Random(preset.seed * 31 + chunkIndex * 40503 + 7);
            float noiseOffsetX = (float)rng.NextDouble() * 500f;
            float noiseOffsetY = (float)rng.NextDouble() * 500f;

            // Index 4 ist der mittlere Chunk - dort startet der Spieler.
            bool isCenterChunk = chunkIndex == 4;

            placed.Clear();
            for (int i = 0; i < preset.propsPerChunk; i++)
            {
                Vector2 local = Vector2.zero;
                bool found = false;

                for (int attempt = 0; attempt < 30; attempt++)
                {
                    local = new Vector2(
                        Mathf.Lerp(-halfWidth, halfWidth, (float)rng.NextDouble()),
                        Mathf.Lerp(-halfHeight, halfHeight, (float)rng.NextDouble()));

                    if (isCenterChunk && local.magnitude < preset.startClearRadius) continue;

                    if (preset.propClumping > 0f)
                    {
                        float size = Mathf.Max(1f, preset.propClumpSize);
                        float n = Mathf.PerlinNoise((local.x + noiseOffsetX) / size, (local.y + noiseOffsetY) / size);
                        if (n < preset.propClumping * 0.6f) continue;
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

                if (!found) continue;
                placed.Add(local);

                GameObject prefab = PickProp(entries, rng);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container.transform);
                if (instance == null) continue;

                Undo.RegisterCreatedObjectUndo(instance, UndoName);
                instance.transform.localPosition = new Vector3(local.x, local.y, 0f);

                float scale = Mathf.Lerp(preset.propScaleRange.x, preset.propScaleRange.y, (float)rng.NextDouble());
                Vector3 prefabScale = prefab.transform.localScale;
                instance.transform.localScale = new Vector3(prefabScale.x * scale, prefabScale.y * scale, prefabScale.z);

                if (preset.propRandomFlipX)
                {
                    var renderer = instance.GetComponentInChildren<SpriteRenderer>();
                    if (renderer != null) renderer.flipX = rng.Next(2) == 0;
                }

                placedTotal++;
            }

            ApplyRandomizer(world, chunk, container.transform, preset, scaleMid);
        }

        MarkDirty(world);
        return placedTotal;
    }

    public static void ClearProps(WorldManager3x3 world)
    {
        var chunks = CollectChunks(world);
        for (int i = 0; i < chunks.Count; i++)
        {
            Transform container = chunks[i].Find(PropContainerName);
            if (container != null) Undo.DestroyObjectImmediate(container.gameObject);

            var randomizer = chunks[i].GetComponent<ChunkPropRandomizer>();
            if (randomizer != null) Undo.DestroyObjectImmediate(randomizer);
        }
        MarkDirty(world);
    }

    private static void ApplyRandomizer(WorldManager3x3 world, Transform chunk, Transform container, WorldGenPreset preset, float scaleMid)
    {
        var randomizer = chunk.GetComponent<ChunkPropRandomizer>();

        if (!preset.reshufflePropsOnMove)
        {
            if (randomizer != null) Undo.DestroyObjectImmediate(randomizer);
            return;
        }

        if (randomizer == null) randomizer = Undo.AddComponent<ChunkPropRandomizer>(chunk.gameObject);

        Undo.RecordObject(randomizer, UndoName);
        randomizer.propContainer = container;
        randomizer.chunkWidth = world.chunkWidth;
        randomizer.chunkHeight = world.chunkHeight;
        randomizer.edgeMargin = preset.propEdgeMargin;
        randomizer.minDistance = preset.propMinDistance;
        randomizer.clumping = preset.propClumping;
        randomizer.clumpSize = preset.propClumpSize;
        randomizer.scaleRange = preset.propScaleRange;
        randomizer.randomFlipX = preset.propRandomFlipX;
        randomizer.countJitter = preset.reshuffleCountJitter;
        randomizer.editorScaleMid = scaleMid;
        EditorUtility.SetDirty(randomizer);
    }

    private static GameObject PickProp(List<WorldGenPreset.PropEntry> entries, System.Random rng)
    {
        float total = 0f;
        for (int i = 0; i < entries.Count; i++) total += entries[i].weight;

        float pick = (float)rng.NextDouble() * total;
        for (int i = 0; i < entries.Count; i++)
        {
            pick -= entries[i].weight;
            if (pick <= 0f) return entries[i].prefab;
        }
        return entries[entries.Count - 1].prefab;
    }

    // -------------------------------------------------------------- Neue Welt

    /// <summary>
    /// Legt eine komplette neue Welt an: Container mit WorldManager3x3 und neun
    /// leeren Tilemap-Chunks. Renderer-Einstellungen (Sorting Layer, Material)
    /// kommen von der Vorlage, damit die neue Welt genauso gezeichnet wird wie
    /// die bestehenden.
    /// </summary>
    public static WorldManager3x3 CreateWorld(Transform gridParent, string worldName, WorldManager3x3 template)
    {
        var root = new GameObject(worldName);
        Undo.RegisterCreatedObjectUndo(root, "Neue Welt anlegen");
        root.transform.SetParent(gridParent, false);

        var manager = Undo.AddComponent<WorldManager3x3>(root);
        if (template != null)
        {
            manager.chunkWidth = template.chunkWidth;
            manager.chunkHeight = template.chunkHeight;
        }

        TilemapRenderer templateRenderer = null;
        Tilemap templateMap = null;
        if (template != null)
        {
            var templateChunks = CollectChunks(template);
            if (templateChunks.Count > 0)
            {
                templateRenderer = templateChunks[0].GetComponent<TilemapRenderer>();
                templateMap = templateChunks[0].GetComponent<Tilemap>();
            }
        }

        var chunks = new GameObject[9];
        for (int i = 0; i < 9; i++)
        {
            var chunk = new GameObject($"{worldName}_Floor ({i + 1})");
            Undo.RegisterCreatedObjectUndo(chunk, "Neue Welt anlegen");
            chunk.transform.SetParent(root.transform, false);
            chunk.layer = template != null ? template.gameObject.layer : root.layer;

            var map = chunk.AddComponent<Tilemap>();
            var renderer = chunk.AddComponent<TilemapRenderer>();

            if (templateMap != null)
            {
                map.tileAnchor = templateMap.tileAnchor;
                map.orientation = templateMap.orientation;
                map.color = templateMap.color;
                map.animationFrameRate = templateMap.animationFrameRate;
            }

            if (templateRenderer != null)
            {
                renderer.sharedMaterial = templateRenderer.sharedMaterial;
                renderer.sortingLayerID = templateRenderer.sortingLayerID;
                renderer.sortingOrder = templateRenderer.sortingOrder;
                renderer.mode = templateRenderer.mode;
                renderer.maskInteraction = templateRenderer.maskInteraction;
                renderer.chunkSize = templateRenderer.chunkSize;
            }

            chunks[i] = chunk;
        }

        manager.chunksFlat = chunks;
        AlignChunks(manager);
        EditorUtility.SetDirty(manager);
        return manager;
    }

    private static int Mod(int value, int modulus) => ((value % modulus) + modulus) % modulus;


    private static void MarkDirty(WorldManager3x3 world)
    {
        if (world != null) EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
    }
}
