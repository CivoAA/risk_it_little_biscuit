using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class SpriteSet
{
    public List<Sprite> sprites = new();
}

[RequireComponent(typeof(ParticleSystem))]
public class RandomParticleSprites : MonoBehaviour
{
    [Header("🎨 Sprite-Sets (Index = SkinIndexLast)")]
    public List<SpriteSet> skinSpriteSets = new();

    private ParticleSystem ps;
    private int lastAppliedSkin = 99;

    private void Start()
    {
        ps = GetComponent<ParticleSystem>();
        InvokeRepeating(nameof(TryApplySprites), 0.2f, 1f);
    }

    private void TryApplySprites()
    {
        if (PlayerSkinSwitcher.Instance == null)
            return;

        int skinIndex = PlayerSkinSwitcher.Instance.skinIndexlast;

        if (skinIndex == 99 || skinIndex == lastAppliedSkin)
            return;

        lastAppliedSkin = skinIndex;
        ApplySpritesForSkin(skinIndex);
    }

    private void ApplySpritesForSkin(int skinIndex)
    {
        if (skinIndex < 0 || skinIndex >= skinSpriteSets.Count)
        {
            Debug.LogWarning($"{name}: Kein Sprite-Set für Skin {skinIndex}!");
            return;
        }

        var sprites = skinSpriteSets[skinIndex].sprites;
        if (sprites == null || sprites.Count == 0)
        {
            Debug.LogWarning($"{name}: Sprite-Set {skinIndex} ist leer!");
            return;
        }

        var textureSheet = ps.textureSheetAnimation;
        textureSheet.enabled = true;
        textureSheet.mode = ParticleSystemAnimationMode.Sprites;

        // Entferne alte Sprites
        int existing = textureSheet.spriteCount;
        for (int i = existing - 1; i >= 0; i--)
            textureSheet.RemoveSprite(i);

        // Füge neue hinzu
        foreach (var sprite in sprites)
            textureSheet.AddSprite(sprite);

        textureSheet.frameOverTime = new ParticleSystem.MinMaxCurve(0, sprites.Count);
        textureSheet.animation = ParticleSystemAnimationType.WholeSheet;

        Debug.Log($"✅ Partikel aktualisiert für Skin {skinIndex} ({sprites.Count} Sprites).");
    }
}
