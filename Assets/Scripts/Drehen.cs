using UnityEngine;
using System.Collections;

public class Drehen : MonoBehaviour
{
    public float rotationSpeed;   // Grad pro Sekunde

    void Update()
    {
        if (PlayerController.Instance.transform.position.x > transform.position.x)
            {
                transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime * -1f);
            }
            else
            {
                transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
            }
    }

}