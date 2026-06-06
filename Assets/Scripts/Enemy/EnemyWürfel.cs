/*using UnityEngine;

public class EnemyWürfel : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;

    void Update()
    {
        //Debug.Log(PlayerController.Instance.transform.position.x);
        //Debug.Log(transform.position.x);
        if (PlayerController.Instance.transform.position.x >= transform.position.x + 10f)
        {
            if (Drehen_würfel.Instance.rotating)
            {
                if (Drehen_würfel.Instance.eins)
                {
                    Drehen_würfel.Instance.zwei = true;
                    Drehen_würfel.Instance.eins = false;
                }
                else if (Drehen_würfel.Instance.zwei)
                {
                    Drehen_würfel.Instance.zwei = false;
                    Drehen_würfel.Instance.drei = true;
                }
                else if (Drehen_würfel.Instance.drei)
                {
                    Drehen_würfel.Instance.vier = true;
                    Drehen_würfel.Instance.drei = false;
                }
                else if (Drehen_würfel.Instance.vier)
                {
                    Drehen_würfel.Instance.eins = true;
                    Drehen_würfel.Instance.vier = false;
                }
                Drehen_würfel.Instance.richtungswechsel = 0;
                Debug.Log("R");
            }

        }
        else if (PlayerController.Instance.transform.position.x <= transform.position.x - 10f)
        {
            if (Drehen_würfel.Instance.rotating)
            {
                Drehen_würfel.Instance.richtungswechsel = 1;
                Debug.Log("L");
            }

        } 
    }
}*/

using UnityEngine;

public class EnemyWürfel : MonoBehaviour
{
    void Update()
    {
        float px = PlayerController.Instance.transform.position.x;
        float x = transform.position.x;

        if (px >= x + 10f) Drehen_würfel.Instance.richtung = true;
        else if (px <= x - 10f) Drehen_würfel.Instance.richtung = false;
    }
}
