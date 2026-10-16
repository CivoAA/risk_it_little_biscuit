using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Die Themen der Tutorials - eins je Station im Hub.</summary>
public enum TutorialTopic
{
    Character,
    Skilltree,
    Workbench,
    LevelSelect,
    Shop,
    Capsule,
}

/// <summary>Eine Seite: Bildtafel links, Ueberschrift + Text (+ Hinweiskasten) rechts.</summary>
public sealed class TutorialPageDef
{
    public string Head, Body, Tip;
    public System.Func<Sprite> TipIcon;
    public System.Action<TutorialPicture> Draw;
}

public sealed class TutorialTopicDef
{
    public TutorialTopic Topic;

    /// <summary>Teil der Loc-Schluessel: ui.tutorial.[Key].title, ui.tutorial.[Key].[n].head/body/tip.</summary>
    public string Key;

    public string Title;
    public System.Func<Sprite> Icon;
    public TutorialPageDef[] Pages;

    public string TitleText => Loc.Get($"ui.tutorial.{Key}.title", Title);

    public string Head(int page) => Loc.Get($"ui.tutorial.{Key}.{page + 1}.head", Pages[page].Head);
    public string Body(int page) => Loc.Get($"ui.tutorial.{Key}.{page + 1}.body", Pages[page].Body);

    public string Tip(int page) =>
        string.IsNullOrEmpty(Pages[page].Tip) ? null : Loc.Get($"ui.tutorial.{Key}.{page + 1}.tip", Pages[page].Tip);
}

/// <summary>
/// Was die Tutorials erzaehlen und zeigen. Die Texte stehen in de/en.json
/// (<c>ui.tutorial.*</c>), hier nur als deutscher Rueckfall. Woerter zwischen
/// Sternchen (*so*) werden golden hervorgehoben.
///
/// Die Bildtafeln bauen sich aus echten Spielgrafiken zusammen - Portraets aus
/// <see cref="CharacterLooks"/>, Kacheln und Symbole der Werkbank, Skill-Formen
/// aus <see cref="SkillShapeSprites"/>. Aendert sich dort etwas, ziehen die
/// Tutorials von selbst mit; nur die Ablaeufe (wann der Zeiger wohin klickt)
/// stehen hier fest.
/// </summary>
public static class TutorialContent
{
    private const int W = TutorialPicture.W, H = TutorialPicture.H;

    private static List<TutorialTopicDef> all;

    public static IReadOnlyList<TutorialTopicDef> All => all ??= Build();

    public static TutorialTopicDef Get(TutorialTopic topic)
    {
        foreach (TutorialTopicDef t in All)
            if (t.Topic == topic) return t;
        return All[0];
    }

    private static List<TutorialTopicDef> Build()
    {
        return new List<TutorialTopicDef>
        {
            new TutorialTopicDef
            {
                Topic = TutorialTopic.Character, Key = "character", Title = "CHARAKTERE",
                Icon = () => GameHudSkin.CookieFace,
                Pages = new[]
                {
                    new TutorialPageDef
                    {
                        Head = "Deine Keksdose",
                        Body = "Jeder Charakter ist ein anderes Essen und spielt sich anders. *Klick* auf ein " +
                               "Porträt, um ihn dir anzusehen. Mit *WÄHLEN* (oder Enter) nimmst du ihn mit " +
                               "in deinen nächsten Lauf.",
                        Draw = DrawRoster,
                    },
                    new TutorialPageDef
                    {
                        Head = "Jeder spielt anders",
                        Body = "Jeder Charakter bringt seine eigene *Startwaffe* mit und hat einen eigenen " +
                               "*Skilltree* und eigene Builds an der *Werkbank*. Was du mit einem lernst, " +
                               "bleibt bei ihm.",
                        Tip = "Graue Charaktere sind noch *gesperrt*. Du bekommst sie über *Erfolge* - welche, " +
                              "steht in der Kapsel unter UNLOCKS.",
                        TipIcon = () => GameHudSkin.Lock,
                        Draw = DrawCharacterKit,
                    },
                },
            },
            new TutorialTopicDef
            {
                Topic = TutorialTopic.Skilltree, Key = "skilltree", Title = "SKILLTREE",
                Icon = () => GameHudSkin.Gem,
                Pages = new[]
                {
                    new TutorialPageDef
                    {
                        Head = "Vier Pfade",
                        Body = "Jeder Charakter hat seinen eigenen Skilltree mit vier Pfaden: *Kampf*, " +
                               "*Geist*, *Wissen* und *Glück*. Links wählst du den Pfad, daneben die Knoten. " +
                               "Ein Knoten lässt sich erst lernen, wenn der davor gelernt ist.",
                        Draw = DrawPaths,
                    },
                    new TutorialPageDef
                    {
                        Head = "Skillpunkte sammeln",
                        Body = "Alle XP, die du im Lauf einsammelst, landen auch auf dem Konto deines " +
                               "Charakters. Jedes *Charakter-Level* bringt einen *Skillpunkt*. Klick einen " +
                               "Knoten an und drück *LERNEN*.",
                        Tip = "Besiegt ein Charakter einen Boss zum ersten Mal, gibt es ein *Extra-Level*. " +
                              "Mit *RESET* bekommst du alle Punkte zurück.",
                        TipIcon = () => GameHudSkin.Gem,
                        Draw = DrawPoints,
                    },
                    new TutorialPageDef
                    {
                        Head = "Alle lernen mit",
                        Body = "Du musst nicht jeden Charakter einzeln hochspielen: Während du mit einem " +
                               "spielst, sammeln *alle anderen passiv ein wenig XP* mit. Wechselst du, " +
                               "warten oft schon ein paar Skillpunkte auf dich.",
                        Tip = "Am schnellsten levelt aber immer der Charakter, mit dem du gerade spielst.",
                        TipIcon = () => GameHudSkin.Bulb,
                        Draw = DrawShared,
                    },
                },
            },
            new TutorialTopicDef
            {
                Topic = TutorialTopic.Workbench, Key = "workbench", Title = "WERKBANK",
                Icon = () => GameHudSkin.Hammer,
                Pages = new[]
                {
                    new TutorialPageDef
                    {
                        Head = "Der Verteiler",
                        Body = "An der Werkbank legst du fest, *woraus* beim Level-Up gezogen wird. " +
                               "Ausgerüstet ist damit noch nichts - es ist der *Topf* für deine " +
                               "Level-Up-Karten. Was nicht drinliegt, kommt im Lauf nicht vor.",
                        Draw = DrawPool,
                    },
                    new TutorialPageDef
                    {
                        Head = "Plätze füllen",
                        Body = "*Klick* eine Kachel in der *Bibliothek* rechts, um sie in den Verteiler zu " +
                               "legen. Klickst du sie im Verteiler an, fliegt sie wieder raus. Platz 1 " +
                               "gehört fest der Startwaffe deines Charakters.",
                        Tip = "Jeder Slot im Lauf öffnet *3 Plätze* - mehr Slots gibt es im Shop. Was du " +
                              "leer lässt, wird beim Start *zufällig* aufgefüllt.",
                        TipIcon = () => GameHudSkin.Dice,
                        Draw = DrawFill,
                    },
                    new TutorialPageDef
                    {
                        Head = "Evolutionen",
                        Body = "Zwei passende Zutaten auf *Maximalstufe* verschmelzen im Lauf zu einer " +
                               "*Evolution*. Die Leiste unten zeigt, welche Evos dein Build schaffen kann - " +
                               "*alle* öffnet die ganze Liste.",
                        Tip = "Klick unten auf eine *Evo*: Dann legt die Werkbank die passenden Waffen " +
                              "und Buffs *von selbst* in den Verteiler - solange noch genug Platz frei ist.",
                        TipIcon = () => GameHudSkin.Bulb,
                        Draw = DrawEvo,
                    },
                    new TutorialPageDef
                    {
                        Head = "Builds speichern",
                        Body = "Mit *1* bis *4* wechselst du zwischen gespeicherten Builds - der zuletzt " +
                               "gewählte geht mit in den Lauf. *ZUFÄLLIG* würfelt einen Build aus, " +
                               "*LEEREN* räumt ihn ab.",
                        Tip = "Weitere Build-Plätze kannst du im *Shop* kaufen.",
                        TipIcon = () => GameHudSkin.Coin,
                        Draw = DrawPresets,
                    },
                },
            },
            new TutorialTopicDef
            {
                Topic = TutorialTopic.LevelSelect, Key = "levelselect", Title = "LEVELAUSWAHL",
                Icon = () => GameHudSkin.Pin,
                Pages = new[]
                {
                    new TutorialPageDef
                    {
                        Head = "Die Reiseroute",
                        Body = "Wähl eine *Station* auf der Route und stell ein, ob du *STORY* oder " +
                               "*ENDLESS* spielen willst. Los geht es erst mit *SPIELEN* (oder Enter).",
                        Tip = "Rechts im Steckbrief stehen der Boss der Welt, ihre Gegnerarten und deine " +
                              "Bestzeit.",
                        TipIcon = () => GameHudSkin.Clock,
                        Draw = DrawRoute,
                    },
                    new TutorialPageDef
                    {
                        Head = "Story und Endless",
                        Body = "*Story*: Überlebe, bis der Boss kommt, und besiege ihn - dann öffnet sich " +
                               "die *nächste Welt*. *Endless*: Hast du alle fünf Welten geschafft und das Herz des " +
                               "Verkohlten besiegt, spielst du jede Welt ohne Ende, so lange du durchhältst.",
                        Tip = "Gold gibt es nach *jedem* Lauf - auch wenn du verlierst.",
                        TipIcon = () => GameHudSkin.Coin,
                        Draw = DrawModes,
                    },
                },
            },
            new TutorialTopicDef
            {
                Topic = TutorialTopic.Shop, Key = "shop", Title = "SHOP",
                Icon = () => GameHudSkin.Coin,
                Pages = new[]
                {
                    new TutorialPageDef
                    {
                        Head = "Ricos Laden",
                        Body = "Hier gibst du das Gold aus deinen Läufen aus. Unter *UPGRADES* gibt es " +
                               "dauerhafte Verbesserungen - mehr Plätze, Rerolls, Start-XP und mehr. Unter " +
                               "*FREISCHALTEN* kaufst du neue Waffen und Buffs.",
                        Tip = "Verkauft? Mit *RESET* bekommst du das Gold für alle Käufe zurück und kannst " +
                              "neu verteilen.",
                        TipIcon = () => GameHudSkin.Bulb,
                        Draw = DrawShop,
                    },
                },
            },
            new TutorialTopicDef
            {
                Topic = TutorialTopic.Capsule, Key = "capsule", Title = "KAPSEL",
                Icon = () => GameHudSkin.Book,
                Pages = new[]
                {
                    new TutorialPageDef
                    {
                        Head = "Erfolge und Unlocks",
                        Body = "In der Kapsel stehen deine *Erfolge* und was sie dir *freischalten* - neue " +
                               "Charaktere, Welten und Shop-Waren. Wähl einen Eintrag, um zu sehen, wie du " +
                               "ihn bekommst.",
                        Tip = "Lernst du im Skilltree das *Bestiarium*, zählt die Kapsel jede Gegnerart mit. " +
                              "Je mehr du von einer Art besiegst, desto mehr Schaden machst du gegen sie.",
                        TipIcon = () => GameHudSkin.Skull,
                        Draw = DrawCapsule,
                    },
                },
            },
        };
    }

    // ==================================================================
    //  Hilfen
    // ==================================================================

    private static string T(string key, string fallback) => Loc.Get("ui.tutorial.pic." + key, fallback);

    private static int Me() => Mathf.Clamp(Shop.SkinIndex, 0, Mathf.Max(0, Characters.Count - 1));

    /// <summary>Die n-te Figur ausser <paramref name="skip"/>.</summary>
    private static int Other(int skip, int n)
    {
        int count = Mathf.Max(1, Characters.Count);
        int found = 0;
        for (int i = 0; i < count; i++)
        {
            if (i == skip) continue;
            if (found++ == n) return i;
        }
        return 0;
    }

    private static int FirstLocked()
    {
        for (int i = 0; i < Characters.Count; i++)
            if (!Characters.IsAvailable(i)) return i;
        return Mathf.Max(0, Characters.Count - 1);
    }

    private static Sprite Icon14(string id) => WorkbenchIcons.Get(id, 14);
    private static Sprite Icon10(string id) => WorkbenchIcons.Get(id, 10);

    /// <summary>Waffen fuer die Bildtafeln - nie die Startwaffe des gewaehlten Charakters doppelt, nie eine fremde.</summary>
    private static string[] SomeWeapons(int count)
    {
        string start = Characters.StartWeaponId(Me());
        string[] wanted =
        {
            "cookie_saw", "boba_gun", "shurikookie", "celestial_star", "fire_ball", "jam_jar", "candy_bomb",
        };
        var list = new List<string>();
        foreach (string id in wanted)
        {
            if (id == start) continue;
            // Startwaffen der anderen bekommt dieser Charakter nie - nicht als Beispiel zeigen.
            if (!Characters.IsWeaponAllowed(id, Me())) continue;
            list.Add(id);
            if (list.Count == count) break;
        }
        return list.ToArray();
    }

    private static readonly string[] SomeBuffs = { "buff_damage", "buff_crit_chance", "buff_move_speed", "buff_armor" };

    private static void Show(Graphic g, bool on)
    {
        if (g != null && g.enabled != on) g.enabled = on;
    }

    private static Color Darker(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a);

    /// <summary>Kachel der Werkbank: Rahmen + Symbol, beides 2x.</summary>
    private sealed class Slot
    {
        public Image Frame, Icon;

        public void Set(string frame, Sprite icon, Color tint)
        {
            Frame.sprite = TutorialPicture.WorkbenchSprite(frame);
            Icon.sprite = icon;
            Icon.color = tint;
            Show(Icon, icon != null);
        }
    }

    private static Slot MakeSlot(TutorialPicture p, float x, float y, string frame, Sprite icon)
    {
        var s = new Slot
        {
            Frame = p.Workbench(x, y, frame),
            Icon = p.Img(x, y, 28, 28, icon, Color.white),
        };
        s.Set(frame, icon, Color.white);
        return s;
    }

    // ==================================================================
    //  Charaktere
    // ==================================================================

    /// <summary>Drei Portraets: Klick sieht an, WAEHLEN nimmt.</summary>
    private static void DrawRoster(TutorialPicture p)
    {
        p.Loop = 7f;

        int n = Mathf.Clamp(Characters.Count, 1, 3);
        float[] xs = { 2, 47, 92 };
        const float py = 8;

        for (int i = 0; i < n; i++)
        {
            p.Img(xs[i], py, 42, 42, GameHudSkin.SlotEmpty(42, 42), Color.white);
            p.Portrait(xs[i], py, i);
        }

        Image frame = p.Img(xs[0] - 3, py - 3, 48, 48, GameHudSkin.SlotFrame(GameHudSkin.SlotLook.Gold),
                            Color.white, true);
        Image check = p.Sprite(0, 0, GameHudSkin.Check);
        TextMeshProUGUI name = p.Text(0, 57, W, Characters.DisplayName(0), GameHudSkin.Cream);

        FakeButton choose = p.Button(36, 76, 64, 18, Loc.Get("ui.charselect.choose", "WÄHLEN"),
                                     GameHudSkin.ButtonLook.Gold);
        string chooseText = Loc.Get("ui.charselect.choose", "WÄHLEN");
        string pickedText = Loc.Get("ui.charselect.picked", "GEWÄHLT!");

        int last = n - 1;
        p.Burst(4.0f, xs[last] + 21, py + 21, 0.6f);

        p.Tick(t =>
        {
            int viewed = Mathf.Min(t < 0.8f ? 0 : t < 2.0f ? 1 : 2, last);
            int chosen = t >= 4.0f && t < 6.6f ? last : 0;

            TutorialPicture.Place(frame, xs[viewed] - 3, py - 3);
            name.text = Characters.DisplayName(viewed);
            TutorialPicture.Place(check, xs[chosen] + 42 - 8, py - 2);

            bool over = t >= 3.4f && t < 6.4f;
            if (t >= 3.95f && t < 4.13f) choose.SetLook(GameHudSkin.ButtonLook.Pressed);
            else if (t >= 4.13f && t < 6.6f) choose.SetLook(GameHudSkin.ButtonLook.Active);
            else choose.SetLook(over ? GameHudSkin.ButtonLook.GoldHover : GameHudSkin.ButtonLook.Gold);
            choose.SetText(t >= 4.13f && t < 6.6f ? pickedText : chooseText);
        });

        p.Cursor(TutorialPicture.At(0f, 118, 104),
                 TutorialPicture.ClickAt(0.8f, 68, 30),
                 TutorialPicture.ClickAt(2.0f, 113, 30),
                 TutorialPicture.ClickAt(3.95f, 68, 85),
                 TutorialPicture.At(6.4f, 68, 85),
                 TutorialPicture.At(7.0f, 118, 104));
    }

    /// <summary>Oben: Charakter -> Startwaffe. Unten: gesperrter Charakter, ein Erfolg schaltet ihn frei.</summary>
    private static void DrawCharacterKit(TutorialPicture p)
    {
        p.Loop = 6f;
        int me = Me();

        p.Img(3, 3, 48, 48, GameHudSkin.SlotFrame(GameHudSkin.SlotLook.Wood), Color.white, true);
        p.Portrait(6, 6, me);

        Sprite arrow = GameHudSkin.Arrow;
        p.Sprite(58, 27 - arrow.rect.height / 2f, arrow);

        Slot weapon = MakeSlot(p, 76, 13, "slot_locked", Icon14(Characters.StartWeaponId(me)));
        p.Text(56, 43, 78, Loc.Get("ui.charselect.weapon", "STARTWAFFE"), GameHudSkin.Gold);

        p.Img(8, 60, 120, 2, GameHudSkin.Rule, Color.white, true);

        int locked = FirstLocked();
        Image lockedFrame = p.Img(3, 64, 48, 48, GameHudSkin.SlotFrame(GameHudSkin.SlotLook.Stone),
                                  Color.white, true);
        Image portrait = p.Portrait(6, 67, locked);

        Sprite lockSprite = GameHudSkin.Lock;
        Image lockImg = p.Sprite(27 - Mathf.Floor(lockSprite.rect.width / 2f),
                                 88 - Mathf.Floor(lockSprite.rect.height / 2f), lockSprite);

        TextMeshProUGUI status = p.Text(56, 72, 78, "", GameHudSkin.StoneLight);
        p.Text(56, 86, 78, T("byachievement", "PER ERFOLG"), GameHudSkin.ParchDark);

        Sprite starSprite = GameHudSkin.Star;
        Image star = p.Sprite(0, 0, starSprite);
        float sw = starSprite.rect.width, sh = starSprite.rect.height;

        p.Burst(2.5f, 27, 88, 0.6f);

        string lockedText = Loc.Get("ui.charselect.locked", "GESPERRT");
        string freeText = T("unlocked", "FREI!");
        Color dark = new Color(0.2f, 0.17f, 0.2f, 1f);

        p.Tick(t =>
        {
            // Die Startwaffe wippt ein wenig, damit das Auge hinsieht.
            TutorialPicture.Place(weapon.Icon, 76, 13 + Mathf.Round(Mathf.Sin(t * 3f) * 1.2f));

            bool free = t >= 2.5f && t < 5.6f;
            portrait.color = free ? Color.white : dark;
            Show(lockImg, !free);
            lockedFrame.sprite = GameHudSkin.SlotFrame(free ? GameHudSkin.SlotLook.Gold : GameHudSkin.SlotLook.Stone);
            status.text = free ? freeText : lockedText;
            status.color = free ? (Color)GameHudSkin.Mint : GameHudSkin.StoneLight;

            // Stern wartet unten rechts, fliegt dann aufs Portraet.
            Vector2 home = new Vector2(112, 98 - Mathf.Round(Mathf.Sin(t * 4f)));
            Vector2 target = new Vector2(27, 88);
            float f = TutorialPicture.Ease(t, 1.8f, 2.5f);
            Vector2 at = TutorialPicture.Fly(home, target, f, 14f);
            Show(star, t < 2.5f);
            TutorialPicture.Place(star, at.x - Mathf.Floor(sw / 2f), at.y - Mathf.Floor(sh / 2f));
        });
    }

    // ==================================================================
    //  Skilltree
    // ==================================================================

    private static string CategoryName(SkillCategory c) =>
        Loc.Get("skill.branch." + SkillCategoryStyle.IdOf(c) + ".name", SkillCategoryStyle.For(c).Name);

    /// <summary>Vier Pfadknoepfe links, rechts ein kleiner Baum, der sich im Takt freischaltet.</summary>
    private static void DrawPaths(TutorialPicture p)
    {
        p.Loop = 8f;

        var cats = (SkillCategory[])System.Enum.GetValues(typeof(SkillCategory));
        int count = Mathf.Min(4, cats.Length);

        var buttons = new Image[count];
        var labels = new TextMeshProUGUI[count];
        for (int i = 0; i < count; i++)
        {
            float y = 8 + i * 25;
            buttons[i] = p.Img(4, y, 44, 21, GameHudSkin.TintButton(false), SkillCategoryStyle.For(cats[i]).Color, true);
            labels[i] = OptionsKit.Label("Cat", p.Root, 4, y, 44, 20, CategoryName(cats[i]), p.Font,
                                         OptionsKit.SizeText, GameHudSkin.Cream, TextAlignmentOptions.Center);
        }

        var shapes = new SkillShapeSprites(13);
        p.Own(shapes);

        // Knoten (oben links, 13x13) und wer wessen Vorgaenger ist.
        Vector2[] pos =
        {
            new Vector2(58, 48),                                            // 0 Start
            new Vector2(78, 48), new Vector2(98, 48), new Vector2(118, 48), // 1-3 Mitte
            new Vector2(78, 22), new Vector2(98, 22),                       // 4-5 oben
            new Vector2(78, 74), new Vector2(98, 74),                       // 6-7 unten
        };
        SkillShape[] form =
        {
            SkillShape.Kreis, SkillShape.Raute, SkillShape.Rechteck, SkillShape.Stern,
            SkillShape.Sechseck, SkillShape.Dreieck, SkillShape.Kreuz, SkillShape.Kreis,
        };
        // Reihenfolge, in der gelernt wird - immer erst der Vorgaenger.
        int[] order = { 0, 1, 4, 2, 6, 5, 3, 7 };

        // Verbindungen: Rechteck + Knoten, den sie erreicht.
        Rect[] lineRects =
        {
            new Rect(64, 53, 20, 2), new Rect(84, 53, 20, 2), new Rect(104, 53, 20, 2),
            new Rect(83, 28, 2, 26), new Rect(84, 27, 20, 2),
            new Rect(83, 54, 2, 26), new Rect(84, 79, 20, 2),
        };
        int[] lineTo = { 1, 2, 3, 4, 5, 6, 7 };

        var lines = new Image[lineRects.Length];
        for (int i = 0; i < lines.Length; i++)
            lines[i] = p.Img(lineRects[i].x, lineRects[i].y, lineRects[i].width, lineRects[i].height,
                             GameHudSkin.White, GameHudSkin.StoneDark);

        var fills = new Image[pos.Length];
        var rims = new Image[pos.Length];
        for (int i = 0; i < pos.Length; i++)
        {
            fills[i] = p.Img(pos[i].x, pos[i].y, shapes.Dim, shapes.Dim, shapes.Fill(form[i]), Color.white);
            rims[i] = p.Img(pos[i].x, pos[i].y, shapes.Dim, shapes.Dim, shapes.Outline(form[i]), Color.white);
        }

        var learnedAt = new float[pos.Length];

        p.Tick(t =>
        {
            int sel = Mathf.FloorToInt(t / 2f) % count;
            float phase = t % 2f;
            Color col = SkillCategoryStyle.For(cats[sel]).Color;

            for (int i = 0; i < count; i++)
            {
                bool on = i == sel;
                buttons[i].sprite = GameHudSkin.TintButton(on);
                Color c = SkillCategoryStyle.For(cats[i]).Color;
                buttons[i].color = on ? c : Darker(c, 0.7f);
                float x = on ? 8 : 4;
                TutorialPicture.Place(buttons[i], x, 8 + i * 25);
                TutorialPicture.Place(labels[i], x, 8 + i * 25);
                labels[i].color = on ? (Color)GameHudSkin.Cream : GameHudSkin.ParchDark;
            }

            int learned = 1 + Mathf.Clamp(Mathf.FloorToInt((phase - 0.2f) / 0.22f), 0, order.Length - 1);
            var isLearned = new bool[pos.Length];
            for (int k = 0; k < learned; k++)
            {
                isLearned[order[k]] = true;
                learnedAt[order[k]] = 0.2f + (k - 1) * 0.22f;
            }

            for (int i = 0; i < pos.Length; i++)
            {
                bool on = isLearned[i];
                bool fresh = on && i != 0 && phase - learnedAt[i] < 0.12f;
                fills[i].color = !on ? (Color)GameHudSkin.StoneDark : fresh ? (Color)GameHudSkin.Cream : col;
                rims[i].color = on ? Darker(col, 0.5f) : (Color)GameHudSkin.Ink;
            }

            for (int i = 0; i < lines.Length; i++)
                lines[i].color = isLearned[lineTo[i]] ? col : (Color)GameHudSkin.StoneDark;
        });
    }

    /// <summary>XP-Balken laeuft voll -> +1 Skillpunkt -> Knoten waehlen -> LERNEN.</summary>
    private static void DrawPoints(TutorialPicture p)
    {
        p.Loop = 7f;
        int me = Me();

        p.Img(3, 3, 48, 48, GameHudSkin.SlotFrame(GameHudSkin.SlotLook.Wood), Color.white, true);
        p.Portrait(6, 6, me);

        string lvFormat = Loc.Get("ui.skilltree.charlevel", "LV {0}");
        TextMeshProUGUI level = p.Text(57, 6, 76, "", GameHudSkin.Gold, TextAlignmentOptions.Left);
        p.Img(57, 21, 76, 8, GameHudSkin.BarFrame, Color.white, true);
        Image fill = p.Img(59, 23, 0, 4, GameHudSkin.White, GameHudSkin.Gold);

        Image gem = p.Sprite(57, 36, GameHudSkin.Gem);
        TextMeshProUGUI point = p.Text(57 + GameHudSkin.Gem.rect.width + 3, 34, 70, T("point", "+1 SKILLPUNKT"),
                                       GameHudSkin.Mint, TextAlignmentOptions.Left);

        var shapes = new SkillShapeSprites(19);
        p.Own(shapes);
        const float nx = 22, ny = 72;
        Color kampf = SkillCategoryStyle.For(SkillCategory.Kampf).Color;
        Image ring = p.Img(nx - 3, ny - 3, 25, 25, GameHudSkin.Ring, GameHudSkin.Gold, true);
        Image nodeFill = p.Img(nx, ny, shapes.Dim, shapes.Dim, shapes.Fill(SkillShape.Raute), GameHudSkin.StoneDark);
        Image nodeRim = p.Img(nx, ny, shapes.Dim, shapes.Dim, shapes.Outline(SkillShape.Raute), GameHudSkin.Ink);

        FakeButton learn = p.Button(54, 72, 76, 19, Loc.Get("ui.skilltree.learn", "LERNEN"),
                                    GameHudSkin.ButtonLook.Gold, GameHudSkin.Gem);

        p.Burst(2.0f, 131, 25, 0.5f);
        p.Burst(4.6f, nx + 9, ny + 9, 0.6f);

        p.Tick(t =>
        {
            bool up = t >= 2.0f;
            level.text = string.Format(lvFormat, up ? 4 : 3);

            float w = up ? 72f * 0.25f * TutorialPicture.Ease(t, 2.2f, 7f) : 72f * TutorialPicture.Ease(t, 0.2f, 2.0f);
            OptionsKit.Move(fill.rectTransform, 59, 23, Mathf.Round(w), 4);
            Show(fill, w >= 1f);

            bool havePoint = t >= 2.1f && t < 4.6f;
            float rise = Mathf.Round(4f * (1f - TutorialPicture.Ease(t, 2.1f, 2.4f)));
            Show(gem, havePoint);
            Show(point, havePoint);
            TutorialPicture.Place(gem, 57, 36 + rise);
            TutorialPicture.Place(point, 57 + GameHudSkin.Gem.rect.width + 3, 34 + rise);

            bool selected = t >= 3.4f;
            bool learned = t >= 4.6f;
            Show(ring, selected);
            nodeFill.color = learned ? kampf : (Color)GameHudSkin.StoneDark;
            nodeRim.color = learned ? Darker(kampf, 0.5f) : (Color)GameHudSkin.Ink;

            bool over = t >= 4.1f && t < 6.4f;
            if (t >= 4.5f && t < 4.68f) learn.SetLook(GameHudSkin.ButtonLook.Pressed);
            else if (t >= 4.68f) learn.SetLook(GameHudSkin.ButtonLook.Disabled);
            else learn.SetLook(over ? GameHudSkin.ButtonLook.GoldHover : GameHudSkin.ButtonLook.Gold);
        });

        p.Cursor(TutorialPicture.At(0f, 122, 104),
                 TutorialPicture.At(2.9f, 122, 104),
                 TutorialPicture.ClickAt(3.4f, nx + 9, ny + 9),
                 TutorialPicture.ClickAt(4.5f, 96, 81),
                 TutorialPicture.At(6.4f, 96, 81),
                 TutorialPicture.At(7.0f, 122, 104));
    }

    /// <summary>Der aktive Charakter sammelt XP, ein Teil davon fliesst zu den anderen.</summary>
    private static void DrawShared(TutorialPicture p)
    {
        p.Loop = 5f;

        int me = Me();
        int a = Other(me, 0), b = Other(me, 1);

        p.Img(44, 2, 48, 48, GameHudSkin.SlotFrame(GameHudSkin.SlotLook.Gold), Color.white, true);
        p.Portrait(47, 5, me);
        p.Text(94, 18, 42, Loc.Get("ui.charselect.active", "AKTIV"), GameHudSkin.Gold, TextAlignmentOptions.Left);

        p.Img(40, 52, 56, 7, GameHudSkin.BarFrame, Color.white, true);
        Image mainFill = p.Img(42, 54, 0, 3, GameHudSkin.White, GameHudSkin.Gold);

        p.Portrait(10, 62, a);
        p.Portrait(84, 62, b);
        p.Img(8, 105, 46, 6, GameHudSkin.BarFrame, Color.white, true);
        p.Img(82, 105, 46, 6, GameHudSkin.BarFrame, Color.white, true);
        Image fillA = p.Img(10, 107, 0, 2, GameHudSkin.White, GameHudSkin.Mint);
        Image fillB = p.Img(84, 107, 0, 2, GameHudSkin.White, GameHudSkin.Mint);

        const int sparkCount = 6;
        var sparks = new Image[sparkCount];
        for (int i = 0; i < sparkCount; i++) sparks[i] = p.Img(0, 0, 6, 6, GameHudSkin.Sparkle, Color.white);

        Vector2 from = new Vector2(68, 58);
        Vector2[] to = { new Vector2(31, 104), new Vector2(105, 104) };

        p.Tick(t =>
        {
            float main = (t % 1.25f) / 1.25f;
            OptionsKit.Move(mainFill.rectTransform, 42, 54, Mathf.Round(52f * main), 3);
            Show(mainFill, main * 52f >= 1f);

            float slow = Mathf.Round(42f * (t / 5f));
            OptionsKit.Move(fillA.rectTransform, 10, 107, slow, 2);
            OptionsKit.Move(fillB.rectTransform, 84, 107, slow, 2);
            Show(fillA, slow >= 1f);
            Show(fillB, slow >= 1f);

            for (int i = 0; i < sparkCount; i++)
            {
                float ph = ((t + i * 0.42f) % 1.25f) / 1.25f;
                Vector2 dest = to[i % 2];
                // Leichter Bogen nach aussen, damit die Funken nicht durch die Portraets fliegen.
                Vector2 v = Vector2.Lerp(from, dest, ph);
                v.x += (i % 2 == 0 ? -1f : 1f) * Mathf.Sin(ph * Mathf.PI) * 10f;
                Show(sparks[i], ph < 0.92f);
                TutorialPicture.Place(sparks[i], v.x - 3, v.y - 3);
            }
        });
    }

    // ==================================================================
    //  Werkbank
    // ==================================================================

    /// <summary>Verteiler oben, darunter werden Level-Up-Karten daraus gezogen.</summary>
    private static void DrawPool(TutorialPicture p)
    {
        p.Loop = 6f;

        string[] weapons = SomeWeapons(2);
        string[] pool = { Characters.StartWeaponId(Me()), weapons[0], weapons[1], SomeBuffs[0] };

        p.Text(0, 1, W, T("pool", "VERTEILER"), GameHudSkin.Gold);

        var highlight = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            float x = 6 + i * 32;
            string frame = i == 0 ? "slot_locked" : i == 3 ? "slot_buff" : "slot_weapon";
            MakeSlot(p, x, 14, frame, Icon14(pool[i]));
            highlight[i] = p.Workbench(x, 14, "slot_highlight");
        }

        Sprite down = GameHudSkin.ArrowDown;
        p.Sprite(Mathf.Round(68 - down.rect.width / 2f), 46, down);
        p.Text(0, 56, W, T("levelup", "LEVEL-UP"), GameHudSkin.ParchDark);

        int[][] deals = { new[] { 1, 3, 2 }, new[] { 0, 1, 3 }, new[] { 2, 0, 1 } };

        var cards = new Image[3];
        var cardIcons = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            float x = 16 + i * 36;
            cards[i] = p.Img(x, 72, 32, 36, GameHudSkin.Card, Color.white, true);
            cardIcons[i] = p.Img(x + 2, 76, 28, 28, null, Color.white);
        }

        p.Tick(t =>
        {
            int d = Mathf.FloorToInt(t / 2f) % deals.Length;
            float phase = t % 2f;
            int[] deal = deals[d];

            var used = new bool[4];
            for (int i = 0; i < 3; i++)
            {
                used[deal[i]] = true;
                float drop = Mathf.Round(-7f * (1f - TutorialPicture.Ease(phase, 0.05f + i * 0.1f, 0.3f + i * 0.1f)));
                bool on = phase >= 0.05f + i * 0.1f;
                float x = 16 + i * 36;
                Show(cards[i], on);
                Show(cardIcons[i], on);
                TutorialPicture.Place(cards[i], x, 72 + drop);
                TutorialPicture.Place(cardIcons[i], x + 2, 76 + drop);
                cardIcons[i].sprite = Icon14(pool[deal[i]]);
            }

            for (int i = 0; i < 4; i++) Show(highlight[i], used[i] && phase < 1.8f);
        });
    }

    /// <summary>Bibliothek -> Verteiler per Klick, und wieder heraus.</summary>
    private static void DrawFill(TutorialPicture p)
    {
        p.Loop = 7.5f;

        string start = Characters.StartWeaponId(Me());
        string[] weapons = SomeWeapons(3);
        string[] lib = { weapons[0], weapons[1], weapons[2], SomeBuffs[0] };

        p.Text(0, 3, 68, T("pool", "VERTEILER"), GameHudSkin.Gold);
        p.Text(68, 3, 68, T("library", "BIBLIOTHEK"), GameHudSkin.Gold);

        Vector2[] poolPos = { new Vector2(4, 18), new Vector2(36, 18), new Vector2(4, 50), new Vector2(36, 50) };
        Vector2[] libPos = { new Vector2(72, 18), new Vector2(104, 18), new Vector2(72, 50), new Vector2(104, 50) };

        var pool = new Slot[4];
        for (int i = 0; i < 4; i++)
            pool[i] = MakeSlot(p, poolPos[i].x, poolPos[i].y, i == 0 ? "slot_locked" : "slot_empty",
                               i == 0 ? Icon14(start) : null);

        var library = new Slot[4];
        for (int i = 0; i < 4; i++)
            library[i] = MakeSlot(p, libPos[i].x, libPos[i].y, i == 3 ? "slot_buff" : "slot_weapon", Icon14(lib[i]));

        Sprite dice = GameHudSkin.Dice;
        p.Sprite(12, 90, dice);
        p.Text(14 + dice.rect.width, 88, 100, T("random", "Leer = Zufall"), GameHudSkin.ParchDark,
               TextAlignmentOptions.Left);

        Image fly = p.Img(0, 0, 28, 28, null, Color.white);

        // Fluege: von, nach, Start, welches Symbol. Der vierte nimmt wieder heraus.
        var flights = new[]
        {
            new { From = libPos[0], To = poolPos[1], At = 1.0f, Lib = 0 },
            new { From = libPos[3], To = poolPos[3], At = 2.4f, Lib = 3 },
            new { From = libPos[1], To = poolPos[2], At = 3.8f, Lib = 1 },
            new { From = poolPos[1], To = libPos[0], At = 5.4f, Lib = 0 },
        };
        const float flightTime = 0.45f;

        Color used = new Color(0.45f, 0.45f, 0.45f, 1f);

        p.Tick(t =>
        {
            bool in1 = t >= 1.0f + flightTime && t < 5.4f;
            bool in3 = t >= 2.4f + flightTime;
            bool in2 = t >= 3.8f + flightTime;

            pool[1].Set(in1 ? "slot_weapon" : "slot_empty", in1 ? Icon14(lib[0]) : null, Color.white);
            pool[3].Set(in3 ? "slot_buff" : "slot_empty", in3 ? Icon14(lib[3]) : null, Color.white);
            pool[2].Set(in2 ? "slot_weapon" : "slot_empty", in2 ? Icon14(lib[1]) : null, Color.white);

            library[0].Icon.color = t >= 1.0f && t < 5.4f + flightTime ? used : Color.white;
            library[3].Icon.color = t >= 2.4f ? used : Color.white;
            library[1].Icon.color = t >= 3.8f ? used : Color.white;

            bool flying = false;
            foreach (var f in flights)
            {
                if (t < f.At || t >= f.At + flightTime) continue;
                Vector2 v = TutorialPicture.Fly(f.From, f.To, TutorialPicture.Ease(t, f.At, f.At + flightTime), 10f);
                fly.sprite = Icon14(lib[f.Lib]);
                TutorialPicture.Place(fly, v.x, v.y);
                flying = true;
            }
            Show(fly, flying);
        });

        p.Cursor(TutorialPicture.At(0f, 122, 106),
                 TutorialPicture.ClickAt(1.0f, 86, 32),
                 TutorialPicture.ClickAt(2.4f, 118, 64),
                 TutorialPicture.ClickAt(3.8f, 118, 32),
                 TutorialPicture.ClickAt(5.4f, 50, 32),
                 TutorialPicture.At(6.8f, 50, 32),
                 TutorialPicture.At(7.5f, 122, 106));
    }

    /// <summary>Evo-Chip anklicken -> beide Zutaten fliegen in den Verteiler.</summary>
    private static void DrawEvo(TutorialPicture p)
    {
        p.Loop = 7f;

        EvoDef evo = WeaponCatalog.RecipeBoomerang;
        string a = evo.IngredientA, b = evo.IngredientB;

        const float cx = 30, cy = 4;
        Image chip = p.Workbench(cx, cy, "evo_chip_pending");

        float[] slotDx = { 2, 28, 54 };
        string[] ids = { a, b, evo.Id };
        for (int k = 0; k < 3; k++)
        {
            p.Workbench(cx + slotDx[k], cy + 4, "slot_evo");
            p.Img(cx + slotDx[k], cy + 4, 20, 20, Icon10(ids[k]), Color.white);
        }
        p.Workbench(cx + 22, cy + 10, "sign_plus");
        p.Workbench(cx + 48, cy + 10, "sign_equals");

        TextMeshProUGUI name = p.Text(cx + 4, cy + 27, 68, evo.Name, GameHudSkin.ParchDark);
        TextMeshProUGUI state = p.Text(cx + 76 + 1, cy + 17, 28, "", GameHudSkin.ParchDark,
                                       TextAlignmentOptions.Left);

        p.Text(0, 57, W, T("pool", "VERTEILER"), GameHudSkin.Gold);

        string start = Characters.StartWeaponId(Me());
        var pool = new Slot[4];
        for (int i = 0; i < 4; i++)
            pool[i] = MakeSlot(p, 6 + i * 32, 72, i == 0 ? "slot_locked" : "slot_empty", i == 0 ? Icon14(start) : null);

        Image flyA = p.Img(0, 0, 28, 28, Icon14(a), Color.white);
        Image flyB = p.Img(0, 0, 28, 28, Icon14(b), Color.white);

        p.Burst(2.5f, cx + 38, cy + 24, 0.6f);

        string none = Loc.Get("ui.workbench.evo.none", "0/2");
        string ready = Loc.Get("ui.workbench.evo.ready", "2/2");

        Vector2 fromA = new Vector2(cx + slotDx[0] - 4, cy);
        Vector2 fromB = new Vector2(cx + slotDx[1] - 4, cy);
        Vector2 toA = new Vector2(38, 72), toB = new Vector2(102, 72);

        p.Tick(t =>
        {
            bool done = t >= 2.5f;
            chip.sprite = TutorialPicture.WorkbenchSprite(done ? "evo_chip_ok" : "evo_chip_pending");
            name.color = done ? (Color)GameHudSkin.Cream : GameHudSkin.ParchDark;
            state.text = done ? ready : none;
            state.color = done ? (Color)GameHudSkin.Mint : GameHudSkin.ParchDark;

            bool hasA = t >= 2.2f, hasB = t >= 2.5f;
            pool[1].Set(hasA ? "slot_weapon" : "slot_empty", hasA ? Icon14(a) : null, Color.white);
            pool[3].Set(hasB ? "slot_buff" : "slot_empty", hasB ? Icon14(b) : null, Color.white);

            float pa = TutorialPicture.Ease(t, 1.6f, 2.2f), pb = TutorialPicture.Ease(t, 1.9f, 2.5f);
            Show(flyA, t >= 1.6f && t < 2.2f);
            Show(flyB, t >= 1.9f && t < 2.5f);
            Vector2 va = TutorialPicture.Fly(fromA, toA, pa, 8f), vb = TutorialPicture.Fly(fromB, toB, pb, 8f);
            TutorialPicture.Place(flyA, va.x, va.y);
            TutorialPicture.Place(flyB, vb.x, vb.y);
        });

        p.Cursor(TutorialPicture.At(0f, 122, 106),
                 TutorialPicture.ClickAt(1.2f, cx + 64, cy + 42),
                 TutorialPicture.At(6.4f, cx + 64, cy + 42),
                 TutorialPicture.At(7.0f, 122, 106));
    }

    /// <summary>Builds 1-4 umschalten, ZUFAELLIG und LEEREN.</summary>
    private static void DrawPresets(TutorialPicture p)
    {
        p.Loop = 8f;

        var presets = new FakeButton[4];
        for (int i = 0; i < 4; i++)
        {
            float x = 16 + i * 28;
            presets[i] = i < 2
                ? p.Button(x, 6, 24, 18, (i + 1).ToString(), GameHudSkin.ButtonLook.Wood)
                : p.Button(x, 6, 24, 18, "", GameHudSkin.ButtonLook.Disabled, GameHudSkin.Lock);
        }

        string start = Characters.StartWeaponId(Me());
        string[] w = SomeWeapons(5);
        string[][] sets =
        {
            new[] { start, w[0], w[1], SomeBuffs[0] },
            new[] { start, w[2], w[3], SomeBuffs[1] },
            new[] { start, w[4], w[0], SomeBuffs[2] },
            new[] { start, null, null, null },
        };

        var pool = new Slot[4];
        for (int i = 0; i < 4; i++) pool[i] = MakeSlot(p, 6 + i * 32, 36, "slot_empty", null);

        FakeButton clear = p.Button(4, 76, 62, 18, Loc.Get("ui.workbench.btn.clear", "LEEREN"),
                                    GameHudSkin.ButtonLook.Wood);
        FakeButton random = p.Button(70, 76, 62, 18, Loc.Get("ui.workbench.btn.random", "ZUFÄLLIG"),
                                     GameHudSkin.ButtonLook.Wood);

        p.Tick(t =>
        {
            int active = t >= 1.0f && t < 6.0f ? 1 : 0;
            for (int i = 0; i < 2; i++)
            {
                bool pressed = (i == 1 && t >= 1.0f && t < 1.18f) || (i == 0 && t >= 6.0f && t < 6.18f);
                presets[i].SetLook(pressed ? GameHudSkin.ButtonLook.Pressed
                                 : i == active ? GameHudSkin.ButtonLook.Active : GameHudSkin.ButtonLook.Wood);
            }

            random.SetLook(t >= 2.6f && t < 2.78f ? GameHudSkin.ButtonLook.Pressed : GameHudSkin.ButtonLook.Wood);
            clear.SetLook(t >= 4.4f && t < 4.58f ? GameHudSkin.ButtonLook.Pressed : GameHudSkin.ButtonLook.Wood);

            string[] set;
            if (t < 1.0f || t >= 6.0f) set = sets[0];
            else if (t < 2.6f) set = sets[1];
            else if (t < 3.2f)
            {
                // Wuerfeln: die Plaetze flackern durch, bis sie stehen bleiben.
                int roll = Mathf.FloorToInt(t / 0.08f);
                set = new[] { start, w[roll % 5], w[(roll + 2) % 5], SomeBuffs[roll % SomeBuffs.Length] };
            }
            else if (t < 4.4f) set = sets[2];
            else set = sets[3];

            for (int i = 0; i < 4; i++)
            {
                string id = set[i];
                string frame = i == 0 ? "slot_locked" : id == null ? "slot_empty" : i == 3 ? "slot_buff" : "slot_weapon";
                pool[i].Set(frame, id != null ? Icon14(id) : null, Color.white);
            }
        });

        p.Cursor(TutorialPicture.At(0f, 122, 106),
                 TutorialPicture.ClickAt(1.0f, 56, 15),
                 TutorialPicture.ClickAt(2.6f, 101, 85),
                 TutorialPicture.ClickAt(4.4f, 35, 85),
                 TutorialPicture.ClickAt(6.0f, 28, 15),
                 TutorialPicture.At(7.4f, 28, 15),
                 TutorialPicture.At(8.0f, 122, 106));
    }

    // ==================================================================
    //  Levelauswahl
    // ==================================================================

    /// <summary>Station waehlen, Modus umschalten, SPIELEN - die Nadel huepft mit.</summary>
    private static void DrawRoute(TutorialPicture p)
    {
        p.Loop = 6.5f;

        float[] sx = { 10, 54, 98 };
        const float sy = 24, size = 28;
        Color[] world = { GameHudSkin.MintDark, GameHudSkin.Jam, GameHudSkin.IcingLight };
        string[] names =
        {
            T("world.forest", "WALD"), T("world.kitchen", "KÜCHE"), T("world.glacier", "GLETSCHER"),
        };

        // Punktepfad zwischen den Stationen: golden bis zur letzten offenen.
        for (int s = 0; s < 2; s++)
        {
            bool lit = s == 0;
            for (float x = sx[s] + size + 3; x < sx[s + 1] - 3; x += 6)
                p.Img(x, sy + 13, 4, 4, GameHudSkin.TrailDot(lit), Color.white);
        }

        var frames = new Image[3];
        for (int s = 0; s < 3; s++)
        {
            bool locked = s == 2;
            p.Img(sx[s] + 3, sy + 3, size - 6, size - 6, GameHudSkin.White, locked ? Darker(world[s], 0.35f) : world[s]);
            frames[s] = p.Img(sx[s], sy, size, size, GameHudSkin.SlotFrame(locked ? GameHudSkin.SlotLook.Stone
                                                                              : GameHudSkin.SlotLook.Wood),
                              Color.white, true);
            if (locked)
            {
                Sprite l = GameHudSkin.Lock;
                p.Sprite(sx[s] + 14 - Mathf.Floor(l.rect.width / 2f), sy + 14 - Mathf.Floor(l.rect.height / 2f), l);
            }
            else
            {
                p.ShadowText(sx[s], sy + 7, size, (s + 1).ToString(), GameHudSkin.Cream);
            }
        }

        TextMeshProUGUI name = p.Text(0, 55, W, names[0], GameHudSkin.Cream);

        FakeButton story = p.Button(6, 70, 60, 18, Loc.Get("ui.levelselect.story", "STORY"), GameHudSkin.ButtonLook.Active);
        FakeButton endless = p.Button(70, 70, 60, 18, Loc.Get("ui.levelselect.endless", "ENDLESS"),
                                      GameHudSkin.ButtonLook.Wood);
        FakeButton play = p.Button(28, 92, 80, 18, Loc.Get("ui.levelselect.play", "SPIELEN"), GameHudSkin.ButtonLook.Gold);

        Sprite pinSprite = GameHudSkin.Pin;
        Image pin = p.Sprite(0, 0, pinSprite, 2);
        float pw = pinSprite.rect.width * 2, ph = pinSprite.rect.height * 2;

        p.Burst(4.6f, sx[1] + 14, sy + 4, 0.6f);

        p.Tick(t =>
        {
            int sel = t >= 0.9f ? 1 : 0;
            for (int s = 0; s < 2; s++)
                frames[s].sprite = GameHudSkin.SlotFrame(s == sel ? GameHudSkin.SlotLook.Gold : GameHudSkin.SlotLook.Wood);
            name.text = names[sel];

            bool isEndless = t >= 2.1f && t < 3.3f;
            story.SetLook(isEndless ? GameHudSkin.ButtonLook.Wood : GameHudSkin.ButtonLook.Active);
            endless.SetLook(isEndless ? GameHudSkin.ButtonLook.Active : GameHudSkin.ButtonLook.Wood);

            if (t >= 4.5f && t < 4.68f) play.SetLook(GameHudSkin.ButtonLook.Pressed);
            else play.SetLook(t >= 4.0f && t < 6.0f ? GameHudSkin.ButtonLook.GoldHover : GameHudSkin.ButtonLook.Gold);

            // Nadel: steht ueber der Station, huepft beim Wechsel, springt bei SPIELEN ab.
            Vector2 a = new Vector2(sx[0] + 14, sy - 2), b = new Vector2(sx[1] + 14, sy - 2);
            Vector2 v = TutorialPicture.Fly(a, b, TutorialPicture.Ease(t, 0.9f, 1.3f), 3f);
            v.y -= Mathf.Round(Mathf.Abs(Mathf.Sin(t * 5f)) * 2f) * (t < 4.5f ? 1f : 0f);
            float jump = TutorialPicture.Ease(t, 4.55f, 5.0f);
            v.y -= jump * 30f;
            Show(pin, jump < 0.99f);
            TutorialPicture.Place(pin, v.x - Mathf.Floor(pw / 2f), v.y - ph);
        });

        p.Cursor(TutorialPicture.At(0f, 122, 106),
                 TutorialPicture.ClickAt(0.9f, sx[1] + 14, sy + 16),
                 TutorialPicture.ClickAt(2.1f, 100, 79),
                 TutorialPicture.ClickAt(3.3f, 36, 79),
                 TutorialPicture.ClickAt(4.5f, 68, 101),
                 TutorialPicture.At(6.0f, 68, 101),
                 TutorialPicture.At(6.5f, 122, 106));
    }

    /// <summary>Links Story (Boss faellt, naechste Welt), rechts Endless (Uhr laeuft und laeuft).</summary>
    private static void DrawModes(TutorialPicture p)
    {
        p.Loop = 6f;

        p.Img(67, 6, 2, 100, GameHudSkin.White, GameHudSkin.StoneDark);

        // ---- Story ----
        p.Text(0, 2, 67, Loc.Get("ui.levelselect.story", "STORY"), GameHudSkin.Gold);

        Sprite boss = Resources.Load<Sprite>("LevelSelect/Bosses/Glutwurz");
        if (boss == null)
        {
            Sprite[] any = Resources.LoadAll<Sprite>("LevelSelect/Bosses");
            if (any != null && any.Length > 0) boss = any[0];
        }

        p.Img(7, 15, 54, 54, GameHudSkin.SlotFrame(GameHudSkin.SlotLook.Wood), Color.white, true);
        Image bossImg = p.Img(10, 18, 48, 48, boss, Color.white);

        Sprite crown = GameHudSkin.Crown;
        float cw = crown.rect.width, ch = crown.rect.height;
        Image crownImg = p.Sprite(0, 0, crown, 2);

        TextMeshProUGUI win = p.Text(0, 72, 67, T("victory", "SIEG!"), GameHudSkin.Mint);
        TextMeshProUGUI next = p.Text(0, 86, 67, T("nextworld", "NÄCHSTE WELT"), GameHudSkin.Gold);
        p.Burst(1.4f, 34, 30, 0.6f);

        // ---- Endless ----
        p.Text(69, 2, 67, Loc.Get("ui.levelselect.endless", "ENDLESS"), GameHudSkin.Gold);
        Sprite clock = GameHudSkin.Clock;
        p.Sprite(Mathf.Round(102 - clock.rect.width * 3f / 2f), 20, clock, 3);
        TextMeshProUGUI time = p.Text(69, 56, 67, "00:00", GameHudSkin.Cream);
        p.Text(69, 72, 67, Loc.Get("ui.levelselect.nolimit", "OHNE ENDE"), GameHudSkin.ParchDark);
        Sprite skull = GameHudSkin.Skull;
        Image skullImg = p.Sprite(Mathf.Round(102 - skull.rect.width), 88, skull, 2);

        p.Tick(t =>
        {
            float f = TutorialPicture.Ease(t, 1.0f, 1.4f);
            Show(crownImg, t >= 1.0f);
            TutorialPicture.Place(crownImg, Mathf.Round(34 - cw), Mathf.Round(-ch * 2 + f * (16 + ch * 2) - 2));
            bossImg.color = t >= 1.4f ? new Color(0.55f, 0.5f, 0.55f, 1f) : Color.white;
            Show(win, t >= 1.4f);
            Show(next, t >= 2.0f);

            int secs = Mathf.FloorToInt(t / 6f * 1799f);
            time.text = (secs / 60).ToString("00") + ":" + (secs % 60).ToString("00");
            skullImg.color = t > 5.2f ? Color.white : new Color(1f, 1f, 1f, 0.35f);
        });
    }

    // ==================================================================
    //  Shop
    // ==================================================================

    /// <summary>Gold kommt aus dem Lauf, KAUFEN hebt ein Upgrade eine Stufe.</summary>
    private static void DrawShop(TutorialPicture p)
    {
        p.Loop = 6.5f;

        Sprite coin = GameHudSkin.Coin;
        p.Sprite(6, 6, coin);
        string goldLabel = Loc.Get("ui.shop.gold", "GOLD:");
        TextMeshProUGUI gold = p.Text(9 + coin.rect.width, 3, 90, "", GameHudSkin.Gold, TextAlignmentOptions.Left);

        p.Button(2, 22, 66, 16, Loc.Get("ui.shop.tab.unlocks", "FREISCHALTEN"), GameHudSkin.ButtonLook.Wood);
        p.Button(70, 22, 64, 16, Loc.Get("ui.shop.tab.upgrades", "UPGRADES"), GameHudSkin.ButtonLook.Active);

        p.Img(2, 42, 132, 40, GameHudSkin.Well, Color.white, true);
        p.Workbench(8, 48, "slot_weapon");
        Sprite plus = GameHudSkin.Plus;
        p.Sprite(8 + 14 - Mathf.Floor(plus.rect.width / 2f), 48 + 14 - Mathf.Floor(plus.rect.height / 2f), plus);

        p.Text(42, 47, 90, Loc.Get("shop.weapon_slot.name", "Waffenplatz"), GameHudSkin.Cream, TextAlignmentOptions.Left);
        string levelFormat = Loc.Get("ui.shop.level", "Stufe {0}/{1}");
        TextMeshProUGUI level = p.Text(42, 61, 90, "", GameHudSkin.ParchDark, TextAlignmentOptions.Left);

        p.Text(4, 88, 64, Loc.Get("ui.shop.price", "PREIS:") + " 300", GameHudSkin.ParchDark, TextAlignmentOptions.Left);
        FakeButton buy = p.Button(70, 86, 64, 18, Loc.Get("ui.shop.buy", "KAUFEN"), GameHudSkin.ButtonLook.Gold);

        const int coinCount = 5;
        var coins = new Image[coinCount];
        for (int i = 0; i < coinCount; i++) coins[i] = p.Sprite(0, 0, coin);

        p.Burst(2.8f, 22, 62, 0.6f);

        p.Tick(t =>
        {
            int value = Mathf.RoundToInt(Mathf.Lerp(910, 1250, TutorialPicture.Ease(t, 0.3f, 1.3f)));
            if (t >= 2.8f) value -= 300;
            gold.text = goldLabel + " " + value;

            int lv = t >= 2.8f ? 2 : 1;
            level.text = string.Format(levelFormat, lv, 3);

            if (t >= 2.8f && t < 2.98f) buy.SetLook(GameHudSkin.ButtonLook.Pressed);
            else buy.SetLook(t >= 2.3f && t < 5.8f ? GameHudSkin.ButtonLook.GoldHover : GameHudSkin.ButtonLook.Gold);

            // Muenzen aus dem letzten Lauf fliegen in den Zaehler.
            for (int i = 0; i < coinCount; i++)
            {
                float start = 0.15f + i * 0.13f;
                float f = TutorialPicture.Ease(t, start, start + 0.5f);
                Show(coins[i], t >= start && f < 1f);
                Vector2 v = Vector2.Lerp(new Vector2(30 + i * 20, 112), new Vector2(6, 6), f);
                TutorialPicture.Place(coins[i], v.x, v.y);
            }
        });

        p.Cursor(TutorialPicture.At(0f, 124, 108),
                 TutorialPicture.At(2.0f, 124, 108),
                 TutorialPicture.ClickAt(2.8f, 102, 95),
                 TutorialPicture.At(5.8f, 102, 95),
                 TutorialPicture.At(6.5f, 124, 108));
    }

    // ==================================================================
    //  Kapsel
    // ==================================================================

    /// <summary>Die drei Reiter nacheinander: Erfolg wird voll, Unlock geht auf, Bestiarium zaehlt.</summary>
    private static void DrawCapsule(TutorialPicture p)
    {
        p.Loop = 9f;

        Sprite[] tabIcons = { GameHudSkin.Star, GameHudSkin.Key, GameHudSkin.Skull };
        string[] tabNames =
        {
            Loc.Get("ui.book.tab.achievements", "ERFOLGE"),
            Loc.Get("ui.book.tab.unlocks", "UNLOCKS"),
            Loc.Get("ui.book.tab.bestiary", "BESTIARIUM"),
        };

        var tabs = new FakeButton[3];
        for (int i = 0; i < 3; i++)
            tabs[i] = p.Button(4 + i * 44, 4, 40, 18, "", GameHudSkin.ButtonLook.Wood, tabIcons[i]);

        p.Img(4, 26, 128, 82, GameHudSkin.Well, Color.white, true);
        TextMeshProUGUI title = p.Text(4, 29, 128, "", GameHudSkin.Gold);

        // ---- Erfolge ----
        var achGroup = new List<Graphic>();
        Sprite achIcon = Resources.Load<Sprite>("AchievementsBook/boss_cookie_king_32");
        achGroup.Add(p.Img(12, 46, 32, 32, achIcon, Color.white));
        achGroup.Add(p.Text(50, 46, 78, T("ach", "BOSS BESIEGT"), GameHudSkin.Cream, TextAlignmentOptions.Left));
        achGroup.Add(p.Img(50, 62, 74, 7, GameHudSkin.BarFrame, Color.white, true));
        Image achFill = p.Img(52, 64, 0, 3, GameHudSkin.White, GameHudSkin.Gold);
        achGroup.Add(achFill);
        Image achCheck = p.Sprite(114, 74, GameHudSkin.Check);

        // ---- Unlocks ----
        var unlockGroup = new List<Graphic>();
        int unlockChar = FirstLocked();
        Sprite unlockIcon = Resources.Load<Sprite>("Unlocks/unlock_char_mochi");
        Image unlockImg = p.Img(11, 45, 34, 34, unlockIcon, Color.white);
        unlockGroup.Add(unlockImg);
        Sprite lockSprite = GameHudSkin.Lock;
        Image unlockLock = p.Sprite(28 - Mathf.Floor(lockSprite.rect.width / 2f),
                                    62 - Mathf.Floor(lockSprite.rect.height / 2f), lockSprite);
        unlockGroup.Add(p.Text(50, 46, 78, Characters.DisplayName(unlockIcon != null ? 5 : unlockChar),
                               GameHudSkin.Cream, TextAlignmentOptions.Left));
        TextMeshProUGUI unlockState = p.Text(50, 60, 78, "", GameHudSkin.StoneLight, TextAlignmentOptions.Left);
        unlockGroup.Add(unlockState);

        // ---- Bestiarium ----
        var bestGroup = new List<Graphic>();
        Sprite enemy = Resources.Load<Sprite>("Bestiary/Marshmello");
        int ew = enemy != null ? Mathf.RoundToInt(enemy.rect.width) : 0;
        int eh = enemy != null ? Mathf.RoundToInt(enemy.rect.height) : 0;
        int escale = ew > 0 && ew <= 24 && eh <= 24 ? 2 : 1;
        bestGroup.Add(p.Img(28 - ew * escale / 2, 64 - eh * escale / 2, ew * escale, eh * escale, enemy, Color.white));
        string killsFormat = T("kills", "{0} BESIEGT");
        string bonusFormat = T("bonus", "+{0} % SCHADEN");
        TextMeshProUGUI kills = p.Text(56, 48, 74, "", GameHudSkin.Cream, TextAlignmentOptions.Left);
        TextMeshProUGUI bonus = p.Text(56, 62, 74, "", GameHudSkin.Mint, TextAlignmentOptions.Left);
        bestGroup.Add(kills);
        bestGroup.Add(bonus);

        p.Burst(1.7f, 118, 78, 0.5f);
        p.Burst(4.3f, 28, 62, 0.6f);
        p.Burst(7.6f, 28, 64, 0.5f);

        string lockedText = Loc.Get("ui.charselect.locked", "GESPERRT");
        string freeText = T("unlocked", "FREI!");

        p.Tick(t =>
        {
            int tab = Mathf.Clamp(Mathf.FloorToInt(t / 3f), 0, 2);
            float local = t - tab * 3f;

            for (int i = 0; i < 3; i++)
                tabs[i].SetLook(i == tab ? GameHudSkin.ButtonLook.Active : GameHudSkin.ButtonLook.Wood);
            title.text = tabNames[tab];

            foreach (Graphic g in achGroup) Show(g, tab == 0);
            foreach (Graphic g in unlockGroup) Show(g, tab == 1);
            foreach (Graphic g in bestGroup) Show(g, tab == 2);
            if (achIcon == null) Show(achGroup[0], false);
            if (unlockIcon == null) Show(unlockImg, false);
            if (enemy == null) Show(bestGroup[0], false);

            // Erfolg: Balken laeuft voll, dann der Haken.
            float fill = TutorialPicture.Ease(local, 0.3f, 1.6f);
            OptionsKit.Move(achFill.rectTransform, 52, 64, Mathf.Round(70f * fill), 3);
            if (tab == 0) Show(achFill, fill * 70f >= 1f);
            Show(achCheck, tab == 0 && local >= 1.7f);
            if (tab == 0 && achIcon != null)
                ((Image)achGroup[0]).color = local >= 1.7f ? Color.white : new Color(0.4f, 0.36f, 0.4f, 1f);

            // Unlock: gesperrt, dann frei.
            bool free = local >= 1.3f;
            if (unlockIcon != null) unlockImg.color = free ? Color.white : new Color(0.2f, 0.17f, 0.2f, 1f);
            Show(unlockLock, tab == 1 && !free);
            unlockState.text = free ? freeText : lockedText;
            unlockState.color = free ? (Color)GameHudSkin.Mint : GameHudSkin.StoneLight;

            // Bestiarium: Zaehler laeuft, bei der ersten Stufe gibt es Bonus.
            int k = Mathf.RoundToInt(150f * TutorialPicture.Ease(local, 0.2f, 1.6f));
            kills.text = string.Format(killsFormat, k);
            bonus.text = string.Format(bonusFormat, Bestiary.PercentPerStep);
            if (tab == 2) Show(bonus, k >= 150);
        });
    }
}
