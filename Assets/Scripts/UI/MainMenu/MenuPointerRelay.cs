using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Kleiner Melder, der beim Start automatisch auf jeden Menü-Button gesetzt wird.
/// Sagt dem MenuSelectionPointer Bescheid, sobald der Button unter der Maus liegt
/// oder per Tastatur/Controller angewählt ist. Nichts zum Einstellen.
/// </summary>
[AddComponentMenu("")]
public class MenuPointerRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
                                               ISelectHandler, IDeselectHandler
{
    [HideInInspector] public MenuSelectionPointer Owner;

    private RectTransform Rect => transform as RectTransform;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Owner != null) Owner.PointAt(Rect);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (Owner != null) Owner.ClearIf(Rect);
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (Owner != null) Owner.PointAt(Rect);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (Owner != null) Owner.ClearIf(Rect);
    }
}
