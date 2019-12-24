using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Entities;
using Random = Unity.Mathematics.Random;

namespace UTJ {

public class TestBeam : MonoBehaviour
{
    Random random_;

    void test_fire()
    {
        var pos = Vector3.zero;
        for (var i = 0; i < 10; ++i)
        {
            var vel = random_.NextFloat3Direction() * 32f;
            BeamSystem.Instantiate(World.DefaultGameObjectInjectionWorld.EntityManager, BeamManager.Prefab, pos, vel, Time.GetCurrent());
        }
    }

    void Start()
    {
        random_.InitState(12345);
    }

    void Update()
    {
        // if (random_.NextFloat(1f) < 0.1f)
        test_fire();
    }
}

} // namespace UTJ {
