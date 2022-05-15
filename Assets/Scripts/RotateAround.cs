using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateAround : MonoBehaviour
{
    public Vector3 speed;
    void FixedUpdate()
    {
        transform.Rotate(speed);
    }
}
