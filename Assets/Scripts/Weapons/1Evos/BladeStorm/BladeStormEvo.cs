using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BladeStormEvo : Weapon
{
    [SerializeField] private GameObject prefab;
    public List<Enemy> enemiesInRange = new List<Enemy>();

    private enum BladeState { Orbit, Outbound, Inbound }

    private class BladeData
    {
        public GameObject obj;
        public BladeState state;
    }

    private List<BladeData> blades = new List<BladeData>();
    private Enemy currentTarget;
    private Vector2 lastTargetPos;
    private float attackCounter;

    private int currentMaxBlades = 0;
    private float bladeHealthCheckTimer = 0f; // Sicherheitsprüfungstimer

    void Update()
    {
        blades.RemoveAll(b => b.obj == null);
        enemiesInRange.RemoveAll(e => e == null);

        if (weaponLevel < 0) return;

        // Achievment Unlocken
        AchievementManager.Instance.UnlockAchievement("Blade_Swarm_Evo");

        // maximale Anzahl anhand der aktuellen Werte berechnen
        int desiredBladeCount = Mathf.RoundToInt(stats[weaponLevel].shots + PlayerController.Instance.playerShots);
        if (desiredBladeCount > 9) desiredBladeCount = 9;

        // Falls mehr Blades benötigt werden -> nachspawnen
        if (desiredBladeCount > currentMaxBlades)
        {
            int toSpawn = desiredBladeCount - currentMaxBlades;
            for (int i = 0; i < toSpawn; i++)
            {
                GameObject bladeObj = Instantiate(prefab, transform.position, Quaternion.identity, transform);
                blades.Add(new BladeData { obj = bladeObj, state = BladeState.Orbit });
            }
            currentMaxBlades = desiredBladeCount;
            PositionBlades();
        }

        attackCounter -= Time.deltaTime;
        PositionBlades();

        if (currentTarget != null)
            lastTargetPos = currentTarget.transform.position;

        if (attackCounter <= 0f && AllBladesInOrbit())
        {
            attackCounter = stats[weaponLevel].cooldown;
            UpdateTarget();
            if (currentTarget != null)
            {
                lastTargetPos = currentTarget.transform.position;
                StartCoroutine(FireBladesSequentially());
            }
        }

        // 🔍 Sicherheitsprüfung: hängen gebliebene Blades erkennen
        bladeHealthCheckTimer += Time.deltaTime;
        if (bladeHealthCheckTimer >= 3f)
        {
            bladeHealthCheckTimer = 0f;

            bool stuckBlades = false;

            foreach (var b in blades)
            {
                if (b.obj == null)
                {
                    stuckBlades = true;
                    break;
                }

                if (b.state != BladeState.Orbit)
                {
                    float distToPlayer = Vector2.Distance(b.obj.transform.position, transform.position);
                    if (distToPlayer > 40f)
                    {
                        stuckBlades = true;
                        break;
                    }
                }
            }

            if (stuckBlades)
            {
                Debug.LogWarning("⚠️ BladeStormEvo: Blades hängen fest → Respawn ausgelöst!");
                ResetAllBlades();
            }
        }
    }

    private bool AllBladesInOrbit()
    {
        return blades.TrueForAll(b => b.state == BladeState.Orbit);
    }

    private void PositionBlades()
    {
        float baseRadius = 1f;
        int orbitIndex = 0;

        for (int i = 0; i < blades.Count; i++)
        {
            var blade = blades[i];
            if (blade.state != BladeState.Orbit || blade.obj == null) continue;

            Vector2 pos = transform.position;

            switch (orbitIndex)
            {
                case 0: pos += (Vector2.left + Vector2.up * 0.4f) * baseRadius; break;
                case 1: pos += (Vector2.right + Vector2.up * 0.4f) * baseRadius; break;
                case 2: pos += Vector2.up * baseRadius * 1.4f; break;
                case 3: pos += (Vector2.left + Vector2.up * 0.8f) * baseRadius; break;
                case 4: pos += (Vector2.right + Vector2.up * 0.8f) * baseRadius; break;
                case 5: pos += (Vector2.left * 0.7f + Vector2.up * 1.3f) * (baseRadius * 0.8f); break;
                case 6: pos += (Vector2.right * 0.7f + Vector2.up * 1.3f) * (baseRadius * 0.8f); break;
                case 7: pos += (Vector2.left * 0.7f + Vector2.down * 0.25f) * (baseRadius * 0.8f); break;
                case 8: pos += (Vector2.right * 0.7f + Vector2.down * 0.25f) * (baseRadius * 0.8f); break;
                default: pos += Vector2.up * (baseRadius * 0.5f); break;
            }

            blade.obj.transform.position = pos;
            blade.obj.transform.rotation = Quaternion.Euler(0f, 0f, 90);
            orbitIndex++;
        }
    }

    private void UpdateTarget()
    {
        enemiesInRange.RemoveAll(e => e == null);
        if (enemiesInRange.Count == 0)
        {
            currentTarget = null;
            return;
        }

        int randomIndex = Random.Range(0, enemiesInRange.Count);
        currentTarget = enemiesInRange[randomIndex];
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
            enemiesInRange.Add(collider.GetComponent<Enemy>());
    }

    private void OnTriggerExit2D(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
            enemiesInRange.Remove(collider.GetComponent<Enemy>());
    }

    private IEnumerator FireBladesSequentially()
    {
        foreach (var blade in blades)
        {
            if (blade.state != BladeState.Orbit || blade.obj == null) continue;

            blade.obj.transform.SetParent(null);
            blade.state = BladeState.Outbound;

            StartCoroutine(FlyOutbound(blade));
            yield return new WaitForSeconds(0.1f);
        }
    }

    private IEnumerator FlyOutbound(BladeData blade)
    {
        float outboundSpeed = 20f;
        float flightTime = 0f;

        Vector2 dirToTarget = ((currentTarget != null ? (Vector2)currentTarget.transform.position : lastTargetPos) - (Vector2)transform.position).normalized;
        Vector2 controlOffset = dirToTarget * 8f;

        while (blade.obj != null && blade.state == BladeState.Outbound)
        {
            flightTime += Time.deltaTime;

            // Gegner tot? → trotzdem stabile Richtung beibehalten
            if (currentTarget == null)
                lastTargetPos = transform.position + (Vector3)dirToTarget * 5f;

            Vector2 targetPos = lastTargetPos + dirToTarget * 1.5f;
            Vector2 dir = (targetPos - (Vector2)blade.obj.transform.position).normalized;
            blade.obj.transform.position += (Vector3)(dir * outboundSpeed * Time.deltaTime);

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            blade.obj.transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);

            // Sofortiger Rückflug, wenn Gegner weg oder zu weit entfernt
            if (currentTarget == null && Vector2.Distance(blade.obj.transform.position, transform.position) > 10f)
            {
                blade.state = BladeState.Inbound;
                StartCoroutine(FlyInbound(blade, lastTargetPos, controlOffset));
                yield break;
            }

            if (Vector2.Distance(blade.obj.transform.position, targetPos) < 0.15f || flightTime > 2f)
            {
                blade.state = BladeState.Inbound;
                StartCoroutine(FlyInbound(blade, lastTargetPos, controlOffset));
                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator FlyInbound(BladeData blade, Vector2 targetPos, Vector2 controlOffset)
    {
        float returnSpeed = 2f;
        float t = 0f;
        float inboundTimer = 0f;

        // ✅ Endpunkt beim Start fixieren (nicht beweglich)
        Vector2 fixedEnd = transform.position;
        Vector2 bezierStart = blade.obj != null ? blade.obj.transform.position : transform.position;

        while (blade.obj != null && blade.state == BladeState.Inbound && t < 1f)
        {
            t += Time.deltaTime * returnSpeed;
            inboundTimer += Time.deltaTime;

            Vector2 control = targetPos + controlOffset;
            Vector2 pos = Bezier(bezierStart, control, fixedEnd, t);
            blade.obj.transform.position = pos;

            Vector2 dir = (fixedEnd - pos).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            blade.obj.transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);

            if (inboundTimer > 2.5f)
                break;

            yield return null;
        }

        if (blade.obj != null)
        {
            blade.obj.transform.SetParent(transform, true);
            blade.state = BladeState.Orbit;
            blade.obj.transform.rotation = Quaternion.identity;
            PositionBlades();
        }
    }

    private Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        Vector2 ab = Vector2.Lerp(a, b, t);
        Vector2 bc = Vector2.Lerp(b, c, t);
        return Vector2.Lerp(ab, bc, t);
    }

    // 🔁 Sicherheits-Reset, wenn Blades hängen bleiben
    private void ResetAllBlades()
    {
        foreach (var b in blades)
        {
            if (b.obj != null)
                Destroy(b.obj);
        }
        blades.Clear();

        currentMaxBlades = 0;

        int desiredBladeCount = Mathf.RoundToInt(stats[weaponLevel].shots + PlayerController.Instance.playerShots);
        if (desiredBladeCount > 9) desiredBladeCount = 9;

        for (int i = 0; i < desiredBladeCount; i++)
        {
            GameObject bladeObj = Instantiate(prefab, transform.position, Quaternion.identity, transform);
            blades.Add(new BladeData { obj = bladeObj, state = BladeState.Orbit });
        }

        currentMaxBlades = desiredBladeCount;
        PositionBlades();
    }
}
