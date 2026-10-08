using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// Ein Gegner im Lauf: laeuft auf den Spieler zu, nimmt Schaden, stirbt und
/// laesst dabei fallen, was seine Rolle hergibt.
///
/// WAS SICH BEIM REMASTER GEAENDERT HAT
///
/// Die Werte (Leben, Schaden, Tempo, Erfahrung, Gewicht) kommen jetzt aus dem
/// <see cref="EnemyCatalog"/> und nicht mehr aus dem Prefab. Am Prefab steht
/// nur noch die <see cref="EnemyId"/> - wer der Gegner IST. Balancing ist damit
/// eine Datei statt 25, und ein neu gebautes Prefab bringt kein leeres Blatt
/// mehr mit. Dieselbe Linie wie bei den Erfolgen und den Wellenplaenen.
///
/// Alte Prefabs, an denen noch keine Id steht, laufen unveraendert weiter: ist
/// <see cref="id"/> gleich <see cref="EnemyId.None"/>, gelten die alten
/// Inspector-Felder. Deshalb stehen sie noch hier. Sie verschwinden erst, wenn
/// das letzte Prefab in der Werkstatt umgestellt ist (Tools -> Gegner).
///
/// Die Oberflaeche nach aussen ist absichtlich dieselbe geblieben:
/// <see cref="TakeDamage"/>, <see cref="IsBoss"/>, <see cref="HealthFraction"/>,
/// <see cref="ApplyRunScaling"/>, <see cref="ApplyPull"/>, <see cref="Alive"/>.
/// An diesen sechs Sachen haengt der Rest des Spiels - rund vierzig Waffen, der
/// SpawnDirector, der Keks-Koenig und die Test-Szene. Nichts davon musste
/// angefasst werden.
/// </summary>
public class Enemy : MonoBehaviour
{
    // ---------------------------------------------------------------- Wer ich bin

    [Header("Wer bin ich")]
    [Tooltip("Gesetzt = alle Werte kommen aus dem EnemyCatalog. " +
             "None = die alten Felder weiter unten gelten (Altbestand).")]
    [SerializeField] private EnemyId id = EnemyId.None;

    /// <summary>Die Id dieses Gegners. None heisst: noch nicht umgestellt.</summary>
    public EnemyId Id => id;

    // ------------------------------------------------------------- Bausteine

    [Header("Bausteine")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private GameObject destroyEffect;

    // ------------------------------------------------- Altbestand (id == None)

    [Header("Alte Werte - gelten nur, solange oben None steht")]
    [SerializeField] private float moveSpeed;
    [SerializeField] private float damage;
    [SerializeField] private float health;
    [SerializeField] private int experienceToGive;
    [SerializeField] private float pushTime;
    [SerializeField] private bool rightlooking = true;
    [SerializeField] private bool MiniBoss = false;
    [SerializeField] private bool BossBoss = false;
    [SerializeField] private bool Death_Boss = false;

    // ---------------------------------------------------------------- Zustand

    /// <summary>Die Rolle, aus Katalog oder aus den alten Schaltern abgeleitet.</summary>
    private EnemyRole role = EnemyRole.Normal;

    /// <summary>Wohin das Bild schaut, aus Katalog oder aus "rightlooking".</summary>
    private EnemyFacing facing = EnemyFacing.Neutral;

    /// <summary>
    /// Das ungebremste Lauftempo. Bleibt unangetastet - Slow und Rueckstoss
    /// rechnen sich als Faktor beziehungsweise Vorzeichen OBEN DRAUF.
    ///
    /// Frueher hat beides direkt in moveSpeed geschrieben: der Rueckstoss hat
    /// das Vorzeichen gedreht, der Slow den Betrag ersetzt. Traf beides
    /// zusammen, blieb der Gegner hinterher dauerhaft rueckwaerts oder
    /// dauerhaft langsam stehen, je nachdem was zuerst auslief.
    /// </summary>
    private float baseSpeed;

    private float slowFactor = 1f;
    private float slowUntil;

    private float pushCounter;

    /// <summary>
    /// Globaler Daempfer auf den Rueckstoss: ein Treffer schiebt nur noch so
    /// viel von pushTime zurueck wie frueher.
    /// </summary>
    private const float KnockbackScale = 0.4f;

    /// <summary>
    /// Nach einem Rueckstoss so lange (Sekunden) kein neuer. Frueher hat jeder
    /// Treffer den Rueckstoss neu gestartet - wer mit einer tickenden
    /// Flaechenwaffe draufhielt, hat den Gegner dauerhaft auf Abstand gehalten.
    /// </summary>
    private const float KnockbackImmunity = 0.6f;

    private float nextPushAllowed;

    private Vector3 direction;

    /// <summary>Gesetzt bei Gegnern, die huepfen statt laufen (Kirschslime).</summary>
    private HopMovement hop;

    // Zug von aussen (Wirbel). Wird als Geschwindigkeit auf die normale
    // Laufbewegung addiert und laeuft nach kurzer Zeit von selbst aus, damit
    // ein zerstoerter Wirbel keinen Gegner dauerhaft mitzieht.
    private Vector2 externalVelocity;
    private float externalVelocityTimer;

    /// <summary>
    /// Leben beim Spawn, nach der Lauf-Skalierung. Bezugspunkt fuer
    /// <see cref="HealthFraction"/>.
    /// </summary>
    private float maxHealth;

    // ------------------------------------------------------------- Oberflaeche

    /// <summary>
    /// Aktuelle Laufgeschwindigkeit. Grundlage fuer den Vorhalt der Schuetzen,
    /// siehe <see cref="Aim.PredictDirection"/>.
    /// </summary>
    public Vector2 Velocity
    {
        get { return rb != null ? rb.linearVelocity : Vector2.zero; }
    }

    /// <summary>
    /// Bosse und Minibosse lassen sich nicht ziehen oder wegschieben. Elites
    /// zaehlen nicht dazu - die sind nur starke Einzelgaenger.
    /// </summary>
    public bool IsBoss => EnemyCatalog.IsBossRole(role);

    public bool IsElite => role == EnemyRole.Elite;

    /// <summary>Beruehrungsschaden nach Lauf-Skalierung. Fuer Attacken, die ein Vielfaches davon machen.</summary>
    public float ContactDamage => damage;

    /// <summary>Lauftempo gerade jetzt (Katalog, Lauf-Skalierung, Slow). Fuer Gegner, die sich selbst steuern.</summary>
    public float CurrentSpeed => baseSpeed * slowFactor;

    /// <summary>Die Rolle dieses Gegners. Der Director und die Erfolge fragen danach.</summary>
    public EnemyRole Role => role;

    /// <summary>
    /// Wie viel Leben noch steht, 1 = voll. Der Keks-Koenig haengt daran
    /// seinen Phasenwechsel auf. Ohne das muesste jeder Boss sein Leben ein
    /// zweites Mal selbst mitzaehlen, und die beiden Staende liefen
    /// auseinander, sobald irgendwo anders Schaden dazukommt.
    /// </summary>
    public float HealthFraction
    {
        get
        {
            // Fragt jemand schon vor Start (Start-Reihenfolge ist nicht
            // garantiert), gilt der aktuelle Stand als voll.
            if (maxHealth <= 0f) maxHealth = health;
            return maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 1f;
        }
    }

    /// <summary>
    /// Setzt das Leben auf einen Anteil des Startwerts. Nur fuer die
    /// Test-Szene: damit laesst sich die zweite Phase eines Bosses anspringen,
    /// ohne ihn vorher von Hand halb totzuschlagen. Toetet nie - das soll der
    /// Spieler schon selbst machen.
    /// </summary>
    public void DebugSetHealthFraction(float fraction)
    {
        if (maxHealth <= 0f) maxHealth = health;
        health = Mathf.Max(1f, maxHealth * Mathf.Clamp01(fraction));
    }

    /// <summary>
    /// Setzt das Leben auf einen festen Wert (mindestens 1). Fuer die
    /// Testtaste "Verkohlter auf 10 HP" - der Bezugspunkt fuer
    /// <see cref="HealthFraction"/> bleibt das volle Leben.
    /// </summary>
    public void DebugSetHealth(float hp)
    {
        if (maxHealth <= 0f) maxHealth = health;
        health = Mathf.Max(1f, hp);
    }

    /// <summary>Volles Leben (nach Lauf-Skalierung) - Bezugspunkt von <see cref="HealthFraction"/>.</summary>
    public float MaxHealth
    {
        get
        {
            if (maxHealth <= 0f) maxHealth = health;
            return maxHealth;
        }
    }

    /// <summary>
    /// Neuer Lebenspool fuer eine eigene Phase (das Herz des Verkohlten): volles
    /// Leben = <paramref name="newMax"/>, aktuell ein Anteil davon. Die
    /// Boss-Leiste zeigt ab dann diesen Pool.
    /// </summary>
    public void ResetHealthPool(float newMax, float fraction)
    {
        maxHealth = Mathf.Max(1f, newMax);
        health = Mathf.Max(1f, maxHealth * Mathf.Clamp01(fraction));
    }

    // ---------------------------------------------------------------- Register

    private static readonly List<Enemy> alive = new List<Enemy>();

    /// <summary>
    /// Alle Gegner, die gerade leben. Der <see cref="SpawnDirector"/> rechnet
    /// daraus den Druck auf dem Feld aus - und zwar auch ueber Gegner, die er
    /// nicht selbst gesetzt hat (Mini-Muffins aus einem Muffin zum Beispiel).
    /// Ohne die wuerde er nachlegen, obwohl es schon voll ist.
    /// </summary>
    public static IReadOnlyList<Enemy> Alive => alive;

    /// <summary>
    /// Was dieser Gegner im Druck-Budget wiegt. Setzt der Director beim
    /// Spawnen; wer anders erzeugt wird, zaehlt als 1.
    /// </summary>
    [System.NonSerialized] public float RunThreat = 1f;

    /// <summary>
    /// Darf der Director diesen Gegner nachziehen, wenn der Spieler wegrennt?
    /// Fuer Kaefig und Ring-Gegner eines Encirclements: nein - die gehoeren an
    /// ihren Platz.
    /// </summary>
    [System.NonSerialized] public bool CanRecycle;

    /// <summary>
    /// Als was der Director diesen Gegner gesetzt hat. Bei umgestellten Prefabs
    /// dasselbe wie <see cref="Id"/>; beim Altbestand (Keks-Koenig steht noch
    /// auf None) die einzige Stelle, an der man erfaehrt, WER er ist.
    /// </summary>
    [System.NonSerialized] public EnemyId SpawnedAs = EnemyId.None;

    /// <summary>
    /// Steuert sich selbst (eigenes Skript setzt die Geschwindigkeit). Dann
    /// schiebt <see cref="FixedUpdate"/> nicht mehr Richtung Spieler - sonst
    /// liefe ein Huepfer am Boden weiter oder ein Springer in der Luft.
    /// Bosse mit Rolle Boss steuern sich ohnehin selbst.
    /// </summary>
    [System.NonSerialized] public bool SelfSteered;

    /// <summary>
    /// Gerade nicht zu treffen (z.B. hoch in der Luft ueber dem Bildrand).
    /// <see cref="TakeDamage"/> ignoriert dann alles.
    /// </summary>
    [System.NonSerialized] public bool Untouchable;

    /// <summary>
    /// Unter diesen Anteil des vollen Lebens faellt der Gegner nicht - er
    /// stirbt also auch nicht. 0 = aus. Der Verkohlte haelt sich so am Leben,
    /// bis er den Spieler eingesaugt hat (Phase 3); stand er vorher schon
    /// darunter (Testtaste), bleibt er beim alten Stand stehen.
    /// </summary>
    [System.NonSerialized] public float MinHealthFraction;

    /// <summary>
    /// Toedlicher Treffer stirbt nicht sofort: der Gegner wird unberuehrbar,
    /// meldet <see cref="DeathHeld"/> und wartet, bis sein Skript
    /// <see cref="FinishHeldDeath"/> ruft (das Herz des Verkohlten zerfaellt
    /// erst, dann gibt es Beute und Sieg).
    /// </summary>
    [System.NonSerialized] public bool HoldDeath;

    /// <summary>Kommt einmal, wenn ein Gegner mit <see cref="HoldDeath"/> toedlich getroffen wurde.</summary>
    public event System.Action DeathHeld;

    /// <summary>Beim Tod keinen Todeseffekt des Prefabs abspielen (das Skript zeigt einen eigenen).</summary>
    [System.NonSerialized] public bool SkipDeathEffect;

    private bool deathHeld;

    /// <summary>Wer der Gegner ist: Katalog-Id, sonst wie der Director ihn gesetzt hat.</summary>
    public EnemyId Identity => id != EnemyId.None ? id : SpawnedAs;

    // -------------------------------------------------------------- Aufbau

    /// <summary>
    /// Holt die Werte aus dem Katalog - und zwar in Awake, nicht in Start.
    ///
    /// Das ist keine Kosmetik: der <see cref="SpawnDirector"/> ruft direkt
    /// nach dem Instantiate <see cref="ApplyRunScaling"/> auf, und das laeuft
    /// noch vor dem ersten Start. Kaemen die Katalogwerte erst in Start, wuerde
    /// die Skalierung ueberschrieben und jeder Gegner stuende mit
    /// Grundschwierigkeit auf dem Feld.
    /// </summary>
    protected virtual void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        hop = GetComponent<HopMovement>();

        EnemyDef def = EnemyCatalog.Get(id);
        if (def != null)
        {
            health           = def.Health;
            damage           = def.Damage;
            moveSpeed        = def.Speed;
            experienceToGive = def.Exp;
            pushTime         = def.PushTime;
            role             = def.Role;
            facing           = def.Facing;
            RunThreat        = def.Threat;
        }
        else
        {
            // Altbestand: Rolle und Blickrichtung aus den alten Schaltern.
            // "rightlooking == true" hiess frueher "flipX, wenn der Spieler
            // rechts steht" - das Bild schaut also nach links.
            role   = LegacyRole();
            facing = rightlooking ? EnemyFacing.ArtFacesLeft : EnemyFacing.Neutral;
        }

        // Skilltree "Schwachstellen kennen": Bosse und Minibosse mit weniger
        // Leben. Steht vor der Lauf-Skalierung, die rechnet dann darauf weiter.
        PlayerController player = PlayerController.Instance;
        if (player != null && player.bossHealthReduction > 0f && EnemyCatalog.IsBossRole(role))
        {
            health = Mathf.Max(1f, health * (1f - Mathf.Clamp01(player.bossHealthReduction / 100f)));
        }

        baseSpeed = moveSpeed;
        maxHealth = health;

        // Elites erkennt man am Leuchten.
        if (role == EnemyRole.Elite && spriteRenderer != null) EliteGlow.Attach(spriteRenderer);
    }

    /// <summary>Altbestand: der Schalter "MiniBoss" am Prefab meint heute Elite.</summary>
    private EnemyRole LegacyRole()
    {
        if (Death_Boss) return EnemyRole.DeathBoss;
        if (BossBoss) return EnemyRole.Boss;
        if (MiniBoss) return EnemyRole.Elite;
        return EnemyRole.Normal;
    }

    protected virtual void OnEnable()
    {
        alive.Add(this);
        RegisterCollider();
    }

    protected virtual void OnDisable()
    {
        alive.Remove(this);
        UnregisterCollider();
        if (IsFrozen) Unfreeze();
    }

    protected virtual void Start()
    {
        // Wer ohne Director gesetzt wird (Test-Szene, alte Aufbauten), laeuft
        // nie durch ApplyRunScaling - dann gilt der Katalog- bzw. Prefab-Wert.
        if (maxHealth <= 0f) maxHealth = health;
        if (baseSpeed == 0f && moveSpeed != 0f) baseSpeed = moveSpeed;
    }

    /// <summary>
    /// Haengt die Lauf-Schwierigkeit an: mehr Leben, mehr Schaden, mehr
    /// Belohnung. Ruft <see cref="RunDifficulty"/> direkt nach dem Spawnen auf,
    /// also bevor der Gegner das erste Mal laeuft.
    /// </summary>
    public void ApplyRunScaling(float healthFactor, float damageFactor, float rewardFactor)
    {
        health = Mathf.Max(1f, health * healthFactor);
        damage *= damageFactor;
        experienceToGive = Mathf.Max(0, Mathf.RoundToInt(experienceToGive * rewardFactor));

        // Der Bezugspunkt muss NACH der Skalierung stehen, sonst waere ein
        // Boss in einem harten Lauf sofort unter 50% und die zweite Phase
        // liefe von Anfang an.
        maxHealth = health;
    }

    /// <summary>
    /// Zieht den Gegner fuer kurze Zeit in eine Richtung. Der Aufrufer muss das
    /// jeden Frame erneuern (der Wirbel tut das), sonst laeuft der Zug aus.
    /// </summary>
    public void ApplyPull(Vector2 velocity, float holdTime = 0.2f)
    {
        if (IsBoss) return;

        externalVelocity = velocity;
        externalVelocityTimer = holdTime;
    }

    // ------------------------------------------------------------- Bewegung

    protected virtual void FixedUpdate()
    {
        PlayerController player = PlayerController.Instance;
        if (player == null || !player.gameObject.activeSelf)
        {
            if (rb != null) rb.linearVelocity = Vector2.zero;
            return;
        }

        // Festgeklebt: kein Laufen, kein Umdrehen, kein Schieben.
        if (IsFrozen)
        {
            if (Time.time < frozenUntil) return;
            Unfreeze();
        }

        float dt = Time.fixedDeltaTime;

        FacePlayer(player);

        if (pushCounter > 0f) pushCounter -= dt;

        if (slowUntil > 0f && Time.time >= slowUntil)
        {
            slowFactor = 1f;
            slowUntil = 0f;
        }

        if (externalVelocityTimer > 0f)
        {
            externalVelocityTimer -= dt;
            if (externalVelocityTimer <= 0f) externalVelocity = Vector2.zero;
        }

        // Der Keks-Koenig steuert sich selbst (Sprint, Stehenbleiben zwischen
        // den Attacken). Wuerde hier zusaetzlich geschoben, liefe er waehrend
        // seiner eigenen Attacken weiter.
        if (role == EnemyRole.Boss || SelfSteered || rb == null) return;

        direction = PursuitDirection(player);
        UpdateBlocked(player, dt);

        // Rueckstoss dreht nur das Vorzeichen, Slow nur den Betrag. Beide
        // rechnen auf baseSpeed und koennen sich deshalb nicht gegenseitig
        // ueberschreiben.
        float speed = baseSpeed * slowFactor;
        if (pushCounter > 0f) speed = -speed;
        else if (hop != null) speed *= hop.SpeedFactor;   // Huepfer: nur in der Luft

        Vector2 chase = (Vector2)direction * speed;
        Vector2 push = ThrottledSeparation(ref chase);

        rb.linearVelocity = chase + externalVelocity + push * baseSpeed * SeparationStrength;
    }

    // ---------------------------------------------------------- Festkleben

    /// <summary>Faerbung waehrend des Festklebens (Multiplikator auf das Bild).</summary>
    private static readonly Color FrozenTint = new Color(0.78f, 0.84f, 0.95f, 1f);

    private float frozenUntil;
    private float recycleHoldUntil;
    private Animator[] frozenAnimators;
    private float[] frozenAnimatorSpeeds;
    private RigidbodyConstraints2D frozenConstraints;
    private Color frozenColor;

    /// <summary>Skilltree "Klebreis": steht still, dreht sich nicht, Animation steht.</summary>
    public bool IsFrozen => frozenUntil > 0f;

    /// <summary>
    /// Solange setzt der <see cref="SpawnDirector"/> den Gegner nicht um. Laeuft
    /// ueber das Festkleben hinaus - jeder Gegner mit eigenem Nachlauf, damit
    /// nach dem Aufloesen nicht alle auf einmal vor dem Spieler auftauchen.
    /// </summary>
    public bool RecycleHeld => Time.time < recycleHoldUntil;

    /// <summary>
    /// Klebt den Gegner fest. Bosse und Minibosse steuern sich selbst und
    /// bleiben frei.
    /// </summary>
    /// <param name="seconds">So lange steht er still.</param>
    /// <param name="recycleDelay">So lange nach dem Aufloesen wird er noch nicht umgesetzt.</param>
    public void Freeze(float seconds, float recycleDelay)
    {
        if (IsBoss) return;

        if (!IsFrozen)
        {
            frozenAnimators = GetComponentsInChildren<Animator>();
            frozenAnimatorSpeeds = new float[frozenAnimators.Length];
            for (int i = 0; i < frozenAnimators.Length; i++)
            {
                frozenAnimatorSpeeds[i] = frozenAnimators[i].speed;
                frozenAnimators[i].speed = 0f;
            }

            if (rb != null)
            {
                frozenConstraints = rb.constraints;
                rb.linearVelocity = Vector2.zero;
                rb.constraints = RigidbodyConstraints2D.FreezeAll;
            }

            if (spriteRenderer != null)
            {
                frozenColor = spriteRenderer.color;
                spriteRenderer.color = frozenColor * FrozenTint;
            }
        }

        frozenUntil = Mathf.Max(frozenUntil, Time.time + seconds);
        recycleHoldUntil = Mathf.Max(recycleHoldUntil, frozenUntil + recycleDelay);
    }

    private void Unfreeze()
    {
        frozenUntil = 0f;

        if (frozenAnimators != null)
        {
            for (int i = 0; i < frozenAnimators.Length; i++)
            {
                if (frozenAnimators[i] != null) frozenAnimators[i].speed = frozenAnimatorSpeeds[i];
            }
            frozenAnimators = null;
        }

        if (rb != null) rb.constraints = frozenConstraints;
        if (spriteRenderer != null) spriteRenderer.color = frozenColor;

        // Rueckstoss und Zug, die waehrend des Festklebens angekommen sind, verfallen.
        pushCounter = 0f;
        externalVelocity = Vector2.zero;
        externalVelocityTimer = 0f;

        // Der Stau von vor dem Festkleben gilt nicht mehr.
        ResetBlocked();
    }

    // ------------------------------------------------------------- Laufweg

    /// <summary>
    /// Wohin der Gegner laeuft: immer geradewegs auf den Spieler zu.
    ///
    /// Ein Versuch mit Vorhalt (auf den Punkt zielen, an dem der Spieler gleich
    /// sein wird) und seitlichem Versatz ist wieder raus: lief der Spieler auf
    /// einen Gegner zu, lag der Zielpunkt HINTER dem Gegner, und der wich
    /// zurueck statt anzugreifen. Gegen das Umkreisen des Haufens hilft jetzt
    /// <see cref="IsBlocked"/> - wer hinten festhaengt und aus dem Bild faellt,
    /// wird vom Director nach vorn geholt.
    /// </summary>
    private Vector3 PursuitDirection(PlayerController player)
    {
        Vector2 toPlayer = (Vector2)(player.transform.position - transform.position);
        if (toPlayer.sqrMagnitude < 0.0001f) return Vector3.zero;
        return toPlayer.normalized;
    }

    // ------------------------------------------------------------- Stau

    /// <summary>Ab so vielen Gegnern zwischen mir und dem Spieler gilt der Weg als dicht.</summary>
    private const int BlockedEnter = 6;

    /// <summary>Erst unter so vielen ist der Weg wieder frei - sonst flackert der Zustand.</summary>
    private const int BlockedExit = 3;

    /// <summary>Naeher als das am Spieler ist niemand blockiert - die vorderste Reihe drueckt immer.</summary>
    private const float BlockedMinDistance = 4f;

    /// <summary>Breite des Korridors zum Spieler, als Vielfaches des eigenen Koerper-Radius.</summary>
    private const float BlockedCorridorRadii = 4f;

    /// <summary>So oft wird nachgezaehlt. Jeder Gegner mit eigenem Versatz, damit nicht alle im selben Frame.</summary>
    private const float BlockedCheckInterval = 0.4f;

    private static readonly Collider2D[] corridorHits = new Collider2D[32];

    private float blockedCheckTimer = -1f;
    private float blockedSince = -1f;

    /// <summary>
    /// Stehen zu viele andere Gegner zwischen diesem und dem Spieler? Er laeuft
    /// trotzdem weiter auf den Spieler zu - faellt er dabei aber aus dem Bild,
    /// setzt ihn der <see cref="SpawnDirector"/> an anderer Stelle neu an.
    /// </summary>
    public bool IsBlocked => blockedSince >= 0f;

    /// <summary>Wie lange der Weg schon dicht ist, 0 = frei.</summary>
    public float BlockedFor => blockedSince >= 0f ? Time.time - blockedSince : 0f;

    /// <summary>
    /// Nach einem Umsetzen: Stau vergessen und gleich neu zaehlen. Sonst gilt
    /// der Gegner an der neuen Stelle noch als blockiert und wuerde sofort
    /// ein zweites Mal umgesetzt.
    /// </summary>
    public void ResetBlocked()
    {
        blockedSince = -1f;
        blockedCheckTimer = 0f;
    }

    private void UpdateBlocked(PlayerController player, float dt)
    {
        if (blockedCheckTimer < 0f) blockedCheckTimer = Random.value * BlockedCheckInterval;

        blockedCheckTimer -= dt;
        if (blockedCheckTimer > 0f) return;
        blockedCheckTimer = BlockedCheckInterval;

        // Nur Gegner, die der Director auch nachziehen darf - Kaefig und Ring
        // gehoeren an ihren Platz und sollen weiter druecken.
        if (IsBoss || !CanRecycle || ownCollider == null)
        {
            blockedSince = -1f;
            return;
        }

        Vector2 me = BodyCenter;
        Vector2 toPlayer = (Vector2)player.transform.position - me;
        float distance = toPlayer.magnitude;

        if (distance < BlockedMinDistance)
        {
            blockedSince = -1f;
            return;
        }

        int ahead = CountEnemiesAhead(me, toPlayer / distance, distance);

        if (blockedSince < 0f)
        {
            if (ahead >= BlockedEnter) blockedSince = Time.time;
        }
        else if (ahead < BlockedExit)
        {
            blockedSince = -1f;
        }
    }

    /// <summary>
    /// Zaehlt die Gegner in einem Korridor von mir bis kurz vor den Spieler.
    /// Den letzten Meter lassen wir aus - wer direkt am Spieler klebt, steht
    /// nicht im Weg, sondern ist schon da.
    /// </summary>
    private int CountEnemiesAhead(Vector2 me, Vector2 dir, float distance)
    {
        float myRadius = BodyRadius;
        float length = distance - 1f - myRadius;
        if (length <= 0f) return 0;

        Vector2 center = me + dir * (myRadius + length * 0.5f);
        Vector2 size = new Vector2(length, Mathf.Max(0.5f, myRadius * BlockedCorridorRadii));
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(1 << gameObject.layer);
        filter.useTriggers = false;

        int count = Physics2D.OverlapBox(center, size, angle, filter, corridorHits);

        int enemies = 0;
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = corridorHits[i];
            if (hit == ownCollider) continue;
            if (byCollider.TryGetValue(hit, out Enemy other) && other != null) enemies++;
        }
        return enemies;
    }

    // ------------------------------------------------------------- Abstand

    /// <summary>
    /// Ab welchem Anteil der beiden Koerper-Radien sich zwei Gegner
    /// wegdruecken. 1 = sie beruehren sich gerade so, darunter duerfen sie
    /// ein Stueck uebereinander stehen. 0.75 heisst: dicht hintereinander,
    /// aber hoechstens ein Viertel ineinander.
    /// </summary>
    private const float SeparationSpacing = 0.75f;

    /// <summary>Wie stark weggedrueckt wird, als Anteil des eigenen Tempos.</summary>
    private const float SeparationStrength = 2f;

    /// <summary>Abstand neu rechnen jeden n-ten Physikschritt (siehe <see cref="ThrottledSeparation"/>).</summary>
    private const int SeparationEvery = 3;

    private int separationPhase = -1;
    private Vector2 lastPush;
    private Vector2 lastChaseFix;

    private static readonly Collider2D[] separationHits = new Collider2D[16];
    private static readonly Dictionary<Collider2D, Enemy> byCollider = new Dictionary<Collider2D, Enemy>();

    private Collider2D ownCollider;
    private Sprite radiusSprite;
    private float bodyRadius;

    /// <summary>
    /// Mitte des sichtbaren Koerpers in Weltkoordinaten. Der Pivot sitzt bei
    /// manchen Bildern unten, deshalb nicht einfach transform.position.
    /// </summary>
    private Vector2 BodyCenter
    {
        get
        {
            if (spriteRenderer != null && spriteRenderer.sprite != null)
                return spriteRenderer.bounds.center;
            return ownCollider != null ? (Vector2)ownCollider.bounds.center : (Vector2)transform.position;
        }
    }

    /// <summary>
    /// Radius des sichtbaren Koerpers - aus dem Bild, nicht aus dem Collider.
    ///
    /// Die Collider taugen dafuer nicht: beim Fliegenpilz etwa ist er noch fuer
    /// 100 Pixel pro Einheit gebaut, das Bild steht inzwischen auf 20 - der
    /// Collider ist also ein Fuenftel so gross wie der Pilz. Mit dem Collider
    /// als Mass standen die Pilze fast deckungsgleich aufeinander.
    /// Die Sprite-Bounds sind bei "Tight"-Meshes schon um den leeren Rand
    /// beschnitten. Neu gerechnet wird nur, wenn die Animation das Bild wechselt.
    /// </summary>
    private float BodyRadius
    {
        get
        {
            Sprite sprite = spriteRenderer != null ? spriteRenderer.sprite : null;
            if (sprite != null)
            {
                if (sprite != radiusSprite)
                {
                    radiusSprite = sprite;
                    Vector3 size = Vector3.Scale(sprite.bounds.extents, transform.lossyScale);
                    bodyRadius = Mathf.Min(Mathf.Abs(size.x), Mathf.Abs(size.y));
                }
                return bodyRadius;
            }
            return ownCollider != null ? ownCollider.bounds.extents.x : 0.3f;
        }
    }

    private void RegisterCollider()
    {
        if (ownCollider == null) ownCollider = GetComponent<Collider2D>();
        if (ownCollider != null) byCollider[ownCollider] = this;
    }

    private void UnregisterCollider()
    {
        if (ownCollider != null) byCollider.Remove(ownCollider);
    }

    /// <summary>
    /// <see cref="Separation"/> nur jeden <see cref="SeparationEvery"/>-ten
    /// Physikschritt, dazwischen gilt das letzte Ergebnis weiter.
    ///
    /// Die Abfrage ist pro Gegner der teuerste Teil des Physikschritts. Bei
    /// 500+ Gegnern dauerte ein Schritt so lange, dass Unity im naechsten Frame
    /// mehrere Schritte nachholen musste - und jeder davon wieder alle Abfragen
    /// machte. Das war der kurze Freeze nach grossen Wellen. Jeder Gegner hat
    /// seinen eigenen Versatz, damit nicht alle im selben Schritt rechnen.
    ///
    /// Die Kaefig-Wand steht praktisch still - bei ihrem Tempo wirkt der
    /// Abstand ohnehin nicht, sie rechnet ihn deshalb gar nicht erst.
    /// </summary>
    private Vector2 ThrottledSeparation(ref Vector2 chase)
    {
        if (role == EnemyRole.Blocker) return Vector2.zero;

        if (separationPhase < 0) separationPhase = Random.Range(0, SeparationEvery);

        int step = Mathf.RoundToInt(Time.fixedTime / Time.fixedDeltaTime);
        if ((step + separationPhase) % SeparationEvery == 0)
        {
            Vector2 before = chase;
            lastPush = Separation(ref chase);
            lastChaseFix = chase - before;
            return lastPush;
        }

        // Rueckstoss dreht die Laufrichtung um - die alte Korrektur zeigt dann
        // in die falsche Richtung.
        if (pushCounter <= 0f) chase += lastChaseFix;
        return lastPush;
    }

    /// <summary>
    /// Abstand zu den Nachbarn.
    ///
    /// Die Kollision allein reicht nicht: jeder Gegner setzt jeden Schritt
    /// seine Geschwindigkeit fest auf den Spieler zu. Laeuft der Spieler im
    /// Kreis, druecken alle in denselben Punkt, und der Physik-Loeser schiebt
    /// pro Schritt nur ein Stueck zurueck - die Gruppe presst sich zu einem
    /// Klumpen zusammen.
    ///
    /// Deshalb zwei Dinge: wer zu tief in einem Nachbarn steckt, wird
    /// weggeschoben (Rueckgabe). Und der Teil der eigenen Laufrichtung, der
    /// geradewegs in den Nachbarn vorne hineinfuehrt, wird abgezogen - der
    /// Hintere reiht sich ein, statt weiter in den Vorderen zu druecken.
    /// </summary>
    private Vector2 Separation(ref Vector2 chase)
    {
        if (ownCollider == null) return Vector2.zero;

        Vector2 me = BodyCenter;
        float myRadius = BodyRadius;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(1 << gameObject.layer);
        filter.useTriggers = false;

        // Etwas weiter suchen als der eigene Koerper: der Collider eines
        // Nachbarn kann deutlich kleiner sein als sein Bild.
        int count = Physics2D.OverlapCircle(me, myRadius * 3f, filter, separationHits);

        Vector2 push = Vector2.zero;
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = separationHits[i];
            if (hit == ownCollider) continue;

            Vector2 otherCenter;
            float otherRadius;
            if (byCollider.TryGetValue(hit, out Enemy other) && other != null)
            {
                otherCenter = other.BodyCenter;
                otherRadius = other.BodyRadius;
            }
            else
            {
                otherCenter = hit.bounds.center;
                otherRadius = hit.bounds.extents.x;
            }

            Vector2 away = me - otherCenter;
            float distance = away.magnitude;
            float desired = (myRadius + otherRadius) * SeparationSpacing;
            if (distance >= desired) continue;

            // Genau aufeinander: keine Richtung ablesbar, also zufaellig.
            if (distance < 0.001f)
            {
                push += Random.insideUnitCircle.normalized;
                continue;
            }

            Vector2 awayDir = away / distance;
            float depth = 1f - distance / desired;
            push += awayDir * depth;

            float into = -Vector2.Dot(chase, awayDir);
            if (into > 0f) chase += awayDir * into * Mathf.Clamp01(depth * 3f);
        }

        return Vector2.ClampMagnitude(push, 1.5f);
    }

    /// <summary>
    /// Spiegelt das Bild zum Spieler hin - je nachdem, wie herum es gezeichnet
    /// ist. Die 0.2 Einheiten Totzone verhindern das Flackern, wenn der Spieler
    /// genau auf der Achse steht.
    /// </summary>
    private void FacePlayer(PlayerController player)
    {
        if (facing == EnemyFacing.Neutral || spriteRenderer == null) return;

        float xDiff = player.transform.position.x - transform.position.x;
        if (Mathf.Abs(xDiff) <= 0.2f) return;

        bool playerIsRight = xDiff > 0f;
        spriteRenderer.flipX = facing == EnemyFacing.ArtFacesRight
            ? !playerIsRight
            : playerIsRight;
    }

    // -------------------------------------------------------------- Schaden

    protected virtual void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerController.Instance.TakeDamage(damage);
        }
    }

    protected virtual void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("PlayerHitbox"))
        {
            PlayerController.Instance.TakeDamage(damage);
        }
    }

    /// <param name="slowMultiplier">Tempo-Faktor fuer 1,5 s (0,6 = 40 % langsamer), null = kein Slow.</param>
    /// <param name="knockback">Anteil des Rueckstosses, 1 = normal, 0 = keiner.</param>
    public virtual void TakeDamage(float damage, float? slowMultiplier = null, float knockback = 1f)
    {
        if (Untouchable) return;

        float finalDamage = damage * PlayerController.Instance.damageMultiplier * Bestiary.DamageFactor(id);
        float critChance = PlayerController.Instance.critChance;
        float critDamage = PlayerController.Instance.critDamage;

        if (Random.value < critChance)
        {
            finalDamage *= critDamage;
            DamageNumberController.Instance.CreateNumberCrit(finalDamage, transform.position);
        }
        else
        {
            DamageNumberController.Instance.CreateNumber(finalDamage, transform.position);
        }

        float before = health;
        health -= finalDamage;
        if (MinHealthFraction > 0f)
        {
            float floor = Mathf.Min(before, Mathf.Max(1f, maxHealth * MinHealthFraction));
            if (health < floor) health = floor;
        }

        if (LifeSteal.Instance != null)
        {
            LifeSteal.Instance.StealLife((int)damage);
        }

        ApplyKnockback(knockback);

        if (health <= 0f)
        {
            if (HoldDeath)
            {
                health = 0f;
                if (!deathHeld)
                {
                    deathHeld = true;
                    Untouchable = true;
                    DeathHeld?.Invoke();
                }
                return;
            }
            Die();
            return;
        }

        // Slow nur anwenden, wenn der Gegner noch lebt - sonst laeuft er auf
        // einem gleich zerstoerten Objekt.
        if (slowMultiplier.HasValue) ApplySlow(slowMultiplier.Value, 1.5f);
    }

    /// <summary>
    /// Bremst den Gegner. Ein zweiter Treffer waehrend eines laufenden Slows
    /// verlaengert ihn und nimmt den staerkeren der beiden Werte.
    ///
    /// Frueher hat ein Schalter jeden weiteren Slow verworfen, solange der
    /// erste lief - wer dauerhaft mit einer Slow-Waffe draufhielt, hat den
    /// Gegner also nur jede 1.5 Sekunden einmal gebremst.
    /// </summary>
    private void ApplyKnockback(float knockback)
    {
        if (knockback <= 0f || Time.time < nextPushAllowed) return;

        float push = pushTime * KnockbackScale * knockback;
        if (push <= 0f) return;

        pushCounter = push;
        nextPushAllowed = Time.time + push + KnockbackImmunity;
    }

    /// <summary>Bremst ohne Schaden, z.B. in der Windschneise des <see cref="TyphoonFan"/>.</summary>
    public void ApplySlow(float multiplier, float duration)
    {
        slowFactor = Mathf.Min(slowFactor, Mathf.Clamp01(multiplier));
        slowUntil = Mathf.Max(slowUntil, Time.time + duration);
    }

    // ----------------------------------------------------------------- Tod

    /// <summary>Den angehaltenen Tod jetzt ausfuehren (Beute, Erfolge, Boss-Sieg, weg).</summary>
    public void FinishHeldDeath()
    {
        if (this == null) return;
        Die();
    }

    private void Die()
    {
        TryDropPickup();

        // Ohne Null-Pruefung reisst ein fehlender Spawner den ganzen
        // Todesfall mit: die NullReference fliegt, das Destroy unten laeuft
        // nie, und der Gegner steht mit negativem Leben weiter herum.
        DropExperience();

        GrantRewards();
        SpawnDeathEffect();
        if (GameManager.Instance != null) GameManager.Instance.kills++;
        Bestiary.AddKill(id);

        AudioController.Instance.PalyModifiedSound(AudioController.Instance.enemyDeath);

        Destroy(gameObject);
    }

    /// <summary>
    /// Was es fuer diesen Gegner gibt. Frueher eine Kette aus if/else-if ueber
    /// drei Bools, bei der der Boss-Zweig zusaetzlich zu den anderen lief -
    /// jetzt genau ein Fall je Rolle.
    /// </summary>
    private void GrantRewards()
    {
        switch (role)
        {
            case EnemyRole.DeathBoss:
                PlayerController.Instance.attractAllXP = true;
                Achievements.Unlock(Ach.Death);
                break;

            case EnemyRole.Elite:
                // Die Erfolge heissen im Code noch "Miniboss" (Steam-Schluessel),
                // zaehlen aber Elites.
                Achievements.Progress(Ach.Kill10Miniboss, 1f);
                Achievements.Progress(Ach.Kill100Miniboss, 1f);
                ClearCage();
                break;

            case EnemyRole.MiniBoss:
                // Die Beute ist vorerst nur die XP aus dem Katalog - die
                // fliegen dem Spieler wie beim Boss gleich zu.
                PlayerController.Instance.attractAllXP = true;
                ClearCage();
                break;

            case EnemyRole.Boss:
                PlayerController.Instance.attractAllXP = true;
                GameManager.Instance.bossSpawned = true;
                // Erster Sieg ueber diesen Boss mit diesem Charakter: ein
                // Charakter-Level geschenkt.
                Skills.RegisterBossVictory(Identity != EnemyId.None ? Identity.ToString() : name.Replace("(Clone)", "").Trim());
                if (SpawnDeath.Instance != null)
                {
                    Vector3 deathPos = transform.position;
                    deathPos.x += 50f;
                    SpawnDeath.Instance.Spawn(deathPos);
                }
                break;

            case EnemyRole.Blocker:
                // Die Kaefig-Wand ist Kulisse. Sie zaehlt auf nichts.
                break;

            default:
                Achievements.Progress(Ach.Kill100, 1f);
                Achievements.Progress(Ach.Kill1000, 1f);
                Achievements.Progress(Ach.Kill10000, 1f);
                break;
        }
    }

    /// <summary>
    /// Raeumt die Kaefig-Wand eines Encirclements weg. Sie steht auf dem Layer
    /// "Enemy_barrier" und wuerde ohne das fuer immer stehen bleiben - deshalb
    /// setzt der Director sie auch nur zusammen mit einem Elite oder Miniboss
    /// in der Mitte.
    /// </summary>
    private void ClearCage()
    {
        int blockerLayer = LayerMask.NameToLayer("Enemy_barrier");
        if (blockerLayer < 0) return;

        for (int i = alive.Count - 1; i >= 0; i--)
        {
            Enemy other = alive[i];
            if (other == null || other == this) continue;
            if (other.gameObject.layer != blockerLayer) continue;

            Destroy(other.gameObject);
        }
    }

    private void SpawnDeathEffect()
    {
        if (destroyEffect == null || SkipDeathEffect) return;

        RunPool.Spawn(destroyEffect, transform.position, transform.rotation, "Effekte");
    }

    /// <summary>
    /// Magnet und Herz. Die Wahrscheinlichkeiten stehen im
    /// <see cref="EnemyCatalog"/> - frueher standen sie als nackte Zahlen hier,
    /// mit einem Kommentar, der nicht dazu passte.
    /// </summary>
    private void TryDropPickup()
    {
        PickUpManager pickups = PickUpManager.Instance;
        if (pickups == null) return;

        PlayerController player = PlayerController.Instance;
        float dropMult = player != null ? player.dropChanceMultiplier : 1f;

        if (Random.value < EnemyCatalog.MagnetChance * dropMult && pickups.magnet_PickUP != null)
        {
            MoveToRunScene(Instantiate(pickups.magnet_PickUP,
                                       transform.position, Quaternion.identity));
        }

        if (Random.value < EnemyCatalog.HeartChance * dropMult && pickups.heart_PickUP != null)
        {
            bool golden = player != null && Random.value < player.goldenHeartChance;
            MoveToRunScene(pickups.SpawnHeart(transform.position, golden));
        }
    }

    /// <summary>
    /// XP beim Tod. Mit dem Skill "LuckyXpChance" faellt manchmal die
    /// doppelte Menge - dann golden eingefaerbt und mit "x2" darueber.
    /// </summary>
    private void DropExperience()
    {
        if (SpawnExp.Instance == null) return;

        PlayerController player = PlayerController.Instance;
        bool lucky = player != null && Random.value < player.luckyXpChance;

        SpawnExp.Instance.SpawnEP(transform.position, lucky ? experienceToGive * 2 : experienceToGive, lucky);
    }

    /// <summary>Todeseffekt-Prefab - der SpawnDirector legt davon beim Laden einen Vorrat an.</summary>
    public GameObject DeathEffect => destroyEffect;

    private static void MoveToRunScene(GameObject spawned)
    {
        if (spawned == null) return;

        Scene runScene = RunScene.Current;
        if (runScene.IsValid() && runScene.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(spawned, runScene);
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Nur fuer die Gegner-Werkstatt: stellt ein altes Prefab auf den Katalog
    /// um. Die alten Felder bleiben stehen (falls jemand zurueck will), zaehlen
    /// aber ab jetzt nicht mehr.
    /// </summary>
    public void EditorSetId(EnemyId newId)
    {
        id = newId;
    }

    /// <summary>Nur fuer die Werkstatt: Bausteine eintragen.</summary>
    public void EditorBind(SpriteRenderer renderer, Rigidbody2D body, GameObject effect)
    {
        spriteRenderer = renderer;
        rb = body;
        if (effect != null) destroyEffect = effect;
    }

    /// <summary>Nur fuer die Werkstatt: das Todes-Effekt-Prefab dieses Gegners.</summary>
    public GameObject EditorDestroyEffect => destroyEffect;
#endif
}
