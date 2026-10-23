using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Steam-Build in einem Schritt: Version hochzaehlen (1.2J -> 1.2K, endet sie
/// auf eine Ziffer oder Z, kommt ein A dran), speichern und nach
/// Builds/Version_&lt;Version&gt;/ bauen. Hochladen macht danach
/// G:\SteamUpload\upload.bat (nimmt den neuesten Ordner).
///
/// Geht auch ohne offenen Editor:
///   Unity.exe -batchmode -quit -projectPath ... -executeMethod SteamBuild.BuildNext
/// </summary>
public static class SteamBuild
{
    private const string TestScene = "Assets/Scenes/test_scene.unity";
    private const string ExeName = "Risk_it_Little_Biscuit.exe";

    [MenuItem("Tools/Steam/Version hochzaehlen + bauen", false, 200)]
    public static void BuildNext()
    {
        string old = PlayerSettings.bundleVersion;
        string next = NextVersion(old);
        PlayerSettings.bundleVersion = next;
        AssetDatabase.SaveAssets();
        Build(old, next, "Version_" + next);
    }

    /// <summary>
    /// Ohne Hochzaehlen, mit eigenem Ordner (z.B. zweiter Build derselben Version):
    ///   -executeMethod SteamBuild.BuildCurrent -buildFolder Version_1.2K1
    /// </summary>
    public static void BuildCurrent()
    {
        string version = PlayerSettings.bundleVersion;
        string folder = "Version_" + version;
        string[] args = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(args, "-buildFolder");
        if (i >= 0 && i + 1 < args.Length) folder = args[i + 1];
        Build(version, version, folder);
    }

    private static void Build(string old, string next, string folder)
    {
        string dir = Path.Combine(Directory.GetCurrentDirectory(), "Builds", folder);
        var options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled && s.path != TestScene)
                .Select(s => s.path)
                .ToArray(),
            locationPathName = Path.Combine(dir, ExeName),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[SteamBuild] {old} -> {next} gebaut: {dir}");
        }
        else
        {
            Debug.LogError($"[SteamBuild] Build {next} fehlgeschlagen ({report.summary.result}).");
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    private static string NextVersion(string version)
    {
        char last = version.Length > 0 ? version[version.Length - 1] : '0';
        if (char.IsLetter(last) && char.ToUpperInvariant(last) != 'Z')
            return version.Substring(0, version.Length - 1) + (char)(last + 1);
        return version + "A";
    }
}
