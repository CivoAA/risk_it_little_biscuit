using UnityEngine;
using TMPro;

public class SkillTooltip : MonoBehaviour
{
    public static SkillTooltip Instance;

    [Header("UI References")]
    public GameObject panel;
    public TMP_Text skillNameText;
    public TMP_Text priceText;
    public TMP_Text descriptionText;
    public TMP_Text prerequisiteText;

    [Header("Settings")]
    public Vector2 offset = new Vector2(40f, -40f);
    public bool followMouse = true;
    public float followSpeed = 40f;

    private RectTransform panelRect;
    private Canvas canvas;
    private Camera cam;
    private bool isVisible = false;

    private void Awake()
    {
        Instance = this;
        panelRect = panel.GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Hide();
    }

    private void Update()
    {
        // Wichtig: jedes Frame folgen, solange sichtbar
        if (isVisible && followMouse)
        {
            FollowMouse();
        }
    }

    public void Show(string skillName, int price, string description, System.Collections.Generic.List<string> prerequisites)
    {
        if (!panel.activeSelf)
            panel.SetActive(true);

        skillNameText.text = skillName;
        priceText.text = $"Price: {price}";
        descriptionText.text = description;

        // Falls keine Voraussetzungen vorhanden sind:
        if (prerequisites == null || prerequisites.Count == 0)
        {
            prerequisiteText.text = "Requires: None";
        }
        else
        {
            // Liste der Skillnamen zusammenfügen mit Kommas oder Zeilenumbrüchen
            prerequisiteText.text = "Requires: " + string.Join(", ", prerequisites);
            // alternativ für neue Zeilen:
            // prerequisiteText.text = "Requires:\n" + string.Join("\n", prerequisites);
        }

        UpdatePositionInstant();
        isVisible = true;
    }

    public void Hide()
    {
        if (panel.activeSelf)
            panel.SetActive(false);
        isVisible = false;
    }

    private void UpdatePositionInstant()
    {
        SetTooltipPosition(Input.mousePosition);
    }

    private void FollowMouse()
    {
        // Tooltip soll auch auf demselben Button mitwandern!
        Vector2 mousePos = Input.mousePosition;
        SetTooltipPosition(mousePos);
    }

    private void SetTooltipPosition(Vector2 screenPos)
    {
        if (canvas == null || panelRect == null) return;

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            screenPos,
            cam,
            out localPoint
        );

        // Tooltip folgt Maus, mit Offset
        Vector2 targetPos = localPoint + offset;

        // Optional smooth follow
        if (followSpeed <= 0)
            panelRect.anchoredPosition = targetPos;
        else
            panelRect.anchoredPosition = Vector2.Lerp(
                panelRect.anchoredPosition,
                targetPos,
                Time.deltaTime * followSpeed
            );
    }
}
