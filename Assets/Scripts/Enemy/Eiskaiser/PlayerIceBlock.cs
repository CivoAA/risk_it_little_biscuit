using UnityEngine;

/// <summary>
/// Der Eisblock, in den der Frostring des Eiskaisers den Spieler einfriert.
///
/// Solange er steht, setzt er <see cref="PlayerController.FrozenUntil"/> - der
/// Spieler bleibt stehen, seine Waffen feuern weiter. Jede neu gedrueckte
/// Taste kratzt am Eis: die Zeit wird kuerzer und der Block wackelt. Wer
/// haemmert, ist schneller frei.
///
/// Bilder aus Tools/eiskaiser.py (eiskaiser_eisblock): 0-2 zufrieren,
/// 3-6 halten (Schleife), 7 Riss, 8-9 zerspringen.
/// </summary>
public class PlayerIceBlock : MonoBehaviour
{
    private const float Fps = 12f;
    private const float MashCut = 0.12f;          // so viel kuerzer je Tastendruck
    private const float MinRemaining = 0.15f;     // ganz weg haemmern geht nicht
    private const float ShakeTime = 0.08f;

    private static PlayerIceBlock current;

    private SpriteRenderer sr;
    private Sprite[] frames;
    private PlayerController player;
    private float until;
    private float age;
    private float shakeUntil;
    private bool breaking;
    private float breakAge;

    /// <summary>Ist der Spieler gerade eingefroren? (Ein zweiter Treffer verlaengert nicht.)</summary>
    public static bool Active => current != null && !current.breaking;

    /// <summary>Friert den Spieler fuer <paramref name="seconds"/> ein.</summary>
    public static void Apply(PlayerController player, Sprite[] frames, float seconds)
    {
        if (player == null || frames == null || frames.Length < 10 || Active) return;

        var go = new GameObject("Eisblock");
        RunScene.Place(go, "Effekte");
        var block = go.AddComponent<PlayerIceBlock>();
        block.sr = go.AddComponent<SpriteRenderer>();
        block.sr.sortingLayerName = "Objects";
        block.sr.sortingOrder = 20;                // vor dem Keks
        block.frames = frames;
        block.player = player;
        block.until = Time.time + seconds;
        block.sr.sprite = frames[0];
        current = block;
        player.FrozenUntil = block.until;
        block.Follow();
        EiskaiserSounds.Freeze();
    }

    private void OnDestroy()
    {
        if (current == this) current = null;
    }

    private void Update()
    {
        if (player == null || !player.gameObject.activeInHierarchy)
        {
            Destroy(gameObject);
            return;
        }

        float dt = Time.deltaTime;
        age += dt;

        if (!breaking)
        {
            if (Input.anyKeyDown && age > 0.1f)
            {
                until = Mathf.Max(Time.time + MinRemaining, until - MashCut);
                shakeUntil = Time.time + ShakeTime;
                EiskaiserSounds.Scratch();
            }
            player.FrozenUntil = until;

            int f = age < 3f / Fps ? Mathf.FloorToInt(age * Fps) : 3 + Mathf.FloorToInt(age * 8f) % 4;
            if (until - Time.time < 0.14f) f = 7;     // Riss kurz vor dem Ende
            sr.sprite = frames[Mathf.Clamp(f, 0, 7)];

            if (Time.time >= until)
            {
                breaking = true;
                player.FrozenUntil = 0f;
                EiskaiserSounds.Shatter();
            }
        }
        else
        {
            breakAge += dt;
            int f = 8 + Mathf.FloorToInt(breakAge * Fps);
            if (f > 9)
            {
                Destroy(gameObject);
                return;
            }
            sr.sprite = frames[f];
        }
    }

    private void LateUpdate()
    {
        if (player != null) Follow();
    }

    private void Follow()
    {
        Vector3 p = player.transform.position;
        // Pivot des Blocks liegt am Boden, der Keks steht mittig: etwas tiefer setzen
        float shake = Time.time < shakeUntil ? (Mathf.Sin(Time.time * 120f) > 0 ? 1f : -1f) / 32f : 0f;
        transform.position = new Vector3(p.x + shake, p.y - 0.62f, p.z);
    }
}
