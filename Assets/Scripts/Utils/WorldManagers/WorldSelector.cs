using UnityEngine;

public class WorldSelector : MonoBehaviour
{
    [Header("World Objekte (entsprechend Map ID Index)")]
    public GameObject[] worlds;
    public GameObject[] enemySpawner;

    private int lastActiveMapID = -1;

    void Update()
    {
        if (MapsManager.Instance == null)
            return;

        int id = MapsManager.Instance.selectedMap;

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

        // Alle deaktivieren
        for (int i = 0; i < worlds.Length; i++)
        {
            if (worlds[i] != null)
                worlds[i].SetActive(false);
        }

        // Ziel-Map aktivieren
        if (id >= 0 && id < worlds.Length && worlds[id] != null)
        {
            worlds[id].SetActive(true);
            enemySpawner[id].SetActive(true);
            Debug.Log($"🌍 WorldSelector: Aktiviert -> {worlds[id].name} (ID {id})");
        }
        else
        {
            Debug.LogWarning($"⚠️ Kein gültiges World-Objekt für ID {id} gefunden!");
        }
    }
}
