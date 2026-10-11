using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Bewegt einen Stern wie das alte DVD-Logo als Bildschirmschoner: schraeg und
/// immer gleich schnell ueber den Bildschirm, an den Bildraendern abprallen,
/// bis die Lebenszeit um ist.
///
/// Der Stern lebt im Bild, nicht in der Welt: gespeichert wird nur sein Abstand
/// zur Bildmitte (<see cref="offset"/>), und nur der bewegt sich. Die
/// Welt-Position wird jeden Frame daraus neu gesetzt (Kamera + Abstand). Der
/// Spieler kann laufen wie er will - auf dem Bildschirm fliegt der Stern immer
/// gleich schnell. Ein Weltobjekt bleibt er trotzdem, damit sein Trigger
/// Gegner trifft.
///
/// Gesetzt wird kurz vor dem Rendern der Kamera, also erst nachdem
/// Cinemachine (LateUpdate) die Kamera fuer diesen Frame bewegt hat - sonst
/// wuerde der Stern einen Frame hinterherzittern.
/// </summary>
public class BouncingStar : MonoBehaviour
{
    private Camera cam;
    private Vector2 offset;     // Abstand zur Bildmitte in Welteinheiten
    private Vector2 velocity;   // Tempo im Bild, Welteinheiten pro Sekunde
    private Vector2 halfSize;   // halbe Sterngroesse - abgeprallt wird an der Kante, nicht an der Mitte
    private float lifeLeft;
    private int lastFrame = -1;

    /// <summary>
    /// Startet den Stern an seiner aktuellen Bildposition (beim Spieler) in eine
    /// zufaellige Schraegrichtung. Ohne Kamera bleibt er einfach liegen und
    /// verschwindet nach Ablauf der Zeit.
    /// </summary>
    public static void Launch(GameObject star, float speed, float lifetime)
    {
        Camera camera = Camera.main;
        if (camera == null || !camera.orthographic)
        {
            Destroy(star, lifetime);
            return;
        }

        // Der Stern wird jeden Frame versetzt - ohne Rigidbody waere sein
        // Trigger ein statischer Collider, und den zu bewegen ist teuer.
        if (!star.TryGetComponent(out Rigidbody2D body))
        {
            body = star.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
        }

        star.AddComponent<BouncingStar>().Init(camera, RandomDiagonal() * speed, lifetime);
    }

    // Wie beim DVD-Logo nur schraeg: beide Achsen mindestens 0.6, sonst
    // laeuft der Stern fast waagerecht/senkrecht und trifft kaum etwas.
    private static Vector2 RandomDiagonal()
    {
        float x = Random.Range(0.6f, 0.8f);
        float y = Mathf.Sqrt(1f - x * x);
        if (Random.value < 0.5f) x = -x;
        if (Random.value < 0.5f) y = -y;
        return Random.value < 0.5f ? new Vector2(x, y) : new Vector2(y, x);
    }

    private void Init(Camera camera, Vector2 startVelocity, float lifetime)
    {
        cam = camera;
        velocity = startVelocity;
        lifeLeft = lifetime;

        Renderer r = GetComponentInChildren<Renderer>();
        halfSize = r != null ? (Vector2)r.bounds.extents : Vector2.zero;

        offset = transform.position - cam.transform.position;
        Bounce();
        Place();
    }

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != cam || lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;

        lifeLeft -= Time.deltaTime;
        if (lifeLeft <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        offset += velocity * Time.deltaTime;
        Bounce();
        Place();
    }

    // Prallt an den Bildraendern ab: was ueber den Rand hinausgeht, wird
    // zurueckgespiegelt, und die Richtung zeigt danach nach innen.
    private void Bounce()
    {
        float maxX = Mathf.Max(0f, cam.orthographicSize * cam.aspect - halfSize.x);
        float maxY = Mathf.Max(0f, cam.orthographicSize - halfSize.y);

        if (offset.x > maxX)       { offset.x = Mathf.Max(-maxX,  2f * maxX - offset.x); velocity.x = -Mathf.Abs(velocity.x); }
        else if (offset.x < -maxX) { offset.x = Mathf.Min( maxX, -2f * maxX - offset.x); velocity.x =  Mathf.Abs(velocity.x); }

        if (offset.y > maxY)       { offset.y = Mathf.Max(-maxY,  2f * maxY - offset.y); velocity.y = -Mathf.Abs(velocity.y); }
        else if (offset.y < -maxY) { offset.y = Mathf.Min( maxY, -2f * maxY - offset.y); velocity.y =  Mathf.Abs(velocity.y); }
    }

    // Bildposition -> Welt: an die Kamera haengen, Z vom Stern behalten.
    private void Place()
    {
        Vector3 c = cam.transform.position;
        transform.position = new Vector3(c.x + offset.x, c.y + offset.y, transform.position.z);
    }
}
