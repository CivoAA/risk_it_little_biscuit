using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Das Terminal im Hub. Haengt an einem leeren GameObject, die Zone ist ein
/// Rechteck - Groesse und Versatz stehen im Inspector, der Gizmo zeigt sie in
/// der Szene an. [E] in der Zone oeffnet die Konsole.
///
/// Cheat-Codes gibt es auf zwei Wegen:
///   * ohne Code: die Liste unten im Inspector (Code, Antwort, Unlock, Muenzen)
///   * mit Code:  HubConsoleCheats.cs, dort kann ein Befehl beliebig viel tun
/// </summary>
public class HubConsoleTerminal : HubInteractable
{
    /// <summary>Ein Cheat-Code, der ohne Code-Aenderung auskommt.</summary>
    [System.Serializable]
    public class CheatCode
    {
        [Tooltip("Was eingetippt wird. Gross-/Kleinschreibung ist egal, ein Wort ohne Leerzeichen.")]
        public string code = "";

        [Tooltip("Was die Konsole daraufhin ausgibt.")]
        [TextArea(1, 4)]
        public string antwort = "Freigeschaltet.";

        [Tooltip("Optional: Unlock-ID aus Unlocks.cs, die freigeschaltet wird. Leer = keine.")]
        public string unlockId = "";

        [Tooltip("Optional: so viele Muenzen dazu. 0 = keine.")]
        public int muenzen = 0;

        [Tooltip("An: der Code laesst sich pro Spielstand nur einmal einloesen.")]
        public bool nurEinmal = false;

        [Tooltip("An: taucht in 'hilfe' auf. Fuer echte Cheats aus lassen.")]
        public bool inHilfeZeigen = false;

        [System.NonSerialized] public bool schonBenutzt;
    }

    [Header("Konsole")]
    [Tooltip("Leer lassen - dann wird die HubConsoleUI auf diesem Objekt genommen " +
             "oder angelegt.")]
    [SerializeField] private HubConsoleUI console;

    [Header("Cheat-Codes (ohne Code-Aenderung)")]
    [Tooltip("Ein Eintrag = ein Codewort. Fuer alles Kompliziertere: HubConsoleCheats.cs")]
    [SerializeField] private List<CheatCode> codes = new List<CheatCode>();

    void Reset()
    {
        // Sinnvolle Startwerte, wenn die Komponente frisch drankommt
        shape = InteractShape.Rechteck;
        interactSize = new Vector2(3f, 2f);
        promptText = "[E] Terminal";
    }

    protected override void Start()
    {
        base.Start();

        if (console == null) console = GetComponent<HubConsoleUI>();
        if (console == null) console = gameObject.AddComponent<HubConsoleUI>();

        RegisterCodes();
    }

    /// <summary>Haengt die Inspector-Codes als versteckte Befehle in die Tabelle.</summary>
    void RegisterCodes()
    {
        if (codes == null) return;

        HubConsole.EnsureDefaults();

        foreach (CheatCode entry in codes)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.code)) continue;

            string name = entry.code.Trim();

            if (name.Contains(" "))
            {
                Debug.LogWarning($"{this.name}: Cheat-Code '{name}' hat ein Leerzeichen - " +
                                 "das erste Wort waere der Befehl, der Rest ein Argument. " +
                                 "Besser zusammenschreiben.");
                continue;
            }

            CheatCode captured = entry;   // sonst zeigt die Lambda auf die Schleifenvariable
            HubConsole.Add(name, "Cheat", (args, sink) => Redeem(captured, sink),
                           hidden: !entry.inHilfeZeigen);
        }
    }

    static void Redeem(CheatCode entry, IHubConsoleSink sink)
    {
        if (entry.nurEinmal && entry.schonBenutzt)
        {
            sink.PrintError("Den hast du schon eingeloest.");
            return;
        }

        bool etwasPassiert = false;

        if (!string.IsNullOrWhiteSpace(entry.unlockId))
        {
            if (Unlocks.Find(entry.unlockId) == null)
            {
                sink.PrintError($"Unlock '{entry.unlockId}' steht auf keiner Liste.");
            }
            else
            {
                Unlocks.Grant(entry.unlockId);
                etwasPassiert = true;
            }
        }

        if (entry.muenzen != 0)
        {
            Shop.AddCurrency(entry.muenzen);
            etwasPassiert = true;
        }

        if (!string.IsNullOrEmpty(entry.antwort)) sink.Print(entry.antwort);

        // Nur verbrauchen, wenn auch wirklich etwas angekommen ist - sonst
        // waere ein Code bei fehlendem Manager still fuer immer weg.
        if (etwasPassiert || (string.IsNullOrWhiteSpace(entry.unlockId) && entry.muenzen == 0))
            entry.schonBenutzt = true;
    }

    protected override void OnInteract()
    {
        if (console == null)
        {
            Debug.LogWarning($"{name}: keine HubConsoleUI gefunden.");
            return;
        }
        console.Open();
    }
}
