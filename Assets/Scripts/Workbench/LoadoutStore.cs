using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Der Spielstand-Teil der Werkbank: JE CHARAKTER zwei Listen von Ids und die
/// Fahne, ob der Verteiler ueberhaupt in Gebrauch ist. Namen, Symbole und
/// Rezepte stehen im Katalog <see cref="WeaponCatalog"/>.
///
/// Jeder Charakter hat seinen eigenen Verteiler - genau wie seinen eigenen
/// Skilltree (<see cref="SkillTrees.ForCharacter"/>). Ein Wechsel im Hub
/// blaettert nur um; was der andere Charakter zusammengestellt hatte, liegt
/// unberuehrt da, bis man wieder zu ihm wechselt. Welcher gerade gilt, sagt
/// <see cref="Use"/> - der Speicher fragt das nirgends selbst ab.
///
/// Dieselben Regeln wie bei <see cref="UnlockStore"/> und <see cref="ShopStore"/>:
///   * Eine Id in der Datei, die der Katalog nicht kennt, bleibt unangetastet
///     liegen. Kommt die Waffe zurueck, ist sie noch im Verteiler.
///   * Die Reihenfolge in der Datei ist die Reihenfolge, in der der Spieler
///     gewaehlt hat - danach fuellen sich die Slots von links.
/// </summary>
public class LoadoutStore
{
    public const int CurrentVersion = 2;
    private const string FileName = "loadout.json";

    /// <summary>Der Verteiler eines Charakters.</summary>
    [Serializable]
    private class Entry
    {
        public int character;
        public bool active;
        public List<string> weapons = new List<string>();
        public List<string> buffs = new List<string>();
    }

    [Serializable]
    private class SaveFile
    {
        public int version = CurrentVersion;
        public List<Entry> characters = new List<Entry>();
    }

    // ---- Altes Format (v1): ein einziger Verteiler, fuer alle Charaktere
    //      derselbe. Er wird dem Charakter zugeschlagen, der beim ersten
    //      Laden nach der Umstellung gewaehlt ist - siehe orphan.
    [Serializable]
    private class LegacyFile
    {
        public bool active;
        public List<string> weapons;
        public List<string> buffs;
    }

    private readonly string savePath;
    private readonly List<Entry> entries = new List<Entry>();

    /// <summary>
    /// Der Verteiler aus einem v1-Spielstand, solange ihn noch kein Charakter
    /// geerbt hat. Der erste <see cref="Use"/> nach dem Laden nimmt ihn - wer
    /// den Speicher benutzt, sagt also besser gleich, um wen es geht.
    /// </summary>
    private Entry orphan;

    private Entry current;

    /// <summary>Wenn true, wird nichts auf die Platte geschrieben (Test-Szene).</summary>
    public bool ReadOnly { get; set; }

    /// <summary>
    /// True, wenn beim Laden ein altes Format uebernommen wurde. Dann lohnt es
    /// sich, einmal zu speichern, damit die Datei im neuen Format dasteht.
    /// </summary>
    public bool Migrated { get; private set; }

    public LoadoutStore(string directory = null)
    {
        savePath = Path.Combine(directory ?? Application.persistentDataPath, FileName);
    }

    public string SavePath => savePath;

    // ==================================================================
    //  Der gewaehlte Charakter
    // ==================================================================

    /// <summary>
    /// Stellt auf den Verteiler dieses Charakters um. Hat er noch keinen,
    /// bekommt er einen leeren. Nichts geht dabei verloren: der bisherige
    /// bleibt in der Liste und ist beim naechsten Umstellen wieder da.
    /// </summary>
    public void Use(int character) => current = Of(character);

    /// <summary>Der Charakter, dessen Verteiler gerade unter Weapons/Buffs liegt.</summary>
    public int Character => Current.character;

    private Entry Current => current ??= Of(0);

    private Entry Of(int character)
    {
        foreach (Entry e in entries)
        {
            if (e.character == character) return e;
        }

        // Der v1-Verteiler gehoert dem ersten Charakter, der danach fragt -
        // das ist der, mit dem der Spieler zuletzt unterwegs war.
        Entry made = orphan ?? new Entry();
        orphan = null;

        made.character = character;
        entries.Add(made);
        return made;
    }

    // ==================================================================
    //  Inhalt des aktuellen Charakters
    // ==================================================================

    /// <summary>
    /// False, solange der Spieler nie "Build uebernehmen" gedrueckt hat. Dann
    /// zieht das Spiel wie bisher aus allem, was freigeschaltet ist - eine
    /// unbenutzte Werkbank darf niemandem den Lauf beschneiden.
    /// </summary>
    public bool Active
    {
        get => Current.active;
        set => Current.active = value;
    }

    public List<string> Weapons => Current.weapons;
    public List<string> Buffs => Current.buffs;

    public void Set(PoolKind kind, IEnumerable<string> ids)
    {
        List<string> target = kind == PoolKind.Buff ? Buffs : Weapons;
        target.Clear();
        Fill(target, ids);
    }

    /// <summary>Haengt an, was nicht leer und noch nicht drin ist.</summary>
    private static void Fill(List<string> target, IEnumerable<string> ids)
    {
        if (ids == null) return;

        foreach (string id in ids)
        {
            if (!string.IsNullOrEmpty(id) && !target.Contains(id)) target.Add(id);
        }
    }

    /// <summary>Raeumt die Verteiler ALLER Charaktere.</summary>
    public void ResetAll()
    {
        foreach (Entry e in entries)
        {
            e.weapons.Clear();
            e.buffs.Clear();
            e.active = false;
        }

        orphan = null;
        Save();
    }

    // ==================================================================

    public void Load()
    {
        entries.Clear();
        orphan = null;
        current = null;
        Migrated = false;

        if (!File.Exists(savePath))
        {
            Save();
            return;
        }

        string json = null;
        try
        {
            json = File.ReadAllText(savePath);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Werkbank] {FileName} nicht lesbar: {e.Message}");
        }

        if (string.IsNullOrWhiteSpace(json)) return;

        SaveFile file;
        try
        {
            file = JsonUtility.FromJson<SaveFile>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Werkbank] Spielstand nicht lesbar: {e.Message}");
            Backup(".corrupt");
            Save();
            return;
        }

        if (file == null)
        {
            Backup(".corrupt");
            Save();
            return;
        }

        if (file.version < CurrentVersion)
        {
            ReadLegacy(json);
            return;
        }

        if (file.characters == null) return;

        foreach (Entry e in file.characters)
        {
            // Zwei Eintraege fuer denselben Charakter waeren ein Ratespiel,
            // welcher gilt - der erste gewinnt, der zweite fliegt raus.
            if (e == null || Has(e.character)) continue;

            Entry made = new Entry { character = e.character, active = e.active };
            Fill(made.weapons, e.weapons);
            Fill(made.buffs, e.buffs);
            entries.Add(made);
        }
    }

    private bool Has(int character)
    {
        foreach (Entry e in entries)
        {
            if (e.character == character) return true;
        }
        return false;
    }

    /// <summary>
    /// Uebernimmt einen v1-Spielstand: der eine Verteiler, den es damals gab,
    /// wartet als <see cref="orphan"/> auf den ersten <see cref="Use"/>.
    /// </summary>
    private void ReadLegacy(string json)
    {
        LegacyFile file;
        try
        {
            file = JsonUtility.FromJson<LegacyFile>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Werkbank] Altes Format nicht lesbar: {e.Message}");
            return;
        }

        if (file == null) return;

        Migrated = true;
        orphan = new Entry { active = file.active };
        Fill(orphan.weapons, file.weapons);
        Fill(orphan.buffs, file.buffs);
    }

    public void Save()
    {
        if (ReadOnly) return;

        SaveFile file = new SaveFile
        {
            version = CurrentVersion,
            characters = entries,
        };

        try
        {
            string json = JsonUtility.ToJson(file, true);
            string tmp = savePath + ".tmp";

            File.WriteAllText(tmp, json);
            File.Copy(tmp, savePath, true);
            File.Delete(tmp);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Werkbank] Speichern fehlgeschlagen: {e.Message}");
        }
    }

    private void Backup(string suffix)
    {
        try
        {
            File.Copy(savePath, savePath + suffix, true);
        }
        catch
        {
            // Sicherung ist nett, aber nichts, wofuer das Laden scheitern darf.
        }
    }
}
