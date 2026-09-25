using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// DER SKILLTREE-EDITOR - Tools -> Skilltree -> Editor.
///
/// Links die drei Bahnen einer Kategorie, rechts die Einstellungen des
/// angeklickten Knotens. So wird gebaut:
///
///   1. Oben den CHARAKTER waehlen. In der Liste stehen alle Charaktere aus
///      <see cref="Characters"/> - auch die, die noch keinen Baum haben. Einen
///      davon anklicken legt seinen Baum an (wahlweise als Kopie eines anderen).
///      Kommt spaeter ein Charakter dazu, steht er hier von selbst.
///   2. Darunter die Kategorie umschalten: Kampf, Geist, Wissen, Glueck.
///   3. Einen Knoten anklicken. Rechts steht dann, was er gibt: Werte wie
///      "+10 Leben" oder Schalter wie ein Waffen-Upgrade. Mehrere sind erlaubt.
///   4. Am gewaehlten Knoten erscheinen drei Plus-Knoepfe: geradeaus, nach oben
///      und nach unten. Der neue Knoten haengt automatisch an dem, von dem aus
///      man ihn angelegt hat - die Linie zeichnet sich von selbst. Ganz links
///      steht der Startknoten der Kategorie; dort faengt jede Bahn an.
///   5. Braucht ein Knoten ZWEI Vorgaenger (z.B. oben UND Mitte), auf
///      "Verbinden" klicken und dann den zusaetzlichen Vorgaenger anklicken.
///      Oder rechts in der Liste "Braucht vorher" die Haken setzen.
///   6. Weg damit: das kleine rote x an der Ecke des gewaehlten Knotens, die
///      Entf-Taste oder der Knopf unten im Inspektor. Was daran hing, rutscht
///      auf den Vorgaenger - es bleibt also nichts in der Luft haengen.
///
/// Knoten lassen sich mit der Maus verschieben; sie rasten auf Bahn und Spalte
/// ein. Die Ids aendern sich dabei NICHT - sie stehen im Spielstand.
///
/// EINE REGEL FUER DIESE DATEI: alles, was die Auswahl oder die Felder rechts
/// umbaut - Baum wechseln, Reiter wechseln, Knoten anklicken, anlegen, loeschen -
/// wird in <see cref="pending"/> gemerkt und erst zu Beginn des naechsten
/// OnGUI-Durchgangs ausgefuehrt.
/// Passiert es mitten im Zeichnen, meldet IMGUI eine andere Zahl von Feldern an,
/// als es dann zeichnet; die Felder rechts nehmen danach keine Eingaben mehr an.
///
/// Gespeichert wird direkt im Asset unter Assets/Resources/SkillTrees/, mit
/// Undo. Einen ganzen Baum am Stueck schreiben oder diktieren lassen geht ueber
/// <see cref="SkillTreeTextIO"/>.
/// </summary>
public class SkillTreeEditorWindow : EditorWindow
{
    // ------------------------------------------------------------- Masse

    const float InspectorWidth = 330f;
    const float GridX          = 130f;   // Abstand zweier Spalten bei Zoom 1
    const float LaneGap        = 95f;    // Abstand der Bahnen bei Zoom 1
    const float NodeSize       = 48f;    // Kantenlaenge bei Zoom 1
    const float Margin         = 40f;

    [MenuItem("Tools/Skilltree/Editor", false, 200)]
    public static void Open()
    {
        var window = GetWindow<SkillTreeEditorWindow>("Skilltree");
        window.minSize = new Vector2(820f, 460f);
        window.Show();
    }

    SkillTreeAsset asset;
    SkillCategory category = SkillCategory.Kampf;
    string selectedId;

    /// <summary>Gesetzt, solange auf den Klick fuer eine zusaetzliche Linie gewartet wird.</summary>
    string linkSourceId;

    Vector2 canvasScroll;
    Vector2 inspectorScroll;
    float zoom = 1f;

    string draggingId;
    Vector2 dragOffset;
    bool dragMoved;

    List<SkillTreeAsset> allTrees = new List<SkillTreeAsset>();

    /// <summary>
    /// Was das Fenster umbaut - Baum wechseln, Reiter wechseln, Knoten auswaehlen,
    /// anlegen, loeschen - darf nicht mitten im Zeichnen passieren: danach stimmt
    /// das GUI-Layout nicht mehr mit dem ueberein, was Unity im Layout-Durchgang
    /// angemeldet bekommen hat, und die Felder rechts reagieren nicht mehr.
    ///
    /// Der Knopf merkt die Aenderung darum nur vor; ausgefuehrt wird sie ganz am
    /// Anfang des naechsten OnGUI-Durchgangs.
    /// </summary>
    System.Action pending;

    /// <summary>
    /// Die Flaeche unter der Kopfzeile, gemessen im letzten Durchgang, in dem sie
    /// wirklich feststand. Warum das noetig ist, steht in OnGUI.
    /// </summary>
    Rect bodyRect;

    /// <summary>
    /// Solange noch nichts gemessen wurde (allererster Durchgang): alles unter der
    /// Kopfzeile. Die Hoehe ist geschaetzt - Werkzeugleiste plus Reiter - und gilt
    /// nur fuer diesen einen Durchgang.
    /// </summary>
    Rect FallbackBody()
    {
        const float header = 46f;
        return new Rect(0f, header, position.width, Mathf.Max(0f, position.height - header));
    }

    // ==================================================================

    void OnEnable()
    {
        RefreshTreeList();

        if (asset == null && allTrees.Count > 0) SetAsset(allTrees[0]);
    }

    void OnFocus() => RefreshTreeList();

    void RefreshTreeList() => allTrees = SkillTreeTextIO.LoadAll();

    void SetAsset(SkillTreeAsset next)
    {
        asset = next;
        selectedId = null;
        linkSourceId = null;
        canvasScroll = Vector2.zero;

        if (asset == null) return;

        // Beim Oeffnen geraderuecken: alle vier Kategorien da, und jede mit ihrem
        // Startknoten ganz links. Aeltere Baeume ruecken dabei eine Spalte nach
        // rechts und haengen sich an den Start - siehe SkillTreeAsset.
        bool fixedUp = asset.EnsureCategories();
        if (asset.EnsureStartNodes()) fixedUp = true;

        if (fixedUp) EditorUtility.SetDirty(asset);
    }

    void OnGUI()
    {
        // Gemerktes ZUERST: bevor irgendein Feld angemeldet ist, damit Layout- und
        // Zeichendurchgang dasselbe Fenster sehen. Am Ende von OnGUI waere es nicht
        // sicher - wirft das Zeichnen dazwischen, bliebe die Aenderung liegen, und
        // der Loeschknopf taete scheinbar nichts.
        if (pending != null)
        {
            System.Action action = pending;
            pending = null;
            action();
        }

        DrawToolbar();

        if (asset == null)
        {
            EditorGUILayout.Space(20f);
            EditorGUILayout.HelpBox(
                "Noch kein Skilltree ausgewaehlt.\n\n" +
                "Oben \"Neu...\" anklicken, um einen anzulegen - wahlweise als Kopie eines " +
                "vorhandenen Baums. Die Assets landen unter " + SkillTreeTextIO.AssetFolder + ".",
                MessageType.Info);
            return;
        }

        DrawCategoryTabs();

        // GetRect liefert im LAYOUT-Durchgang nur einen Platzhalter (1x1) - die
        // echte Groesse steht erst danach fest. Die Zeichenflaeche stoert das nicht,
        // sie zeichnet mit festen Rechtecken. Der Inspektor rechts baut aber mit
        // GUILayout, und GUILayout merkt sich die Positionen aus genau diesem
        // Layout-Durchgang: mit dem Platzhalter landen alle Felder in einem Kasten
        // ohne Hoehe und werden spaeter weggeschnitten - die Spalte bleibt leer.
        // Darum die Groesse aus dem letzten Durchgang merken und die benutzen.
        Rect measured = GUILayoutUtility.GetRect(0f, 0f, GUILayout.ExpandWidth(true),
                                                 GUILayout.ExpandHeight(true));

        if (Event.current.type != EventType.Layout && measured.height > 1f) bodyRect = measured;

        Rect body = bodyRect.height > 1f ? bodyRect : FallbackBody();

        var canvasRect = new Rect(body.x, body.y, body.width - InspectorWidth, body.height);
        var inspectorRect = new Rect(canvasRect.xMax, body.y, InspectorWidth, body.height);

        DrawCanvas(canvasRect);
        DrawInspector(inspectorRect);

        // Ist in diesem Durchgang etwas dazugekommen, laeuft es oben im naechsten.
        if (pending != null) Repaint();
    }

    // ==================================================================
    //  Kopfzeile
    // ==================================================================

    /// <summary>
    /// Ein Eintrag in der Auswahl oben. Die Liste faengt bei den CHARAKTEREN an -
    /// auch bei denen, die noch keinen Baum haben. Kommt spaeter einer dazu
    /// (ein Eintrag mehr in <see cref="Characters"/>), steht er hier von selbst,
    /// und ein Klick legt seinen Baum an.
    /// </summary>
    struct Pick
    {
        public string Label;
        public SkillTreeAsset Tree;   // null = fuer diesen Charakter gibt es noch keinen
        public int Character;         // -1 = Baum, der keinem Charakter gehoert
    }

    List<Pick> BuildPicks()
    {
        var picks = new List<Pick>();
        var used = new HashSet<SkillTreeAsset>();

        for (int i = 0; i < Characters.Count; i++)
        {
            SkillTreeAsset tree = TreeForCharacter(i);
            if (tree != null) used.Add(tree);

            picks.Add(new Pick
            {
                Label = tree != null
                    ? $"Charakter {i} - {Characters.NameOf(i)}   ({tree.treeId})"
                    : $"Charakter {i} - {Characters.NameOf(i)}   (noch kein Baum - anlegen)",
                Tree      = tree,
                Character = i,
            });
        }

        // Alles, was keinem Charakter zugeordnet ist: Vorlagen, Reste, oder ein
        // Baum mit einer Nummer, die es im Katalog (noch) nicht gibt.
        foreach (SkillTreeAsset t in allTrees)
        {
            if (t == null || used.Contains(t)) continue;

            string who = t.characterIndex >= 0 ? $"Charakter {t.characterIndex}?" : "ohne Charakter";

            picks.Add(new Pick
            {
                Label     = $"{t.displayName}   ({t.treeId}, {who})",
                Tree      = t,
                Character = t.characterIndex,
            });
        }

        return picks;
    }

    /// <summary>Der Baum dieses Charakters - dieselbe Zuordnung wie im Spiel.</summary>
    SkillTreeAsset TreeForCharacter(int index)
    {
        foreach (SkillTreeAsset t in allTrees)
        {
            if (t != null && t.characterIndex == index) return t;
        }
        return null;
    }

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        List<Pick> picks = BuildPicks();

        var names = new string[picks.Count];
        int current = -1;

        for (int i = 0; i < picks.Count; i++)
        {
            names[i] = picks[i].Label;
            if (picks[i].Tree != null && picks[i].Tree == asset) current = i;
        }

        int picked = EditorGUILayout.Popup(current, names, EditorStyles.toolbarDropDown,
                                           GUILayout.Width(340f));

        // Wie bei allem anderen hier: erst im naechsten Durchgang ausfuehren, sonst
        // zeichnet der Rest des Fensters schon den naechsten Baum.
        if (picked != current && picked >= 0 && picked < picks.Count)
        {
            Pick chosen = picks[picked];

            pending = chosen.Tree != null
                ? (System.Action)(() => SetAsset(chosen.Tree))
                : () => SkillTreeCreateWindow.Show(this, chosen.Character);
        }

        if (GUILayout.Button("Neu...", EditorStyles.toolbarButton, GUILayout.Width(60f)))
            SkillTreeCreateWindow.Show(this);

        using (new EditorGUI.DisabledScope(asset == null))
        {
            if (GUILayout.Button("Im Projekt zeigen", EditorStyles.toolbarButton, GUILayout.Width(120f)))
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }

            if (GUILayout.Button("Als Text speichern", EditorStyles.toolbarButton, GUILayout.Width(120f)))
            {
                Selection.activeObject = asset;
                SkillTreeTextIO.ExportMenu();
            }

            if (GUILayout.Button("Pruefen", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                SkillTools.Validate();
        }

        GUILayout.FlexibleSpace();

        GUILayout.Label("Zoom", GUILayout.Width(36f));
        zoom = GUILayout.HorizontalSlider(zoom, 0.55f, 1.5f, GUILayout.Width(90f));

        EditorGUILayout.EndHorizontal();

        if (linkSourceId != null)
        {
            EditorGUILayout.HelpBox(
                $"Verbinden: Klicke jetzt den Knoten an, der \"{NameOf(linkSourceId)}\" als " +
                "zusaetzliche Voraussetzung bekommen soll. ESC bricht ab.",
                MessageType.Info);
        }
    }

    void DrawCategoryTabs()
    {
        var values = (SkillCategory[])System.Enum.GetValues(typeof(SkillCategory));
        var labels = new string[values.Length];

        for (int i = 0; i < values.Length; i++)
        {
            int count = asset.NodesOf(values[i]).Count;
            labels[i] = $"{asset.CategoryOf(values[i]).displayName} ({count})";
        }

        int index = System.Array.IndexOf(values, category);
        int next = GUILayout.Toolbar(index, labels, GUILayout.Height(24f));

        if (next == index) return;

        // Auch der Reiterwechsel erst im naechsten Durchgang - er tauscht die Felder
        // rechts aus, und mitten im Zeichnen verzaehlt sich IMGUI daran.
        SkillCategory chosen = values[next];

        pending = () =>
        {
            category = chosen;
            selectedId = null;
            linkSourceId = null;
            canvasScroll = Vector2.zero;
        };
    }

    // ==================================================================
    //  Zeichenflaeche
    // ==================================================================

    float StepWidth => GridX * zoom;
    float LaneHeight => LaneGap * zoom;
    float NodeWidth => NodeSize * zoom;

    void DrawCanvas(Rect rect)
    {
        List<SkillNodeData> nodes = asset.NodesOf(category);

        int maxStep = 0;
        foreach (SkillNodeData n in nodes) maxStep = Mathf.Max(maxStep, n.step);

        // Zwei Spalten Luft rechts, damit die Plus-Knoepfe hinter dem letzten
        // Knoten noch Platz haben.
        float contentWidth = Margin * 2f + (maxStep + 2) * StepWidth;
        float contentHeight = LaneHeight * 2f + NodeWidth * 2f + Margin * 2f;

        var content = new Rect(0f, 0f, Mathf.Max(contentWidth, rect.width - 20f),
                               Mathf.Max(contentHeight, rect.height - 20f));

        EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.18f));

        canvasScroll = GUI.BeginScrollView(rect, canvasScroll, content);

        float centerY = content.height * 0.5f;

        DrawLaneBackgrounds(content, centerY);
        DrawConnections(nodes, centerY);

        foreach (SkillNodeData n in nodes) DrawNode(n, centerY);

        DrawAddButtons(nodes, centerY);
        HandleCanvasInput(nodes, centerY);

        GUI.EndScrollView();
    }

    void DrawLaneBackgrounds(Rect content, float centerY)
    {
        if (Event.current.type != EventType.Repaint) return;

        var lanes = (SkillLane[])System.Enum.GetValues(typeof(SkillLane));

        foreach (SkillLane lane in lanes)
        {
            float y = centerY - SkillTreeLayout.LaneOffset(lane) * LaneHeight;

            // Ein ruhiger Streifen je Bahn - man sieht sofort, wo oben, Mitte und
            // unten liegen, auch wenn eine Bahn noch leer ist.
            EditorGUI.DrawRect(new Rect(0f, y - NodeWidth * 0.75f, content.width, NodeWidth * 1.5f),
                               new Color(1f, 1f, 1f, 0.025f));

            var label = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(1f, 1f, 1f, 0.35f) },
            };

            GUI.Label(new Rect(6f, y - 8f, 60f, 16f), lane.ToString().ToUpperInvariant(), label);
        }
    }

    Vector2 CenterOf(SkillNodeData n, float centerY)
    {
        return new Vector2(Margin + NodeWidth * 0.5f + n.step * StepWidth,
                           centerY - SkillTreeLayout.LaneOffset(n.lane) * LaneHeight);
    }

    Rect RectOf(SkillNodeData n, float centerY)
    {
        Vector2 c = CenterOf(n, centerY);
        return new Rect(c.x - NodeWidth * 0.5f, c.y - NodeWidth * 0.5f, NodeWidth, NodeWidth);
    }

    /// <summary>
    /// Die Linien - genau so gewinkelt wie im Spiel (siehe
    /// HubSkilltreeGraph.AddConnection), damit der Editor nicht etwas anderes
    /// zeigt als der Hub.
    /// </summary>
    void DrawConnections(List<SkillNodeData> nodes, float centerY)
    {
        if (Event.current.type != EventType.Repaint) return;

        foreach (SkillNodeData n in nodes)
        {
            foreach (string parentId in n.requires)
            {
                SkillNodeData parent = asset.Find(parentId);
                if (parent == null || parent.category != n.category) continue;

                Vector2 a = CenterOf(parent, centerY);
                Vector2 b = CenterOf(n, centerY);

                float half = NodeWidth * 0.5f;
                float x0 = Mathf.Min(a.x, b.x) + half;
                float x1 = Mathf.Max(a.x, b.x) - half;

                Handles.color = new Color(0.75f, 0.72f, 0.6f, 0.85f);

                if (x1 <= x0)
                {
                    Handles.DrawAAPolyLine(3f, new Vector3(a.x, a.y), new Vector3(b.x, b.y));
                    continue;
                }

                if (Mathf.Approximately(a.y, b.y))
                {
                    Handles.DrawAAPolyLine(3f, new Vector3(x0, a.y), new Vector3(x1, a.y));
                    continue;
                }

                float mid = (x0 + x1) * 0.5f;

                Handles.DrawAAPolyLine(3f,
                    new Vector3(x0, a.y), new Vector3(mid, a.y),
                    new Vector3(mid, b.y), new Vector3(x1, b.y));
            }
        }
    }

    void DrawNode(SkillNodeData n, float centerY)
    {
        Rect r = RectOf(n, centerY);
        bool isSelected = n.id == selectedId;
        bool isLinkSource = n.id == linkSourceId;

        Color color = asset.CategoryOf(category).color;
        Color fill = n.requires.Count == 0 ? color : Color.Lerp(color, Color.white, 0.25f);
        Color outline = isSelected ? Color.white
                      : isLinkSource ? new Color(1f, 0.85f, 0.3f)
                      : Color.Lerp(color, Color.black, 0.5f);

        SkillShapeGizmo.Draw(r, n.shape, fill, outline, isSelected || isLinkSource ? 3f : 2f);

        if (Event.current.type != EventType.Repaint) return;

        // Name unter dem Knoten, Preis darueber - beides klein, damit der Baum
        // auch bei vielen Knoten lesbar bleibt.
        var below = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.UpperCenter,
            wordWrap  = true,
            normal    = { textColor = Color.white },
        };

        GUI.Label(new Rect(r.x - StepWidth * 0.2f, r.yMax + 2f, r.width + StepWidth * 0.4f, 30f),
                  DisplayNameOf(n), below);

        var above = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.LowerCenter,
            normal    = { textColor = new Color(1f, 1f, 1f, 0.55f) },
        };

        GUI.Label(new Rect(r.x - 20f, r.y - 18f, r.width + 40f, 16f), n.price + " SP", above);
    }

    /// <summary>
    /// Die Plus-Knoepfe am gewaehlten Knoten: geradeaus, schraeg nach oben und
    /// schraeg nach unten. Und fuer jede noch leere Bahn einer am Startknoten.
    /// </summary>
    void DrawAddButtons(List<SkillNodeData> nodes, float centerY)
    {
        var lanes = (SkillLane[])System.Enum.GetValues(typeof(SkillLane));

        // Leere Bahnen bekommen einen Knopf gleich hinter dem Startknoten - der
        // neue Knoten haengt dann am Start, wie jeder erste Knoten einer Bahn.
        SkillNodeData start = asset.StartOf(category);

        foreach (SkillLane lane in lanes)
        {
            if (asset.NodesOf(category, lane).Count > 0) continue;

            float y = centerY - SkillTreeLayout.LaneOffset(lane) * LaneHeight;
            var r = new Rect(Margin + StepWidth, y - 14f, 28f, 28f);

            if (!GUI.Button(r, "+")) continue;

            SkillLane captured = lane;
            pending = () => AddNode(captured, 1, start);
        }

        SkillNodeData selected = asset.Find(selectedId);
        if (selected == null || selected.category != category) return;

        Vector2 c = CenterOf(selected, centerY);
        float x = c.x + StepWidth * 0.52f;

        DrawAddButton(new Rect(x, c.y - 14f, 28f, 28f), selected, selected.lane, "+");

        if (selected.lane != SkillLane.Oben)
        {
            SkillLane up = selected.lane == SkillLane.Unten ? SkillLane.Mitte : SkillLane.Oben;
            DrawAddButton(new Rect(x, c.y - LaneHeight * 0.55f - 14f, 28f, 28f), selected, up, "↗");
        }

        if (selected.lane != SkillLane.Unten)
        {
            SkillLane down = selected.lane == SkillLane.Oben ? SkillLane.Mitte : SkillLane.Unten;
            DrawAddButton(new Rect(x, c.y + LaneHeight * 0.55f - 14f, 28f, 28f), selected, down, "↘");
        }

        // Loeschen direkt am Knoten - der Knopf unten im Inspektor ist bei vielen
        // Belohnungen weit weg. Entf tut dasselbe.
        if (SkillTreeAsset.IsStartId(selected.id)) return;

        // Rechts oben an die Ecke: links liegen die Linie und die Plus-Knoepfe des
        // Vorgaengers, mittig darueber steht der Preis.
        var trash = new Rect(c.x + NodeWidth * 0.5f + 4f, c.y - NodeWidth * 0.5f - 18f, 22f, 18f);

        Color was = GUI.backgroundColor;
        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);

        if (GUI.Button(trash, new GUIContent("x", "Knoten loeschen (Entf)")))
        {
            SkillNodeData doomed = selected;
            pending = () => DeleteNode(doomed);
        }

        GUI.backgroundColor = was;
    }

    void DrawAddButton(Rect r, SkillNodeData from, SkillLane lane, string label)
    {
        if (!GUI.Button(r, label)) return;

        int step = NextFreeStep(lane, from.step + 1);
        pending = () => AddNode(lane, step, from);
    }

    /// <summary>Die naechste Spalte dieser Bahn, in der noch nichts steht.</summary>
    int NextFreeStep(SkillLane lane, int from)
    {
        List<SkillNodeData> inLane = asset.NodesOf(category, lane);

        for (int step = Mathf.Max(0, from); step < from + 100; step++)
        {
            bool taken = false;

            foreach (SkillNodeData n in inLane)
            {
                if (n.step == step) { taken = true; break; }
            }

            if (!taken) return step;
        }

        return from;
    }

    /// <summary>
    /// Maus und Tasten auf der Zeichenflaeche.
    ///
    /// WICHTIG: was die rechte Spalte umbaut - also die Auswahl - wird NICHT
    /// sofort gesetzt, sondern wie Anlegen und Loeschen in <see cref="pending"/>
    /// gemerkt. Sonst zeichnet die Inspektorseite im selben Durchgang andere
    /// Felder, als im Layout-Durchgang angemeldet waren; IMGUI verzaehlt sich
    /// dann und die Felder rechts nehmen keine Eingaben mehr an.
    /// </summary>
    void HandleCanvasInput(List<SkillNodeData> nodes, float centerY)
    {
        Event e = Event.current;

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape && linkSourceId != null)
        {
            pending = () => linkSourceId = null;
            e.Use();
            Repaint();
            return;
        }

        // Entf loescht den gewaehlten Knoten - dasselbe wie das x am Knoten und der
        // Knopf rechts. Nur wenn gerade kein Feld den Tastendruck braucht: der
        // Inspektor wird NACH der Zeichenflaeche gezeichnet, sonst frisst diese
        // Abfrage das Entf beim Tippen im Namensfeld.
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete &&
            GUIUtility.keyboardControl == 0)
        {
            SkillNodeData chosen = asset.Find(selectedId);

            if (chosen != null && chosen.category == category)
            {
                pending = () => DeleteNode(chosen);
                e.Use();
                Repaint();
                return;
            }
        }

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            SkillNodeData hit = NodeAt(nodes, e.mousePosition, centerY);

            if (hit != null && linkSourceId != null && hit.id != linkSourceId)
            {
                string source = linkSourceId;

                pending = () =>
                {
                    AddRequirement(hit, source);
                    linkSourceId = null;
                    selectedId = hit.id;
                };

                e.Use();
                Repaint();
                return;
            }

            pending = () =>
            {
                selectedId = hit != null ? hit.id : null;
                linkSourceId = null;
            };

            // Ziehen darf sofort losgehen: es verschiebt nur Knoten und aendert
            // nichts an den Feldern rechts. Der Startknoten bleibt, wo er ist.
            if (hit != null && !SkillTreeAsset.IsStartId(hit.id))
            {
                draggingId = hit.id;
                dragOffset = e.mousePosition - CenterOf(hit, centerY);
                dragMoved = false;
            }

            GUI.FocusControl(null);
            e.Use();
            Repaint();
            return;
        }

        if (e.type == EventType.MouseDrag && e.button == 0 && draggingId != null)
        {
            SkillNodeData node = asset.Find(draggingId);

            if (node != null)
            {
                Vector2 target = e.mousePosition - dragOffset;

                // Mindestens Spalte 1: Spalte 0 gehoert dem Startknoten.
                int step = Mathf.Max(1, Mathf.RoundToInt((target.x - Margin - NodeWidth * 0.5f) / StepWidth));
                SkillLane lane = LaneAt(target.y, centerY);

                if (step != node.step || lane != node.lane)
                {
                    // Auf einen belegten Platz laesst sich nicht ablegen - sonst
                    // liegen zwei Knoten uebereinander und man findet sie nicht mehr.
                    if (!Occupied(lane, step, node))
                    {
                        if (!dragMoved) Undo.RecordObject(asset, "Knoten verschieben");
                        dragMoved = true;

                        node.lane = lane;
                        node.step = step;
                        EditorUtility.SetDirty(asset);
                    }
                }
            }

            e.Use();
            Repaint();
            return;
        }

        if (e.type == EventType.MouseUp && e.button == 0 && draggingId != null)
        {
            draggingId = null;
            e.Use();
        }
    }

    bool Occupied(SkillLane lane, int step, SkillNodeData except)
    {
        foreach (SkillNodeData n in asset.NodesOf(category, lane))
        {
            if (n != except && n.step == step) return true;
        }
        return false;
    }

    SkillLane LaneAt(float y, float centerY)
    {
        if (y < centerY - LaneHeight * 0.5f) return SkillLane.Oben;
        if (y > centerY + LaneHeight * 0.5f) return SkillLane.Unten;
        return SkillLane.Mitte;
    }

    SkillNodeData NodeAt(List<SkillNodeData> nodes, Vector2 point, float centerY)
    {
        foreach (SkillNodeData n in nodes)
        {
            if (RectOf(n, centerY).Contains(point)) return n;
        }
        return null;
    }

    // ==================================================================
    //  Einstellungen rechts
    // ==================================================================

    void DrawInspector(Rect rect)
    {
        EditorGUI.DrawRect(rect, new Color(0.22f, 0.22f, 0.24f));

        GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f));
        inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll);

        SkillNodeData node = asset.Find(selectedId);

        if (node != null && node.category == category) DrawNodeInspector(node);
        else DrawCategoryInspector();

        EditorGUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    void DrawCategoryInspector()
    {
        EditorGUILayout.LabelField("Kategorie", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Kein Knoten gewaehlt. Klicke links einen an - oder aendere hier, wie diese " +
            "Kategorie im Hub aussieht.", MessageType.None);

        SkillCategoryData data = null;

        foreach (SkillCategoryData c in asset.categories)
        {
            if (c.category == category) { data = c; break; }
        }

        if (data == null) return;

        EditorGUI.BeginChangeCheck();

        string name  = EditorGUILayout.TextField("Name auf dem Knopf", data.displayName);
        string path  = EditorGUILayout.TextField("Ueberschrift", data.pathLabel);
        Color color  = EditorGUILayout.ColorField("Farbe", data.color);

        EditorGUILayout.LabelField("Beschreibung");
        string desc  = EditorGUILayout.TextArea(data.description, GUILayout.Height(50f));

        EditorGUILayout.LabelField("Spruch darunter");
        string quote = EditorGUILayout.TextArea(data.quote, GUILayout.Height(34f));

        var icon = (Sprite)EditorGUILayout.ObjectField("Symbol", data.icon, typeof(Sprite), false);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(asset, "Kategorie aendern");
            data.displayName = name;
            data.pathLabel   = path;
            data.color       = color;
            data.description = desc;
            data.quote       = quote;
            data.icon        = icon;
            EditorUtility.SetDirty(asset);
        }

        EditorGUILayout.Space(16f);
        EditorGUILayout.LabelField("Baum", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        string display = EditorGUILayout.TextField("Anzeigename", asset.displayName);
        int who = EditorGUILayout.IntField(
            new GUIContent("Charakter", "Skin-Index aus dem Shop. -1 = keinem zugeordnet."),
            asset.characterIndex);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(asset, "Baum aendern");
            asset.displayName    = display;
            asset.characterIndex = who;
            EditorUtility.SetDirty(asset);
        }

        EditorGUILayout.LabelField("Id im Spielstand", asset.treeId);
        EditorGUILayout.HelpBox("Die Id nicht mehr aendern - sie steht so in skills.json.",
                                MessageType.None);
    }

    void DrawNodeInspector(SkillNodeData node)
    {
        // Der Startknoten ist nur der Anker der Kategorie: keine Belohnung, kein
        // Preis, keine Vorbedingung, und weg darf er auch nicht.
        bool isStart = SkillTreeAsset.IsStartId(node.id);

        EditorGUILayout.LabelField("Knoten", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Id", node.id);

        if (isStart)
        {
            EditorGUILayout.HelpBox(
                "Der Anfang dieser Kategorie. Er steht fest in Spalte 0, kostet nichts und gibt " +
                "nichts. Von hier gehen die drei Bahnen ab: den Knoten anklicken und einen der " +
                "drei Plus-Knoepfe nehmen - geradeaus, schraeg hoch, schraeg runter.",
                MessageType.Info);
        }

        EditorGUI.BeginChangeCheck();

        string name = EditorGUILayout.TextField(
            new GUIContent("Name", "Steht im Hub ueber der Beschreibung. Leer = aus der Id gebaut."),
            node.displayName);

        var shape = (SkillShape)EditorGUILayout.EnumPopup("Form", node.shape);

        int price = node.price;
        var lane = node.lane;
        int step = node.step;

        if (!isStart)
        {
            price = EditorGUILayout.IntField("Preis (SP)", node.price);

            EditorGUILayout.BeginHorizontal();
            lane = (SkillLane)EditorGUILayout.EnumPopup("Bahn", node.lane);
            step = EditorGUILayout.IntField("Spalte", node.step);
            EditorGUILayout.EndHorizontal();
        }

        var icon = (Sprite)EditorGUILayout.ObjectField(
            new GUIContent("Eigenes Bild", "Leer = die Form oben wird gezeichnet."),
            node.icon, typeof(Sprite), false);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(asset, "Knoten aendern");
            node.displayName = name;
            node.shape       = shape;
            node.icon        = icon;

            if (!isStart)
            {
                node.price = Mathf.Max(0, price);

                // Spalte 0 bleibt dem Start vorbehalten.
                step = Mathf.Max(1, step);
                if (!Occupied(lane, step, node)) { node.lane = lane; node.step = step; }
            }

            EditorUtility.SetDirty(asset);
        }

        if (!isStart)
        {
            EditorGUILayout.Space(10f);
            DrawRewards(node);

            EditorGUILayout.Space(10f);
            DrawRequirements(node);
        }

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Eigener Beschreibungstext", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(isStart
            ? "Steht im Hub in der Karte rechts, wenn die Maus auf dem Startknoten liegt."
            : "Leer lassen - dann baut sich der Text aus den Belohnungen.",
            MessageType.None);

        EditorGUI.BeginChangeCheck();
        string desc = EditorGUILayout.TextArea(node.description, GUILayout.Height(46f));

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(asset, "Beschreibung aendern");
            node.description = desc;
            EditorUtility.SetDirty(asset);
        }

        if (isStart) return;

        EditorGUILayout.Space(16f);

        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);

        if (GUILayout.Button(new GUIContent("Knoten loeschen",
                             "Geht auch mit Entf oder dem kleinen x am Knoten.")))
            pending = () => DeleteNode(node);

        GUI.backgroundColor = Color.white;
    }

    void DrawRewards(SkillNodeData node)
    {
        EditorGUILayout.LabelField("Was der Knoten gibt", EditorStyles.boldLabel);

        for (int i = 0; i < node.rewards.Count; i++)
        {
            SkillRewardData r = node.rewards[i];
            if (r == null) continue;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            var kind = (SkillRewardKind)EditorGUILayout.EnumPopup(r.kind);

            if (EditorGUI.EndChangeCheck())
            {
                // Wert oder Schalter zeichnet unterschiedliche Felder - also auch
                // hier erst im naechsten Durchgang umstellen.
                pending = () =>
                {
                    Undo.RecordObject(asset, "Belohnung aendern");
                    r.kind = kind;
                    EditorUtility.SetDirty(asset);
                };
            }

            if (GUILayout.Button("X", GUILayout.Width(24f)))
            {
                int index = i;
                pending = () =>
                {
                    Undo.RecordObject(asset, "Belohnung entfernen");
                    if (index < node.rewards.Count) node.rewards.RemoveAt(index);
                    EditorUtility.SetDirty(asset);
                };
            }

            EditorGUILayout.EndHorizontal();

            if (r.kind == SkillRewardKind.Wert) DrawStatReward(r);
            else DrawGrantReward(r);

            EditorGUI.BeginChangeCheck();
            string over = EditorGUILayout.TextField(
                new GUIContent("Eigener Text", "Leer = Text wird aus Effekt und Wert gebaut."),
                r.descriptionOverride);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(asset, "Belohnungstext aendern");
                r.descriptionOverride = over;
                EditorUtility.SetDirty(asset);
            }

            EditorGUILayout.LabelField("Zeigt im Spiel", r.ToRuntime().Describe(),
                                       EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("+ Wert"))
        {
            pending = () => AddReward(node, new SkillRewardData
            {
                kind  = SkillRewardKind.Wert,
                stat  = SkillType.IncreaseMaxHealth,
                value = SkillDefaults.ValueFor(SkillType.IncreaseMaxHealth),
            });
        }

        if (GUILayout.Button("+ Schalter"))
        {
            pending = () => AddReward(node, new SkillRewardData
            {
                kind    = SkillRewardKind.Schalter,
                grantId = SkillGrants.All.Count > 0 ? SkillGrants.All[0].Id : "",
            });
        }

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// Effekt aus der Liste, Wert daneben. Beim Umschalten des Effekts wird der
    /// Vorgabewert aus <see cref="SkillDefaults"/> eingetragen - ueberschreiben
    /// darf man ihn danach.
    /// </summary>
    void DrawStatReward(SkillRewardData r)
    {
        EditorGUI.BeginChangeCheck();
        var stat = (SkillType)EditorGUILayout.EnumPopup("Effekt", r.stat);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(asset, "Effekt aendern");
            r.stat  = stat;
            r.value = SkillDefaults.ValueFor(stat);
            EditorUtility.SetDirty(asset);
        }

        EditorGUI.BeginChangeCheck();
        float value = EditorGUILayout.FloatField("Wert", r.value);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(asset, "Wert aendern");
            r.value = value;
            EditorUtility.SetDirty(asset);
        }
    }

    /// <summary>
    /// Schalter aus <see cref="SkillGrants"/>. Der letzte Eintrag laesst eine
    /// eigene Id zu - damit man einen Schalter eintragen kann, bevor er im
    /// Katalog steht.
    /// </summary>
    void DrawGrantReward(SkillRewardData r)
    {
        int count = SkillGrants.All.Count;
        var labels = new string[count + 1];

        int current = count;   // "eigene Id"

        for (int i = 0; i < count; i++)
        {
            labels[i] = SkillGrants.All[i].Name;
            if (SkillGrants.All[i].Id == r.grantId) current = i;
        }

        labels[count] = "eigene Id...";

        EditorGUI.BeginChangeCheck();
        int picked = EditorGUILayout.Popup("Schalter", current, labels);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(asset, "Schalter aendern");
            r.grantId = picked < count ? SkillGrants.All[picked].Id : r.grantId;
            EditorUtility.SetDirty(asset);
        }

        if (picked >= count || SkillGrants.Find(r.grantId) == null)
        {
            EditorGUI.BeginChangeCheck();
            string id = EditorGUILayout.TextField("Id", r.grantId);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(asset, "Schalter aendern");
                r.grantId = id;
                EditorUtility.SetDirty(asset);
            }

            EditorGUILayout.HelpBox(
                "Diese Id steht noch nicht in SkillGrants. Solange sie dort fehlt, fragt sie " +
                "im Spiel niemand ab - eine Zeile in SkillGrants.All ergaenzen.",
                MessageType.Warning);
        }
        else
        {
            EditorGUILayout.LabelField(" ", SkillGrants.DescriptionOf(r.grantId),
                                       EditorStyles.wordWrappedMiniLabel);
        }
    }

    void DrawRequirements(SkillNodeData node)
    {
        EditorGUILayout.LabelField("Braucht vorher", EditorStyles.boldLabel);

        if (GUILayout.Button(linkSourceId == node.id
                ? "Verbinden abbrechen"
                : "Verbinden: naechsten Klick als Voraussetzung setzen"))
        {
            linkSourceId = linkSourceId == node.id ? null : node.id;
            Repaint();
        }

        EditorGUILayout.HelpBox(
            "Mehrere Haken heisst: ALLE davon muessen gekauft sein. So braucht der zweite " +
            "Knoten oben z.B. den ersten oben UND den ersten in der Mitte.",
            MessageType.None);

        foreach (SkillNodeData other in asset.NodesOf(category))
        {
            if (other == node) continue;

            bool was = node.requires.Contains(other.id);
            bool now = EditorGUILayout.ToggleLeft(
                $"{other.lane}  •  Spalte {other.step}  •  {DisplayNameOf(other)}", was);

            if (now == was) continue;

            Undo.RecordObject(asset, "Voraussetzung aendern");

            if (now)
            {
                if (!WouldLoop(node, other)) node.requires.Add(other.id);
                else Debug.LogWarning($"[Skills] '{other.id}' als Voraussetzung von '{node.id}' " +
                                      "wuerde einen Kreis ergeben - beide warten dann ewig aufeinander.");
            }
            else
            {
                node.requires.Remove(other.id);
            }

            EditorUtility.SetDirty(asset);
        }
    }

    void AddRequirement(SkillNodeData node, string parentId)
    {
        // Der Startknoten haengt an nichts - sonst waere er nicht mehr der Anfang.
        if (SkillTreeAsset.IsStartId(node.id)) return;
        if (node.requires.Contains(parentId)) return;

        SkillNodeData parent = asset.Find(parentId);
        if (parent == null || parent.category != node.category) return;

        if (WouldLoop(node, parent))
        {
            Debug.LogWarning($"[Skills] '{parentId}' als Voraussetzung von '{node.id}' wuerde " +
                             "einen Kreis ergeben.");
            return;
        }

        Undo.RecordObject(asset, "Voraussetzung anhaengen");
        node.requires.Add(parentId);
        EditorUtility.SetDirty(asset);
    }

    /// <summary>
    /// Haengt <paramref name="parent"/> selbst (ueber Ecken) an
    /// <paramref name="node"/>? Dann waeren beide nie freizuschalten.
    /// </summary>
    bool WouldLoop(SkillNodeData node, SkillNodeData parent)
    {
        var seen = new HashSet<string>();
        var stack = new Stack<SkillNodeData>();
        stack.Push(parent);

        while (stack.Count > 0)
        {
            SkillNodeData current = stack.Pop();
            if (current == null || !seen.Add(current.id)) continue;
            if (current.id == node.id) return true;

            foreach (string id in current.requires) stack.Push(asset.Find(id));
        }

        return false;
    }

    // ==================================================================
    //  Aendern
    // ==================================================================

    void AddReward(SkillNodeData node, SkillRewardData reward)
    {
        Undo.RecordObject(asset, "Belohnung anhaengen");
        node.rewards.Add(reward);
        EditorUtility.SetDirty(asset);
    }

    void AddNode(SkillLane lane, int step, SkillNodeData parent)
    {
        Undo.RecordObject(asset, "Knoten anlegen");

        // Spalte 0 gehoert dem Startknoten - alles Neue faengt bei 1 an, und wenn
        // kein Vorgaenger angeklickt wurde, haengt es am Start.
        step = Mathf.Max(1, step);
        if (parent == null) parent = asset.StartOf(category);

        SkillNodeData node = SkillTreeTextIO.NewNode(asset, category, lane);
        node.step  = step;
        node.price = SkillDefaults.PriceForStep(step);

        if (parent != null) node.requires.Add(parent.id);

        asset.nodes.Add(node);
        selectedId = node.id;

        EditorUtility.SetDirty(asset);
    }

    void DeleteNode(SkillNodeData node)
    {
        // Ohne Startknoten haette die Kategorie keinen Anfang mehr.
        if (SkillTreeAsset.IsStartId(node.id))
        {
            Debug.LogWarning("[Skills] Der Startknoten einer Kategorie bleibt.");
            return;
        }

        Undo.RecordObject(asset, "Knoten loeschen");

        // Was an ihm hing, haengt danach an SEINEM Vorgaenger - sonst stuende es
        // ohne Linie da und waere im Hub sofort kaufbar. Hatte er keinen, uebernimmt
        // der Startknoten.
        string heir = node.requires.Count > 0
            ? node.requires[0]
            : SkillTreeAsset.StartIdOf(node.category);

        foreach (SkillNodeData other in asset.nodes)
        {
            if (other == null || !other.requires.Remove(node.id)) continue;

            if (other.id != heir && !other.requires.Contains(heir)) other.requires.Add(heir);
        }

        asset.nodes.Remove(node);
        selectedId = null;
        linkSourceId = null;

        EditorUtility.SetDirty(asset);
    }

    // ==================================================================

    string DisplayNameOf(SkillNodeData n)
    {
        if (n == null) return "";
        if (!string.IsNullOrWhiteSpace(n.displayName)) return n.displayName;

        return n.rewards.Count > 0 ? n.rewards[0].ToRuntime().Describe() : n.id;
    }

    string NameOf(string id) => DisplayNameOf(asset != null ? asset.Find(id) : null);

    /// <summary>Ruft das Fenster, nachdem ein Baum angelegt wurde.</summary>
    internal void Adopt(SkillTreeAsset created)
    {
        RefreshTreeList();
        SetAsset(created);
        Repaint();
    }
}

/// <summary>
/// Das kleine Fenster hinter "Neu..." - Id, Name, Charakter und wahlweise ein
/// Baum als Vorlage. Die Vorlage kopiert alle Knoten, sodass man den Baum eines
/// Charakters als Ausgangspunkt fuer den naechsten nehmen und nur noch anpassen
/// kann.
/// </summary>
public class SkillTreeCreateWindow : EditorWindow
{
    SkillTreeEditorWindow owner;

    string treeId = "char_1";
    string displayName = "Skilltree";
    int characterIndex = 1;
    int templateIndex;

    List<SkillTreeAsset> trees;

    /// <summary>
    /// Ohne Charakter: die Felder stehen auf dem naechsten freien Platz.
    /// Mit Charakter (aus der Auswahl oben, "noch kein Baum"): Id, Name und
    /// Nummer sind schon ausgefuellt, es bleibt nur noch die Vorlage.
    /// </summary>
    public static void Show(SkillTreeEditorWindow owner, int characterIndex = -1)
    {
        var w = CreateInstance<SkillTreeCreateWindow>();
        w.owner = owner;
        w.trees = SkillTreeTextIO.LoadAll();

        // Ohne Vorgabe den ersten Charakter nehmen, der noch keinen Baum hat.
        if (characterIndex < 0) characterIndex = FirstFreeCharacter(w.trees);

        if (characterIndex >= 0)
        {
            w.characterIndex = characterIndex;
            w.treeId         = "char_" + characterIndex;
            w.displayName    = Characters.NameOf(characterIndex);
        }

        w.titleContent = new GUIContent("Neuer Skilltree");
        w.minSize = w.maxSize = new Vector2(400f, 240f);
        w.ShowUtility();
    }

    /// <summary>Der erste Charakter ohne eigenen Baum, oder -1 wenn alle einen haben.</summary>
    static int FirstFreeCharacter(List<SkillTreeAsset> trees)
    {
        for (int i = 0; i < Characters.Count; i++)
        {
            bool taken = false;

            foreach (SkillTreeAsset t in trees)
            {
                if (t != null && t.characterIndex == i) { taken = true; break; }
            }

            if (!taken) return i;
        }

        return -1;
    }

    void OnGUI()
    {
        EditorGUILayout.Space(6f);

        treeId = EditorGUILayout.TextField(
            new GUIContent("Id", "Schluessel im Spielstand, z.B. char_1. Spaeter nicht mehr aendern."),
            treeId);

        displayName = EditorGUILayout.TextField("Anzeigename", displayName);

        characterIndex = EditorGUILayout.IntField(
            new GUIContent("Charakter", "Skin-Index aus dem Shop. -1 = keinem zugeordnet."),
            characterIndex);

        var labels = new string[trees.Count + 1];
        labels[0] = "leer (nur der Startknoten je Kategorie)";

        for (int i = 0; i < trees.Count; i++)
            labels[i + 1] = $"Kopie von {trees[i].displayName} ({trees[i].treeId})";

        templateIndex = EditorGUILayout.Popup("Vorlage", templateIndex, labels);

        EditorGUILayout.Space(10f);
        EditorGUILayout.HelpBox(
            "Eine Kopie uebernimmt alle Knoten mitsamt Werten und Verbindungen. Die Ids bleiben " +
            "gleich - das ist in Ordnung, weil im Spielstand zusaetzlich die Baum-Id steht.",
            MessageType.None);

        GUILayout.FlexibleSpace();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Abbrechen")) Close();

        if (GUILayout.Button("Anlegen"))
        {
            SkillTreeAsset template = templateIndex > 0 ? trees[templateIndex - 1] : null;
            SkillTreeAsset created = SkillTreeTextIO.Create(treeId, displayName, characterIndex, template);

            if (created != null)
            {
                owner?.Adopt(created);
                Close();
            }
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(6f);
    }
}
