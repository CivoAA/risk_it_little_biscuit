using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DeathstrikePrefab : MonoBehaviour
{
    public Deathstrike weapon;

    private Dictionary<Enemy, float> lastHitTimes = new Dictionary<Enemy, float>();
    private float hitCooldown = 1f;
    private float lifeTime = 0.75f;

    // Blitz aus Resources/Weapons/deathstrike_blitz (deathstrike_blitz.py):
    // 64x384 PPU 32, einmal abgespielt - Vorentladung, Einschlag, Nachschlag, Funken.
    private const float Fps = 14f;
    private static Sprite[] frames;
    private SpriteRenderer sr;
    private float age;

    void Start()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;

        sr = GetComponent<SpriteRenderer>();
        ShowFrame();

        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        age += Time.deltaTime;
        ShowFrame();
    }

    private void ShowFrame()
    {
        Sprite[] f = Frames();
        if (sr == null || f == null) return;
        sr.sprite = f[Mathf.Min(f.Length - 1, Mathf.FloorToInt(age * Fps))];
    }

    private static Sprite[] Frames()
    {
        if (frames != null) return frames;
        Sprite[] all = Resources.LoadAll<Sprite>("Weapons/deathstrike_blitz");
        if (all == null || all.Length == 0) return null;
        System.Array.Sort(all, (a, b) => FrameIndex(a).CompareTo(FrameIndex(b)));
        return frames = all;
    }

    private static int FrameIndex(Sprite s)
    {
        int i = s.name.LastIndexOf('_');
        return i >= 0 && int.TryParse(s.name.Substring(i + 1), out int n) ? n : 0;
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (!col.CompareTag("Enemy")) return;
        if (weapon == null || !weapon.IsActive) return;

        Enemy enemy = col.GetComponent<Enemy>();
        if (enemy == null) return;

        if (lastHitTimes.TryGetValue(enemy, out float lastHit))
        {
            if (Time.time - lastHit < hitCooldown)
                return;
        }

        enemy.TakeDamage(weapon.CurrentStats.damage);
        lastHitTimes[enemy] = Time.time;
    }
}
