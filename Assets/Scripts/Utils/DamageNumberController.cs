using System;
using System.Collections.Generic;
using UnityEngine;

public class DamageNumberController : MonoBehaviour
{
    public static DamageNumberController Instance;
    public DamageNumber prefab;
    public DamageNumber prefabCrit;
    public DamageNumber prefabDodge;

    /// <summary>
    /// Mehr Schadenszahlen gleichzeitig liest ohnehin niemand. Ohne Grenze
    /// erzeugte ein Treffer in einen grossen Haufen Hunderte Textobjekte im
    /// selben Frame - das war ein Teil der Ruckler bei vielen Gegnern.
    /// </summary>
    private const int MaxActiveNumbers = 120;

    // Abgelaufene Zahlen werden wiederverwendet statt zerstoert und neu
    // instanziiert - je Prefab ein eigener Stapel.
    private readonly Dictionary<DamageNumber, Stack<DamageNumber>> pool = new Dictionary<DamageNumber, Stack<DamageNumber>>();
    private int activeNumbers;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Einen Vorrat Zahlen anlegen, solange der Lauf laedt. Textobjekte sind
    /// teuer in der Erzeugung - der erste grosse Treffer soll sie nicht alle
    /// auf einmal bauen muessen.
    /// </summary>
    void Start()
    {
        if (Instance != this || prefab == null) return;

        var made = new List<DamageNumber>(MaxActiveNumbers);
        for (int i = 0; i < MaxActiveNumbers; i++) made.Add(Spawn(prefab, new Vector3(0f, -10000f, 0f)));
        foreach (DamageNumber number in made) Release(number);
    }

    public void CreateNumber(float value, Vector3 location)
    {
        // "Schadenszahlen" in den Optionen: aus heisst ruhigeres Bild. Betrifft
        // nur die Zahlen - Hinweise wie "Dodge" oder "No more Rerolles left"
        // laufen ueber CreateText und bleiben.
        if (!GameSettings.DamageNumbers || activeNumbers >= MaxActiveNumbers) return;

        Spawn(prefab, location).SetValue(Mathf.RoundToInt(value));
    }

    public void CreateNumberCrit(float value, Vector3 location)
    {
        if (!GameSettings.DamageNumbers || activeNumbers >= MaxActiveNumbers) return;

        Spawn(prefabCrit, location).SetValue(Mathf.RoundToInt(value));
    }

    public void CreateDodgeText(Vector3 location)
    {
        Spawn(prefabDodge, location).SetText("Dodge");
    }
    public void CreateText(string Text, Vector3 location)
    {
        Spawn(prefab, location).SetText(Text);
    }

    private DamageNumber Spawn(DamageNumber source, Vector3 location)
    {
        DamageNumber number = null;
        if (pool.TryGetValue(source, out Stack<DamageNumber> stack))
        {
            while (number == null && stack.Count > 0) number = stack.Pop();
        }

        if (number != null)
        {
            number.transform.SetPositionAndRotation(location, transform.rotation);
            number.gameObject.SetActive(true);
        }
        else
        {
            number = Instantiate(source, location, transform.rotation, transform);
            number.Source = source;
        }

        number.Restart();
        activeNumbers++;
        return number;
    }

    /// <summary>Abgelaufene Zahl zurueck in den Pool.</summary>
    public void Release(DamageNumber number)
    {
        // Nicht von hier ausgegeben (z. B. TextDisplay): wie frueher zerstoeren.
        if (number.Source == null)
        {
            Destroy(number.gameObject);
            return;
        }

        activeNumbers = Mathf.Max(0, activeNumbers - 1);

        number.gameObject.SetActive(false);
        if (!pool.TryGetValue(number.Source, out Stack<DamageNumber> stack))
        {
            stack = new Stack<DamageNumber>();
            pool[number.Source] = stack;
        }
        stack.Push(number);
    }
}
