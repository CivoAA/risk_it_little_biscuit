using UnityEngine;

/// <summary>
/// Steht als einziges "Logik"-Objekt in einer Map-Szene und sagt, welche Karte
/// das hier ist. Alles andere in der Szene ist reine Welt: Grid, Tilemaps,
/// Props, der WorldManager der Welt.
///
/// Was hier landet, ist das, was sich von Karte zu Karte wirklich
/// unterscheidet: Name, alte Map-ID, Wellenplan und Lauf-Musik. Ein Spawnpunkt
/// kann spaeter danebentreten, ohne dass dafuer eine Szene angefasst werden
/// muss.
/// </summary>
[DisallowMultipleComponent]
public class MapDefinition : MonoBehaviour
{
    /// <summary>Die Karte, die gerade laeuft. Null, solange keine geladen ist.</summary>
    public static MapDefinition Active { get; private set; }

    [Tooltip("Name der Welt, aus der diese Szene entstanden ist (z.B. World1).")]
    [SerializeField] private string mapName = "";

    [Tooltip("Dieselbe ID wie MapsManager.selectedMap / MapName.mapID im alten System. " +
             "Bindeglied, solange beide Systeme nebeneinander laufen.")]
    [SerializeField] private int legacyMapId = -1;

    [Tooltip("Welcher Wellenplan aus WavePlans auf dieser Karte laeuft. " +
             "Leer = der Plan zum Weltnamen.")]
    [SerializeField] private string planId = "";

    [Tooltip("Musik, die waehrend eines Laufs auf dieser Karte spielt. " +
             "Leer = die allgemeine Lauf-Musik vom AudioController.")]
    [SerializeField] private AudioClip music;

    [Tooltip("Karte ist ein Gang, in dem man nur nach links und rechts laeuft (Kueche). " +
             "Gegner spawnen dann nur zwischen Spawn Min Y und Spawn Max Y.")]
    [SerializeField] private bool limitSpawnY;

    [Tooltip("Unterste Spawnhoehe in Weltkoordinaten (knapp ueber der unteren Wand).")]
    [SerializeField] private float spawnMinY = -9.5f;

    [Tooltip("Oberste Spawnhoehe in Weltkoordinaten (knapp unter der oberen Wand).")]
    [SerializeField] private float spawnMaxY = 9f;

    public string MapName => mapName;

    /// <summary>
    /// Spawnband der Karte: Gegner tauchen nur zwischen <paramref name="minY"/>
    /// und <paramref name="maxY"/> auf. False = die Karte ist offen, keine Grenze.
    /// Rauslaufen duerfen sie trotzdem - die Waende halten Gegner nicht auf,
    /// sie laufen von selbst wieder rein.
    /// </summary>
    public bool TryGetSpawnBand(out float minY, out float maxY)
    {
        minY = Mathf.Min(spawnMinY, spawnMaxY);
        maxY = Mathf.Max(spawnMinY, spawnMaxY);
        return limitSpawnY;
    }

    public int LegacyMapId => legacyMapId;

    /// <summary>Wellenplan der Karte - liest der <see cref="SpawnDirector"/> beim Start.</summary>
    public string PlanId => string.IsNullOrWhiteSpace(planId) ? mapName : planId;

    /// <summary>Lauf-Musik der Karte; null = allgemeine Lauf-Musik.</summary>
    public AudioClip Music => music;

    void Awake()
    {
        Active = this;

        // Der GameManager muss beim Zurueckgehen wissen, welche Map-Szene neben
        // GameCore liegt - die Szene traegt sich hier selbst ein.
        MapSceneSystem.SetRunMapScene(gameObject.scene.name);
    }

    void OnDestroy()
    {
        if (Active == this) Active = null;
        MapSceneSystem.ClearRunMapScene(gameObject.scene.name);
    }
}
