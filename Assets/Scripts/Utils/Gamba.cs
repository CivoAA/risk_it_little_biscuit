using TMPro;
using UnityEngine;

public class Gamba : MonoBehaviour
{
    public static Gamba Instance;

    // Felder (im Inspector einstellbar)
    public float feld1_1;
    public float feld1_2;

    public float feld2_1;
    public float feld2_2;

    public float feld3_1;
    public float feld3_2;

    public float feld4_1;
    public float feld4_2;

    [Header("Spin Settings")]
    public float spinDuration = 3f; // Dauer bis zum Anhalten (Sekunden)
    public int extraSpins = 20;     // volle Umdrehungen zusätzlich

    private float startAngle;
    private float targetAngle;
    private float currentTime;
    private bool spinning;
    public int wins;
    public TMP_Text mulitpliretext;
    private string text;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Update()
    {
        if (!spinning) return;

        currentTime += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(currentTime / spinDuration);

        // ease-out (anfangs schnell, gegen Ende langsam)
        float easedT = 1f - Mathf.Pow(1f - t, 3f);

        // WICHTIG: Mathf.Lerp (nicht LerpAngle) damit die vielen vollen Drehungen wirklich durchlaufen werden
        float z = Mathf.Lerp(startAngle, targetAngle, easedT);
        transform.rotation = Quaternion.Euler(0f, 0f, z);

        if (t >= 1f)
        {
            spinning = false;
            float finalAngle = targetAngle % 360f;
            if (finalAngle < 0f) finalAngle += 360f;
            ReadRotation(); // prüft, in welchem Feld gelandet wurde
            text = $"x{wins}";
            mulitpliretext.text = text;
            if (wins == 2)
            {
                AudioController.Instance.PalySound(AudioController.Instance.WinSound);
            }
            else if (wins == 0)
            {
               AudioController.Instance.PalySound(AudioController.Instance.LoseSound); 
            }
            else if (wins == 1)
            {
               AudioController.Instance.PalySound(AudioController.Instance.LevelUpSound); 
            }
        }
    }

    // Startet den Spin — setzt vorher Rotation auf 0 (wie gewünscht)
    public void StartSpin()
    {
        // sofort auf 0 setzen
        transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        startAngle = 0f;

        float randomZ = Random.Range(0f, 360f);
        targetAngle = startAngle + (extraSpins * 360f) + randomZ;

        currentTime = 0f;
        spinning = true;

        //Debug.Log($"Spin gestartet: extraSpins={extraSpins}, randomOffset={randomZ}, targetAngle(total)={targetAngle}");
    }

    // prüft die aktuelle (0..360) Rotation gegen die Felder
    public void ReadRotation()
    {
        float zRotation = transform.eulerAngles.z;

        if (IsInRange(zRotation, feld1_1, feld1_2))
        {
            wins = 2;
        }
        else if (IsInRange(zRotation, feld2_1, feld2_2))
        {
            wins = 0;
        }
        else if (IsInRange(zRotation, feld3_1, feld3_2))
        {
            wins = 2;
        }
        else if (IsInRange(zRotation, feld4_1, feld4_2))
        {
           wins = 0;
        }
    }

    bool IsInRange(float value, float min, float max)
    {
        // Standardfall
        if (min <= max)
            return value >= min && value <= max;

        // Wrap-around (z. B. 270 .. 20)
        return value >= min || value <= max;
    }
}