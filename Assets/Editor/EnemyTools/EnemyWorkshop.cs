using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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
        public string name;
        public float health, damage, speed;
        public int exp;
        public float threat, pushTime;
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
                name = def.Name,
                health = def.Health,
                damage = def.Damage,
                speed = def.Speed,
                exp = def.Exp,
                threat = def.Threat,
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
    }

    private List<Draft> drafts = new List<Draft>();
    private int selected;
    private int tab;
    private bool dirty;

    private Vector2 listScroll;
    private Vector2 bodyScroll;
    private Vector2 legacyScroll;

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

    [MenuItem("Tools/Gegner/Werkstatt", false, 0)]
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
            if (string.IsNullOrEmpty(d.sheet) || PrefabExists(d)) continue;

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
        selected = Mathf.Clamp(selected, 0, Mathf.Max(0, drafts.Count - 1));
        dirty = false;
        framesFor = "";
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
            Draft d = drafts[i];

            bool hasPrefab = !string.IsNullOrEmpty(d.prefab)
                             && AssetDatabase.LoadAssetAtPath<GameObject>(d.prefab) != null;
            string mark = hasPrefab ? "" : (string.IsNullOrEmpty(d.sheet) ? "  (kein Bild)" : "  (kein Prefab)");

            bool on = i == selected;
            bool now = GUILayout.Toggle(on, d.name + mark, "Button");
            if (now && !on)
            {
                selected = i;
                framesFor = "";
                GUI.FocusControl(null);
            }
        }

        EditorGUILayout.EndScrollView();

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

    private void DrawDetail(Draft d)
    {
        EditorGUILayout.BeginVertical();
        bodyScroll = EditorGUILayout.BeginScrollView(bodyScroll);

        EditorGUI.BeginChangeCheck();

        // ------------------------------------------------------------ Kopf
        EditorGUILayout.LabelField(d.name, EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Id im Code", d.id.ToString());
        d.name = EditorGUILayout.TextField(new GUIContent(
            "Anzeigename", "Nur hier im Fenster sichtbar, nicht im Spiel."), d.name);

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

        d.threat = EditorGUILayout.FloatField(new GUIContent(
            "Gewicht", "Was er im Druck-Budget des Spawn-Directors zaehlt. "
                     + "Marshmello = 1, Fetti = 8."), d.threat);

        d.role = (EnemyRole)EditorGUILayout.EnumPopup(new GUIContent(
            "Rolle", "Entscheidet ueber Truhe, Seelen und welche Erfolge zaehlen."), d.role);

        DrawRoleHint(d.role);
        DrawBalanceHint(d);

        EditorGUILayout.Space(6f);

        // ---------------------------------------------------------- Koerper
        EditorGUILayout.LabelField("Koerper", EditorStyles.boldLabel);
        d.scale = EditorGUILayout.FloatField(new GUIContent(
            "Groesse", "Skalierung des Prefabs. 1 = so gross wie gezeichnet."), d.scale);

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
            case EnemyRole.Elite:
                text = "Wie Normal - zaehlt auf dieselben Erfolge, ist nur dicker.";
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
    /// Ein Gegner, der mehr wiegt als er aushaelt, macht das Feld leer; einer,
    /// der zu wenig wiegt, macht es zu. Deshalb hier ein grober Vergleich mit
    /// dem Marshmello, an dem die ganze Kurve haengt.
    /// </summary>
    private void DrawBalanceHint(Draft d)
    {
        if (d.role == EnemyRole.Blocker || d.threat <= 0f) return;

        EnemyDef basis = EnemyCatalog.Get(EnemyId.Marshmello);
        if (basis == null) return;

        float taken = d.health / Mathf.Max(1f, basis.Health);
        float weighed = d.threat / Mathf.Max(0.01f, basis.Threat);

        if (weighed <= 0f) return;

        float ratio = taken / weighed;
        if (ratio > 3f)
        {
            EditorGUILayout.HelpBox(
                string.Format(CultureInfo.InvariantCulture,
                    "Er haelt {0:0.#}x so viel aus wie ein Marshmello, wiegt aber nur "
                  + "{1:0.#}x so viel. Der Director legt dann zu wenig nach - das Feld "
                  + "wirkt leer. Gewicht eher Richtung {2:0.#}.",
                    taken, weighed, taken * basis.Threat / 3f),
                MessageType.Warning);
        }
        else if (ratio < 0.33f)
        {
            EditorGUILayout.HelpBox(
                string.Format(CultureInfo.InvariantCulture,
                    "Er wiegt {0:0.#}x so viel wie ein Marshmello, haelt aber nur "
                  + "{1:0.#}x so viel aus. Der Director haelt dann zu wenig auf dem "
                  + "Feld - es wirkt leer, obwohl der Druck stimmt.",
                    weighed, taken),
                MessageType.Warning);
        }
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
                if (string.IsNullOrEmpty(d.sheet)) continue;

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
    private void DrawAudit()
    {
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
    /// </summary>
    private void SaveCatalog()
    {
        string full = Path.GetFullPath(CatalogPath);
        if (!File.Exists(full))
        {
            Report("EnemyCatalog.cs nicht gefunden unter " + CatalogPath);
            return;
        }

        string[] lines = File.ReadAllLines(full);

        int start = System.Array.FindIndex(lines, l => l.Contains("WERKSTATT-ANFANG"));
        int end = System.Array.FindIndex(lines, l => l.Contains("WERKSTATT-ENDE"));

        if (start < 0 || end < 0 || end <= start)
        {
            Report("Die Marker WERKSTATT-ANFANG / WERKSTATT-ENDE fehlen in EnemyCatalog.cs. "
               + "Ohne sie weiss das Tool nicht, welchen Teil es ersetzen darf - es "
               + "schreibt lieber gar nichts.");
            return;
        }

        var sb = new StringBuilder();

        for (int i = 0; i <= start; i++) sb.AppendLine(lines[i]);

        sb.AppendLine("    // ================================================================");
        sb.AppendLine("    private static void Build()");
        sb.AppendLine("    {");

        foreach (Draft d in drafts)
        {
            AppendEntry(sb, d);
        }

        sb.AppendLine("    }");
        sb.AppendLine("    // ================================================================");

        for (int i = end; i < lines.Length; i++) sb.AppendLine(lines[i]);

        File.WriteAllText(full, sb.ToString(), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(CatalogPath);

        dirty = false;

        Debug.Log("[Gegner-Werkstatt] " + drafts.Count + " Eintraege nach " + CatalogPath
                  + " geschrieben.");
    }

    private static void AppendEntry(StringBuilder sb, Draft d)
    {
        sb.AppendLine();
        sb.AppendLine("        Def(EnemyId." + d.id + ", \"" + Escape(d.name) + "\",");
        sb.AppendLine("            health: " + F(d.health)
                    + ", damage: " + F(d.damage)
                    + ", speed: " + F(d.speed)
                    + ", exp: " + d.exp.ToString(CultureInfo.InvariantCulture)
                    + ", threat: " + F(d.threat)
                    + ", pushTime: " + F(d.pushTime) + ",");
        sb.AppendLine("            role: EnemyRole." + d.role
                    + ", facing: EnemyFacing." + d.facing + ",");
        sb.AppendLine("            sheet: \"" + Escape(d.sheet) + "\", fps: " + F(d.fps) + ",");
        sb.AppendLine("            colliderRadius: " + F(d.colliderRadius)
                    + ", colliderOffset: new Vector2(" + F(d.colliderOffset.x)
                    + ", " + F(d.colliderOffset.y) + "), scale: " + F(d.scale) + ",");
        sb.AppendLine("            prefab: \"" + Escape(d.prefab) + "\");");
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
            Report("In " + CoreScenePath + " steckt kein SpawnCatalog. Erst "
                 + "Tools -> Spawns -> Spawn-Director in GameCore einbauen.");
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
             + "Danach die Test-Szene neu bauen: Tools -> Test Scene.");
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
