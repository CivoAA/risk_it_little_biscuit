using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Skilltree "Wirbelsturm" (<see cref="SkillGrants.Wirbelsturm"/>): faellt das
/// Leben unter 40 %, wirbelt Toast 5 Sekunden lang mit dem Faecher im Kreis.
/// Jeder Gegner im <see cref="Radius"/> wird dabei ununterbrochen nach aussen
/// gedrueckt - niemand kommt an ihn heran. Macht keinen Schaden. Bosse und
/// Minibosse bleiben stehen (<see cref="Enemy.ApplyPull"/> ignoriert sie),
/// festgeklebte Gegner (Klebreis) auch.
///
/// Ausgeloest wird er von <see cref="PlayerController.TakeDamage"/>, der auch
/// die Abklingzeit haelt. Hier stehen die Werte, der Druck und das Bild: drei
/// Salatsicheln, die um den Spieler kreisen.
/// </summary>
public class Whirlwind : MonoBehaviour
{
    /// <summary>Ab diesem Lebensanteil springt der Wirbelsturm an.</summary>
    public const float Threshold = 0.4f;

    /// <summary>So lange wirbelt er (Sekunden).</summary>
    public const float Duration = 5f;

    /// <summary>Abklingzeit, gerechnet ab dem ENDE der Wirkung.</summary>
    public const float Cooldown = 35f;

    /// <summary>Reichweite des Drucks (Welteinheiten).</summary>
    public const float Radius = 3f;

    /// <summary>So schnell werden die Gegner nach aussen gedrueckt.</summary>
    public const float PushSpeed = 9f;

    private const int Blades = 3;
    private const float BladeOrbit = 1.6f;      // Abstand der Sicheln zum Spieler
    private const float BladeSize = 1.2f;       // Groesse einer Sichel in Tiles
    private const float SpinSpeed = 540f;       // Grad pro Sekunde
    private const float FadeTime = 0.3f;

    private Transform owner;
    private float age;
    private SpriteRenderer[] blades;

    /// <summary>Startet den Wirbelsturm um <paramref name="player"/>.</summary>
    public static void Fire(Transform player)
    {
        GameObject go = new GameObject("Whirlwind");
        go.transform.position = player.position;

        Scene run = RunScene.Current;
        if (run.IsValid() && run.isLoaded) SceneManager.MoveGameObjectToScene(go, run);

        go.AddComponent<Whirlwind>().owner = player;
    }

    private void Start()
    {
        Sprite sprite = SaladFan.GeneratedWave;
        Vector2 spriteSize = sprite.bounds.size;

        blades = new SpriteRenderer[Blades];
        for (int i = 0; i < Blades; i++)
        {
            GameObject b = new GameObject("Blade");
            b.transform.SetParent(transform, false);
            b.transform.localScale = new Vector3(BladeSize / spriteSize.x, BladeSize / spriteSize.y, 1f);

            SpriteRenderer sr = b.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 50;
            blades[i] = sr;
        }
    }

    private void Update()
    {
        if (owner == null)
        {
            Destroy(gameObject);
            return;
        }

        age += Time.deltaTime;
        if (age >= Duration)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = owner.position + Vector3.up * 0.4f;

        float alpha = Mathf.Clamp01(Mathf.Min(age, Duration - age) / FadeTime);
        float spin = age * SpinSpeed;

        for (int i = 0; i < blades.Length; i++)
        {
            float angle = spin + 360f / blades.Length * i;
            Vector2 offset = Quaternion.Euler(0f, 0f, angle) * Vector2.right * BladeOrbit;

            // Die Sichel zeigt in Drehrichtung (tangential).
            blades[i].transform.localPosition = offset;
            blades[i].transform.localRotation = Quaternion.Euler(0f, 0f, angle + 90f);

            Color c = blades[i].color;
            c.a = alpha;
            blades[i].color = c;
        }
    }

    private void FixedUpdate()
    {
        if (owner == null) return;

        Vector2 center = owner.position;
        float r2 = Radius * Radius;
        var enemies = Enemy.Alive;

        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy e = enemies[i];
            if (e == null) continue;

            Vector2 away = (Vector2)e.transform.position - center;
            if (away.sqrMagnitude > r2) continue;
            if (away.sqrMagnitude < 0.0001f) away = Random.insideUnitCircle;

            e.ApplyPull(away.normalized * PushSpeed, 0.1f);
        }
    }
}
