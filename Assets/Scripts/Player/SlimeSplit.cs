using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Skilltree "Schleimteilung" (<see cref="SkillGrants.Schleimteilung"/>): faellt
/// das Leben unter 35 %, spaltet sich vom Schleimkoenig ein Gelee-Doppelgaenger
/// ab und bleibt dort stehen, wo er war. Alle Gegner im <see cref="Radius"/>
/// laufen <see cref="Duration"/> Sekunden lang auf den Doppelgaenger zu statt
/// auf den Spieler (<see cref="Enemy.Lure"/>) - auch die, die waehrenddessen
/// neu in den Radius kommen. Danach zerplatzt er, und alle laufen wieder zum
/// Spieler. Macht keinen Schaden. Bosse und Minibosse lassen sich nicht locken.
///
/// Ausgeloest wird er von <see cref="PlayerController.TakeDamage"/>, der auch
/// die Abklingzeit haelt. Hier stehen die Werte, das Locken und das Bild: eine
/// lila eingefaerbte, wabbelnde Kopie des Spieler-Sprites.
/// </summary>
public class SlimeSplit : MonoBehaviour
{
    /// <summary>Ab diesem Lebensanteil spaltet er sich.</summary>
    public const float Threshold = 0.35f;

    /// <summary>So lange steht der Doppelgaenger (Sekunden).</summary>
    public const float Duration = 5f;

    /// <summary>Abklingzeit, gerechnet ab dem ENDE der Wirkung.</summary>
    public const float Cooldown = 35f;

    /// <summary>Gegner in dieser Reichweite (Tiles) um den Doppelgaenger werden gelockt.</summary>
    public const float Radius = 8f;

    /// <summary>So oft wird nachgelockt, damit neu ankommende Gegner auch mitlaufen.</summary>
    private const float LureInterval = 0.25f;

    private const float FadeTime = 0.25f;
    private static readonly Color Tint = new Color(0.75f, 0.55f, 1f, 0.8f);
    private static readonly Vector2 CenterOffset = new Vector2(0f, 0.45f);

    private float age;
    private float nextLure;
    private SpriteRenderer body;
    private Vector3 baseScale;

    /// <summary>Spaltet den Doppelgaenger dort ab, wo <paramref name="player"/> gerade steht.</summary>
    public static void Fire(Transform player, SpriteRenderer playerBody)
    {
        GameObject go = new GameObject("SlimeSplit");
        go.transform.position = player.position;

        Scene run = RunScene.Current;
        if (run.IsValid() && run.isLoaded) SceneManager.MoveGameObjectToScene(go, run);

        SlimeSplit split = go.AddComponent<SlimeSplit>();
        split.Build(playerBody);
    }

    private void Build(SpriteRenderer playerBody)
    {
        GameObject b = new GameObject("Body");
        b.transform.SetParent(transform, false);

        body = b.AddComponent<SpriteRenderer>();
        if (playerBody != null)
        {
            // eine Momentaufnahme des Spielers - gleiche Lage, gleiche Ebene, eins dahinter
            b.transform.position = playerBody.transform.position;
            b.transform.localScale = playerBody.transform.lossyScale;
            body.sprite = playerBody.sprite;
            body.flipX = playerBody.flipX;
            body.sortingLayerID = playerBody.sortingLayerID;
            body.sortingOrder = playerBody.sortingOrder - 1;
        }
        body.color = Tint;
        baseScale = b.transform.localScale;

        Pop();
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= Duration)
        {
            Pop();
            Destroy(gameObject);
            return;
        }

        if (body != null)
        {
            // Gelee wabbelt: breit und flach im Wechsel, Fuesse bleiben am Boden
            float wobble = Mathf.Sin(age * 9f) * 0.08f;
            body.transform.localScale = new Vector3(baseScale.x * (1f + wobble), baseScale.y * (1f - wobble), baseScale.z);

            Color c = Tint;
            c.a *= Mathf.Clamp01(Mathf.Min(age, Duration - age) / FadeTime);
            body.color = c;
        }

        if (Time.time >= nextLure)
        {
            nextLure = Time.time + LureInterval;
            LureAll();
        }
    }

    private void LureAll()
    {
        Vector2 center = transform.position;
        float r2 = Radius * Radius;
        float left = Duration - age;
        var enemies = Enemy.Alive;

        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy e = enemies[i];
            if (e == null || e.IsBoss) continue;
            if (((Vector2)e.transform.position - center).sqrMagnitude > r2) continue;

            e.Lure(transform, left);
        }
    }

    private void Pop()
    {
        Vector2 at = (Vector2)transform.position + CenterOffset;
        FoxFx.Play(RoyalSplat.HitFor(RoyalSplat.HitRadius), at, 22f, 1);
        WeaponSounds.Play(WeaponSounds.SplatLand);
    }
}
