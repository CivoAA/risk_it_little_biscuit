using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Einmal abspielen und weg - fuer den Flammenring des <see cref="FoxFire"/>.
/// Immer in Originalgroesse: die Lauf-Kamera ist pixelgenau, skalierte
/// Pixelart wird dort ungleichmaessig und unscharf.
/// </summary>
public class FoxFx : MonoBehaviour
{
    private Sprite[] frames;
    private float fps;
    private float age;
    private SpriteRenderer sr;

    public static void Play(Sprite[] frames, Vector2 at, float fps, int sortingOffset)
    {
        if (frames == null || frames.Length == 0) return;

        GameObject go = new GameObject("FoxFx");
        const float ppu = 32f;
        go.transform.position = new Vector3(Mathf.Round(at.x * ppu) / ppu, Mathf.Round(at.y * ppu) / ppu, 0f);

        Scene gameScene = RunScene.Current;
        if (gameScene.IsValid() && gameScene.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(go, gameScene);
        }

        FoxFx fx = go.AddComponent<FoxFx>();
        fx.frames = frames;
        fx.fps = fps;
        fx.sr = go.AddComponent<SpriteRenderer>();
        SaladFan.CopySorting(fx.sr, sortingOffset);
        fx.sr.sprite = frames[0];
    }

    void Update()
    {
        age += Time.deltaTime;
        int frame = (int)(age * fps);
        if (frame >= frames.Length)
        {
            Destroy(gameObject);
            return;
        }
        sr.sprite = frames[frame];
    }
}
