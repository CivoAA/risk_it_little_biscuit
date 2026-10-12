using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Macht aus einem TextMeshPro-Text einen "juicy" Dialogtext - nach dem
/// RPG-Dialogbox-Reel von mii_misan:
///
///   Typewriter  - 30 Zeichen pro Sekunde
///   Pausen      - kurz halten nach . ! ? (und kürzer nach ,)
///   Letter Pop  - jeder neue Buchstabe springt mit back_out auf
///   Wavy Text   - &lt;wave&gt;Wort&lt;/wave&gt; im Text lässt das Wort wellen
///   Wackeln     - &lt;shake&gt;Wort&lt;/shake&gt; zittert (Schreck, Wut)
///   Farbe       - &lt;hot&gt; Glut-Orange, &lt;ice&gt; Eisblau, &lt;jam&gt; Marmeladenrot,
///                 &lt;gold&gt; Gold (Schluesselwoerter der Story)
///
/// Benutzung: Komponente auf das Objekt mit dem TMP-Text, dann
/// <see cref="Play"/> mit dem Seitentext aufrufen. <see cref="IsDone"/> sagt,
/// ob alles da ist, <see cref="Complete"/> zeigt sofort den Rest (Klick
/// während des Tippens).
///
/// Technisch: der Text steht komplett im Mesh, noch nicht getippte Zeichen
/// sind nur unsichtbar. So verschiebt sich beim Tippen nichts, und Zeilen
/// brechen von Anfang an dort um, wo sie am Ende stehen.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class DialogueText : MonoBehaviour
{
    [Header("Typewriter")]
    [SerializeField, Tooltip("chars += 30 * dt")]
    private float charsPerSecond = 30f;
    [SerializeField, Tooltip("Halt nach . ! ?")]
    private float sentencePause = 0.34f;
    [SerializeField, Tooltip("Halt nach , ; :")]
    private float commaPause = 0.12f;

    [Header("Letter Pop")]
    [SerializeField, Tooltip("scale = back_out(age / 0.14)")]
    private float popTime = 0.14f;

    [Header("Wavy Text")]
    [SerializeField] private float waveSpeed = 6f;
    [SerializeField] private float waveSpacing = 0.6f;
    [SerializeField, Tooltip("Video: 2.5 - bei unserer 8er-Schrift springt das zu sehr.")]
    private float waveHeight = 1.5f;

    [Header("Shake")]
    [SerializeField] private float shakeAmount = 0.7f;

    /// <summary>Farben fuer &lt;hot&gt;, &lt;ice&gt;, &lt;jam&gt;, &lt;gold&gt; (Index 1-4, 0 = Textfarbe).</summary>
    private static readonly Color32[] Tints =
    {
        new Color32(0, 0, 0, 0),
        new Color32(0xff, 0x9a, 0x3c, 0xff),
        new Color32(0x8f, 0xd3, 0xf5, 0xff),
        new Color32(0xf0, 0x8a, 0x9a, 0xff),
        new Color32(0xfb, 0xd8, 0x6a, 0xff),
    };

    /// <summary>Feuert fuer jeden neu getippten Buchstaben - fuer Stimmen-Klicks.</summary>
    public event System.Action<char> CharRevealed;

    /// <summary>Tippgeschwindigkeit (Zeichen pro Sekunde).</summary>
    public float CharsPerSecond
    {
        get => charsPerSecond;
        set => charsPerSecond = Mathf.Max(1f, value);
    }

    private TMP_Text text;
    private string plain = "";
    private bool[] wave = new bool[0];
    private bool[] shake = new bool[0];
    private byte[] tint = new byte[0];
    private float[] revealedAt = new float[0];
    private int revealed;
    private float charBudget;
    private float pauseLeft;
    private float clock;

    /// <summary>Alle Zeichen der aktuellen Seite sind sichtbar.</summary>
    public bool IsDone => revealed >= plain.Length;

    /// <summary>Es wird gerade getippt (für Porträt-Animation und Ähnliches).</summary>
    public bool IsTyping => !IsDone && pauseLeft <= 0f;

    private void Awake() => text = GetComponent<TMP_Text>();

    /// <summary>Neue Seite: Markup auswerten und von vorn tippen.</summary>
    public void Play(string markup)
    {
        if (text == null) text = GetComponent<TMP_Text>();

        Parse(markup ?? "", out plain, out wave, out shake, out tint);
        revealedAt = new float[plain.Length];
        revealed = 0;
        charBudget = 0f;
        pauseLeft = 0f;

        // Kein Rich-Text: der Text im Mesh muss Zeichen für Zeichen dem String
        // entsprechen, sonst passen Wellen und Tippstand nicht mehr zusammen.
        text.richText = false;
        text.text = plain;
        text.ForceMeshUpdate();
        // Leere Seite: nichts anwenden - UpdateVertexData wuerde sonst die alten
        // Vertices der vorigen Seite zurueck ins Mesh schreiben.
        if (plain.Length > 0) Apply();
    }

    /// <summary>Sofort alles zeigen - ohne Pop, der Rest steht einfach da.</summary>
    public void Complete()
    {
        for (int i = revealed; i < plain.Length; i++) revealedAt[i] = clock - popTime;
        revealed = plain.Length;
        pauseLeft = 0f;
    }

    private void OnEnable() => clock = 0f;

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        clock += dt;

        if (!IsDone)
        {
            if (pauseLeft > 0f)
            {
                pauseLeft -= dt;
            }
            else
            {
                charBudget += charsPerSecond * dt;
                while (charBudget >= 1f && revealed < plain.Length)
                {
                    charBudget -= 1f;
                    char c = plain[revealed];
                    revealedAt[revealed] = clock;
                    revealed++;
                    CharRevealed?.Invoke(c);

                    // Pause erst nach dem letzten Satzzeichen einer Gruppe ("?!", "...").
                    bool nextIsPunct = revealed < plain.Length && IsPunct(plain[revealed]);
                    float pause = nextIsPunct ? 0f : PauseAfter(c);
                    if (pause > 0f)
                    {
                        pauseLeft = pause;
                        charBudget = 0f;
                        break;
                    }
                }
            }
        }
    }

    private void LateUpdate()
    {
        if (text != null && plain.Length > 0) Apply();
    }

    /// <summary>Schreibt Sichtbarkeit, Pop und Welle in die Vertices.</summary>
    private void Apply()
    {
        // Frisches Mesh, damit Verschiebungen nicht von Frame zu Frame aufaddieren.
        text.ForceMeshUpdate();
        TMP_TextInfo info = text.textInfo;

        for (int i = 0; i < info.characterCount; i++)
        {
            TMP_CharacterInfo ch = info.characterInfo[i];
            if (!ch.isVisible) continue;

            int src = ch.index;
            int mat = ch.materialReferenceIndex;
            int v0 = ch.vertexIndex;
            Vector3[] verts = info.meshInfo[mat].vertices;
            Color32[] cols = info.meshInfo[mat].colors32;

            if (src >= revealed)
            {
                for (int k = 0; k < 4; k++) cols[v0 + k].a = 0;
                continue;
            }

            // ---------- Letter Pop ----------
            float age = clock - revealedAt[src];
            float scale = BackOut(Mathf.Clamp01(age / popTime));

            // ---------- Wavy Text ----------
            float oy = src < wave.Length && wave[src]
                ? Mathf.Sin(clock * waveSpeed + src * waveSpacing) * waveHeight
                : 0f;

            // ---------- Shake ----------
            float ox = 0f;
            if (src < shake.Length && shake[src])
            {
                float t = Mathf.Floor(clock * 24f);
                ox = (Hash(src * 7.13f + t) - 0.5f) * 2f * shakeAmount;
                oy += (Hash(src * 3.71f + t * 1.3f) - 0.5f) * 2f * shakeAmount;
            }

            // ---------- Farbe ----------
            if (src < tint.Length && tint[src] > 0)
            {
                Color32 tc = Tints[tint[src]];
                for (int k = 0; k < 4; k++)
                {
                    byte a = cols[v0 + k].a;
                    cols[v0 + k] = new Color32(tc.r, tc.g, tc.b, a);
                }
            }

            Vector3 center = (verts[v0] + verts[v0 + 2]) * 0.5f;
            for (int k = 0; k < 4; k++)
                verts[v0 + k] = center + (verts[v0 + k] - center) * scale + new Vector3(ox, oy, 0f);
        }

        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
    }

    private float PauseAfter(char c)
    {
        switch (c)
        {
            case '.': case '!': case '?': case '…': return sentencePause;
            case ',': case ';': case ':': return commaPause;
            default: return 0f;
        }
    }

    private static bool IsPunct(char c) => c == '.' || c == '!' || c == '?' || c == '…';

    private static float Hash(float x)
    {
        float s = Mathf.Sin(x * 12.9898f) * 43758.5453f;
        return s - Mathf.Floor(s);
    }

    private static readonly string[] TintTags = { null, "hot", "ice", "jam", "gold" };

    /// <summary>
    /// Nimmt die Tags (&lt;wave&gt;, &lt;shake&gt;, &lt;hot&gt;, &lt;ice&gt;, &lt;jam&gt;,
    /// &lt;gold&gt; und ihre Gegenstuecke) aus dem Text und merkt sich je Zeichen,
    /// was es tut. Alles andere bleibt wörtlich stehen.
    /// </summary>
    private static void Parse(string markup, out string plainText, out bool[] waveFlags,
                              out bool[] shakeFlags, out byte[] tintIds)
    {
        var sb = new StringBuilder(markup.Length);
        var waves = new List<bool>(markup.Length);
        var shakes = new List<bool>(markup.Length);
        var tints = new List<byte>(markup.Length);
        bool inWave = false, inShake = false;
        byte inTint = 0;

        for (int i = 0; i < markup.Length; i++)
        {
            if (markup[i] == '<')
            {
                int end = markup.IndexOf('>', i);
                if (end > i)
                {
                    string tag = markup.Substring(i + 1, end - i - 1);
                    bool closing = tag.StartsWith("/");
                    string name = closing ? tag.Substring(1) : tag;
                    bool known = true;
                    if (name == "wave") inWave = !closing;
                    else if (name == "shake") inShake = !closing;
                    else
                    {
                        int t = System.Array.IndexOf(TintTags, name);
                        if (t > 0) inTint = closing ? (byte)0 : (byte)t;
                        else known = false;
                    }
                    if (known)
                    {
                        i = end;
                        continue;
                    }
                }
            }
            sb.Append(markup[i]);
            waves.Add(inWave);
            shakes.Add(inShake);
            tints.Add(inTint);
        }

        plainText = sb.ToString();
        waveFlags = waves.ToArray();
        shakeFlags = shakes.ToArray();
        tintIds = tints.ToArray();
    }

    private static float BackOut(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }
}
