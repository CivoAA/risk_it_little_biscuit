using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UnlockUIManager : MonoBehaviour
{
    public static UnlockUIManager Instance;

    public GameObject unlockFramePrefab; // "frame_icon" Prefab
    public Transform contentParent;      // Content des ScrollView
    public UnlockManager unlockManager;  // Deine Unlock-Liste

    [Header("Detail-Anzeige")]
    public TMP_Text nameText;
    public TMP_Text descriptionText;
    public Image unlockIcon;

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(this);
        else
            Instance = this;
    }

    private void Start()
    {
        RefreshUI();
    }

    // 🔄 Neue Methode zum Aktualisieren der UI
    public void RefreshUI()
    {
        // Vorherigen Inhalt löschen
        foreach (Transform child in contentParent)
            Destroy(child.gameObject);

        // Neu generieren
        GenerateUnlocks();
    }

    private void GenerateUnlocks()
    {
        foreach (var unlock in unlockManager.unlocks)
        {
            GameObject frame = Instantiate(unlockFramePrefab, contentParent);

            Transform iconTransform = frame.transform.Find("unlock_icon");
            if (iconTransform == null) continue;

            Image iconImage = iconTransform.GetComponent<Image>();
            if (iconImage == null) continue;

            iconImage.sprite = unlock.unlockedIcon;

            // 🔒 Wenn NICHT freigeschaltet → Silhouette (dunkler)
            iconImage.color = unlock.isUnlocked ? Color.white : new Color(0f, 0f, 0f, 0.9f);

            // 🔘 Button-Click-Event
            Button button = frame.GetComponent<Button>();
            if (button != null)
            {
                Unlock capturedUnlock = unlock; // wichtig für Closure
                button.onClick.AddListener(() => 
                {
                    AudioController.Instance.PalySound(AudioController.Instance.MenuClick);
                    ShowUnlockInfo(capturedUnlock);
                });
            }
        }
    }

    public void ShowUnlockInfo(Unlock unlock)
    {
        // 🧩 Name: ??? wenn nicht unlocked
        if (nameText != null)
            nameText.text = unlock.isUnlocked ? unlock.displayName : "???";

        // 📄 Beschreibung bleibt immer sichtbar
        if (descriptionText != null)
            descriptionText.text = unlock.description;

        // 🖼️ Icon: dunkler, wenn nicht freigeschaltet
        if (unlockIcon != null)
        {
            unlockIcon.sprite = unlock.unlockedIcon;
            unlockIcon.color = unlock.isUnlocked ? Color.white : new Color(0f, 0f, 0f, 0.9f);
        }
    }
}
