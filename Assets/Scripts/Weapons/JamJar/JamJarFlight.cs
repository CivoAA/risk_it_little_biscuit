using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ein fliegendes Marmeladenglas (Jam Jar und Sticky Shatter). Fliegt in einem
/// kleinen Bogen zum Ziel und platzt dort - garantiert: die Flugzeit steht beim
/// Abwurf fest, statt dass auf "nah genug am Ziel" gewartet wird.
///
/// Frueher hing das Glas am Spieler und wartete auf einen Abstand unter 0.001.
/// Solange man lief, schob die Spielerbewegung es jeden Frame wieder weg - das
/// Glas blieb als Glas liegen, bis man stehen blieb.
///
/// Mit mehreren Zielen huepft das Glas von Punkt zu Punkt (Sticky Shatter)
/// und zerbricht erst am letzten.
/// </summary>
public class JamJarFlight : MonoBehaviour
{
    /// <summary>Bogenhoehe je Einheit Wurfweite.</summary>
    private const float ArcPerUnit = 0.15f;

    /// <summary>Drehung in Grad pro Sekunde - nur fuer Glaeser ohne eigene Animation.</summary>
    private const float SpinSpeed = 540f;

    private Vector2 start;
    private Vector2 target;
    private float flightTime;
    private float arcHeight;
    private float age;
    private float speed;
    private bool spin;
    private IReadOnlyList<Vector2> targets;
    private int leg;
    private Action<Vector2> landed;

    /// <summary>
    /// Schickt <paramref name="jar"/> nach <paramref name="target"/>. Beim
    /// Aufprall wird das Glas zerstoert und <paramref name="onLanded"/> mit dem
    /// Zielpunkt gerufen.
    /// </summary>
    public static void Launch(GameObject jar, Vector2 target, float speed, Action<Vector2> onLanded)
    {
        Launch(jar, new[] { target }, speed, onLanded);
    }

    /// <summary>
    /// Wie oben, aber das Glas schlaegt nacheinander an allen
    /// <paramref name="targets"/> auf. <paramref name="onImpact"/> kommt bei
    /// jedem Aufschlag, zerstoert wird das Glas erst am letzten.
    /// </summary>
    public static void Launch(GameObject jar, IReadOnlyList<Vector2> targets, float speed, Action<Vector2> onImpact)
    {
        JamJarFlight flight = jar.AddComponent<JamJarFlight>();
        flight.targets = targets;
        flight.speed = Mathf.Max(0.01f, speed);
        flight.spin = jar.GetComponent<SpriteFlipbook>() == null;
        flight.landed = onImpact;
        flight.StartLeg(jar.transform.position, targets[0]);
    }

    private void StartLeg(Vector2 from, Vector2 to)
    {
        start = from;
        target = to;
        age = 0f;

        float distance = Vector2.Distance(from, to);
        flightTime = Mathf.Max(0.1f, distance / speed);
        arcHeight = distance * ArcPerUnit;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / flightTime);

        Vector2 pos = Vector2.Lerp(start, target, t);
        pos.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
        transform.position = new Vector3(pos.x, pos.y, transform.position.z);

        if (spin) transform.Rotate(0f, 0f, SpinSpeed * Time.deltaTime);

        if (t < 1f) return;

        Vector2 reached = target;
        Action<Vector2> callback = landed;
        leg++;

        if (leg < targets.Count)
        {
            StartLeg(reached, targets[leg]);
        }
        else
        {
            landed = null;
            Destroy(gameObject);
        }

        callback?.Invoke(reached);
    }
}
