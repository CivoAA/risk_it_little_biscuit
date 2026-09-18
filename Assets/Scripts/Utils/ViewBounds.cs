using UnityEngine;

/// <summary>
/// Das sichtbare Rechteck der Spielkamera in Weltkoordinaten.
///
/// Gebraucht wird das dort, wo frueher zwei Hilfsobjekte am Kamerarand hingen
/// ("No-Spawn-Zone" der Random-Spawner). Die Objekte stehen seit dem
/// Szenen-Split in GameCore, die Spawner in der Map-Szene - und
/// szenenuebergreifende Referenzen speichert Unity nicht. Statt die Punkte zu
/// ersetzen, fragen wir die Kamera direkt: sie ist die Quelle, aus der die
/// Punkte ohnehin abgeleitet waren.
/// </summary>
public static class ViewBounds
{
    private static Camera cached;

    /// <summary>
    /// Das Kamera-Rechteck. Liefert false, wenn gerade keine Kamera da ist -
    /// dann muss der Aufrufer entscheiden, was das bedeutet.
    /// </summary>
    public static bool TryGetWorldRect(out Rect rect)
    {
        Camera camera = Current;
        if (camera == null || !camera.orthographic)
        {
            rect = default;
            return false;
        }

        float halfHeight = camera.orthographicSize;
        float halfWidth = halfHeight * camera.aspect;
        Vector3 center = camera.transform.position;

        rect = new Rect(center.x - halfWidth, center.y - halfHeight, halfWidth * 2f, halfHeight * 2f);
        return true;
    }

    private static Camera Current
    {
        get
        {
            if (cached != null) return cached;

            cached = Camera.main;
            if (cached == null) cached = Object.FindAnyObjectByType<Camera>();

            return cached;
        }
    }
}
