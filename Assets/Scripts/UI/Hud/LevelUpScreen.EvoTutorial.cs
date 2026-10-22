using UnityEngine;
using UnityEngine.UI;
using Line = StoryTexts.Line;

/// <summary>
/// Evo-Tutorial des <see cref="LevelUpScreen"/>: beim allerersten Level-Up
/// (pro Profil) meldet sich Pomo, danach muss einmal das Evo-Buch auf -
/// Karten, Reroll und Banish sind solange gesperrt, der EVOS-Knopf blinkt
/// und ein Pfeil zeigt drauf. Im Buch erklaert Pomo Zutaten, Max-Stufe und
/// den einen Evo-Slot, danach geht die Auswahl normal weiter.
///
/// Gesehen steht in <see cref="ProfilePrefs"/> (<see cref="EvoTutorialKey"/>),
/// "Spielstand zuruecksetzen" loescht es mit (<see cref="TutorialPanel.AllSeenKeys"/>).
/// Wer in den Optionen "Tipps" abschaltet, bekommt es nicht.
/// Texte: story.tut.evo.* in <see cref="StoryTexts"/> und de/en.json.
/// </summary>
public partial class LevelUpScreen
{
    public const string EvoTutorialKey = "tutorial.seen.evo_levelup";

    /// <summary>So lange nach dem Austeilen, bis Pomo dazwischenredet.</summary>
    private const float EvoTutDelay = 0.6f;

    private enum EvoTut { Off, Waiting, Intro, PressEvos, Book, Done }

    private EvoTut evoTut = EvoTut.Off;
    private RectTransform evoTutArrow;
    private float slotsFlashUntil;

    /// <summary>Waehrend des Tutorials zaehlt nur der EVOS-Knopf.</summary>
    private bool EvoTutLocked => evoTut == EvoTut.Waiting || evoTut == EvoTut.Intro || evoTut == EvoTut.PressEvos;

    /// <summary>Pomo redet (oder gleich) - das Fenster liest dann keine Eingabe.</summary>
    private bool EvoTutTalking => evoTut == EvoTut.Waiting || StoryDialog.Blocking;

    /// <summary>Aus Enter(Choose) beim frischen Oeffnen.</summary>
    private void MaybeStartEvoTutorial()
    {
        if (evoTut != EvoTut.Off) return;
        if (!GameSettings.Tips || ProfilePrefs.GetInt(EvoTutorialKey) == 1) return;
        PlayerController p = PlayerController.Instance;
        if (p == null || p.EvoCombinations == null || p.EvoCombinations.Count == 0) return;
        if (HasEvoOffer() || StoryDialog.IsOpen) return;

        evoTut = EvoTut.Waiting;
    }

    /// <summary>Jeden Frame aus UpdateChoose.</summary>
    private void UpdateEvoTutChoose(float now)
    {
        if (evoTut == EvoTut.Waiting && now >= openedAt + EvoTutDelay)
        {
            evoTut = EvoTut.Intro;
            StoryDialog.Play(StoryTexts.Convo("tut.evo.levelup"), () => evoTut = EvoTut.PressEvos, TutDialogOptions(0.3f));
        }

        bool point = evoTut == EvoTut.PressEvos && !StoryDialog.Blocking;
        evoButton.Active = point && Mathf.Repeat(now * 2.5f, 1f) < 0.5f;

        if (point && evoTutArrow == null) BuildEvoTutArrow();
        if (evoTutArrow == null) return;
        evoTutArrow.gameObject.SetActive(point);
        if (!point) return;

        // Pfeil ueber dem Knopf, wippt einen Pixel.
        RectInt r = EvoButtonRect;
        int bob = Mathf.Repeat(now * 3f, 1f) < 0.5f ? 0 : 2;
        SetPos(evoTutArrow, r.x + r.width / 2f, -(r.y - 7 - bob));
    }

    /// <summary>Aus Enter(Book): das erste Oeffnen waehrend des Tutorials.</summary>
    private void OnEvoTutBookOpened()
    {
        if (evoTut != EvoTut.PressEvos) return;
        evoTut = EvoTut.Book;
        evoButton.Active = false;

        PlayerController p = PlayerController.Instance;
        int slots = p != null ? p.EvoSlots : Shop.BaseEvoSlots;
        StoryDialog.Options o = TutDialogOptions(0.15f);
        o.FormatArgs = new object[] { slots };
        StoryDialog.Play(EvoBookLines(slots), () =>
        {
            evoTut = EvoTut.Done;
            ProfilePrefs.SetInt(EvoTutorialKey, 1);
            slotsFlashUntil = Time.unscaledTime + 2.5f;
        }, o);
    }

    /// <summary>Aus UpdateBook: die Slot-Anzeige blinkt kurz, nachdem Pomo darauf gezeigt hat.</summary>
    private void UpdateEvoTutBook(float now)
    {
        if (slotsFlashUntil <= 0f) return;
        if (now >= slotsFlashUntil)
        {
            slotsFlashUntil = 0f;
            RefreshBook();
            return;
        }
        slotsText.SetColor(Mathf.Repeat(now * 3f, 1f) < 0.5f ? GameHudSkin.GoldLight : GameHudSkin.Cream);
    }

    private static Line[] EvoBookLines(int slots)
    {
        Line[] lines = StoryTexts.Convo("tut.evo.book");
        if (slots <= 1) return lines;
        // Mit gekauften Slots: die Slot-Seite passend austauschen.
        var copy = (Line[])lines.Clone();
        Line[] many = StoryTexts.Convo("tut.evo.book.many");
        for (int i = 0; i < copy.Length; i++)
            if (copy[i].Key == "story.tut.evo.book.3") copy[i] = many[0];
        return copy;
    }

    private static StoryDialog.Options TutDialogOptions(float dim) => new StoryDialog.Options
    {
        Dim = dim,
        FreezeTime = false,    // steht beim Level-Up eh schon
        BlockHub = false,
    };

    private RectInt EvoButtonRect
    {
        get
        {
            int x = (RefW - (3 * ButtonW + 2 * ButtonGap)) / 2;
            return new RectInt(x + 2 * (ButtonW + ButtonGap), ButtonsY, ButtonW, ButtonH);
        }
    }

    private void BuildEvoTutArrow()
    {
        Sprite s = GameHudSkin.Arrow;
        int w = Mathf.RoundToInt(s.rect.width), h = Mathf.RoundToInt(s.rect.height);
        Image img = Img("EvoTutArrow", chooseLayer, new RectInt(0, 0, w, h), s, Color.white);
        evoTutArrow = img.rectTransform;
        evoTutArrow.pivot = new Vector2(0.5f, 0.5f);
        evoTutArrow.localEulerAngles = new Vector3(0f, 0f, -90f);   // zeigt nach unten
    }
}
