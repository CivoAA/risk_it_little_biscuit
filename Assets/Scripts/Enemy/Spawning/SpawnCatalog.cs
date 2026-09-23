using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Haelt die Prefabs zu den <see cref="EnemyId"/>s.
///
/// Was ein Gegner KANN (Leben, Schaden, Tempo, Gewicht) steht seit dem
/// Remaster im <see cref="EnemyCatalog"/> im Code. Hier bleibt nur die eine
/// Sache, die nicht in Code passt: welches Prefab-Asset zu welcher Id gehoert.
/// Das ist eine Referenz auf eine Datei, und die kann nur Unity aufloesen.
///
/// Die Liste fuellt der Installer (Tools -> Spawns) beziehungsweise die
/// Gegner-Werkstatt (Tools -> Gegner), damit niemand 20 Felder von Hand zieht.
/// </summary>
[DisallowMultipleComponent]
public class SpawnCatalog : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        public EnemyId id;
        public GameObject prefab;
    }

    [Tooltip("Ein Prefab je Gegnerart. Fuellt der Installer oder die Werkstatt.")]
    [SerializeField] private List<Entry> entries = new List<Entry>();

    [Tooltip("Die zehn Slime-Varianten fuer EnemyId.Slime.")]
    [SerializeField] private List<GameObject> slimeVariants = new List<GameObject>();

    private Dictionary<EnemyId, GameObject> lookup;

    /// <summary>
    /// Wie schwer ein Gegner auf dem Feld wiegt.
    ///
    /// Bleibt als Durchreiche stehen, weil der <see cref="SpawnDirector"/> und
    /// die Wellenplaene seit jeher hierueber fragen. Die Zahl selbst steht
    /// jetzt beim Gegner im <see cref="EnemyCatalog"/> - dort, wo auch sein
    /// Leben steht, damit beim Balancing beides zusammen im Blick ist.
    /// </summary>
    public static float Threat(EnemyId id)
    {
        return EnemyCatalog.Threat(id);
    }

    public GameObject Prefab(EnemyId id)
    {
        if (id == EnemyId.Slime)
        {
            if (slimeVariants == null || slimeVariants.Count == 0) return null;
            return slimeVariants[Random.Range(0, slimeVariants.Count)];
        }

        EnsureLookup();
        return lookup.TryGetValue(id, out GameObject prefab) ? prefab : null;
    }

    public bool Has(EnemyId id)
    {
        return Prefab(id) != null;
    }

    private void EnsureLookup()
    {
        if (lookup != null) return;

        lookup = new Dictionary<EnemyId, GameObject>();
        foreach (Entry entry in entries)
        {
            if (entry == null || entry.prefab == null || entry.id == EnemyId.None) continue;
            lookup[entry.id] = entry.prefab;
        }
    }

#if UNITY_EDITOR
    /// <summary>Nur fuer den Installer und die Werkstatt.</summary>
    public void EditorSet(EnemyId id, GameObject prefab)
    {
        if (prefab == null || id == EnemyId.None) return;

        foreach (Entry entry in entries)
        {
            if (entry.id != id) continue;
            entry.prefab = prefab;
            lookup = null;
            return;
        }

        entries.Add(new Entry { id = id, prefab = prefab });
        lookup = null;
    }

    /// <summary>Nur fuer den Installer.</summary>
    public void EditorSetSlimes(IEnumerable<GameObject> variants)
    {
        slimeVariants = new List<GameObject>();
        foreach (GameObject variant in variants)
        {
            if (variant != null) slimeVariants.Add(variant);
        }
    }

    /// <summary>Nur fuer den Installer: was fehlt noch?</summary>
    public List<EnemyId> EditorMissing()
    {
        var missing = new List<EnemyId>();
        foreach (EnemyId id in System.Enum.GetValues(typeof(EnemyId)))
        {
            if (id == EnemyId.None) continue;
            if (!Has(id)) missing.Add(id);
        }
        return missing;
    }
#endif
}
