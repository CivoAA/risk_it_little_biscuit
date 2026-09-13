#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Alle Waffen treffen nach demselben Muster:
/// CompareTag("Enemy") auf dem Collider, dann GetComponent&lt;Enemy&gt;() auf
/// demselben GameObject. Sitzt die Enemy-Komponente woanders, gibt es einen
/// Trigger, aber keinen Schaden. Dieser Test macht solche Prefabs sichtbar.
/// </summary>
public class EnemyPrefabStructureTests
{
    [Test]
    public void Jeder_Enemy_Collider_hat_die_Enemy_Komponente_am_selben_GameObject()
    {
        var problems = new List<string>();
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Enemy" });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) continue;

            foreach (Collider2D col in root.GetComponentsInChildren<Collider2D>(true))
            {
                if (!col.CompareTag("Enemy")) continue;
                if (col.GetComponent<Enemy>() != null) continue;

                problems.Add(path + " -> '" + col.gameObject.name + "' ("
                             + col.GetType().Name + ") ist als Enemy getaggt, "
                             + "hat aber keine Enemy-Komponente");
            }

            foreach (Enemy enemy in root.GetComponentsInChildren<Enemy>(true))
            {
                if (enemy.GetComponent<Collider2D>() == null)
                {
                    problems.Add(path + " -> '" + enemy.gameObject.name
                                 + "' hat eine Enemy-Komponente, aber keinen Collider2D");
                }
                else if (!enemy.CompareTag("Enemy"))
                {
                    problems.Add(path + " -> '" + enemy.gameObject.name
                                 + "' hat eine Enemy-Komponente, ist aber als '"
                                 + enemy.tag + "' getaggt");
                }
            }
        }

        Debug.Log("[SCAN] " + guids.Length + " Gegner-Prefabs geprüft, "
                  + problems.Count + " Auffälligkeiten");
        foreach (string p in problems) Debug.Log("[SCAN] " + p);

        Assert.IsEmpty(problems, string.Join("\n", problems));
    }
}
#endif
