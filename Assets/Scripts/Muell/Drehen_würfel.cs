/*using UnityEngine;
using System.Collections;
using TMPro;

public class Drehen_würfel : MonoBehaviour
{
    public static Drehen_würfel Instance;
    public Transform parentTransform;
    public Transform childTransform;
    public float rotationSpeed = 200f;
    public float moveAmount = 7.8125f; // X-Verschiebung pro Kipp
    public float delayBetweenRolls = 0.1f;
    public bool rotating;
    public bool eins;
    public bool zwei;
    public bool drei;
    public bool vier;
    public bool richtung = true;
    public int richtungswechsel = 0;

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

    void Start()
    {
        eins = true;
    }

    void Update()
    {
        Debug.Log(transform.position);
        if (richtungswechsel == 1)
        {
            richtung = false;
            rotating = false;
            richtungswechsel = 2;

        }
        else if (richtungswechsel == 0)
        {
            richtung = true;
            rotating = true;
            richtungswechsel = 2;
        }
        if (richtung)
        {
            //Debug.Log("Right");
            Rotation_Right();
        }
        else
        {
            Debug.Log("Left");
            Rotation_Left();
        }
    }
    public void Rotation_Right()
    {
        if (rotating && (eins || zwei || drei || vier))
        {
            // aktuelle Z-Rotation holen
            float z = transform.eulerAngles.z;

            if (z >= 270f - 0.5f && z <= 270f + 0.5f && eins)
            {
                eins = false;
                rotating = false; // Stoppen
                zwei = true;
            }
            else if (z >= 180f - 0.5f && z <= 180f + 0.5f && zwei)
            {
                zwei = false;
                rotating = false;
                drei = true;
            }
            else if (z >= 90f - 0.5f && z <= 90f + 0.5f && drei)
            {
                drei = false;
                rotating = false;
                vier = true;
            }
            else if (z >= 0f - 0.5f && z <= 0f + 0.5f && vier)
            {
                vier = false;
                rotating = false;
                eins = true;
            }
        }
        if (!rotating)
        {
            // aktuelle Z-Rotation holen
            float z = transform.eulerAngles.z;

            // -90° entspricht 270° in Unity
            if (z >= 270f - 0.5f && z <= 270f + 0.5f && zwei)
            {
                transform.rotation = Quaternion.Euler(0f, 0f, 270f);
                Transform oldParent = childTransform.parent;
                childTransform.parent = null;              // Parent entfernen
                parentTransform.position = new Vector3(parentTransform.position.x + 7.8125f, parentTransform.position.y, parentTransform.position.z);
                childTransform.parent = oldParent;         // Child wieder anhängen
                rotating = true;
            }
            else if (z >= 180f - 0.5f && z <= 180f + 0.5f && drei)
            {
                transform.rotation = Quaternion.Euler(0f, 0f, 180f);
                Transform oldParent = childTransform.parent;
                childTransform.parent = null;              // Parent entfernen
                parentTransform.position = new Vector3(parentTransform.position.x + 7.8125f, parentTransform.position.y, parentTransform.position.z);
                childTransform.parent = oldParent;         // Child wieder anhängen
                rotating = true;
            }
            else if (z >= 90f - 0.5f && z <= 90f + 0.5f && vier)
            {
                transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                Transform oldParent = childTransform.parent;
                childTransform.parent = null;              // Parent entfernen
                parentTransform.position = new Vector3(parentTransform.position.x + 7.8125f, parentTransform.position.y, parentTransform.position.z);
                childTransform.parent = oldParent;         // Child wieder anhängen
                rotating = true;
            }
            else if (z >= 0f - 0.5f && z <= 0f + 0.5f && eins)
            {
                transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                Transform oldParent = childTransform.parent;
                childTransform.parent = null;              // Parent entfernen
                parentTransform.position = new Vector3(parentTransform.position.x + 7.8125f, parentTransform.position.y, parentTransform.position.z);
                childTransform.parent = oldParent;         // Child wieder anhängen
                rotating = true;
            }
        }
        transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
    }
    public void Rotation_Left()
    {
        if (rotating && (eins || zwei || drei || vier))
        {
            // aktuelle Z-Rotation holen
            float z = transform.eulerAngles.z;

            if (z >= 270f - 0.5f && z <= 270f + 0.5f && zwei)
            {
                zwei = false;
                rotating = false; // Stoppen
                eins = true;
            }
            else if (z >= 180f - 0.5f && z <= 180f + 0.5f && drei)
            {
                drei = false;
                rotating = false;
                zwei = true;
            }
            else if (z >= 90f - 0.5f && z <= 90f + 0.5f && vier)
            {
                vier = false;
                rotating = false;
                drei = true;
            }
            else if (z >= 0f - 0.5f && z <= 0f + 0.5f && eins)
            {
                eins = false;
                rotating = false;
                vier = true;
            }
        }
        if (!rotating)
        {
            // aktuelle Z-Rotation holen
            float z = transform.eulerAngles.z;

            // -90° entspricht 270° in Unity
            if (z >= 270f - 0.5f && z <= 270f + 0.5f && zwei)
            {
                transform.rotation = Quaternion.Euler(0f, 0f, 270f);
                Transform oldParent = childTransform.parent;
                childTransform.parent = null;              // Parent entfernen
                parentTransform.position = new Vector3(parentTransform.position.x - 7.8125f, parentTransform.position.y, parentTransform.position.z);
                childTransform.parent = oldParent;         // Child wieder anhängen
                rotating = true;
            }
            else if (z >= 180f - 0.5f && z <= 180f + 0.5f && drei)
            {
                transform.rotation = Quaternion.Euler(0f, 0f, 180f);
                Transform oldParent = childTransform.parent;
                childTransform.parent = null;              // Parent entfernen
                parentTransform.position = new Vector3(parentTransform.position.x - 7.8125f, parentTransform.position.y, parentTransform.position.z);
                childTransform.parent = oldParent;         // Child wieder anhängen
                rotating = true;
            }
            else if (z >= 90f - 0.5f && z <= 90f + 0.5f && vier)
            {
                transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                Transform oldParent = childTransform.parent;
                childTransform.parent = null;              // Parent entfernen
                parentTransform.position = new Vector3(parentTransform.position.x - 7.8125f, parentTransform.position.y, parentTransform.position.z);
                childTransform.parent = oldParent;         // Child wieder anhängen
                rotating = true;
            }
            else if (z >= 0f - 0.5f && z <= 0f + 0.5f && eins)
            {
                transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                Transform oldParent = childTransform.parent;
                childTransform.parent = null;              // Parent entfernen
                parentTransform.position = new Vector3(parentTransform.position.x - 7.8125f, parentTransform.position.y, parentTransform.position.z);
                childTransform.parent = oldParent;         // Child wieder anhängen
                rotating = true;
            }
        }
       transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime * -1);
    }
}*/

using UnityEngine;

public class Drehen_würfel : MonoBehaviour
{
    public static Drehen_würfel Instance;

    public Transform parentTransform;
    public Transform childTransform;
    public float rotationSpeed = 200f;
    public float moveAmount = 7.8125f;
    public bool richtung = true;

    int step;
    float targetAngle;
    bool hasTarget = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void Start()
    {
        step = 0;
        targetAngle = 0f;
        hasTarget = true;
    }

    void Update()
    {
        if (!hasTarget) return;

        var current = transform.eulerAngles;
        var currentZ = current.z;
        var newRot = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0f, 0f, targetAngle), rotationSpeed * Time.deltaTime);
        transform.rotation = newRot;

        float delta = Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.z, targetAngle));
        if (delta <= 0.1f)
        {
            transform.rotation = Quaternion.Euler(0f, 0f, targetAngle);

            var oldParent = childTransform.parent;
            childTransform.parent = null;
            var dir = richtung ? 1f : -1f;
            parentTransform.position = new Vector3(parentTransform.position.x + dir * moveAmount, parentTransform.position.y, parentTransform.position.z);
            childTransform.parent = oldParent;

            step = (step + (richtung ? 1 : -1) + 4) % 4;
            targetAngle = step * 90f;
            hasTarget = true;
        }
    }
}
