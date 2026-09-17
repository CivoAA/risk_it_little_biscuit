using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Zeigt den Tooltip, solange die Maus über einem Skill-Knoten steht. Wird von
/// <see cref="SkillTreeView"/> an jeden erzeugten Knoten gehängt.
/// </summary>
public class SkillNodeHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private SkillNodeDef def;

    public void Setup(SkillNodeDef node) => def = node;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (def == null || SkillTooltip.Instance == null) return;

        // Nur die Vorbedingungen zeigen, die noch fehlen - alles andere ist Lärm.
        var missing = new List<string>();
        foreach (SkillNodeDef parent in def.Requires)
        {
            if (!Skills.IsUnlocked(parent)) missing.Add(parent.Name);
        }

        SkillTooltip.Instance.Show(def.Name, def.Price, def.Description, missing);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SkillTooltip.Instance?.Hide();
    }
}
