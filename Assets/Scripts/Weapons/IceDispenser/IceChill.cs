using UnityEngine;

/// <summary>
/// Kaelte auf einem Gegner (<see cref="IceDispenser"/>): zaehlt die Treffer
/// der Eiswuerfel mit, zeigt sie als Schneeflocken ueber dem Kopf und friert
/// den Gegner beim <see cref="IceDispenser.ChillToFreeze"/>. Treffer in einen
/// Eisblock (<see cref="Enemy.Freeze"/>). Ohne neuen Treffer verfallen die
/// Stapel nach <see cref="IceDispenser.ChillDecay"/> Sekunden.
///
/// Haengt am Gegner und bleibt dort; wird der Gegner in den Pool
/// zurueckgelegt, ist alles wieder auf null.
/// </summary>
public class IceChill : MonoBehaviour
{
    private const float BlockFormFps = 16f;
    private const float BlockShimmerFps = 3f;
    private const int BlockFormFrames = 3;

    private Enemy enemy;
    private SpriteRenderer enemySprite;
    private SpriteRenderer mark;
    private SpriteRenderer block;
    private Sprite[] blockFrames;

    private int stacks;
    private float lastHit;
    private float blockSince = -1f;
    private float blockUntil;

    /// <summary>
    /// Ein Kaeltestapel mehr. true, wenn damit die Schwelle erreicht ist -
    /// normale Gegner sind dann eingefroren, Bosse nicht (der Aufrufer beisst).
    /// </summary>
    public static bool Add(Enemy e, float freezeTime)
    {
        if (e == null || e.IsFrozen) return false;

        IceChill chill = e.GetComponent<IceChill>();
        if (chill == null)
        {
            chill = e.gameObject.AddComponent<IceChill>();
            chill.enemy = e;
            chill.enemySprite = e.GetComponentInChildren<SpriteRenderer>();
        }
        chill.enabled = true;

        if (Time.time - chill.lastHit > IceDispenser.ChillDecay) chill.stacks = 0;
        chill.lastHit = Time.time;
        chill.stacks++;

        if (chill.stacks < IceDispenser.ChillToFreeze)
        {
            chill.DrawMark();
            return false;
        }

        chill.stacks = 0;
        chill.HideMark();
        if (!e.IsBoss)
        {
            e.Freeze(freezeTime, 0.5f);
            chill.ShowBlock(freezeTime);
            WeaponSounds.Play(WeaponSounds.IceFreeze);
        }
        return true;
    }

    /// <summary>Alles weg (zerschmettert).</summary>
    public static void Clear(Enemy e)
    {
        if (e == null) return;
        IceChill chill = e.GetComponent<IceChill>();
        if (chill == null) return;
        chill.stacks = 0;
        chill.HideMark();
        chill.HideBlock();
    }

    void Update()
    {
        if (enemy == null)
        {
            Destroy(this);
            return;
        }

        bool markOn = stacks > 0 && Time.time - lastHit <= IceDispenser.ChillDecay;
        if (markOn) DrawMark();
        else HideMark();

        if (blockSince >= 0f)
        {
            if (!enemy.IsFrozen || Time.time >= blockUntil) HideBlock();
            else DrawBlock();
        }

        if (!markOn && blockSince < 0f) enabled = false;
    }

    void OnDisable()
    {
        // Gegner zurueck in den Pool (oder Komponente schlaeft): nichts haengen lassen
        if (enemy != null && !enemy.gameObject.activeInHierarchy)
        {
            stacks = 0;
            HideMark();
            HideBlock();
        }
    }

    void OnDestroy()
    {
        if (mark != null) Destroy(mark.gameObject);
        if (block != null) Destroy(block.gameObject);
    }

    // ------------------------------------------------------------------

    private SpriteRenderer Child(string name, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(enemy.transform, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        if (enemySprite != null)
        {
            sr.sortingLayerID = enemySprite.sortingLayerID;
            sr.sortingOrder = enemySprite.sortingOrder + order;
        }
        return sr;
    }

    /// <summary>Gegen die Skalierung des Gegners, damit die Pixel 1:1 bleiben.</summary>
    private void Unscale(Transform t)
    {
        Vector3 parent = enemy.transform.lossyScale;
        t.localScale = new Vector3(1f / Mathf.Max(0.01f, Mathf.Abs(parent.x)),
                                   1f / Mathf.Max(0.01f, Mathf.Abs(parent.y)), 1f);
    }

    private void DrawMark()
    {
        if (mark == null) mark = Child("IceChillMark", 3);
        mark.enabled = true;
        Sprite[] frames = IceDispenser.Mark;
        mark.sprite = frames[Mathf.Clamp(stacks - 1, 0, frames.Length - 1)];

        float top = enemySprite != null ? enemySprite.bounds.max.y : enemy.transform.position.y + 0.6f;
        float x = enemySprite != null ? enemySprite.bounds.center.x : enemy.transform.position.x;
        mark.transform.position = SweetFx.Snap(new Vector2(x, top + 0.18f));
        Unscale(mark.transform);
    }

    private void HideMark()
    {
        if (mark != null) mark.enabled = false;
    }

    private void ShowBlock(float seconds)
    {
        if (block == null) block = Child("IceChillBlock", 2);
        float width = enemySprite != null ? enemySprite.bounds.size.x * 0.8f : 0.8f;
        blockFrames = IceDispenser.BlockFor(Mathf.Max(0.6f, width));
        blockSince = Time.time;
        blockUntil = Time.time + seconds;
        block.enabled = true;
        DrawBlock();
    }

    private void DrawBlock()
    {
        if (block == null || blockFrames == null) return;
        float t = Time.time - blockSince;
        int formed = (int)(t * BlockFormFps);
        int frame;
        if (formed < BlockFormFrames || blockFrames.Length <= BlockFormFrames)
        {
            frame = Mathf.Min(formed, blockFrames.Length - 1);
        }
        else
        {
            int shimmer = blockFrames.Length - BlockFormFrames + 1;
            frame = BlockFormFrames - 1 + (int)(t * BlockShimmerFps) % shimmer;
        }
        block.sprite = blockFrames[frame];

        // Pivot unten: auf die Fuesse des Gegners
        float bottom = enemySprite != null ? enemySprite.bounds.min.y : enemy.transform.position.y;
        float x = enemySprite != null ? enemySprite.bounds.center.x : enemy.transform.position.x;
        block.transform.position = SweetFx.Snap(new Vector2(x, bottom - 1f / 32f));
        Unscale(block.transform);
    }

    private void HideBlock()
    {
        blockSince = -1f;
        if (block != null) block.enabled = false;
    }
}
