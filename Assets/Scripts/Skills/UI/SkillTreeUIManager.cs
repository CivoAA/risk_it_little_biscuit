using System.Collections;
using UnityEngine;

public class SkillTreeUIManager : MonoBehaviour
{
    [Header("Wird gezoomt")]
    public RectTransform zoomRoot;     // Parent von Skill Tree Panel & Detail Panel

    [Header("Focus-Anker (bewegen sich NICHT mit)")]
    public RectTransform baseAnchor;
    public RectTransform windAnchor;
    public RectTransform heartAnchor;
    public RectTransform swordAnchor;
    public RectTransform clockAnchor;

    [Header("Panels")]
    public GameObject skillTreePanel;
    public GameObject detailSkillTreePanel;
    public CanvasGroup skillTreeCanvasGroup;

    [Header("Zoom Einstellungen")]
    public float zoomScale = 1.8f;
    public float zoomTime  = 0.4f;
    [Range(0f, 1f)]
    public float hideSkillTreeAt = 0.8f;

    [Header("TimeScale-safe UI")]
    public bool useUnscaledTime = true;

    Vector2 overviewPos;
    Vector3 overviewScale;
    Coroutine currentRoutine;

    // 🔒 Zoom-Lock gegen Mehrfachklicks
    bool isZooming = false;

    void Awake()
    {
        overviewPos   = zoomRoot.anchoredPosition;
        overviewScale = zoomRoot.localScale;

        if (skillTreePanel != null)
            skillTreePanel.SetActive(true);

        if (detailSkillTreePanel != null)
            detailSkillTreePanel.SetActive(true);

        if (skillTreeCanvasGroup != null)
        {
            skillTreeCanvasGroup.alpha = 1f;
            skillTreeCanvasGroup.interactable = true;
            skillTreeCanvasGroup.blocksRaycasts = true;
        }
    }

    // ---------- Öffentliche Buttons ----------

    public void ZoomToBase()  => ZoomTo(baseAnchor);
    public void ZoomToWind()  => ZoomTo(windAnchor);
    public void ZoomToHeart() => ZoomTo(heartAnchor);
    public void ZoomToSword() => ZoomTo(swordAnchor);
    public void ZoomToClock() => ZoomTo(clockAnchor);

    public void ZoomBack()
    {
        if (isZooming) return;

        // 🔒 Bereits zurückgezoomt? → nicht nochmal ausführen
        if (Vector2.Distance(zoomRoot.anchoredPosition, overviewPos) < 0.1f &&
            Vector3.Distance(zoomRoot.localScale, overviewScale) < 0.01f)
        {
            return;
        }

        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        currentRoutine = StartCoroutine(
            ZoomRoutine(overviewPos, overviewScale, true)
        );
    }


    // ---------- ZOOM LOGIK ----------

    void ZoomTo(RectTransform anchor)
    {
        if (anchor == null) return;

        // 🚫 Während Zoom keine neue Aktion erlauben
        if (isZooming) return;

        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        // Anchor-Position relativ zu zoomRoot
        Vector2 anchorLocal =
            (Vector2)zoomRoot.InverseTransformPoint(anchor.position);

        Vector3 targetScale = Vector3.one * zoomScale;
        Vector2 targetPos   = -anchorLocal * zoomScale;

        currentRoutine = StartCoroutine(
            ZoomRoutine(targetPos, targetScale, false)
        );
    }

    IEnumerator ZoomRoutine(Vector2 targetPos, Vector3 targetScale, bool zoomBack)
    {
        isZooming = true; // 🔒 LOCK

        Vector2 startPos   = zoomRoot.anchoredPosition;
        Vector3 startScale = zoomRoot.localScale;

        if (zoomBack && skillTreePanel != null && skillTreeCanvasGroup != null)
        {
            skillTreePanel.SetActive(true);
            skillTreeCanvasGroup.alpha = 0f;
            skillTreeCanvasGroup.interactable = false;
            skillTreeCanvasGroup.blocksRaycasts = false;
        }

        bool fadeDone = false;
        float t = 0f;

        while (t < 1f)
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t += (zoomTime > 0f) ? (dt / zoomTime) : 1f;

            float k = Mathf.SmoothStep(0f, 1f, t);

            zoomRoot.anchoredPosition =
                Vector2.Lerp(startPos, targetPos, k);
            zoomRoot.localScale =
                Vector3.Lerp(startScale, targetScale, k);

            if (skillTreeCanvasGroup != null)
            {
                if (!zoomBack)
                {
                    if (k >= hideSkillTreeAt)
                    {
                        float fadeT =
                            Mathf.InverseLerp(hideSkillTreeAt, 1f, k);

                        skillTreeCanvasGroup.alpha =
                            Mathf.Lerp(1f, 0f, fadeT);

                        if (!fadeDone && fadeT >= 1f)
                        {
                            skillTreeCanvasGroup.interactable = false;
                            skillTreeCanvasGroup.blocksRaycasts = false;
                            fadeDone = true;
                        }
                    }
                }
                else
                {
                    if (k >= hideSkillTreeAt)
                    {
                        float fadeT =
                            Mathf.InverseLerp(hideSkillTreeAt, 1f, k);

                        skillTreeCanvasGroup.alpha =
                            Mathf.Lerp(0f, 1f, fadeT);

                        if (!fadeDone && fadeT >= 1f)
                        {
                            skillTreeCanvasGroup.interactable = true;
                            skillTreeCanvasGroup.blocksRaycasts = true;
                            fadeDone = true;
                        }
                    }
                }
            }

            yield return null;
        }

        zoomRoot.anchoredPosition = targetPos;
        zoomRoot.localScale       = targetScale;

        if (skillTreeCanvasGroup != null && !fadeDone)
        {
            skillTreeCanvasGroup.alpha = zoomBack ? 1f : 0f;
            skillTreeCanvasGroup.interactable = zoomBack;
            skillTreeCanvasGroup.blocksRaycasts = zoomBack;
        }

        currentRoutine = null;
        isZooming = false; // 🔓 UNLOCK
    }
}