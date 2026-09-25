using UnityEngine;

/// <summary>
/// Steht als einziges "Logik"-Objekt in einer Map-Szene und sagt, welche Karte
/// das hier ist. Alles andere in der Szene ist reine Welt: Grid, Tilemaps,
/// Props, der WorldManager der Welt.
///
/// Was hier landet, ist das, was sich von Karte zu Karte wirklich
/// unterscheidet. Im Moment sind das nur Name und die alte Map-ID; Spawnpunkt,
/// Musik oder ein Wave-Preset koennen spaeter danebentreten, ohne dass dafuer
/// eine Szene angefasst werden muss.
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

    public string MapName => mapName;

    public int LegacyMapId => legacyMapId;

    /// <summary>Wellenplan der Karte - liest der <see cref="SpawnDirector"/> beim Start.</summary>
    public string PlanId => string.IsNullOrWhiteSpace(planId) ? mapName : planId;

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
