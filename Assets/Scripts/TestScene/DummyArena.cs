using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Verwaltet die Trainings-Dummies der Test-Szene und schaltet zwischen
/// Einzelziel und einer Gruppe um.
///
/// Hintergrund: Mit nur einem Dummy sieht eine Single-Target-Waffe immer besser
/// aus als eine AoE-Waffe, obwohl im echten Spiel meistens mehrere Gegner
/// gleichzeitig dastehen. Mit dem Ring aus Dummies lassen sich beide Bauarten
/// fair vergleichen – der <see cref="DamageMeter"/> zählt den Schaden an allen
/// Dummies zusammen.
///
/// Die Dummies werden gepoolt, nicht ständig erzeugt und zerstört: beim
/// Deaktivieren feuert Unity OnTriggerExit2D, sodass die Waffen sie sauber aus
/// ihren Ziellisten werfen.
/// </summary>
public class DummyArena : MonoBehaviour
{
    [Header("Gruppen")]
    [Tooltip("Anzahl Dummies, durch die der Button der Reihe nach schaltet.")]
    public int[] dummyCounts = { 1, 10 };

    [Header("Aufstellung")]
    [Tooltip("Abstand des einzelnen Dummies vom Spieler.")]
    public float singleDistance = 4f;

    [Tooltip("Radius des Rings, auf dem die Gruppe um den Spieler steht.")]
    public float ringRadius = 4f;

    private readonly List<TrainingDummy> pool = new List<TrainingDummy>();
    private TrainingDummy template;
    private int countIndex;

    /// <summary>Wie viele Dummies gerade aktiv sind.</summary>
    public int CurrentCount
    {
        get
        {
            if (dummyCounts == null || dummyCounts.Length == 0)
            {
                return 1;
            }
            return Mathf.Max(1, dummyCounts[Mathf.Clamp(countIndex, 0, dummyCounts.Length - 1)]);
        }
    }

    void Start()
    {
        template = FindFirstObjectByType<TrainingDummy>(FindObjectsInactive.Include);

        if (template == null)
        {
            Debug.LogWarning("[DummyArena] Kein TrainingDummy in der Szene gefunden – Arena deaktiviert.");
            enabled = false;
            return;
        }

        pool.Add(template);
        Rearrange();
    }

    /// <summary>Schaltet auf die nächste Gruppengröße und stellt neu auf.</summary>
    public void Cycle()
    {
        if (dummyCounts != null && dummyCounts.Length > 0)
        {
            countIndex = (countIndex + 1) % dummyCounts.Length;
        }

        Rearrange();
    }

    /// <summary>Stellt die aktuelle Gruppe neu um den Spieler herum auf.</summary>
    public void Rearrange()
    {
        if (template == null)
        {
            return;
        }

        int count = CurrentCount;
        EnsurePool(count);

        Vector3 center = PlayerController.Instance != null
            ? PlayerController.Instance.transform.position
            : transform.position;

        for (int i = 0; i < pool.Count; i++)
        {
            TrainingDummy dummy = pool[i];
            if (dummy == null)
            {
                continue;
            }

            bool used = i < count;
            if (used)
            {
                // Erst setzen, dann einschalten – sonst blitzt der Dummy
                // einen Frame lang an der alten Stelle auf.
                dummy.transform.position = PositionFor(i, count, center);
            }

            dummy.gameObject.SetActive(used);
        }
    }

    private Vector3 PositionFor(int index, int count, Vector3 center)
    {
        if (count <= 1)
        {
            return center + new Vector3(0f, singleDistance, 0f);
        }

        // Bei 12 Uhr anfangen und im Uhrzeigersinn verteilen.
        float angle = 90f - index * (360f / count);
        float rad = angle * Mathf.Deg2Rad;
        return center + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * ringRadius;
    }

    private void EnsurePool(int count)
    {
        pool.RemoveAll(d => d == null);

        while (pool.Count < count)
        {
            GameObject clone = Instantiate(template.gameObject, template.transform.position, Quaternion.identity);
            clone.name = $"Training Dummy ({pool.Count + 1})";
            pool.Add(clone.GetComponent<TrainingDummy>());
        }
    }
}
