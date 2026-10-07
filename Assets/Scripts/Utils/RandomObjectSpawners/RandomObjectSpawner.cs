using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class RandomObjectSpawner : MonoBehaviour
{
    [Header("Objekte, die gespawnt werden können")]
    public GameObject[] spawnablePrefabs;

    [Header("Einstellungen")]
    public Transform player;
    public float blockSize = 20f;
    public float spawnAheadDistance = 100f;
    public float minY = -8f;
    public float maxY = 8f;
    public int maxObjectsPerBlock = 1;
    [Tooltip("Luecke zwischen zwei Mixer-Feldern, in Feld-Durchmessern (1 = ein ganzes Feld dazwischen).")]
    public float mixerGap = 1f;
    [Tooltip("Nur fuer Props (alles ausser Mixern): Mindestabstand zu Mixer-Feldern, zu anderen " +
             "Props dieses Spawners und zum Spieler. 0 = wie frueher ein Versuch ohne Pruefung.")]
    public float propSpacing = 0f;

    [Header("No-Spawn-Zone um Kamera")]
    public Transform leftNoSpawnPoint;
    public Transform rightNoSpawnPoint;

    private HashSet<int> generatedBlocks = new HashSet<int>();
    private GameObject[] mixerPrefabs;
    private readonly List<Vector2> placedProps = new List<Vector2>();

    private void Update()
    {
        // Die Inspector-Referenz gilt, solange Spieler und Spawner in derselben
        // Szene liegen. Sobald die Maps in eigene Szenen wandern, faellt sie weg
        // (szenenuebergreifende Referenzen speichert Unity nicht) - dann kommt
        // der Spieler hier ueber sein Singleton.
        if (player == null && PlayerController.Instance != null)
            player = PlayerController.Instance.transform;

        if (player == null)
            return;

        int leftBlock = Mathf.FloorToInt((player.position.x - spawnAheadDistance) / blockSize);
        int rightBlock = Mathf.FloorToInt((player.position.x + spawnAheadDistance) / blockSize);

        for (int block = leftBlock; block <= rightBlock; block++)
        {
            if (!generatedBlocks.Contains(block))
            {
                GenerateBlock(block);
                generatedBlocks.Add(block);
            }
        }
    }

    private void GenerateBlock(int blockIndex)
    {
        if (spawnablePrefabs.Length == 0)
            return;

        float blockStartX = blockIndex * blockSize;
        float blockEndX = blockStartX + blockSize;

        int baseCount = Random.Range(0, maxObjectsPerBlock + 1);

        // Skilltree "MoreMixers": zusaetzliche Objekte sind immer Mixer.
        if (mixerPrefabs == null) mixerPrefabs = System.Array.FindAll(spawnablePrefabs, MixerObject.IsMixer);
        int objectCount = mixerPrefabs.Length > 0 ? MixerObject.ScaleSpawnCount(baseCount) : baseCount;

        for (int i = 0; i < objectCount; i++)
        {
            GameObject prefab = i < baseCount
                ? spawnablePrefabs[Random.Range(0, spawnablePrefabs.Length)]
                : mixerPrefabs[Random.Range(0, mixerPrefabs.Length)];

            // Mixer wuerfeln neu, bis ihr Feld frei steht - Props mit
            // propSpacing ebenso, alle anderen haben wie bisher einen Versuch.
            bool isMixer = System.Array.IndexOf(mixerPrefabs, prefab) >= 0;
            bool spacedProp = !isMixer && propSpacing > 0f;
            int attempts = isMixer ? MixerPlacement.Attempts : spacedProp ? 8 : 1;
            bool found = false;
            float randomX = 0f, randomY = 0f;

            for (int a = 0; a < attempts && !found; a++)
            {
                randomX = Random.Range(blockStartX, blockEndX);
                randomY = Random.Range(minY, maxY);

                // 🔹 Überspringen, falls in der No-Spawn-Zone
                if (IsInNoSpawnZone(randomX))
                    continue;

                Vector2 spot = new Vector2(randomX, randomY);
                found = isMixer ? MixerPlacement.IsFree(prefab, spot, mixerGap)
                      : !spacedProp || IsPropSpotFree(spot);
            }

            if (!found)
                continue;

            GameObject spawnedObject = Instantiate(prefab, new Vector3(randomX, randomY, 0f), Quaternion.identity);
            if (spacedProp) placedProps.Add(new Vector2(randomX, randomY));

            Scene gameScene = RunScene.Current;
            if (gameScene.IsValid() && gameScene.isLoaded)
            {
                RunScene.Place(spawnedObject, isMixer ? "Mixer" : "Map-Objekte");
            }
            else
            {
                Debug.LogWarning("⚠️ Keine Lauf-Szene gefunden! Objekt bleibt in aktueller Scene.");
            }
        }
    }

    /// <summary>
    /// Ein Prop haelt <see cref="propSpacing"/> Abstand zu Spieler, eigenen
    /// Props und Mixer-Feldern. Mixer selbst weichen den Props ueber deren
    /// <see cref="MixerBlocker"/> aus - je nachdem, wer zuerst steht.
    /// </summary>
    private bool IsPropSpotFree(Vector2 spot)
    {
        float minSqr = propSpacing * propSpacing;

        if (player != null && ((Vector2)player.position - spot).sqrMagnitude < minSqr)
            return false;

        for (int i = 0; i < placedProps.Count; i++)
        {
            if ((placedProps[i] - spot).sqrMagnitude < minSqr)
                return false;
        }

        var mixers = MixerObject.All;
        for (int i = 0; i < mixers.Count; i++)
        {
            MixerObject mixer = mixers[i];
            if (mixer == null) continue;

            GameObject go = mixer.gameObject;
            float min = MixerPlacement.FieldRadius(go) + propSpacing * 0.5f;
            if ((MixerPlacement.FieldCenter(go, go.transform.position) - spot).sqrMagnitude < min * min)
                return false;
        }

        return true;
    }

    private bool IsInNoSpawnZone(float x)
    {
        if (leftNoSpawnPoint != null && rightNoSpawnPoint != null)
        {
            float left = Mathf.Min(leftNoSpawnPoint.position.x, rightNoSpawnPoint.position.x);
            float right = Mathf.Max(leftNoSpawnPoint.position.x, rightNoSpawnPoint.position.x);

            return x >= left && x <= right;
        }

        // Die beiden Punkte hingen am Kamerarand und stehen seit dem
        // Szenen-Split in GameCore - die Referenz von hier aus ist damit leer.
        // Ohne Ersatz waere die Antwort "nein, spawn ruhig", und dann poppen
        // Baeume mitten im Bild neben dem Spieler auf.
        if (ViewBounds.TryGetWorldRect(out Rect view))
        {
            return x >= view.xMin && x <= view.xMax;
        }

        return false;
    }
}
