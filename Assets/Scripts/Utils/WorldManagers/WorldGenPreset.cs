using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Rezept fuer eine 3x3-Endloswelt: welche Tiles wie haeufig auf dem Boden
/// landen und wie viele Props (Baeume) pro Chunk verteilt werden.
/// Gelesen wird das Preset vom Welt-Generator (Tools -> Map -> Welt-Generator);
/// zur Laufzeit braucht nur der <see cref="ChunkPropRandomizer"/> ein paar der
/// Prop-Werte, die der Generator direkt an die Chunks weiterreicht.
/// </summary>
[CreateAssetMenu(fileName = "WorldGenPreset", menuName = "Map/Welt-Generator Preset")]
public class WorldGenPreset : ScriptableObject
{
    [Serializable]
    public class TileEntry
    {
        public TileBase tile;

        [Tooltip("Relative Haeufigkeit. 0 = kommt nicht vor, 5 = fuenfmal so oft wie ein Tile mit 1.")]
        [Range(0f, 20f)] public float weight = 1f;
    }

    /// <summary>
    /// Eine Farbgruppe, die als zusammenhaengender Fleck auf den Untergrund
    /// gemalt wird - z.B. alle lila Blumen-Tiles.
    /// </summary>
    [Serializable]
    public class TileGroup
    {
        public string name = "Gruppe";

        [Tooltip("Wie oft diese Gruppe im Vergleich zu den anderen als Fleck vorkommt.")]
        [Range(0f, 20f)] public float weight = 1f;

        [Tooltip("Wie dicht der Fleck gefuellt ist. 1 = randvoll, 0.5 = die Haelfte bleibt Untergrund.")]
        [Range(0.05f, 1f)] public float density = 0.7f;

        [Tooltip("Die Tiles dieser Gruppe - innerhalb des Flecks werden sie nach Gewicht gemischt.")]
        public List<TileEntry> tiles = new List<TileEntry>();
    }

    [Serializable]
    public class PropEntry
    {
        public GameObject prefab;

        [Tooltip("Relative Haeufigkeit gegenueber den anderen Prefabs in der Liste.")]
        [Range(0f, 20f)] public float weight = 1f;
    }

    [Header("🎲 Zufall")]
    [Tooltip("Gleicher Seed = gleiches Ergebnis. Im Fenster gibt es einen Wuerfel-Button.")]
    public int seed = 12345;

    [Header("🌿 Untergrund")]
    [Tooltip("Gras- und Blank-Tiles. Die liegen ueberall dort, wo kein Farbfleck ist. " +
             "Je hoeher das Gewicht des blanken Tiles, desto ruhiger sieht der Boden aus.")]
    public List<TileEntry> backgroundTiles = new List<TileEntry>();

    [Header("🌸 Farb-Flecken")]
    [Tooltip("Je eine Gruppe pro Farbe - die Tiles einer Gruppe landen immer zusammen auf einem Haufen.")]
    public List<TileGroup> patchGroups = new List<TileGroup>();

    [Tooltip("REGLER 1 - Wie gross ein Farbfleck ist (Durchmesser in Tiles). " +
             "Klein = viele kleine Tupfer, gross = dicke Farbinseln.")]
    [Range(2, 30)] public int patchSize = 8;

    [Tooltip("REGLER 2 - Wie viel Untergrund zwischen den Flecken liegt. " +
             "0 = die Flecken stossen aneinander und die Farben vermischen sich, " +
             "1 = weit auseinander mit viel Gras dazwischen.")]
    [Range(0f, 1f)] public float patchSpacing = 0.45f;

    [Header("🌳 Props (Baeume & Co.)")]
    public List<PropEntry> props = new List<PropEntry>();

    [Tooltip("Wie viele Props pro Chunk platziert werden (ein Chunk ist standardmaessig 52x40 Tiles gross).")]
    [Range(0, 400)] public int propsPerChunk = 40;

    [Tooltip("Mindestabstand zwischen zwei Props, in Tiles.")]
    [Range(0f, 20f)] public float propMinDistance = 3f;

    [Tooltip("0 = gleichmaessig ueber den Chunk verteilt, 1 = dichte Waldstuecke mit freien Lichtungen dazwischen.")]
    [Range(0f, 1f)] public float propClumping = 0.4f;

    [Tooltip("Wie gross ein Waldstueck ungefaehr ist (in Tiles). Wirkt nur, wenn 'Clumping' groesser 0 ist.")]
    [Range(2, 40)] public int propClumpSize = 14;

    [Tooltip("Streifen am Chunkrand, der frei bleibt (in Tiles).")]
    [Range(0f, 10f)] public float propEdgeMargin = 1f;

    [Tooltip("Radius um den Startpunkt (Mitte des mittleren Chunks), in dem keine Props stehen - " +
             "damit der Spieler nicht im Baum startet.")]
    [Range(0f, 30f)] public float startClearRadius = 8f;

    [Tooltip("Zufaellige Groesse relativ zur Prefab-Skalierung (x = kleinste, y = groesste).")]
    public Vector2 propScaleRange = new Vector2(0.9f, 1.15f);

    [Tooltip("Spiegelt einen Teil der Props horizontal, damit sie nicht alle identisch aussehen.")]
    public bool propRandomFlipX = true;

    [Header("♾️ Endlos-Gefuehl")]
    [Tooltip("Haengt jedem Chunk einen ChunkPropRandomizer an: sobald der WorldManager3x3 einen Chunk " +
             "nach vorne umsetzt, werden dessen Props neu gewuerfelt. Der Spieler sieht dann nie zweimal " +
             "denselben Wald, obwohl nur 9 Tilemaps im Kreis geschoben werden.")]
    public bool reshufflePropsOnMove = true;

    [Tooltip("Anteil der Props, der beim Nachruecken zufaellig ausgeblendet wird - sorgt fuer " +
             "unterschiedlich dichte Gegenden.")]
    [Range(0f, 0.9f)] public float reshuffleCountJitter = 0.3f;

    /// <summary>Summe der Gewichte einer Tile-Liste (Eintraege ohne Tile zaehlen nicht mit).</summary>
    public static float TotalTileWeight(List<TileEntry> entries)
    {
        float sum = 0f;
        if (entries == null) return sum;
        for (int i = 0; i < entries.Count; i++)
            if (entries[i] != null && entries[i].tile != null) sum += Mathf.Max(0f, entries[i].weight);
        return sum;
    }

    /// <summary>Summe aller Gruppen-Gewichte (leere Gruppen zaehlen nicht mit).</summary>
    public float TotalGroupWeight()
    {
        float sum = 0f;
        for (int i = 0; i < patchGroups.Count; i++)
        {
            var group = patchGroups[i];
            if (group != null && TotalTileWeight(group.tiles) > 0f) sum += Mathf.Max(0f, group.weight);
        }
        return sum;
    }

    /// <summary>Summe aller Prop-Gewichte (Eintraege ohne Prefab zaehlen nicht mit).</summary>
    public float TotalPropWeight()
    {
        float sum = 0f;
        for (int i = 0; i < props.Count; i++)
            if (props[i] != null && props[i].prefab != null) sum += Mathf.Max(0f, props[i].weight);
        return sum;
    }
}
