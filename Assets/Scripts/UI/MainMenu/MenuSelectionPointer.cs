using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Schiebt ein Auswahl-Bild links neben den Button, auf dem gerade die Maus steht
/// oder der per Tastatur/Controller angewählt ist.
///
/// Die Komponente gehört auf das Canvas. Ins Feld "pointer" kommt das Bild-Objekt,
/// in "buttonRoot" das Objekt, unter dem die Buttons liegen (leer = dieses Objekt).
/// Die Buttons selbst müssen nichts wissen - beim Start hängt sich diese Komponente
/// an jeden gefundenen Button einen kleinen Melder (MenuPointerRelay).
/// </summary>
[DisallowMultipleComponent]
public class MenuSelectionPointer : MonoBehaviour
{
    [SerializeField, Tooltip("Das Auswahl-Bild, das neben den Button geschoben wird.")]
    private RectTransform pointer;

    [SerializeField, Tooltip("Unter welchem Objekt nach Buttons gesucht wird. Leer = dieses Objekt.")]
    private RectTransform buttonRoot;

    [SerializeField, Tooltip("Abstand zwischen Bild und Button, in Canvas-Einheiten.")]
    private float gap = 3f;

    [SerializeField, Tooltip("Versteckt das Bild, solange noch nichts angewählt wurde.")]
    private bool hideUntilFirstSelection = true;

    [SerializeField, Tooltip("Bild wieder verstecken, sobald die Maus den Button verlässt. " +
                             "Aus = das Bild bleibt beim zuletzt gewählten Button stehen.")]
    private bool hideOnExit = false;

    private readonly List<Button> buttons = new List<Button>();
    private RectTransform current;

    private void Start()
    {
        if (buttonRoot == null) buttonRoot = transform as RectTransform;

        if (pointer == null)
        {
            Debug.LogWarning("[MenuSelectionPointer] Kein Auswahl-Bild zugewiesen.", this);
            enabled = false;
            return;
        }

        // Das Bild hängt an seiner rechten Kante, damit es links neben dem Button sitzt.
        pointer.pivot = new Vector2(1f, 0.5f);

        buttons.Clear();
        buttonRoot.GetComponentsInChildren(true, buttons);
        foreach (Button b in buttons)
        {
            MenuPointerRelay relay = b.gameObject.GetComponent<MenuPointerRelay>();
            if (relay == null) relay = b.gameObject.AddComponent<MenuPointerRelay>();
            relay.Owner = this;
        }

        if (hideUntilFirstSelection) pointer.gameObject.SetActive(false);
    }

    /// <summary>Setzt das Bild links neben den übergebenen Button.</summary>
    public void PointAt(RectTransform target)
    {
        if (pointer == null || target == null) return;

        current = target;
        if (!pointer.gameObject.activeSelf) pointer.gameObject.SetActive(true);

        // Über die Weltecken statt über anchoredPosition: so stimmt die Position auch
        // dann, wenn Buttons und Bild unterschiedliche Anker oder Eltern haben.
        Vector3[] corners = new Vector3[4];
        target.GetWorldCorners(corners); // 0 = unten links, 1 = oben links
        float leftX = corners[0].x;
        float midY = (corners[0].y + corners[1].y) * 0.5f;

        pointer.position = new Vector3(leftX, midY, pointer.position.z);

        // Der Abstand wird lokal abgezogen, damit er in Canvas-Einheiten zählt
        // und nicht mit der Bildschirmauflösung mitwächst.
        pointer.anchoredPosition -= new Vector2(gap, 0f);
    }

    /// <summary>Blendet das Bild aus, falls es noch an diesem Button hängt.</summary>
    public void ClearIf(RectTransform target)
    {
        if (!hideOnExit || pointer == null) return;
        if (current != target) return;

        current = null;
        pointer.gameObject.SetActive(false);
    }
}
