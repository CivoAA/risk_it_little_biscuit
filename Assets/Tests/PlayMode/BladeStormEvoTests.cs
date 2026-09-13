#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class BladeStormEvoTests : WeaponTestBase
{
    private BladeStormEvo evo;

    private static BladeStormEvoPrefab[] Blades()
    {
        return Object.FindObjectsByType<BladeStormEvoPrefab>(FindObjectsSortMode.None);
    }

    private IEnumerator GrantEvo()
    {
        evo = FindWeapon<BladeStormEvo>("Blade Storm Evo");
        evo.weaponLevel = 0;

        int expected = Mathf.RoundToInt(evo.stats[0].shots);
        yield return WaitUntil(() => Blades().Length >= expected, 5f,
            "Blade Storm Evo hat nicht alle Klingen gespawnt");
    }

    [UnityTest]
    public IEnumerator Spawnt_genau_so_viele_Klingen_wie_shots()
    {
        yield return GrantEvo();

        int expected = Mathf.RoundToInt(evo.stats[0].shots + player.playerShots);
        Assert.AreEqual(expected, Blades().Length);
    }

    /// <summary>
    /// Früher wurde der Bestand gegen einen Zähler geprüft, der nie kleiner
    /// wurde – eine zerstörte Klinge blieb dadurch dauerhaft weg.
    /// </summary>
    [UnityTest]
    public IEnumerator Spawnt_verlorene_Klingen_nach()
    {
        yield return GrantEvo();

        int expected = Blades().Length;
        Object.DestroyImmediate(Blades()[0].gameObject);
        Assert.AreEqual(expected - 1, Blades().Length, "Vorbedingung: eine Klinge ist weg");

        yield return WaitUntil(() => Blades().Length == expected, 3f,
            "Die verlorene Klinge kam nicht zurück");
    }

    /// <summary>
    /// Trifft ein Ziel, alle Klingen kommen zurück und es gibt eine zweite
    /// Salve. Der alte Coroutine-Aufbau konnte hier dauerhaft hängen bleiben,
    /// weil nur gefeuert wird, wenn alle Klingen im Orbit sind.
    /// </summary>
    [UnityTest]
    public IEnumerator Trifft_ein_Ziel_und_feuert_danach_erneut()
    {
        yield return GrantEvo();

        DamageProbe probe = CreateProbe(player.transform.position + new Vector3(3f, 0f, 0f),
                                        new Vector2(1.5f, 1.5f));

        yield return WaitUntil(() => evo.enemiesInRange.Contains(probe), 3f,
            "Der Erfassungsbereich der Waffe hat den Gegner nicht registriert");

        yield return WaitUntil(() => probe.hits > 0, 8f,
            "Die Evo hat das Ziel nicht getroffen");

        int hitsAfterFirstVolley = probe.hits;

        yield return WaitUntil(() => Blades().Length > 0 && AllInOrbit(), 8f,
            "Die Klingen sind nicht alle in den Orbit zurückgekehrt");

        yield return WaitUntil(() => probe.hits > hitsAfterFirstVolley, 8f,
            "Es gab keine zweite Salve – die Waffe hängt");

        Object.Destroy(probe.gameObject);
    }

    /// <summary>
    /// Der Cooldown lief früher während des ganzen Fluges weiter ins Negative,
    /// die nächste Salve startete dadurch ohne jede Pause.
    /// </summary>
    [UnityTest]
    public IEnumerator Haelt_den_Cooldown_zwischen_zwei_Salven_ein()
    {
        yield return GrantEvo();

        DamageProbe probe = CreateProbe(player.transform.position + new Vector3(3f, 0f, 0f),
                                        new Vector2(1.5f, 1.5f));
        yield return WaitUntil(() => evo.enemiesInRange.Contains(probe), 3f,
            "Der Erfassungsbereich der Waffe hat den Gegner nicht registriert");

        var volleyStarts = new List<float>();
        bool wasAllInOrbit = true;
        float deadline = Time.time + 20f;

        while (Time.time < deadline && volleyStarts.Count < 3)
        {
            bool allInOrbit = AllInOrbit();
            if (wasAllInOrbit && !allInOrbit) volleyStarts.Add(Time.time);
            wasAllInOrbit = allInOrbit;
            yield return null;
        }

        Assert.GreaterOrEqual(volleyStarts.Count, 3, "Zu wenige Salven beobachtet");

        float cooldown = evo.stats[0].cooldown;
        for (int i = 1; i < volleyStarts.Count; i++)
        {
            float gap = volleyStarts[i] - volleyStarts[i - 1];
            Assert.GreaterOrEqual(gap, cooldown * 0.9f,
                "Salve " + i + " kam nach nur " + gap.ToString("F2") + "s – der Cooldown von "
                + cooldown + "s wurde nicht eingehalten");
        }

        Object.Destroy(probe.gameObject);
    }

    /// <summary>
    /// Der Rückflug endete früher an der Spielerposition von vor ~0.5s. Bei
    /// schneller Bewegung landete die Klinge im Leeren und sprang danach
    /// sichtbar in ihren Orbit-Slot.
    /// </summary>
    [UnityTest]
    public IEnumerator Klingen_landen_beim_bewegten_Spieler_statt_zu_springen()
    {
        yield return GrantEvo();

        DamageProbe probe = CreateProbe(player.transform.position + new Vector3(4f, 0f, 0f),
                                        new Vector2(1.5f, 1.5f));
        yield return WaitUntil(() => evo.enemiesInRange.Contains(probe), 3f,
            "Der Erfassungsbereich der Waffe hat den Gegner nicht registriert");

        var wasInOrbit = new Dictionary<BladeStormEvoPrefab, bool>();
        var lastPosition = new Dictionary<BladeStormEvoPrefab, Vector3>();

        float worstGap = 0f;
        int landings = 0;
        float deadline = Time.time + 20f;

        while (Time.time < deadline && landings < 5)
        {
            // Spieler zügig bewegen (schneller als sein normales Tempo).
            player.transform.position += new Vector3(9f * Time.deltaTime, 0f, 0f);

            foreach (BladeStormEvoPrefab blade in Blades())
            {
                bool inOrbit = blade.InOrbit;

                if (wasInOrbit.TryGetValue(blade, out bool previous) && !previous && inOrbit)
                {
                    // Abstand zum Spieler unmittelbar vor dem Andocken
                    float gap = Vector2.Distance(lastPosition[blade], player.transform.position);
                    worstGap = Mathf.Max(worstGap, gap);
                    landings++;
                }

                wasInOrbit[blade] = inOrbit;
                lastPosition[blade] = blade.transform.position;
            }

            yield return null;
        }

        Assert.GreaterOrEqual(landings, 1, "Keine Klinge ist zurückgekehrt");
        Assert.Less(worstGap, 2.5f,
            "Eine Klinge war beim Andocken noch " + worstGap.ToString("F2")
            + " Einheiten vom Spieler entfernt – sie springt also sichtbar in den Orbit");

        Object.Destroy(probe.gameObject);
    }

    private static bool AllInOrbit()
    {
        foreach (BladeStormEvoPrefab blade in Blades())
        {
            if (!blade.InOrbit) return false;
        }
        return true;
    }
}
#endif
