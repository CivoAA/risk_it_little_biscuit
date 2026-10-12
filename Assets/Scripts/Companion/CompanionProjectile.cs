using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Schuss des <see cref="Companion"/>: eine Zuckerperle in der Sorte des
/// Begleiters. Die Bilder kommen von <see cref="CompanionVisual"/>, beim
/// Treffer bleibt ein kurzes Funkeln stehen.
/// </summary>
public class CompanionProjectile : TrackingProjectile
{
    private const float HitFps = 20f;

    private Companion owner;
    private Sprite[] hitFrames;

    public void Launch(Companion companion, Vector2 flyDirection, Enemy target)
    {
        owner = companion;
        ApplyLook(companion != null ? companion.Visual : null);
        Launch(flyDirection, target);
    }

    private void ApplyLook(CompanionVisual visual)
    {
        if (visual == null || visual.ShotFrames == null || visual.ShotFrames.Length == 0) return;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null) return;

        // Das Prefab ist noch auf den alten Platzhalter (halbdurchsichtig) eingestellt.
        sr.color = Color.white;
        sr.sprite = visual.ShotFrames[0];

        SpriteFlipbook flipbook = gameObject.AddComponent<SpriteFlipbook>();
        flipbook.SetFrames(visual.ShotFrames, 14f);

        hitFrames = visual.HitFrames;
    }

    protected override void ApplyDamage(Enemy enemy)
    {
        if (owner == null) return;

        enemy.TakeDamage(owner.Damage);
        SpawnHit();
    }

    private void SpawnHit()
    {
        if (hitFrames == null || hitFrames.Length == 0) return;

        SpriteRenderer mine = GetComponent<SpriteRenderer>();

        GameObject go = new GameObject("CompanionHit");
        go.transform.position = transform.position;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        if (mine != null)
        {
            sr.sharedMaterial = mine.sharedMaterial;
            sr.sortingLayerID = mine.sortingLayerID;
            sr.sortingOrder = mine.sortingOrder + 1;
        }

        SpriteFlipbook flipbook = go.AddComponent<SpriteFlipbook>();
        flipbook.SetFrames(hitFrames, HitFps, startAtZero: true);
        flipbook.ShowInstantly();

        Scene gameScene = RunScene.Current;
        if (gameScene.IsValid() && gameScene.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(go, gameScene);
        }

        Destroy(go, hitFrames.Length / HitFps);
    }
}
