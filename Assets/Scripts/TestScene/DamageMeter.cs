using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sammelt allen Schaden, der in der Test-Szene an Trainings-Dummies ausgeteilt
/// wird, und rechnet daraus Last-Hit, DPS (gleitendes Fenster), DPM und
/// Gesamtwerte.
///
/// Gemessen wird mit skalierter Zeit: Pausen und offene Level-Up-Panels
/// (Time.timeScale = 0) verfälschen die Werte dadurch nicht.
/// </summary>
public class DamageMeter : MonoBehaviour
{
    public static DamageMeter Instance { get; private set; }

    [Header("Messfenster")]
    [Tooltip("Fenster für den DPS-Wert in Sekunden.")]
    public float dpsWindow = 5f;

    [Tooltip("Fenster für den DPM-Wert in Sekunden.")]
    public float dpmWindow = 60f;

    [Header("Messwerte (nur Anzeige)")]
    public float lastHit;
    public bool lastHitWasCrit;
    public float totalDamage;
    public int hitCount;
    public int critCount;

    /// <summary>Gemessene Zeit seit dem letzten Reset (ohne Pausen).</summary>
    public float Elapsed { get; private set; }

    private struct Hit
    {
        public float time;
        public float amount;
    }

    private readonly List<Hit> hits = new List<Hit>();
    private float clock;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Update()
    {
        clock += Time.deltaTime;
        Elapsed = clock;
        TrimOldHits();
    }

    /// <summary>Einen Treffer melden. Tut nichts, wenn kein Meter existiert.</summary>
    public static void Report(float amount, bool crit)
    {
        if (Instance != null)
        {
            Instance.Add(amount, crit);
        }
    }

    public void Add(float amount, bool crit)
    {
        lastHit = amount;
        lastHitWasCrit = crit;
        totalDamage += amount;
        hitCount++;
        if (crit)
        {
            critCount++;
        }

        hits.Add(new Hit { time = clock, amount = amount });
    }

    public void ResetMeter()
    {
        hits.Clear();
        clock = 0f;
        Elapsed = 0f;
        lastHit = 0f;
        lastHitWasCrit = false;
        totalDamage = 0f;
        hitCount = 0;
        critCount = 0;
    }

    /// <summary>Schaden pro Sekunde über das DPS-Fenster.</summary>
    public float Dps => WindowDamage(dpsWindow) / Mathf.Max(0.0001f, Mathf.Min(dpsWindow, clock));

    /// <summary>Schaden pro Minute, hochgerechnet aus dem DPM-Fenster.</summary>
    public float Dpm => WindowDamage(dpmWindow) / Mathf.Max(0.0001f, Mathf.Min(dpmWindow, clock)) * 60f;

    /// <summary>Durchschnittlicher Schaden pro Sekunde seit dem letzten Reset.</summary>
    public float AverageDps => clock <= 0.0001f ? 0f : totalDamage / clock;

    public float CritRate => hitCount == 0 ? 0f : (float)critCount / hitCount;

    private float WindowDamage(float window)
    {
        float cutoff = clock - window;
        float sum = 0f;
        for (int i = hits.Count - 1; i >= 0; i--)
        {
            if (hits[i].time < cutoff)
            {
                break;
            }
            sum += hits[i].amount;
        }
        return sum;
    }

    private void TrimOldHits()
    {
        float cutoff = clock - Mathf.Max(dpsWindow, dpmWindow);
        int drop = 0;
        while (drop < hits.Count && hits[drop].time < cutoff)
        {
            drop++;
        }
        if (drop > 0)
        {
            hits.RemoveRange(0, drop);
        }
    }
}
