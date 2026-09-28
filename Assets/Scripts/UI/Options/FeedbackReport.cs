using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

/// <summary>
/// Schickt eine Rueckmeldung aus dem <see cref="FeedbackPanel"/> als
/// Nachricht in einen Discord-Kanal - ueber einen Webhook, ohne Server
/// dazwischen.
///
/// DIE WEBHOOK-URL STEHT NICHT IM CODE. Das Repo ist oeffentlich; wer die URL
/// kennt, kann in den Kanal schreiben. Sie liegt stattdessen in
///
///   Assets/Resources/Feedback/webhook.txt      (eine Zeile, nur die URL)
///
/// und dieser Ordner steht in .gitignore. Wer ohne die Datei baut, bekommt
/// ein Spiel, in dem SENDEN "kein Webhook eingerichtet" meldet - sonst
/// bricht nichts. Achtung: im fertigen Build steckt die URL trotzdem und
/// laesst sich mit etwas Muehe herausziehen. Wird der Kanal zugemuellt,
/// in Discord einen neuen Webhook anlegen und die Datei tauschen.
///
/// Mitgeschickt wird, was man zum Nachstellen braucht: Spielversion, Build-Art,
/// System, Hardware, Aufloesung, Sprache, geladene Szenen, Laufzeit.
/// @everyone & Co. im Text pingen niemanden (allowed_mentions leer).
/// </summary>
public static class FeedbackReport
{
    public enum Kind { Bug, Feedback }

    public const int MaxName = 40;
    public const int MaxMessage = 1000;
    public const int MinMessage = 10;

    /// <summary>Mindestabstand zwischen zwei Sendungen, gegen Doppelklicks und Spam.</summary>
    public const float Cooldown = 30f;

    private const string WebhookResource = "Feedback/webhook";
    private const string WebhookPrefix = "https://discord.com/api/webhooks/";

    private static float lastSent = -999f;

    public static bool CoolingDown => Time.realtimeSinceStartup - lastSent < Cooldown;

    public static string WebhookUrl
    {
        get
        {
            TextAsset file = Resources.Load<TextAsset>(WebhookResource);
            if (file == null) return null;
            string url = file.text.Trim();
            return url.StartsWith(WebhookPrefix) ? url : null;
        }
    }

    /// <summary>
    /// Sendet und meldet sich per <paramref name="done"/> (true = angekommen).
    /// Laeuft mit Echtzeit - im Pausenmenue steht timeScale auf 0.
    /// </summary>
    public static IEnumerator Send(Kind kind, string name, string message, System.Action<bool> done)
    {
        string url = WebhookUrl;
        if (url == null)
        {
            Debug.LogWarning("[Feedback] Kein Webhook - erwartet wird eine Zeile mit der URL in " +
                             "Assets/Resources/" + WebhookResource + ".txt");
            done(false);
            yield break;
        }

        lastSent = Time.realtimeSinceStartup;
        byte[] body = Encoding.UTF8.GetBytes(BuildJson(kind, name, message));

        using (var request = new UnityWebRequest(url + "?wait=true", UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 15;

            yield return request.SendWebRequest();

            bool ok = request.result == UnityWebRequest.Result.Success;
            if (!ok)
            {
                Debug.LogWarning($"[Feedback] Senden fehlgeschlagen: {request.responseCode} {request.error}\n" +
                                 request.downloadHandler?.text);
                // Fehlschlag zaehlt nicht als Sendung - sofort nochmal versuchen ist erlaubt.
                lastSent = -999f;
            }
            done(ok);
        }
    }

    // ==================================================================
    //  Nachricht
    // ==================================================================

    public static string BuildJson(Kind kind, string name, string message)
    {
        name = Clip(string.IsNullOrWhiteSpace(name) ? "anonym" : name.Trim(), MaxName);
        message = Clip(message.Trim(), MaxMessage);

        bool bug = kind == Kind.Bug;
        string title = bug ? "\U0001F41E Bug-Report" : "\U0001F4AC Feedback";
        int color = bug ? 0xd4566c : 0x6fb7e0;

        var sb = new StringBuilder(2048);
        sb.Append('{');
        Prop(sb, "username", "Little Biscuit Feedback").Append(',');
        sb.Append("\"allowed_mentions\":{\"parse\":[]},");
        sb.Append("\"embeds\":[{");
        Prop(sb, "title", title).Append(',');
        Prop(sb, "description", message).Append(',');
        sb.Append("\"color\":").Append(color).Append(',');
        Prop(sb, "timestamp", System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture))
            .Append(',');
        sb.Append("\"fields\":[");

        Field(sb, "Von", name, true).Append(',');
        Field(sb, "Version", $"{Application.version} ({BuildKind()})", true).Append(',');
        Field(sb, "Sprache", Loc.Language, true).Append(',');
        Field(sb, "Szene", Scenes(), true).Append(',');
        Field(sb, "Laufzeit", Runtime(), true).Append(',');
        Field(sb, "Bildschirm", $"{Screen.width}x{Screen.height}, {Screen.fullScreenMode}", true).Append(',');
        Field(sb, "System", SystemInfo.operatingSystem, false).Append(',');
        Field(sb, "Hardware",
              $"{SystemInfo.processorType} ({SystemInfo.processorCount} Kerne)\n" +
              $"{SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsMemorySize} MB, {SystemInfo.graphicsDeviceType})\n" +
              $"{SystemInfo.systemMemorySize} MB RAM", false).Append(',');
        Field(sb, "Unity", $"{Application.unityVersion}, {Application.platform}", true);

        sb.Append("]}]}");
        return sb.ToString();
    }

    private static string BuildKind()
    {
        if (Application.isEditor) return "Editor";
        return Debug.isDebugBuild ? "Development" : "Release";
    }

    private static string Scenes()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (!s.isLoaded) continue;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(s.name);
        }
        return sb.Length > 0 ? sb.ToString() : "-";
    }

    private static string Runtime()
    {
        int t = Mathf.FloorToInt(Time.realtimeSinceStartup);
        return $"{t / 3600}:{t / 60 % 60:00}:{t % 60:00}";
    }

    // ---------- JSON von Hand (JsonUtility kann keine verschachtelten Listen) ----------

    private static StringBuilder Field(StringBuilder sb, string name, string value, bool inline)
    {
        sb.Append('{');
        Prop(sb, "name", name).Append(',');
        Prop(sb, "value", string.IsNullOrEmpty(value) ? "-" : Clip(value, 1024)).Append(',');
        sb.Append("\"inline\":").Append(inline ? "true" : "false");
        return sb.Append('}');
    }

    private static StringBuilder Prop(StringBuilder sb, string key, string value)
    {
        sb.Append('"').Append(key).Append("\":\"");
        foreach (char c in value)
        {
            switch (c)
            {
                case '"':  sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        return sb.Append('"');
    }

    private static string Clip(string s, int max) => s.Length <= max ? s : s.Substring(0, max - 1) + "…";
}
