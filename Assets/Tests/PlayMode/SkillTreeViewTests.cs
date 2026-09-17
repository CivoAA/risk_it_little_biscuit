using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// Lädt die World Map und prüft, dass der Skilltree wirklich aus dem Katalog
/// gebaut wird - also dass für jeden Knoten ein Knopf und für jede Vorbedingung
/// eine Linie entsteht.
///
/// Der Spielstand wird dabei nicht angefasst: die Systeme laufen im Sandbox-Modus.
/// </summary>
public class SkillTreeViewTests
{
    [OneTimeSetUp]
    public void SandboxAn()
    {
        // Verhindert, dass der Test in die echten Speicherdateien schreibt.
        Shop.SandboxMode = true;
        Skills.SandboxMode = true;
        Unlocks.SandboxMode = true;
        Achievements.SandboxMode = true;
    }

    [UnityTest]
    public IEnumerator SkillTree_baut_sich_aus_dem_Katalog()
    {
        yield return SceneManager.LoadSceneAsync("World Map", LoadSceneMode.Single);
        yield return null;

        GameObject canvas = FindInactiveByName("SkillTreeCanvas");
        Assert.IsNotNull(canvas, "SkillTreeCanvas nicht in der World Map gefunden.");

        SkillTreeView view = canvas.GetComponent<SkillTreeView>();
        Assert.IsNotNull(view,
            "Am SkillTreeCanvas hängt keine SkillTreeView - dann bleibt der Baum leer.");

        // Das Panel ist in der Szene aus; erst beim Öffnen baut sich der Baum.
        canvas.SetActive(true);
        yield return null;
        yield return null;

        int expectedNodes = 0;
        int expectedLines = 0;

        foreach (SkillNodeDef node in SkillTrees.Default.AllNodes())
        {
            expectedNodes++;
            expectedLines += node.Requires.Count;
        }

        int foundNodes = 0;
        int foundLines = 0;

        foreach (Transform t in canvas.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.StartsWith("Skill_")) foundNodes++;
            else if (t.name.StartsWith("Line_")) foundLines++;
        }

        Assert.AreEqual(expectedNodes, foundNodes,
            $"Es sollten {expectedNodes} Skill-Knöpfe entstehen, gefunden wurden {foundNodes}.");
        Assert.AreEqual(expectedLines, foundLines,
            $"Es sollten {expectedLines} Verbindungslinien entstehen, gefunden wurden {foundLines}.");
    }

    [UnityTest]
    public IEnumerator Wurzeln_sind_anklickbar_gesperrte_Knoten_nicht()
    {
        yield return SceneManager.LoadSceneAsync("World Map", LoadSceneMode.Single);
        yield return null;

        GameObject canvas = FindInactiveByName("SkillTreeCanvas");
        Assert.IsNotNull(canvas);

        canvas.SetActive(true);
        yield return null;
        yield return null;

        var buttons = new Dictionary<string, Button>();
        foreach (Button b in canvas.GetComponentsInChildren<Button>(true))
        {
            if (b.name.StartsWith("Skill_")) buttons[b.name] = b;
        }

        Assert.Greater(buttons.Count, 0, "Keine Skill-Knöpfe erzeugt.");

        foreach (SkillNodeDef node in SkillTrees.Default.AllNodes())
        {
            if (!buttons.TryGetValue("Skill_" + node.LocalId, out Button button)) continue;
            if (Skills.IsUnlocked(node)) continue;

            bool shouldBeClickable = Skills.RequirementsMet(node);

            Assert.AreEqual(shouldBeClickable, button.interactable,
                $"'{node.Key}': Vorbedingungen erfüllt = {shouldBeClickable}, " +
                $"Knopf anklickbar = {button.interactable}.");
        }
    }

    private static GameObject FindInactiveByName(string name)
    {
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.name != name) continue;
            if (!go.scene.IsValid()) continue; // Prefab-Vorlagen überspringen
            return go;
        }
        return null;
    }
}
