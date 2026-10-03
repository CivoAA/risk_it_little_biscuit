using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Wackelt die Kamera - kurz und kraeftig, fuer Einschlaege (Schleimkoenig
/// landet). Aufruf von ueberall: <c>ScreenShake.Kick(3f, 0.35f)</c>.
///
/// Haengt sich als CinemachineExtension zur Laufzeit an jede aktive
/// CinemachineCamera (Spielszene, Test-Szene) - kein Prefab, nichts zu
/// verdrahten. Der Versatz ist auf ganze Pixel gerundet (32 pro Einheit, wie
/// die Pixel-Perfect-Kamera). Sonst wuerde die Welt waehrend des Wackelns
/// zwischen den Pixeln flimmern.
///
/// Der Versatz haengt nur an der Uhr (Rauschen ueber Time.time), nicht an der
/// Kamera - zwei Kameras wackeln also gleich, und ein zweiter Kick waehrend
/// des ersten verlaengert bzw. verstaerkt, statt neu anzusetzen.
/// </summary>
[AddComponentMenu("")]
public class ScreenShake : CinemachineExtension
{
    private const float PixelsPerUnit = 32f;

    /// <summary>So schnell zittert es (Rauschfrequenz, 1/s).</summary>
    private const float Frequency = 26f;

    private static float strength;     // in Pixeln
    private static float startTime;
    private static float duration;

    /// <summary>
    /// Kamera wackeln lassen. <paramref name="pixels"/> = groesster Ausschlag
    /// in Bildpixeln, klingt ueber <paramref name="seconds"/> auf 0 ab.
    /// </summary>
    public static void Kick(float pixels, float seconds)
    {
        // Laeuft schon ein staerkeres, bleibt es dabei.
        if (Current() > pixels) return;

        strength = pixels;
        duration = Mathf.Max(0.05f, seconds);
        startTime = Time.time;

        AttachToCameras();
    }

    /// <summary>Ausschlag gerade jetzt (Pixel).</summary>
    private static float Current()
    {
        if (duration <= 0f) return 0f;
        float t = (Time.time - startTime) / duration;
        if (t >= 1f) return 0f;
        // Erst voll, dann weich aus: (1-t)^2
        return strength * (1f - t) * (1f - t);
    }

    private static void AttachToCameras()
    {
        CinemachineCamera[] cams = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
        foreach (CinemachineCamera cam in cams)
        {
            if (cam.GetComponent<ScreenShake>() == null) cam.gameObject.AddComponent<ScreenShake>();
        }
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize) return;

        float amp = Current();
        if (amp <= 0.01f) return;

        float t = Time.time * Frequency;
        float x = (Mathf.PerlinNoise(t, 0.37f) * 2f - 1f) * amp;
        float y = (Mathf.PerlinNoise(0.71f, t) * 2f - 1f) * amp;

        // Ganze Pixel, sonst flimmert die Pixelwelt.
        float px = Mathf.Floor(x + 0.5f);
        float py = Mathf.Floor(y + 0.5f);

        state.PositionCorrection += state.GetCorrectedOrientation() * new Vector3(px, py, 0f) / PixelsPerUnit;
    }
}
