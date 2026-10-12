using UnityEngine;

/// <summary>
/// Die Milchkrone der <see cref="ChocoChipsEvo"/> - nur Bild, kein Schaden.
/// Zwei Streifen mit gleich vielen Bildern: die hintere Haelfte (Pfuetze,
/// Innenseite, hintere Wand) liegt unter dem Keks, die vordere Wand ueber
/// ihm - so sieht es aus, als tauche er in die Milch ein. Laeuft einmal ab
/// und folgt dabei dem Spieler.
/// </summary>
public class MilkCrown : MonoBehaviour
{
    public const float Fps = 30f;

    private Transform follow;
    private Vector2 offset;
    private SpriteRenderer back;
    private SpriteRenderer front;
    private float age;

    public static void Create(Transform follow, Vector2 offset)
    {
        Sprite[] backFrames = BackFrames;
        Sprite[] frontFrames = FrontFrames;
        if (backFrames.Length == 0) return;

        GameObject go = new GameObject("MilkCrown");
        go.transform.position = (Vector2)follow.position + offset;
        ChocoChipsEvo.MoveToRunScene(go);

        MilkCrown crown = go.AddComponent<MilkCrown>();
        crown.follow = follow;
        crown.offset = offset;

        crown.back = go.AddComponent<SpriteRenderer>();
        SaladFan.CopySorting(crown.back, -1);

        if (frontFrames.Length == backFrames.Length)
        {
            GameObject frontGo = new GameObject("Front");
            frontGo.transform.SetParent(go.transform, false);
            crown.front = frontGo.AddComponent<SpriteRenderer>();
            SaladFan.CopySorting(crown.front, 1);
        }

        crown.Show(0);
    }

    void Update()
    {
        age += Time.deltaTime;
        int frame = (int)(age * Fps);
        if (frame >= BackFrames.Length)
        {
            Destroy(gameObject);
            return;
        }

        if (follow != null) transform.position = (Vector2)follow.position + offset;
        Show(frame);
    }

    private void Show(int frame)
    {
        back.sprite = BackFrames[frame];
        if (front != null) front.sprite = FrontFrames[frame];
    }

    // ------------------------------------------------------------------
    //  Bilder
    // ------------------------------------------------------------------

    private static Sprite[] backFrames;
    private static Sprite[] frontFrames;

    private static Sprite[] BackFrames
    {
        get
        {
            if (backFrames == null || backFrames.Length == 0 || backFrames[0] == null)
                backFrames = SpriteStrip.Load("Weapons/milk_crown_back");
            return backFrames;
        }
    }

    private static Sprite[] FrontFrames
    {
        get
        {
            if (frontFrames == null || frontFrames.Length == 0 || frontFrames[0] == null)
                frontFrames = SpriteStrip.Load("Weapons/milk_crown_front");
            return frontFrames;
        }
    }
}
