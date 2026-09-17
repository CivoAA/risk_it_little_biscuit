using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

public class WM_UIController : MonoBehaviour
{
    [SerializeField] private TMP_Text currencyText;
    [SerializeField] private TMP_Text skillCurrencyText; // optional in Inspector setzen
    [SerializeField] private List<TMP_Text> buttonTexts = new List<TMP_Text>();
    public Image CharButtonImageMain;
    public Image CharButtonImageL;
    public Image CharButtonImageR;
    public Image WaffenButtonImageMain;
    public Image WaffenButtonImageL;
    public Image WaffenButtonImageR;
    public Sprite[] characterSprites;
    public Sprite[] waffenSprites;
    public int currentIndex;

    private const int defaultButtonCount = 8;
    private int lastCurrency = int.MinValue;
    private int lastSkillCurrency = int.MinValue;

    // Die Nachsuche per GameObject.Find lief frueher in jedem Frame, in dem das
    // Textfeld fehlte - zusammen mit einer LogWarning pro Frame. Beides passiert
    // jetzt nur noch einmal je Szene.
    private bool currencyTextSearched;
    private bool skillCurrencyTextSearched;

    public static WM_UIController Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    void Start()
    {
        currentIndex = Shop.SkinIndex;
        UpdateCarousel();
    }
    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        UpdateCurrencyText();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Eine neue Szene bringt neue Textfelder mit: Suche und zuletzt
        // angezeigte Werte zuruecksetzen, damit beides frisch gefuellt wird.
        currencyTextSearched = false;
        skillCurrencyTextSearched = false;
        lastCurrency = int.MinValue;
        lastSkillCurrency = int.MinValue;

        // Coroutine starten, damit die Scene einmal vollständig initialisiert ist
        StartCoroutine(AssignUIRoutine(scene));
    }

    private IEnumerator AssignUIRoutine(Scene scene)
    {
        // einen Frame warten, UI-Instanzen werden so meist fertig erstellt
        yield return null;

        // Ein Textfeld je Shop-Eintrag. Der Katalog gibt die Anzahl vor und ist
        // schon vor der ersten Szene geladen - warten muss hier niemand mehr.
        int targetCount = Mathf.Max(defaultButtonCount, Shop.All.Count);

        // Liste auf passende Größe bringen (vorhandene Einträge bleiben erhalten)
        while (buttonTexts.Count < targetCount)
            buttonTexts.Add(null);

        // Alle GameObjects (auch deaktivierte) aus der Szene holen
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

        // Currency Text suchen (Name: "Text currency")
        bool currencyFound = false;
        foreach (var obj in allObjects)
        {
            if (obj == null) continue;
            if (!obj.scene.IsValid() || obj.scene != scene) continue; // nur Objekte aus der gerade geladenen Szene
            if (obj.name == "Text currency")
            {
                TMP_Text txt = obj.GetComponentInChildren<TMP_Text>(true);
                if (txt != null)
                {
                    currencyText = txt;
                    currencyFound = true;
                }
                break;
            }
        }
        if (!currencyFound)
            Debug.LogWarning($"WM_UIController: 'Text currency' nicht in Szene '{scene.name}' gefunden.");

        // Button Texts (Text 0 .. Text N-1) finden und setzen
        for (int i = 0; i < targetCount; i++)
        {
            // Wenn bereits im Inspector gesetzt/gespeichert → verwenden (überschreiben, falls nötig)
            if (buttonTexts[i] != null)
            {
                buttonTexts[i].text = LevelTextAt(i);
                continue;
            }

            string targetName = $"Text {i}";
            TMP_Text foundText = null;

            foreach (var obj in allObjects)
            {
                if (obj == null) continue;
                if (!obj.scene.IsValid() || obj.scene != scene) continue;
                if (obj.name != targetName) continue;

                // Schau im Objekt nach TMP_Text (auch in deaktivierten Kindern)
                TMP_Text txt = obj.GetComponentInChildren<TMP_Text>(true);
                if (txt != null)
                {
                    foundText = txt;
                    break;
                }
            }

            if (foundText != null)
            {
                buttonTexts[i] = foundText;
                buttonTexts[i].text = LevelTextAt(i);
            }
            else
            {
                Debug.LogWarning($"WM_UIController: '{targetName}' nicht in Szene '{scene.name}' gefunden.");
            }
        }

        // Currency anzeigen (sofern vorhanden)
        UpdateCurrencyText();

        Debug.Log("WM_UIController: UI-Zuweisung abgeschlossen.");
    }

    // externe Methode zum setzen (z. B. von SaveGame UI)
    public void UpdateCurrency(int value)
    {
        if (currencyText != null)
        {
            currencyText.text = value.ToString();
            lastCurrency = value;
        }
    }

    void Update()
    {
        if (IsSceneLoaded("World Map"))
        {
            UpdateCurrencyText();
            UpdateSkillCurrencyText();

        }
    }

    private bool IsSceneLoaded(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.name == sceneName && scene.isLoaded)
                return true;
        }
        return false;
    }

    /// <summary>Gekaufte Stufe des i-ten Shop-Eintrags, als Text fuer den Button.</summary>
    private static string LevelTextAt(int i)
    {
        if (i < 0 || i >= Shop.All.Count) return "0";
        return Shop.LevelOf(Shop.All[i]).ToString();
    }

    public void UpdateCurrencyText()
    {
        // Wenn das Textfeld noch nicht gefunden wurde, einmal nachträglich suchen.
        // GameObject.Find geht über die ganze Szene - das darf nicht in jedem
        // Frame passieren, nur weil das Feld gerade fehlt.
        if (currencyText == null && !currencyTextSearched)
        {
            currencyTextSearched = true;

            var found = GameObject.Find("Text currency");
            if (found != null)
                currencyText = found.GetComponentInChildren<TMP_Text>(true);

            if (currencyText == null)
                Debug.LogWarning("⚠️ currencyText nicht gefunden! Kann Text nicht aktualisieren.");
        }

        if (currencyText == null) return;

        int cur = Shop.Currency;
        if (cur == lastCurrency) return;

        currencyText.text = cur.ToString();
        lastCurrency = cur;
    }


    // kann aufgerufen werden, wenn du manuell alle Button-Texte neu setzen willst
    public void RefreshButtonTexts()
    {
        for (int i = 0; i < buttonTexts.Count; i++)
        {
            if (buttonTexts[i] != null) buttonTexts[i].text = LevelTextAt(i);
        }
    }
    public void Next() // Rechts klicken
    {
        currentIndex = (currentIndex + 1) % characterSprites.Length;
        UpdateCarousel();
    }

    public void Previous() // Links klicken
    {
        currentIndex = (currentIndex - 1 + characterSprites.Length) % characterSprites.Length;
        UpdateCarousel();
    }
    public void UpdateCarousel()
    {
        // Mitte
        CharButtonImageMain.sprite = characterSprites[currentIndex];
        WaffenButtonImageMain.sprite = waffenSprites[currentIndex];

        // Rechts (nächster)
        int rightIndex = (currentIndex + 1) % characterSprites.Length;
        CharButtonImageR.sprite = characterSprites[rightIndex];
        WaffenButtonImageR.sprite = waffenSprites[rightIndex];

        // Links (vorheriger)
        int leftIndex = (currentIndex - 1 + characterSprites.Length) % characterSprites.Length;
        CharButtonImageL.sprite = characterSprites[leftIndex];
        WaffenButtonImageL.sprite = waffenSprites[leftIndex];
        SelectChar();
    }
    public void SelectChar()
    {
        // Speichert gleich mit - die Startwaffe leitet sich daraus ab (Characters.cs).
        Shop.SkinIndex = currentIndex;

        // Sobald es Skilltrees pro Charakter gibt, wechselt hier der Baum mit.
        Skills.SetActiveTreeForCharacter(currentIndex);

        if (PlayerSkinSwitcher.Instance != null)
            PlayerSkinSwitcher.Instance.skinIndex = currentIndex;

        if (WM_PlayerSkinSwitcher.Instance != null)
            WM_PlayerSkinSwitcher.Instance.skinIndex = currentIndex;
    }

    public void UpdateSkillCurrencyText()
    {
        // falls nicht im Inspector gesetzt, einmal per Name versuchen
        if (skillCurrencyText == null && !skillCurrencyTextSearched)
        {
            skillCurrencyTextSearched = true;

            var go = GameObject.Find("Text SkillCurrency");
            if (go != null) skillCurrencyText = go.GetComponentInChildren<TMP_Text>(true);
        }

        if (skillCurrencyText == null) return;

        int currency = Skills.Currency;
        if (currency == lastSkillCurrency) return;

        skillCurrencyText.text = currency.ToString();
        lastSkillCurrency = currency;
    }



}