using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Pixelgenaue Umrandung fuer einen oder mehrere SpriteRenderer, ohne eigenen Shader:
/// legt acht getoente Kopien des Sprites einen Pixel versetzt dahinter.
/// Folgt Animationen, weil das Sprite jeden Frame nachgezogen wird.
///
/// Mehrteilige Objekte: einfach alle Teile eintragen (oder leer lassen, dann werden
/// alle SpriteRenderer im Objekt und seinen Kindern genommen). Alle Kopien landen
/// hinter dem hintersten Teil, damit die Umrandung nicht in den inneren Nahtstellen
/// zwischen den Teilen auftaucht.
///
/// Achtung: funktioniert nur mit SpriteRenderern. Kacheln in einer Tilemap haben
/// keine eigenen Renderer und lassen sich so nicht umranden.
/// </summary>
[DisallowMultipleComponent]
public class SpriteOutline : MonoBehaviour
{
    // 8 Richtungen statt 4 - sonst bleiben die diagonalen Ecken offen
    static readonly Vector2[] Dirs =
    {
        new Vector2( 1,  0), new Vector2(-1,  0), new Vector2( 0,  1), new Vector2( 0, -1),
        new Vector2( 1,  1), new Vector2(-1,  1), new Vector2( 1, -1), new Vector2(-1, -1),
    };

    [Tooltip("Leer = alle SpriteRenderer an diesem Objekt und in seinen Kindern.")]
    [SerializeField] private SpriteRenderer[] targets;
    [SerializeField] private Color outlineColor = new Color32(0xB8, 0x86, 0x0B, 0xFF);
    [Tooltip("Staerke in Sprite-Pixeln. Die Weltbreite kommt aus der PPU des Sprites.")]
    [SerializeField, Range(1, 4)] private int thicknessPx = 1;

    readonly List<SpriteRenderer> parts = new List<SpriteRenderer>();
    SpriteRenderer[][] copies;      // [teil][richtung]
    int baseOrder;
    bool visible;

    public Color OutlineColor
    {
        get => outlineColor;
        set
        {
            outlineColor = value;
            if (copies == null) return;
            foreach (var row in copies)
                foreach (var c in row) if (c) c.color = value;
        }
    }

    public int ThicknessPx { get => thicknessPx; set => thicknessPx = Mathf.Clamp(value, 1, 4); }

    /// <summary>Teile nachtraeglich setzen, z.B. aus HubInteractable.</summary>
    public void SetTargets(SpriteRenderer[] renderers)
    {
        targets = renderers;
        Rebuild();
    }

    void Awake() => Rebuild();

    void Rebuild()
    {
        Clear();

        // Wichtig: erst sammeln, dann bauen - sonst wuerde GetComponentsInChildren
        // die eigenen Outline-Kopien als neue Teile einsammeln.
        parts.Clear();
        if (targets != null && targets.Length > 0)
        {
            foreach (var t in targets) if (t != null) parts.Add(t);
        }
        else
        {
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
                if (sr.GetComponentInParent<SpriteOutline>() == this && !IsCopy(sr)) parts.Add(sr);
        }

        if (parts.Count == 0)
        {
            Debug.LogWarning($"{name}: SpriteOutline findet keinen SpriteRenderer. " +
                             "Kacheln aus einer Tilemap lassen sich nicht umranden - " +
                             "dafuer muss das Objekt aus echten Sprites bestehen.");
            enabled = false;
            return;
        }

        // Alle Kopien hinter das hinterste Teil legen. Sonst zeichnet Teil A seine
        // Umrandung ueber Teil B und die inneren Kanten werden sichtbar.
        baseOrder = int.MaxValue;
        foreach (var p in parts) baseOrder = Mathf.Min(baseOrder, p.sortingOrder);
        baseOrder -= 1;

        copies = new SpriteRenderer[parts.Count][];
        for (int i = 0; i < parts.Count; i++)
        {
            var holder = new GameObject("Outline").transform;
            holder.SetParent(parts[i].transform, false);
            holder.localPosition = Vector3.zero;
            holder.localRotation = Quaternion.identity;
            holder.localScale = Vector3.one;

            copies[i] = new SpriteRenderer[Dirs.Length];
            for (int k = 0; k < Dirs.Length; k++)
            {
                var go = new GameObject("o" + k);
                go.layer = parts[i].gameObject.layer;
                go.transform.SetParent(holder, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.color = outlineColor;
                copies[i][k] = sr;
            }
        }
        SetVisible(visible);
    }

    static bool IsCopy(SpriteRenderer sr)
    {
        Transform t = sr.transform.parent;
        return t != null && t.name == "Outline";
    }

    void Clear()
    {
        if (copies == null) return;
        foreach (var row in copies)
            foreach (var c in row)
                if (c) { if (Application.isPlaying) Destroy(c.transform.parent.gameObject);
                         else DestroyImmediate(c.transform.parent.gameObject); break; }
        copies = null;
    }

    public void SetVisible(bool v)
    {
        visible = v;
        if (copies == null) return;
        foreach (var row in copies)
            foreach (var c in row) if (c) c.enabled = v;
        if (v) Sync();
    }

    // LateUpdate: erst laufen lassen, was das Sprite setzt (Animator), dann kopieren
    void LateUpdate()
    {
        if (visible) Sync();
    }

    void Sync()
    {
        if (copies == null) return;

        for (int i = 0; i < parts.Count; i++)
        {
            var p = parts[i];
            if (p == null) continue;

            Sprite s = p.sprite;
            bool on = s != null && p.enabled;
            float unit = (on && s.pixelsPerUnit > 0f) ? thicknessPx / s.pixelsPerUnit : 0f;

            for (int k = 0; k < copies[i].Length; k++)
            {
                var c = copies[i][k];
                if (c == null) continue;
                c.enabled = on;
                if (!on) continue;

                c.sprite = s;
                c.flipX = p.flipX;
                c.flipY = p.flipY;
                c.sortingLayerID = p.sortingLayerID;
                c.sortingOrder = baseOrder;          // hinter allen Teilen
                c.color = outlineColor;
                c.transform.localPosition = new Vector3(Dirs[k].x * unit, Dirs[k].y * unit, 0f);
            }
        }
    }
}
