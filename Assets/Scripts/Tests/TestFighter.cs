using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Entities;
using Random = Unity.Mathematics.Random;

namespace UTJ {

public class TestFighter : MonoBehaviour
{
    Random random_;

    void Start()
    {
        random_.InitState(1234);
        int spawnnum = SceneManager.Num;
        bool first = true;
        while (spawnnum > 0)
        {
            var num = random_.NextInt(3, 5);
            var center = new float3(random_.NextFloat(-200, 200), 64f, random_.NextFloat(-200, 200));
            var replay_index_center = random_.NextInt(100, 10000);
            for (var j = 0; j < num; ++j)
            {
                var pos = center + random_.NextFloat3(-6, 6);
                var replay_index = replay_index_center + random_.NextInt(20, 40) * (random_.NextBool() ? 1 : -1);
                var entity = FighterSystem.Instantiate(FighterManager.Prefab,
                                                       pos,
                                                       quaternion.identity,
                                                       replay_index);
                if (first)
                {
                    var fighterSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<FighterSystem>();
                    fighterSystem.PrimaryEntity = entity;
                    first = false;
                }
                --spawnnum;
                if (spawnnum > 0)
                    continue;
                break;
            }
        }
    }
}

} // namespace UTJ {
