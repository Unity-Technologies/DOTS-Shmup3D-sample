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

namespace UTJ {

[UpdateBefore(typeof(CollisionSystem))]
[UpdateInGroup(typeof(LateSimulationSystemGroup))]
public class GroundHeightSystem : JobComponentSystem
{
    EntityQuery _query;
    BuildPhysicsWorld _buildPhysicsWorldSystem;
    CollisionSystem _collisionSystem;

    protected override void OnCreate()
    {
        _query = GetEntityQuery(new EntityQueryDesc() {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<GroundHeightInfoComponent>(),
                    ComponentType.ReadWrite<GroundHeightComponent>(),
                },
            });
        _buildPhysicsWorldSystem = World.GetOrCreateSystem<BuildPhysicsWorld>();
        _collisionSystem = World.GetOrCreateSystem<CollisionSystem>();
    }

    [BurstCompile]
    struct Job : IJobChunk
    {
        [ReadOnly] public CollisionWorld MCollisionWorld;
        [ReadOnly] public ArchetypeChunkComponentType<Translation> TranslationType;
        [ReadOnly] public ArchetypeChunkComponentType<Rotation> RotationType;
        [ReadOnly] public ArchetypeChunkComponentType<GroundHeightInfoComponent> GroundHeightInfoComponentType;
        public ArchetypeChunkComponentType<GroundHeightComponent> GroundHeightComponentType;
        
        public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            var chunkTranslations = chunk.GetNativeArray(TranslationType);
            var chunkRotations = chunk.GetNativeArray(RotationType);
            var chunkGroundHeightInfos = chunk.GetNativeArray(GroundHeightInfoComponentType);
            var chunkGroundHeights = chunk.GetNativeArray(GroundHeightComponentType);
            for (var i = 0; i < chunk.Count; ++i) {
                var translation = chunkTranslations[i].Value;
                var rotation = chunkRotations[i].Value;
                var heightInfo = chunkGroundHeightInfos[i];
                var pos = math.mul(rotation, heightInfo.Position) + translation;
                bool hitted = Utility.RaycastGround(MCollisionWorld, pos, out var hitPos);
                float height = hitted ? translation.y - hitPos.y : float.MaxValue;
                chunkGroundHeights[i] = new GroundHeightComponent {
                    Height = height,
                };
            }
        }
    }

	protected override JobHandle OnUpdate(JobHandle handle)
	{
        var job = new Job {
            MCollisionWorld = _buildPhysicsWorldSystem.PhysicsWorld.CollisionWorld,
            TranslationType = GetArchetypeChunkComponentType<Translation>(true /* isReadOnly */),
            RotationType = GetArchetypeChunkComponentType<Rotation>(true /* isReadOnly */),
            GroundHeightInfoComponentType = GetArchetypeChunkComponentType<GroundHeightInfoComponent>(true /* isReadOnly */),
            GroundHeightComponentType = GetArchetypeChunkComponentType<GroundHeightComponent>(false /* isReadOnly */),
        };
        handle = job.Schedule(_query, handle);

        _collisionSystem.AddDependingJobHandle(handle);

        return handle;
    }
}

} // namespace UTJ {
