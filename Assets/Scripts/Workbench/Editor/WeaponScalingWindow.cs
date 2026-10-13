using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Werkbank > Skalierungs-Texte.
///
/// Zeigt jede Waffe, jeden Buff und jede Evo aus <see cref="WeaponCatalog"/>
/// mit einem grossen Textfeld: wie sie mit Extra-Schuss, AOE, Wirkdauer und
/// Cooldown skaliert (beim Buff: was der Buff macht). Im Spiel steht der Text
/// in der Waffenkunde (<see cref="WeaponGuidePanel"/>) und in der Werkbank
/// hinter dem "?" der Info-Box.
///
/// Gespeichert wird als weapon.[id].scaling in
/// Assets/Resources/Localization/de.json und en.json - direkt neben .name und
/// .desc. Die Dateien werden zeilenweise angefasst, damit ihr Aufbau (eine
/// Zeile je Eintrag, Leerzeilen zwischen Gruppen) erhalten bleibt.
/// </summary>
public class WeaponScalingWindow : EditorWindow
{
    private const string LocFolder = "Assets/Resources/Localization/";
    private static readonly string[] Languages = { "de", "en" };
    private static readonly string[] LanguageLabels = { "Deutsch", "English" };
    private static readonly string[] KindLabels = { "Alle", "Waffen", "Buffs", "Evos" };

    [Serializable]
    private class LocEntry
    {
        public string key;
        public string value;
    }

    [Serializable]
    private class LocFile
    {
        public string language;
        public List<LocEntry> entries = new List<LocEntry>();
    }

    /// <summary>Stand auf der Platte, je Sprache: Schluessel -> Text.</summary>
    private readonly Dictionary<string, Dictionary<string, string>> saved =
        new Dictionary<string, Dictionary<string, string>>();

    /// <summary>Ungespeicherte Aenderungen, je Sprache: Schluessel -> Text.</summary>
    private readonly Dictionary<string, Dictionary<string, string>> edits =
        new Dictionary<string, Dictionary<string, string>>();

    private string selectedId;
    private int language;
    private int kindFilter;
    private string search = "";
    private Vector2 listScroll, textScroll;

    private GUIStyle textStyle, emptyStyle, dirtyStyle;

    [MenuItem("Tools/Werkbank/Skalierungs-Texte", false, 206)]
    private static void Open()
    {
        WeaponScalingWindow window = GetWindow<WeaponScalingWindow>("Skalierung");
        window.minSize = new Vector2(760f, 420f);
        window.Show();
    }

    private void OnEnable()
    {
        LoadAll();
        if (selectedId == null && WeaponCatalog.All.Count > 0) selectedId = WeaponCatalog.All[0].Id;
    }

    private void OnDestroy()
    {
        if (!HasEdits()) return;
        if (EditorUtility.DisplayDialog("Skalierungs-Texte",
                "Es gibt ungespeicherte Änderungen. Jetzt speichern?", "Speichern", "Verwerfen"))
        {
            SaveAll();
        }
    }

    // ==================================================================
    //  Oberflaeche
    // ==================================================================

    private void OnGUI()
    {
        BuildStyles();
        HandleShortcuts();

        EditorGUILayout.BeginHorizontal();
        DrawList();
        DrawDetail();
        EditorGUILayout.EndHorizontal();
    }

    private void BuildStyles()
    {
        if (textStyle != null) return;

        textStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true, fontSize = 13 };
        textStyle.padding = new RectOffset(8, 8, 8, 8);

        emptyStyle = new GUIStyle(EditorStyles.label);
        emptyStyle.normal.textColor = new Color(0.95f, 0.6f, 0.25f);

        dirtyStyle = new GUIStyle(EditorStyles.boldLabel);
        dirtyStyle.normal.textColor = new Color(0.95f, 0.8f, 0.3f);
    }

    private void HandleShortcuts()
    {
        Event e = Event.current;
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.S && (e.control || e.command))
        {
            SaveAll();
            e.Use();
        }
    }

    private void DrawList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(250f));

        kindFilter = GUILayout.Toolbar(kindFilter, KindLabels);
        search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);

        listScroll = EditorGUILayout.BeginScrollView(listScroll);
        PoolKind? shownKind = null;

        foreach (WeaponDef def in WeaponCatalog.All)
        {
            if (!Matches(def)) continue;

            if (shownKind != def.Kind)
            {
                shownKind = def.Kind;
                GUILayout.Space(4f);
                EditorGUILayout.LabelField(KindName(def.Kind), EditorStyles.boldLabel);
            }

            bool empty = string.IsNullOrWhiteSpace(Current(def.Id, Languages[language]));
            string label = def.Name + (IsDirty(def.Id) ? "  *" : "") + (empty ? "  (leer)" : "");

            GUIStyle style = def.Id == selectedId
                ? EditorStyles.selectionRect
                : empty ? emptyStyle : EditorStyles.label;

            if (GUILayout.Button(label, style))
            {
                selectedId = def.Id;
                GUI.FocusControl(null);
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private bool Matches(WeaponDef def)
    {
        if (kindFilter == 1 && def.Kind != PoolKind.Weapon) return false;
        if (kindFilter == 2 && def.Kind != PoolKind.Buff) return false;
        if (kindFilter == 3 && def.Kind != PoolKind.Evo) return false;
        if (string.IsNullOrEmpty(search)) return true;

        return def.Id.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
            || def.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void DrawDetail()
    {
        EditorGUILayout.BeginVertical();

        WeaponDef def = WeaponCatalog.Find(selectedId);
        if (def == null)
        {
            EditorGUILayout.HelpBox("Links einen Eintrag wählen.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.LabelField(def.Name, EditorStyles.largeLabel, GUILayout.Height(22f));
        EditorGUILayout.LabelField($"{KindName(def.Kind)}  ·  {def.Id}  ·  {def.ScalingKey}", EditorStyles.miniLabel);

        string extra = InfoLine(def);
        if (!string.IsNullOrEmpty(extra)) EditorGUILayout.LabelField(extra, EditorStyles.wordWrappedMiniLabel);

        string desc = def.Description;
        if (!string.IsNullOrEmpty(desc)) EditorGUILayout.HelpBox(desc, MessageType.None);

        GUILayout.Space(4f);
        EditorGUILayout.BeginHorizontal();
        language = GUILayout.Toolbar(language, LanguageLabels, GUILayout.Width(200f));
        GUILayout.FlexibleSpace();
        if (IsDirty(def.Id, Languages[language])) GUILayout.Label("ungespeichert", dirtyStyle);
        EditorGUILayout.EndHorizontal();

        string lang = Languages[language];
        string before = Current(def.Id, lang);

        textScroll = EditorGUILayout.BeginScrollView(textScroll, GUILayout.ExpandHeight(true));
        string after = EditorGUILayout.TextArea(before, textStyle, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();

        if (after != before) SetEdit(def.ScalingKey, lang, after);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label($"{after.Length} Zeichen · {CountLines(after)} Zeilen", EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();

        using (new EditorGUI.DisabledScope(!IsDirty(def.Id)))
        {
            if (GUILayout.Button("Eintrag zurücksetzen", GUILayout.Width(140f))) Revert(def.Id);
        }

        using (new EditorGUI.DisabledScope(!HasEdits()))
        {
            if (GUILayout.Button("Alles speichern (Strg+S)", GUILayout.Width(170f))) SaveAll();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "Vorlage: je eine Zeile für Extra-Schuss, Fläche (AOE), Wirkdauer und Cooldown. " +
            "Verhältnisse wie 1:3 heißen: drei Extra-Schüsse für ein Geschoss mehr. " +
            "Im Spiel: Waffenkunde im Shop (rechte Wand) und in der Werkbank hinter dem \"?\".",
            MessageType.None);

        EditorGUILayout.EndVertical();
    }

    /// <summary>Rezept bei Evos, Evo-Zugehoerigkeit bei Waffen und Buffs.</summary>
    private static string InfoLine(WeaponDef def)
    {
        if (def.Kind == PoolKind.Evo)
        {
            EvoDef recipe = WeaponCatalog.FindEvo(def.Id);
            if (recipe == null) return "";
            string a = recipe.A != null ? recipe.A.Name : recipe.IngredientA;
            string b = recipe.B != null ? recipe.B.Name : recipe.IngredientB;
            return recipe.IngredientCount == 1 ? $"Rezept: {a} auf Maximalstufe" : $"Rezept: {a} + {b}";
        }

        List<EvoDef> uses = WeaponCatalog.RecipesUsing(def.Id);
        if (uses.Count == 0) return "";
        List<string> names = new List<string>();
        foreach (EvoDef e in uses) names.Add(e.Name);
        return "Zutat für: " + string.Join(", ", names);
    }

    private static string KindName(PoolKind kind)
    {
        switch (kind)
        {
            case PoolKind.Weapon: return "Waffe";
            case PoolKind.Buff: return "Buff";
            default: return "Evo";
        }
    }

    private static int CountLines(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        int n = 1;
        foreach (char c in text) if (c == '\n') n++;
        return n;
    }

    // ==================================================================
    //  Daten
    // ==================================================================

    private static string Key(string id) => $"weapon.{id}.scaling";

    private string Current(string id, string lang)
    {
        string key = Key(id);
        if (edits.TryGetValue(lang, out var e) && e.TryGetValue(key, out string edited)) return edited;
        if (saved.TryGetValue(lang, out var s) && s.TryGetValue(key, out string onDisk)) return onDisk ?? "";
        return "";
    }

    private void SetEdit(string key, string lang, string text)
    {
        if (!edits.TryGetValue(lang, out var e)) edits[lang] = e = new Dictionary<string, string>();

        saved.TryGetValue(lang, out var s);
        string onDisk = s != null && s.TryGetValue(key, out string v) ? v ?? "" : "";

        if (text == onDisk) e.Remove(key);
        else e[key] = text;
    }

    private bool IsDirty(string id, string lang)
    {
        return edits.TryGetValue(lang, out var e) && e.ContainsKey(Key(id));
    }

    private bool IsDirty(string id)
    {
        foreach (string lang in Languages) if (IsDirty(id, lang)) return true;
        return false;
    }

    private bool HasEdits()
    {
        foreach (var e in edits.Values) if (e.Count > 0) return true;
        return false;
    }

    private void Revert(string id)
    {
        foreach (var e in edits.Values) e.Remove(Key(id));
        GUI.FocusControl(null);
    }

    private void LoadAll()
    {
        saved.Clear();
        foreach (string lang in Languages)
        {
            var map = new Dictionary<string, string>();
            string path = LocFolder + lang + ".json";
            if (File.Exists(path))
            {
                LocFile file = JsonUtility.FromJson<LocFile>(File.ReadAllText(path, Encoding.UTF8));
                if (file?.entries != null)
                {
                    foreach (LocEntry entry in file.entries)
                    {
                        if (entry != null && !string.IsNullOrEmpty(entry.key)) map[entry.key] = entry.value;
                    }
                }
            }
            saved[lang] = map;
        }
    }

    private void SaveAll()
    {
        int count = 0;

        foreach (string lang in Languages)
        {
            if (!edits.TryGetValue(lang, out var e) || e.Count == 0) continue;

            string path = LocFolder + lang + ".json";
            if (!File.Exists(path))
            {
                Debug.LogError($"[Skalierung] {path} nicht gefunden.");
                continue;
            }

            List<string> lines = new List<string>(File.ReadAllText(path, Encoding.UTF8).Split('\n'));
            foreach (var pair in e)
            {
                WriteEntry(lines, pair.Key, pair.Value);
                count++;
            }

            File.WriteAllText(path, string.Join("\n", lines), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(path);
            e.Clear();
        }

        LoadAll();
        Loc.Reload();
        GUI.FocusControl(null);
        if (count > 0) Debug.Log($"[Skalierung] {count} Text(e) gespeichert.");
    }

    /// <summary>
    /// Ersetzt die Zeile des Schluessels oder legt sie direkt hinter
    /// weapon.[id].desc an. Eine Zeile je Eintrag, wie im Rest der Datei.
    /// </summary>
    private static void WriteEntry(List<string> lines, string key, string value)
    {
        string marker = $"\"key\": \"{key}\"";
        string entry = $"{{ \"key\": \"{key}\", \"value\": \"{Escape(value)}\" }}";

        for (int i = 0; i < lines.Count; i++)
        {
            if (!lines[i].Contains(marker)) continue;
            bool comma = lines[i].TrimEnd('\r', ' ').EndsWith(",");
            lines[i] = Indent(lines[i]) + entry + (comma ? "," : "") + CarriageReturn(lines[i]);
            return;
        }

        string descMarker = $"\"key\": \"{key.Substring(0, key.Length - ".scaling".Length)}.desc\"";
        for (int i = 0; i < lines.Count; i++)
        {
            if (!lines[i].Contains(descMarker)) continue;

            string cr = CarriageReturn(lines[i]);
            string trimmed = lines[i].TrimEnd('\r', ' ');
            bool comma = trimmed.EndsWith(",");
            if (!comma) lines[i] = trimmed + "," + cr;

            lines.Insert(i + 1, Indent(lines[i]) + entry + (comma ? "," : "") + cr);
            return;
        }

        // Ohne .desc: gleich an den Anfang der Liste.
        for (int i = 0; i < lines.Count; i++)
        {
            if (!lines[i].Contains("\"entries\"")) continue;
            lines.Insert(i + 1, "    " + entry + "," + CarriageReturn(lines[i]));
            return;
        }

        Debug.LogError($"[Skalierung] Konnte {key} nicht einfügen - Datei hat keine \"entries\"-Liste.");
    }

    private static string Indent(string line)
    {
        int n = 0;
        while (n < line.Length && (line[n] == ' ' || line[n] == '\t')) n++;
        return line.Substring(0, n);
    }

    private static string CarriageReturn(string line) => line.EndsWith("\r") ? "\r" : "";

    private static string Escape(string text)
    {
        StringBuilder sb = new StringBuilder(text.Length + 16);
        foreach (char c in text)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }
}
