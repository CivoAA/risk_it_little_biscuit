using UnityEngine;

/// <summary>
/// Raeumt einen Effekt ab, sobald seine Animation einmal durch ist. Kommt der
/// Effekt aus dem <see cref="RunPool"/>, geht er dorthin zurueck und spielt
/// beim naechsten Aktivieren von vorn.
/// </summary>
public class AnimatedObjectDestroy : MonoBehaviour
{
    [SerializeField] private Animator animator;

    private float remaining;
    private bool started;
    private int startState;
    private bool reused;

    void Awake()
    {
        // Ohne das baut der Animator bei jedem Aktivieren alles neu auf - bei
        // einem grossen Haufen, der auf einmal faellt, war das der teuerste
        // Teil des Todes. So wird nur die Animation auf Anfang gesetzt.
        if (animator != null) animator.keepAnimatorStateOnDisable = true;
    }

    void OnEnable()
    {
        started = false;
        if (reused && animator != null) animator.Play(startState, 0, 0f);
    }

    void OnDisable()
    {
        reused = true;
    }

    void Update()
    {
        if (!started)
        {
            started = true;
            if (animator != null)
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                if (startState == 0) startState = state.fullPathHash;
                remaining = state.length;
            }
            else remaining = 0f;
        }

        remaining -= Time.deltaTime;
        if (remaining <= 0f) RunPool.Release(gameObject);
    }
}
