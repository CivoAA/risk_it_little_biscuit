using UnityEngine;

/// <summary>
/// Zentraler, gecachter Ersatz für GameObject.Find(...).GetComponent&lt;T&gt;()
/// in den Projektil-Prefabs. Die Suche läuft nur einmal pro Waffen-Typ statt
/// bei jedem gespawnten Projektil, und fehlende Objekte führen zu einer
/// klaren Fehlermeldung statt zu einer NullReferenceException.
/// </summary>
public static class WeaponFinder
{
    public static T Find<T>(string objectName) where T : Component
    {
        // Unitys überladener null-Vergleich sorgt dafür, dass nach einem
        // Szenenwechsel (zerstörtes Objekt) automatisch neu gesucht wird.
        if (Cache<T>.value == null)
        {
            GameObject go = GameObject.Find(objectName);
            if (go != null)
            {
                Cache<T>.value = go.GetComponent<T>();
            }

            if (Cache<T>.value == null)
            {
                Debug.LogError($"WeaponFinder: Objekt '{objectName}' mit Komponente '{typeof(T).Name}' nicht gefunden!");
            }
        }

        return Cache<T>.value;
    }

    private static class Cache<T> where T : Component
    {
        public static T value;
    }
}
