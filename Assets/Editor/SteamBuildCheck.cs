using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Prueft jeden Build, damit nichts in den Steam-Upload rutscht, was dort
/// nicht hingehoert.
///
/// VOR dem Build:
///   * test_scene (Unsterblich-Knopf, Cheat-HUD) fliegt aus der Szenenliste.
///     In den Build Settings bleibt sie eingetragen, sonst kann sie sich im
///     Editor nicht selbst neu laden (TestSceneBuilder).
///   * Steht in Assets/Resources/Feedback/webhook.txt eine Discord-Webhook-URL,
///     bricht der Build ab. Alles unter Resources laesst sich aus dem Build
///     auslesen - dort darf nur die Adresse des Feedback-Proxys stehen
///     (Tools/feedback-proxy, siehe <see cref="FeedbackReport"/>).
///   * Development Build: nur eine Warnung - so etwas gehoert nicht zu Steam.
///
/// NACH dem Build:
///   * Den Ordner "*_BurstDebugInformation_DoNotShip" loeschen.
///   * Eine steam_appid.txt neben der exe loeschen - auf Steam kommt die
///     AppID vom Client, die Datei ist nur zum Testen im Editor.
/// </summary>
[InitializeOnLoad]
public class SteamBuildCheck : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    private const string WebhookFile = "Assets/Resources/" + FeedbackReport.WebhookResource + ".txt";
    private const string TestScene = "Assets/Scenes/test_scene.unity";

    static SteamBuildCheck()
    {
        BuildPlayerWindow.RegisterBuildPlayerHandler(options =>
        {
            options.scenes = System.Array.FindAll(options.scenes, s => s != TestScene);
            BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(options);
        });
    }

    // Spaet laufen, damit Burst seinen Ordner schon geschrieben hat.
    public int callbackOrder => 10000;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (File.Exists(WebhookFile) &&
            File.ReadAllText(WebhookFile).Trim().StartsWith(FeedbackReport.DiscordPrefix))
        {
            throw new BuildFailedException(
                "[SteamBuildCheck] In " + WebhookFile + " steht eine Discord-Webhook-URL. " +
                "Die waere im Build fuer jeden lesbar. Stattdessen die Adresse des " +
                "Feedback-Proxys eintragen (Tools/feedback-proxy/README.md) oder die Datei entfernen.");
        }

        if ((report.summary.options & BuildOptions.Development) != 0)
        {
            Debug.LogWarning("[SteamBuildCheck] Development Build - nicht auf Steam hochladen.");
        }
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        BuildTarget target = report.summary.platform;
        if (target != BuildTarget.StandaloneWindows && target != BuildTarget.StandaloneWindows64 &&
            target != BuildTarget.StandaloneLinux64 && target != BuildTarget.StandaloneOSX)
        {
            return;
        }

        string exe = report.summary.outputPath;
        string dir = Directory.Exists(exe) ? exe : Path.GetDirectoryName(exe);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

        foreach (string folder in Directory.GetDirectories(dir, "*_DoNotShip"))
        {
            Directory.Delete(folder, true);
            Debug.Log("[SteamBuildCheck] Entfernt: " + folder);
        }

        string appId = Path.Combine(dir, "steam_appid.txt");
        if (File.Exists(appId))
        {
            File.Delete(appId);
            Debug.Log("[SteamBuildCheck] Entfernt: " + appId);
        }
    }
}
