using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleCameraMotion : MonoBehaviour
{
    void Update()
    {
        transform.position = new Vector3(Mathf.Cos(Time.time)*0.75f, Mathf.Sin(Time.time*0.8f)*0.4f, Mathf.Sin(Time.time)*0.5f) * 2f;
        transform.rotation = Quaternion.LookRotation(-transform.position);
    }
}
