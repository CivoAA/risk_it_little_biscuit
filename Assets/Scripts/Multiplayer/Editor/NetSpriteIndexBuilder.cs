using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Schreibt Resources/Net/SpriteIndex.txt: fuer jede Sprite-Textur unter
/// einem Resources-Ordner den Ladepfad. Der Koop braucht das, um Sprites zu
/// finden, die ein Mitspieler zeigt, die hier aber noch nicht geladen sind
/// (<see cref="NetSprites"/>). Laeuft vor jedem Build von selbst und ist
/// ausserdem unter Tools -> Koop -> Sprite-Index aufrufbar.
/// </summary>
public class NetSpriteIndexBuilder : IPreprocessBuildWithReport
{
    private const string OutPath = "Assets/Resources/Net/SpriteIndex.txt";

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report) => Build();

    [MenuItem("Tools/Koop/Sprite-Index")]
    public static void Build()
    {
        var lines = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            int at = path.IndexOf("/Resources/");
            if (at < 0) continue;

            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
            if (importer.textureType != TextureImporterType.Sprite) continue;

            string resources = path.Substring(at + "/Resources/".Length);
            resources = Path.ChangeExtension(resources, null).Replace('\\', '/');
            string texture = Path.GetFileNameWithoutExtension(path);
            lines.Add(texture + "|" + resources);
        }
        lines.Sort(System.StringComparer.Ordinal);

        string text = string.Join("\n", lines) + "\n";
        string full = Path.GetFullPath(OutPath);
        if (File.Exists(full) && File.ReadAllText(full) == text) return;

        Directory.CreateDirectory(Path.GetDirectoryName(full));
        File.WriteAllText(full, text, new UTF8Encoding(false));
        AssetDatabase.ImportAsset(OutPath);
        Debug.Log($"[Koop] Sprite-Index mit {lines.Count} Texturen geschrieben.");
    }
}
