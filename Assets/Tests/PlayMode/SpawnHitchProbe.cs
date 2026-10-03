#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

/// <summary>
/// Messung, kein Test im engeren Sinn: laedt GameCore + Wald, spawnt Gegner
/// in den Mengen der Wellenplaene und schreibt Frame-Zeiten ins Log
/// ("[SpawnHitch]"). Sucht den "kurzen Freeze bei vielen Gegnern".
/// </summary>
public class SpawnHitchProbe
{
    private readonly StringBuilder log = new StringBuilder();
    private SpawnCatalog catalog;
    private PlayerController player;

    private class FixedCounter : MonoBehaviour
    {
        public int steps;
        void FixedUpdate() { steps++; }
    }

    private FixedCounter counter;

    private static readonly string[] Markers =
    {
        "FixedBehaviourUpdate", "Physics2D.Simulate", "BehaviourUpdate",
        "Instantiate", "GC.Collect", "Destroy", "PostLateUpdate.FinishFrameRendering",
    };

    [UnityTest]
    [Explicit("Messung, kein Test - nur gezielt starten")]
    [Timeout(600000)]
    public IEnumerator Spawn_Hitches_messen()
    {
        AudioListener.volume = 0f;
        AudioListener.pause = true;

        yield return SceneManager.LoadSceneAsync(MapSceneSystem.CoreScene, LoadSceneMode.Single);
        yield return SceneManager.LoadSceneAsync("Map_World0", LoadSceneMode.Additive);
        for (int i = 0; i < 30; i++) yield return null;

        player = PlayerController.Instance;
        Assert.IsNotNull(player, "kein Spieler");
        if (!player.gameObject.activeSelf) player.gameObject.SetActive(true);
        player.playerMaxHealth = 1e9f;
        player.playerHealth = 1e9f;

        // Waffen aus, sonst sterben die Gegner und die Messung schwimmt.
        foreach (Weapon w in Object.FindObjectsByType<Weapon>(FindObjectsSortMode.None)) w.gameObject.SetActive(false);

        SpawnDirector director = Object.FindAnyObjectByType<SpawnDirector>();
        if (director != null) director.enabled = false;
        catalog = Object.FindAnyObjectByType<SpawnCatalog>();
        Assert.IsNotNull(catalog, "kein SpawnCatalog");

        counter = new GameObject("FixedCounter").AddComponent<FixedCounter>();
        Line($"Fixed {Time.fixedDeltaTime}, maxDelta {Time.maximumDeltaTime}, alive {Enemy.Alive.Count}");

        yield return Measure("Leerlauf", 60, null);

        yield return Measure("Burst 60 Fetti Cluster (erste Instanz)", 120, () => Burst(EnemyId.Fetti, 60, cluster: true));
        yield return Measure("Burst 60 Marshmello Cluster", 120, () => Burst(EnemyId.Marshmello, 60, cluster: true));

        // Feld fuellen wie mitten im Lauf.
        for (int k = 0; k < 10; k++) { Burst(EnemyId.Marshmello, 25, cluster: false); Drain(1000); yield return null; }
        yield return Measure("Leerlauf mit ~" + Enemy.Alive.Count, 120, null);

        yield return Measure("Burst 60 Muffin Cluster bei vollem Feld", 120, () => Burst(EnemyId.Muffin, 60, cluster: true));
        yield return Measure("Burst 60 Fetti verteilt bei vollem Feld", 120, () => Burst(EnemyId.Fetti, 60, cluster: false));
        yield return Measure("Kaefig 160 Blocker + 26 Ring", 120, Cage);
        yield return Measure("Massentod 150", 120, () => KillMany(150));
        // Im Spiel laeuft der Todeseffekt (0,5 s) bei 60 FPS in 30 Frames ab -
        // hier sind die Frames viel kuerzer, also echte Zeit abwarten, damit
        // der Pool zurueckbekommt, was er hergegeben hat.
        // Bonbons wie eingesammelt zurueckgeben - ohne XP, sonst pausiert
        // das Level-up-Menue das Spiel und die Messung haengt.
        foreach (ExpPickup candy in Object.FindObjectsByType<ExpPickup>(FindObjectsSortMode.None)) RunPool.Release(candy.gameObject);
        yield return new WaitForSecondsRealtime(1f);
        yield return Measure("Massentod 150 (zweites Mal)", 120, () => KillMany(150));


        Debug.Log("[SpawnHitch]\n" + log);

        AudioListener.pause = false;
        AudioListener.volume = 1f;
    }

    private void Line(string s) { log.AppendLine(s); }

    private void Burst(EnemyId id, int count, bool cluster)
    {
        Vector2 p = player.transform.position;
        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector2 center = p + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 18f;
        float spread = Mathf.Max(1.5f, Mathf.Sqrt(count) * 0.6f);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < count; i++)
        {
            Vector2 pos = cluster
                ? center + Random.insideUnitCircle * spread
                : p + Random.insideUnitCircle.normalized * Random.Range(15f, 22f);
            Spawn(id, pos);
        }
        Line($"   Instantiate x{count} {id}: {sw.Elapsed.TotalMilliseconds:0.0} ms");
    }

    private void Cage()
    {
        Vector2 p = player.transform.position;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 160; i++)
        {
            float a = i / 160f * Mathf.PI * 2f;
            Spawn(EnemyId.Blocker, p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 19f);
        }
        for (int i = 0; i < 26; i++)
        {
            float a = i / 26f * Mathf.PI * 2f;
            Spawn(EnemyId.Marshmello, p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 14f);
        }
        Line($"   Instantiate Kaefig: {sw.Elapsed.TotalMilliseconds:0.0} ms");
    }

    private void KillMany(int n)
    {
        var sw = Stopwatch.StartNew();
        var list = new List<Enemy>();
        foreach (Enemy e in Enemy.Alive) if (e != null && !e.IsBoss && e.Identity != EnemyId.Blocker) list.Add(e);
        int killed = 0;
        foreach (Enemy e in list)
        {
            if (killed >= n) break;
            e.TakeDamage(1e9f, null, 0f);
            killed++;
        }
        Line($"   TakeDamage-Tod x{killed}: {sw.Elapsed.TotalMilliseconds:0.0} ms");
    }

    private readonly Queue<KeyValuePair<EnemyId, Vector2>> queue = new Queue<KeyValuePair<EnemyId, Vector2>>();
    private const int PerFrame = 25;

    private void Spawn(EnemyId id, Vector2 pos)
    {
        queue.Enqueue(new KeyValuePair<EnemyId, Vector2>(id, pos));
    }

    private void Drain(int max)
    {
        while (queue.Count > 0 && max-- > 0) { var q = queue.Dequeue(); SpawnNow(q.Key, q.Value); }
    }

    private void SpawnNow(EnemyId id, Vector2 pos)
    {
        GameObject prefab = catalog.Prefab(id);
        if (prefab == null) return;
        GameObject go = Object.Instantiate(prefab, pos, Quaternion.identity);
        RunScene.Place(go, "Gegner");
        Enemy e = go.GetComponent<Enemy>();
        if (e != null)
        {
            e.RunThreat = SpawnCatalog.Threat(id);
            e.CanRecycle = id != EnemyId.Blocker;
            e.SpawnedAs = id;
            RunDifficulty.Apply(e);
        }
    }

    private IEnumerator Measure(string label, int frames, System.Action action)
    {
        var recorders = new List<ProfilerRecorder>();
        foreach (string m in Markers) recorders.Add(ProfilerRecorder.StartNew(ProfilerCategory.Scripts, m, 4));
        var gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 4);

        Line($"== {label} (alive vorher {Enemy.Alive.Count})");
        action?.Invoke();
        Drain(PerFrame);

        var times = new List<float>();
        var sw = Stopwatch.StartNew();
        float worst = 0f;
        string worstInfo = "";
        double prev = 0;
        int prevSteps = counter.steps;
        int prevGc = System.GC.CollectionCount(0);

        for (int f = 0; f < frames; f++)
        {
            yield return null;
            Drain(PerFrame);
            double now = sw.Elapsed.TotalMilliseconds;
            float ms = (float)(now - prev);
            prev = now;
            int steps = counter.steps - prevSteps;
            prevSteps = counter.steps;
            int gcs = System.GC.CollectionCount(0) - prevGc;
            prevGc = System.GC.CollectionCount(0);
            times.Add(ms);

            if (f < 3 || ms > 25f)
            {
                var sb = new StringBuilder();
                for (int r = 0; r < Markers.Length; r++)
                {
                    long v = recorders[r].LastValue;
                    if (v > 500000) sb.Append($" {Markers[r]}={v / 1e6:0.0}");
                }
                Line($"   f{f}: {ms:0.0} ms, fixed x{steps}, dt {Time.deltaTime * 1000:0}, alive {Enemy.Alive.Count}, GC x{gcs}, alloc {gcAlloc.LastValue / 1024} KB{sb}");
            }
            if (ms > worst) { worst = ms; worstInfo = $"f{f}"; }
        }

        times.Sort();
        Line($"   -> max {worst:0.0} ms ({worstInfo}), median {times[times.Count / 2]:0.0} ms, p95 {times[(int)(times.Count * 0.95f)]:0.0} ms");

        foreach (var r in recorders) r.Dispose();
        gcAlloc.Dispose();
    }
}
#endif
