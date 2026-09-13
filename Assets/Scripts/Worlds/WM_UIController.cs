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
        currentIndex = SaveGame.Instance.currentData.skinIndex;
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
        // Coroutine starten, damit die Scene einmal vollständig initialisiert ist
        StartCoroutine(AssignUIRoutine(scene));
    }

    private IEnumerator AssignUIRoutine(Scene scene)
    {
        // einen Frame warten, UI-Instanzen werden so meist fertig erstellt
        yield return null;

        // kurz warten, falls SaveGame noch nicht ready ist (max. 10 Frames)
        int tries = 0;
        while ((SaveGame.Instance == null || SaveGame.Instance.currentData == null) && tries < 10)
        {
            tries++;
            yield return null;
        }

        if (SaveGame.Instance == null)
        {
            Debug.LogError("WM_UIController: SaveGame.Instance ist null. UI-Zuweisung abgebrochen.");
            yield break;
        }
        if (SaveGame.Instance.currentData == null)
        {
            Debug.LogError("WM_UIController: SaveGame.Instance.currentData ist null. UI-Zuweisung abgebrochen.");
            yield break;
        }

        // Ziel-Anzahl an Button-Slots (mind. defaultButtonCount, ggf. mehr wenn Save mehr Buttons hat)
        int targetCount = Mathf.Max(defaultButtonCount, SaveGame.Instance.currentData.buttons?.Count ?? defaultButtonCount);

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
                // Text setzen, falls Save-Eintrag existiert
                if (i < SaveGame.Instance.currentData.buttons.Count)
                    buttonTexts[i].text = SaveGame.Instance.currentData.buttons[i].level.ToString();
                else
                    buttonTexts[i].text = "0";
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
                if (i < SaveGame.Instance.currentData.buttons.Count)
                    buttonTexts[i].text = SaveGame.Instance.currentData.buttons[i].level.ToString(); //<------------------ heir wird der text eigegeben
                else
                    buttonTexts[i].text = "0";
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
            //SkillSaveManager.Instance.ApplyToScene();
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

    public void UpdateCurrencyText()
    {
        if (SaveGame.Instance == null || SaveGame.Instance.currentData == null)
            return;

        int cur = SaveGame.Instance.currentData.currency;

        // Wenn das Textfeld noch nicht gefunden wurde, such es hier nachträglich
        if (currencyText == null)
        {
            var found = GameObject.Find("Text currency");
            if (found != null)
                currencyText = found.GetComponentInChildren<TMP_Text>(true);
        }

        // Jetzt Text immer aktualisieren, egal ob lastCurrency gleich ist oder nicht
        if (currencyText != null)
        {
            currencyText.text = cur.ToString();
            lastCurrency = cur;
        }
        else
        {
            Debug.LogWarning("⚠️ currencyText nicht gefunden! Kann Text nicht aktualisieren.");
        }
    }


    // kann aufgerufen werden, wenn du manuell alle Button-Texte neu setzen willst
    public void RefreshButtonTexts()
    {
        if (SaveGame.Instance == null || SaveGame.Instance.currentData == null) return;
        for (int i = 0; i < buttonTexts.Count; i++)
        {
            if (buttonTexts[i] != null)
            {
                if (i < SaveGame.Instance.currentData.buttons.Count)
                    buttonTexts[i].text = SaveGame.Instance.currentData.buttons[i].level.ToString();
                else
                    buttonTexts[i].text = "0";
            }
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
        if (LevelPoint.Instance != null)
        {
            if (LevelPoint.Instance.extraData != null)
                LevelPoint.Instance.extraData[0] = currentIndex;
            LevelPoint.Instance.Startweapon(currentIndex);
        }

        if (PlayerSkinSwitcher.Instance != null)
            PlayerSkinSwitcher.Instance.skinIndex = currentIndex;

        if (WM_PlayerSkinSwitcher.Instance != null)
            WM_PlayerSkinSwitcher.Instance.skinIndex = currentIndex;

        if (SaveGame.Instance != null && SaveGame.Instance.currentData != null)
        {
            SaveGame.Instance.currentData.skinIndex = currentIndex;
            SaveGame.Instance.SaveGameData();
        }
    }

    public void UpdateSkillCurrencyText()
    {
        if (SkillSaveManager.Instance == null)
        {
            Debug.LogWarning("⚠ SkillSaveManager.Instance ist NULL!");
            return;
        }

        // 🔧 HIER war der Fehler: saveData → currentData
        int currency = SkillSaveManager.Instance.currentData.skillCurrency;

        // falls nicht im Inspector gesetzt, versuch's per Name
        if (skillCurrencyText == null)
        {
            var go = GameObject.Find("Text SkillCurrency");
            if (go != null) skillCurrencyText = go.GetComponentInChildren<TMP_Text>(true);
        }

        if (skillCurrencyText != null)
        {
            skillCurrencyText.text = currency.ToString();
            // Debug optional:
            // Debug.Log($"💰 Skill Currency Anzeige aktualisiert: {currency}");
        }
    }



}