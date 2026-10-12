using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Leuchtebene der Geisterwald-Gegner: ein Kind-Objekt mit unbeleuchtetem
/// Sprite (Sprite-Unlit), das ueber dem normalen, vom Nachtlicht gedimmten
/// Bild liegt. Darauf sind nur die leuchtenden Teile gemalt (Augen, Flammen,
/// Zuckerguss ...) plus ein weicher Schein drumherum - so gluehen sie im
/// dunklen Wald, waehrend der Rest des Gegners im Dunkeln bleibt.
///
/// Der Animator spielt weiter nur den Koerper ab; die Ebene sucht sich in
/// LateUpdate das passende Bild (gleicher Index im Streifen) und uebernimmt
/// Spiegelung, Farbe und Sichtbarkeit vom Koerper.
///
/// Optional ein kleines echtes Licht (Light2D), das den Boden um den Gegner
/// erhellt. Weil jedes Licht Rechenzeit kostet, leuchten hoechstens
/// <see cref="MaxLights"/> Gegner gleichzeitig - die anderen haben nur den Schein.
/// </summary>
public class EnemyGlowLayer : MonoBehaviour
{
    /// <summary>So viele Gegner-Lichter gleichzeitig, mehr nicht.</summary>
    public const int MaxLights = 10;

    [SerializeField] private SpriteRenderer body;
    [SerializeField] private SpriteRenderer glow;
    [SerializeField] private Sprite[] bodyFrames;
    [SerializeField] private Sprite[] glowFrames;

    [Header("Licht (optional)")]
    [SerializeField] private bool castLight = true;
    [SerializeField] private Color lightColor = Color.white;
    [SerializeField] private float lightIntensity = 0.7f;
    [SerializeField] private float lightRadius = 1.6f;
    [SerializeField] private Vector2 lightOffset = new Vector2(0f, 0.5f);

    private static int activeLights;
    private static int[] allLayers;

    private Dictionary<Sprite, Sprite> map;
    private Sprite lastBody;
    private Light2D lamp;
    private bool holdsLight;

    private void Awake()
    {
        if (body == null) body = GetComponent<SpriteRenderer>();
        map = new Dictionary<Sprite, Sprite>(bodyFrames != null ? bodyFrames.Length : 0);
        if (bodyFrames != null && glowFrames != null)
        {
            int n = Mathf.Min(bodyFrames.Length, glowFrames.Length);
            for (int i = 0; i < n; i++)
            {
                if (bodyFrames[i] != null && !map.ContainsKey(bodyFrames[i])) map.Add(bodyFrames[i], glowFrames[i]);
            }
        }
    }

    private void OnEnable()
    {
        lastBody = null;
        if (!castLight || activeLights >= MaxLights) return;

        if (lamp == null) lamp = MakeLight();
        lamp.enabled = true;
        holdsLight = true;
        activeLights++;
    }

    private void OnDisable()
    {
        if (!holdsLight) return;
        holdsLight = false;
        activeLights = Mathf.Max(0, activeLights - 1);
        if (lamp != null) lamp.enabled = false;
    }

    private void LateUpdate()
    {
        if (body == null || glow == null) return;

        Sprite s = body.sprite;
        if (s != lastBody)
        {
            lastBody = s;
            glow.sprite = s != null && map.TryGetValue(s, out Sprite g) ? g : null;
        }

        glow.flipX = body.flipX;
        glow.enabled = body.enabled;
        glow.color = body.color;
        if (lamp != null && holdsLight)
        {
            Vector2 o = lightOffset;
            if (body.flipX) o.x = -o.x;
            lamp.transform.localPosition = o;
            lamp.color = lightColor * body.color.a;
        }
    }

    private Light2D MakeLight()
    {
        var go = new GameObject("Licht");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = lightOffset;
        var l = go.AddComponent<Light2D>();
        l.lightType = Light2D.LightType.Point;
        l.color = lightColor;
        l.intensity = lightIntensity;
        l.pointLightOuterRadius = lightRadius;
        l.pointLightInnerRadius = 0f;
        l.falloffIntensity = 0.7f;
        l.shadowsEnabled = false;
        if (allLayers == null || allLayers.Length != SortingLayer.layers.Length)
        {
            allLayers = new int[SortingLayer.layers.Length];
            for (int i = 0; i < allLayers.Length; i++) allLayers[i] = SortingLayer.layers[i].id;
        }
        l.targetSortingLayers = allLayers;
        return l;
    }
}
