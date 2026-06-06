using UnityEngine;
using System.Collections;

public class DrehenWaffen : MonoBehaviour
{
    public float rotationSpeed;   // Grad pro Sekunde

    void Update()
    {
                transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
    }

}