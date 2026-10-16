using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Haelt die Kachelkanten jeder Pixel-Perfect-Kamera auf echten
/// Bildschirmpixel-Grenzen. Zwei Faelle, beide sonst duenne (oft schwarze)
/// Linien zwischen den Kacheln, die beim Laufen flackern - die Grafikkarte
/// trifft an der Kante eine Pixelmitte und holt sich die Farbe der
/// Nachbarkachel aus dem Tile-Streifen bzw. den Hintergrund:
///
/// 1. Falsches Raster mit Cinemachine. Im Lauf sitzt CinemachinePixelPerfect
///    an der vcam; die Welt wird dann mit dem vcam-Zoom gezeichnet (1080p:
///    3 Bildschirmpixel pro Grafikpixel). Die Pixel-Perfect-Kamera rastet die
///    Kameraposition aber auf ihr eigenes, feineres Raster ein (1080p: 1/192
///    statt 1/96 Einheit) - Unity setzt den Wert in UpdateCameraProperties
///    jedes Bild zurueck. Bei der Haelfte aller Positionen steht die Kamera
///    also auf einem halben Bildschirmpixel. Deshalb "nur manchmal".
///
/// 2. Ungerade Fenstergroesse (Fenstermodus, Titelleiste, 1366er-Laptops mit
///    Skalierung ...): die Bildmitte liegt auf einem halben Pixel.
///
/// Abhilfe: das Bild ueber die Projektionsmatrix auf das echte Pixelraster
/// schieben. Die Pixel-Perfect-Kamera fasst die nie an (sie setzt nur
/// worldToCameraMatrix) - die Reihenfolge der Render-Callbacks ist damit
/// egal. Die Kamera bewegt sich weiter in Bildschirmpixeln, also so glatt wie
/// bisher. Kein Prefab, nichts zu verdrahten: haengt sich beim Spielstart
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

        Vector2 shift = Vector2.zero;   // in Bildschirmpixeln

        // Fall 1: Position so nachrechnen, wie die Pixel-Perfect-Kamera sie
        // einrastet, und den Rest bis zum echten Pixelraster dazugeben.
        if (ppc.gridSnapping == PixelPerfectCamera.GridSnapping.None
            && ppc.cropFrame == PixelPerfectCamera.CropFrame.None && cam.pixelHeight > 0)
        {
            RenderTexture rt = cam.targetTexture;
            int w = rt != null ? rt.width : Screen.width;
            int h = rt != null ? rt.height : Screen.height;
            int zoom = Mathf.Max(1, Mathf.Min(h / ppc.refResolutionY, w / ppc.refResolutionX));
            float ppcStep = 1f / (zoom * ppc.assetsPPU);

            // Ein echter Bildschirmpixel in Welt-Einheiten.
            float screenStep = 2f * cam.orthographicSize / cam.pixelHeight;

            Vector3 p = cam.transform.position;
            Vector2 snapped = new Vector2(
                Mathf.Round(p.x / ppcStep) * ppcStep,
                Mathf.Round(p.y / ppcStep) * ppcStep);
            Vector2 aligned = new Vector2(
                Mathf.Round(snapped.x / screenStep) * screenStep,
                Mathf.Round(snapped.y / screenStep) * screenStep);

            // Kamera weiter rechts = Bild weiter links.
            shift = (snapped - aligned) / screenStep;
        }

        // Fall 2: halber Pixel bei ungerader Breite/Hoehe.
        if ((cam.pixelWidth & 1) == 1) shift.x += 0.5f;
        if ((cam.pixelHeight & 1) == 1) shift.y += 0.5f;

        if (shift.sqrMagnitude < 1e-6f) return;

        // Ein Bildschirmpixel ist in Clip-Koordinaten 2/Breite gross.
        float dx = shift.x * 2f / cam.pixelWidth;
        float dy = shift.y * 2f / cam.pixelHeight;
        cam.projectionMatrix = Matrix4x4.Translate(new Vector3(dx, dy, 0f)) * cam.projectionMatrix;
    }
}
