using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Die Gegner, die ein Wellenplan ansprechen kann. Namen statt Prefab-Felder -
/// damit ein Plan reiner Text bleibt und nicht bei jeder Aenderung durch den
/// Inspector muss.
/// </summary>
public enum EnemyId
{
    None = 0,

    // Grundgegner
    Marshmello,
    EliteMarshmello,
    EvilSlime,
    MausMitMesser,
    MiniMilch,
    SaureMilch,
    Muffin,
    Suppe,
    Pancake,
    Fetti,

    /// <summary>Eine der zehn Slime-Varianten, zufaellig gewaehlt.</summary>
    Slime,

    // Besonderes
    Blocker,              // bewegt sich kaum - das ist der Kaefig, nicht der Gegner
    MiniBossMarshmello,
    MesserMaus1,
    MesserMaus2,
    KeksKoenig,
}

/// <summary>
/// Haelt die Prefabs zu den <see cref="EnemyId"/>s und weiss, wie schwer ein
/// Gegner wiegt.
///
/// Das Gewicht ("Threat") ist die Waehrung des <see cref="SpawnDirector"/>:
/// er haelt nicht eine Stueckzahl auf dem Feld, sondern eine Summe. Ein Fetti
/// zaehlt so viel wie acht Marshmellos - dadurch bleibt der Druck gleich,
/// egal aus welchen Gegnern eine Phase besteht.
///
/// Die Prefab-Liste fuellt der Installer (Tools -> Spawns) aus dem alten
/// TimeWaveManager, damit niemand 16 Felder von Hand zieht.
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

    [Tooltip("Ein Prefab je Gegnerart. Fuellt der Installer.")]
    [SerializeField] private List<Entry> entries = new List<Entry>();

    [Tooltip("Die zehn Slime-Varianten fuer EnemyId.Slime.")]
    [SerializeField] private List<GameObject> slimeVariants = new List<GameObject>();

    private Dictionary<EnemyId, GameObject> lookup;

    /// <summary>
    /// Wie schwer ein Gegner auf dem Feld wiegt. Grob an den Werten der
    /// Prefabs entlang (Leben x Bedrohlichkeit), nicht exakt - das ist der
    /// Regler, an dem sich Balancing am schnellsten anfuehlt.
    /// </summary>
    public static float Threat(EnemyId id)
    {
        switch (id)
        {
            case EnemyId.Marshmello: return 1f;
            case EnemyId.EvilSlime: return 1.5f;
            case EnemyId.MiniMilch: return 2f;
            case EnemyId.Slime: return 2f;
            case EnemyId.MausMitMesser: return 3f;
            case EnemyId.Muffin: return 3f;
            case EnemyId.EliteMarshmello: return 4f;
            case EnemyId.SaureMilch: return 4f;
            case EnemyId.Pancake: return 4f;
            case EnemyId.Suppe: return 5f;
            case EnemyId.Fetti: return 8f;
            case EnemyId.MiniBossMarshmello: return 15f;
            case EnemyId.MesserMaus1: return 40f;
            case EnemyId.MesserMaus2: return 40f;
            case EnemyId.KeksKoenig: return 100f;

            // Der Kaefig eines Encirclements zaehlt nicht zum Druck - sonst
            // wuerde der Director waehrend des Kampfes nichts mehr nachlegen
            // und danach auf einen Schlag alles nachholen.
            case EnemyId.Blocker: return 0f;

            default: return 1f;
        }
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
    /// <summary>Nur fuer den Installer.</summary>
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
