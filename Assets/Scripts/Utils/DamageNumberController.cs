using System;
using UnityEngine;

public class DamageNumberController : MonoBehaviour
{
    public static DamageNumberController Instance;
    public DamageNumber prefab;
    public DamageNumber prefabCrit;
    public DamageNumber prefabDodge;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
        }
        else
        {
            Instance = this;
        }
    }

    public void CreateNumber(float value, Vector3 location)
    {
        DamageNumber damageNumber = Instantiate(prefab, location, transform.rotation, transform);
        damageNumber.SetValue(Mathf.RoundToInt(value));
    }

    public void CreateNumberCrit(float value, Vector3 location)
    {
        DamageNumber damageNumber = Instantiate(prefabCrit, location, transform.rotation, transform);
        damageNumber.SetValue(Mathf.RoundToInt(value));
    }

    public void CreateDodgeText(Vector3 location)
    {
        DamageNumber damageNumber = Instantiate(prefabDodge, location, transform.rotation, transform);
        damageNumber.SetText("Dodge");
    }
    public void CreateText(string Text, Vector3 location)
    {
        DamageNumber damageNumber = Instantiate(prefab, location, transform.rotation, transform);
        damageNumber.SetText(Text);
    }
}
