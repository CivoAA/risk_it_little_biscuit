using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Markiert ein Objekt (z. B. einen Baum), dessen Sprites kein Mixer-Feld
/// beruehren darf. Die Zufalls-Spawner fragen das ueber
/// <see cref="MixerPlacement"/> ab, bevor sie einen Mixer hinstellen.
/// </summary>
[DisallowMultipleComponent]
public class MixerBlocker : MonoBehaviour
{
    [Tooltip("Zusaetzlicher Abstand um die Sprites herum, in Welt-Einheiten.")]
    public float padding = 0f;

    private static readonly List<MixerBlocker> all = new List<MixerBlocker>();
    public static IReadOnlyList<MixerBlocker> All => all;

    private SpriteRenderer[] renderers;

    private void Awake()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void OnEnable()
    {
        if (!all.Contains(this)) all.Add(this);
    }

    private void OnDisable()
    {
        all.Remove(this);
    }

    /// <summary>Umriss aller Sprites in Weltkoordinaten (plus Polster).</summary>
    public bool TryGetBounds(out Bounds bounds)
    {
        bounds = default;
        bool any = false;

        foreach (SpriteRenderer sr in renderers)
        {
            if (sr == null || sr.sprite == null) continue;
            if (!any) { bounds = sr.bounds; any = true; }
            else bounds.Encapsulate(sr.bounds);
        }

        if (any && padding > 0f) bounds.Expand(padding * 2f);
        return any;
    }
}
