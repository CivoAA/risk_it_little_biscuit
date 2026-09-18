using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SKILLTREES ALS TEXT - lesen und schreiben.
///
/// Wozu: der Editor ist zum Bauen da, aber manchmal will man einen ganzen Baum
/// am Stueck hinschreiben oder diktieren lassen ("mach mir den Baum von
/// Charakter 2 wie den von Charakter 0, nur mit mehr Leben"). Dafuer gibt es
/// dieses Format. Es ist absichtlich eine Zeile je Knoten und ohne Klammern -
/// so kann man es tippen, ohne etwas zu vergessen.
///
/// DAS FORMAT
/// <code>
///   # Zeilen mit Raute sind Kommentare
///   tree id=char_0 name="Standard" character=0
///
///   category Kampf color=#B24141 name="KAMPF" path="KAMPFPFAD"
///            desc="Schaerfe deine Waffen." quote="\"Angriff ist die beste Verteidigung.\""
///
///   node id=kampf_mitte_1 cat=Kampf lane=Mitte step=1 shape=Kreis price=10
///        name="Schlagkraft" stat=IncreaseDamage:0.25
///
///   node id=kampf_oben_1  cat=Kampf lane=Oben  step=1 shape=Rechteck price=15
///        stat=IncreaseCritChance:0.05 req=kampf_mitte_1
///
///   node id=kampf_oben_2  cat=Kampf lane=Oben  step=2 shape=Stern price=60
///        name="Vier Richtungen" grant=shurikookie_vier_richtungen
///        req=kampf_oben_1,kampf_mitte_1
/// </code>
///
/// Regeln, die man sich merken muss:
///   - Ein Knoten darf MEHRERE stat= und grant= haben. Sie werden alle vergeben.
///   - req= zaehlt die Ids auf, die vorher gekauft sein muessen; mehrere mit
///     Komma. Alle muessen aus DERSELBEN Kategorie kommen.
///   - Jede Kategorie hat genau EINEN Startknoten: id=kampf_start, Bahn Mitte,
///     Spalte 0. Er kostet nichts und gibt nichts - er ist nur der Punkt, an dem
///     die drei Bahnen haengen. Hinschreiben muss man ihn nicht: fehlt er, wird
///     er beim Einlesen ergaenzt.
///   - Leeres req= (oder gar keins) haengt den Knoten an genau diesen Start.
///   - lane ist Oben, Mitte oder Unten. step ist die Spalte von links. Spalte 0
///     gehoert dem Startknoten, alles andere faengt bei 1 an.
///   - Ein Knoten darf weiter oben auf einen zeigen, der weiter unten steht -
///     die Reihenfolge der Zeilen ist egal.
///   - Umbrueche innerhalb einer Zeile sind erlaubt, solange die Folgezeile
///     eingerueckt ist und nicht mit tree/category/node beginnt.
///
/// Zu erreichen ueber Tools -> Skilltree.
/// </summary>
public static class SkillTreeTextIO
{
    public const string AssetFolder = "Assets/Resources/" + SkillTrees.ResourceFolder;

    // ==================================================================
    //  Menue
    // ==================================================================

    [MenuItem("Tools/Skilltree/Baum aus Textdatei bauen...")]
    public static void ImportMenu()
    {
        string path = EditorUtility.OpenFilePanel("Skilltree-Text waehlen", Application.dataPath, "txt");
        if (string.IsNullOrEmpty(path)) return;

        SkillTreeAsset asset = ImportFile(path);

        if (asset != null)
        {
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }

    [MenuItem("Tools/Skilltree/Gewaehlten Baum als Text speichern...")]
    public static void ExportMenu()
    {
        var asset = Selection.activeObject as SkillTreeAsset;

        if (asset == null)
        {
            EditorUtility.DisplayDialog("Skilltree",
                "Erst im Projektfenster ein Baum-Asset anklicken.", "Gut");
            return;
        }

        string path = EditorUtility.SaveFilePanel("Skilltree-Text speichern",
                                                  Application.dataPath, asset.treeId, "txt");
        if (string.IsNullOrEmpty(path)) return;

        File.WriteAllText(path, Export(asset), Encoding.UTF8);
        Debug.Log($"[Skills] '{asset.treeId}' geschrieben nach {path}");
    }

    [MenuItem("Tools/Skilltree/Ordner im Projekt zeigen")]
    public static void PingFolder()
    {
        Object folder = AssetDatabase.LoadAssetAtPath<Object>(AssetFolder);

        if (folder == null)
        {
            EditorUtility.DisplayDialog("Skilltree",
                $"Den Ordner {AssetFolder} gibt es noch nicht. Er entsteht, sobald der " +
                "erste Baum angelegt wird.", "Gut");
            return;
        }

        Selection.activeObject = folder;
        EditorGUIUtility.PingObject(folder);
    }

    // ==================================================================
    //  Einlesen
    // ==================================================================

    /// <summary>Liest eine Textdatei und legt das Asset an bzw. ueberschreibt es.</summary>
    public static SkillTreeAsset ImportFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError($"[Skills] Datei nicht gefunden: {filePath}");
            return null;
        }

        return Import(File.ReadAllText(filePath, Encoding.UTF8));
    }

    /// <summary>
    /// Baut aus dem Text ein Asset unter <see cref="AssetFolder"/>. Gibt es dort
    /// schon einen Baum mit derselben treeId, wird DER ueberschrieben - so bleibt
    /// die Zuordnung im Spielstand erhalten.
    /// </summary>
    public static SkillTreeAsset Import(string text)
    {
        var errors = new List<string>();
        Parsed parsed = Parse(text, errors);

        foreach (string e in errors) Debug.LogError("[Skills] " + e);

        if (parsed == null || string.IsNullOrWhiteSpace(parsed.TreeId))
        {
            Debug.LogError("[Skills] Kein 'tree id=...' im Text gefunden - nichts angelegt.");
            return null;
        }

        EnsureFolder();

        SkillTreeAsset asset = FindByTreeId(parsed.TreeId);
        bool isNew = asset == null;

        if (isNew)
        {
            asset = ScriptableObject.CreateInstance<SkillTreeAsset>();
            AssetDatabase.CreateAsset(asset, $"{AssetFolder}/{parsed.TreeId}.asset");
        }
        else
        {
            Undo.RecordObject(asset, "Skilltree aus Text");
        }

        asset.treeId         = parsed.TreeId;
        asset.displayName    = parsed.DisplayName;
        asset.characterIndex = parsed.CharacterIndex;
        asset.categories     = parsed.Categories;
        asset.nodes          = parsed.Nodes;
        asset.EnsureCategories();

        // Fehlt im Text der Startknoten, wird er hier ergaenzt und alles, was
        // ohne req= dasteht, haengt sich daran. Man muss ihn also nicht tippen.
        asset.EnsureStartNodes();

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        SkillTrees.Reload();

        Debug.Log($"[Skills] '{asset.treeId}' {(isNew ? "angelegt" : "aktualisiert")} - " +
                  $"{asset.nodes.Count} Knoten.");

        return asset;
    }

    class Parsed
    {
        public string TreeId = "";
        public string DisplayName = "Skilltree";
        public int CharacterIndex;
        public List<SkillCategoryData> Categories = new List<SkillCategoryData>();
        public List<SkillNodeData> Nodes = new List<SkillNodeData>();
    }

    static Parsed Parse(string text, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var result = new Parsed();

        foreach (string raw in JoinContinuations(text))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;

            List<string> tokens = Tokenize(line);
            if (tokens.Count == 0) continue;

            string head = tokens[0].ToLowerInvariant();
            tokens.RemoveAt(0);

            switch (head)
            {
                case "tree":     ParseTree(tokens, result, errors); break;
                case "category": ParseCategory(tokens, result, errors); break;
                case "node":     ParseNode(tokens, result, errors); break;

                default:
                    errors.Add($"Unbekannte Zeile: '{line}' (erwartet tree, category oder node).");
                    break;
            }
        }

        return result;
    }

    /// <summary>
    /// Haengt eingerueckte Folgezeilen an die Zeile davor - so darf ein Knoten
    /// ueber mehrere Zeilen gehen, ohne dass man ein Fortsetzungszeichen braucht.
    /// </summary>
    static IEnumerable<string> JoinContinuations(string text)
    {
        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var current = new StringBuilder();

        foreach (string line in lines)
        {
            bool isContinuation = line.Length > 0 && char.IsWhiteSpace(line[0]) &&
                                  current.Length > 0 && !line.TrimStart().StartsWith("#");

            if (isContinuation)
            {
                current.Append(' ').Append(line.Trim());
                continue;
            }

            if (current.Length > 0) yield return current.ToString();
            current.Clear();
            current.Append(line);
        }

        if (current.Length > 0) yield return current.ToString();
    }

    /// <summary>Zerlegt an Leerzeichen, laesst aber "..." zusammen.</summary>
    static List<string> Tokenize(string line)
    {
        var tokens = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];

            if (ch == '\\' && i + 1 < line.Length && line[i + 1] == '"')
            {
                sb.Append('"');
                i++;
                continue;
            }

            if (ch == '"') { inQuotes = !inQuotes; continue; }

            if (!inQuotes && char.IsWhiteSpace(ch))
            {
                if (sb.Length > 0) { tokens.Add(sb.ToString()); sb.Clear(); }
                continue;
            }

            sb.Append(ch);
        }

        if (sb.Length > 0) tokens.Add(sb.ToString());
        return tokens;
    }

    static bool Split(string token, out string key, out string value)
    {
        int at = token.IndexOf('=');

        if (at <= 0)
        {
            key = token;
            value = "";
            return false;
        }

        key   = token.Substring(0, at).ToLowerInvariant();
        value = token.Substring(at + 1);
        return true;
    }

    static void ParseTree(List<string> tokens, Parsed result, List<string> errors)
    {
        foreach (string token in tokens)
        {
            if (!Split(token, out string key, out string value)) continue;

            switch (key)
            {
                case "id":        result.TreeId = value; break;
                case "name":      result.DisplayName = value; break;
                case "character": result.CharacterIndex = ParseInt(value, 0, errors); break;

                default: errors.Add($"tree: unbekanntes Feld '{key}'."); break;
            }
        }
    }

    static void ParseCategory(List<string> tokens, Parsed result, List<string> errors)
    {
        if (tokens.Count == 0 || !TryCategory(tokens[0], out SkillCategory category))
        {
            errors.Add("category: erstes Wort muss Kampf, Geist, Wissen oder Glueck sein.");
            return;
        }

        SkillCategoryData data = SkillCategoryData.Default(category);

        for (int i = 1; i < tokens.Count; i++)
        {
            if (!Split(tokens[i], out string key, out string value)) continue;

            switch (key)
            {
                case "name":  data.displayName = value; break;
                case "path":  data.pathLabel = value; break;
                case "desc":  data.description = value; break;
                case "quote": data.quote = value; break;

                case "color":
                    if (ColorUtility.TryParseHtmlString(value.StartsWith("#") ? value : "#" + value,
                                                        out Color c))
                        data.color = c;
                    else
                        errors.Add($"category {category}: '{value}' ist keine Farbe (z.B. #B24141).");
                    break;

                default: errors.Add($"category {category}: unbekanntes Feld '{key}'."); break;
            }
        }

        result.Categories.RemoveAll(x => x.category == category);
        result.Categories.Add(data);
    }

    static void ParseNode(List<string> tokens, Parsed result, List<string> errors)
    {
        var node = new SkillNodeData();
        bool hasId = false;

        foreach (string token in tokens)
        {
            if (!Split(token, out string key, out string value)) continue;

            switch (key)
            {
                case "id":
                    node.id = value;
                    hasId = true;
                    break;

                case "cat":
                case "category":
                    if (TryCategory(value, out SkillCategory cat)) node.category = cat;
                    else errors.Add($"node {node.id}: '{value}' ist keine Kategorie.");
                    break;

                case "lane":
                    if (TryEnum(value, out SkillLane lane)) node.lane = lane;
                    else errors.Add($"node {node.id}: '{value}' ist keine Bahn (Oben/Mitte/Unten).");
                    break;

                case "step":
                    node.step = ParseInt(value, 0, errors);
                    break;

                case "shape":
                    if (TryEnum(value, out SkillShape shape)) node.shape = shape;
                    else errors.Add($"node {node.id}: '{value}' ist keine Form.");
                    break;

                case "price":
                    node.price = ParseInt(value, 10, errors);
                    break;

                case "name":
                    node.displayName = value;
                    break;

                case "desc":
                    node.description = value;
                    break;

                case "stat":
                    AddStat(node, value, errors);
                    break;

                case "grant":
                    node.rewards.Add(new SkillRewardData
                    {
                        kind    = SkillRewardKind.Schalter,
                        grantId = value,
                    });
                    break;

                case "req":
                case "requires":
                    foreach (string part in value.Split(','))
                    {
                        string id = part.Trim();
                        if (id.Length > 0) node.requires.Add(id);
                    }
                    break;

                default:
                    errors.Add($"node {node.id}: unbekanntes Feld '{key}'.");
                    break;
            }
        }

        if (!hasId)
        {
            errors.Add("node: ohne id=... geht es nicht - die Zeile wird verworfen.");
            return;
        }

        result.Nodes.Add(node);
    }

    /// <summary>"IncreaseDamage:0.25" oder nur "IncreaseDamage" (dann der Standardwert).</summary>
    static void AddStat(SkillNodeData node, string value, List<string> errors)
    {
        string name = value;
        float number = float.NaN;

        int colon = value.IndexOf(':');

        if (colon > 0)
        {
            name = value.Substring(0, colon);

            if (!float.TryParse(value.Substring(colon + 1), NumberStyles.Float,
                                CultureInfo.InvariantCulture, out number))
            {
                errors.Add($"node {node.id}: '{value}' - hinter dem Doppelpunkt muss eine Zahl stehen.");
                number = float.NaN;
            }
        }

        if (!TryEnum(name, out SkillType stat))
        {
            errors.Add($"node {node.id}: '{name}' ist kein SkillType.");
            return;
        }

        node.rewards.Add(new SkillRewardData
        {
            kind  = SkillRewardKind.Wert,
            stat  = stat,
            value = float.IsNaN(number) ? SkillDefaults.ValueFor(stat) : number,
        });
    }

    static bool TryCategory(string text, out SkillCategory category)
    {
        // Glueck darf man auch mit Umlaut schreiben.
        string cleaned = text.Replace("ü", "ue").Replace("Ü", "Ue");
        return TryEnum(cleaned, out category);
    }

    static bool TryEnum<T>(string text, out T value) where T : struct
    {
        return System.Enum.TryParse(text, true, out value) &&
               System.Enum.IsDefined(typeof(T), value);
    }

    static int ParseInt(string text, int fallback, List<string> errors)
    {
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) return v;

        errors.Add($"'{text}' ist keine ganze Zahl - es gilt {fallback}.");
        return fallback;
    }

    // ==================================================================
    //  Schreiben
    // ==================================================================

    /// <summary>Schreibt einen Baum in dasselbe Format, das <see cref="Import"/> liest.</summary>
    public static string Export(SkillTreeAsset asset)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Skilltree als Text. Einlesen ueber Tools -> Skilltree -> Baum aus Textdatei bauen.");
        sb.AppendLine("# Das Format steht in SkillTreeTextIO.");
        sb.AppendLine();

        sb.AppendLine($"tree id={asset.treeId} name={Quote(asset.displayName)} " +
                      $"character={asset.characterIndex}");
        sb.AppendLine();

        foreach (SkillCategory category in (SkillCategory[])System.Enum.GetValues(typeof(SkillCategory)))
        {
            SkillCategoryData c = asset.CategoryOf(category);

            sb.AppendLine($"category {category} color=#{ColorUtility.ToHtmlStringRGB(c.color)} " +
                          $"name={Quote(c.displayName)} path={Quote(c.pathLabel)}");
            sb.AppendLine($"         desc={Quote(c.description)} quote={Quote(c.quote)}");
        }

        foreach (SkillCategory category in (SkillCategory[])System.Enum.GetValues(typeof(SkillCategory)))
        {
            List<SkillNodeData> list = asset.NodesOf(category);
            if (list.Count == 0) continue;

            sb.AppendLine();
            sb.AppendLine($"# ---------------------------------------------------- {category}");

            foreach (SkillNodeData n in list) sb.AppendLine(ExportNode(n));
        }

        return sb.ToString();
    }

    static string ExportNode(SkillNodeData n)
    {
        var sb = new StringBuilder();

        sb.Append($"node id={n.id} cat={n.category} lane={n.lane} step={n.step} " +
                  $"shape={n.shape} price={n.price}");

        if (!string.IsNullOrWhiteSpace(n.displayName)) sb.Append($" name={Quote(n.displayName)}");
        if (!string.IsNullOrWhiteSpace(n.description)) sb.Append($" desc={Quote(n.description)}");

        foreach (SkillRewardData r in n.rewards)
        {
            if (r == null) continue;

            sb.Append(r.kind == SkillRewardKind.Schalter
                ? $" grant={r.grantId}"
                : $" stat={r.stat}:{r.value.ToString("0.####", CultureInfo.InvariantCulture)}");
        }

        if (n.requires.Count > 0) sb.Append($" req={string.Join(",", n.requires)}");

        return sb.ToString();
    }

    static string Quote(string text)
    {
        text = text ?? "";
        return "\"" + text.Replace("\"", "\\\"").Replace("\n", " ") + "\"";
    }

    // ==================================================================
    //  Hilfsmittel, die auch der Editor benutzt
    // ==================================================================

    public static void EnsureFolder()
    {
        if (AssetDatabase.IsValidFolder(AssetFolder)) return;

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        AssetDatabase.CreateFolder("Assets/Resources", SkillTrees.ResourceFolder);
    }

    /// <summary>Alle Baum-Assets im Projekt.</summary>
    public static List<SkillTreeAsset> LoadAll()
    {
        var list = new List<SkillTreeAsset>();

        foreach (string guid in AssetDatabase.FindAssets("t:SkillTreeAsset"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<SkillTreeAsset>(
                AssetDatabase.GUIDToAssetPath(guid));

            if (asset != null) list.Add(asset);
        }

        list.Sort((a, b) =>
        {
            int byIndex = a.characterIndex.CompareTo(b.characterIndex);
            return byIndex != 0 ? byIndex : string.CompareOrdinal(a.treeId, b.treeId);
        });

        return list;
    }

    public static SkillTreeAsset FindByTreeId(string treeId)
    {
        foreach (SkillTreeAsset a in LoadAll())
        {
            if (a.treeId == treeId) return a;
        }
        return null;
    }

    /// <summary>
    /// Legt einen neuen Baum an. <paramref name="template"/> darf null sein -
    /// dann entsteht je Kategorie nur der Startknoten, an dem weitergebaut wird.
    /// </summary>
    public static SkillTreeAsset Create(string treeId, string displayName, int characterIndex,
                                        SkillTreeAsset template)
    {
        if (string.IsNullOrWhiteSpace(treeId))
        {
            Debug.LogError("[Skills] Ein Baum braucht eine Id.");
            return null;
        }

        if (FindByTreeId(treeId) != null)
        {
            Debug.LogError($"[Skills] Es gibt schon einen Baum mit der Id '{treeId}'.");
            return null;
        }

        EnsureFolder();

        var asset = ScriptableObject.CreateInstance<SkillTreeAsset>();
        asset.treeId         = treeId;
        asset.displayName    = string.IsNullOrWhiteSpace(displayName) ? treeId : displayName;
        asset.characterIndex = characterIndex;
        asset.EnsureCategories();

        if (template != null)
        {
            // Farben und Texte mitnehmen, dann jeden Knoten kopieren. Die Ids
            // bleiben gleich - sie sind nur innerhalb eines Baums eindeutig, und
            // der Spielstand haengt zusaetzlich an der treeId.
            asset.categories.Clear();
            foreach (SkillCategoryData c in template.categories) asset.categories.Add(c.WithDefaults());
            asset.EnsureCategories();

            foreach (SkillNodeData n in template.nodes) asset.nodes.Add(n.Copy());
        }

        // Jede Kategorie faengt mit genau einem Knoten an - auch bei einer Kopie,
        // falls die Vorlage noch von vor dieser Regel stammt.
        asset.EnsureStartNodes();

        AssetDatabase.CreateAsset(asset, $"{AssetFolder}/{treeId}.asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        SkillTrees.Reload();

        return asset;
    }

    /// <summary>Ein frischer Knoten mit einem Beispielwert. Schritt und Vorgaenger setzt der Aufrufer.</summary>
    public static SkillNodeData NewNode(SkillTreeAsset asset, SkillCategory category, SkillLane lane)
    {
        return new SkillNodeData
        {
            id       = asset.NextId(category, lane),
            category = category,
            lane     = lane,
            step     = 0,
            shape    = SkillShape.Kreis,
            price    = SkillDefaults.PriceForStep(0),
            rewards  = new List<SkillRewardData>
            {
                new SkillRewardData
                {
                    kind  = SkillRewardKind.Wert,
                    stat  = SkillType.IncreaseMaxHealth,
                    value = SkillDefaults.ValueFor(SkillType.IncreaseMaxHealth),
                },
            },
        };
    }
}
