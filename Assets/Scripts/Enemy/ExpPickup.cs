using UnityEngine;
using System.Collections.Generic;

public class ExpPickup : MonoBehaviour
{
    [Header("Settings")]
    public int xpValue = 1;
    public float flySpeed = 3f;
    public float collectDistance = 0.5f;
    public float maxChaseTime = 5f;   // nach so vielen Sekunden auto-einsammeln

    [Tooltip("Bilder pro Sekunde der Idle-Schleife.")]
    public float idleFps = 8f;

    /// <summary>
    /// Ab so viel XP wird das Bonbon mittel bzw. gross (gleich wie SpawnExp).
    /// Passend zu den Gegnern: Grundgegner (1-9 XP) klein, zaehe Brocken
    /// (Eichel, Golem, Honey ...) mittel, Elites und Minibosse gross.
    /// </summary>
    public const int MediumFrom = 10, BigFrom = 100;

    /// <summary>
    /// Zusammenlegen: Bonbons, die schon eine Weile liegen und nicht direkt
    /// beim Spieler sind, werden alle paar Augenblicke je Rasterfeld zu einem
    /// zusammengefasst (es wird dann ggf. mittel/gross). Frisch gefallene
    /// Haufen bleiben also erst mal sichtbar liegen - erst wenn man weiterzieht,
    /// schrumpft die Zahl der Objekte. Liegen schon viele auf der Map, werden
    /// die Felder groesser.
    /// </summary>
    private const float MergeAfter = 3f, MergeInterval = 0.5f, MergeMinPlayerDistance = 6f;
    private const float MergeCell = 1.5f, CrowdedMergeCell = 3f;
    private const int CrowdedFrom = 150;

    /// <summary>
    /// Steht der Spieler still, sammelt sich um ihn ein Ring aus Bonbons
    /// knapp ausserhalb der Sammelreichweite. Liegen dort genug, werden sie
    /// nach Richtung (Tortenstuecke um den Spieler) zu einer Handvoll
    /// grosser zusammengelegt.
    /// </summary>
    private const int NearMergeFrom = 12, NearSectors = 6;

    /// <summary>
    /// Magnet/Boss: nur die naechsten Bonbons im Umkreis fliegen sichtbar zum
    /// Spieler, alle anderen werden sofort in einem Rutsch gutgeschrieben -
    /// die haetten ihn vor dem Auto-Einsammeln sowieso nicht erreicht.
    /// </summary>
    private const float AttractFlyRange = 12f;
    private const int AttractMaxFlying = 40;

    private bool movingToPlayer = false;
    private Transform playerTransform;
    private float chaseTimer = 0f;

    private string look;
    private SpriteRenderer spriteRenderer;
    private Sprite[] idleFrames;
    private float idleTime;

    // Globale Liste aller Candies im Spiel
    private static readonly HashSet<ExpPickup> allCandies = new HashSet<ExpPickup>();
    private static bool attractTriggered = false;

    // Bonbons kommen aus dem RunPool und werden wiederverwendet: alles, was
    // ein Bonbon waehrend seines Lebens aendert, wird beim Aktivieren
    // zurueckgesetzt, und eingerichtet wird erst im ersten Update - dann hat
    // SpawnExp xpValue und Aussehen schon gesetzt.
    private bool initialized;
    private SpriteRenderer[] renderers;
    private Color[] baseColors;
    private Collider2D pickupCollider;
    private int shownFrame = -1;
    private float bornAt;

    /// <summary>Glueckstreffer (goldenes Bonbon, "x2") - wird nie zusammengelegt.</summary>
    [HideInInspector] public bool lucky;

    private void Awake()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) baseColors[i] = renderers[i].color;
        pickupCollider = GetComponent<Collider2D>();
    }

    private void OnEnable()
    {
        allCandies.Add(this);
        movingToPlayer = false;
        playerTransform = null;
        chaseTimer = 0f;
        initialized = false;
        bornAt = Time.time;
        if (pickupCollider != null) pickupCollider.enabled = true;
    }

    private void OnDisable()
    {
        allCandies.Remove(this);
        look = null;
        lucky = false;

        // Der Glueckstreffer faerbt golden ein - das gilt nur fuer dieses Leben.
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].color = baseColors[i];
    }

    /// <summary>
    /// Bildstreifen aus Resources/PickUps waehlen (candy_small/medium/big/lucky).
    /// Ohne Aufruf entscheidet der XP-Wert. Gibt false zurueck, wenn es den
    /// Streifen nicht gibt - dann bleibt das Prefab-Sprite.
    /// </summary>
    public bool SetLook(string key)
    {
        look = key;
        return PickUps.LoadFrames(key) != null;
    }

    public static string LookFor(int xp) =>
        xp >= BigFrom ? "candy_big" : xp >= MediumFrom ? "candy_medium" : "candy_small";

    private void Initialize()
    {
        initialized = true;
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        idleFrames = PickUps.LoadFrames(look ?? LookFor(xpValue));
        shownFrame = -1;
        if (idleFrames != null)
            idleTime = Random.Range(0, idleFrames.Length) / Mathf.Max(0.01f, idleFps);
        AnimateIdle();
    }

    private void AnimateIdle()
    {
        if (idleFrames == null || spriteRenderer == null) return;
        int frame = Mathf.FloorToInt(idleTime * idleFps) % idleFrames.Length;
        if (frame == shownFrame) return;
        shownFrame = frame;
        spriteRenderer.sprite = idleFrames[frame];
    }

    private static float nextMergeAt;
    private static readonly Dictionary<Vector2Int, ExpPickup> mergeCells = new Dictionary<Vector2Int, ExpPickup>();
    private static readonly List<ExpPickup> mergedAway = new List<ExpPickup>();

    /// <summary>
    /// Ein Durchgang ueber alle Bonbons (ein Rasterfeld je Bonbon, kein
    /// Paarvergleich): je Feld bekommt das erste die XP der anderen, die
    /// anderen gehen zurueck in den Pool.
    /// </summary>
    private static void MergeRestingCandies(PlayerController pc)
    {
        float cell = allCandies.Count >= CrowdedFrom ? CrowdedMergeCell : MergeCell;
        float minAge = Time.time - MergeAfter;
        float minDistSqr = MergeMinPlayerDistance * MergeMinPlayerDistance;
        Vector2 player = pc != null ? (Vector2)pc.transform.position : new Vector2(float.MaxValue, float.MaxValue);

        mergeCells.Clear();
        mergedAway.Clear();
        nearCandies.Clear();

        foreach (ExpPickup candy in allCandies)
        {
            if (candy.movingToPlayer || candy.lucky || candy.look != null || candy.bornAt > minAge) continue;

            Vector2 pos = candy.transform.position;
            if ((pos - player).sqrMagnitude < minDistSqr)
            {
                nearCandies.Add(candy);
                continue;
            }

            var key = new Vector2Int(Mathf.FloorToInt(pos.x / cell), Mathf.FloorToInt(pos.y / cell));
            if (mergeCells.TryGetValue(key, out ExpPickup target))
            {
                target.xpValue += candy.xpValue;
                mergedAway.Add(candy);
            }
            else
            {
                mergeCells[key] = candy;
            }
        }

        MergeNearCandies(player);
        nearCandies.Clear();

        if (mergedAway.Count == 0) return;

        // Erst nach der Schleife zurueckgeben - OnDisable aendert allCandies.
        foreach (ExpPickup candy in mergedAway) RunPool.Release(candy.gameObject);
        mergedAway.Clear();

        foreach (ExpPickup target in mergeCells.Values) target.RefreshLook();
        foreach (ExpPickup target in nearTargets)
            if (target != null) target.RefreshLook();
        System.Array.Clear(nearTargets, 0, NearSectors);
        mergeCells.Clear();
    }

    private static readonly List<ExpPickup> nearCandies = new List<ExpPickup>();
    private static readonly ExpPickup[] nearTargets = new ExpPickup[NearSectors];

    /// <summary>
    /// Ring um den Spieler (siehe <see cref="NearMergeFrom"/>): je Richtung
    /// bekommt das erste Bonbon die XP der anderen.
    /// </summary>
    private static void MergeNearCandies(Vector2 player)
    {
        System.Array.Clear(nearTargets, 0, NearSectors);
        if (nearCandies.Count < NearMergeFrom) return;

        float sectorSize = 360f / NearSectors;
        foreach (ExpPickup candy in nearCandies)
        {
            Vector2 offset = (Vector2)candy.transform.position - player;
            float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg + 360f;
            int sector = Mathf.FloorToInt(angle / sectorSize) % NearSectors;

            ExpPickup target = nearTargets[sector];
            if (target == null)
            {
                nearTargets[sector] = candy;
            }
            else
            {
                target.xpValue += candy.xpValue;
                mergedAway.Add(candy);
            }
        }
    }

    private void RefreshLook()
    {
        if (!initialized) return; // Initialize waehlt das Aussehen selbst
        idleFrames = PickUps.LoadFrames(LookFor(xpValue));
        shownFrame = -1;
        AnimateIdle();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Spieler ist in Sammelreichweite → Candy fängt an zu folgen
        if (other.CompareTag("Player"))
        {
            StartChasingPlayer();
        }
    }

    private void Update()
    {
        if (!initialized) Initialize();

        idleTime += Time.deltaTime;
        AnimateIdle();

        var pc = PlayerController.Instance;

        // Magnet (attractAllXP) aktiviert → alle Candies fliegen los
        if (pc != null && pc.attractAllXP && !attractTriggered)
        {
            attractTriggered = true;
            AttractAllCandies(pc);
            if (!gameObject.activeSelf) return; // selbst gleich gutgeschrieben
        }

        // Das erste Bonbon, das zur Zeit dran ist, fasst fuer alle zusammen.
        if (Time.time >= nextMergeAt)
        {
            nextMergeAt = Time.time + MergeInterval;
            MergeRestingCandies(pc);
            if (!gameObject.activeSelf) return; // selbst in ein anderes gewandert
        }
        else if (pc != null && !pc.attractAllXP && attractTriggered)
        {
            attractTriggered = false;
        }

        // Wenn Candy gerade folgt
        if (movingToPlayer && playerTransform != null)
        {
            chaseTimer += Time.deltaTime;

            // Richtung Spieler bewegen
            transform.position = Vector3.MoveTowards(
                transform.position,
                playerTransform.position,
                flySpeed * Time.deltaTime
            );

            // Wenn Spieler erreicht → einsammeln
            if (Vector3.Distance(transform.position, playerTransform.position) <= collectDistance)
            {
                CollectAndDestroy();
                return;
            }

            // Wenn zu lange unterwegs → automatisch einsammeln
            if (chaseTimer >= maxChaseTime)
            {
                CollectAndDestroy();
                return;
            }
        }
    }

    private void StartChasingPlayer()
    {
        if (movingToPlayer) return;

        movingToPlayer = true;
        chaseTimer = 0f;

        // Ab jetzt zaehlt nur noch der Abstand. Ein Collider ohne Rigidbody,
        // der jeden Frame verschoben wird, baut die Physik jedes Mal neu
        // auf - bei Hunderten fliegender Bonbons der groesste Brocken.
        if (pickupCollider != null) pickupCollider.enabled = false;

        var pc = PlayerController.Instance;
        if (pc != null)
            playerTransform = pc.transform;
    }

    private static readonly List<ExpPickup> attractBuffer = new List<ExpPickup>();

    private static void AttractAllCandies(PlayerController pc)
    {
        // Kopie: Gutschreiben gibt Bonbons in den Pool zurueck, und OnDisable
        // aendert allCandies.
        Vector2 center = pc.transform.position;
        attractBuffer.Clear();
        foreach (var candy in allCandies)
            if (candy != null && !candy.movingToPlayer) attractBuffer.Add(candy);

        attractBuffer.Sort((a, b) =>
            ((Vector2)a.transform.position - center).sqrMagnitude
                .CompareTo(((Vector2)b.transform.position - center).sqrMagnitude));

        float flyRangeSqr = AttractFlyRange * AttractFlyRange;
        int flying = 0;
        long instantXp = 0;

        foreach (var candy in attractBuffer)
        {
            bool inRange = ((Vector2)candy.transform.position - center).sqrMagnitude <= flyRangeSqr;
            if (inRange && flying < AttractMaxFlying)
            {
                candy.StartChasingPlayer();
                flying++;
            }
            else
            {
                instantXp += candy.xpValue;
                RunPool.Release(candy.gameObject);
            }
        }
        attractBuffer.Clear();

        if (instantXp > 0)
            pc.GetExperience((int)System.Math.Min(instantXp, int.MaxValue));
    }

    private void CollectAndDestroy()
    {
        var pc = PlayerController.Instance;
        if (pc != null)
            pc.GetExperience(xpValue);

        // Zurueck in den Pool (siehe SpawnExp) - aus dem Pool stammt fast jedes Bonbon.
        RunPool.Release(gameObject);
    }
}
