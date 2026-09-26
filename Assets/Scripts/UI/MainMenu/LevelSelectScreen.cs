using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Platzhalter für die Levelauswahl. Baut sich - wie das Options-Panel - per Code
/// auf demselben 320x180-Raster auf, damit hier noch keine Szenen-Objekte gepflegt
/// werden müssen, solange der Inhalt nicht feststeht.
///
/// Sobald klar ist, wie die Levelauswahl aussehen soll, kommt das hier raus und
/// wird durch die echten Level-Kacheln ersetzt.
/// </summary>
public class LevelSelectScreen : MonoBehaviour
{
    private const int RefWidth = 320;
    private const int RefHeight = 180;

    [SerializeField, Tooltip("Szene, in die der Zurück-Knopf führt.")]
    private string menuSceneName = "Main Menu";

    private void Start()
    {
        TMP_FontAsset font = PixelUI.FindPixelFont();

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        gameObject.AddComponent<GraphicRaycaster>();
        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        Image bg = PixelUI.Panel("Background", transform, Vector2.zero, Vector2.zero, PixelUI.PanelFill);
        RectTransform br = bg.rectTransform;
        br.anchorMin = Vector2.zero;
        br.anchorMax = Vector2.one;
        br.sizeDelta = Vector2.zero;

        PixelUI.Label("Title", transform, new Vector2(240f, 14f), new Vector2(0f, 40f),
                      "LEVELAUSWAHL", 14f, PixelUI.TextAccent, TextAlignmentOptions.Center, font);

        PixelUI.Label("Hint", transform, new Vector2(240f, 10f), new Vector2(0f, 20f),
                      "hier kommen die level rein", 8f, PixelUI.TextDim,
                      TextAlignmentOptions.Center, font);

        Button back = PixelUI.TextButton("Back", transform, new Vector2(90f, 16f), new Vector2(0f, -40f),
                                         "ZURUECK", 8f, font);
        back.onClick.AddListener(BackToMenu);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) BackToMenu();
    }

    public void BackToMenu()
    {
        if (AudioController.Instance != null && AudioController.Instance.MenuClick != null)
            AudioController.Instance.MenuClick.Play();

        if (!Application.CanStreamedLevelBeLoaded(menuSceneName))
        {
            Debug.LogError($"[Levelauswahl] Szene \"{menuSceneName}\" ist nicht in den Build Settings.");
            return;
        }

        SceneFader.Load(menuSceneName);
    }
}
