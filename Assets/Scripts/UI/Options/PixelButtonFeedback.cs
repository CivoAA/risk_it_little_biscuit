using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Hover-/Klick-Feedback für die per Code gebauten Pixel-Buttons. Gleiche Idee wie
/// MenuButtonVisual im Hauptmenü: Farben werden gesetzt statt getintet, weil Unitys
/// Farbtint multipliziert und damit nie das zeigt, was rauskommen soll.
/// </summary>
[RequireComponent(typeof(Button))]
public class PixelButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
                                                  IPointerDownHandler, IPointerUpHandler
{
    private Image fill;
    private TMP_Text label;
    private Button button;
    private bool hovered;
    private bool pressed;

    public void Setup(Image fillImage, TMP_Text labelText)
    {
        fill = fillImage;
        label = labelText;
        button = GetComponent<Button>();
        Apply();
    }

    private void OnEnable()
    {
        hovered = false;
        pressed = false;
        Apply();
    }

    public void OnPointerEnter(PointerEventData e) { hovered = true;  Apply(); }
    public void OnPointerExit(PointerEventData e)  { hovered = false; pressed = false; Apply(); }
    public void OnPointerDown(PointerEventData e)  { pressed = true;  Apply(); }
    public void OnPointerUp(PointerEventData e)    { pressed = false; Apply(); }

    private void Apply()
    {
        if (button == null) button = GetComponent<Button>();
        bool on = button == null || button.interactable;

        Color fillColor;
        Color textColor;

        if (!on)             { fillColor = PixelUI.RowFill;    textColor = PixelUI.TextDim; }
        else if (pressed)    { fillColor = PixelUI.RowPressed; textColor = PixelUI.TextAccent; }
        else if (hovered)    { fillColor = PixelUI.RowHover;   textColor = PixelUI.TextAccent; }
        else                 { fillColor = PixelUI.RowFill;    textColor = PixelUI.TextNormal; }

        if (fill != null)  fill.color = fillColor;
        if (label != null) label.color = textColor;
    }
}
