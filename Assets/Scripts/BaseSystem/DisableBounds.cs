using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DisableBounds : MonoBehaviour
{
    void Start()
    {
        var mf = GetComponent<MeshFilter>();
        var mesh = mf.sharedMesh;
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 99999999f);
    }
}
