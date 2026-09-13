using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Hover-/Klick-Feedback für die Menü-Buttons: Platte wechselt die Farbe und
/// hebt sich beim Draufzeigen um ein paar Pixel, beim Drücken geht sie runter.
/// Bewusst statt Unity-Farbtint, weil der die Farben multipliziert und im
/// Editor deshalb nicht das zeigt, was im Spiel rauskommt.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Button))]
public class MenuButtonVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
                                               IPointerDownHandler, IPointerUpHandler,
                                               ISelectHandler, IDeselectHandler
{
    [Header("Teile")]
    [SerializeField] private Image plate;
    [SerializeField] private RectTransform plateRect;
    [SerializeField] private TMP_Text label;
    [SerializeField, Tooltip("Optionales Icon neben dem Text - wird wie der Text eingefärbt.")]
    private Graphic icon;

    [Header("Plattenfarben")]
    [SerializeField] private Color normalColor = new Color(0.278f, 0.133f, 0.090f);
    [SerializeField] private Color hoverColor = new Color(0.360f, 0.180f, 0.121f);
    [SerializeField] private Color pressedColor = new Color(0.203f, 0.082f, 0.050f);
    [SerializeField] private Color disabledColor = new Color(0.180f, 0.113f, 0.090f);

    [Header("Textfarben")]
    [SerializeField] private Color labelNormal = new Color(1f, 0.972f, 0.803f);
    [SerializeField] private Color labelHover = new Color(0.980f, 0.584f, 0.129f);
    [SerializeField] private Color labelDisabled = new Color(1f, 0.972f, 0.803f, 0.35f);

    [Header("Bewegung")]
    [SerializeField] private float hoverLift = 4f;
    [SerializeField] private float pressOffset = -4f;

    private Button button;
    private bool isHovered;
    private bool isPressed;
    private bool wasInteractable = true;
    private Vector2 restPosition;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (plateRect != null) restPosition = plateRect.anchoredPosition;
    }

    private void OnEnable()
    {
        if (button == null) button = GetComponent<Button>();
        isHovered = false;
        isPressed = false;
        Apply();
    }

    private void Update()
    {
        if (button == null) return;

        if (button.interactable != wasInteractable)
        {
            wasInteractable = button.interactable;
            if (!wasInteractable)
            {
                isHovered = false;
                isPressed = false;
            }
            Apply();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        Apply();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        isPressed = false;
        Apply();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        Apply();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        Apply();
    }

    public void OnSelect(BaseEventData eventData)
    {
        isHovered = true;
        Apply();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isHovered = false;
        Apply();
    }

    private void Apply()
    {
        bool interactable = button == null || button.interactable;

        Color plateColor;
        Color textColor;
        float offset;

        if (!interactable)
        {
            plateColor = disabledColor;
            textColor = labelDisabled;
            offset = 0f;
        }
        else if (isPressed)
        {
            plateColor = pressedColor;
            textColor = labelHover;
            offset = pressOffset;
        }
        else if (isHovered)
        {
            plateColor = hoverColor;
            textColor = labelHover;
            offset = hoverLift;
        }
        else
        {
            plateColor = normalColor;
            textColor = labelNormal;
            offset = 0f;
        }

        if (plate != null) plate.color = plateColor;
        if (label != null) label.color = textColor;
        if (icon != null) icon.color = textColor;
        if (plateRect != null) plateRect.anchoredPosition = restPosition + new Vector2(0f, offset);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (button == null) button = GetComponent<Button>();
        if (plateRect != null && !Application.isPlaying) restPosition = plateRect.anchoredPosition;
        Apply();
    }
#endif
}
