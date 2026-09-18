using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Zeichnet die Knotenformen im Editor-Fenster.
///
/// Im Spiel entstehen dieselben Formen als Pixeltextur
/// (<see cref="SkillShapeSprites"/>) - hier reichen Vielecke, weil der Editor
/// nicht pixelgenau sein muss, sondern gross und lesbar.
///
/// Wer in <see cref="SkillShape"/> eine Form ergaenzt, traegt sie an beiden
/// Stellen ein: hier als Eckpunkte, dort als Pixelmaske. Fehlt eine, faellt die
/// Form auf einen Kreis zurueck - das faellt sofort auf.
/// </summary>
public static class SkillShapeGizmo
{
    public static void Draw(Rect rect, SkillShape shape, Color fill, Color outline, float thickness)
    {
        if (Event.current.type != EventType.Repaint) return;

        Vector3[] points = Polygon(shape, rect);
        if (points.Length < 3) return;

        // Faecher aus der Mitte: funktioniert auch beim Stern, der nicht konvex
        // ist, solange jede Kante von der Mitte aus sichtbar ist.
        Vector3 center = rect.center;
        Handles.color = fill;

        for (int i = 0; i < points.Length; i++)
        {
            Handles.DrawAAConvexPolygon(center, points[i], points[(i + 1) % points.Length]);
        }

        var loop = new Vector3[points.Length + 1];
        points.CopyTo(loop, 0);
        loop[points.Length] = points[0];

        Handles.color = outline;
        Handles.DrawAAPolyLine(thickness, loop);
    }

    static Vector3[] Polygon(SkillShape shape, Rect rect)
    {
        Vector2 c = rect.center;
        float r = Mathf.Min(rect.width, rect.height) * 0.5f;

        switch (shape)
        {
            case SkillShape.Kreis:
                return Regular(c, r, 24, 0f);

            case SkillShape.Rechteck:
                // Quadrat mit abgeschnittenen Ecken - wie die Pixelform im Spiel.
                {
                    float k = r * 0.72f;
                    float e = r * 0.28f;

                    return new[]
                    {
                        V(c.x - k + e, c.y - k), V(c.x + k - e, c.y - k),
                        V(c.x + k, c.y - k + e), V(c.x + k, c.y + k - e),
                        V(c.x + k - e, c.y + k), V(c.x - k + e, c.y + k),
                        V(c.x - k, c.y + k - e), V(c.x - k, c.y - k + e),
                    };
                }

            case SkillShape.Raute:
                return new[] { V(c.x, c.y - r), V(c.x + r, c.y), V(c.x, c.y + r), V(c.x - r, c.y) };

            case SkillShape.Sechseck:
                return Regular(c, r, 6, Mathf.PI * 0.5f);

            case SkillShape.Dreieck:
                return Regular(c, r, 3, -Mathf.PI * 0.5f);

            case SkillShape.Kreuz:
                {
                    float a = r * 0.34f;   // halbe Armbreite
                    float b = r * 0.95f;   // Armlaenge

                    return new[]
                    {
                        V(c.x - a, c.y - b), V(c.x + a, c.y - b), V(c.x + a, c.y - a),
                        V(c.x + b, c.y - a), V(c.x + b, c.y + a), V(c.x + a, c.y + a),
                        V(c.x + a, c.y + b), V(c.x - a, c.y + b), V(c.x - a, c.y + a),
                        V(c.x - b, c.y + a), V(c.x - b, c.y - a), V(c.x - a, c.y - a),
                    };
                }

            case SkillShape.Stern:
                {
                    var pts = new List<Vector3>();
                    float inner = r * 0.45f;

                    // Zehn Punkte, abwechselnd aussen und innen; die erste Zacke
                    // zeigt nach oben.
                    for (int i = 0; i < 10; i++)
                    {
                        float angle = -Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                        float radius = (i % 2 == 0) ? r : inner;

                        pts.Add(V(c.x + Mathf.Cos(angle) * radius,
                                  c.y + Mathf.Sin(angle) * radius));
                    }

                    return pts.ToArray();
                }

            default:
                return Regular(c, r, 24, 0f);
        }
    }

    static Vector3[] Regular(Vector2 c, float r, int corners, float startAngle)
    {
        var pts = new Vector3[corners];

        for (int i = 0; i < corners; i++)
        {
            float angle = startAngle + i * Mathf.PI * 2f / corners;
            pts[i] = V(c.x + Mathf.Cos(angle) * r, c.y + Mathf.Sin(angle) * r);
        }

        return pts;
    }

    static Vector3 V(float x, float y) => new Vector3(x, y, 0f);
}
