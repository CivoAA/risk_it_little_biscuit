using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Haelt die Kachelkanten jeder Pixel-Perfect-Kamera auf echten
/// Bildschirmpixel-Grenzen - auch bei ungerader Fenstergroesse.
///
/// Die Pixel-Perfect-Kamera rastet ihre Position auf ganze Pixel ein, misst
/// das aber von der Bildmitte aus. Ist das Fenster ungerade breit oder hoch
/// (Fenstermodus, Titelleiste, 1366er-Laptops mit Skalierung ...), liegt die
/// Bildmitte auf einem halben Pixel - und damit jede Kachelkante genau auf
/// einer Pixelmitte. Die Grafikkarte holt sich dort mal die Farbe der
/// Nachbarkachel aus dem Tile-Streifen: duenne Linien zwischen den Kacheln,
/// die beim Laufen flackern.
///
/// Abhilfe: das Bild um einen halben Pixel verschieben. Das geht ueber die
/// Projektionsmatrix, weil die Pixel-Perfect-Kamera die nie anfasst (sie
/// setzt nur worldToCameraMatrix) - die Reihenfolge der Render-Callbacks ist
/// damit egal. Kein Prefab, nichts zu verdrahten: haengt sich beim Spielstart
/// selbst ein.
/// </summary>
public static class PixelGridAlign
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (!cam.orthographic) return;
        if (!cam.TryGetComponent(out PixelPerfectCamera ppc) || !ppc.isActiveAndEnabled) return;

        // Immer frisch aus Groesse und Zoom rechnen lassen, sonst bliebe nach
        // dem ersten Mal eine feste Matrix stehen und Cinemachine koennte die
        // Ortho-Groesse nicht mehr aendern.
        cam.ResetProjectionMatrix();

        bool oddX = (cam.pixelWidth & 1) == 1;
        bool oddY = (cam.pixelHeight & 1) == 1;
        if (!oddX && !oddY) return;

        // Ein Bildschirmpixel ist in Clip-Koordinaten 2/Breite gross, ein
        // halber also 1/Breite.
        float dx = oddX ? 1f / cam.pixelWidth : 0f;
        float dy = oddY ? 1f / cam.pixelHeight : 0f;
        cam.projectionMatrix = Matrix4x4.Translate(new Vector3(dx, dy, 0f)) * cam.projectionMatrix;
    }
}
