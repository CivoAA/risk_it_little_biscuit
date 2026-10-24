using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Opferschrein auf der Karte: ein Altar, daneben eine finstere Ausgabe des
/// Haendlers. Wer im Feld steht, laedt ihn auf - doppelt so lange wie einen
/// Mixer (<see cref="ChargeFactor"/>). Ist er voll, steht das Spiel und der
/// <see cref="LevelUpScreen"/> zeigt die Opfer-Ansicht (LevelUpScreen.Shrine.cs).
/// Was ein Opfer kostet, steht in <see cref="Sacrifice"/>.
///
/// Nach dem Opfer verschwindet der Haendler, der Altar wird dunkel, und das
/// geopferte Teil schwebt wippend ueber ihm.
///
/// Gespawnt wird er von den Zufalls-Spawnern neben den Mixern: jedes Mal,
/// wenn dort ein Mixer entsteht, kommt mit <see cref="SpawnChance"/> ein
/// Schrein im selben Block dazu (<see cref="MaybeSpawn"/>). Er haelt dieselben
/// Abstaende wie ein Mixer (<see cref="MixerPlacement"/>).
///
/// Baut sich komplett per Code auf. Altar und Haendler kommen vorerst aus
/// Resources/Shrine (Kopien von Altar und Rico aus dem Hub), den Ring am
/// Boden malt <see cref="FieldRing"/>. Im Koop gibt es (noch) keinen Schrein.
/// </summary>
[DisallowMultipleComponent]
public class SacrificeShrine : MonoBehaviour
{
    /// <summary>Chance je gespawntem Mixer, dass ein Schrein dazukommt (1 zu 10).</summary>
    public const float SpawnChance = 0.1f;

    /// <summary>So viel laenger als ein Mixer muss man im Feld stehen.</summary>
    public const float ChargeFactor = 2f;

    private const float FallbackMixerSize = 3.8f;   // MixerObject.normalSize im Mixer-Prefab
    private const float FallbackRadius = 4.25f;     // Feld des Mixer-Prefabs
    private const float DrainDelay = 0.75f;         // wie MixerObject.RechargeSprite
    private const float DrainTime = 2f;
    private const string SortingLayer = "Objects";

    // Das geopferte Teil ueber dem Altar
    private const float OfferingHeight = 1.55f;     // Mitte ueber dem Altar-Pivot (Altar ist 2 Einheiten hoch)
    private const float OfferingSize = 0.8f;        // so hoch wird das Symbol gezeichnet
    private const float OfferingBob = 0.08f;
    private const float OfferingBobSpeed = 2.2f;

    private static readonly List<SacrificeShrine> all = new List<SacrificeShrine>();

    /// <summary>Alle Schreine auf der Karte - die Spawner halten zu ihnen Abstand.</summary>
    public static IReadOnlyList<SacrificeShrine> All => all;

    /// <summary>Der Schrein, dessen Fenster gerade offen ist (null = keins).</summary>
    public static SacrificeShrine Current { get; private set; }

    public static bool PanelOpen => Current != null;

    /// <summary>Radius des Felds in Welt-Einheiten.</summary>
    public float Radius { get; private set; } = FallbackRadius;

    private float progress;
    private float lastInsideAt = -999f;
    private bool used;
    private bool mustLeave;   // nach Abbrechen erst raus, dann wieder laden

    private SpriteRenderer altar, keeper, field, offering;
    private Sprite[] keeperFrames;
    private FieldRing ring;
    private float usedAt;
    private bool faded;

    // ==================================================================
    //  Spawnen
    // ==================================================================

    /// <summary>
    /// Von den Zufalls-Spawnern direkt nach einem neuen Mixer: mit
    /// <see cref="SpawnChance"/> einen Schrein an eine freie Stelle desselben Blocks.
    /// </summary>
    /// <param name="randomSpot">Wuerfelt eine Stelle im Block.</param>
    /// <param name="allowed">Darf dort gespawnt werden (Kamera-Sperrzone)?</param>
    public static void MaybeSpawn(GameObject mixerPrefab, float gapInFields,
                                  System.Func<Vector2> randomSpot, System.Func<Vector2, bool> allowed)
    {
        // Koop: die Welt waere nicht bei allen gleich - dort gibt es keinen Schrein.
        if (NetWorld.Deterministic || NetRun.Active != null) return;
        if (mixerPrefab == null || randomSpot == null) return;
        if (Random.value >= SpawnChance) return;

        for (int a = 0; a < MixerPlacement.Attempts; a++)
        {
            Vector2 spot = randomSpot();
            if (allowed != null && !allowed(spot)) continue;
            if (!MixerPlacement.IsFree(mixerPrefab, spot, gapInFields)) continue;

            Spawn(spot, MixerPlacement.FieldRadius(mixerPrefab));
            return;
        }
    }

    public static SacrificeShrine Spawn(Vector2 position, float radius)
    {
        var go = new GameObject("SacrificeShrine");
        go.transform.position = new Vector3(position.x, position.y, 0f);
        SacrificeShrine shrine = go.AddComponent<SacrificeShrine>();
        shrine.Radius = radius > 0f ? radius : FallbackRadius;
        shrine.Build();
        RunScene.Place(go, "Mixer");
        return shrine;
    }

    // ==================================================================
    //  Aufbau
    // ==================================================================

    private void Awake() => all.Add(this);

    private void OnDestroy()
    {
        all.Remove(this);
        if (Current == this) Current = null;
        ring?.Destroy();
    }

    private void Build()
    {
        Sprite altarSprite = Resources.Load<Sprite>("Shrine/shrine_altar");
        keeperFrames = Resources.LoadAll<Sprite>("Shrine/shrine_keeper");
        System.Array.Sort(keeperFrames, (x, y) => string.CompareOrdinal(x.name, y.name));

        field = NewPart("Field", Vector3.zero, -2);
        ring = new FieldRing(field, Radius);

        altar = NewPart("Altar", Vector3.zero, 0);
        altar.sprite = altarSprite;

        // Der Haendler steht rechts neben dem Altar - dunkel und etwas rot.
        keeper = NewPart("Keeper", new Vector3(1.6f, 0.2f, 0f), 0);
        keeper.sprite = keeperFrames.Length > 0 ? keeperFrames[0] : null;
        keeper.color = new Color(0.55f, 0.32f, 0.42f);
    }

    private SpriteRenderer NewPart(string name, Vector3 local, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = local;
        SpriteRenderer r = go.AddComponent<SpriteRenderer>();
        r.sortingLayerName = SortingLayer;
        r.sortingOrder = order;
        return r;
    }

    // ==================================================================
    //  Laufzeit
    // ==================================================================

    private void Update()
    {
        float now = Time.time;

        if (keeper != null && keeper.enabled && keeperFrames != null && keeperFrames.Length > 1)
            keeper.sprite = keeperFrames[Mathf.FloorToInt(Time.unscaledTime * 2f) % keeperFrames.Length];

        if (used)
        {
            UpdateUsed();
            return;
        }

        bool inside = PlayerInside();
        if (!inside) mustLeave = false;

        if (inside && !mustLeave && Current == null && Time.timeScale > 0f)
        {
            lastInsideAt = now;
            progress += Time.deltaTime / ChargeSeconds();
            if (progress >= 1f)
            {
                progress = 1f;
                Open();
            }
        }
        else if (!inside && now - lastInsideAt > DrainDelay)
        {
            progress = Mathf.Max(0f, progress - Time.deltaTime / DrainTime);
        }

        ring.Paint(progress);
    }

    private bool PlayerInside()
    {
        PlayerController p = PlayerController.Instance;
        if (p == null || p.NetGhost) return false;
        return ((Vector2)p.transform.position - (Vector2)transform.position).sqrMagnitude <= Radius * Radius;
    }

    /// <summary>Doppelt so lange wie ein Mixer - mit dessen Groesse und dem Schrumpftempo des Spielers.</summary>
    private static float ChargeSeconds()
    {
        float size = FallbackMixerSize;
        var mixers = MixerObject.All;
        for (int i = 0; i < mixers.Count; i++)
            if (mixers[i] != null) { size = mixers[i].normalSize; break; }

        PlayerController p = PlayerController.Instance;
        float speed = p != null && p.powerUpShrinkSpeed > 0.01f ? p.powerUpShrinkSpeed : 1f;
        return ChargeFactor * size / speed;
    }

    private void Open()
    {
        Current = this;
        Time.timeScale = 0f;
        if (AudioController.Instance != null)
            AudioController.Instance.PalySound(AudioController.Instance.MenuClick);
    }

    /// <summary>
    /// Fenster zu. Nach einem Opfer ist der Schrein verbraucht und traegt das
    /// Symbol des geopferten Teils (<paramref name="offeringIcon"/>), sonst
    /// laedt er wieder von vorn, sobald der Spieler einmal raus und wieder rein ist.
    /// </summary>
    public static void Close(bool sacrificed, Sprite offeringIcon = null)
    {
        SacrificeShrine shrine = Current;
        Current = null;

        if (shrine != null)
        {
            if (sacrificed)
            {
                shrine.used = true;
                shrine.usedAt = Time.unscaledTime;
                shrine.ShowOffering(offeringIcon);
            }
            else
            {
                shrine.progress = 0f;
                shrine.mustLeave = true;
            }
        }

        // Wie UIController.PowerUpPanelClose: ein anderes offenes Fenster haelt das Spiel an.
        UIController ui = UIController.Instance;
        bool otherOpen = ui != null && ((ui.LevelUpPanel != null && ui.LevelUpPanel.activeSelf)
                                     || (ui.PowerUpPanel != null && ui.PowerUpPanel.activeSelf)
                                     || (ui.EvoPanel != null && ui.EvoPanel.activeSelf));
        if (!otherOpen && !PauseMenuPanel.IsOpen) Time.timeScale = 1f;
    }

    private void ShowOffering(Sprite icon)
    {
        if (icon == null) return;
        offering = NewPart("Offering", new Vector3(0f, OfferingHeight, 0f), 1);
        offering.sprite = icon;
        // Weapon-Symbole haben verschiedene PPU - auf eine feste Hoehe bringen.
        float h = icon.bounds.size.y;
        float scale = h > 0.0001f ? OfferingSize / h : 1f;
        offering.transform.localScale = new Vector3(scale, scale, 1f);
        offering.color = new Color(1f, 1f, 1f, 0f);
    }

    private void UpdateUsed()
    {
        if (field.enabled) field.enabled = false;

        float t = Mathf.Clamp01((Time.unscaledTime - usedAt) / 0.6f);
        if (!faded)
        {
            // Der Haendler hat, was er wollte, und verschwindet - der Altar wird dunkel.
            if (keeper != null)
            {
                Color c = keeper.color;
                c.a = Mathf.Round((1f - t) * 4f) / 4f;
                keeper.color = c;
                if (t >= 1f) keeper.enabled = false;
            }
            if (altar != null) altar.color = Color.Lerp(Color.white, new Color(0.45f, 0.4f, 0.45f), t);
            faded = t >= 1f;
        }

        // Das Opfer schwebt ueber dem Altar und wippt sacht auf und ab - fuer immer.
        if (offering != null)
        {
            float bob = Mathf.Sin(Time.time * OfferingBobSpeed) * OfferingBob;
            offering.transform.localPosition = new Vector3(0f, OfferingHeight + bob, 0f);
            offering.color = new Color(1f, 1f, 1f, Mathf.Round(t * 4f) / 4f);
        }
        else if (faded)
        {
            enabled = false;
        }
    }
}
