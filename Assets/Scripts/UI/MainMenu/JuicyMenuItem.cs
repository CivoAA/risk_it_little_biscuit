using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Melder auf jedem Eintrag des <see cref="JuicyMainMenu"/>. Wird zur Laufzeit
/// angehängt und sagt nur weiter, ob die Maus drauf steht oder der Eintrag per
/// Tastatur/Controller angewählt ist. Nichts zum Einstellen.
/// </summary>
[AddComponentMenu("")]
public class JuicyMenuItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
                                            ISelectHandler, IDeselectHandler
{
    [HideInInspector] public JuicyMainMenu Owner;
    [HideInInspector] public int Index;

    public bool PointerInside { get; private set; }
    public bool Selected { get; private set; }

    private void OnDisable()
    {
        PointerInside = false;
        Selected = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        PointerInside = true;
        if (Owner != null) Owner.OnItemPointerEnter(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        PointerInside = false;
        if (Owner != null) Owner.OnItemPointerExit(this);
    }

    public void OnSelect(BaseEventData eventData) => Selected = true;

    public void OnDeselect(BaseEventData eventData) => Selected = false;
}
