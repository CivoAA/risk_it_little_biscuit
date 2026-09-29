using UnityEngine;

public class PlayerSkinSwitcher : MonoBehaviour
{
    public static PlayerSkinSwitcher Instance;
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
        skinIndex = Shop.RunSkinIndex;
        skinIndexlast = 99;
    }

    void Update()
    {
        if (skinIndex != skinIndexlast)
        {
            // Seit 29.09.2026 sind alle Charaktere 32x32 und stehen nur noch in
            // CharacterLooks. Die alten 64er-Overrides unten sind unbenutzt;
            // NormalSkinOverride bleibt nur Rueckfall, falls das Asset fehlt.
            RuntimeAnimatorController look = CharacterLooks.AnimatorFor(skinIndex);
            if (look != null) animator.runtimeAnimatorController = look;
            else if (skinIndex == 0) SetNormalSkin();
            else Debug.Log("Unbekannter Skin!");
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