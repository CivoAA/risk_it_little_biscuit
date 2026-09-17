using UnityEngine;

/// <summary>
/// Schaltet in der Game-Szene die Welt frei, die im <see cref="MapsManager"/>
/// steht. Fehlt der Manager ganz (Szene direkt gestartet), faellt die Auswahl
/// auf Welt 0 zurueck - sonst bliebe die Szene leer.
/// </summary>
public class WorldSelector : MonoBehaviour
{
    [Header("World Objekte (entsprechend Map ID Index)")]
    public GameObject[] worlds;
    public GameObject[] enemySpawner;

    private int lastActiveMapID = -1;

    void Update()
    {
        // Ohne Manager gibt es keine Auswahl - dann gilt Welt 0.
        int id = MapsManager.Instance != null ? MapsManager.Instance.selectedMap : 0;

        // Wenn sich die Map geändert hat oder noch keine aktiv ist
        if (id != lastActiveMapID)
        {
            ActivateWorld(id);
            lastActiveMapID = id;
        }
    }

    private void ActivateWorld(int id)
    {
        // Sicherheitscheck
        if (worlds == null || worlds.Length == 0)
        {
            Debug.LogWarning("⚠️ Keine World-Objekte zugewiesen!");
            return;
        }

        if (id < 0 || id >= worlds.Length || worlds[id] == null)
        {
            Debug.LogWarning($"⚠️ Kein gültiges World-Objekt für ID {id} - es bleibt bei Welt 0.");
            id = 0;
            if (worlds[0] == null) return;
        }

        // Alle deaktivieren
        for (int i = 0; i < worlds.Length; i++)
        {
            if (worlds[i] != null)
                worlds[i].SetActive(false);
        }

        // Ziel-Map aktivieren
        worlds[id].SetActive(true);

        // Der Spawner ist optional und muss nicht fuer jede Welt gefuellt sein.
        if (enemySpawner != null && id < enemySpawner.Length && enemySpawner[id] != null)
            enemySpawner[id].SetActive(true);

        Debug.Log($"🌍 WorldSelector: Aktiviert -> {worlds[id].name} (ID {id})");
    }
}
