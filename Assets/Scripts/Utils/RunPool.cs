using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Wiederverwendung fuer Objekte, die waehrend eines Laufs zu Hunderten
/// entstehen und gleich wieder verschwinden: Todeseffekte und XP-Bonbons.
///
/// Warum: stirbt ein grosser Haufen auf einmal, legte jeder Kill einen
/// Effekt und ein Bonbon neu an. 150 Kills kosteten so 50+ ms in einem
/// einzigen Frame - der Rest des Rucklers nach dem Spawn-Fix.
///
/// Die Objekte liegen wie alles aus dem Lauf in der Lauf-Szene (siehe
/// <see cref="RunScene"/>) und verschwinden mit ihr. Was danach noch im Pool
/// steht, ist dann "null" und wird beim Herausnehmen uebersprungen.
/// </summary>
public static class RunPool
{
    /// <summary>Merkt sich das Prefab, aus dem ein Objekt stammt.</summary>
    private class Pooled : MonoBehaviour
    {
        public GameObject Source;
    }

    private static readonly Dictionary<GameObject, Stack<GameObject>> free = new Dictionary<GameObject, Stack<GameObject>>();

    /// <summary>Holt ein freies Objekt aus dem Pool oder legt ein neues an - aktiv, an der Stelle.</summary>
    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, string container)
    {
        if (prefab == null) return null;

        Scene run = RunScene.Current;

        if (free.TryGetValue(prefab, out Stack<GameObject> stack))
        {
            while (stack.Count > 0)
            {
                GameObject reused = stack.Pop();
                if (reused == null) continue;

                // Aus einem frueheren Lauf oder ausserhalb der Lauf-Szene
                // entstanden: nicht wiederverwenden, sonst liegt es im Hub.
                if (run.IsValid() && reused.scene != run)
                {
                    Object.Destroy(reused);
                    continue;
                }

                reused.transform.SetPositionAndRotation(position, rotation);
                reused.SetActive(true);
                return reused;
            }
        }

        GameObject created = Object.Instantiate(prefab, position, rotation);
        created.AddComponent<Pooled>().Source = prefab;
        RunScene.Place(created, container);
        return created;
    }

    /// <summary>
    /// Gibt ein Objekt zurueck. Stammt es nicht aus dem Pool, wird es wie
    /// frueher zerstoert.
    /// </summary>
    public static void Release(GameObject go)
    {
        if (go == null) return;

        Pooled pooled = go.GetComponent<Pooled>();
        if (pooled == null || pooled.Source == null)
        {
            Object.Destroy(go);
            return;
        }

        if (!go.activeSelf) return; // schon zurueckgegeben

        go.SetActive(false);
        if (!free.TryGetValue(pooled.Source, out Stack<GameObject> stack))
        {
            stack = new Stack<GameObject>();
            free[pooled.Source] = stack;
        }
        stack.Push(go);
    }

    /// <summary>
    /// Legt vorab <paramref name="count"/> freie Objekte an - beim Laden des
    /// Laufs statt mitten im ersten grossen Gefecht.
    /// </summary>
    public static void Prewarm(GameObject prefab, int count, string container)
    {
        // Ohne Lauf-Szene blieben die Objekte in der aktiven Szene (dem Hub) liegen.
        Scene run = RunScene.Current;
        if (prefab == null || !run.IsValid() || !run.isLoaded) return;

        var made = new List<GameObject>(count);
        for (int i = 0; i < count; i++) made.Add(Spawn(prefab, new Vector3(0f, -10000f, 0f), Quaternion.identity, container));
        foreach (GameObject go in made) Release(go);
    }
}
