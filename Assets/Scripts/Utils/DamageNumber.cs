using TMPro;
using UnityEngine;

public class DamageNumber : MonoBehaviour
{
    [SerializeField] private TMP_Text damageText;
    private float floatSpeed;
    private float lifetime = 1f; // Sekunden, bis sie verschwinden

    void Start()
    {
        floatSpeed = Random.Range(0.1f, 1.5f);
    }

    void Update()
    {
        // Bewegung und Lebenszeit unabhängig vom Time.timeScale
        transform.position += Vector3.up * Time.unscaledDeltaTime * floatSpeed;
        lifetime -= Time.unscaledDeltaTime;

        if (lifetime <= 0f)
            Destroy(gameObject);
    }

    public void SetValue(int value)
    {
        damageText.text = value.ToString();
    }

    public void SetText(string value)
    {
        damageText.text = value;
    }
}

