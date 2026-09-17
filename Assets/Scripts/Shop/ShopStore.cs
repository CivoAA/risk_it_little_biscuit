using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Der Spielstand-Teil des Shops: Währung, gewählter Charakter und je Eintrag
/// die gekaufte Stufe. Keine Namen, keine Preise, keine Werte - die stehen im
/// Katalog <see cref="Shop"/> und werden mit dem Spiel ausgeliefert.
///
/// Dieselben Regeln wie beim Achievement-Speicher:
///   * Eine Id in der Datei, die der Katalog nicht kennt, bleibt unangetastet
///     liegen. Kommt der Eintrag zurück, ist die Stufe noch da.
///   * Eine Id im Katalog, die in der Datei fehlt, startet auf Stufe 0.
///   * Eine Stufe, die es nicht mehr gibt (Preisliste gekürzt), wird geklemmt.
///
/// Zusätzlich merkt sich jeder Eintrag, wie viel dafür insgesamt bezahlt wurde
/// (<c>spent</c>). Werden Preise später gesenkt oder Stufen gestrichen, bekommt
/// der Spieler beim Laden die Differenz zurück. Werden Preise erhöht, wird nichts
/// nachgefordert - eine bereits gekaufte Stufe bleibt gekauft.
/// </summary>
public class ShopStore
{
    public const int CurrentVersion = 2;
    private const string FileName = "save.json";

    [Serializable]
    private class Entry
    {
        public string id;
        public int level;
        public int spent;
    }

    [Serializable]
    private class SaveFile
    {
        public int version = CurrentVersion;
        public int currency;
        public int skinIndex;
        public List<Entry> items = new List<Entry>();
    }

    // ---- Altes Format (v1). "buttons" trug Index, Stufe und die damaligen
    //      Preise mit sich - daraus lässt sich das Ausgegebene exakt herleiten.
    [Serializable]
    private class LegacyButton
    {
        public string Name;
        public int index;
        public int level;
        public List<int> cost;
    }

    [Serializable]
    private class LegacyFile
    {
        public int currency;
        public int skinIndex;
        public List<LegacyButton> buttons;
    }

    /// <summary>
    /// Reihenfolge der alten Zahlen-Indizes. Position = alter Index, Wert = neue Id.
    /// Wird nur beim einmaligen Übernehmen eines v1-Spielstands gebraucht.
    /// </summary>
    private static readonly string[] LegacyIndexToId =
    {
        "currency_gain", "rerolls", "banish", "start_xp", "shrink_speed",
        "buff_slot", "weapon_slot", "evo_slot",
        "boba_gun", "shurikookie", "spikefork", "deathstrike",
        "celestial_star", "blade_swarm", "candy_bomb", "time_laser",
        "buff_xp_gain", "buff_currency", "buff_life_steal", "buff_luck",
        "buff_extra_shot", "buff_aoe_range", "buff_damage", "buff_crit_chance",
        "buff_crit_damage", "companion",
    };

    private readonly string savePath;
    private readonly Dictionary<string, Entry> byId = new Dictionary<string, Entry>();
    private readonly List<Entry> order = new List<Entry>();

    private bool dirty;

    public int Currency { get; set; }
    public int SkinIndex { get; set; }

    /// <summary>Wenn true, wird nichts auf die Platte geschrieben (Test-Szene).</summary>
    public bool ReadOnly { get; set; }

    public ShopStore(string directory = null)
    {
        savePath = Path.Combine(directory ?? Application.persistentDataPath, FileName);
    }

    public string SavePath => savePath;

    // ==================================================================
    //  Zugriff
    // ==================================================================

    public int LevelOf(string id) => Get(id, false)?.level ?? 0;

    public void SetLevel(string id, int level)
    {
        Entry e = Get(id, true);
        if (e.level == level) return;
        e.level = level;
        dirty = true;
    }

    public void AddSpent(string id, int amount)
    {
        Entry e = Get(id, true);
        e.spent += amount;
        dirty = true;
    }

    private Entry Get(string id, bool create)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (byId.TryGetValue(id, out Entry e)) return e;
        if (!create) return null;

        e = new Entry { id = id, level = 0, spent = 0 };
        byId.Add(id, e);
        order.Add(e);
        dirty = true;
        return e;
    }

    /// <summary>
    /// Setzt alle bekannten Einträge auf Stufe 0 und erstattet das Ausgegebene.
    /// Einträge, die der Katalog nicht kennt, bleiben liegen.
    /// </summary>
    public int RefundEverything()
    {
        int refund = 0;

        foreach (ShopItemDef def in Shop.All)
        {
            Entry e = Get(def.Id, false);
            if (e == null) continue;

            refund += e.spent;
            e.spent = 0;
            e.level = 0;
        }

        Currency += refund;
        dirty = true;
        return refund;
    }

    // ==================================================================
    //  Laden
    // ==================================================================

    public void Load()
    {
        byId.Clear();
        order.Clear();
        Currency = 0;
        SkinIndex = 0;
        dirty = false;

        if (File.Exists(savePath))
        {
            string json = null;
            try
            {
                json = File.ReadAllText(savePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Shop] {FileName} nicht lesbar: {e.Message}");
            }

            if (!string.IsNullOrWhiteSpace(json) && !TryReadCurrent(json) && !TryReadLegacy(json))
            {
                Backup(".corrupt");
                Debug.LogWarning($"[Shop] {FileName} war unlesbar und wurde als .corrupt gesichert.");
            }
        }

        SyncWithCatalog();

        if (dirty) Save();
    }

    private bool TryReadCurrent(string json)
    {
        if (json.IndexOf("\"version\"", StringComparison.Ordinal) < 0) return false;

        SaveFile file;
        try
        {
            file = JsonUtility.FromJson<SaveFile>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Shop] Spielstand nicht lesbar: {e.Message}");
            return false;
        }

        if (file == null) return false;

        Currency = file.currency;
        SkinIndex = file.skinIndex;

        if (file.items == null) return true;

        foreach (Entry e in file.items)
        {
            if (e == null || string.IsNullOrEmpty(e.id) || byId.ContainsKey(e.id)) continue;
            byId.Add(e.id, e);
            order.Add(e);
        }

        return true;
    }

    private bool TryReadLegacy(string json)
    {
        LegacyFile file;
        try
        {
            file = JsonUtility.FromJson<LegacyFile>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Shop] Altes Format nicht lesbar: {e.Message}");
            return false;
        }

        if (file?.buttons == null) return false;

        Currency = file.currency;
        SkinIndex = file.skinIndex;

        foreach (LegacyButton old in file.buttons)
        {
            if (old == null) continue;
            if (old.index < 0 || old.index >= LegacyIndexToId.Length) continue;

            string id = LegacyIndexToId[old.index];
            if (byId.ContainsKey(id)) continue;

            // Ausgegebenes aus den damals gespeicherten Preisen herleiten.
            int spent = 0;
            int level = old.level;

            if (old.cost != null)
            {
                level = Mathf.Clamp(level, 0, old.cost.Count);
                for (int i = 0; i < level; i++) spent += old.cost[i];
            }

            Entry e = new Entry { id = id, level = level, spent = spent };
            byId.Add(id, e);
            order.Add(e);
        }

        Backup(".v1.bak");
        dirty = true;
        Debug.Log($"[Shop] Alten Spielstand übernommen ({order.Count} Einträge), Sicherung als {FileName}.v1.bak.");
        return true;
    }

    /// <summary>
    /// Legt fehlende Einträge an, klemmt zu hohe Stufen und erstattet, was durch
    /// gesenkte Preise oder gestrichene Stufen zu viel bezahlt wurde.
    /// </summary>
    private void SyncWithCatalog()
    {
        int refund = 0;

        foreach (ShopItemDef def in Shop.All)
        {
            Entry e = Get(def.Id, true);

            int clamped = Mathf.Clamp(e.level, 0, def.MaxLevel);
            if (clamped != e.level)
            {
                e.level = clamped;
                dirty = true;
            }

            int expected = def.TotalCostUpTo(e.level);

            if (e.spent > expected)
            {
                refund += e.spent - expected;
                e.spent = expected;
                dirty = true;
            }
            else if (e.spent < expected)
            {
                // Preise wurden erhöht - nichts nachfordern, nur den Buchwert
                // nachziehen, damit ein späteres Zurücksetzen nicht zu viel erstattet.
                e.spent = expected;
                dirty = true;
            }
        }

        if (refund > 0)
        {
            Currency += refund;
            Debug.Log($"[Shop] Preise haben sich geändert - {refund} erstattet.");
        }
    }

    // ==================================================================
    //  Speichern
    // ==================================================================

    public void Flush()
    {
        dirty = true;
        Save();
    }

    public void Save()
    {
        if (ReadOnly)
        {
            dirty = false;
            return;
        }

        SaveFile file = new SaveFile
        {
            version = CurrentVersion,
            currency = Currency,
            skinIndex = SkinIndex,
            items = order,
        };

        try
        {
            string json = JsonUtility.ToJson(file, true);
            string tmp = savePath + ".tmp";

            File.WriteAllText(tmp, json);
            File.Copy(tmp, savePath, true);
            File.Delete(tmp);

            // Erst nach dem erfolgreichen Schreiben abhaken - sonst ist eine
            // gescheiterte Schreiboperation still verloren.
            dirty = false;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Shop] Speichern fehlgeschlagen: {e.Message}");
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
            // Sicherung ist nett, aber nichts, wofür das Laden scheitern darf.
        }
    }
}
