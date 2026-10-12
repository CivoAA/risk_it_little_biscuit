using System.Collections.Generic;
using UnityEngine;
using Who = StoryTexts.Who;
using Mood = StoryTexts.Mood;

/// <summary>
/// Bilder, Namen und Farben der Story-Sprecher, und die Bildstreifen der
/// Cutscenes. Alles liegt in Resources/Story (Pomo, Splitter, Ofen, Kopien
/// von Verkohlter und Level-Panoramen - aus dem Artwork-Ordner,
/// pomo.py / story_art.py), die Boss-Kacheln kommen aus
/// Resources/LevelSelect/Bosses wie in der Levelauswahl.
/// </summary>
public static class StoryArt
{
    private static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();

    // Bilder in pomo_portrait.png (siehe pomo.py)
    private const int PomoIdle = 0, PomoBlink = 1, PomoTalk = 2, PomoTalkWide = 3,
                      PomoSad = 4, PomoSadTalk = 5, PomoHappy = 6, PomoHappyTalk = 7,
                      PomoShock = 8, PomoShockTalk = 9, PomoRingA = 10, PomoRingB = 11;

    /// <summary>Ein Bildstreifen aus Resources/Story, nach Bildnummer sortiert.</summary>
    public static Sprite[] Strip(string name)
    {
        if (cache.TryGetValue(name, out Sprite[] cached) && cached != null) return cached;
        Sprite[] frames = Resources.LoadAll<Sprite>("Story/" + name);
        if (frames == null || frames.Length == 0)
        {
            Debug.LogWarning("[Story] Bild fehlt: Resources/Story/" + name);
            frames = new Sprite[0];
        }
        else
        {
            System.Array.Sort(frames, (a, b) => Index(a).CompareTo(Index(b)));
        }
        cache[name] = frames;
        return frames;
    }

    public static Sprite Frame(string strip, int i)
    {
        Sprite[] s = Strip(strip);
        return s.Length == 0 ? null : s[((i % s.Length) + s.Length) % s.Length];
    }

    private static int Index(Sprite s)
    {
        int u = s.name.LastIndexOf('_');
        return u >= 0 && int.TryParse(s.name.Substring(u + 1), out int n) ? n : 0;
    }

    // ==================================================================
    //  Sprecher
    // ==================================================================

    public static EnemyId EnemyOf(Who who)
    {
        switch (who)
        {
            case Who.KeksKoenig: return EnemyId.KeksKoenig;
            case Who.Glutwurz: return EnemyId.Glutwurz;
            case Who.Eiskaiser: return EnemyId.Eiskaiser;
            case Who.Gespenst: return EnemyId.Squiddy;
            case Who.Squiddy: return EnemyId.Squiddy;
            case Who.Verkohlter: return EnemyId.Verkohlter;
            default: return EnemyId.None;
        }
    }

    public static string NameOf(Who who)
    {
        switch (who)
        {
            case Who.Pomo: return StoryTexts.T("story.name.pomo");
            case Who.Squiddy: return StoryTexts.T("story.name.squiddy");
            case Who.Player: return Characters.DisplayName(Shop.SkinIndex);
            default: return Bestiary.NameOf(EnemyOf(who));
        }
    }

    public static Color AccentOf(Who who)
    {
        switch (who)
        {
            case Who.Pomo: return GameHudSkin.JamLight;
            case Who.Player: return CharacterLooks.AccentFor(Shop.SkinIndex);
            case Who.KeksKoenig: return GameHudSkin.Gold;
            case Who.Glutwurz: return GameHudSkin.Mint;
            case Who.Eiskaiser: return GameHudSkin.IcingLight;
            case Who.Gespenst: return GameHudSkin.StoneLight;
            case Who.Squiddy: return GameHudSkin.GrapeLight;
            case Who.Verkohlter: return new Color32(0xff, 0x9a, 0x3c, 0xff);
            default: return GameHudSkin.Cream;
        }
    }

    /// <summary>Pomo und die Erzaehlerin stehen links, alle anderen rechts.</summary>
    public static bool OnLeft(Who who) => who == Who.Pomo;

    /// <summary>
    /// Portrait fuer einen Sprecher. talking = Mund auf (wechselt beim Tippen),
    /// blink = Augen zu (nur Pomo). Kann null sein.
    /// </summary>
    public static Sprite Portrait(Who who, Mood mood, bool talking, bool blink)
    {
        switch (who)
        {
            case Who.Pomo:
                return Frame("pomo_portrait", PomoFrame(mood, talking, blink));

            case Who.Player:
                Sprite[] p = CharacterLooks.PortraitFor(Shop.SkinIndex);
                if (p == null || p.Length == 0) return null;
                return p[talking && p.Length > 1 ? 1 : 0];

            case Who.Squiddy:
                return Frame("portrait_squiddy", 0);

            default:
                return BossPortrait(EnemyOf(who));
        }
    }

    private static int PomoFrame(Mood mood, bool talking, bool blink)
    {
        switch (mood)
        {
            case Mood.Sad: return talking ? PomoSadTalk : PomoSad;
            case Mood.Happy: return talking ? PomoHappyTalk : PomoHappy;
            case Mood.Shock: return talking ? PomoShockTalk : PomoShock;
            case Mood.Ring: return Mathf.FloorToInt(Time.unscaledTime * 18f) % 2 == 0 ? PomoRingA : PomoRingB;
            default:
                if (talking) return Mathf.FloorToInt(Time.unscaledTime * 7f) % 3 == 0 ? PomoTalkWide : PomoTalk;
                return blink ? PomoBlink : PomoIdle;
        }
    }

    public static Sprite PomoRing(int i) => Frame("pomo_portrait", i % 2 == 0 ? PomoRingA : PomoRingB);

    // ==================================================================
    //  Spielfigur ohne Hintergrund
    // ==================================================================

    private static Animator puppet;
    private static SpriteRenderer puppetSprite;
    private static RuntimeAnimatorController puppetController;

    /// <summary>
    /// Aktuelles Idle-Bild des gewaehlten Charakters - ohne den Hintergrund
    /// der Keksdosen-Portraits. Ein unsichtbarer Animator spielt die Idle-
    /// Animation (wie die Figur in der Levelauswahl), hier wird nur sein Bild
    /// abgelesen. Null, solange er noch nichts gezeigt hat.
    /// </summary>
    public static Sprite PlayerFrame()
    {
        RuntimeAnimatorController c = CharacterLooks.AnimatorFor(Shop.SkinIndex);
        if (c == null && WM_PlayerSkinSwitcher.Instance != null && Shop.SkinIndex == 0)
            c = WM_PlayerSkinSwitcher.Instance.NormalSkinOverride;
        if (c == null) return null;

        if (puppet == null)
        {
            var go = new GameObject("StoryPlayerPuppet");
            go.hideFlags = HideFlags.HideInHierarchy;
            Object.DontDestroyOnLoad(go);
            puppetSprite = go.AddComponent<SpriteRenderer>();
            puppetSprite.enabled = false;            // nur Bildquelle
            puppet = go.AddComponent<Animator>();
            puppet.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            puppet.updateMode = AnimatorUpdateMode.UnscaledTime;
            puppetController = null;
        }
        if (c != puppetController)
        {
            puppetController = c;
            puppet.runtimeAnimatorController = c;
            puppetSprite.sprite = null;
            puppet.Play("Idle", 0, 0f);
        }
        puppet.SetBool("moving", false);
        puppet.SetFloat("MoveX", 0f);
        puppet.SetFloat("MoveY", -1f);
        puppet.SetFloat("LastMoveX", 0f);
        puppet.SetFloat("LastMoveY", -1f);
        return puppetSprite.sprite;
    }

    /// <summary>
    /// Wohin ein Bild der Spielfigur gehoert, damit ihr Koerper (ohne leere
    /// Zeilen unter den Fuessen, ohne herausstehendes Schwert) mittig ueber
    /// <paramref name="centerX"/> auf <paramref name="baseline"/> steht.
    /// Seitenpixel, oben links; Breite/Hoehe = ganzes Bild mal scale.
    /// </summary>
    public static Rect PlayerRect(Sprite s, float centerX, float baseline, int scale)
    {
        Rect body = CharacterLooks.BodyRect(s);
        int foot = CharacterLooks.FootRowsFor(Shop.SkinIndex);
        float w = s.rect.width * scale, h = s.rect.height * scale;
        float x = centerX - (body.x + body.width / 2f) * scale;
        float y = baseline - (s.rect.height - (body.y + foot)) * scale;
        return new Rect(Mathf.Round(x), Mathf.Round(y), w, h);
    }

    private static readonly Dictionary<EnemyId, Sprite> bosses = new Dictionary<EnemyId, Sprite>();

    public static Sprite BossPortrait(EnemyId id)
    {
        if (id == EnemyId.None) return null;
        if (bosses.TryGetValue(id, out Sprite s)) return s;
        Sprite[] all = Resources.LoadAll<Sprite>("LevelSelect/Bosses/" + id);
        s = all != null && all.Length > 0 ? all[0] : Bestiary.Icon(id);
        bosses[id] = s;
        return s;
    }
}
