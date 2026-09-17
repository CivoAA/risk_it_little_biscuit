using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Baut den Skilltree zur Laufzeit aus dem Katalog <see cref="SkillTrees"/> -
/// Knöpfe, Verbindungslinien und Zustände. In der Szene liegt kein einziger
/// Skill-Knoten mehr.
///
/// Diese Komponente gehört auf das SkillTreeCanvas. Für jeden Ast sucht sie ein
/// Panel namens "[Ast] Tree Panel" (also "Wind Tree Panel", "Sword Tree Panel"
/// usw.) und füllt dessen "Button Root". Findet sie keins, legt sie selbst einen
/// Behälter an - ein neuer Ast im Katalog braucht also nichts in der Szene, er
/// sieht nur hübscher aus, wenn ein gestaltetes Panel dafür existiert.
///
/// Wird der Baum ausgetauscht (später: einer pro Charakter), reicht
/// <see cref="Rebuild"/> - hier ist nichts auf den heutigen Baum festgenagelt.
/// </summary>
[DisallowMultipleComponent]
public class SkillTreeView : MonoBehaviour
{
    [Header("Aussehen")]
    [Tooltip("Kantenlänge eines Skill-Knopfes in UI-Einheiten.")]
    [SerializeField] private float nodeSize = 64f;

    [Tooltip("Dicke der Verbindungslinien.")]
    [SerializeField] private float lineThickness = 4f;

    [SerializeField] private Color lineLocked = new Color(0.39f, 0.39f, 0.39f, 0.6f);
    [SerializeField] private Color lineOpen = new Color(1f, 1f, 1f, 0.9f);

    [Tooltip("Farbe eines Knotens, der gekauft werden kann.")]
    [SerializeField] private Color tintBuyable = new Color(0.39f, 0.39f, 0.39f);

    [Tooltip("Farbe eines Knotens, dessen Vorbedingungen noch fehlen.")]
    [SerializeField] private Color tintLocked = Color.white;

    private readonly List<Entry> entries = new List<Entry>();
    private readonly List<Line> lines = new List<Line>();
    private readonly List<GameObject> spawned = new List<GameObject>();

    private SkillTreeDef built;

    private class Entry
    {
        public SkillNodeDef Def;
        public Image Icon;
        public Button Button;
    }

    private class Line
    {
        public SkillNodeDef From;
        public SkillNodeDef To;
        public Image Image;
    }

    private void OnEnable()
    {
        Skills.Changed += Refresh;
        Loc.LanguageChanged += Refresh;

        if (built != Skills.ActiveTree) Rebuild();
        else Refresh();
    }

    private void OnDisable()
    {
        Skills.Changed -= Refresh;
        Loc.LanguageChanged -= Refresh;
    }

    private void Start()
    {
        if (built == null) Rebuild();
    }

    // ==================================================================
    //  Aufbau
    // ==================================================================

    /// <summary>Wirft alles weg und baut den gerade aktiven Baum neu.</summary>
    public void Rebuild()
    {
        Clear();

        SkillTreeDef tree = Skills.ActiveTree;
        built = tree;

        if (tree == null) return;

        foreach (SkillBranchDef branch in tree.Branches)
        {
            RectTransform root = ResolveBranchRoot(branch);
            if (root == null) continue;

            // Erst die Linien, damit sie hinter den Knöpfen liegen.
            foreach (SkillNodeDef node in branch.Nodes)
            {
                foreach (SkillNodeDef parent in node.Requires) AddLine(root, parent, node);
            }

            foreach (SkillNodeDef node in branch.Nodes) AddNode(root, node);
        }

        Refresh();
    }

    private void Clear()
    {
        foreach (GameObject go in spawned)
        {
            if (go != null) Destroy(go);
        }

        spawned.Clear();
        entries.Clear();
        lines.Clear();
    }

    /// <summary>
    /// Sucht den Behälter des Astes in der Szene. Vorhandene, gestaltete Panels
    /// gewinnen - sonst wird einer angelegt, damit ein neuer Ast sofort sichtbar ist.
    /// </summary>
    private RectTransform ResolveBranchRoot(SkillBranchDef branch)
    {
        string panelName = branch.NameEn + " Tree Panel";

        foreach (RectTransform rt in GetComponentsInChildren<RectTransform>(true))
        {
            if (!string.Equals(rt.name, panelName, System.StringComparison.OrdinalIgnoreCase)) continue;

            RectTransform buttonRoot = FindChild(rt, "Button Root");
            return buttonRoot != null ? buttonRoot : rt;
        }

        Debug.Log($"[Skills] Kein Panel '{panelName}' gefunden - für den Ast '{branch.Id}' " +
                  "wird ein einfacher Behälter angelegt.");

        GameObject holder = new GameObject(branch.Id + " Branch", typeof(RectTransform));
        holder.transform.SetParent(transform, false);
        spawned.Add(holder);

        return (RectTransform)holder.transform;
    }

    private static RectTransform FindChild(RectTransform parent, string childName)
    {
        foreach (RectTransform rt in parent.GetComponentsInChildren<RectTransform>(true))
        {
            if (rt != parent && string.Equals(rt.name, childName, System.StringComparison.OrdinalIgnoreCase))
                return rt;
        }
        return null;
    }

    private void AddNode(RectTransform root, SkillNodeDef def)
    {
        GameObject go = new GameObject("Skill_" + def.LocalId, typeof(RectTransform));
        go.transform.SetParent(root, false);
        spawned.Add(go);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(nodeSize, nodeSize);
        rt.anchoredPosition = SkillTreeLayout.PositionOf(def);

        Image icon = go.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = true;

        Button button = go.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = icon;

        SkillNodeDef captured = def;
        button.onClick.AddListener(() => OnClicked(captured));

        SkillNodeHover hover = go.AddComponent<SkillNodeHover>();
        hover.Setup(def);

        entries.Add(new Entry { Def = def, Icon = icon, Button = button });
    }

    private void AddLine(RectTransform root, SkillNodeDef from, SkillNodeDef to)
    {
        GameObject go = new GameObject($"Line_{from.LocalId}_{to.LocalId}", typeof(RectTransform));
        go.transform.SetParent(root, false);
        spawned.Add(go);

        Vector2 a = SkillTreeLayout.PositionOf(from);
        Vector2 b = SkillTreeLayout.PositionOf(to);
        Vector2 delta = b - a;

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(delta.magnitude, lineThickness);
        rt.anchoredPosition = a + delta * 0.5f;
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

        Image img = go.AddComponent<Image>();
        img.raycastTarget = false;

        lines.Add(new Line { From = from, To = to, Image = img });
    }

    // ==================================================================
    //  Zustand
    // ==================================================================

    public void Refresh()
    {
        foreach (Entry e in entries)
        {
            if (e.Icon == null) continue;

            bool unlocked = Skills.IsUnlocked(e.Def);
            bool open = !unlocked && Skills.RequirementsMet(e.Def);

            e.Icon.sprite = SkillIcons.For(e.Def, unlocked);
            e.Icon.color = unlocked ? Color.white : open ? tintBuyable : tintLocked;

            if (e.Button != null) e.Button.interactable = open;
        }

        foreach (Line l in lines)
        {
            if (l.Image == null) continue;
            l.Image.color = Skills.IsUnlocked(l.From) ? lineOpen : lineLocked;
        }
    }

    private void OnClicked(SkillNodeDef def)
    {
        if (Skills.TryUnlock(def))
        {
            AudioController ac = AudioController.Instance;
            if (ac != null) ac.PalySound(ac.MenuClick);
        }
        else
        {
            AudioController ac = AudioController.Instance;
            if (ac != null) ac.PalySound(ac.PlayerHit);
        }
    }
}

