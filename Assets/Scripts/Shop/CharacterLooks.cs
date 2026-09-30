using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Wie die Charaktere aussehen - fuer Menues ausserhalb des Laufs, allen voran
/// die Charakterauswahl im Hub. Der Hub laedt das Player-Prefab nicht und
/// kennt dessen Animatoren darum nicht; dieses Asset reicht sie nach.
///
/// Liegt als <c>Assets/Resources/Characters/CharacterLooks.asset</c>, Eintrag i
/// gehoert zu Charakter i aus <see cref="Characters"/> - also dieselbe
/// Reihenfolge wie im PlayerSkinSwitcher (alle Charaktere kommen von hier).
///
/// KOMMT EIN CHARAKTER DAZU: unten einen Eintrag anhaengen - Animator, Fusszeilen
/// und eine Akzentfarbe. Fuer jeden Index nehmen PlayerSkinSwitcher (Lauf) und
/// WM_PlayerSkinSwitcher (Hub) den Animator ebenfalls von hier, das Prefab und
/// die Szenen brauchen dafuer keinen neuen Eintrag. Fehlt der
/// Eintrag, nimmt die Auswahl den Animator vom Hub-Spieler, und fehlt auch
/// der, bleibt das Fenster eben leer - kaputt geht nichts.
/// </summary>
[CreateAssetMenu(fileName = "CharacterLooks", menuName = "Risk It/Charakter-Aussehen")]
public class CharacterLooks : ScriptableObject
{
    [Serializable]
    public class Look
    {
        [Tooltip("Animator mit Idle-/Walk-Blendtree - derselbe wie im PlayerSkinSwitcher.")]
        public RuntimeAnimatorController animator;

        [Tooltip("Leere Pixelzeilen unter den Fuessen im Frame (64er-Keks: 6, 32er-Charaktere: 0). " +
                 "Damit steht er in der Charakterauswahl auf der Platte statt darueber zu schweben.")]
        public int footRows;

        [Tooltip("Toent Licht, Hintergrund und Spruch in der Charakterauswahl.")]
        public Color accent = new Color(0.95f, 0.76f, 0.31f, 1f);

        [Tooltip("Portraet fuer die Keksdose, 42x42 je Bild (Tools/char_icons.py). Bild 0 steht " +
                 "still, unter dem Cursor laeuft die ganze Reihe. Leer = Idle-Animation wie bisher.")]
        public Sprite[] portrait;
    }

    public const string ResourcePath = "Characters/CharacterLooks";

    [SerializeField] private List<Look> looks = new List<Look>();

    // Farben fuer Charaktere ohne Eintrag - reihum, damit neue sich unterscheiden.
    private static readonly Color32[] FallbackAccents =
    {
        GameHudSkin.Gold, GameHudSkin.StoneLight, GameHudSkin.Jam, GameHudSkin.Grape,
        GameHudSkin.Mint, GameHudSkin.Icing, GameHudSkin.JamLight, GameHudSkin.GoldDark, GameHudSkin.IcingLight,
    };

    private static CharacterLooks cached;
    private static bool searched;

    public static CharacterLooks Instance
    {
        get
        {
            if (!searched)
            {
                searched = true;
                cached = Resources.Load<CharacterLooks>(ResourcePath);
            }
            return cached;
        }
    }

    public static RuntimeAnimatorController AnimatorFor(int index)
    {
        Look look = Instance != null ? Instance.Get(index) : null;
        return look != null ? look.animator : null;
    }

    public static Color AccentFor(int index)
    {
        Look look = Instance != null ? Instance.Get(index) : null;
        if (look != null) return look.accent;
        return FallbackAccents[Mathf.Abs(index) % FallbackAccents.Length];
    }

    public static Sprite[] PortraitFor(int index)
    {
        Look look = Instance != null ? Instance.Get(index) : null;
        return look != null && look.portrait != null && look.portrait.Length > 0 ? look.portrait : null;
    }

    public static int FootRowsFor(int index)
    {
        Look look = Instance != null ? Instance.Get(index) : null;
        return look != null ? Mathf.Max(0, look.footRows) : 0;
    }

    /// <summary>
    /// Der Koerper im Frame, in Texturpixeln: ein Quadrat von einer Einheit
    /// (PPU), unten mittig am Pivot. Bei normalen Frames ist das der ganze
    /// Frame. Groesser ist er nur, wenn etwas heraussteht - der Zwiebelritter
    /// hat 64er-Zellen fuer sein Schwert, ist aber eine 32er-Figur. Menues
    /// skalieren und zentrieren nach diesem Quadrat statt nach dem Frame,
    /// sonst wird er halb so gross gezeichnet.
    /// </summary>
    public static Rect BodyRect(Sprite s)
    {
        float w = s.rect.width, h = s.rect.height;
        float unit = Mathf.Min(s.pixelsPerUnit, Mathf.Max(w, h));
        if (unit <= 0f || (w <= unit && h <= unit)) return new Rect(0f, 0f, w, h);

        float bw = Mathf.Min(unit, w), bh = Mathf.Min(unit, h);
        float x = Mathf.Clamp(Mathf.Round(s.pivot.x - bw / 2f), 0f, w - bw);
        float y = Mathf.Clamp(Mathf.Round(s.pivot.y), 0f, h - bh);
        return new Rect(x, y, bw, bh);
    }

    private Look Get(int index) => index >= 0 && index < looks.Count ? looks[index] : null;
}
