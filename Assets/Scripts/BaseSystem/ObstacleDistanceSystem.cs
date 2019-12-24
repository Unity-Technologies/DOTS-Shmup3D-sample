using UnityEngine;
using UnityEngine.Assertions;
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Burst;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Rendering;
using RaycastHit = Unity.Physics.RaycastHit;

namespace UTJ {

[UpdateBefore(typeof(CollisionSystem))]
[UpdateInGroup(typeof(LateSimulationSystemGroup))]
public class ObstacleDistanceSystem : JobComponentSystem
{
    EntityQuery _query;
    BuildPhysicsWorld _buildPhysicsWorldSystem;
    CollisionSystem _collisionSystem;

    protected override void OnCreate()
    {
        _query = GetEntityQuery(new EntityQueryDesc() {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<ObstacleDistanceComponent>(),
                },
            });
        _buildPhysicsWorldSystem = World.GetOrCreateSystem<BuildPhysicsWorld>();
        _collisionSystem = World.GetOrCreateSystem<CollisionSystem>();
    }

    [BurstCompile]
    struct MyJob : IJobChunk
    {
        [ReadOnly] public CollisionWorld MCollisionWorld;
        [ReadOnly] public ArchetypeChunkComponentType<Translation> TranslationType;
        [ReadOnly] public ArchetypeChunkComponentType<Rotation> RotationType;
        [ReadOnly] public ArchetypeChunkComponentType<ObstacleDistanceSettingComponent> ObstacleDistanceSettingComponentType;
        public ArchetypeChunkComponentType<ObstacleDistanceComponent> ObstacleDistanceComponentType;
        
        public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            var chunkTranslations = chunk.GetNativeArray(TranslationType);
            var chunkRotations = chunk.GetNativeArray(RotationType);
            var chunkObstacleDistanceSettings = chunk.GetNativeArray(ObstacleDistanceSettingComponentType);
            var chunkObstacleDistances = chunk.GetNativeArray(ObstacleDistanceComponentType);
            for (var i = 0; i < chunk.Count; ++i) {
                var translation = chunkTranslations[i].Value;
                var rotation = chunkRotations[i].Value;
                var setting = chunkObstacleDistanceSettings[i];
                ref var od = ref chunkObstacleDistances.AsWritableRef(i);
                var ray = new RaycastInput {
                    Start = translation,
                    End = translation + math.mul(rotation, new float3(0, 0, 10000)),
                    Filter = setting.Filter,
                };
                bool hitted = MCollisionWorld.CastRay(ray, out var hit);
                float distance = float.MaxValue;
                if (hitted) {
                    var diff = hit.Position - translation;
                    distance = math.length(diff);
                }
                od.Distance = distance;
            }
        }
    }

	protected override JobHandle OnUpdate(JobHandle handle)
	{
        var job = new MyJob {
            MCollisionWorld = _buildPhysicsWorldSystem.PhysicsWorld.CollisionWorld,
            TranslationType = GetArchetypeChunkComponentType<Translation>(true /* isReadOnly */),
            RotationType = GetArchetypeChunkComponentType<Rotation>(true /* isReadOnly */),
            ObstacleDistanceSettingComponentType = GetArchetypeChunkComponentType<ObstacleDistanceSettingComponent>(true /* isReadOnly */),
            ObstacleDistanceComponentType = GetArchetypeChunkComponentType<ObstacleDistanceComponent>(false /* isReadOnly */),
        };
        handle = job.Schedule(_query, handle);

        _collisionSystem.AddDependingJobHandle(handle);

        return handle;
    }
}

} // namespace UTJ {
