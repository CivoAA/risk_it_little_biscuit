using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Die Gegner-Werkstatt: ein Fenster, in dem ein Gegner von den Werten bis zum
/// fertigen Prefab entsteht.
///
/// WARUM ES DAS GIBT
///
/// Einen Gegner von Hand anzulegen waren bisher neun Schritte, von denen acht
/// vergessen werden konnten: Sprites schneiden, Clip bauen, Loop anhaken,
/// Controller anlegen, GameObject mit SpriteRenderer/Rigidbody/Collider/Animator
/// zusammenstecken, Tag und Layer setzen, Sortierebene setzen, Werte eintragen,
/// ins Prefab speichern, in den SpawnCatalog haengen. Vergisst man den Tag,
/// trifft keine einzige Waffe - und das faellt erst im Spiel auf.
///
/// Die Werkstatt macht alle Schritte in einem Zug und ist beliebig oft
/// wiederholbar: ein vorhandenes Prefab wird aktualisiert, nicht ersetzt.
/// Eigene Bauteile, die jemand spaeter dazugehaengt hat (ein Spezial-Angriff
/// etwa), bleiben dabei stehen.
///
/// DREI SACHEN, DIE SIE BESSER KANN ALS DIE HAND
///
///   1. Animationstempo. Jeder Gegner hat andere Bildzahlen - 2 bei der Maus,
///      12 beim Kirschslime. Ein fester Wert sieht bei einem von beiden immer
///      falsch aus. Hier laeuft die Vorschau live, und der Regler zeigt dazu,
///      wie lang eine Runde dauert.
///   2. Blickrichtung. Nicht jedes Bild ist in dieselbe Richtung gezeichnet.
///      Die Vorschau hat einen Schalter "Spieler steht links/rechts" - wenn der
///      Gegner dort in die falsche Richtung guckt, stimmt die Einstellung
///      nicht, und man sieht es sofort statt erst im Spiel.
///   3. Trefferkreis. Kommt aus den Sprite-Massen statt aus dem Gefuehl.
///
/// Gespeichert werden die Werte NICHT im Prefab, sondern im
/// <see cref="EnemyCatalog"/> - die Werkstatt schreibt den Datenblock dieser
/// Datei neu. Deshalb ueberlebt Balancing jeden Prefab-Neubau.
/// </summary>
public class EnemyWorkshop : EditorWindow
{
    // ------------------------------------------------------------- Konstanten

    private const string CatalogPath = "Assets/Scripts/Enemy/EnemyCatalog.cs";
    private const string AnimFolder = "Assets/Animations/Gegner/Neu";
    private const string PrefabFolder = "Assets/Prefabs/Enemy/Neu";
    private const string EnemyPrefabRoot = "Assets/Prefabs/Enemy";
    private const string CoreScenePath = "Assets/Scenes/Core/GameCore.unity";

    /// <summary>Vorlage fuer Material und Sortierebene - so sieht Neues aus wie Altes.</summary>
    private const string TemplatePrefab = "Assets/Prefabs/Enemy/fin_marshmallow_0.prefab";

    private const string SortingLayer = "Objects";

    /// <summary>
    /// Der Gegner-Layer heisst im Projekt "Enemys " - mit Leerzeichen am Ende.
    /// Das ist ein Tippfehler von frueher, den man nicht mehr reparieren kann,
    /// ohne alle Prefabs anzufassen. Deshalb hier mehrere Schreibweisen.
    /// </summary>
    private static readonly string[] LayerCandidates = { "Enemys ", "Enemys", "Enemy" };

    private static readonly string[] Tabs = { "Werkstatt", "Alt-Prefabs", "Pruefung" };

    // ----------------------------------------------------------------- Daten

    /// <summary>Ein Gegner, wie er gerade im Fenster bearbeitet wird.</summary>
    private class Draft
    {
        public EnemyId id;

        /// <summary>
        /// Der Name im Code (EnemyId.xxx). Steht getrennt von <see cref="id"/>,
        /// weil ein frisch angelegter oder umbenannter Gegner diesen Namen hat,
        /// BEVOR Unity neu kompiliert hat und es den Enum-Wert gibt.
        /// </summary>
        public string code;

        public string name;
        public float health, damage, speed;
        public int exp;
        public float pushTime;
        public bool archived;
        public EnemyRole role;
        public EnemyFacing facing;
        public string sheet;
        public float fps;
        public float colliderRadius;
        public Vector2 colliderOffset;
        public float scale;
        public string prefab;

        public static Draft From(EnemyDef def)
        {
            return new Draft
            {
                id = def.Id,
                code = def.Id.ToString(),
                name = def.Name,
                health = def.Health,
                damage = def.Damage,
                speed = def.Speed,
                exp = def.Exp,
                archived = def.Archived,
                pushTime = def.PushTime,
                role = def.Role,
                facing = def.Facing,
                sheet = def.Sheet,
                fps = def.Fps,
                colliderRadius = def.ColliderRadius,
                colliderOffset = def.ColliderOffset,
                scale = def.Scale,
                prefab = def.Prefab,
            };
        }

        /// <summary>Dieselbe Rechnung wie im Katalog - live, waehrend man tippt.</summary>
        public float Threat
        {
            get { return EnemyCatalog.AutoThreat(health, damage, speed, role); }
        }
    }

    private List<Draft> drafts = new List<Draft>();
    private int selected;
    private int tab;
    private bool dirty;
    private bool showArchive;

    private Vector2 listScroll;
    private Vector2 bodyScroll;
    private Vector2 legacyScroll;

    // ------------------------------------------------ Anlegen / Umbenennen

    /// <summary>
    /// Welcher Gegner nach dem Neukompilieren gewaehlt sein soll. SessionState,
    /// weil Anlegen und Umbenennen den Code aendern - danach laedt Unity alles
    /// neu, und das Fenster vergisst seine Felder.
    /// </summary>
    private const string SelectAfterReloadKey = "EnemyWorkshop.SelectAfterReload";

    private string newEnemyName = "";
    private bool newEnemyCopy = true;

    private bool renameOpen;
    private string renameName = "";
    private string renameId = "";
    private bool renameIdTouched;
    private bool renamePrefab = true;

    // --------------------------------------------------------- Vorschau-Kram

    private Sprite[] frames = new Sprite[0];
    private string framesFor = "";

    private bool playing = true;
    private double playStarted;
    private int scrubFrame;

    /// <summary>Wo der Spieler in der Vorschau steht - zeigt, ob die Spiegelung stimmt.</summary>
    private bool previewPlayerRight = true;

    private float previewZoom = 4f;

    // ------------------------------------------------------------------ Start

    [MenuItem("Tools/Gegner/Werkstatt", false, 102)]
    public static void Open()
    {
        EnemyWorkshop window = GetWindow<EnemyWorkshop>("Gegner");
        window.minSize = new Vector2(880f, 560f);
        window.Reload();
        window.Show();
    }

    /// <summary>
    /// Einstieg fuer den Batch-Mode (Unity -batchmode -executeMethod
    /// EnemyWorkshop.BuildAllFromCommandLine). Baut jeden Gegner, zu dem ein
    /// Sprite-Sheet im Katalog steht, und traegt sie danach in GameCore ein -
    /// ohne dass jemand das Fenster oeffnen muss.
    /// </summary>
    public static void BuildAllFromCommandLine()
    {
        var window = CreateInstance<EnemyWorkshop>();
        window.Reload();

        int built = 0;
        foreach (Draft d in window.drafts)
        {
            // Dieselbe Regel wie im Fenster: vorhandene Prefabs bleiben
            // unangetastet. Ein Batch-Lauf soll nichts kaputt machen koennen,
            // was niemand mehr zurueckholen kann.
            if (d.archived || string.IsNullOrEmpty(d.sheet) || PrefabExists(d)) continue;

            Debug.Log("[Gegner-Werkstatt] " + window.BuildPrefab(d));
            built++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Der Katalog wird mitgeschrieben: BuildPrefab traegt bei einem neu
        // angelegten Gegner den Prefab-Pfad nach, und ohne Speichern waere der
        // beim naechsten Start wieder leer.
        if (window.dirty) window.SaveCatalog();

        window.RegisterAllInScene();

        Debug.Log("[Gegner-Werkstatt] " + built + " Gegner gebaut.");
        DestroyImmediate(window);
    }

    private void OnEnable()
    {
        if (drafts.Count == 0) Reload();
        EditorApplication.update += Tick;
        playStarted = EditorApplication.timeSinceStartup;
    }

    private void OnDisable()
    {
        EditorApplication.update -= Tick;
    }

    private void Tick()
    {
        if (playing && tab == 0 && frames.Length > 1) Repaint();
    }

    private void Reload()
    {
        drafts = EnemyCatalog.All.Select(Draft.From).ToList();

        string wanted = SessionState.GetString(SelectAfterReloadKey, "");
        if (!string.IsNullOrEmpty(wanted))
        {
            SessionState.EraseString(SelectAfterReloadKey);
            int index = drafts.FindIndex(x => x.code == wanted);
            if (index >= 0) selected = index;
        }

        selected = Mathf.Clamp(selected, 0, Mathf.Max(0, drafts.Count - 1));
        renameOpen = false;
        dirty = false;
        framesFor = "";
        startWeapons = null;
    }

    // ------------------------------------------------------------------- GUI

    private void OnGUI()
    {
        DrawToolbar();

        switch (tab)
        {
            case 0: DrawWorkshop(); break;
            case 1: DrawLegacy(); break;
            default: DrawAudit(); break;
        }
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        int newTab = GUILayout.Toolbar(tab, Tabs, EditorStyles.toolbarButton, GUILayout.Width(300f));
        if (newTab != tab)
        {
            tab = newTab;
            GUI.FocusControl(null);
        }

        GUILayout.FlexibleSpace();

        if (dirty)
        {
            GUILayout.Label("ungespeicherte Aenderungen", EditorStyles.miniLabel);
        }

        using (new EditorGUI.DisabledScope(!dirty))
        {
            if (GUILayout.Button("Katalog speichern", EditorStyles.toolbarButton, GUILayout.Width(130f)))
            {
                SaveCatalog();
            }
        }

        if (GUILayout.Button("Neu laden", EditorStyles.toolbarButton, GUILayout.Width(80f)))
        {
            if (!dirty || EditorUtility.DisplayDialog(
                    "Neu laden",
                    "Ungespeicherte Aenderungen gehen verloren. Trotzdem neu laden?",
                    "Neu laden", "Abbrechen"))
            {
                Reload();
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    // -------------------------------------------------------------- Werkstatt

    private void DrawWorkshop()
    {
        EditorGUILayout.BeginHorizontal();

        DrawList();

        if (selected >= 0 && selected < drafts.Count)
        {
            DrawDetail(drafts[selected]);
        }
        else
        {
            EditorGUILayout.HelpBox("Kein Gegner gewaehlt.", MessageType.Info);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(230f));

        listScroll = EditorGUILayout.BeginScrollView(listScroll);

        for (int i = 0; i < drafts.Count; i++)
        {
            if (!drafts[i].archived) DrawListEntry(i);
        }

        // Das Archiv liegt zugeklappt unten: weg aus dem Blick, aber nicht weg.
        int archivedCount = drafts.Count(x => x.archived);
        if (archivedCount > 0)
        {
            EditorGUILayout.Space(6f);
            showArchive = EditorGUILayout.Foldout(showArchive,
                "Archiv (" + archivedCount + ")", true);

            if (showArchive)
            {
                for (int i = 0; i < drafts.Count; i++)
                {
                    if (drafts[i].archived) DrawListEntry(i);
                }
            }
        }

        EditorGUILayout.EndScrollView();

        DrawNewEnemyBox();

        // Bewusst nur die fehlenden: ein Sammelknopf, der auch die 25
        // gewachsenen Prefabs ueberschreibt, waere genau der Unfall, gegen den
        // der Umstieg einzeln und auf Knopfdruck laeuft.
        if (GUILayout.Button("Fehlende Prefabs bauen"))
        {
            BuildMissing();
        }

        if (GUILayout.Button("In GameCore eintragen"))
        {
            RegisterAllInScene();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawListEntry(int i)
    {
        Draft d = drafts[i];

        string mark = PrefabExists(d) ? "" : (string.IsNullOrEmpty(d.sheet) ? "  (kein Bild)" : "  (kein Prefab)");

        bool on = i == selected;
        bool now = GUILayout.Toggle(on, d.name + mark, "Button");
        if (now && !on)
        {
            selected = i;
            framesFor = "";
            renameOpen = false;
            GUI.FocusControl(null);
        }
    }

    private void DrawDetail(Draft d)
    {
        EditorGUILayout.BeginVertical();
        bodyScroll = EditorGUILayout.BeginScrollView(bodyScroll);

        // ------------------------------------------------------------ Kopf
        //
        // Vor dem ChangeCheck: Tippen im Umbenennen-Feld ist noch keine
        // Aenderung am Gegner - erst der Knopf ist eine.
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(d.name, EditorStyles.boldLabel);
        if (!renameOpen && GUILayout.Button("Umbenennen", GUILayout.Width(110f)))
        {
            renameOpen = true;
            renameName = d.name;
            renameId = d.code;
            renameIdTouched = false;
            renamePrefab = true;
            GUI.FocusControl(null);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField("Id im Code", "EnemyId." + d.code);

        if (renameOpen) DrawRenameBox(d);

        EditorGUI.BeginChangeCheck();

        DrawArchiveBox(d);
        DrawDeleteButton(d);

        EditorGUILayout.Space(6f);

        // ------------------------------------------------------------- Bild
        EditorGUILayout.LabelField("Bild und Animation", EditorStyles.boldLabel);
        DrawSheetField(d);
        LoadFrames(d);

        if (frames.Length == 0)
        {
            EditorGUILayout.HelpBox(
                string.IsNullOrEmpty(d.sheet)
                    ? "Noch kein Sprite-Sheet gewaehlt."
                    : "In diesem Sheet stecken keine Sprites. Ist es in den Import-"
                      + "Einstellungen auf \"Multiple\" gestellt und geschnitten?",
                MessageType.Warning);
        }
        else
        {
            DrawPreview(d);
        }

        EditorGUILayout.Space(6f);

        // ------------------------------------------------------- Kampfwerte
        EditorGUILayout.LabelField("Kampfwerte", EditorStyles.boldLabel);
        d.health = EditorGUILayout.FloatField(new GUIContent(
            "Leben", "Vor der Chaos-Skalierung. RunDifficulty rechnet im Lauf drauf."), d.health);
        d.damage = EditorGUILayout.FloatField(new GUIContent(
            "Schaden", "Pro Beruehrung, solange der Spieler im Gegner steht."), d.damage);
        d.speed = EditorGUILayout.FloatField(new GUIContent(
            "Tempo", "Der Spieler laeuft 4. Alles darueber holt ihn ein."), d.speed);
        d.exp = EditorGUILayout.IntField(new GUIContent(
            "Erfahrung", "Ab 50 faellt eine mittlere, ab 300 eine grosse Kugel."), d.exp);
        d.pushTime = EditorGUILayout.FloatField(new GUIContent(
            "Rueckstoss (s)", "Wie lange ein Treffer ihn zurueckdrueckt. 0 = gar nicht."), d.pushTime);

        d.role = (EnemyRole)EditorGUILayout.EnumPopup(new GUIContent(
            "Rolle", "Entscheidet ueber Truhe, Seelen und welche Erfolge zaehlen."), d.role);

        DrawRoleHint(d.role);
        DrawThreat(d);
        DrawWeaponCheck(d);

        EditorGUILayout.Space(6f);

        // ---------------------------------------------------------- Koerper
        EditorGUILayout.LabelField("Koerper", EditorStyles.boldLabel);
        if (KeepsOwnScale(d))
        {
            d.scale = EditorGUILayout.FloatField(new GUIContent(
                "Groesse", "Nur Bosse duerfen skaliert werden."), d.scale);
        }
        else
        {
            EditorGUILayout.LabelField(new GUIContent("Groesse",
                "Keine Skalierung - die Groesse kommt aus dem Pixelbild."),
                new GUIContent("1 (so gross wie gezeichnet, " + EnemyCatalog.PixelsPerUnit
                             + " Pixel = 1 Einheit)"));
            d.scale = 1f;
        }

        EditorGUILayout.BeginHorizontal();
        d.colliderRadius = EditorGUILayout.FloatField(new GUIContent(
            "Trefferkreis", "0 = aus dem Bild gerechnet."), d.colliderRadius);
        using (new EditorGUI.DisabledScope(frames.Length == 0))
        {
            if (GUILayout.Button("aus Bild", GUILayout.Width(70f)))
            {
                AutoCollider(d);
            }
        }
        EditorGUILayout.EndHorizontal();

        d.colliderOffset = EditorGUILayout.Vector2Field("Kreis-Versatz", d.colliderOffset);

        if (EditorGUI.EndChangeCheck()) dirty = true;

        EditorGUILayout.Space(10f);

        // ----------------------------------------------------------- Knoepfe
        DrawBuildButtons(d);

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawSheetField(Draft d)
    {
        Texture2D current = string.IsNullOrEmpty(d.sheet)
            ? null
            : AssetDatabase.LoadAssetAtPath<Texture2D>(d.sheet);

        Texture2D picked = (Texture2D)EditorGUILayout.ObjectField(
            new GUIContent("Sprite-Sheet", "Die PNG mit den Laufbildern."),
            current, typeof(Texture2D), false);

        if (picked != current)
        {
            d.sheet = picked != null ? AssetDatabase.GetAssetPath(picked) : "";
            framesFor = "";
            dirty = true;
        }
    }

    /// <summary>
    /// Die Vorschau. Zeigt das Bild so, wie es im Spiel stuende - inklusive
    /// Spiegelung, damit die Blickrichtung hier auffaellt und nicht erst dort.
    /// </summary>
    private void DrawPreview(Draft d)
    {
        // --- Tempo
        EditorGUILayout.BeginHorizontal();
        d.fps = EditorGUILayout.Slider(new GUIContent(
            "Tempo (Bilder/s)", "Jeder Gegner hat andere Bildzahlen - deshalb je Gegner eigen."),
            d.fps, 1f, 24f);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField(" ", string.Format(
            CultureInfo.InvariantCulture,
            "{0} Bilder, eine Runde dauert {1:0.00}s",
            frames.Length, frames.Length / Mathf.Max(0.001f, d.fps)),
            EditorStyles.miniLabel);

        // --- Blickrichtung
        d.facing = (EnemyFacing)EditorGUILayout.EnumPopup(new GUIContent(
            "Bild schaut nach", "Wie das Bild GEZEICHNET ist. Das Spiegeln folgt daraus."),
            d.facing);

        // --- Steuerung
        //
        // Ab hier wird nur die Vorschau bedient, nicht der Gegner geaendert.
        // Ohne das Zuruecksetzen von GUI.changed wuerde schon ein Druck auf
        // "Pause" den Katalog als ungespeichert markieren.
        bool changedBefore = GUI.changed;

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(playing ? "Pause" : "Abspielen", GUILayout.Width(80f)))
        {
            playing = !playing;
            playStarted = EditorApplication.timeSinceStartup;
        }

        previewPlayerRight = GUILayout.Toggle(previewPlayerRight,
            previewPlayerRight ? "Spieler steht rechts" : "Spieler steht links",
            "Button", GUILayout.Width(150f));

        GUILayout.Label("Zoom", GUILayout.Width(36f));
        previewZoom = GUILayout.HorizontalSlider(previewZoom, 1f, 10f, GUILayout.Width(80f));

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        // --- Einzelbild, wenn pausiert
        int frame;
        if (playing)
        {
            double elapsed = EditorApplication.timeSinceStartup - playStarted;
            frame = Mathf.Abs((int)(elapsed * d.fps)) % frames.Length;
            scrubFrame = frame;
        }
        else
        {
            scrubFrame = EditorGUILayout.IntSlider("Bild", scrubFrame, 0, frames.Length - 1);
            frame = scrubFrame;
        }

        GUI.changed = changedBefore;

        // --- Bild
        Rect box = GUILayoutUtility.GetRect(10f, 170f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(box, new Color(0.16f, 0.16f, 0.18f));
        DrawSprite(box, frames[frame], Flipped(d.facing, previewPlayerRight), d.scale);

        GUI.Label(new Rect(box.x + 6f, box.y + 4f, 260f, 16f),
                  "Bild " + (frame + 1) + " / " + frames.Length, EditorStyles.miniLabel);

        if (d.facing == EnemyFacing.Neutral)
        {
            GUI.Label(new Rect(box.x + 6f, box.yMax - 18f, 400f, 16f),
                      "Neutral: wird nie gespiegelt.", EditorStyles.miniLabel);
        }
    }

    /// <summary>
    /// Dieselbe Rechnung wie in <see cref="Enemy"/>: schaut das Bild nach
    /// rechts, wird gespiegelt, wenn der Spieler links steht - und umgekehrt.
    /// </summary>
    private static bool Flipped(EnemyFacing facing, bool playerIsRight)
    {
        switch (facing)
        {
            case EnemyFacing.ArtFacesRight: return !playerIsRight;
            case EnemyFacing.ArtFacesLeft: return playerIsRight;
            default: return false;
        }
    }

    private void DrawSprite(Rect box, Sprite sprite, bool flip, float scale)
    {
        if (sprite == null || sprite.texture == null) return;

        Texture2D tex = sprite.texture;
        Rect r = sprite.rect;

        float pixels = Mathf.Max(1f, previewZoom) * Mathf.Max(0.1f, scale);
        float w = r.width * pixels;
        float h = r.height * pixels;

        // Passt es nicht in den Kasten, gleichmaessig kleiner rechnen - sonst
        // waere die Vorschau bei grossen Bossen nur noch ein Ausschnitt.
        float fit = Mathf.Min(1f, Mathf.Min((box.width - 20f) / w, (box.height - 30f) / h));
        w *= fit;
        h *= fit;

        var dest = new Rect(box.x + (box.width - w) * 0.5f,
                            box.y + (box.height - h) * 0.5f, w, h);

        var uv = new Rect(r.x / tex.width, r.y / tex.height,
                          r.width / tex.width, r.height / tex.height);

        if (flip)
        {
            uv.x += uv.width;
            uv.width = -uv.width;
        }

        GUI.DrawTextureWithTexCoords(dest, tex, uv, true);
    }

    private void DrawRoleHint(EnemyRole role)
    {
        string text;
        switch (role)
        {
            case EnemyRole.Normal:
                text = "Zaehlt auf Kill100 / Kill1000 / Kill10000.";
                break;
            case EnemyRole.MiniBoss:
                text = "Truhe, 1 Seele, Kill10Miniboss / Kill100Miniboss. "
                     + "Raeumt beim Sterben die Kaefig-Wand weg. Nicht schiebbar.";
                break;
            case EnemyRole.Boss:
                text = "Laeuft NICHT von selbst - ein eigenes Skript muss ihn steuern "
                     + "(wie EnemyKeckKoenig). 10 Seelen, ruft danach den Tod.";
                break;
            case EnemyRole.DeathBoss:
                text = "Truhe, 50 Seelen, Erfolg \"Death\", zieht alle XP an.";
                break;
            default:
                text = "Kaefig-Wand: zaehlt nicht zum Druck und gibt nichts.";
                break;
        }

        EditorGUILayout.HelpBox(text, MessageType.None);
    }

    /// <summary>
    /// Das Gewicht ist nicht mehr einstellbar - es folgt aus Leben, Schaden und
    /// Tempo. Hier steht, wie es zustande kommt, damit man sieht, an welchem
    /// Wert man drehen muss, wenn ein Gegner zu dicht oder zu duenn kommt.
    /// </summary>
    private static void DrawThreat(Draft d)
    {
        float threat = d.Threat;

        EditorGUILayout.LabelField(new GUIContent("Gewicht (automatisch)",
            "Was er im Druck-Budget des Spawn-Directors zaehlt. Marshmello = 1."),
            new GUIContent(threat.ToString("0.#", CultureInfo.InvariantCulture)),
            EditorStyles.boldLabel);

        string text;
        if (d.role == EnemyRole.Blocker)
        {
            text = "Kaefig-Wand: wiegt nichts.";
        }
        else
        {
            text = string.Format(CultureInfo.InvariantCulture,
                "= Leben x{0:0.00}  *  Schaden x{1:0.00}  *  Tempo x{2:0.00}   (gegen Marshmello)\n"
              + "Bei Druck 100 stehen davon etwa {3:0} gleichzeitig auf dem Feld.",
                Mathf.Pow(Mathf.Max(1f, d.health) / EnemyCatalog.RefHealth, EnemyCatalog.HealthExponent),
                Mathf.Pow(Mathf.Max(0.5f, d.damage) / EnemyCatalog.RefDamage, EnemyCatalog.DamageExponent),
                Mathf.Pow(Mathf.Max(0.3f, d.speed) / EnemyCatalog.RefSpeed, EnemyCatalog.SpeedExponent),
                100f / Mathf.Max(0.1f, threat));

            if (d.role == EnemyRole.MiniBoss || d.role == EnemyRole.Boss || d.role == EnemyRole.DeathBoss)
            {
                text += "\nBosse zaehlen im Director nicht zum Druck - die Zahl ist nur zum Vergleich.";
            }
        }

        EditorGUILayout.HelpBox(text, MessageType.None);
    }

    // ------------------------------------------------------- Startwaffen

    private const string PlayerPrefab = "Assets/Prefabs/Player.prefab";

    private struct StartWeapon
    {
        public string character;
        public string weapon;
        public float damage;
    }

    private List<StartWeapon> startWeapons;

    /// <summary>
    /// Die Startwaffe jedes Charakters auf Stufe 1, direkt aus dem Player-Prefab
    /// gelesen - aendert jemand dort den Schaden, stimmt die Anzeige sofort mit.
    /// </summary>
    private List<StartWeapon> StartWeapons()
    {
        if (startWeapons != null) return startWeapons;

        startWeapons = new List<StartWeapon>();

        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
        if (player == null) return startWeapons;

        Weapon[] weapons = player.GetComponentsInChildren<Weapon>(true);

        for (int c = 0; c < Characters.Count; c++)
        {
            string id = Characters.StartWeaponId(c);
            Weapon weapon = weapons.FirstOrDefault(w => w.weaponID == id);
            if (weapon == null || weapon.stats == null || weapon.stats.Count == 0) continue;

            startWeapons.Add(new StartWeapon
            {
                character = Characters.NameOf(c),
                weapon = id,
                damage = weapon.stats[0].damage,
            });
        }

        return startWeapons;
    }

    /// <summary>
    /// Wie viele Treffer der Gegner am Anfang eines Laufs aushaelt - je
    /// Startwaffe, auf Stufe 1, ohne Buffs und Krits. Der Marshmello faellt mit
    /// jeder davon beim ersten Treffer; daran sieht man am schnellsten, ob ein
    /// neuer Gegner fuer seine Phase zu zaeh ist.
    /// </summary>
    private void DrawWeaponCheck(Draft d)
    {
        if (d.role == EnemyRole.Blocker) return;

        List<StartWeapon> list = StartWeapons();
        if (list.Count == 0) return;

        var sb = new StringBuilder("Treffer bis tot, Startwaffen auf Stufe 1 (Chaos 1):");
        foreach (StartWeapon w in list)
        {
            int hits = Mathf.CeilToInt(d.health / Mathf.Max(0.01f, w.damage));
            sb.Append(string.Format(CultureInfo.InvariantCulture,
                "\n  {0} ({1}): {2:0.#} Schaden je Treffer -> {3} Treffer",
                w.character, w.weapon, w.damage, hits));
        }

        EditorGUILayout.HelpBox(sb.ToString(), MessageType.None);
    }

    // ------------------------------------------------------ Neuer Gegner

    /// <summary>
    /// Ein neuer Gegner braucht zwei Dinge im Code: einen Namen im Enum
    /// <see cref="EnemyId"/> und einen Eintrag im Katalog. Beides schreibt
    /// dieser Knopf in einem Zug; danach kompiliert Unity neu, und der Gegner
    /// steht in der Liste - gewaehlt, fertig zum Bearbeiten.
    ///
    /// Der Enum-Name kommt ans ENDE der Liste. Die Ids stehen in Prefabs und
    /// im SpawnCatalog als Zahl - ein Name weiter oben wuerde alle dahinter
    /// verschieben.
    /// </summary>
    private void DrawNewEnemyBox()
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Neuer Gegner", EditorStyles.boldLabel);

        newEnemyName = EditorGUILayout.TextField(newEnemyName);

        Draft template = selected >= 0 && selected < drafts.Count ? drafts[selected] : null;
        if (template != null)
        {
            newEnemyCopy = EditorGUILayout.ToggleLeft(new GUIContent(
                "Werte von \"" + template.name + "\"",
                "Leben, Schaden, Tempo, Rolle usw. uebernehmen. Aus = Marshmello-Werte."),
                newEnemyCopy);
        }

        bool hasName = !string.IsNullOrWhiteSpace(newEnemyName);
        string id = ToIdentifier(newEnemyName);
        string problem = hasName ? IdProblem(id, null, true) : null;

        if (hasName)
        {
            EditorGUILayout.LabelField("Id: EnemyId." + id, EditorStyles.miniLabel);
        }

        if (problem != null) EditorGUILayout.HelpBox(problem, MessageType.Warning);

        using (new EditorGUI.DisabledScope(!hasName || problem != null || EditorApplication.isCompiling))
        {
            if (GUILayout.Button("Anlegen"))
            {
                CreateEnemy(newEnemyName.Trim(), id, newEnemyCopy ? template : null);
            }
        }

        if (EditorApplication.isCompiling)
        {
            EditorGUILayout.HelpBox("Unity kompiliert gerade ...", MessageType.None);
        }

        EditorGUILayout.Space(4f);
    }

    private void CreateEnemy(string name, string id, Draft template)
    {
        if (dirty && !EditorUtility.DisplayDialog("Neuer Gegner",
                "Beim Anlegen wird der Katalog gespeichert - auch deine anderen "
              + "ungespeicherten Aenderungen.", "Anlegen", "Abbrechen"))
        {
            return;
        }

        // Marshmello-Werte als Grundlage: der ist das Mass fuer alles andere.
        var d = new Draft
        {
            id = EnemyId.None,
            code = id,
            name = name,
            health = EnemyCatalog.RefHealth,
            damage = EnemyCatalog.RefDamage,
            speed = EnemyCatalog.RefSpeed,
            exp = 1,
            pushTime = 0.25f,
            role = EnemyRole.Normal,
            facing = EnemyFacing.Neutral,
            sheet = "",
            fps = 8f,
            colliderRadius = 0f,
            colliderOffset = Vector2.zero,
            scale = 1f,
            prefab = "",
        };

        // Bild und Prefab bewusst NICHT: sonst teilen sich zwei Gegner eine
        // Prefab-Datei, und der naechste Bau ueberschreibt den anderen.
        if (template != null)
        {
            d.health = template.health;
            d.damage = template.damage;
            d.speed = template.speed;
            d.exp = template.exp;
            d.pushTime = template.pushTime;
            d.role = template.role;
            d.facing = template.facing;
            d.fps = template.fps;
            if (KeepsOwnScale(d)) d.scale = template.scale;
        }

        if (!SaveCatalog(d, null, null)) return;

        // Der Code hat sich in jedem Fall geaendert (neuer Katalog-Eintrag),
        // Unity kompiliert also auch dann neu, wenn die Id schon im Enum stand.

        SessionState.SetString(SelectAfterReloadKey, id);
        newEnemyName = "";

        Report(name + " ist angelegt (EnemyId." + id + ").\n\n"
             + "Unity kompiliert jetzt kurz neu, danach steht er gewaehlt in der Liste.\n\n"
             + "Dann: Sprite-Sheet waehlen, Werte einstellen, \"Katalog speichern\", "
             + "\"Prefab anlegen\" und \"In GameCore eintragen\". In einen Wellenplan "
             + "kommt er ueber Tools -> Gegner -> Wellenplaene.");

        GUIUtility.ExitGUI();
    }

    // -------------------------------------------------------- Umbenennen

    /// <summary>
    /// Umbenennen auf Wunsch bis in den Code: der Enum-Name wird ueberall im
    /// Projekt mitgezogen (Wellenplaene, Skripte), die Prefab-Datei wird
    /// umbenannt statt neu angelegt. Die Zahl hinter dem Enum-Namen bleibt
    /// dieselbe - Prefabs und SpawnCatalog merken also nichts davon.
    /// </summary>
    private void DrawRenameBox(Draft d)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        string before = renameName;
        renameName = EditorGUILayout.TextField("Neuer Name", renameName);

        // Die Id laeuft dem Namen hinterher, bis jemand sie selbst anfasst.
        if (!renameIdTouched && renameName != before) renameId = ToIdentifier(renameName);

        string typedId = EditorGUILayout.TextField(new GUIContent("Id im Code",
            "Der Name im Code (EnemyId.xxx). Wird in allen Skripten mitgeaendert."),
            renameId);
        if (typedId != renameId)
        {
            renameId = typedId;
            renameIdTouched = true;
        }

        bool hasPrefab = PrefabExists(d);
        string newPrefabName = SafeName(renameName);
        bool prefabNameChanges = hasPrefab
            && Path.GetFileNameWithoutExtension(d.prefab) != newPrefabName;

        if (prefabNameChanges)
        {
            renamePrefab = EditorGUILayout.ToggleLeft(
                "Prefab-Datei mitumbenennen (" + newPrefabName + ".prefab)", renamePrefab);
        }

        string problem = string.IsNullOrWhiteSpace(renameName)
            ? "Der Name ist leer."
            : IdProblem(renameId, d.code, false);

        if (problem != null) EditorGUILayout.HelpBox(problem, MessageType.Warning);

        if (problem == null && renameId != d.code)
        {
            EditorGUILayout.HelpBox("EnemyId." + d.code + " wird in allen Skripten zu EnemyId."
                                  + renameId + ". Danach kompiliert Unity neu.", MessageType.None);
        }

        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(problem != null || EditorApplication.isCompiling))
        {
            if (GUILayout.Button("Umbenennen"))
            {
                RenameEnemy(d, renameName.Trim(), renameId, prefabNameChanges && renamePrefab);
            }
        }
        if (GUILayout.Button("Abbrechen"))
        {
            renameOpen = false;
            GUI.FocusControl(null);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void RenameEnemy(Draft d, string name, string id, bool renamePrefabFile)
    {
        if (dirty && !EditorUtility.DisplayDialog("Umbenennen",
                "Beim Umbenennen wird der Katalog gespeichert - auch deine anderen "
              + "ungespeicherten Aenderungen.", "Umbenennen", "Abbrechen"))
        {
            return;
        }

        string oldCode = d.code;
        var notes = new StringBuilder();

        if (renamePrefabFile)
        {
            // RenameAsset behaelt die guid - alles, was auf das Prefab zeigt,
            // zeigt danach weiter darauf.
            string newPrefabName = SafeName(name);
            string error = AssetDatabase.RenameAsset(d.prefab, newPrefabName);
            if (string.IsNullOrEmpty(error))
            {
                d.prefab = Path.GetDirectoryName(d.prefab).Replace('\\', '/') + "/" + newPrefabName + ".prefab";
            }
            else
            {
                notes.AppendLine("Prefab-Datei nicht umbenannt: " + error);
            }
        }

        d.name = name;
        d.code = id;

        bool codeChanges = oldCode != id;

        if (!SaveCatalog(null, codeChanges ? oldCode : null, codeChanges ? id : null))
        {
            d.code = oldCode;
            return;
        }

        if (codeChanges)
        {
            List<string> touched = RenameInScripts(oldCode, id);
            notes.AppendLine("EnemyId." + oldCode + " -> EnemyId." + id
                           + (touched.Count > 0 ? " auch in:\n  " + string.Join("\n  ", touched) : ""));
        }

        AssetDatabase.Refresh();

        renameOpen = false;
        SessionState.SetString(SelectAfterReloadKey, id);

        Report("Umbenannt in \"" + name + "\".\n\n" + notes);

        GUIUtility.ExitGUI();
    }

    /// <summary>
    /// Zieht EnemyId.Alt -> EnemyId.Neu in allen Skripten unter Assets nach
    /// (der Katalog selbst wird in <see cref="SaveCatalog"/> erledigt). Kodierung
    /// und Zeilenenden bleiben, wie sie waren.
    /// </summary>
    private static List<string> RenameInScripts(string oldCode, string newCode)
    {
        var touched = new List<string>();
        var pattern = new Regex(@"\bEnemyId\." + Regex.Escape(oldCode) + @"\b");
        string catalogFull = Path.GetFullPath(CatalogPath);

        foreach (string file in Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories))
        {
            if (Path.GetFullPath(file) == catalogFull) continue;

            byte[] bytes = File.ReadAllBytes(file);
            bool bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            string text = new UTF8Encoding(false).GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));

            if (!pattern.IsMatch(text)) continue;

            File.WriteAllText(file, pattern.Replace(text, "EnemyId." + newCode), new UTF8Encoding(bom));
            touched.Add("Assets" + file.Substring(Application.dataPath.Length).Replace('\\', '/'));
        }

        return touched;
    }

    // ------------------------------------------------------------ Namen

    /// <summary>
    /// Macht aus einem Anzeigenamen einen Namen, den C# frisst:
    /// "Pilz-Koenig (Miniboss)" -> "PilzKoenigMiniboss".
    /// </summary>
    private static string ToIdentifier(string name)
    {
        string s = (name ?? "")
            .Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue")
            .Replace("Ä", "Ae").Replace("Ö", "Oe").Replace("Ü", "Ue")
            .Replace("ß", "ss");

        var sb = new StringBuilder();
        bool upper = true;
        foreach (char c in s)
        {
            if (c < 128 && char.IsLetterOrDigit(c))
            {
                sb.Append(upper ? char.ToUpperInvariant(c) : c);
                upper = false;
            }
            else
            {
                upper = true;
            }
        }

        if (sb.Length > 0 && char.IsDigit(sb[0])) sb.Insert(0, "Gegner");
        return sb.ToString();
    }

    /// <summary>
    /// Warum die Id nicht geht - oder null, wenn sie passt. <paramref name="own"/>
    /// ist die bisherige Id beim Umbenennen (die darf bleiben).
    ///
    /// Beim Anlegen darf eine Id wiederverwendet werden, die noch im Enum
    /// steht, aber keinen Katalog-Eintrag mehr hat (frueher geloeschte Gegner) -
    /// dann kommt nur der Eintrag dazu, das Enum bleibt, wie es ist.
    /// </summary>
    private string IdProblem(string id, string own, bool creating)
    {
        if (string.IsNullOrEmpty(id)) return "Daraus laesst sich keine Id machen - bitte Buchstaben verwenden.";

        if (!Regex.IsMatch(id, "^[A-Za-z_][A-Za-z0-9_]*$"))
        {
            return "Die Id darf nur Buchstaben (ohne Umlaute), Ziffern und _ enthalten "
                 + "und nicht mit einer Ziffer anfangen.";
        }

        if (id == own) return null;

        // Ohne Ruecksicht auf Gross/Klein: Windows unterscheidet bei den
        // Prefab-Dateien auch nicht.
        if (drafts.Any(x => string.Equals(x.code, id, System.StringComparison.OrdinalIgnoreCase)))
        {
            return "EnemyId." + id + " gibt es schon.";
        }

        foreach (string n in System.Enum.GetNames(typeof(EnemyId)))
        {
            if (!string.Equals(n, id, System.StringComparison.OrdinalIgnoreCase)) continue;

            if (creating && n == id && n != EnemyId.None.ToString()) return null;
            return "EnemyId." + n + " steht schon im Code.";
        }

        return null;
    }

    private static bool EnumHas(string id)
    {
        return System.Enum.GetNames(typeof(EnemyId)).Contains(id);
    }

    // ---------------------------------------------------------------- Archiv

    private void DrawArchiveBox(Draft d)
    {
        if (d.archived)
        {
            EditorGUILayout.HelpBox(
                "Liegt im Archiv. Werte, Bild und Prefab sind alle noch da - er taucht "
              + "nur in den Listen nicht mehr auf.", MessageType.Info);

            if (GUILayout.Button("Aus dem Archiv holen"))
            {
                d.archived = false;
                dirty = true;
            }
            return;
        }

        if (GUILayout.Button(new GUIContent("Ins Archiv legen",
                "Nicht loeschen, nur weglegen. Zurueckholen geht jederzeit."),
                GUILayout.Width(130f)))
        {
            List<string> used = PlanUsage(d.id);
            if (used.Count > 0 && !EditorUtility.DisplayDialog("Ins Archiv?",
                    d.name + " steht noch in einem Wellenplan:\n\n  "
                  + string.Join("\n  ", used) + "\n\n"
                  + "Dort spawnt er weiter, bis du ihn in der Wellenplan-Werkstatt "
                  + "austauschst. Im Archiv ist er nur aus den Listen raus.",
                    "Trotzdem ins Archiv", "Abbrechen"))
            {
                return;
            }

            d.archived = true;
            showArchive = true;
            dirty = true;
        }
    }

    // --------------------------------------------------------------- Loeschen

    /// <summary>
    /// Endgueltig weg - anders als das Archiv. Nimmt den Eintrag aus dem
    /// Katalog und auf Wunsch das Prefab mit. Die Id bleibt im Enum stehen:
    /// der SpawnCatalog in GameCore speichert sie als Zahl, und ein
    /// geloeschter Name wuerde alle Ids dahinter verrutschen lassen.
    /// </summary>
    private void DrawDeleteButton(Draft d)
    {
        if (!GUILayout.Button(new GUIContent("Loeschen",
                "Endgueltig aus dem Katalog nehmen, auf Wunsch mit Prefab."),
                GUILayout.Width(130f)))
        {
            return;
        }

        // Steht er noch in einem Plan, wuerde dort stumm eine Luecke
        // entstehen. Erst austauschen, dann loeschen.
        List<string> used = PlanUsage(d.id);
        if (used.Count > 0)
        {
            Report(d.name + " steht noch in einem Wellenplan:\n\n  "
                 + string.Join("\n  ", used) + "\n\n"
                 + "Erst in der Wellenplan-Werkstatt austauschen, dann loeschen.");
            return;
        }

        // Teilt sich ein anderer Eintrag das Prefab, bleibt die Datei liegen.
        bool prefabShared = drafts.Any(x => x != d && x.prefab == d.prefab);
        bool prefabExists = !prefabShared && PrefabExists(d);

        string message = d.name + " endgueltig loeschen?\n\n"
                       + "Der Katalog wird dabei sofort gespeichert"
                       + (dirty ? " - auch deine anderen ungespeicherten Aenderungen." : ".")
                       + (prefabExists ? "\n\nPrefab: " + d.prefab : "");

        int choice = prefabExists
            ? EditorUtility.DisplayDialogComplex("Gegner loeschen", message,
                  "Mit Prefab loeschen", "Abbrechen", "Nur Katalog-Eintrag")
            : (EditorUtility.DisplayDialog("Gegner loeschen", message, "Loeschen", "Abbrechen") ? 2 : 1);

        if (choice == 1) return;

        drafts.Remove(d);
        dirty = true;
        SaveCatalog();

        if (choice == 0) AssetDatabase.DeleteAsset(d.prefab);

        selected = Mathf.Clamp(selected, 0, Mathf.Max(0, drafts.Count - 1));
        framesFor = "";

        // Der Rest des Fensters wuerde sonst noch den geloeschten Eintrag zeichnen.
        GUIUtility.ExitGUI();
    }

    /// <summary>Wo der Gegner in den Wellenplaenen vorkommt, als lesbare Zeilen.</summary>
    private static List<string> PlanUsage(EnemyId id)
    {
        var result = new List<string>();

        foreach (string planId in WavePlans.AllIds)
        {
            RunPlan plan = WavePlans.For(planId);

            var phases = new List<Phase>(plan.Phases);
            if (plan.Endless != null) phases.Add(plan.Endless);

            foreach (Phase phase in phases)
            {
                bool inPool = phase.Enemies.Any(e => e.Id == id);
                bool inBeat = phase.Beats.Any(b => b.Enemy == id || b.RingEnemy == id);

                if (inPool || inBeat)
                {
                    result.Add(planId + " / " + phase.Name + (inPool ? " (Pool)" : " (Beat)"));
                }
            }
        }

        return result;
    }

    private void DrawBuildButtons(Draft d)
    {
        bool canBuild = frames.Length > 0;

        bool exists = PrefabExists(d);

        using (new EditorGUI.DisabledScope(!canBuild))
        {
            if (GUILayout.Button(exists ? "Prefab neu bauen" : "Prefab anlegen",
                                 GUILayout.Height(30f))
                && ConfirmOverwrite(d, exists))
            {
                string result = BuildPrefab(d);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Report(result);
            }
        }

        if (exists)
        {
            EditorGUILayout.HelpBox(
                "Dieses Prefab gibt es schon. Neu bauen setzt Sprite, Trefferkreis, "
              + "Animator, Tag und Layer auf den Stand aus diesem Fenster. Zusaetzliche "
              + "Bauteile am Prefab bleiben stehen.",
                MessageType.Warning);
        }

        if (!canBuild)
        {
            EditorGUILayout.HelpBox("Ohne Sprite-Sheet kann kein Prefab gebaut werden.",
                                    MessageType.Info);
        }

        if (!string.IsNullOrEmpty(d.prefab))
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(d.prefab);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Prefab", d.prefab);
            using (new EditorGUI.DisabledScope(asset == null))
            {
                if (GUILayout.Button("zeigen", GUILayout.Width(60f)))
                {
                    EditorGUIUtility.PingObject(asset);
                    Selection.activeObject = asset;
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    // ---------------------------------------------------------------- Frames

    private void LoadFrames(Draft d)
    {
        if (framesFor == d.sheet) return;

        framesFor = d.sheet;
        frames = LoadSprites(d.sheet);
        scrubFrame = 0;
        playStarted = EditorApplication.timeSinceStartup;
    }

    /// <summary>
    /// Alle Einzelbilder eines Sheets, in der richtigen Reihenfolge.
    ///
    /// Die normale Sortierung nach Name reicht nicht: "_10" kaeme vor "_2", und
    /// eine zwoelfteilige Animation liefe in falscher Reihenfolge. Deshalb wird
    /// die Zahl am Ende des Namens ausgelesen und danach sortiert.
    /// </summary>
    private static Sprite[] LoadSprites(string sheetPath)
    {
        if (string.IsNullOrEmpty(sheetPath)) return new Sprite[0];

        return AssetDatabase.LoadAllAssetsAtPath(sheetPath)
                            .OfType<Sprite>()
                            .OrderBy(TrailingNumber)
                            .ThenBy(s => s.name, System.StringComparer.Ordinal)
                            .ToArray();
    }

    private static int TrailingNumber(Sprite sprite)
    {
        string name = sprite.name;
        int end = name.Length;
        while (end > 0 && char.IsDigit(name[end - 1])) end--;

        if (end >= name.Length) return int.MaxValue;   // keine Zahl am Ende
        return int.TryParse(name.Substring(end), out int value) ? value : int.MaxValue;
    }

    private void AutoCollider(Draft d)
    {
        if (frames.Length == 0) return;

        Bounds b = frames[0].bounds;
        d.colliderRadius = Mathf.Max(0.05f, Mathf.Min(b.extents.x, b.extents.y) * 0.9f);
        d.colliderOffset = new Vector2(b.center.x, b.center.y);
        dirty = true;
    }

    // --------------------------------------------------------------- Bauen

    /// <summary>
    /// Baut nur die Gegner, zu denen noch KEIN Prefab liegt.
    ///
    /// Vorhandene bleiben unangetastet, auch wenn im Katalog ein Sheet zu
    /// ihnen steht. Ein bestehendes Prefab ist ueber Monate gewachsen - da
    /// haengen Spezial-Skripte dran, angepasste Collider, eigene Controller.
    /// Wer eines davon wirklich neu bauen will, macht das einzeln ueber den
    /// Knopf beim Gegner und bekommt vorher die Rueckfrage.
    /// </summary>
    private void BuildMissing()
    {
        var report = new StringBuilder();
        int built = 0;
        int skipped = 0;

        try
        {
            for (int i = 0; i < drafts.Count; i++)
            {
                Draft d = drafts[i];
                if (d.archived || string.IsNullOrEmpty(d.sheet)) continue;

                if (PrefabExists(d))
                {
                    skipped++;
                    continue;
                }

                EditorUtility.DisplayProgressBar("Gegner-Werkstatt", d.name,
                                                 (float)i / drafts.Count);

                report.AppendLine(BuildPrefab(d));
                built++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Report(built + " Gegner neu gebaut, " + skipped
             + " vorhandene nicht angefasst.\n\n" + report);
    }

    private static bool PrefabExists(Draft d)
    {
        return !string.IsNullOrEmpty(d.prefab)
            && AssetDatabase.LoadAssetAtPath<GameObject>(d.prefab) != null;
    }

    /// <summary>
    /// Rueckfrage, bevor ein gewachsenes Prefab neu gebaut wird. Bei einem neu
    /// angelegten gibt es nichts zu verlieren - da faellt sie weg.
    /// </summary>
    private static bool ConfirmOverwrite(Draft d, bool exists)
    {
        if (!exists) return true;

        return EditorUtility.DisplayDialog(
            "Prefab neu bauen?",
            d.name + " liegt schon unter\n" + d.prefab + "\n\n"
          + "Neu bauen ueberschreibt Sprite, Trefferkreis, Groesse, Animator, Tag "
          + "und Layer mit dem Stand aus diesem Fenster. Alles andere am Prefab "
          + "bleibt stehen.",
            "Neu bauen", "Abbrechen");
    }

    /// <summary>
    /// Baut Clip, Controller und Prefab. Ein vorhandenes Prefab wird
    /// aktualisiert statt ersetzt - wer spaeter von Hand ein Bauteil
    /// dazugehaengt hat, verliert es dabei nicht.
    /// </summary>
    private string BuildPrefab(Draft d)
    {
        // Vor dem Laden der Sprites: deren Masse (und damit der Trefferkreis)
        // haengen an der Pixeldichte.
        if (!KeepsOwnScale(d)) SetPixelsPerUnit(d.sheet);

        Sprite[] sprites = LoadSprites(d.sheet);
        if (sprites.Length == 0) return d.name + ": kein Sprite im Sheet - uebersprungen.";

        EnsureFolder(AnimFolder);
        EnsureFolder(PrefabFolder);

        string safeName = SafeName(d.name);

        AnimationClip clip = BuildClip(d, sprites, safeName);
        AnimatorController controller = BuildController(clip, safeName);

        if (string.IsNullOrEmpty(d.prefab))
        {
            d.prefab = PrefabFolder + "/" + safeName + ".prefab";
            dirty = true;
        }

        EnsureFolder(Path.GetDirectoryName(d.prefab).Replace('\\', '/'));

        bool existed = AssetDatabase.LoadAssetAtPath<GameObject>(d.prefab) != null;

        GameObject root = existed
            ? PrefabUtility.LoadPrefabContents(d.prefab)
            : new GameObject(safeName);

        try
        {
            Assemble(root, d, sprites[0], controller);

            PrefabUtility.SaveAsPrefabAsset(root, d.prefab);
        }
        finally
        {
            if (existed) PrefabUtility.UnloadPrefabContents(root);
            else DestroyImmediate(root);
        }

        return string.Format(CultureInfo.InvariantCulture,
            "{0}: {1} Bilder @ {2:0.#} fps, Prefab {3}.",
            d.name, sprites.Length, d.fps, existed ? "aktualisiert" : "angelegt");
    }

    /// <summary>
    /// Steckt die Bauteile zusammen. Alles ueber GetOrAdd, damit ein zweiter
    /// Lauf nichts doppelt anlegt und nichts Fremdes wegwirft.
    /// </summary>
    private void Assemble(GameObject root, Draft d, Sprite first, AnimatorController controller)
    {
        root.name = SafeName(d.name);

        // Tag und Layer sind das, was beim Handbau am haeufigsten fehlt: ohne
        // den Tag "Enemy" findet KEINE Waffe den Gegner, er ist unverwundbar,
        // und es gibt keine Fehlermeldung dazu.
        root.tag = "Enemy";
        root.layer = EnemyLayer();

        root.transform.localScale = Vector3.one * Mathf.Max(0.01f, d.scale);

        SpriteRenderer renderer = GetOrAdd<SpriteRenderer>(root);
        renderer.sprite = first;
        renderer.sortingLayerName = SortingLayer;
        renderer.sortingOrder = 0;

        Material template = TemplateMaterial();
        if (template != null) renderer.sharedMaterial = template;

        Rigidbody2D body = GetOrAdd<Rigidbody2D>(root);
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.interpolation = RigidbodyInterpolation2D.None;
        body.collisionDetectionMode = CollisionDetectionMode2D.Discrete;

        CircleCollider2D collider = GetOrAdd<CircleCollider2D>(root);
        if (d.colliderRadius > 0f)
        {
            collider.radius = d.colliderRadius;
            collider.offset = d.colliderOffset;
        }
        else
        {
            Bounds b = first.bounds;
            collider.radius = Mathf.Max(0.05f, Mathf.Min(b.extents.x, b.extents.y) * 0.9f);
            collider.offset = new Vector2(b.center.x, b.center.y);
        }

        Animator animator = GetOrAdd<Animator>(root);
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        Enemy enemy = GetOrAdd<Enemy>(root);
        enemy.EditorSetId(d.id);
        enemy.EditorBind(renderer, body, enemy.EditorDestroyEffect != null
                                          ? enemy.EditorDestroyEffect
                                          : DefaultDestroyEffect());

        // Das alte EnemyTeleport macht dasselbe wie das Nachziehen im
        // SpawnDirector, nur ungebuendelt und gegen dessen Spawn-Muster. An
        // einem neu gebauten Gegner hat es nichts mehr verloren.
        EnemyTeleport stale = root.GetComponent<EnemyTeleport>();
        if (stale != null) DestroyImmediate(stale, true);

        EditorUtility.SetDirty(root);
    }

    private AnimationClip BuildClip(Draft d, Sprite[] sprites, string safeName)
    {
        string path = AnimFolder + "/" + safeName + ".anim";

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        bool fresh = clip == null;
        if (fresh) clip = new AnimationClip();

        // Der Name muss VOR dem Anlegen stehen: der Controller benennt seinen
        // Zustand danach, und beim zweiten Lauf wird dieser Zustand ueber den
        // Namen wiedergefunden statt ein zweiter danebengelegt.
        clip.name = safeName;
        clip.frameRate = Mathf.Max(1f, d.fps);

        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");

        // Ein Schluessel je Bild - plus einer am Ende mit dem letzten Bild.
        // Ohne den waere das letzte Bild nur einen Wimpernschlag lang zu sehen:
        // die Cliplaenge endet sonst exakt auf seinem Schluessel.
        var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        float step = 1f / Mathf.Max(1f, d.fps);

        for (int i = 0; i < sprites.Length; i++)
        {
            keys[i] = new ObjectReferenceKeyframe { time = i * step, value = sprites[i] };
        }
        keys[sprites.Length] = new ObjectReferenceKeyframe
        {
            time = sprites.Length * step,
            value = sprites[sprites.Length - 1],
        };

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        if (fresh) AssetDatabase.CreateAsset(clip, path);
        else EditorUtility.SetDirty(clip);

        return clip;
    }

    /// <summary>
    /// Ein Zustand, ein Clip, keine Parameter. Mehr braucht ein laufender
    /// Gegner nicht - und was nicht da ist, kann auch nicht falsch verdrahtet
    /// sein.
    /// </summary>
    private AnimatorController BuildController(AnimationClip clip, string safeName)
    {
        string path = AnimFolder + "/" + safeName + ".controller";

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(path, clip);
            return controller;
        }

        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        AnimatorState state = machine.states
            .Select(s => s.state)
            .FirstOrDefault(s => s != null && s.name == clip.name);

        if (state == null)
        {
            state = machine.AddState(clip.name);
        }

        state.motion = clip;
        machine.defaultState = state;

        EditorUtility.SetDirty(controller);
        return controller;
    }

    // ------------------------------------------------------------- Alt-Prefabs

    private class LegacyRow
    {
        public string path;
        public GameObject asset;
        public Enemy enemy;
        public EnemyId pick;
    }

    private List<LegacyRow> legacy;

    private void DrawLegacy()
    {
        EditorGUILayout.HelpBox(
            "Prefabs, an denen noch keine Id steht. Sie laufen unveraendert mit ihren "
          + "eigenen Werten weiter - der Umstieg passiert nur auf Knopfdruck, einzeln, "
          + "damit du jeden im Spiel gegenpruefen kannst.",
            MessageType.Info);

        if (GUILayout.Button("Prefabs suchen") || legacy == null)
        {
            ScanLegacy();
        }

        if (legacy == null) return;

        EditorGUILayout.LabelField(legacy.Count + " Prefab(s) noch auf dem alten Stand.",
                                   EditorStyles.boldLabel);

        legacyScroll = EditorGUILayout.BeginScrollView(legacyScroll);

        foreach (LegacyRow row in legacy.ToList())
        {
            if (row.asset == null || row.enemy == null) continue;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(Path.GetFileNameWithoutExtension(row.path),
                                       EditorStyles.boldLabel);
            if (GUILayout.Button("zeigen", GUILayout.Width(60f)))
            {
                EditorGUIUtility.PingObject(row.asset);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(row.path, EditorStyles.miniLabel);

            row.pick = (EnemyId)EditorGUILayout.EnumPopup("wird zu", row.pick);

            EnemyDef def = EnemyCatalog.Get(row.pick);
            if (def != null)
            {
                EditorGUILayout.LabelField("Katalog sagt", string.Format(
                    CultureInfo.InvariantCulture,
                    "Leben {0:0.#}, Schaden {1:0.#}, Tempo {2:0.##}, EXP {3}, Rolle {4}",
                    def.Health, def.Damage, def.Speed, def.Exp, def.Role));
            }

            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(row.pick == EnemyId.None))
            {
                if (GUILayout.Button("Werte des Prefabs in den Katalog holen"))
                {
                    PullIntoCatalog(row);
                }

                if (GUILayout.Button("Auf neues System umstellen"))
                {
                    Migrate(row);
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndScrollView();
    }

    private void ScanLegacy()
    {
        legacy = new List<LegacyRow>();

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { EnemyPrefabRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) continue;

            Enemy enemy = asset.GetComponent<Enemy>();
            if (enemy == null || enemy.Id != EnemyId.None) continue;

            legacy.Add(new LegacyRow
            {
                path = path,
                asset = asset,
                enemy = enemy,
                pick = GuessId(path),
            });
        }
    }

    /// <summary>Rateversuch anhand des Dateinamens - der Mensch bestaetigt.</summary>
    private static EnemyId GuessId(string path)
    {
        string file = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

        foreach (EnemyDef def in EnemyCatalog.All)
        {
            if (string.IsNullOrEmpty(def.Prefab)) continue;
            if (string.Equals(def.Prefab, path, System.StringComparison.OrdinalIgnoreCase))
            {
                return def.Id;
            }
        }

        if (file.Contains("marshmallow")) return EnemyId.Marshmello;
        if (file.Contains("evil_slime")) return EnemyId.EvilSlime;
        if (file.Contains("muffin")) return EnemyId.Muffin;
        if (file.Contains("ramen")) return EnemyId.Suppe;
        if (file.Contains("pencake")) return EnemyId.Pancake;
        if (file.Contains("saure_milch")) return EnemyId.SaureMilch;
        if (file.Contains("fett")) return EnemyId.Fetti;
        if (file.Contains("blocker")) return EnemyId.Blocker;
        if (file.Contains("slime")) return EnemyId.Slime;

        return EnemyId.None;
    }

    /// <summary>
    /// Holt die Zahlen aus dem alten Prefab in den Entwurf. So geht beim
    /// Umstellen kein ueber Monate eingestelltes Balancing verloren.
    /// </summary>
    private void PullIntoCatalog(LegacyRow row)
    {
        var so = new SerializedObject(row.enemy);

        Draft d = drafts.FirstOrDefault(x => x.id == row.pick);
        if (d == null) return;

        d.health   = so.FindProperty("health").floatValue;
        d.damage   = so.FindProperty("damage").floatValue;
        d.speed    = so.FindProperty("moveSpeed").floatValue;
        d.exp      = so.FindProperty("experienceToGive").intValue;
        d.pushTime = so.FindProperty("pushTime").floatValue;

        bool miniBoss  = so.FindProperty("MiniBoss").boolValue;
        bool bossBoss  = so.FindProperty("BossBoss").boolValue;
        bool deathBoss = so.FindProperty("Death_Boss").boolValue;

        d.role = deathBoss ? EnemyRole.DeathBoss
               : bossBoss ? EnemyRole.Boss
               : miniBoss ? EnemyRole.MiniBoss
               : d.role;

        // "rightlooking == true" hiess frueher: spiegeln, wenn der Spieler
        // rechts steht - das Bild schaut also nach links.
        d.facing = so.FindProperty("rightlooking").boolValue
            ? EnemyFacing.ArtFacesLeft
            : EnemyFacing.Neutral;

        d.prefab = row.path;

        dirty = true;
        selected = drafts.IndexOf(d);
        tab = 0;
        framesFor = "";

        Debug.Log("[Gegner-Werkstatt] Werte aus " + row.path + " nach " + d.name
                  + " geholt. Noch nicht gespeichert - \"Katalog speichern\" nicht vergessen.");
    }

    private void Migrate(LegacyRow row)
    {
        EnemyDef def = EnemyCatalog.Get(row.pick);
        if (def == null)
        {
            EditorUtility.DisplayDialog("Gegner-Werkstatt",
                "Zu " + row.pick + " gibt es keinen Katalog-Eintrag.", "Ok");
            return;
        }

        var so = new SerializedObject(row.enemy);
        string before = string.Format(CultureInfo.InvariantCulture,
            "Leben {0:0.#}, Schaden {1:0.#}, Tempo {2:0.##}, EXP {3}",
            so.FindProperty("health").floatValue,
            so.FindProperty("damage").floatValue,
            so.FindProperty("moveSpeed").floatValue,
            so.FindProperty("experienceToGive").intValue);

        string after = string.Format(CultureInfo.InvariantCulture,
            "Leben {0:0.#}, Schaden {1:0.#}, Tempo {2:0.##}, EXP {3}",
            def.Health, def.Damage, def.Speed, def.Exp);

        if (!EditorUtility.DisplayDialog("Umstellen?",
                Path.GetFileNameWithoutExtension(row.path) + " laeuft danach mit den "
              + "Katalogwerten von " + row.pick + ".\n\n"
              + "bisher:  " + before + "\n"
              + "danach:  " + after + "\n\n"
              + "Die alten Felder bleiben im Prefab stehen, zaehlen aber nicht mehr. "
              + "Zurueck geht es jederzeit, indem die Id wieder auf None steht.",
                "Umstellen", "Abbrechen"))
        {
            return;
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(row.path);
        try
        {
            Enemy enemy = contents.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.EditorSetId(row.pick);
                PrefabUtility.SaveAsPrefabAsset(contents, row.path);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        AssetDatabase.SaveAssets();
        ScanLegacy();

        Debug.Log("[Gegner-Werkstatt] " + row.path + " laeuft jetzt ueber den Katalog ("
                  + row.pick + ").");
    }

    // ----------------------------------------------------------------- Pruefung

    /// <summary>
    /// Die Handvoll Fehler, die ein Gegner-Prefab stumm kaputt machen - alle an
    /// einer Stelle sichtbar, statt einzeln im Spiel aufzufallen.
    /// </summary>
    // ------------------------------------------------------- Pixelgroesse

    /// <summary>Bosse behalten ihre Skalierung (Keks-Koenig: 1024px-Bild).</summary>
    private static bool KeepsOwnScale(Draft d)
    {
        return d.role == EnemyRole.Boss || d.role == EnemyRole.DeathBoss;
    }

    private static float PixelsPerUnitOf(string texturePath)
    {
        var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        return importer != null ? importer.spritePixelsToUnits : 0f;
    }

    /// <summary>Stellt ein Bild auf die gemeinsame Pixeldichte. true = geaendert.</summary>
    private static bool SetPixelsPerUnit(string texturePath)
    {
        if (string.IsNullOrEmpty(texturePath)) return false;

        var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer == null || Mathf.Approximately(importer.spritePixelsToUnits, EnemyCatalog.PixelsPerUnit))
        {
            return false;
        }

        importer.spritePixelsToUnits = EnemyCatalog.PixelsPerUnit;
        importer.SaveAndReimport();
        return true;
    }

    /// <summary>
    /// Bringt alle vorhandenen Gegner auf "so gross wie gezeichnet": Bild auf
    /// die gemeinsame Pixeldichte, Prefab auf Skalierung 1.
    ///
    /// Damit sich dabei nichts verschiebt, wird alles, was am Prefab in
    /// Einheiten gemessen ist, im selben Verhaeltnis mitgerechnet wie das
    /// Bild: Trefferkreis, Kind-Objekte (Schatten, Effekte). Das Verhaeltnis
    /// ist alte Pixeldichte / neue - die alte Skalierung faellt dabei heraus,
    /// weil sie in beiden Rechnungen gleich drinsteckt.
    ///
    /// Mehrmals ausfuehren schadet nicht: was schon passt, wird uebersprungen.
    /// </summary>
    private void UnifyPixelSize()
    {
        if (!EditorUtility.DisplayDialog("Pixelgroesse vereinheitlichen",
                "Alle Gegnerbilder (ausser Bossen) werden auf " + EnemyCatalog.PixelsPerUnit
              + " Pixel pro Einheit gestellt, alle Gegner-Prefabs auf Skalierung 1. "
              + "Trefferkreise und Kind-Objekte werden passend mitgerechnet.\n\n"
              + "Gegner werden dadurch so gross, wie sie gezeichnet sind - einige "
              + "deutlich kleiner oder groesser als jetzt."
              + (dirty ? "\n\nDer Katalog wird dabei gespeichert - auch deine anderen "
                       + "ungespeicherten Aenderungen." : ""),
                "Vereinheitlichen", "Abbrechen"))
        {
            return;
        }

        // Welches Prefab mit welchem Bild. Die Slime-Varianten haengen nicht
        // einzeln im Katalog, sondern nur ueber EnemyId.Slime - sie werden ueber
        // ihren Ordner mitgenommen und nach ihrem eigenen Bild gerechnet.
        var jobs = new Dictionary<string, string>();

        foreach (Draft d in drafts)
        {
            if (KeepsOwnScale(d) || !PrefabExists(d)) continue;

            jobs[d.prefab] = d.sheet;

            if (d.id == EnemyId.Slime)
            {
                string folder = Path.GetDirectoryName(d.prefab).Replace('\\', '/');
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!jobs.ContainsKey(path)) jobs[path] = SheetFromRenderer(path);
                }
            }
        }

        // Erst ALLE alten Pixeldichten merken, dann umstellen: zwei Gegner
        // koennen sich ein Bild teilen (Mini-Milch und Saure Milch).
        var oldPpu = new Dictionary<string, float>();
        foreach (string sheet in jobs.Values)
        {
            if (!string.IsNullOrEmpty(sheet) && !oldPpu.ContainsKey(sheet))
            {
                oldPpu[sheet] = PixelsPerUnitOf(sheet);
            }
        }

        var report = new StringBuilder();
        int sheetsChanged = 0;

        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (string sheet in oldPpu.Keys)
            {
                if (SetPixelsPerUnit(sheet)) sheetsChanged++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        int prefabsChanged = 0;
        int i = 0;

        try
        {
            foreach (KeyValuePair<string, string> job in jobs)
            {
                EditorUtility.DisplayProgressBar("Pixelgroesse", job.Key, (float)i++ / jobs.Count);

                float before = !string.IsNullOrEmpty(job.Value) && oldPpu.TryGetValue(job.Value, out float p) && p > 0f
                    ? p
                    : EnemyCatalog.PixelsPerUnit;

                string line = RescalePrefab(job.Key, before / EnemyCatalog.PixelsPerUnit);
                if (line != null)
                {
                    report.AppendLine(line);
                    prefabsChanged++;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        foreach (Draft d in drafts)
        {
            if (KeepsOwnScale(d)) continue;

            // Ein von Hand eingetragener Trefferkreis ist in Einheiten gemessen
            // und muss mitwachsen wie der am Prefab.
            if (d.colliderRadius > 0f && !string.IsNullOrEmpty(d.sheet)
                && oldPpu.TryGetValue(d.sheet, out float p) && p > 0f)
            {
                float k = p / EnemyCatalog.PixelsPerUnit;
                d.colliderRadius *= k;
                d.colliderOffset *= k;
            }

            d.scale = 1f;
        }

        SaveCatalog();
        AssetDatabase.SaveAssets();
        framesFor = "";

        Report(sheetsChanged + " Bild(er) auf " + EnemyCatalog.PixelsPerUnit + " Pixel pro Einheit gestellt, "
             + prefabsChanged + " Prefab(s) angepasst."
             + (report.Length > 0 ? "\n\n" + report : "")
             + "\n\nDanach die Test-Szene neu bauen: Tools -> Szenen -> Test-Szene neu bauen.");
    }

    private static string SheetFromRenderer(string prefabPath)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        SpriteRenderer renderer = asset != null ? asset.GetComponentInChildren<SpriteRenderer>(true) : null;
        return renderer != null && renderer.sprite != null
            ? AssetDatabase.GetAssetPath(renderer.sprite.texture)
            : "";
    }

    /// <summary>
    /// Setzt die Wurzel auf Skalierung 1 und rechnet alles in Einheiten mit
    /// <paramref name="k"/> (alte / neue Pixeldichte) um. null = nichts zu tun.
    /// </summary>
    private static string RescalePrefab(string path, float k)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Vector3 oldScale = root.transform.localScale;
            bool scaleOk = oldScale == Vector3.one;
            if (scaleOk && Mathf.Approximately(k, 1f)) return null;

            root.transform.localScale = Vector3.one;

            if (!Mathf.Approximately(k, 1f))
            {
                foreach (Collider2D c in root.GetComponents<Collider2D>())
                {
                    c.offset *= k;
                    if (c is CircleCollider2D circle) circle.radius *= k;
                    else if (c is BoxCollider2D box) box.size *= k;
                    else if (c is CapsuleCollider2D capsule) capsule.size *= k;
                }

                foreach (Transform child in root.transform)
                {
                    child.localPosition = new Vector3(child.localPosition.x * k,
                                                      child.localPosition.y * k,
                                                      child.localPosition.z);
                    child.localScale = new Vector3(child.localScale.x * k,
                                                   child.localScale.y * k,
                                                   child.localScale.z);
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);

            return string.Format(CultureInfo.InvariantCulture,
                "{0}: Skalierung {1:0.##} -> 1, Pixel x{2:0.##}",
                Path.GetFileNameWithoutExtension(path), oldScale.x, k);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private void DrawAudit()
    {
        EditorGUILayout.HelpBox(
            "Gegner sind so gross, wie sie gezeichnet sind: alle Bilder auf "
          + EnemyCatalog.PixelsPerUnit + " Pixel pro Einheit, alle Prefabs auf "
          + "Skalierung 1 (ausser Bossen). Dieser Knopf bringt Vorhandenes auf den Stand.",
            MessageType.None);

        if (GUILayout.Button("Pixelgroesse vereinheitlichen", GUILayout.Height(26f)))
        {
            UnifyPixelSize();
            GUIUtility.ExitGUI();
        }

        EditorGUILayout.Space(8f);

        EditorGUILayout.HelpBox(
            "Prueft alle Prefabs unter " + EnemyPrefabRoot + " auf die Fehler, die im "
          + "Spiel keine Fehlermeldung geben: fehlender Tag (keine Waffe trifft), "
          + "fehlender Collider, fehlende Id, fehlendes Prefab zu einem Katalog-Eintrag.",
            MessageType.Info);

        if (!GUILayout.Button("Jetzt pruefen", GUILayout.Height(26f))) return;

        var problems = new List<string>();

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { EnemyPrefabRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) continue;

            foreach (Enemy enemy in asset.GetComponentsInChildren<Enemy>(true))
            {
                string who = path + " -> " + enemy.gameObject.name;

                if (!enemy.CompareTag("Enemy"))
                {
                    problems.Add(who + ": Tag ist \"" + enemy.tag
                               + "\" statt \"Enemy\" - keine Waffe trifft ihn.");
                }

                if (enemy.GetComponent<Collider2D>() == null)
                {
                    problems.Add(who + ": kein Collider2D - er kann nicht getroffen werden.");
                }

                if (enemy.GetComponent<Rigidbody2D>() == null)
                {
                    problems.Add(who + ": kein Rigidbody2D - er bewegt sich nicht.");
                }

                if (enemy.Id != EnemyId.None && !EnemyCatalog.Has(enemy.Id))
                {
                    problems.Add(who + ": Id " + enemy.Id + " steht in keinem Katalog-Eintrag.");
                }
            }
        }

        foreach (EnemyDef def in EnemyCatalog.All)
        {
            if (string.IsNullOrEmpty(def.Prefab)) continue;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(def.Prefab) == null)
            {
                problems.Add(def.Name + ": Katalog zeigt auf " + def.Prefab
                           + ", dort liegt aber nichts.");
            }
        }

        // Mixels: jede Abweichung von "so gross wie gezeichnet".
        foreach (Draft d in drafts)
        {
            if (KeepsOwnScale(d)) continue;

            float ppu = string.IsNullOrEmpty(d.sheet) ? 0f : PixelsPerUnitOf(d.sheet);
            if (ppu > 0f && !Mathf.Approximately(ppu, EnemyCatalog.PixelsPerUnit))
            {
                problems.Add(d.name + ": Bild steht auf " + ppu + " statt "
                           + EnemyCatalog.PixelsPerUnit + " Pixel pro Einheit (Mixels).");
            }

            GameObject asset = PrefabExists(d) ? AssetDatabase.LoadAssetAtPath<GameObject>(d.prefab) : null;
            if (asset != null && asset.transform.localScale != Vector3.one)
            {
                problems.Add(d.name + ": Prefab ist skaliert (" + asset.transform.localScale
                           + ") statt 1 (Mixels).");
            }
        }

        if (problems.Count == 0)
        {
            Debug.Log("[Gegner-Werkstatt] Pruefung sauber.");
            EditorUtility.DisplayDialog("Pruefung", "Nichts gefunden - alles sauber.", "Ok");
            return;
        }

        foreach (string p in problems) Debug.LogWarning("[Gegner-Werkstatt] " + p);

        EditorUtility.DisplayDialog("Pruefung",
            problems.Count + " Auffaelligkeit(en) - Einzelheiten stehen in der Konsole.", "Ok");
    }

    // --------------------------------------------------------- Katalog schreiben

    /// <summary>
    /// Schreibt den Datenblock in EnemyCatalog.cs neu. Alles ausserhalb der
    /// beiden Marker bleibt unangetastet - was jemand von Hand daneben
    /// geschrieben hat, ueberlebt also jedes Speichern.
    ///
    /// Ausnahmen, beide nur auf Knopfdruck: <paramref name="extra"/> ist ein
    /// neu angelegter Gegner - sein Name kommt ans Ende des Enums
    /// <see cref="EnemyId"/>. <paramref name="renameFrom"/>/<paramref name="renameTo"/>
    /// benennt einen Enum-Namen um, samt allen EnemyId.xxx in der Datei.
    /// </summary>
    private bool SaveCatalog(Draft extra = null, string renameFrom = null, string renameTo = null)
    {
        string full = Path.GetFullPath(CatalogPath);
        if (!File.Exists(full))
        {
            Report("EnemyCatalog.cs nicht gefunden unter " + CatalogPath);
            return false;
        }

        string[] lines = File.ReadAllLines(full);

        if (extra != null && !EnumHas(extra.code))
        {
            lines = AddEnumMember(lines, extra.code);
            if (lines == null)
            {
                Report("Das Enum EnemyId in EnemyCatalog.cs laesst sich nicht finden - "
                     + "der neue Gegner wurde nicht angelegt.");
                return false;
            }
        }

        if (renameFrom != null)
        {
            lines = RenameEnumMember(lines, renameFrom, renameTo);
            if (lines == null)
            {
                Report("EnemyId." + renameFrom + " steht nicht eindeutig im Enum - "
                     + "es wurde nichts umbenannt.");
                return false;
            }
        }

        int start = System.Array.FindIndex(lines, l => l.Contains("WERKSTATT-ANFANG"));
        int end = System.Array.FindIndex(lines, l => l.Contains("WERKSTATT-ENDE"));

        if (start < 0 || end < 0 || end <= start)
        {
            Report("Die Marker WERKSTATT-ANFANG / WERKSTATT-ENDE fehlen in EnemyCatalog.cs. "
               + "Ohne sie weiss das Tool nicht, welchen Teil es ersetzen darf - es "
               + "schreibt lieber gar nichts.");
            return false;
        }

        var sb = new StringBuilder();

        for (int i = 0; i <= start; i++) sb.AppendLine(lines[i]);

        sb.AppendLine("    // ================================================================");
        sb.AppendLine("    private static void Build()");
        sb.AppendLine("    {");

        foreach (Draft d in drafts.Where(x => !x.archived))
        {
            AppendEntry(sb, d);
        }

        if (extra != null) AppendEntry(sb, extra);

        // Das Archiv steht mit im Code - sonst waere es geloescht und nicht
        // weggelegt. Es kommt nur ans Ende, damit es beim Lesen nicht stoert.
        if (drafts.Any(x => x.archived))
        {
            sb.AppendLine();
            sb.AppendLine("        // ---------------------------------------------------------- Archiv");
            sb.AppendLine("        //");
            sb.AppendLine("        // Weggelegt, nicht geloescht. In der Werkstatt unter \"Archiv\"");
            sb.AppendLine("        // zurueckholen.");

            foreach (Draft d in drafts.Where(x => x.archived))
            {
                AppendEntry(sb, d);
            }
        }

        sb.AppendLine("    }");
        sb.AppendLine("    // ================================================================");

        for (int i = end; i < lines.Length; i++) sb.AppendLine(lines[i]);

        string text = sb.ToString();

        // Verweise ausserhalb des Datenblocks (Kommentare, <see cref>, Hand-Code).
        if (renameFrom != null)
        {
            text = Regex.Replace(text, @"\bEnemyId\." + Regex.Escape(renameFrom) + @"\b",
                                 "EnemyId." + renameTo);
        }

        File.WriteAllText(full, text, new UTF8Encoding(false));
        AssetDatabase.ImportAsset(CatalogPath);

        dirty = false;

        Debug.Log("[Gegner-Werkstatt] " + (drafts.Count + (extra != null ? 1 : 0))
                  + " Eintraege nach " + CatalogPath + " geschrieben.");
        return true;
    }

    /// <summary>Anfang und Ende (Zeile mit "}") des Enums EnemyId, oder -1.</summary>
    private static void FindEnum(string[] lines, out int open, out int close)
    {
        open = System.Array.FindIndex(lines, l => Regex.IsMatch(l, @"^\s*public\s+enum\s+EnemyId\b"));
        close = -1;
        if (open < 0) return;

        for (int i = open + 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "}")
            {
                close = i;
                return;
            }
        }
    }

    private static string[] AddEnumMember(string[] lines, string id)
    {
        FindEnum(lines, out int open, out int close);
        if (open < 0 || close < 0) return null;

        var list = new List<string>(lines);

        // Der letzte Eintrag braucht ein Komma, sonst ist der neue ein Syntaxfehler.
        for (int i = close - 1; i > open; i--)
        {
            string code = Regex.Replace(list[i], @"//.*$", "").Trim();
            if (code.Length == 0 || code == "{") continue;
            if (!code.EndsWith(",")) list[i] = Regex.Replace(list[i], @"^(\s*[^/\s][^/]*?)(\s*)(//.*)?$", "$1,$2$3");
            break;
        }

        list.Insert(close, "    " + id + ",");
        return list.ToArray();
    }

    private static string[] RenameEnumMember(string[] lines, string from, string to)
    {
        FindEnum(lines, out int open, out int close);
        if (open < 0 || close < 0) return null;

        var member = new Regex(@"^(\s*)" + Regex.Escape(from) + @"(\s*(=[^,/]*)?,?\s*(//.*)?)$");

        int hit = -1;
        for (int i = open + 1; i < close; i++)
        {
            if (!member.IsMatch(lines[i])) continue;
            if (hit >= 0) return null;
            hit = i;
        }

        if (hit < 0) return null;

        string[] result = (string[])lines.Clone();
        result[hit] = member.Replace(lines[hit], "${1}" + to + "$2");
        return result;
    }

    private static void AppendEntry(StringBuilder sb, Draft d)
    {
        sb.AppendLine();
        sb.AppendLine("        Def(EnemyId." + d.code + ", \"" + Escape(d.name) + "\",");
        sb.AppendLine("            health: " + F(d.health)
                    + ", damage: " + F(d.damage)
                    + ", speed: " + F(d.speed)
                    + ", exp: " + d.exp.ToString(CultureInfo.InvariantCulture)
                    + ", pushTime: " + F(d.pushTime) + ",");
        sb.AppendLine("            role: EnemyRole." + d.role
                    + ", facing: EnemyFacing." + d.facing + ",");
        sb.AppendLine("            sheet: \"" + Escape(d.sheet) + "\", fps: " + F(d.fps) + ",");
        sb.AppendLine("            colliderRadius: " + F(d.colliderRadius)
                    + ", colliderOffset: new Vector2(" + F(d.colliderOffset.x)
                    + ", " + F(d.colliderOffset.y) + "), scale: " + F(d.scale) + ",");
        sb.AppendLine("            prefab: \"" + Escape(d.prefab) + "\""
                    + (d.archived ? ", archived: true" : "") + ");");
    }

    /// <summary>Zahl als C#-Literal - immer mit Punkt, egal welche Systemsprache.</summary>
    private static string F(float value)
    {
        return value.ToString("0.#####", CultureInfo.InvariantCulture) + "f";
    }

    private static string Escape(string value)
    {
        return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    // ------------------------------------------------------- In die Szene

    /// <summary>
    /// Traegt die gebauten Prefabs in den SpawnCatalog in GameCore ein. Ohne
    /// das kennt der Director die neuen Gegner nicht und ueberspringt sie
    /// stillschweigend.
    /// </summary>
    private void RegisterAllInScene()
    {
        // Die Rueckfrage "erst speichern?" gibt es nur mit Editor. Im
        // Batch-Mode bekommt sie niemand zu sehen, sie liefert "nein" zurueck,
        // und das Eintragen wuerde stumm uebersprungen - dort ist ohnehin
        // keine Szene offen, an der jemand haengt.
        if (!Application.isBatchMode
            && !UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        UnityEngine.SceneManagement.Scene scene =
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                CoreScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

        SpawnCatalog catalog = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            catalog = root.GetComponentInChildren<SpawnCatalog>(true);
            if (catalog != null) break;
        }

        if (catalog == null)
        {
            Report("In " + CoreScenePath + " steckt kein SpawnCatalog - "
                 + "der gehoert an den Spawn Director.");
            return;
        }

        int count = 0;
        foreach (Draft d in drafts)
        {
            if (string.IsNullOrEmpty(d.prefab)) continue;

            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(d.prefab);
            if (asset == null) continue;

            catalog.EditorSet(d.id, asset);
            count++;
        }

        EditorUtility.SetDirty(catalog);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, CoreScenePath);

        Report(count + " Prefab(s) im SpawnCatalog eingetragen.\n\n"
             + "Danach die Test-Szene neu bauen: Tools -> Szenen -> Test-Szene neu bauen.");
    }

    // ------------------------------------------------------------- Kleinkram

    /// <summary>
    /// Meldung an den Menschen. Im Batch-Mode gibt es kein Fenster, in das ein
    /// Dialog passt - dort landet dieselbe Nachricht in der Konsole, damit ein
    /// Durchlauf ohne Editor nicht stumm bleibt.
    /// </summary>
    private static void Report(string message)
    {
        if (Application.isBatchMode) Debug.Log("[Gegner-Werkstatt] " + message);
        else EditorUtility.DisplayDialog("Gegner-Werkstatt", message, "Ok");
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T existing = go.GetComponent<T>();
        return existing != null ? existing : go.AddComponent<T>();
    }

    private static int EnemyLayer()
    {
        foreach (string name in LayerCandidates)
        {
            int layer = LayerMask.NameToLayer(name);
            if (layer >= 0) return layer;
        }
        return 0;
    }

    private static Material TemplateMaterial()
    {
        GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePrefab);
        if (template == null) return null;

        SpriteRenderer renderer = template.GetComponent<SpriteRenderer>();
        return renderer != null ? renderer.sharedMaterial : null;
    }

    private static GameObject DefaultDestroyEffect()
    {
        GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePrefab);
        if (template == null) return null;

        Enemy enemy = template.GetComponent<Enemy>();
        return enemy != null ? enemy.EditorDestroyEffect : null;
    }

    private static string SafeName(string name)
    {
        var sb = new StringBuilder();
        foreach (char c in name ?? "")
        {
            sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
        }

        string result = sb.ToString().Trim('_');
        return string.IsNullOrEmpty(result) ? "Gegner" : result;
    }

    private static void EnsureFolder(string folder)
    {
        if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;

        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        string leaf = Path.GetFileName(folder);

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
