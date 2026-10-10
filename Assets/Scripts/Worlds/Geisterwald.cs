using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Nachtstimmung im Lebkuchen-Geisterwald (Level 4, Szene Map_World6). Haengt
/// am Map-Objekt, der GeistBuilder setzt die Bilder.
///
///  - Nacht: dimmt das globale 2D-Licht aus GameCore auf ein kuehles Mondblau
///    (blendet weich ein) und stellt es beim Entladen der Karte zurueck.
///  - Spielerlicht: ein warmer Lichtkreis folgt der Figur, damit man im
///    Dunkeln sieht, was auf einen zukommt.
///  - Bodennebel und ein paar hohe Nebelschwaden ziehen langsam durchs Bild
///    (beleuchtet: im Dunkeln kaum zu sehen, im Laternenschein warm).
///  - Irrlichter schweben herum und werfen kleine farbige Lichter.
///  - Zuckergeister schweben ab und zu durchs Bild - kommt man ihnen zu nahe,
///    erschrecken sie und fliehen.
///  - Ab und zu flattert ein Fledermausschwarm vorbei.
///
/// Alles ist reine Deko ohne Collider. Wie der <see cref="Schneefall"/> wird
/// nichts zur Laufzeit erzeugt, sobald der Satz steht: Objekte, die aus dem
/// Bild fallen, springen auf die andere Seite. Laeuft mit Time.deltaTime,
/// steht also bei Level-Up und Pause still. Positionen rasten auf ganze
/// Pixel (32 px je Einheit), wie die Pixel-Perfect-Kamera.
///
/// Die Vorschau in Tools/geist_licht.py rechnet mit denselben Nachtwerten.
/// </summary>
public class Geisterwald : MonoBehaviour
{
    [Header("Nacht")]
    [Tooltip("Farbe des globalen Lichts bei Nacht. Mal Staerke = Umgebungslicht (Tools/geist_licht.py AMBIENT). " +
             "Das Projekt rechnet linear: 0.35 Licht macht eine Farbe nicht 35 %, sondern etwa 62 % so hell.")]
    [SerializeField] private Color nightColor = new Color(0.27f, 0.34f, 1f);
    [SerializeField] private float nightIntensity = 0.35f;
    [Tooltip("Sekunden, bis die Nacht nach dem Laden ganz da ist.")]
    [SerializeField] private float fadeIn = 1.6f;

    [Header("Spielerlicht")]
    [SerializeField] private Color playerLightColor = new Color(1f, 0.9f, 0.75f);
    [SerializeField] private float playerLightIntensity = 0.5f;
    [SerializeField] private float playerLightRadius = 5.5f;

    [Header("Bilder (setzt der GeistBuilder)")]
    [SerializeField] private Material unlitMaterial;
    [Tooltip("Sprite-Lit-Default: der Nebel nimmt das Licht an - dunkler Dunst im Wald, warm im Laternenschein.")]
    [SerializeField] private Material litMaterial;
    [SerializeField] private Sprite[] fogSprites;
    [SerializeField] private Sprite[] ghostFrames;     // 0-5 schweben, 6-7 erschrocken
    [SerializeField] private Sprite[] wispSprites;     // gross, klein
    [SerializeField] private Sprite[] batFrames;

    [Header("Mengen")]
    [SerializeField] private int groundFog = 9;
    [SerializeField] private int highFog = 3;
    [SerializeField] private int wisps = 9;
    [SerializeField] private int maxGhosts = 3;
    [SerializeField] private Vector2 ghostInterval = new Vector2(5f, 12f);
    [SerializeField] private Vector2 batInterval = new Vector2(14f, 26f);

    private const float Ppu = 32f;
    private static readonly Color[] WispColors =
    {
        new Color(0.45f, 1f, 0.9f), new Color(0.75f, 0.55f, 1f), new Color(1f, 0.72f, 0.35f),
    };

    private Camera cam;
    private Light2D globalLight;
    private Color dayColor;
    private float dayIntensity;
    private float nightT;

    private Light2D playerLight;

    private class Mote
    {
        public Transform tr;
        public SpriteRenderer sr;
        public Light2D light;
        public Vector2 pos;
        public Vector2 vel;
        public float phase;
        public float speed;
        public float baseAlpha;
        public float baseLight;
        public bool active;
        public bool scared;
        public float age;
        public float fade;
    }

    private readonly List<Mote> fog = new List<Mote>();
    private readonly List<Mote> wispList = new List<Mote>();
    private readonly List<Mote> ghosts = new List<Mote>();
    private readonly List<Mote> bats = new List<Mote>();
    private float ghostTimer;
    private float batTimer;

    // --- Aufbau -------------------------------------------------------------------

    private void Start()
    {
        cam = Camera.main;
        Rect view = View();

        playerLight = MakeLight("Spielerlicht", transform, playerLightColor, playerLightIntensity,
                                playerLightRadius, 0.5f);

        if (fogSprites != null && fogSprites.Length > 0)
        {
            for (int i = 0; i < groundFog + highFog; i++)
            {
                bool high = i >= groundFog;
                Sprite s = fogSprites[i % fogSprites.Length];
                Mote m = MakeMote("Nebel", s, high ? "Foreground" : "Background", high ? 35 : 12);
                if (litMaterial != null) m.sr.sharedMaterial = litMaterial;
                m.baseAlpha = high ? Random.Range(0.35f, 0.5f) : Random.Range(0.75f, 1f);
                m.vel = new Vector2(Random.Range(0.12f, 0.26f), Random.Range(-0.03f, 0.04f)) * (high ? 1.6f : 1f);
                m.pos = RandomIn(view);
                m.phase = Random.value * 10f;
                m.active = true;
                fog.Add(m);
            }
        }

        if (wispSprites != null && wispSprites.Length > 0)
        {
            for (int i = 0; i < wisps; i++)
            {
                Mote m = MakeMote("Irrlicht", wispSprites[i % 3 == 0 ? 0 : wispSprites.Length - 1], "Foreground", 38);
                Color c = WispColors[i % WispColors.Length];
                m.sr.color = c;
                m.baseAlpha = 1f;
                m.light = MakeLight("Licht", m.tr, c, i % 3 == 0 ? 0.6f : 0.4f, i % 3 == 0 ? 1.6f : 1.1f, 0f);
                m.baseLight = m.light.intensity;
                m.pos = RandomIn(view);
                m.phase = Random.value * 100f;
                m.speed = Random.Range(0.6f, 1.2f);
                m.active = true;
                wispList.Add(m);
            }
        }

        if (ghostFrames != null && ghostFrames.Length >= 8)
        {
            for (int i = 0; i < maxGhosts; i++)
            {
                Mote m = MakeMote("Zuckergeist", ghostFrames[0], "Foreground", 30);
                m.light = MakeLight("Schein", m.tr, new Color(0.75f, 0.72f, 1f), 0.3f, 1.3f, 0f);
                m.baseLight = m.light.intensity;
                m.tr.gameObject.SetActive(false);
                ghosts.Add(m);
            }
        }

        if (batFrames != null && batFrames.Length > 0)
        {
            for (int i = 0; i < 5; i++)
            {
                Mote m = MakeMote("Fledermaus", batFrames[0], "Foreground", 36);
                m.tr.gameObject.SetActive(false);
                bats.Add(m);
            }
        }

        ghostTimer = Random.Range(2f, 5f);
        batTimer = Random.Range(6f, 12f);
    }

    private Mote MakeMote(string name, Sprite sprite, string layer, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        if (unlitMaterial != null) sr.sharedMaterial = unlitMaterial;
        sr.sortingLayerName = layer;
        sr.sortingOrder = order;
        return new Mote { tr = go.transform, sr = sr };
    }

    private static Light2D MakeLight(string name, Transform parent, Color color, float intensity, float radius, float inner)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var l = go.AddComponent<Light2D>();
        l.lightType = Light2D.LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.pointLightOuterRadius = radius;
        l.pointLightInnerRadius = inner;
        l.falloffIntensity = 0.6f;
        l.shadowsEnabled = false;
        var layers = new int[SortingLayer.layers.Length];
        for (int i = 0; i < layers.Length; i++) layers[i] = SortingLayer.layers[i].id;
        l.targetSortingLayers = layers;
        return l;
    }

    // --- Nacht ----------------------------------------------------------------------

    private void FindGlobalLight()
    {
        // GameCore laedt oft erst nach der Karte - so lange weitersuchen.
        foreach (Light2D l in FindObjectsByType<Light2D>())
        {
            if (l.lightType != Light2D.LightType.Global) continue;
            globalLight = l;
            dayColor = l.color;
            dayIntensity = l.intensity;
            return;
        }
    }

    private void UpdateNight(float dt)
    {
        if (globalLight == null)
        {
            FindGlobalLight();
            if (globalLight == null) return;
        }
        nightT = Mathf.MoveTowards(nightT, 1f, dt / Mathf.Max(0.01f, fadeIn));
        float t = nightT * nightT * (3f - 2f * nightT);
        globalLight.color = Color.Lerp(dayColor, nightColor, t);
        globalLight.intensity = Mathf.Lerp(dayIntensity, nightIntensity, t);
    }

    private void OnDestroy()
    {
        if (globalLight != null)
        {
            globalLight.color = dayColor;
            globalLight.intensity = dayIntensity;
        }
    }

    // --- Lauf -----------------------------------------------------------------------

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        // Die Nacht blendet auch ein, wenn das Spiel gleich zu Beginn pausiert.
        UpdateNight(Mathf.Max(dt, Time.unscaledDeltaTime * 0.5f));

        Rect view = View();
        float t = Time.time;

        Vector2 player = view.center;
        bool hasPlayer = PlayerController.Instance != null;
        if (hasPlayer) player = PlayerController.Instance.transform.position;

        if (playerLight != null)
        {
            playerLight.gameObject.SetActive(hasPlayer);
            playerLight.transform.position = new Vector3(player.x, player.y + 0.3f, 0f);
            playerLight.intensity = playerLightIntensity * (0.94f + 0.06f * Mathf.Sin(t * 1.7f));
        }

        UpdateFog(view, dt, t);
        UpdateWisps(view, dt, t);
        UpdateGhosts(view, dt, t, player, hasPlayer);
        UpdateBats(view, dt);
    }

    private void UpdateFog(Rect view, float dt, float t)
    {
        foreach (Mote m in fog)
        {
            m.pos += m.vel * dt;
            Vector2 half = m.sr.sprite.bounds.extents;
            Rect wide = new Rect(view.xMin - half.x, view.yMin - half.y, view.width + half.x * 2f, view.height + half.y * 2f);
            if (Wrap(ref m.pos, wide))
                m.age = 0f;   // auf der anderen Seite weich einblenden
            m.age += dt;
            float a = m.baseAlpha * Mathf.Clamp01(m.age / 2f) * (0.85f + 0.15f * Mathf.Sin(t * 0.4f + m.phase));
            m.sr.color = new Color(1f, 1f, 1f, a);
            m.tr.position = Snap(m.pos);
        }
    }

    private void UpdateWisps(Rect view, float dt, float t)
    {
        foreach (Mote m in wispList)
        {
            // Irrlicht: zieht in weichen Kurven, haelt kurz inne, flackert
            float a = Mathf.PerlinNoise(m.phase, t * 0.15f * m.speed) * Mathf.PI * 4f;
            float go = Mathf.Clamp01(Mathf.PerlinNoise(m.phase + 7f, t * 0.3f) * 1.6f - 0.3f);
            m.pos += new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (0.7f * m.speed * go * dt);
            Wrap(ref m.pos, view);
            float pulse = 0.65f + 0.35f * Mathf.Sin(t * 3.1f * m.speed + m.phase);
            m.sr.color = new Color(m.sr.color.r, m.sr.color.g, m.sr.color.b, pulse);
            m.light.intensity = m.baseLight * pulse;
            float bob = Mathf.Sin(t * 2f + m.phase) * 0.12f;
            m.tr.position = Snap(m.pos + new Vector2(0f, bob));
        }
    }

    private void UpdateGhosts(Rect view, float dt, float t, Vector2 player, bool hasPlayer)
    {
        ghostTimer -= dt;
        if (ghostTimer <= 0f)
        {
            ghostTimer = Random.Range(ghostInterval.x, ghostInterval.y);
            foreach (Mote m in ghosts)
            {
                if (m.active) continue;
                SpawnGhost(m, view);
                break;
            }
        }

        foreach (Mote m in ghosts)
        {
            if (!m.active) continue;
            m.age += dt;

            if (!m.scared && hasPlayer && (m.pos - player).sqrMagnitude < 2.4f * 2.4f)
            {
                // Buh! Der Keks hat ihn erschreckt - weg hier.
                m.scared = true;
                Vector2 away = (m.pos - player).normalized;
                if (away.sqrMagnitude < 0.01f) away = Vector2.up;
                m.vel = (away + Vector2.up * 0.6f).normalized * 3.2f;
                m.age = 0f;
            }

            m.pos += m.vel * dt;
            int frame;
            float alpha;
            if (m.scared)
            {
                frame = 6 + Mathf.FloorToInt(m.age * 14f) % 2;
                alpha = m.fade * Mathf.Clamp01(1f - m.age / 0.7f);
                if (m.age > 0.7f) { Hide(m); continue; }
            }
            else
            {
                frame = Mathf.FloorToInt(m.age * 6f) % 6;
                m.fade = Mathf.Clamp01(m.age / 1.2f) * 0.8f;
                alpha = m.fade;
                Rect wide = new Rect(view.xMin - 3f, view.yMin - 3f, view.width + 6f, view.height + 6f);
                if (m.age > 2f && !wide.Contains(m.pos)) { Hide(m); continue; }
            }
            m.sr.sprite = ghostFrames[frame];
            m.sr.flipX = m.vel.x < 0f;
            m.sr.color = new Color(1f, 1f, 1f, alpha);
            m.light.intensity = m.baseLight * alpha;
            float bob = m.scared ? 0f : Mathf.Sin(t * 2.4f + m.phase) * 0.18f;
            m.tr.position = Snap(m.pos + new Vector2(0f, bob));
        }
    }

    private void SpawnGhost(Mote m, Rect view)
    {
        bool fromLeft = Random.value < 0.5f;
        float y = Random.Range(view.yMin + 1f, view.yMax - 1f);
        m.pos = new Vector2(fromLeft ? view.xMin - 0.8f : view.xMax + 0.8f, y);
        m.vel = new Vector2(fromLeft ? 1f : -1f, Random.Range(-0.15f, 0.15f)) * Random.Range(0.55f, 0.85f);
        m.phase = Random.value * 10f;
        m.age = 0f;
        m.fade = 0f;
        m.scared = false;
        m.active = true;
        m.sr.color = new Color(1f, 1f, 1f, 0f);
        m.tr.position = Snap(m.pos);
        m.tr.gameObject.SetActive(true);
    }

    private void UpdateBats(Rect view, float dt)
    {
        batTimer -= dt;
        if (batTimer <= 0f)
        {
            batTimer = Random.Range(batInterval.x, batInterval.y);
            bool any = false;
            foreach (Mote m in bats) any |= m.active;
            if (!any)
            {
                bool fromLeft = Random.value < 0.5f;
                float y = Random.Range(view.yMin + 1.5f, view.yMax - 1f);
                Vector2 dir = new Vector2(fromLeft ? 1f : -1f, Random.Range(-0.25f, 0.35f)).normalized;
                int n = Random.Range(3, bats.Count + 1);
                for (int i = 0; i < n; i++)
                {
                    Mote m = bats[i];
                    m.pos = new Vector2(fromLeft ? view.xMin - 1f - i * 0.7f : view.xMax + 1f + i * 0.7f,
                                        y + Random.Range(-1.2f, 1.2f));
                    m.vel = dir * Random.Range(3.2f, 4.2f);
                    m.phase = Random.value * 10f;
                    m.age = 0f;
                    m.active = true;
                    m.sr.flipX = !fromLeft;
                    m.tr.gameObject.SetActive(true);
                }
            }
        }

        foreach (Mote m in bats)
        {
            if (!m.active) continue;
            m.age += dt;
            m.pos += m.vel * dt;
            m.sr.sprite = batFrames[Mathf.FloorToInt(m.age * 12f + m.phase) % batFrames.Length];
            float wobble = Mathf.Sin(m.age * 7f + m.phase) * 0.25f;
            m.tr.position = Snap(m.pos + new Vector2(0f, wobble));
            Rect wide = new Rect(view.xMin - 4f, view.yMin - 4f, view.width + 8f, view.height + 8f);
            if (m.age > 1f && !wide.Contains(m.pos)) Hide(m);
        }
    }

    private static void Hide(Mote m)
    {
        m.active = false;
        m.tr.gameObject.SetActive(false);
    }

    // --- Kleinkram ------------------------------------------------------------------

    private Rect View()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return new Rect(-9f, -6f, 18f, 12f);
        float h = cam.orthographicSize * 2f;
        float w = h * cam.aspect;
        Vector3 c = cam.transform.position;
        return new Rect(c.x - w / 2f - 1f, c.y - h / 2f - 1f, w + 2f, h + 2f);
    }

    private static Vector2 RandomIn(Rect r) => new Vector2(Random.Range(r.xMin, r.xMax), Random.Range(r.yMin, r.yMax));

    /// <summary>Haelt p im Rechteck, indem es auf die Gegenseite springt. True = ist gesprungen.</summary>
    private static bool Wrap(ref Vector2 p, Rect r)
    {
        bool jumped = false;
        if (p.x < r.xMin) { p.x += r.width; jumped = true; }
        else if (p.x > r.xMax) { p.x -= r.width; jumped = true; }
        if (p.y < r.yMin) { p.y += r.height; jumped = true; }
        else if (p.y > r.yMax) { p.y -= r.height; jumped = true; }
        return jumped;
    }

    private static Vector3 Snap(Vector2 p) =>
        new Vector3(Mathf.Round(p.x * Ppu) / Ppu, Mathf.Round(p.y * Ppu) / Ppu, 0f);
}
