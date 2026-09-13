#if UNITY_INCLUDE_TESTS
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Deckt die drei gemeldeten Kleinigkeiten ab:
/// Klingen bleiben am Dummy haengen, zeigen beim Spawnen nach oben, und drehen
/// sich im pausierten Level-Up-Menue mit den Tasten mit.
/// </summary>
public class BladeOrientationTests : WeaponTestBase
{
    private static BladeStormEvoPrefab[] Blades()
    {
        return Object.FindObjectsByType<BladeStormEvoPrefab>(FindObjectsSortMode.None);
    }

    private IEnumerator GrantEvo(BladeStormEvo evo)
    {
        evo.weaponLevel = 0;
        int expected = Mathf.RoundToInt(evo.stats[0].shots);
        yield return WaitUntil(() => Blades().Length >= expected, 5f,
            "Blade Storm Evo hat nicht alle Klingen gespawnt");
    }

    /// <summary>
    /// Frisch gespawnte Klingen behielten ihre Instantiate-Rotation und zeigten
    /// mit der Spitze nach oben, bis der Spieler das erste Mal seitwaerts lief.
    /// </summary>
    [UnityTest]
    public IEnumerator Klingen_sind_direkt_nach_dem_Spawn_ausgerichtet()
    {
        BladeStormEvo evo = FindWeapon<BladeStormEvo>("Blade Storm Evo");
        yield return GrantEvo(evo);
        yield return null;

        foreach (BladeStormEvoPrefab blade in Blades())
        {
            if (!blade.InOrbit) continue;

            float z = blade.transform.eulerAngles.z;
            float offBy = Mathf.Min(Mathf.Abs(Mathf.DeltaAngle(z, 90f)),
                                    Mathf.Abs(Mathf.DeltaAngle(z, -90f)));

            Assert.Less(offBy, 1f,
                "Klinge steht bei " + z.ToString("F1") + "° statt seitlich ausgerichtet "
                + "– sie zeigt mit der Spitze nach oben");
        }
    }
    /// <summary>
    /// Bei Tempo 20 legt eine Klinge 0.4 Einheiten pro Physikschritt zurueck.
    /// Mit der alten festen Ankunftsschwelle von 0.15 konnte sie das Ziel nie
    /// "erreichen": sie schoss darueber hinaus, kehrte um, schoss zurueck – und
    /// zappelte so vor dem Gegner, bis nach 2s der Timeout griff. Ob das
    /// passierte, hing vom Startabstand ab, deshalb das gemeldete "manchmal".
    ///
    /// Geprueft wird die harte Invariante: waehrend des Hinflugs darf eine
    /// Klinge ihre Bewegungsrichtung nicht umkehren.
    /// </summary>
    /// <summary>
    /// Bei Tempo 20 legt eine Klinge 0.4 Einheiten pro Physikschritt zurueck.
    /// Mit der alten festen Ankunftsschwelle von 0.15 konnte sie das Ziel nur
    /// dann "erreichen", wenn der Restweg zufaellig unter 0.15 endete. Sonst
    /// schoss sie darueber hinaus, kehrte um, schoss zurueck – und zappelte vor
    /// dem Gegner, bis nach 2s der Timeout griff. Weil das vom Abstand abhaengt,
    /// trat es nur "manchmal" auf.
    ///
    /// Deshalb wird ueber mehrere Abstaende in 0.1er-Schritten geprueft: einer
    /// davon faellt sicher in den kritischen Bereich zwischen 0.15 und 0.4.
    /// Invariante: waehrend des Hinflugs kehrt eine Klinge ihre Richtung nie um.
    /// </summary>
    [UnityTest]
    public IEnumerator Klinge_bleibt_bei_keinem_Abstand_am_Ziel_haengen()
    {
        BladeStormEvo evo = FindWeapon<BladeStormEvo>("Blade Storm Evo");
        yield return GrantEvo(evo);

        DamageProbe probe = CreateProbe(player.transform.position + new Vector3(4f, 0f, 0f),
                                        new Vector2(1.0f, 1.0f));
        yield return WaitUntil(() => evo.enemiesInRange.Contains(probe), 3f,
            "Der Erfassungsbereich der Waffe hat den Gegner nicht registriert");

        float[] distances = { 3.85f, 3.95f, 4.05f, 4.15f, 4.25f, 4.35f };

        foreach (float distance in distances)
        {
            // Zwischen den Salven umsetzen, damit die Flugbahn stabil bleibt.
            yield return WaitUntil(AllInOrbit, 10f, "Die Klingen kamen nicht zurueck");
            probe.transform.position = player.transform.position + new Vector3(distance, 0f, 0f);

            yield return ObserveOneOutbound();

            Debug.Log("[BLADE] Abstand " + distance.ToString("F2")
                      + ": Hinflug " + lastOutboundDuration.ToString("F2")
                      + "s, Richtungswechsel " + lastReversals);

            Assert.AreEqual(0, lastReversals,
                "Bei Abstand " + distance.ToString("F2") + " hat die Klinge "
                + lastReversals + "x die Richtung gewechselt – sie zappelt vor dem Gegner");

            Assert.Less(lastOutboundDuration, 1f,
                "Bei Abstand " + distance.ToString("F2") + " dauerte der Hinflug "
                + lastOutboundDuration.ToString("F2") + "s – da hat nur der 2s-Timeout gegriffen");
        }

        Object.Destroy(probe.gameObject);
    }

    private int lastReversals;
    private float lastOutboundDuration;

    /// <summary>Beobachtet den naechsten Hinflug einer Klinge Schritt fuer Schritt.</summary>
    private IEnumerator ObserveOneOutbound()
    {
        BladeStormEvoPrefab blade = null;
        yield return WaitUntil(() =>
        {
            foreach (BladeStormEvoPrefab b in Blades())
            {
                if (b.State == BladeStormEvoPrefab.BladeState.Outbound) { blade = b; return true; }
            }
            return false;
        }, 10f, "Keine Klinge ist losgeflogen");

        lastReversals = 0;
        float start = Time.time;

        Vector2 previous = blade.transform.position;
        Vector2 lastStep = Vector2.zero;

        while (blade != null && blade.State == BladeStormEvoPrefab.BladeState.Outbound)
        {
            yield return new WaitForFixedUpdate();
            if (blade == null) break;

            Vector2 current = blade.transform.position;
            Vector2 step = current - previous;
            previous = current;

            if (step.sqrMagnitude <= 0.000001f) continue;

            if (lastStep != Vector2.zero
                && Vector2.Dot(lastStep.normalized, step.normalized) < 0f)
            {
                lastReversals++;
            }
            lastStep = step;
        }

        lastOutboundDuration = Time.time - start;
    }

    private static bool AllInOrbit()
    {
        foreach (BladeStormEvoPrefab b in Blades())
        {
            if (!b.InOrbit) return false;
        }
        return true;
    }

    /// </summary>
    [UnityTest]
    public IEnumerator Blickrichtung_aendert_sich_im_pausierten_Spiel_nicht()
    {
        Animator animator = GameObject.FindWithTag("PlayerHitbox").GetComponent<Animator>();
        Assert.IsNotNull(animator, "Player-Animator nicht gefunden");

        Time.timeScale = 0f;

        // Sentinel setzen: laeuft der Eingabe-Block trotz Pause weiter,
        // ueberschreibt er den Wert im naechsten Frame.
        animator.SetFloat("LastMoveX", 0.75f);
        animator.SetFloat("LastMoveY", 0.25f);

        for (int i = 0; i < 10; i++) yield return null;

        Assert.AreEqual(0.75f, animator.GetFloat("LastMoveX"), 0.0001f,
            "LastMoveX wurde trotz Pause ueberschrieben – der Spieler dreht sich im Menue mit");
        Assert.AreEqual(0.25f, animator.GetFloat("LastMoveY"), 0.0001f,
            "LastMoveY wurde trotz Pause ueberschrieben");

        Time.timeScale = 1f;

        // Nach dem Fortsetzen muss die Auswertung wieder greifen.
        for (int i = 0; i < 3; i++) yield return null;

        Assert.AreNotEqual(0.75f, animator.GetFloat("LastMoveX"),
            "Nach dem Fortsetzen muss die Eingabe wieder ausgewertet werden");
    }
}
#endif
