using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sprites und Materialien ueber das Netz: statt Bildern gehen Nummern raus.
///
/// Der Absender vergibt jedem Sprite, das er zeigen will, eine Nummer und
/// schickt jedem Empfaenger einmal (zuverlaessig) Textur- und Sprite-Namen
/// dazu (<see cref="NetMsg.SpriteDef"/>). Der Empfaenger sucht das Sprite
/// unter allem, was bei ihm geladen ist - beide haben dasselbe Spiel, also
/// dieselben Bilder. Ist es noch nicht geladen (ein Boss, den man selbst nie
/// gesehen hat), hilft der Index aus dem Build (<see cref="NetSpriteIndex"/>):
/// er kennt fuer jede Textur unter Resources den Ladepfad.
///
/// Was zur Laufzeit aus dem Nichts entsteht (Sprite.Create ohne Namen), kann
/// so nicht gefunden werden und bleibt beim Empfaenger unsichtbar.
/// </summary>
public sealed class NetSprites
{
    private readonly NetSession session;

    // Absender
    private readonly Dictionary<Sprite, int> spriteIds = new Dictionary<Sprite, int>();
    private readonly Dictionary<int, Sprite> spritesById = new Dictionary<int, Sprite>();
    private readonly Dictionary<Material, int> materialIds = new Dictionary<Material, int>();
    private readonly HashSet<int>[] sentSprites = new HashSet<int>[NetSession.MaxPlayers];
    private readonly HashSet<int>[] sentMaterials = new HashSet<int>[NetSession.MaxPlayers];
    private int nextSprite = 1, nextMaterial = 1;

    // Empfaenger, je Absender
    private readonly Dictionary<int, Sprite>[] sprites = new Dictionary<int, Sprite>[NetSession.MaxPlayers];
    private readonly Dictionary<int, Material>[] materials = new Dictionary<int, Material>[NetSession.MaxPlayers];

    // Alles Geladene nach "Textur|Sprite" bzw. Name.
    private static readonly Dictionary<string, Sprite> loaded = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, Material> loadedMaterials = new Dictionary<string, Material>();
    private static float indexedAt = -999f, materialsIndexedAt = -999f;
    private static readonly HashSet<string> missingLogged = new HashSet<string>();

    public NetSprites(NetSession session)
    {
        this.session = session;
        for (int i = 0; i < NetSession.MaxPlayers; i++)
        {
            sentSprites[i] = new HashSet<int>();
            sentMaterials[i] = new HashSet<int>();
            sprites[i] = new Dictionary<int, Sprite>();
            materials[i] = new Dictionary<int, Material>();
        }
        session.On(NetMsg.SpriteDef, OnSpriteDef);
        session.On(NetMsg.MaterialDef, OnMaterialDef);
        session.On(NetMsg.SpriteRequest, OnSpriteRequest);
        session.On(NetMsg.SpriteBlob, OnSpriteBlob);
    }

    public void Dispose()
    {
        session.Off(NetMsg.SpriteDef);
        session.Off(NetMsg.MaterialDef);
        session.Off(NetMsg.SpriteRequest);
        session.Off(NetMsg.SpriteBlob);
    }

    public void ForgetPeer(int slot)
    {
        if (slot < 0 || slot >= NetSession.MaxPlayers) return;
        sentSprites[slot].Clear();
        sentMaterials[slot].Clear();
        sprites[slot].Clear();
        materials[slot].Clear();
    }

    // ==================================================================
    //  Absender
    // ==================================================================

    /// <summary>Nummer eines Sprites (0 = keins). Danach <see cref="Ensure"/> fuer den Empfaenger.</summary>
    public int IdOf(Sprite sprite)
    {
        if (sprite == null) return 0;
        if (!spriteIds.TryGetValue(sprite, out int id))
        {
            id = nextSprite++;
            spriteIds[sprite] = id;
            spritesById[id] = sprite;
        }
        return id;
    }

    public int IdOf(Material material)
    {
        if (material == null) return 0;
        if (!materialIds.TryGetValue(material, out int id))
        {
            id = nextMaterial++;
            materialIds[material] = id;
        }
        return id;
    }

    /// <summary>
    /// Sorgt dafuer, dass <paramref name="slot"/> die Nummer kennt. Muss VOR der
    /// Nachricht laufen, die sie benutzt - beide gehen zuverlaessig und kommen
    /// darum in dieser Reihenfolge an. Schreibt eine eigene Nachricht, also
    /// nicht mitten in einer anderen aufrufen.
    /// </summary>
    public void Ensure(int slot, Sprite sprite, int id)
    {
        if (id == 0 || sprite == null || !sentSprites[slot].Add(id)) return;
        NetWriter w = session.Begin(NetMsg.SpriteDef);
        w.VarUInt((uint)id);
        w.String(sprite.texture != null ? sprite.texture.name : "");
        w.String(sprite.name);
        session.SendTo(slot);
    }

    public void Ensure(int slot, Material material, int id)
    {
        if (id == 0 || material == null || !sentMaterials[slot].Add(id)) return;
        NetWriter w = session.Begin(NetMsg.MaterialDef);
        w.VarUInt((uint)id);
        w.String(StripInstance(material.name));
        session.SendTo(slot);
    }

    /// <summary>Hat der Empfaenger die Nummer schon? (Sonst erst <see cref="Ensure"/>.)</summary>
    public bool Known(int slot, int spriteId) => spriteId == 0 || sentSprites[slot].Contains(spriteId);
    public bool KnownMaterial(int slot, int materialId) => materialId == 0 || sentMaterials[slot].Contains(materialId);

    private static string StripInstance(string name) =>
        name != null && name.EndsWith(" (Instance)") ? name.Substring(0, name.Length - 11) : name ?? "";

    // ==================================================================
    //  Empfaenger
    // ==================================================================

    public Sprite Sprite(int from, int id)
    {
        if (id == 0 || from < 0 || from >= NetSession.MaxPlayers) return null;
        return sprites[from].TryGetValue(id, out Sprite s) ? s : null;
    }

    public Material Material(int from, int id)
    {
        if (id == 0 || from < 0 || from >= NetSession.MaxPlayers) return null;
        return materials[from].TryGetValue(id, out Material m) ? m : null;
    }

    private void OnSpriteDef(int from, NetReader r)
    {
        int id = (int)r.VarUInt();
        string texture = r.String();
        string name = r.String();
        if (from < 0 || from >= NetSession.MaxPlayers) return;
        Sprite found = Find(texture, name);
        sprites[from][id] = found;

        // Nicht da: wohl zur Laufzeit gebaut (Sprite.Create) - dann die Pixel holen.
        if (found == null)
        {
            session.Begin(NetMsg.SpriteRequest).VarUInt((uint)id);
            session.SendTo(from);
        }
    }

    // ---------- Laufzeit-Sprites ----------

    /// <summary>Groesser schicken wir keine Textur ueber das Netz.</summary>
    private const int MaxBlobPixels = 512 * 512;

    private void OnSpriteRequest(int from, NetReader r)
    {
        int id = (int)r.VarUInt();
        if (!spritesById.TryGetValue(id, out Sprite s) || s == null || s.texture == null) return;

        Texture2D tex = s.texture;
        if (!tex.isReadable || tex.width * tex.height > MaxBlobPixels) return;

        byte[] png;
        try { png = tex.EncodeToPNG(); }
        catch (System.Exception) { return; }
        if (png == null) return;

        NetWriter w = session.Begin(NetMsg.SpriteBlob);
        w.VarUInt((uint)id);
        Rect rect = s.rect;
        w.Float(rect.x); w.Float(rect.y); w.Float(rect.width); w.Float(rect.height);
        w.Float(s.pivot.x / Mathf.Max(1f, rect.width));
        w.Float(s.pivot.y / Mathf.Max(1f, rect.height));
        w.Float(s.pixelsPerUnit);
        w.Byte((byte)tex.filterMode);
        w.VarUInt((uint)png.Length);
        w.Bytes(png, 0, png.Length);
        session.SendTo(from);
    }

    private void OnSpriteBlob(int from, NetReader r)
    {
        int id = (int)r.VarUInt();
        var rect = new Rect(r.Float(), r.Float(), r.Float(), r.Float());
        var pivot = new Vector2(r.Float(), r.Float());
        float ppu = r.Float();
        var filter = (FilterMode)r.Byte();
        int count = (int)r.VarUInt();
        if (r.Failed || count <= 0 || count > r.Remaining || from < 0 || from >= NetSession.MaxPlayers) return;

        byte[] png = new byte[count];
        for (int i = 0; i < count; i++) png[i] = r.Byte();

        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = filter, wrapMode = TextureWrapMode.Clamp };
        if (!tex.LoadImage(png)) return;
        tex.filterMode = filter;
        sprites[from][id] = UnityEngine.Sprite.Create(tex, rect, pivot, ppu);
    }

    private void OnMaterialDef(int from, NetReader r)
    {
        int id = (int)r.VarUInt();
        string name = r.String();
        if (from < 0 || from >= NetSession.MaxPlayers) return;
        materials[from][id] = FindMaterial(name);
    }

    private static string Key(string texture, string sprite) => texture + "|" + sprite;

    public static Sprite Find(string texture, string name)
    {
        string key = Key(texture, name);
        if (loaded.TryGetValue(key, out Sprite s) && s != null) return s;

        // Neu geladen seit dem letzten Mal? Hoechstens einmal pro halbe Sekunde neu einsammeln.
        if (Time.unscaledTime - indexedAt > 0.5f)
        {
            IndexLoaded();
            if (loaded.TryGetValue(key, out s) && s != null) return s;
        }

        // Liegt unter Resources, ist aber noch nicht geladen.
        foreach (string path in NetSpriteIndex.PathsFor(texture))
        {
            foreach (Sprite candidate in Resources.LoadAll<Sprite>(path))
            {
                if (candidate == null) continue;
                string k = Key(candidate.texture != null ? candidate.texture.name : "", candidate.name);
                loaded[k] = candidate;
            }
            if (loaded.TryGetValue(key, out s) && s != null) return s;
        }

        if (missingLogged.Add(key)) Debug.LogWarning($"[NetSprites] Sprite nicht gefunden: {key}");
        return null;
    }

    private static void IndexLoaded()
    {
        indexedAt = Time.unscaledTime;
        foreach (Sprite s in Resources.FindObjectsOfTypeAll<Sprite>())
        {
            if (s == null) continue;
            string k = Key(s.texture != null ? s.texture.name : "", s.name);
            if (!loaded.ContainsKey(k) || loaded[k] == null) loaded[k] = s;
        }
    }

    public static Material FindMaterial(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (loadedMaterials.TryGetValue(name, out Material m) && m != null) return m;

        if (Time.unscaledTime - materialsIndexedAt > 1f)
        {
            materialsIndexedAt = Time.unscaledTime;
            foreach (Material mat in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (mat == null) continue;
                string n = StripInstance(mat.name);
                if (!loadedMaterials.ContainsKey(n) || loadedMaterials[n] == null) loadedMaterials[n] = mat;
            }
            if (loadedMaterials.TryGetValue(name, out m) && m != null) return m;
        }
        return null;
    }
}

/// <summary>
/// Welche Textur unter welchem Resources-Pfad liegt. Steht als
/// Resources/Net/SpriteIndex.txt im Build ("Texturname|Pfad" je Zeile) und
/// wird vom Editor vor jedem Build neu geschrieben (NetSpriteIndexBuilder).
/// Fehlt die Datei, findet der Empfaenger eben nur, was schon geladen ist.
/// </summary>
public static class NetSpriteIndex
{
    public const string ResourcePath = "Net/SpriteIndex";

    private static Dictionary<string, List<string>> paths;
    private static readonly List<string> none = new List<string>();

    public static List<string> PathsFor(string texture)
    {
        if (paths == null) Load();
        return texture != null && paths.TryGetValue(texture, out List<string> list) ? list : none;
    }

    private static void Load()
    {
        paths = new Dictionary<string, List<string>>();
        TextAsset file = Resources.Load<TextAsset>(ResourcePath);
        if (file == null) return;

        foreach (string raw in file.text.Split('\n'))
        {
            string line = raw.Trim();
            int bar = line.IndexOf('|');
            if (bar <= 0) continue;
            string tex = line.Substring(0, bar);
            string path = line.Substring(bar + 1);
            if (!paths.TryGetValue(tex, out List<string> list)) paths[tex] = list = new List<string>();
            if (!list.Contains(path)) list.Add(path);
        }
    }
}
