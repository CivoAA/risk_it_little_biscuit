using UnityEngine;

/// <summary>
/// Huepfen statt Gleiten: der Gegner kommt nur vom Fleck, solange seine
/// Animation ihn in der Luft zeigt. Am Boden (Zusammenducken, Landen) steht
/// er still.
///
/// Gelesen wird direkt aus dem Animator - welches Bild der Laufanimation
/// gerade dran ist. So laufen Bewegung und Bild nie auseinander, auch nicht,
/// wenn die Werkstatt das Tempo der Animation aendert.
///
/// <see cref="Enemy"/> fragt <see cref="SpeedFactor"/> jeden Physikschritt ab.
/// Ohne diese Komponente laeuft ein Gegner wie bisher.
/// </summary>
[RequireComponent(typeof(Animator))]
public class HopMovement : MonoBehaviour
{
    [Tooltip("Erstes Bild der Laufanimation, in dem der Gegner in der Luft ist (0 = erstes Bild im Sheet).")]
    [SerializeField] private int firstAirFrame = 4;

    [Tooltip("Letztes Bild, in dem er in der Luft ist.")]
    [SerializeField] private int lastAirFrame = 6;

    [Tooltip("An: im Schnitt so schnell wie das Tempo im Katalog - die Spruenge sind dafuer entsprechend weit. " +
             "Aus: in der Luft genau Katalog-Tempo, im Schnitt also deutlich langsamer.")]
    [SerializeField] private bool keepAverageSpeed = true;

    [Tooltip("Bilder je Sprung, wenn die Animation mehrere Spruenge hintereinander zeigt (z.B. normal + Blinzeln). " +
             "Die Luftbilder gelten dann in jedem Sprung. 0 = die ganze Animation ist ein Sprung.")]
    [SerializeField] private int cycleFrames = 0;

    private Animator animator;

#if UNITY_EDITOR
    /// <summary>Nur fuer die Werkstatt: Luftbilder aus dem Katalog eintragen.</summary>
    public void EditorSetAirFrames(int first, int last)
    {
        firstAirFrame = first;
        lastAirFrame = last;
        keepAverageSpeed = true;
    }
#endif

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    /// <summary>
    /// Faktor auf das Lauftempo: 0 am Boden, in der Luft 1 bzw. so viel mehr,
    /// dass der Schnitt wieder dem Katalog-Tempo entspricht.
    /// </summary>
    public float SpeedFactor
    {
        get
        {
            if (animator == null || !animator.isActiveAndEnabled) return 1f;

            AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0);
            if (clips.Length == 0 || clips[0].clip == null) return 1f;

            AnimationClip clip = clips[0].clip;
            int frameCount = Mathf.Max(1, Mathf.RoundToInt(clip.length * clip.frameRate));

            float t = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            int frame = Mathf.Clamp(Mathf.FloorToInt((t - Mathf.Floor(t)) * frameCount), 0, frameCount - 1);

            if (cycleFrames > 0 && cycleFrames < frameCount)
            {
                frame %= cycleFrames;
                frameCount = cycleFrames;
            }

            if (frame < firstAirFrame || frame > lastAirFrame) return 0f;
            if (!keepAverageSpeed) return 1f;

            int airFrames = Mathf.Max(1, lastAirFrame - firstAirFrame + 1);
            return (float)frameCount / airFrames;
        }
    }
}
