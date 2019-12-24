using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using Random = Unity.Mathematics.Random;

namespace UTJ {

public struct TestTrailRaycasterComponent : IComponentData
{
    public float3 Direction;
}

public class TestTrailWithRaycast : MonoBehaviour
{
    void Start()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
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
            var entity = em.CreateEntity(typeof(Translation),
                                         typeof(Rotation),
                                         typeof(TestTrailRaycasterComponent));
#if UNITY_EDITOR
            em.SetName(entity, "trailRoot");
#endif
            em.SetComponentData(entity, new TestTrailRaycasterComponent { Direction = random.NextFloat3Direction(), });
            var col = coltbl[(i/1023)%coltbl.Length];
            TrailSystem.Instantiate(entity, float3.zero, 0.02f /* width */, col,
                                    float3.zero, 1f/60f /* update_interval */);
        }
    }
}

public class TestTrailRaycastSystem : JobComponentSystem
{
    EntityQuery _query;
    
    protected override void OnCreate()
    {
        _query = GetEntityQuery(new EntityQueryDesc() {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<TestTrailRaycasterComponent>(),
                    ComponentType.ReadWrite<Translation>(),
                },
            });
    }

    [BurstCompile]
    struct MyJob : IJobChunk
    {
        public Unity.Physics.CollisionWorld world;
        public RigidTransform transform;
        [ReadOnly] public ArchetypeChunkComponentType<TestTrailRaycasterComponent> RaycasterType;
        public ArchetypeChunkComponentType<Translation> TranslationType;
        
        public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            var chunkRaycasters = chunk.GetNativeArray(RaycasterType);
            var chunkTranslations = chunk.GetNativeArray(TranslationType);
            for (var i = 0; i < chunk.Count; ++i)
            {
                ref var raycaster = ref chunkRaycasters.AsReadOnlyRef(i);
                ref var translation = ref chunkTranslations.AsWritableRef(i);
                var pos = math.transform(transform, raycaster.Direction)* 100f;
                var input = new Unity.Physics.RaycastInput {
                    Start = pos,
                    End = transform.pos,
                    Filter = new Unity.Physics.CollisionFilter
                    {
                        BelongsTo = ~0u,
                        CollidesWith = ~0u,
                        GroupIndex = 0,
                    },
                };
                bool hitted = world.CastRay(input, out Unity.Physics.RaycastHit hit);
                if (hitted)
                {
                    translation.Value = hit.Position + math.normalize(hit.Position)*0.02f;
                }
                else
                    translation.Value = transform.pos;
            }
        }
    }

    quaternion _rot = quaternion.identity;

    protected override JobHandle OnUpdate(JobHandle handle)
    {
        var physicsWorldSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystem<Unity.Physics.Systems.BuildPhysicsWorld>();
        var dt = 1f/60f;
        var time = UnityEngine.Time.time;
        var euler = new float3(math.sin(time*2f)*2, math.cos(time*2.4f)*1, math.cos(time)*3) * dt;
        _rot = math.mul(_rot, quaternion.Euler(euler));
        var job = new MyJob {
            world = physicsWorldSystem.PhysicsWorld.CollisionWorld,
            transform = new RigidTransform { pos = float3.zero, rot = _rot, },
            RaycasterType = GetArchetypeChunkComponentType<TestTrailRaycasterComponent>(true /* readOnly */),
            TranslationType = GetArchetypeChunkComponentType<Translation>(false /* readOnly */),
        };
        handle = job.Schedule(_query, handle);

        return handle;
    }
}

} // namespace UTJ {
