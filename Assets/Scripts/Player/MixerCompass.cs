using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Skilltree "Kartograf" (<see cref="SkillGrants.Kartograf"/>): ein Pfeil am
/// Bildrand zeigt zum naechsten Mixer, der noch nicht benutzt wurde.
///
/// Damit der Pfeil nicht hin- und herspringt:
///   - Das Ziel wird nur einmal pro Sekunde neu gesucht.
///   - Ein neuer Mixer loest das alte Ziel nur ab, wenn er deutlich naeher ist
///     (<see cref="SwitchMargin"/>). Sind zwei etwa gleich weit, bleibt es beim alten.
///   - Die Richtung rastet in 45°-Schritten ein - das haelt auch das Pixelbild sauber.
///
/// Liegt das Ziel im Bild, verschwindet der Pfeil. Legt sich selbst an (siehe
/// <see cref="Ensure"/>) und lebt in der Lauf-Szene, verschwindet also mit ihr.
/// </summary>
public class MixerCompass : MonoBehaviour
{
    private const float RetargetInterval = 1f;
    private const float SwitchMargin = 0.8f;   // neues Ziel muss < 80 % der alten Entfernung haben
    private const float EdgeMargin = 14f;      // Abstand zum Bildrand in Referenzpixeln
    private const int RefW = 480, RefH = 270;  // wie das restliche HUD
    private const int ArrowScale = 2;

    private static MixerCompass instance;

    private RectTransform canvasRect;
    private RectTransform arrow;
    private Image arrowImage;
    private MixerObject target;
    private float retargetTimer;

    /// <summary>Legt den Pfeil an, falls noch keiner da ist.</summary>
    public static void Ensure()
    {
        if (instance != null) return;

        GameObject go = new GameObject("~MixerCompass");
        Scene run = RunScene.Current;
        if (run.IsValid() && run.isLoaded) SceneManager.MoveGameObjectToScene(go, run);

        instance = go.AddComponent<MixerCompass>();
    }

    private void Awake()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;   // unter Menues und Level-Up

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefW, RefH);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        canvasRect = (RectTransform)transform;

        GameObject a = new GameObject("Arrow", typeof(RectTransform));
        a.transform.SetParent(transform, false);
        arrow = (RectTransform)a.transform;
        arrow.anchorMin = arrow.anchorMax = arrow.pivot = new Vector2(0.5f, 0.5f);

        arrowImage = a.AddComponent<Image>();
        arrowImage.sprite = GameHudSkin.EvoArrow;
        arrowImage.raycastTarget = false;
        Rect r = arrowImage.sprite.rect;
        arrow.sizeDelta = new Vector2(r.width, r.height) * ArrowScale;
        arrowImage.enabled = false;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Update()
    {
        PlayerController player = PlayerController.Instance;
        if (player == null)
        {
            arrowImage.enabled = false;
            return;
        }

        // Benutzt oder weg? Dann sofort neu suchen, nicht erst nach der Sekunde.
        bool lost = target == null || !target.isActiveAndEnabled || !Contains(target);

        retargetTimer -= Time.unscaledDeltaTime;
        if (lost || retargetTimer <= 0f)
        {
            retargetTimer = RetargetInterval;
            PickTarget(player.transform.position, lost);
        }

        UpdateArrow();
    }

    private static bool Contains(MixerObject mixer)
    {
        var list = MixerObject.Unused;
        for (int i = 0; i < list.Count; i++)
            if (list[i] == mixer) return true;
        return false;
    }

    private void PickTarget(Vector3 from, bool lost)
    {
        MixerObject best = null;
        float bestDist = float.MaxValue;

        var list = MixerObject.Unused;
        for (int i = 0; i < list.Count; i++)
        {
            MixerObject m = list[i];
            if (m == null) continue;

            float d = (m.transform.position - from).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = m;
            }
        }

        if (lost || best == null)
        {
            target = best;
            return;
        }

        // Beim alten Ziel bleiben, solange das neue nicht deutlich naeher ist.
        float current = (target.transform.position - from).sqrMagnitude;
        if (best != target && bestDist < current * SwitchMargin * SwitchMargin) target = best;
    }

    private void UpdateArrow()
    {
        Camera cam = Camera.main;
        if (target == null || cam == null)
        {
            arrowImage.enabled = false;
            return;
        }

        Vector3 vp = cam.WorldToViewportPoint(target.transform.position);
        bool onScreen = vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
        if (onScreen)
        {
            arrowImage.enabled = false;
            return;
        }

        // Hinter der Kamera (z < 0) spiegelt der Viewport - dann umdrehen.
        Vector2 size = canvasRect.rect.size;
        Vector2 dir = new Vector2((vp.x - 0.5f) * size.x, (vp.y - 0.5f) * size.y);
        if (vp.z < 0f) dir = -dir;
        if (dir.sqrMagnitude < 0.0001f)
        {
            arrowImage.enabled = false;
            return;
        }

        // Frei am (eingerueckten) Bildrand entlang ...
        Vector2 half = size * 0.5f - new Vector2(EdgeMargin, EdgeMargin);
        float scale = Mathf.Min(
            Mathf.Abs(dir.x) > 0.001f ? half.x / Mathf.Abs(dir.x) : float.MaxValue,
            Mathf.Abs(dir.y) > 0.001f ? half.y / Mathf.Abs(dir.y) : float.MaxValue);
        arrow.anchoredPosition = new Vector2(Mathf.Round(dir.x * scale), Mathf.Round(dir.y * scale));

        // ... die Spitze aber in 45°-Schritten.
        float angle = Mathf.Round(Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg / 45f) * 45f;
        // Das Sprite zeigt nach oben (90°).
        arrow.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
        arrowImage.enabled = true;
    }
}
