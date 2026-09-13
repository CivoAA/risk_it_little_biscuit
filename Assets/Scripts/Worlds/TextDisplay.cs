using UnityEngine;
using UnityEngine.EventSystems;

public class TextDisplay : MonoBehaviour
{
   public static TextDisplay Instance;
    public DamageNumber prefab;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    public void CreateText(string Text, Vector3 location)
    {
        DamageNumber damageNumber = Instantiate(prefab, location, transform.rotation, transform);
        damageNumber.transform.localScale = Vector3.one * 2f;
        damageNumber.SetText(Text);
    }

    public void DisplayTextOnInvoker(string text)
    {
        GameObject clicked = EventSystem.current.currentSelectedGameObject;

        if (clicked == null)
        {
            Debug.LogWarning("No UI element triggered this");
            return;
        }

        RectTransform rt = clicked.GetComponent<RectTransform>();
        if (rt == null) return;

        Vector3 worldPos = rt.position; // UI → World Space
        CreateText(text, worldPos);
    }
}
