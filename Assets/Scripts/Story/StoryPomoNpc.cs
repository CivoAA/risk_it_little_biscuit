using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Pomo als Figur im Hub: steht neben der Reisekarte (Levelauswahl), tickt
/// im Halbsekundentakt hin und her, blinzelt, und redet mit, solange ein
/// Gespraech mit ihr laeuft. [E] in der Naehe = ein Satz, der zum Stand der
/// Story passt.
///
/// Wird von <see cref="StoryDirector"/> beim Laden des Hubs angelegt - kein
/// Szenen-Objekt noetig. Die Position haengt am Bild der Levelauswahl
/// (Glasglocke): rechts daneben, auf ihrer Grundlinie - steht die woanders,
/// wandert Pomo mit.
/// </summary>
public class StoryPomoNpc : HubInteractable
{
    /// <summary>Abstand rechts neben dem Bild der Levelauswahl (Einheiten = 32-px-Kacheln).</summary>
    private const float Gap = 0.35f;

    private SpriteRenderer sr;
    private Sprite[] frames;
    private float nextBlink;
    private int talkIndex;
    private int talkCount;

    /// <summary>So oft mit Pomo reden, bis der Wecker klingelt (Ach.SecretWakeUp).</summary>
    private const int WakeUpTalks = 10;

    protected override string PromptKey => "hub.prompt.pomo";

    public static void EnsureIn(Scene hub)
    {
        foreach (GameObject root in hub.GetRootGameObjects())
            if (root.GetComponentInChildren<StoryPomoNpc>(true) != null) return;

        HubLevelSelectTrigger table = null;
        foreach (GameObject root in hub.GetRootGameObjects())
        {
            table = root.GetComponentInChildren<HubLevelSelectTrigger>(true);
            if (table != null) break;
        }
        if (table == null) return;

        var go = new GameObject("Pomo (Story)");
        SceneManager.MoveGameObjectToScene(go, hub);
        // Neben das Bild der Levelauswahl stellen, nicht an den Objekt-Ursprung.
        Vector3 p = table.transform.position + new Vector3(1.75f, -0.95f, 0f);
        // Nur Bilder in der Naehe zaehlen - unter dem Objekt haengt noch anderes.
        Vector3 origin = table.transform.position;
        bool found = false;
        Bounds b = default;
        foreach (SpriteRenderer r in table.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (r.sprite == null || Vector2.Distance(r.bounds.center, origin) > 4f) continue;
            if (!found) { b = r.bounds; found = true; }
            else b.Encapsulate(r.bounds);
        }
        if (found) p = new Vector3(b.max.x + Gap + 0.5f, b.min.y + 0.05f, origin.z);
        go.transform.position = new Vector3(Mathf.Round(p.x * 32f) / 32f, Mathf.Round(p.y * 32f) / 32f, p.z);
        go.AddComponent<StoryPomoNpc>();
    }

    private void Awake()
    {
        shape = InteractShape.Kreis;
        interactRadius = 1.3f;
        promptText = "[E] Mit Pomo reden";
        frames = StoryArt.Strip("pomo_npc");
        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = frames.Length > 0 ? frames[0] : null;
    }

    protected override void Start()
    {
        base.Start();
        // Gleiche Sortier-Ebene wie der Spieler, damit er vor und hinter ihr laufen kann.
        if (player != null)
        {
            SpriteRenderer ps = player.GetComponentInChildren<SpriteRenderer>();
            if (ps != null)
            {
                sr.sortingLayerID = ps.sortingLayerID;
                sr.sortingOrder = ps.sortingOrder;
            }
        }
        nextBlink = Time.time + 2f;
    }

    protected override void Update()
    {
        base.Update();
        if (frames.Length < 8) return;

        // Redet sie gerade im Dialog? Dann Mund auf/zu.
        bool talking = StoryDialog.IsOpen && StoryDialogSpeakerIsPomo();
        int f;
        if (talking) f = Mathf.FloorToInt(Time.unscaledTime * 6f) % 2 == 0 ? 7 : 0;
        else
        {
            // Ticken: ein Pixel hoch und runter (Bilder 0-5 = ein 1,5-s-Zyklus)
            f = Mathf.FloorToInt(Time.time * 4f) % 6;
            if (Time.time > nextBlink)
            {
                f = 6;
                if (Time.time > nextBlink + 0.12f) nextBlink = Time.time + Random.Range(2f, 4.5f);
            }
        }
        sr.sprite = frames[f];
    }

    private static bool StoryDialogSpeakerIsPomo() => StoryDialog.CurrentSpeaker == StoryTexts.Who.Pomo;

    protected override void OnInteract()
    {
        if (StoryDialog.IsOpen || StoryCutscene.IsPlaying) return;

        // Geheim-Erfolg: zehnmal angequatscht, seit der Hub geladen ist.
        if (++talkCount >= WakeUpTalks) Achievements.Unlock(Ach.SecretWakeUp);

        string id;
        object[] args = null;
        if (StoryProgress.FinaleDone) id = "talk.done" + (1 + (talkIndex++ % 3));
        else if (StoryProgress.FinaleOpen) id = "talk.ready";
        else if (StoryProgress.ShardCount == 0) id = "talk.none";
        else
        {
            id = "talk.some";
            args = new object[] { StoryProgress.ShardWorlds.Length - StoryProgress.ShardCount };
        }

        StoryDialog.Play(id, null, new StoryDialog.Options { FreezeTime = false, BlockHub = true, Dim = 0.25f, FormatArgs = args });
    }
}
