using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Kyrillisch fuer Russisch: weder Jersey10 noch ThaleahFat kennen es. Beim
/// Start wird deshalb aus Assets/Resources/Fonts/Galmuri9.ttf (Pixelschrift,
/// SIL Open Font License, siehe Galmuri9_OFL.txt) eine dynamische TMP-Schrift
/// gebaut und hinten an die globalen Ersatzschriften (TMP Settings) gehaengt -
/// direkt hinter Jersey10. Fehlt einem Text ein Zeichen, sucht TMP dort weiter.
///
/// Galmuri9 ist im Verhaeltnis zur Schriftgroesse viel hoeher als Jersey10
/// (Grossbuchstaben 0,9 em statt 0,54 em). <see cref="Scale"/> holt das auf
/// Jersey-Hoehe zurueck - etwas ueber der reinen Grossbuchstaben-Hoehe, damit
/// die Kleinbuchstaben nicht zu klein wirken.
///
/// Die Schrift wird nur zur Laufzeit gebaut und beim Beenden wieder aus der
/// Liste genommen - so landet nie ein Verweis auf sie im TMP-Settings-Asset.
/// </summary>
public static class FallbackFonts
{
    private const string CyrillicPath = "Fonts/Galmuri9";

    /// <summary>Jersey10: H = 750/1400 em, Galmuri9: H = 900/1000 em -> 0,6; leicht aufgerundet.</summary>
    private const float Scale = 0.65f;

    private static TMP_FontAsset cyrillic;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (cyrillic != null) return;

        Font font = Resources.Load<Font>(CyrillicPath);
        if (font == null)
        {
            Debug.LogWarning($"[FallbackFonts] Assets/Resources/{CyrillicPath}.ttf fehlt - kein Kyrillisch.");
            return;
        }

        // Dieselben Werte wie Jersey10 SDF (Punktgroesse 90, Rand 9, SDFAA).
        cyrillic = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                                                 AtlasPopulationMode.Dynamic, true);
        if (cyrillic == null)
        {
            Debug.LogWarning("[FallbackFonts] Galmuri9 konnte nicht als TMP-Schrift angelegt werden.");
            return;
        }

        cyrillic.name = "Galmuri9 (Kyrillisch)";
        FaceInfo face = cyrillic.faceInfo;
        face.scale = Scale;
        cyrillic.faceInfo = face;

        List<TMP_FontAsset> list = TMP_Settings.fallbackFontAssets;
        if (list == null)
        {
            list = new List<TMP_FontAsset>();
            TMP_Settings.fallbackFontAssets = list;
        }
        if (!list.Contains(cyrillic)) list.Add(cyrillic);

        Application.quitting += Uninstall;
    }

    private static void Uninstall()
    {
        Application.quitting -= Uninstall;
        TMP_Settings.fallbackFontAssets?.Remove(cyrillic);
        cyrillic = null;
    }
}
