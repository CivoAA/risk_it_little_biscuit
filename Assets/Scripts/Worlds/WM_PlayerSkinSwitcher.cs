using UnityEngine;

public class WM_PlayerSkinSwitcher : MonoBehaviour
{
    public static WM_PlayerSkinSwitcher Instance;
    public Animator animator;  // Dein Animator am Player
    public RuntimeAnimatorController NormalSkinOverride;
    public AnimatorOverrideController BlackSkinOverride;
    public AnimatorOverrideController RedSwordSkinOverride;
    public AnimatorOverrideController JamJarSkinOverride;
    public int skinIndex = 0;
    public int skinIndexlast = 99;
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
        skinIndexlast = 99;
    }

    void Update()
    {
        if (skinIndex != skinIndexlast)
        {
            switch (skinIndex)
            {
                case 0:
                    SetNormalSkin();
                    break;
                case 1:
                    SetBlackSkin();
                    break;
                case 2:
                    SetRedSwordSkin();
                    break;
                case 3:
                    SetJamJarSkin();
                    break;
                default:
                    // Neuere Charaktere (ab 4) stehen nur in CharacterLooks.
                    RuntimeAnimatorController look = CharacterLooks.AnimatorFor(skinIndex);
                    if (look != null) animator.runtimeAnimatorController = look;
                    else Debug.Log("Unbekannter Skin!");
                    break;

            }
            skinIndexlast = skinIndex;
        }
    }

    public void SetNormalSkin()
    {
        animator.runtimeAnimatorController = NormalSkinOverride;
    }

    public void SetBlackSkin()
    {
        animator.runtimeAnimatorController = BlackSkinOverride;
    }
    public void SetRedSwordSkin()
    {
        animator.runtimeAnimatorController = RedSwordSkinOverride;
    }
    public void SetJamJarSkin()
    {
        animator.runtimeAnimatorController = JamJarSkinOverride;
    }
}
