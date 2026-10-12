using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Alle Geschosse des Herzens im Endkampf (<see cref="VerkohlterHerzFinale"/>)
/// in einem Topf: Funken (Ringe, Spiralen) und Glutkugeln (Salven). Ein
/// Update fuer alle statt ein Skript pro Geschoss - im Finale fliegen gut
/// zweihundert gleichzeitig.
///
/// Ein Geschoss fliegt geradeaus (optional mit Beschleunigung und Kurve),
/// trifft den Spieler per Abstand (Kreis um seine Mitte) und zerplatzt dabei
/// in ein paar Glutpixel. Verlaesst es den Boden der Kammer, verglimmt es.
///
/// Bilder: herz_funke (4 Bilder 12x12), herz_glutkugel (6 Bilder 24x24).
/// </summary>
public class HerzGeschosse : MonoBehaviour
{
    public enum Art { Funke, Kugel }

    private const float SpawnFade = 0.08f;
    private const float FunkeRadius = 0.15f;
    private const float KugelRadius = 0.3f;
    /// <summary>Spieler-Trefferkreis: etwas kleiner als sein Koerper - wer knapp vorbeischrammt, kommt durch.</summary>
    private const float PlayerRadius = 0.22f;
    private const float MaxLife = 9f;

    private struct Shot
    {
        public SpriteRenderer Sr;
        public Vector2 Pos, Vel;
        public float Accel, Curve, MaxSpeed;
        public float Age, Radius, Damage;
        public Art Kind;
    }

    private readonly List<Shot> shots = new List<Shot>(256);
    private readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
    private readonly List<Spark> sparks = new List<Spark>(64);

    private struct Spark
    {
        public SpriteRenderer Sr;
        public Vector2 Pos, Vel;
        public float Age, Life;
    }

    private VerkohlterHerzkammer chamber;
    private Sprite[] funke, kugel;
    private int objects;

    public int Count => shots.Count;

    public static HerzGeschosse Create(VerkohlterHerzkammer chamber)
    {
        var go = new GameObject("Herz_Geschosse");
        RunScene.Place(go, "Effekte");
        var g = go.AddComponent<HerzGeschosse>();
        g.chamber = chamber;
        g.funke = VerkohlterArt.Strip("herz_funke");
        g.kugel = VerkohlterArt.Strip("herz_glutkugel");
        g.objects = SortingLayer.NameToID("Objects");
        return g;
    }

    // ------------------------------------------------------------- Feuern

    public void Fire(Vector2 from, Vector2 velocity, Art kind, float damage, float accel = 0f, float curve = 0f,
                     float maxSpeed = 12f)
    {
        var s = new Shot
        {
            Sr = Take(kind == Art.Kugel ? 22 : 21),
            Pos = from,
            Vel = velocity,
            Accel = accel,
            Curve = curve,
            MaxSpeed = maxSpeed,
            Radius = kind == Art.Kugel ? KugelRadius : FunkeRadius,
            Damage = damage,
            Kind = kind,
        };
        s.Sr.sprite = VerkohlterArt.Frame(kind == Art.Kugel ? kugel : funke, 0);
        s.Sr.transform.position = VerkohlterArt.Snap(from);
        s.Sr.color = new Color(1f, 1f, 1f, 0f);
        shots.Add(s);
    }

    /// <summary>
    /// Ein Ring aus <paramref name="count"/> Geschossen. <paramref name="gapAt"/>
    /// (Grad, NaN = keine) laesst eine Luecke von <paramref name="gapDeg"/> Grad -
    /// dort kommt man durch.
    /// </summary>
    public void Ring(Vector2 center, float startRadius, int count, float speed, float offsetDeg, Art kind,
                     float damage, float gapAt = float.NaN, float gapDeg = 0f, float accel = 0f, float curve = 0f)
    {
        for (int i = 0; i < count; i++)
        {
            float a = offsetDeg + i * 360f / count;
            if (!float.IsNaN(gapAt) && Mathf.Abs(Mathf.DeltaAngle(a, gapAt)) < gapDeg * 0.5f) continue;
            Vector2 dir = Dir(a);
            Fire(center + dir * startRadius, dir * speed, kind, damage, accel, curve);
        }
    }

    public static Vector2 Dir(float deg)
    {
        float r = deg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
    }

    /// <summary>Alle Geschosse zerplatzen auf einmal (Herz stirbt, Phasenwechsel).</summary>
    public void PopAll()
    {
        for (int i = shots.Count - 1; i >= 0; i--) Burst(i, 2);
    }

    // ------------------------------------------------------------- Ablauf

    private void Update()
    {
        float dt = Time.deltaTime;
        PlayerController pc = PlayerController.Instance;
        bool playerHit = pc != null && pc.gameObject.activeSelf && !pc.Captured;
        Vector2 player = playerHit ? (Vector2)pc.transform.position : Vector2.zero;

        for (int i = shots.Count - 1; i >= 0; i--)
        {
            Shot s = shots[i];
            s.Age += dt;
            if (s.Curve != 0f) s.Vel = Rotate(s.Vel, s.Curve * dt);
            if (s.Accel != 0f)
            {
                float sp = Mathf.Clamp(s.Vel.magnitude + s.Accel * dt, 0.4f, s.MaxSpeed);
                s.Vel = s.Vel.normalized * sp;
            }
            s.Pos += s.Vel * dt;

            Sprite[] strip = s.Kind == Art.Kugel ? kugel : funke;
            if (strip.Length > 0) s.Sr.sprite = strip[(Mathf.FloorToInt(s.Age * 14f) + i) % strip.Length];
            s.Sr.transform.position = VerkohlterArt.Snap(s.Pos);
            s.Sr.color = new Color(1f, 1f, 1f, Mathf.Clamp01(s.Age / SpawnFade));
            shots[i] = s;

            if (playerHit && s.Age > SpawnFade)
            {
                float reach = s.Radius + PlayerRadius;
                if ((player - s.Pos).sqrMagnitude <= reach * reach)
                {
                    pc.TakeDamage(s.Damage);
                    Burst(i, s.Kind == Art.Kugel ? 8 : 4);
                    continue;
                }
            }

            bool gone = s.Age > MaxLife || (chamber != null && !InsideRoom(s.Pos));
            if (gone) Burst(i, 2);
        }

        UpdateSparks(dt);
    }

    private bool InsideRoom(Vector2 p)
    {
        Vector2 f = p - chamber.FloorMid;
        Vector2 r = VerkohlterArt.FloorRadius + new Vector2(1.2f, 1.6f);
        return (f.x * f.x) / (r.x * r.x) + (f.y * f.y) / (r.y * r.y) <= 1f;
    }

    private static Vector2 Rotate(Vector2 v, float deg)
    {
        float r = deg * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    private void Burst(int index, int pixels)
    {
        Shot s = shots[index];
        shots.RemoveAt(index);
        Give(s.Sr);
        for (int k = 0; k < pixels; k++) AddSpark(s.Pos, Random.insideUnitCircle * 2.4f + s.Vel * 0.15f, 0.35f);
    }

    /// <summary>Ein einzelner Glutpixel, der wegspritzt und verglimmt.</summary>
    public void AddSpark(Vector2 at, Vector2 vel, float life)
    {
        var sp = new Spark { Sr = Take(23), Pos = at, Vel = vel, Life = life * Random.Range(0.7f, 1.3f) };
        sp.Sr.sprite = VerkohlterArt.Pixel;
        sp.Sr.transform.localScale = Vector3.one * (Random.value < 0.3f ? 2f : 1f);
        sparks.Add(sp);
    }

    private void UpdateSparks(float dt)
    {
        Color32[] fire = VerkohlterArt.Fire;
        for (int i = sparks.Count - 1; i >= 0; i--)
        {
            Spark sp = sparks[i];
            sp.Age += dt;
            if (sp.Age >= sp.Life)
            {
                sp.Sr.transform.localScale = Vector3.one;
                Give(sp.Sr);
                sparks.RemoveAt(i);
                continue;
            }
            sp.Vel *= 1f - 3f * dt;
            sp.Pos += sp.Vel * dt;
            float q = sp.Age / sp.Life;
            sp.Sr.color = fire[q < 0.25f ? 6 : q < 0.5f ? 5 : q < 0.75f ? 3 : 2];
            sp.Sr.transform.position = VerkohlterArt.Snap(sp.Pos);
            sparks[i] = sp;
        }
    }

    // ------------------------------------------------------------- Vorrat

    private SpriteRenderer Take(int order)
    {
        SpriteRenderer sr;
        if (pool.Count > 0)
        {
            sr = pool.Pop();
            sr.gameObject.SetActive(true);
        }
        else
        {
            var go = new GameObject("Geschoss");
            go.transform.SetParent(transform, false);
            sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerID = objects;
        }
        sr.sortingOrder = order;
        sr.color = Color.white;
        return sr;
    }

    private void Give(SpriteRenderer sr)
    {
        if (sr == null) return;
        sr.gameObject.SetActive(false);
        pool.Push(sr);
    }
}
