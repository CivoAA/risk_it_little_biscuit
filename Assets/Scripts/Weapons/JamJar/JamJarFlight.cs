using System;
using UnityEngine;

/// <summary>
/// Ein fliegendes Marmeladenglas (Jam Jar und Sticky Shatter). Fliegt in einem
/// kleinen Bogen zum Ziel und platzt dort - garantiert: die Flugzeit steht beim
/// Abwurf fest, statt dass auf "nah genug am Ziel" gewartet wird.
///
/// Frueher hing das Glas am Spieler und wartete auf einen Abstand unter 0.001.
/// Solange man lief, schob die Spielerbewegung es jeden Frame wieder weg - das
/// Glas blieb als Glas liegen, bis man stehen blieb.
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
    private bool spin;
    private Action<Vector2> landed;

    /// <summary>
    /// Schickt <paramref name="jar"/> nach <paramref name="target"/>. Beim
    /// Aufprall wird das Glas zerstoert und <paramref name="onLanded"/> mit dem
    /// Zielpunkt gerufen.
    /// </summary>
    public static void Launch(GameObject jar, Vector2 target, float speed, Action<Vector2> onLanded)
    {
        JamJarFlight flight = jar.AddComponent<JamJarFlight>();
        flight.start = jar.transform.position;
        flight.target = target;

        float distance = Vector2.Distance(flight.start, target);
        flight.flightTime = Mathf.Max(0.1f, distance / Mathf.Max(0.01f, speed));
        flight.arcHeight = distance * ArcPerUnit;
        flight.spin = jar.GetComponent<SpriteFlipbook>() == null;
        flight.landed = onLanded;
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

        Action<Vector2> callback = landed;
        landed = null;
        Destroy(gameObject);
        callback?.Invoke(target);
    }
}
