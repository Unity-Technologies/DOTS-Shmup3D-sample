using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace UTJ {

public struct TestTrailComponent : IComponentData
{
    public float3 LocalVector;
}


public class TestTrail : MonoBehaviour
{
    void Start()
    {
        var em = World.Active.EntityManager;
        var random = new Random(12345);
        const int NUM = 1023*2;
        var coltbl = new Color[] {
            Color.HSVToRGB(0.0f, 1f, 1f),
            Color.HSVToRGB(0.1f, 1f, 1f),
            Color.HSVToRGB(0.2f, 1f, 1f),
            Color.HSVToRGB(0.3f, 1f, 1f),
            Color.HSVToRGB(0.4f, 1f, 1f),
            Color.HSVToRGB(0.5f, 1f, 1f),
            Color.HSVToRGB(0.6f, 1f, 1f),
            Color.HSVToRGB(0.7f, 1f, 1f),
            Color.HSVToRGB(0.8f, 1f, 1f),
            Color.HSVToRGB(0.9f, 1f, 1f),
        };
        for (var i = 0; i < NUM; ++i)
        {
            var entity = em.CreateEntity(ComponentType.ReadWrite<Translation>(),
                                         ComponentType.ReadWrite<Rotation>(),
                                        ComponentType.ReadWrite<TestTrailComponent>());
            #if UNITY_EDITOR
            em.SetName(entity, "testTrail");
            #endif
            em.SetComponentData(entity, new TestTrailComponent { LocalVector = random.NextFloat3Direction(), });
            //var col = coltbl[(i/1023)%coltbl.Length];
            var col = i < 1024 ? Color.red : Color.white;
            col.a = 0.1f;
            TrailSystem.Instantiate(entity, float3.zero, 0.2f /* width */, col,
                                    float3.zero, 1f/60f /* update_interval */);
        }
    }
}

public class TestTrailSystem : ComponentSystem
{
    EntityQuery _query;
    
    protected override void OnCreate()
    {
        _query = GetEntityQuery(new EntityQueryDesc() {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<TestTrailComponent>(),
                },
            });
    }

    protected override void OnUpdate()
    {
        var time = UnityEngine.Time.time*8f;
        Entities.ForEach((ref Translation translation, ref TestTrailComponent testtrail) => {
                var pos = math.mul(quaternion.Euler(time*0.1f, time*0.2f, time*0.3f), testtrail.LocalVector) * 10f;
                translation = new Translation { Value = pos, };
            });
    }
}

} // namespace UTJ {
