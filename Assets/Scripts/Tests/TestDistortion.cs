using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Entities;
using Random = Unity.Mathematics.Random;

namespace UTJ {

public class TestDistortion : MonoBehaviour
{
    Random random_;

    void test_distortion()
    {
        for (var i = 0; i < 1; ++i)
        {
            var pos = random_.NextFloat3Direction() * 4f;
            DistortionSystem.Instantiate(World.DefaultGameObjectInjectionWorld.EntityManager,
                                         DistortionManager.Prefab,
                                         pos,
                                         10f /* period */,
                                         1f /* size */);
        }
    }

    void Start()
    {
        random_.InitState(12345);
    }

    void Update()
    {
        if (random_.NextFloat(1f) < 0.1f)
            test_distortion();
    }
}

} // namespace UTJ {
