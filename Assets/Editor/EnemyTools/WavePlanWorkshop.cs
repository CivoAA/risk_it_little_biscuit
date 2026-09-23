using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Die Wellenplan-Werkstatt: welcher Gegner kommt auf welcher Karte, wann, wie
/// viele - und wie voll es dabei wird.
///
/// WARUM ES DAS GIBT
///
/// Ein Plan ist schnell geschrieben, aber schwer zu beurteilen. Drei Fragen
/// stellen sich beim Balancing immer wieder, und keine davon beantwortet der
/// Quelltext von selbst:
///
///   1. "Ist Karte 2 vorne wirklich so leicht wie Karte 1 und hinten haerter?"
///      Dafuer gibt es die Druckkurve mit Vergleichsplan - zwei Linien
///      uebereinander sagen das in einer Sekunde, 200 Zeilen Code nicht.
///   2. "Laufen auf Karte 2 noch Gegner aus Karte 1 herum?" Dafuer gibt es die
///      Ueberschneidungs-Anzeige. Von Hand muesste man vier Plaene nebeneinander
///      legen und Namen vergleichen.
///   3. "Wie viele Gegner sind Druck 150 eigentlich?" Steht neben jeder Phase,
///      ausgerechnet aus den Gewichten der Gegner im Pool.
///
/// Gespeichert wird nach <c>WavePlans.cs</c>, in den Block zwischen den beiden
/// Markern. Die Beschreibungstexte ueber den Plaenen werden dabei mitgenommen -
/// sie stehen hier als "Notiz" und sind dieselbe Prosa, die im Quelltext ueber
/// der Methode landet.
/// </summary>
public class WavePlanWorkshop : EditorWindow
{
    private const string PlansPath = "Assets/Scripts/Enemy/Spawning/WavePlans.cs";

    /// <summary>Welcher Plan auf welcher Karte laeuft - steht sonst nirgends zusammen.</summary>
    private static string MapOf(string planId)
    {
        switch (planId)
        {
            case "World0": return "Karte 1 - Kueche";
            case "World3": return "Karte 2 - Wald";
            case "World1": return "Nachtwald (liegt still)";
            case "World2": return "Dorf (liegt still)";
            default: return "keiner Karte zugeordnet";
        }
    }

    // ------------------------------------------------------------------ Daten

    private class PoolDraft
    {
        public EnemyId id;
        public float weight;
    }

    private class BeatDraft
    {
        public BeatKind kind;
        public float time;
        public EnemyId enemy;
        public float threat;
        public string pattern = "Scatter";
        public float radius;

        public EnemyId ringEnemy;
        public int ringCount;
        public bool cage;
        public float warnTime = 1.5f;

        public float pressureScale = 1f;
        public float duration;
        public string announce = "";
    }

    private class PhaseDraft
    {
        public string name = "";
        public float duration = 300f;
        public float pressureStart;
        public float pressureEnd;
        public string basePattern = "Scatter";
        public bool endless;
        public bool open = true;

        public readonly List<PoolDraft> pool = new List<PoolDraft>();
        public readonly List<BeatDraft> beats = new List<BeatDraft>();
    }

    private class PlanDraft
    {
        public string id;
        public string note = "";
        public readonly List<PhaseDraft> phases = new List<PhaseDraft>();
    }

    private List<PlanDraft> plans = new List<PlanDraft>();
    private int selected;
    private bool dirty;

    private Vector2 listScroll;
    private Vector2 bodyScroll;

    /// <summary>Plan, dessen Kurve zum Vergleich mitgezeichnet wird. -1 = keiner.</summary>
    private int compareTo = -1;

    private bool showNote;

    // ------------------------------------------------------------------ Start

    [MenuItem("Tools/Gegner/Wellenplaene", false, 1)]
    public static void Open()
    {
        WavePlanWorkshop window = GetWindow<WavePlanWorkshop>("Wellenplaene");
        window.minSize = new Vector2(940f, 600f);
        window.Reload();
        window.Show();
    }

    private void OnEnable()
    {
        if (plans.Count == 0) Reload();
    }

    private void Reload()
    {
        plans = new List<PlanDraft>();

        Dictionary<string, string> notes = ReadNotes();

        foreach (string id in WavePlans.AllIds)
        {
            RunPlan plan = WavePlans.For(id);
            var draft = new PlanDraft { id = id };

            notes.TryGetValue(id, out draft.note);

            foreach (Phase phase in plan.Phases) draft.phases.Add(ToDraft(phase, false));
            if (plan.Endless != null) draft.phases.Add(ToDraft(plan.Endless, true));

            plans.Add(draft);
        }

        selected = Mathf.Clamp(selected, 0, Mathf.Max(0, plans.Count - 1));
        dirty = false;
    }

    private static PhaseDraft ToDraft(Phase phase, bool endless)
    {
        var draft = new PhaseDraft
        {
            name = phase.Name,
            duration = phase.Duration,
            pressureStart = phase.PressureStart,
            pressureEnd = phase.PressureEnd,
            basePattern = Patterns.NameOf(phase.BasePattern),
            endless = endless,
        };

        foreach (PoolEntry entry in phase.Enemies)
        {
            draft.pool.Add(new PoolDraft { id = entry.Id, weight = entry.Weight });
        }

        foreach (Beat beat in phase.Beats)
        {
            draft.beats.Add(new BeatDraft
            {
                kind = beat.Kind,
                time = beat.Time,
                enemy = beat.Enemy,
                threat = beat.Threat,
                pattern = Patterns.NameOf(beat.Pattern),
                radius = beat.Radius,
                ringEnemy = beat.RingEnemy,
                ringCount = beat.RingCount,
                cage = beat.Cage,
                warnTime = beat.WarnTime,
                pressureScale = beat.PressureScale,
                duration = beat.Duration,
                announce = beat.Announce ?? "",
            });
        }

        return draft;
    }

    /// <summary>
    /// Holt die Beschreibungstexte ueber den Plan-Methoden aus der Quelldatei.
    ///
    /// Ohne das waere jedes Speichern ein Verlust: die Prosa ueber einem Plan
    /// ("der Anfang liegt gleichauf, das Ende deutlich darueber") ist das
    /// Einzige, was die Absicht hinter den Zahlen festhaelt.
    /// </summary>
    private static Dictionary<string, string> ReadNotes()
    {
        var notes = new Dictionary<string, string>();

        string full = Path.GetFullPath(PlansPath);
        if (!File.Exists(full)) return notes;

        string[] lines = File.ReadAllLines(full);

        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();
            if (!trimmed.StartsWith("public static RunPlan ")) continue;

            int open = trimmed.IndexOf('(');
            if (open < 0) continue;

            string name = trimmed.Substring("public static RunPlan ".Length,
                                            open - "public static RunPlan ".Length).Trim();

            var text = new List<string>();
            for (int j = i - 1; j >= 0; j--)
            {
                string line = lines[j].Trim();
                if (!line.StartsWith("///")) break;

                line = line.Substring(3).Trim();
                if (line == "<summary>" || line == "</summary>") continue;

                text.Insert(0, line);
            }

            notes[name] = string.Join("\n", text).Trim();
        }

        return notes;
    }

    // -------------------------------------------------------------------- GUI

    private void OnGUI()
    {
        DrawToolbar();

        EditorGUILayout.BeginHorizontal();
        DrawPlanList();

        if (selected >= 0 && selected < plans.Count) DrawPlan(plans[selected]);
        else EditorGUILayout.HelpBox("Kein Plan gewaehlt.", MessageType.Info);

        EditorGUILayout.EndHorizontal();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (dirty) GUILayout.Label("ungespeicherte Aenderungen", EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();

        using (new EditorGUI.DisabledScope(!dirty))
        {
            if (GUILayout.Button("Plaene speichern", EditorStyles.toolbarButton, GUILayout.Width(130f)))
            {
                Save();
            }
        }

        if (GUILayout.Button("Neu laden", EditorStyles.toolbarButton, GUILayout.Width(80f)))
        {
            if (!dirty || EditorUtility.DisplayDialog("Neu laden",
                    "Ungespeicherte Aenderungen gehen verloren. Trotzdem neu laden?",
                    "Neu laden", "Abbrechen"))
            {
                Reload();
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawPlanList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(200f));
        listScroll = EditorGUILayout.BeginScrollView(listScroll);

        for (int i = 0; i < plans.Count; i++)
        {
            bool on = i == selected;
            bool now = GUILayout.Toggle(on, plans[i].id + "\n" + MapOf(plans[i].id),
                                        "Button", GUILayout.Height(38f));
            if (now && !on)
            {
                selected = i;
                GUI.FocusControl(null);
            }
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.LabelField("Vergleichskurve", EditorStyles.miniBoldLabel);
        string[] names = plans.Select(p => p.id).Prepend("keine").ToArray();
        int pick = EditorGUILayout.Popup(compareTo + 1, names) - 1;
        if (pick != compareTo)
        {
            compareTo = pick;
            Repaint();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawPlan(PlanDraft plan)
    {
        EditorGUILayout.BeginVertical();
        bodyScroll = EditorGUILayout.BeginScrollView(bodyScroll);

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.LabelField(plan.id + "  -  " + MapOf(plan.id), EditorStyles.boldLabel);

        DrawOverlap(plan);
        DrawPressureCurve(plan);

        showNote = EditorGUILayout.Foldout(showNote, "Notiz (wird als Kommentar gespeichert)", true);
        if (showNote)
        {
            plan.note = EditorGUILayout.TextArea(plan.note, GUILayout.MinHeight(70f));
        }

        EditorGUILayout.Space(6f);

        for (int i = 0; i < plan.phases.Count; i++)
        {
            DrawPhase(plan, plan.phases[i], i);
        }

        EditorGUILayout.Space(4f);
        if (GUILayout.Button("Phase anhaengen"))
        {
            int insertAt = plan.phases.FindIndex(p => p.endless);
            var fresh = new PhaseDraft { name = "Neue Phase", duration = 300f, pressureStart = 20f, pressureEnd = 60f };
            if (insertAt < 0) plan.phases.Add(fresh);
            else plan.phases.Insert(insertAt, fresh);
            dirty = true;
        }

        if (EditorGUI.EndChangeCheck()) dirty = true;

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    // -------------------------------------------------------- Ueberschneidung

    /// <summary>
    /// Welche Gegner dieser Plan mit den anderen teilt. Genau die Frage, die
    /// hinter "Karte 2 soll nicht dieselben Gegner haben wie Karte 1" steckt.
    /// </summary>
    private void DrawOverlap(PlanDraft plan)
    {
        HashSet<EnemyId> mine = EnemiesOf(plan);

        var lines = new List<string>();
        foreach (PlanDraft other in plans)
        {
            if (other == plan) continue;

            HashSet<EnemyId> theirs = EnemiesOf(other);
            theirs.IntersectWith(mine);
            if (theirs.Count == 0) continue;

            lines.Add(other.id + ": " + string.Join(", ", theirs.Select(NameOf)));
        }

        EditorGUILayout.LabelField("Gegner in diesem Plan: " + mine.Count
                                 + " (" + string.Join(", ", mine.Select(NameOf)) + ")",
                                   EditorStyles.wordWrappedMiniLabel);

        if (lines.Count == 0)
        {
            EditorGUILayout.HelpBox("Keine Ueberschneidung mit anderen Plaenen.", MessageType.Info);
            return;
        }

        EditorGUILayout.HelpBox("Auch in anderen Plaenen:\n" + string.Join("\n", lines),
                                MessageType.None);
    }

    private static HashSet<EnemyId> EnemiesOf(PlanDraft plan)
    {
        var set = new HashSet<EnemyId>();

        foreach (PhaseDraft phase in plan.phases)
        {
            foreach (PoolDraft entry in phase.pool)
            {
                if (entry.id != EnemyId.None) set.Add(entry.id);
            }
            foreach (BeatDraft beat in phase.beats)
            {
                if (beat.kind == BeatKind.Calm) continue;
                if (beat.enemy != EnemyId.None) set.Add(beat.enemy);
                if (beat.ringEnemy != EnemyId.None) set.Add(beat.ringEnemy);
            }
        }

        return set;
    }

    private static string NameOf(EnemyId id)
    {
        EnemyDef def = EnemyCatalog.Get(id);
        return def != null ? def.Name : id.ToString();
    }

    // ------------------------------------------------------------- Druckkurve

    /// <summary>
    /// Der Druckverlauf ueber den ganzen Lauf, optional mit einem zweiten Plan
    /// darunter. Beats sitzen als Striche auf der Linie.
    /// </summary>
    private void DrawPressureCurve(PlanDraft plan)
    {
        Rect box = GUILayoutUtility.GetRect(10f, 150f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(box, new Color(0.15f, 0.15f, 0.17f));

        float maxPressure = 10f;
        float totalTime = 0f;

        foreach (PlanDraft p in plans)
        {
            if (p != plan && plans.IndexOf(p) != compareTo) continue;
            foreach (PhaseDraft phase in p.phases)
            {
                if (phase.endless) continue;
                maxPressure = Mathf.Max(maxPressure, phase.pressureStart, phase.pressureEnd);
                if (p == plan) totalTime += phase.duration;
            }
        }

        if (totalTime <= 0f) return;
        maxPressure *= 1.1f;

        if (compareTo >= 0 && compareTo < plans.Count && plans[compareTo] != plan)
        {
            DrawCurve(box, plans[compareTo], totalTime, maxPressure,
                      new Color(0.45f, 0.55f, 0.75f, 0.75f), false);
        }

        DrawCurve(box, plan, totalTime, maxPressure, new Color(0.95f, 0.72f, 0.3f), true);

        GUI.Label(new Rect(box.x + 6f, box.y + 2f, 300f, 16f),
                  "Druck bis " + Mathf.RoundToInt(maxPressure) + "   |   Lauf "
                  + Mathf.RoundToInt(totalTime / 60f) + " min", EditorStyles.miniLabel);

        if (compareTo >= 0 && compareTo < plans.Count && plans[compareTo] != plan)
        {
            GUI.Label(new Rect(box.x + 6f, box.yMax - 16f, 300f, 16f),
                      "blau: " + plans[compareTo].id, EditorStyles.miniLabel);
        }
    }

    private static void DrawCurve(Rect box, PlanDraft plan, float totalTime, float maxPressure,
                                  Color color, bool withBeats)
    {
        Handles.BeginGUI();
        Handles.color = color;

        float t = 0f;
        Vector3 previous = Vector3.zero;
        bool first = true;

        foreach (PhaseDraft phase in plan.phases)
        {
            if (phase.endless) continue;

            Vector3 a = Point(box, t, phase.pressureStart, totalTime, maxPressure);
            Vector3 b = Point(box, t + phase.duration, phase.pressureEnd, totalTime, maxPressure);

            if (!first) Handles.DrawLine(previous, a);
            Handles.DrawLine(a, b);

            previous = b;
            first = false;

            if (withBeats)
            {
                foreach (BeatDraft beat in phase.beats)
                {
                    float x = Point(box, t + beat.time, 0f, totalTime, maxPressure).x;
                    Handles.color = BeatColor(beat.kind);
                    Handles.DrawLine(new Vector3(x, box.yMax - 12f), new Vector3(x, box.yMax - 2f));
                }
                Handles.color = color;
            }

            t += phase.duration;
        }

        Handles.EndGUI();
    }

    private static Vector3 Point(Rect box, float time, float pressure, float totalTime, float maxPressure)
    {
        float x = box.x + 6f + (box.width - 12f) * Mathf.Clamp01(time / totalTime);
        float y = box.yMax - 16f - (box.height - 34f) * Mathf.Clamp01(pressure / maxPressure);
        return new Vector3(x, y, 0f);
    }

    private static Color BeatColor(BeatKind kind)
    {
        switch (kind)
        {
            case BeatKind.Encirclement: return new Color(0.9f, 0.35f, 0.35f);
            case BeatKind.Calm: return new Color(0.4f, 0.8f, 0.5f);
            case BeatKind.Boss: return new Color(1f, 0.4f, 0.9f);
            default: return new Color(0.8f, 0.8f, 0.85f);
        }
    }

    // ------------------------------------------------------------------ Phase

    private void DrawPhase(PlanDraft plan, PhaseDraft phase, int index)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        phase.open = EditorGUILayout.Foldout(phase.open,
            (phase.endless ? "[Endlos] " : "Phase " + (index + 1) + ": ") + phase.name, true);

        GUILayout.FlexibleSpace();
        GUILayout.Label(DescribePressure(phase), EditorStyles.miniLabel);

        using (new EditorGUI.DisabledScope(phase.endless))
        {
            if (GUILayout.Button("x", GUILayout.Width(22f))
                && EditorUtility.DisplayDialog("Phase loeschen?",
                       "\"" + phase.name + "\" aus " + plan.id + " entfernen?", "Loeschen", "Abbrechen"))
            {
                plan.phases.Remove(phase);
                dirty = true;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
        }
        EditorGUILayout.EndHorizontal();

        if (!phase.open)
        {
            EditorGUILayout.EndVertical();
            return;
        }

        phase.name = EditorGUILayout.TextField("Name (steht im Bild)", phase.name);

        using (new EditorGUI.DisabledScope(phase.endless))
        {
            phase.duration = EditorGUILayout.FloatField(new GUIContent(
                "Dauer (s)", "Die Endlos-Phase laeuft, bis der Lauf endet."), phase.duration);
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(new GUIContent("Druck von / bis",
            "Bedrohungssumme, die auf dem Feld gehalten wird. Nicht Stueckzahl."));
        phase.pressureStart = EditorGUILayout.FloatField(phase.pressureStart);
        phase.pressureEnd = EditorGUILayout.FloatField(phase.pressureEnd);
        EditorGUILayout.EndHorizontal();

        phase.basePattern = PatternField("Grundmuster", phase.basePattern);

        // ---- Pool
        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField("Gegner-Pool (gewichtet)", EditorStyles.miniBoldLabel);

        float weightSum = phase.pool.Sum(e => Mathf.Max(0.01f, e.weight));

        for (int i = 0; i < phase.pool.Count; i++)
        {
            PoolDraft entry = phase.pool[i];

            EditorGUILayout.BeginHorizontal();
            entry.id = (EnemyId)EditorGUILayout.EnumPopup(entry.id);
            entry.weight = EditorGUILayout.FloatField(entry.weight, GUILayout.Width(55f));
            GUILayout.Label((entry.weight / weightSum * 100f).ToString("0") + "%",
                            EditorStyles.miniLabel, GUILayout.Width(38f));
            GUILayout.Label("Gew. " + EnemyCatalog.Threat(entry.id).ToString("0.#"),
                            EditorStyles.miniLabel, GUILayout.Width(55f));

            if (GUILayout.Button("-", GUILayout.Width(22f)))
            {
                phase.pool.RemoveAt(i);
                dirty = true;
                EditorGUILayout.EndHorizontal();
                break;
            }
            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("Gegner in den Pool", GUILayout.Width(150f)))
        {
            phase.pool.Add(new PoolDraft { id = EnemyId.Marshmello, weight = 20f });
            dirty = true;
        }

        // ---- Beats
        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField("Beats (feste Zeitpunkte)", EditorStyles.miniBoldLabel);

        foreach (BeatDraft beat in phase.beats.OrderBy(b => b.time).ToList())
        {
            DrawBeat(phase, beat);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Schwall", GUILayout.Width(90f)))
        {
            phase.beats.Add(new BeatDraft { kind = BeatKind.Burst, time = 30f, enemy = EnemyId.Marshmello, threat = 25f });
            dirty = true;
        }
        if (GUILayout.Button("+ Ring", GUILayout.Width(90f)))
        {
            phase.beats.Add(new BeatDraft
            {
                kind = BeatKind.Encirclement, time = 120f, enemy = EnemyId.None,
                ringEnemy = EnemyId.Marshmello, ringCount = 20, radius = 13f,
                pressureScale = 0.35f, duration = 25f, warnTime = 1.5f, announce = "RING!",
            });
            dirty = true;
        }
        if (GUILayout.Button("+ Atempause", GUILayout.Width(100f)))
        {
            phase.beats.Add(new BeatDraft { kind = BeatKind.Calm, time = 150f, duration = 10f, pressureScale = 0.15f });
            dirty = true;
        }
        if (GUILayout.Button("+ Boss", GUILayout.Width(80f)))
        {
            phase.beats.Add(new BeatDraft { kind = BeatKind.Boss, time = 2f, enemy = EnemyId.KeksKoenig, pressureScale = 0.4f, duration = 999f, announce = "BOSS" });
            dirty = true;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawBeat(PhaseDraft phase, BeatDraft beat)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(beat.kind.ToString(), EditorStyles.boldLabel, GUILayout.Width(95f));
        GUILayout.Label("bei", EditorStyles.miniLabel, GUILayout.Width(22f));
        beat.time = EditorGUILayout.FloatField(beat.time, GUILayout.Width(50f));
        GUILayout.Label("s", EditorStyles.miniLabel, GUILayout.Width(12f));

        if (beat.time > phase.duration && !phase.endless)
        {
            GUILayout.Label("nach Phasenende - feuert nie!", EditorStyles.miniLabel);
        }

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("x", GUILayout.Width(22f)))
        {
            phase.beats.Remove(beat);
            dirty = true;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        EditorGUILayout.EndHorizontal();

        switch (beat.kind)
        {
            case BeatKind.Burst:
                beat.enemy = (EnemyId)EditorGUILayout.EnumPopup("Gegner", beat.enemy);
                beat.threat = EditorGUILayout.FloatField(new GUIContent("Bedrohung",
                    "Wird durch das Gewicht des Gegners geteilt: "
                  + Count(beat.threat, beat.enemy) + " Stueck."), beat.threat);
                beat.pattern = PatternField("Muster", beat.pattern);
                if (beat.pattern == "Ring")
                {
                    beat.radius = EditorGUILayout.FloatField("Radius", beat.radius);
                }
                break;

            case BeatKind.Encirclement:
                beat.enemy = (EnemyId)EditorGUILayout.EnumPopup(new GUIContent("Miniboss",
                    "None = nur der Ring, ohne Gegner in der Mitte."), beat.enemy);
                beat.ringEnemy = (EnemyId)EditorGUILayout.EnumPopup("Ring-Gegner", beat.ringEnemy);
                beat.ringCount = EditorGUILayout.IntField("Anzahl im Ring", beat.ringCount);
                beat.radius = EditorGUILayout.FloatField("Radius", beat.radius);
                beat.cage = EditorGUILayout.Toggle(new GUIContent("Kaefig",
                    "Blocker-Wand aussen herum. Loest sich erst auf, wenn ein MINIBOSS "
                  + "stirbt - ohne Miniboss in der Mitte steht sie fuer immer."), beat.cage);

                if (beat.cage && EnemyCatalog.Get(beat.enemy)?.Role != EnemyRole.MiniBoss)
                {
                    EditorGUILayout.HelpBox(
                        "Kaefig ohne Miniboss in der Mitte: die Wand verschwindet nie. "
                      + "Entweder den Kaefig abschalten oder einen Gegner mit Rolle "
                      + "MiniBoss eintragen.", MessageType.Error);
                }

                beat.warnTime = EditorGUILayout.FloatField("Vorwarnung (s)", beat.warnTime);
                beat.pressureScale = EditorGUILayout.Slider("Grunddruck dabei", beat.pressureScale, 0f, 1f);
                beat.duration = EditorGUILayout.FloatField("wie lange gedaempft (s)", beat.duration);
                beat.announce = EditorGUILayout.TextField("Ansage", beat.announce);
                break;

            case BeatKind.Calm:
                beat.duration = EditorGUILayout.FloatField("Dauer (s)", beat.duration);
                beat.pressureScale = EditorGUILayout.Slider(new GUIContent("Grunddruck dabei",
                    "0.15 heisst: nur noch 15% vom normalen Druck."), beat.pressureScale, 0f, 1f);
                break;

            case BeatKind.Boss:
                beat.enemy = (EnemyId)EditorGUILayout.EnumPopup("Boss", beat.enemy);
                beat.pressureScale = EditorGUILayout.Slider("Grunddruck dabei", beat.pressureScale, 0f, 1f);
                beat.announce = EditorGUILayout.TextField("Ansage", beat.announce);

                if (EnemyCatalog.Get(beat.enemy)?.Role != EnemyRole.Boss)
                {
                    EditorGUILayout.HelpBox(
                        "Dieser Gegner hat nicht die Rolle Boss. Ein Boss laeuft nicht von "
                      + "selbst - er braucht ein eigenes Skript wie der Keks-Koenig. Steht "
                      + "hier ein normaler Gegner, bleibt er einfach stehen.",
                        MessageType.Warning);
                }
                break;
        }

        EditorGUILayout.EndVertical();
    }

    private string PatternField(string label, string current)
    {
        int index = Mathf.Max(0, System.Array.IndexOf(Patterns.Names, current));
        int picked = EditorGUILayout.Popup(label, index, Patterns.Names);
        return Patterns.Names[Mathf.Clamp(picked, 0, Patterns.Names.Length - 1)];
    }

    /// <summary>Wie viele Gegner eine Bedrohungsmenge ergibt - die Frage kommt immer.</summary>
    private static string Count(float threat, EnemyId id)
    {
        float each = Mathf.Max(0.1f, EnemyCatalog.Threat(id));
        return Mathf.RoundToInt(threat / each).ToString();
    }

    private static string DescribePressure(PhaseDraft phase)
    {
        return string.Format(CultureInfo.InvariantCulture,
            "Druck {0:0} -> {1:0}   ({2} Beats)",
            phase.pressureStart, phase.pressureEnd, phase.beats.Count);
    }

    // -------------------------------------------------------------- Speichern

    private void Save()
    {
        string full = Path.GetFullPath(PlansPath);
        if (!File.Exists(full))
        {
            EditorUtility.DisplayDialog("Wellenplaene", "WavePlans.cs nicht gefunden.", "Ok");
            return;
        }

        string[] lines = File.ReadAllLines(full);
        int start = System.Array.FindIndex(lines, l => l.Contains("WERKSTATT-ANFANG"));
        int end = System.Array.FindIndex(lines, l => l.Contains("WERKSTATT-ENDE"));

        if (start < 0 || end < 0 || end <= start)
        {
            EditorUtility.DisplayDialog("Wellenplaene",
                "Die Marker WERKSTATT-ANFANG / WERKSTATT-ENDE fehlen in WavePlans.cs. "
              + "Ohne sie weiss das Tool nicht, welchen Teil es ersetzen darf - es "
              + "schreibt lieber gar nichts.", "Ok");
            return;
        }

        var sb = new StringBuilder();
        for (int i = 0; i <= start; i++) sb.AppendLine(lines[i]);
        sb.AppendLine("    // ================================================================");

        foreach (PlanDraft plan in plans) AppendPlan(sb, plan);

        // Die Trennlinie direkt ueber dem Endmarker gehoert noch zum ersetzten
        // Block, deshalb wird sie hier neu geschrieben und der Rest erst ab
        // dem Marker selbst uebernommen.
        sb.AppendLine("    // ================================================================");
        for (int i = end; i < lines.Length; i++) sb.AppendLine(lines[i]);

        File.WriteAllText(full, sb.ToString(), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(PlansPath);

        dirty = false;
        Debug.Log("[Wellenplaene] " + plans.Count + " Plaene nach " + PlansPath + " geschrieben.");
    }

    private static void AppendPlan(StringBuilder sb, PlanDraft plan)
    {
        sb.AppendLine();
        sb.AppendLine("    // ------------------------------------------------------------- " + plan.id);
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(plan.note))
        {
            sb.AppendLine("    /// <summary>");
            foreach (string line in plan.note.Replace("\r", "").Split('\n'))
            {
                sb.AppendLine(string.IsNullOrWhiteSpace(line) ? "    ///" : "    /// " + line.TrimEnd());
            }
            sb.AppendLine("    /// </summary>");
        }

        sb.AppendLine("    public static RunPlan " + plan.id + "()");
        sb.AppendLine("    {");
        sb.AppendLine("        var plan = new RunPlan(\"" + plan.id + "\");");

        foreach (PhaseDraft phase in plan.phases) AppendPhase(sb, phase);

        sb.AppendLine();
        sb.AppendLine("        return plan;");
        sb.AppendLine("    }");
    }

    private static void AppendPhase(StringBuilder sb, PhaseDraft phase)
    {
        sb.AppendLine();

        if (phase.endless)
        {
            sb.AppendLine("        plan.EndlessPhase(\"" + Escape(phase.name) + "\")");
        }
        else
        {
            sb.AppendLine("        plan.Phase(\"" + Escape(phase.name) + "\", " + F(phase.duration) + ")");
        }

        foreach (PoolDraft entry in phase.pool)
        {
            sb.AppendLine("            .Pool(EnemyId." + entry.id + ", " + F(entry.weight) + ")");
        }

        sb.AppendLine("            .Pressure(" + F(phase.pressureStart) + ", " + F(phase.pressureEnd) + ")");
        sb.Append("            .Base(Patterns." + phase.basePattern + ")");

        foreach (BeatDraft beat in phase.beats.OrderBy(b => b.time))
        {
            sb.AppendLine();
            sb.Append("            " + BeatCall(beat));
        }

        sb.AppendLine(";");
    }

    private static string BeatCall(BeatDraft beat)
    {
        switch (beat.kind)
        {
            case BeatKind.Encirclement:
                return ".Encircle(" + F(beat.time) + ", EnemyId." + beat.enemy
                     + ", EnemyId." + beat.ringEnemy + ", " + beat.ringCount
                     + ", " + F(beat.radius) + ", " + (beat.cage ? "true" : "false")
                     + ", \"" + Escape(beat.announce) + "\", " + F(beat.pressureScale)
                     + ", " + F(beat.duration) + ", " + F(beat.warnTime) + ")";

            case BeatKind.Calm:
                return ".Calm(" + F(beat.time) + ", " + F(beat.duration)
                     + ", " + F(beat.pressureScale) + ")";

            case BeatKind.Boss:
                return ".Boss(" + F(beat.time) + ", EnemyId." + beat.enemy
                     + ", \"" + Escape(beat.announce) + "\", " + F(beat.pressureScale) + ")";

            default:
                return ".Burst(" + F(beat.time) + ", EnemyId." + beat.enemy
                     + ", " + F(beat.threat) + ", Patterns." + beat.pattern
                     + ", " + F(beat.radius) + ")";
        }
    }

    private static string F(float value)
    {
        return value.ToString("0.#####", CultureInfo.InvariantCulture) + "f";
    }

    private static string Escape(string value)
    {
        return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
