using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Richtet die Kueche (World1, Szene Map_World0) mit den neuen Grafiken ein:
///  1. legt fuer jedes Sprite aus kueche_boden.png ein Tile an,
///  2. baut fuer jedes Kuechen-Objekt ein Prefab (Sprite, Fuss-Kollision, MixerBlocker),
///  3. legt Boden, Sockelleisten und Wand in allen Background-/Boundaries-Tilemaps neu,
///  4. haengt an World0 einen zweiten RandomObjectSpawner, der die Objekte verteilt.
///
/// Wiederholbar: vorhandene Tiles und Prefabs werden aktualisiert, der
/// Prop-Spawner wird wiederverwendet. Die Bilder kommen aus
/// Tools/kueche_boden.py und Tools/kueche_props.py.
/// </summary>
public static class KuecheBuilder
{
    private const string TileSheet = "Assets/Art/Tiles_Kueche/kueche_boden.png";
    private const string TileFolder = "Assets/Art/Tiles_Kueche/Tiles";
    private const string PropArtFolder = "Assets/Art/World-Objects/Kueche";
    private const string PropInfoPath = PropArtFolder + "/kueche_props.json";
    private const string PrefabFolder = "Assets/Prefabs/MapObjects/Kueche";
    private const string ScenePath = "Assets/Scenes/Maps/Map_World0.unity";
    private const string SortingLayer = "Objects";
    private const int PropLayer = 3; // "Baundaries" - wie die Baeume im Wald
    private const float Ppu = 32f;

    // Aufbau des Streifens, siehe Tools/kueche_boden.py
    private const int Variants = 10;
    private const int WallIndex = 20;
    private const int SkirtingTopIndex = 21;
    private const int SkirtingBottomIndex = 22;

    // Zeilen der Kuechen-Tilemaps (Zellkoordinaten, in jedem Chunk gleich)
    private const int FloorMinY = -10;
    private const int FloorMaxY = 9;
    private const int TopEdgeY = 10;
    private const int BottomEdgeY = -11;

    /// <summary>
    /// Haeufigkeit je Fliesen-Variante (plain, plain2, gloss, chip, crack,
    /// crumbs, flour, jam, milk, sugar). Der Boden soll ruhig bleiben.
    /// </summary>
    private static readonly float[] VariantWeights = { 40f, 40f, 6f, 1.5f, 1.5f, 2f, 1.2f, 0.6f, 0.6f, 1.2f };

    [Serializable] private class PropInfo { public string name; public int colliderW; public int colliderH; }
    [Serializable] private class PropInfoList { public List<PropInfo> props = new List<PropInfo>(); }

    [MenuItem("Tools/Welt/Kueche einrichten", false, 120)]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorUtility.DisplayDialog("Kueche", Build(), "Ok");
    }

    /// <summary>Unity -batchmode -executeMethod KuecheBuilder.BuildFromCommandLine</summary>
    public static void BuildFromCommandLine()
    {
        Debug.Log("[Kueche] " + Build());
    }

    internal static string Build()
    {
        AssetDatabase.Refresh();

        // Szene zuerst oeffnen: OpenScene entlaedt unbenutzte Assets. Vorher
        // geladene Tiles waeren danach zerstoert, und SetTile(null) loescht
        // die Zelle - so war am 07.10.2026 einmal der ganze Boden weg.
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Tile[] tiles = BuildTiles();
        if (tiles == null) return "Keine Sprites in " + TileSheet + " - erst Tools/kueche_boden.py laufen lassen.";

        List<GameObject> prefabs = BuildPrefabs();
        AssetDatabase.SaveAssets();

        int painted = PaintTilemaps(scene, tiles);
        string spawner = SetupSpawner(scene, prefabs);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        return $"{tiles.Length} Tiles, {prefabs.Count} Prefabs, {painted} Zellen neu gelegt. {spawner}";
    }

    // --- 1. Tiles ------------------------------------------------------------

    private static Tile[] BuildTiles()
    {
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(TileSheet).OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1)))
            .ToArray();
        if (sprites.Length <= SkirtingBottomIndex) return null;

        EnsureFolder(TileFolder);
        var tiles = new Tile[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            string path = $"{TileFolder}/{sprites[i].name}.asset";
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.sprite = sprites[i];
            tile.colliderType = Tile.ColliderType.Grid;
            EditorUtility.SetDirty(tile);
            tiles[i] = tile;
        }
        return tiles;
    }

    // --- 2. Prefabs ----------------------------------------------------------

    private static List<GameObject> BuildPrefabs()
    {
        var result = new List<GameObject>();
        if (!File.Exists(PropInfoPath)) return result;

        var info = JsonUtility.FromJson<PropInfoList>(File.ReadAllText(PropInfoPath));
        EnsureFolder(PrefabFolder);

        foreach (PropInfo p in info.props)
        {
            string artPath = $"{PropArtFolder}/kueche_{p.name}.png";
            Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(artPath).OfType<Sprite>().FirstOrDefault();
            if (sprite == null)
            {
                Debug.LogWarning("[Kueche] Kein Sprite: " + artPath);
                continue;
            }

            string prefabPath = $"{PrefabFolder}/Kueche_{Title(p.name)}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            GameObject root = existing != null
                ? PrefabUtility.LoadPrefabContents(prefabPath)
                : new GameObject("Kueche_" + Title(p.name));

            root.layer = PropLayer;

            var sr = GetOrAdd<SpriteRenderer>(root);
            sr.sprite = sprite;
            sr.sortingLayerName = SortingLayer;
            sr.sortingOrder = 0;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;

            // Kollision nur am Fuss: der Keks laeuft hinter dem Topf vorbei,
            // stoesst aber unten dagegen. Mindestens 12 px tief, sonst
            // rutscht er bei schneller Bewegung durch.
            var box = GetOrAdd<BoxCollider2D>(root);
            box.isTrigger = false;
            box.size = new Vector2(p.colliderW / Ppu, Mathf.Max(p.colliderH, 12) / Ppu);
            box.offset = new Vector2(0f, box.size.y * 0.3f);

            GetOrAdd<MixerBlocker>(root);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            if (existing != null) PrefabUtility.UnloadPrefabContents(root);
            else UnityEngine.Object.DestroyImmediate(root);

            result.Add(saved);
        }
        return result;
    }

    // Kein "??": GetComponent liefert in Unity ein Fake-Null, das ?? nicht erkennt.
    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private static string Title(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToUpperInvariant(name[0]) + name.Substring(1);

    // --- 3. Boden --------------------------------------------------------------

    private static int PaintTilemaps(UnityEngine.SceneManagement.Scene scene, Tile[] tiles)
    {
        float total = VariantWeights.Sum();
        int painted = 0;

        var maps = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Tilemap>(true))
            .Where(t => t.name.StartsWith("Background") || t.name.StartsWith("Boundaries"))
            .OrderBy(t => t.name)
            .ToList();

        for (int m = 0; m < maps.Count; m++)
        {
            Tilemap map = maps[m];
            // Jeder Chunk bekommt seinen eigenen Wurf, damit sich die drei
            // Boden-Stuecke beim Weiterlaufen nicht sichtbar wiederholen.
            var rng = new System.Random(4711 + m * 7919);

            map.CompressBounds();
            foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
            {
                if (!map.HasTile(cell)) continue;

                Tile tile;
                if (cell.y == TopEdgeY) tile = tiles[SkirtingTopIndex];
                else if (cell.y == BottomEdgeY) tile = tiles[SkirtingBottomIndex];
                else if (cell.y >= FloorMinY && cell.y <= FloorMaxY)
                {
                    int variant = Pick(rng, total);
                    bool light = ((cell.x + cell.y) & 1) == 0;
                    tile = tiles[variant + (light ? 0 : Variants)];
                }
                else tile = tiles[WallIndex];

                if (tile == null) continue; // nie eine Zelle loeschen
                map.SetTile(cell, tile);
                // SetTile behaelt Drehung und Farbe der alten Zelle. Aus der
                // Wald-Vorlage sind ~100 Zellen je Chunk um 180 Grad gedreht -
                // dort hing die untere Sockelleiste verkehrt herum in der Wand.
                map.SetTransformMatrix(cell, Matrix4x4.identity);
                map.SetColor(cell, Color.white);
                painted++;
            }
            EditorUtility.SetDirty(map);
        }
        return painted;
    }

    private static int Pick(System.Random rng, float total)
    {
        double roll = rng.NextDouble() * total;
        for (int i = 0; i < VariantWeights.Length; i++)
        {
            roll -= VariantWeights[i];
            if (roll < 0) return i;
        }
        return 0;
    }

    // --- 4. Spawner ------------------------------------------------------------

    private static string SetupSpawner(UnityEngine.SceneManagement.Scene scene, List<GameObject> prefabs)
    {
        if (prefabs.Count == 0) return "Keine Prefabs - Spawner nicht angefasst.";

        // Der Mixer-Spawner sitzt auf World0 - der Prop-Spawner kommt daneben.
        RandomObjectSpawner mixerSpawner = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<RandomObjectSpawner>(true))
            .FirstOrDefault(s => s.spawnablePrefabs != null && s.spawnablePrefabs.Any(MixerObject.IsMixer));
        if (mixerSpawner == null) return "Kein Mixer-Spawner in der Szene gefunden - Spawner nicht angelegt.";

        GameObject host = mixerSpawner.gameObject;
        RandomObjectSpawner propSpawner = host.GetComponents<RandomObjectSpawner>()
            .FirstOrDefault(s => s != mixerSpawner && (s.spawnablePrefabs == null || !s.spawnablePrefabs.Any(MixerObject.IsMixer)));
        bool created = propSpawner == null;
        if (created) propSpawner = host.AddComponent<RandomObjectSpawner>();

        propSpawner.spawnablePrefabs = prefabs.ToArray();
        propSpawner.blockSize = 8f;
        propSpawner.spawnAheadDistance = 100f;
        propSpawner.maxObjectsPerBlock = 3;
        // Fusspunkt-Grenzen: oben bleibt Platz fuer die hohe Milchtuete unter der Leiste.
        propSpawner.minY = -9.2f;
        propSpawner.maxY = 7.2f;
        propSpawner.propSpacing = 3.5f;
        EditorUtility.SetDirty(propSpawner);

        return created ? "Prop-Spawner an World0 angelegt." : "Prop-Spawner an World0 aktualisiert.";
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
