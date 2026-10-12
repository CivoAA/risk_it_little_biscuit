using UnityEngine;

/// <summary>
/// Pfeil ueber der Spielfigur, der auf den aktuellen Boss (oder Zwischenboss)
/// zeigt, solange der nicht auf dem Bildschirm zu sehen ist. Sobald der Boss
/// ins Bild kommt, blendet der Pfeil aus. Das Bild wird hier per Code gemalt
/// (PPU 32 wie die Pixel-Perfect-Kamera), es braucht also kein Asset.
/// </summary>
public class BossPointer : MonoBehaviour
{
    private const int PPU = 32;
    private const float Radius = 0.55f;      // Abstand vom Kopfpunkt in Richtung Boss
    private const float HeadGap = 0.3f;      // Kopfpunkt so weit ueber der Figur
    private const float Bob = 0.08f;         // Wippen in Pfeilrichtung
    private const float FadeSpeed = 6f;
    private const float ScreenMargin = 0.04f; // Boss gilt erst etwas innerhalb des Rands als sichtbar

    private static readonly Color Fill = new Color32(0xF2, 0x4E, 0x4E, 0xFF);
    private static readonly Color Light = new Color32(0xFF, 0xA8, 0x8A, 0xFF);
    private static readonly Color Outline = new Color32(0x2A, 0x10, 0x18, 0xFF);

    private SpriteRenderer sr;
    private Enemy boss;
    private float nextSearch;
    private float alpha;
    private PlayerController headOf;
    private float headHeight;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        var go = new GameObject("BossPointer");
        go.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(go);
        go.AddComponent<BossPointer>();
    }

    private void Awake()
    {
        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = BuildSprite();
        sr.sortingLayerName = "Foreground";
        sr.sortingOrder = 500;
        sr.enabled = false;
    }

    private void LateUpdate()
    {
        PlayerController player = PlayerController.Instance;
        if (player == null || !player.gameObject.activeInHierarchy)
        {
            Hide();
            return;
        }

        FindBoss();
        Camera cam = Camera.main;
        bool want = boss != null && cam != null && !OnScreen(cam, boss.transform.position);

        alpha = Mathf.MoveTowards(alpha, want ? 1f : 0f, FadeSpeed * Time.unscaledDeltaTime);
        if (alpha <= 0f || boss == null)
        {
            Hide();
            return;
        }

        Vector2 head = HeadPoint(player);
        Vector2 dir = (Vector2)boss.transform.position - head;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
        dir.Normalize();

        float bob = (Mathf.Sin(Time.unscaledTime * 8f) * 0.5f + 0.5f) * Bob;
        Vector2 pos = head + dir * (Radius + bob);
        // Auf ganze Pixel setzen, sonst flimmert der Pfeil an der Pixel-Kamera
        pos.x = Mathf.Round(pos.x * PPU) / PPU;
        pos.y = Mathf.Round(pos.y * PPU) / PPU;

        transform.position = new Vector3(pos.x, pos.y, 0f);
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        // Leichtes Pulsieren, damit der Pfeil im Getuemmel auffaellt
        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f);
        sr.color = new Color(1f, 1f, 1f, alpha * pulse);
        sr.enabled = true;
    }

    private void Hide()
    {
        if (sr.enabled) sr.enabled = false;
        if (boss == null) alpha = 0f;
    }

    private void FindBoss()
    {
        if (boss != null && boss.isActiveAndEnabled) return;
        boss = null;
        // Nicht jeden Frame die ganze Gegnerliste durchgehen
        if (Time.unscaledTime < nextSearch) return;
        nextSearch = Time.unscaledTime + 0.5f;

        foreach (Enemy e in Enemy.Alive)
        {
            if (e == null || !e.isActiveAndEnabled) continue;
            // Echte Bosse haben Vorrang vor Zwischenbossen
            if (e.Role == EnemyRole.Boss) { boss = e; return; }
            if (e.Role == EnemyRole.MiniBoss && boss == null) boss = e;
        }
    }

    private static bool OnScreen(Camera cam, Vector3 world)
    {
        Vector3 v = cam.WorldToViewportPoint(world);
        return v.x > ScreenMargin && v.x < 1f - ScreenMargin
            && v.y > ScreenMargin && v.y < 1f - ScreenMargin;
    }

    /// <summary>Punkt ueber dem Kopf; die Hoehe wird einmal pro Figur gemessen,
    /// damit Animationsbilder unterschiedlicher Hoehe den Pfeil nicht wackeln lassen.</summary>
    private Vector2 HeadPoint(PlayerController player)
    {
        if (headOf != player)
        {
            headOf = player;
            headHeight = 0.5f;
            foreach (SpriteRenderer r in player.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!r.enabled || r.sprite == null) continue;
                headHeight = Mathf.Max(headHeight, r.bounds.max.y - player.transform.position.y);
            }
            headHeight = Mathf.Min(headHeight, 2f);
        }
        return (Vector2)player.transform.position + Vector2.up * (headHeight + HeadGap);
    }

    // ---------- Bild ----------

    // Pfeil zeigt nach rechts (+x); gedreht wird per Transform.
    private static readonly string[] Shape =
    {
        "....##.....",
        "....#o#....",
        "....#oo#...",
        "#####ooo#..",
        "#lllllooo#.",
        "#oooooooo#.",
        "#####ooo#..",
        "....#oo#...",
        "....#o#....",
        "....##.....",
    };

    private static Sprite BuildSprite()
    {
        int h = Shape.Length, w = Shape[0].Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "BossPointer"
        };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            string row = Shape[h - 1 - y];
            for (int x = 0; x < w; x++)
            {
                switch (row[x])
                {
                    case '#': px[y * w + x] = Outline; break;
                    case 'o': px[y * w + x] = Fill; break;
                    case 'l': px[y * w + x] = Light; break;
                    default: px[y * w + x] = Color.clear; break;
                }
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        // Pivot auf einem ganzen Pixel nahe der Mitte (sonst halber Pixel)
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(5f / w, 5f / h), PPU);
    }
}
