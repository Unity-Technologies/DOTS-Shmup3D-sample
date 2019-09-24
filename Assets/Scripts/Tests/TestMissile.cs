using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Entities;
using Random = Unity.Mathematics.Random;

namespace UTJ {

public class TestMissile : MonoBehaviour
{
    Random random_;

    void Start()
    {
        random_.InitState(12345);
    }

    void Update()
    {
        for (var i = 0; i < 4; ++i) {
            var pos = random_.NextFloat3(-100, 100);
            var dir = random_.NextFloat3Direction();
            var rot = quaternion.LookRotation(dir, new float3(0, 1, 0));
            MissileSystem.Instantiate(MissileManager.Prefab, pos, rot);
        }
    }
}

} // namespace UTJ {
