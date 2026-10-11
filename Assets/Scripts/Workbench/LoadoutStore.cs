using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Der Spielstand-Teil der Werkbank: JE CHARAKTER mehrere gespeicherte Builds
/// (Presets) aus je zwei Listen von Ids, welcher davon zuletzt gewaehlt war,
/// und die Fahne, ob der Verteiler ueberhaupt in Gebrauch ist. Namen, Symbole
/// und Rezepte stehen im Katalog <see cref="WeaponCatalog"/>.
///
/// Jeder Charakter hat seinen eigenen Verteiler - genau wie seinen eigenen
/// Skilltree (<see cref="SkillTrees.ForCharacter"/>). Ein Wechsel im Hub
/// blaettert nur um; was der andere Charakter zusammengestellt hatte, liegt
/// unberuehrt da, bis man wieder zu ihm wechselt. Welcher gerade gilt, sagt
/// <see cref="Use"/> - der Speicher fragt das nirgends selbst ab.
///
/// PRESETS: <see cref="Weapons"/> und <see cref="Buffs"/> meinen immer das
/// gewaehlte Preset (<see cref="PresetIndex"/>). Wie viele Presets der Spieler
/// benutzen darf, entscheidet der Shop - das weiss nur <see cref="Loadout"/>,
/// hier wird nur gespeichert. Presets ueber der gekauften Zahl bleiben liegen
/// (etwa nach einem Shop-Reset) und sind nach dem naechsten Kauf wieder da.
///
/// Dieselben Regeln wie bei <see cref="UnlockStore"/> und <see cref="ShopStore"/>:
///   * Eine Id in der Datei, die der Katalog nicht kennt, bleibt unangetastet
///     liegen. Kommt die Waffe zurueck, ist sie noch im Verteiler.
///   * Die Reihenfolge in der Datei ist die Reihenfolge, in der der Spieler
///     gewaehlt hat - danach fuellen sich die Slots von links.
/// </summary>
public class LoadoutStore
{
    public const int CurrentVersion = 3;
    private const string FileName = "loadout.json";

    /// <summary>Ein gespeicherter Build.</summary>
    [Serializable]
    private class Preset
    {
        public List<string> weapons = new List<string>();
        public List<string> buffs = new List<string>();
    }

    /// <summary>Der Verteiler eines Charakters.</summary>
    [Serializable]
    private class Entry
    {
        public int character;
        public bool active;
        /// <summary>Das zuletzt gewaehlte Preset - das geht mit in den Lauf.</summary>
        public int preset;
        public List<Preset> presets = new List<Preset>();
    }

    [Serializable]
    private class SaveFile
    {
        public int version = CurrentVersion;
        public List<Entry> characters = new List<Entry>();
    }

    /// <summary>Liest nur die Versionsnummer - fehlt sie, ist es eine 0.</summary>
    [Serializable]
    private class VersionProbe
    {
        public int version;
    }

    // ---- Format v2 (bis 11.10.2026): je Charakter genau ein Build. Der wird
    //      beim Laden zu Preset 1.
    [Serializable]
    private class V2Entry
    {
        public int character;
        public bool active;
        public List<string> weapons;
        public List<string> buffs;
    }

    [Serializable]
    private class V2File
    {
        public List<V2Entry> characters;
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
    //  Presets des aktuellen Charakters
    // ==================================================================

    /// <summary>
    /// Das gewaehlte Preset. Setzen legt fehlende Presets leer an - wer auf
    /// Platz 3 springt, findet dort einen leeren Build.
    /// </summary>
    public int PresetIndex
    {
        get => Mathf.Clamp(Current.preset, 0, Mathf.Max(0, Current.presets.Count - 1));
        set
        {
            int index = Mathf.Max(0, value);
            EnsurePresets(index + 1);
            Current.preset = index;
        }
    }

    /// <summary>Wie viele Presets dieser Charakter schon hat - auch ungenutzte.</summary>
    public int PresetCount => Mathf.Max(1, Current.presets.Count);

    /// <summary>Legt leere Presets an, bis es mindestens so viele sind. Entfernt nie welche.</summary>
    public void EnsurePresets(int count)
    {
        List<Preset> list = Current.presets;
        while (list.Count < Mathf.Max(1, count)) list.Add(new Preset());
    }

    private Preset CurrentPreset
    {
        get
        {
            EnsurePresets(1);
            return Current.presets[PresetIndex];
        }
    }

    // ==================================================================
    //  Inhalt des aktuellen Charakters
    // ==================================================================

    /// <summary>
    /// False, solange der Charakter noch nie in einen Lauf gegangen ist. Dann
    /// zieht das Spiel wie bisher aus allem, was freigeschaltet ist - eine
    /// unbenutzte Werkbank darf niemandem den Lauf beschneiden.
    /// </summary>
    public bool Active
    {
        get => Current.active;
        set => Current.active = value;
    }

    public List<string> Weapons => CurrentPreset.weapons;
    public List<string> Buffs => CurrentPreset.buffs;

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

    /// <summary>Raeumt die Verteiler ALLER Charaktere, alle Presets, zurueck auf Preset 1.</summary>
    public void ResetAll()
    {
        foreach (Entry e in entries)
        {
            foreach (Preset p in e.presets)
            {
                p.weapons.Clear();
                p.buffs.Clear();
            }
            e.preset = 0;
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

        VersionProbe probe;
        try
        {
            probe = JsonUtility.FromJson<VersionProbe>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Werkbank] Spielstand nicht lesbar: {e.Message}");
            Backup(".corrupt");
            Save();
            return;
        }

        if (probe == null)
        {
            Backup(".corrupt");
            Save();
            return;
        }

        if (probe.version < 2)
        {
            ReadLegacy(json);
            return;
        }

        if (probe.version == 2)
        {
            ReadV2(json);
            return;
        }

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

        if (file?.characters == null) return;

        foreach (Entry e in file.characters)
        {
            // Zwei Eintraege fuer denselben Charakter waeren ein Ratespiel,
            // welcher gilt - der erste gewinnt, der zweite fliegt raus.
            if (e == null || Has(e.character)) continue;

            Entry made = new Entry { character = e.character, active = e.active, preset = Mathf.Max(0, e.preset) };

            if (e.presets != null)
            {
                foreach (Preset p in e.presets)
                {
                    Preset copy = new Preset();
                    if (p != null)
                    {
                        Fill(copy.weapons, p.weapons);
                        Fill(copy.buffs, p.buffs);
                    }
                    made.presets.Add(copy);
                }
            }

            if (made.presets.Count == 0) made.presets.Add(new Preset());
            made.preset = Mathf.Min(made.preset, made.presets.Count - 1);
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

    /// <summary>v2: je Charakter ein Build - der wird Preset 1.</summary>
    private void ReadV2(string json)
    {
        V2File file;
        try
        {
            file = JsonUtility.FromJson<V2File>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Werkbank] v2-Spielstand nicht lesbar: {e.Message}");
            return;
        }

        Migrated = true;
        if (file?.characters == null) return;

        foreach (V2Entry e in file.characters)
        {
            if (e == null || Has(e.character)) continue;
            entries.Add(Single(e.character, e.active, e.weapons, e.buffs));
        }
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
        orphan = Single(0, file.active, file.weapons, file.buffs);
    }

    /// <summary>Ein Charakter mit genau einem Preset - so sahen alte Spielstaende aus.</summary>
    private static Entry Single(int character, bool active, List<string> weapons, List<string> buffs)
    {
        Preset preset = new Preset();
        Fill(preset.weapons, weapons);
        Fill(preset.buffs, buffs);

        Entry made = new Entry { character = character, active = active };
        made.presets.Add(preset);
        return made;
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
