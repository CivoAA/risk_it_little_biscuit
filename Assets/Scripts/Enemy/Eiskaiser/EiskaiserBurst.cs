using UnityEngine;

/// <summary>Spielt einen Bildstreifen einmal ab und loescht sich dann (Lawinenkugel platzt).</summary>
public class EiskaiserBurst : MonoBehaviour
{
    private SpriteRenderer sr;
    private Sprite[] strip;
    private float fps, t;

    public void Play(SpriteRenderer renderer, Sprite[] frames, float framesPerSecond)
    {
        sr = renderer;
        strip = frames;
        fps = framesPerSecond;
        sr.sprite = strip[0];
    }

    private void Update()
    {
        if (strip == null) return;
        t += Time.deltaTime;
        int i = Mathf.FloorToInt(t * fps);
        if (i >= strip.Length)
        {
            Destroy(gameObject);
            return;
        }
        sr.sprite = strip[i];
    }
}
