using System;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Hält den Glyphen-Cache dynamischer TextMeshPro-Font-Assets aus dem Git-Diff.
///
/// Dynamische Font-Assets (Jersey10 SDF, PixelArtFont) füllen im Editor ihre
/// Glyphen-Tabelle und eine 1024x1024-Atlas-Textur, sobald irgendwo Text gerendert
/// wird. Beides landet als Hex-Blob direkt im .asset - ein Szenen-Speichern reicht,
/// und die Datei steht mit rund 1 MB Diff als "geändert" da, obwohl sich fachlich
/// nichts getan hat. Der Inhalt ist reiner Cache: dank "Clear Dynamic Data on Build"
/// wird er im Build sowieso verworfen und zur Laufzeit neu aufgebaut.
///
/// TextMeshPro räumt selbst auf - aber nur beim sauberen Beenden des Editors
/// (siehe EditorApplication.quitting in TMP_EditorResourceManager). Dieses Tool
/// zieht denselben Aufräumschritt auf jedes Speichern vor, damit der Cache gar nicht
/// erst in einen Commit rutscht.
///
/// Auch von aussen aufrufbar über
/// <c>Unity.exe -batchmode -executeMethod FontCacheTools.ClearAll</c>.
/// </summary>
public static class FontCacheTools
{
    private const string AutoClearMenu = "Tools/Fonts/Automatisch beim Speichern leeren";
    private const string AutoClearPref = "RiftB.Fonts.AutoClearOnSave";

    /// <summary>Beim Speichern automatisch leeren? Liegt pro Rechner in den EditorPrefs, Default an.</summary>
    public static bool AutoClearOnSave
    {
        get => EditorPrefs.GetBool(AutoClearPref, true);
        set => EditorPrefs.SetBool(AutoClearPref, value);
    }

    /// <summary>Leert jedes dynamische Font-Asset im Projekt und schreibt das Ergebnis weg.</summary>
    [MenuItem("Tools/Fonts/Dynamic-Font-Cache leeren", priority = 400)]
    public static void ClearAll()
    {
        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        int cleared = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            // Nur Projekt-Assets - Font-Assets aus Packages sind ohnehin schreibgeschützt.
            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) continue;

            if (!Clear(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path))) continue;

            Debug.Log($"[Fonts] Cache geleert: {path}");
            cleared++;
        }

        if (cleared > 0) AssetDatabase.SaveAssets();

        Debug.Log($"[Fonts] {cleared} von {guids.Length} Font-Assets geleert.");
    }

    /// <summary>
    /// Leert die Font-Assets, die gerade im Speicher liegen - also genau die, die
    /// sich seit dem letzten Aufräumen vollgesogen haben können. Bewusst ohne
    /// AssetDatabase-Suche, damit beim Speichern keine Assets nachgeladen werden.
    /// </summary>
    internal static void ClearLoaded()
    {
        foreach (TMP_FontAsset font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (!EditorUtility.IsPersistent(font)) continue;
            Clear(font);
        }
    }

    /// <summary>Leert ein einzelnes Font-Asset. Liefert true, wenn wirklich etwas zu tun war.</summary>
    private static bool Clear(TMP_FontAsset font)
    {
        if (font == null) return false;

        // Statische Font-Assets tragen ihren Atlas absichtlich im Repo.
        if (font.atlasPopulationMode != AtlasPopulationMode.Dynamic &&
            font.atlasPopulationMode != AtlasPopulationMode.DynamicOS) return false;

        // Leerer Atlas heisst: schon aufgeräumt. Billigster Test, daher zuerst.
        Texture2D atlas = font.atlasTexture;
        if (atlas == null || atlas.width <= 1) return false;

        // Wer "Clear Dynamic Data on Build" abschaltet, will den Cache behalten.
        if (!ClearsDynamicDataOnBuild(font)) return false;

        ClearTables(font);

        EditorUtility.SetDirty(font);
        if (font.atlasTexture != null) EditorUtility.SetDirty(font.atlasTexture);
        return true;
    }

    /// <summary>Liest das interne Flag "Clear Dynamic Data on Build" über die Serialisierung.</summary>
    private static bool ClearsDynamicDataOnBuild(TMP_FontAsset font)
    {
        SerializedProperty flag = new SerializedObject(font).FindProperty("m_ClearDynamicDataOnBuild");
        return flag == null || flag.boolValue;
    }

    private static MethodInfo s_ClearTables;
    private static bool s_ClearTablesResolved;

    /// <summary>
    /// Ruft denselben internen Aufräumschritt auf, den TextMeshPro beim Beenden des
    /// Editors benutzt: Glyphen- und Zeichen-Tabellen samt Atlas leeren, die aus dem
    /// Font importierten OpenType-Tabellen aber stehen lassen. Dadurch entspricht der
    /// Zustand danach exakt dem, was im Repo liegt - der Diff bleibt wirklich leer.
    /// </summary>
    private static void ClearTables(TMP_FontAsset font)
    {
        if (!s_ClearTablesResolved)
        {
            s_ClearTables = typeof(TMP_FontAsset).GetMethod(
                "ClearCharacterAndGlyphTablesInternal",
                BindingFlags.Instance | BindingFlags.NonPublic);
            s_ClearTablesResolved = true;
        }

        if (s_ClearTables != null)
            s_ClearTables.Invoke(font, null);
        else
            font.ClearFontAssetData(true); // Fallback: löscht zusätzlich die OpenType-Tabellen
    }

    [MenuItem(AutoClearMenu, priority = 420)]
    private static void ToggleAutoClear() => AutoClearOnSave = !AutoClearOnSave;

    [MenuItem(AutoClearMenu, true)]
    private static bool ToggleAutoClearValidate()
    {
        Menu.SetChecked(AutoClearMenu, AutoClearOnSave);
        return true;
    }
}

/// <summary>
/// Klinkt sich vor jedem Speichervorgang ein - also auch beim Szenen-Speichern und
/// beim Wechsel in den Play-Mode, den beiden Momenten, in denen der Font-Cache sonst
/// auf die Platte wandert.
/// </summary>
internal class FontCacheSaveProcessor : UnityEditor.AssetModificationProcessor
{
    private static string[] OnWillSaveAssets(string[] paths)
    {
        if (FontCacheTools.AutoClearOnSave) FontCacheTools.ClearLoaded();
        return paths;
    }
}
