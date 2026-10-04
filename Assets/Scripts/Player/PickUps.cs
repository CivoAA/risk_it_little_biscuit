using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PickUps : MonoBehaviour
{
    /// <summary>0 = Herz, 1 = Magnet, 2 = goldenes Herz (heilt wie ein Herz und gibt Max-Leben).</summary>
    public const int GoldenHeartId = 2;

    public float detectRange = 1f;       // Ab wann das Objekt den Spieler anzieht
    public float moveSpeed = 5f;         // Geschwindigkeit beim Hinfliegen
    public int PickUp_id = 99;
    [SerializeField] private GameObject destroyEffect;

    [Tooltip("Bilder pro Sekunde der Idle-Schleife (Schweben + Glanz).")]
    [SerializeField] private float idleFps = 10f;

    private Transform player;
    private SpriteRenderer spriteRenderer;
    private Sprite[] idleFrames;
    private float idleTime;

    /// <summary>
    /// Idle-Schleife je Pickup-Art aus Resources/PickUps (gezeichnet von
    /// Tools/pickups.py). Null, wenn es keine gibt - dann bleibt das Sprite
    /// aus dem Prefab stehen.
    /// </summary>
    public static Sprite[] LoadIdleFrames(int pickUpId)
    {
        string name = pickUpId switch
        {
            0 => "pickup_heart",
            1 => "pickup_magnet",
            GoldenHeartId => "pickup_heart_golden",
            _ => null,
        };
        return name == null ? null : LoadFrames(name);
    }

    private static readonly Dictionary<string, Sprite[]> frameCache = new Dictionary<string, Sprite[]>();

    /// <summary>
    /// Bildstreifen aus Resources/PickUps, nach Bildnummer sortiert und
    /// zwischengespeichert (XP-Bonbons fallen zu Hunderten). Null, wenn es
    /// den Streifen nicht gibt.
    /// </summary>
    public static Sprite[] LoadFrames(string name)
    {
        if (frameCache.TryGetValue(name, out Sprite[] cached)) return cached;
        Sprite[] frames = Resources.LoadAll<Sprite>("PickUps/" + name);
        if (frames == null || frames.Length == 0) frames = null;
        else System.Array.Sort(frames, (a, b) => FrameIndex(a).CompareTo(FrameIndex(b)));
        frameCache[name] = frames;
        return frames;
    }

    private static int FrameIndex(Sprite s)
    {
        int i = s.name.LastIndexOf('_');
        return i >= 0 && int.TryParse(s.name.Substring(i + 1), out int n) ? n : 0;
    }

    void Start()
    {
        // PickUp_id steht erst nach dem Instantiate fest (goldenes Herz),
        // deshalb hier und nicht in Awake.
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        idleFrames = LoadIdleFrames(PickUp_id);
        if (idleFrames != null)
            idleTime = Random.Range(0, idleFrames.Length) / Mathf.Max(0.01f, idleFps);
        AnimateIdle();

        // Nimmt an, dass dein Player das Tag "Player" hat
        GameObject playerObj = GameObject.FindGameObjectWithTag("PlayerHitbox");
        if (playerObj != null)
            player = playerObj.transform;
    }

    void Update()
    {
        idleTime += Time.deltaTime;
        AnimateIdle();

        if (player == null) return;

        float distance = Vector3.Distance(transform.position, player.position);

        // Wenn Player in Reichweite -> Richtung Player bewegen
        if (distance <= detectRange)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                player.position,
                moveSpeed * Time.deltaTime
            );
        }
    }

    private void AnimateIdle()
    {
        if (idleFrames == null || spriteRenderer == null) return;
        spriteRenderer.sprite = idleFrames[Mathf.FloorToInt(idleTime * idleFps) % idleFrames.Length];
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("PlayerHitbox"))
        {
            if(PickUp_id == 0)
            {
                PlayerController.Instance.Heal(PlayerController.Instance.playerMaxHealth * 0.1f);

            }
            else if (PickUp_id == 1)
            {
                PlayerController.Instance.attractAllXP = true;
            }
            else if (PickUp_id == GoldenHeartId)
            {
                PlayerController p = PlayerController.Instance;
                float bonus = EnemyCatalog.GoldenHeartMaxHealth;
                p.playerMaxHealth += bonus;
                p.Heal(p.playerMaxHealth * 0.1f + bonus);
                DamageNumberController.Instance?.CreateText(string.Format(Loc.Get("ui.float.maxhp", "+{0} Max HP"), bonus.ToString("0")), transform.position);
            }
            else if(PickUp_id == 99)
            {
                return;
            }
            else
            {
                return;
            }
            
            // Danach zerstören
            Destroy(gameObject);
            GameObject DestroyEffect =Instantiate(destroyEffect, transform.position, transform.rotation);
            Scene gameScene = RunScene.Current;
            if (gameScene.IsValid() && gameScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(DestroyEffect, gameScene);
            }
        }
    }
}