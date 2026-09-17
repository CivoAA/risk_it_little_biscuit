using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Der Ausgabekanal, den ein Befehl beschreiben darf - mehr bekommt er nicht in
/// die Hand. Kein Dateisystem, keine Szene, kein Editor. Das ist der ganze Trick
/// hinter der "Scheinkonsole": sie fuehrt nichts aus, sie schlaegt nur in einer
/// Tabelle nach, die wir selbst gefuellt haben.
/// </summary>
public interface IHubConsoleSink
{
    /// <summary>Normale Zeile in den Ausgabebereich schreiben.</summary>
    void Print(string line);
    /// <summary>Zeile in Fehlerfarbe schreiben.</summary>
    void PrintError(string line);
    /// <summary>Ausgabebereich leeren.</summary>
    void Clear();
    /// <summary>Das Modal schliessen.</summary>
    void Close();
}

/// <summary>
/// Befehlstabelle der Hub-Konsole.
///
/// Wichtig: das hier ist bewusst KEINE echte Konsole. Es gibt kein eval, keine
/// Reflection, keinen Zugriff auf Pfade. Es laeuft ausschliesslich, was vorher
/// per <see cref="Add"/> eingetragen wurde - alles andere ist schlicht ein
/// "Unbekannter Befehl". Kaputt machen kann man damit nichts.
///
/// Neue Cheats kommen in HubConsoleCheats.cs (Code) oder in die Liste am
/// HubConsoleTerminal im Inspector (ohne Code).
/// </summary>
public static class HubConsole
{
    /// <summary>Laenge einer Eingabezeile. Auch das Eingabefeld begrenzt hart darauf.</summary>
    public const int MaxInputLength = 64;

    /// <summary>Mehr Argumente nimmt kein Befehl an - der Rest fliegt raus.</summary>
    public const int MaxArguments = 8;

    /// <summary>
    /// Erlaubte Sonderzeichen. Alles andere wird vor der Auswertung entfernt,
    /// damit weder TMP-Tags noch sonstiger Unfug durchrutschen.
    /// </summary>
    const string AllowedExtra = " _-.,:/#!?+*";

    public delegate void Handler(string[] args, IHubConsoleSink sink);

    public sealed class Command
    {
        public string Name;
        public string Usage;         // z.B. "<id>" - wird hinter den Namen gehaengt
        public string Description;
        public bool Hidden;          // Cheat-Codes tauchen nicht in "hilfe" auf
        public Handler Run;
    }

    static readonly Dictionary<string, Command> lookup =
        new Dictionary<string, Command>(StringComparer.OrdinalIgnoreCase);

    // Eigene Liste, damit "hilfe" in Eintragungsreihenfolge ausgibt statt in
    // Dictionary-Reihenfolge - die ist nicht stabil.
    static readonly List<Command> order = new List<Command>();

    static bool defaultsReady;

    // ------------------------------------------------------------ Eintragen

    /// <summary>
    /// Traegt einen Befehl ein. Gibt es den Namen schon, wird er ersetzt -
    /// so ueberleben Szenenwechsel und Inspector-Listen ohne Doppelungen.
    /// </summary>
    public static void Add(string name, string description, Handler run,
                           string usage = null, bool hidden = false)
    {
        if (string.IsNullOrWhiteSpace(name) || run == null) return;

        name = name.Trim();

        var cmd = new Command
        {
            Name = name,
            Usage = usage,
            Description = description,
            Hidden = hidden,
            Run = run,
        };

        if (lookup.TryGetValue(name, out Command old))
        {
            int i = order.IndexOf(old);
            if (i >= 0) order[i] = cmd; else order.Add(cmd);
        }
        else
        {
            order.Add(cmd);
        }

        lookup[name] = cmd;
    }

    /// <summary>Kurzform fuer Befehle, die nur eine Zeile zurueckgeben.</summary>
    public static void Add(string name, string description, Func<string[], string> run,
                           string usage = null, bool hidden = false)
    {
        if (run == null) return;
        Add(name, description, (args, sink) =>
        {
            string answer = run(args);
            if (!string.IsNullOrEmpty(answer)) sink.Print(answer);
        }, usage, hidden);
    }

    /// <summary>Zweitname fuer einen bestehenden Befehl, z.B. "cls" fuer "clear".</summary>
    public static void AddAlias(string alias, string target)
    {
        if (string.IsNullOrWhiteSpace(alias)) return;
        if (!lookup.TryGetValue(target, out Command cmd)) return;
        // Absichtlich nicht in "order": der Zweitname soll die Hilfe nicht aufblaehen.
        lookup[alias.Trim()] = cmd;
    }

    public static bool Exists(string name) =>
        !string.IsNullOrWhiteSpace(name) && lookup.ContainsKey(name.Trim());

    /// <summary>Alle Befehle in Eintragungsreihenfolge - fuer "hilfe".</summary>
    public static IReadOnlyList<Command> Commands
    {
        get { EnsureDefaults(); return order; }
    }

    // ------------------------------------------------------------ Ausfuehren

    /// <summary>
    /// Einmalig die eingebauten Befehle und die Cheats registrieren.
    /// Wird von jeder Eingabe und von der UI beim Oeffnen aufgerufen.
    /// </summary>
    public static void EnsureDefaults()
    {
        if (defaultsReady) return;
        defaultsReady = true;

        RegisterBuiltins();
        HubConsoleCheats.Register();
    }

    /// <summary>
    /// Wirft alles raus, was nicht auf der Positivliste steht. Das laeuft schon
    /// im Eingabefeld mit, hier steht es nochmal - falls eine Zeile auf anderem
    /// Weg reinkommt.
    /// </summary>
    public static string Sanitize(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;

        var sb = new StringBuilder(raw.Length);
        foreach (char c in raw)
        {
            if (IsAllowedChar(c)) sb.Append(c);
            if (sb.Length >= MaxInputLength) break;
        }
        return sb.ToString();
    }

    /// <summary>Einzelnes Zeichen pruefen - dafuer gibt es im Eingabefeld einen Filter.</summary>
    public static bool IsAllowedChar(char c) =>
        char.IsLetterOrDigit(c) || AllowedExtra.IndexOf(c) >= 0;

    /// <summary>
    /// Zeile auswerten. Findet sich kein passender Eintrag, passiert genau
    /// nichts ausser einer Fehlerzeile.
    /// </summary>
    public static void Execute(string rawLine, IHubConsoleSink sink)
    {
        if (sink == null) return;
        EnsureDefaults();

        string line = Sanitize(rawLine).Trim();
        if (line.Length == 0) return;

        string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        string name = parts[0];

        int argCount = Mathf.Min(parts.Length - 1, MaxArguments);
        string[] args = new string[argCount];
        Array.Copy(parts, 1, args, 0, argCount);

        if (!lookup.TryGetValue(name, out Command cmd))
        {
            sink.PrintError("'" + name + "' kennt das Terminal nicht.");
            sink.Print("Tipp: 'hilfe' zeigt, was geht.");
            return;
        }

        // Ein Befehl, der sich verschluckt, darf das Spiel nicht mitreissen.
        try
        {
            cmd.Run(args, sink);
        }
        catch (Exception e)
        {
            sink.PrintError("Das Terminal hat sich verschluckt. Nichts ist passiert.");
            Debug.LogError($"HubConsole: Befehl '{cmd.Name}' hat geworfen - {e}");
        }
    }

    // ------------------------------------------------------- Eingebaute Befehle

    static void RegisterBuiltins()
    {
        Add("hilfe", "zeigt diese Liste", (args, sink) =>
        {
            sink.Print("Bekannte Befehle:");
            foreach (Command c in order)
            {
                if (c.Hidden) continue;
                string left = string.IsNullOrEmpty(c.Usage) ? c.Name : c.Name + " " + c.Usage;
                sink.Print("  " + left.PadRight(20) + c.Description);
            }
            sink.Print("");
            sink.Print("Und ein paar Dinge, die hier nicht stehen.");
        });
        AddAlias("help", "hilfe");
        AddAlias("?", "hilfe");

        Add("clear", "leert den Bildschirm", (args, sink) => sink.Clear());
        AddAlias("cls", "clear");
        AddAlias("leeren", "clear");

        Add("ende", "schliesst das Terminal", (args, sink) => sink.Close());
        AddAlias("exit", "ende");
        AddAlias("quit", "ende");
    }
}
