using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Assertions;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Burst;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Rendering;
using UnityEngine.Serialization;
using Random = Unity.Mathematics.Random;

namespace UTJ {

[RequiresEntityConversion]
public class MissileManager : MonoBehaviour, IDeclareReferencedPrefabs, IConvertGameObjectToEntity
{
    public GameObject prefab;
    static Entity _prefabEntity;
    public static Entity Prefab => _prefabEntity;
    Random _random;

    public MissileManager(Random random)
    {
        _random = random;
    }

    public void DeclareReferencedPrefabs(List<GameObject> gameObjects)
    {
        gameObjects.Add(prefab);
    }

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        _prefabEntity = conversionSystem.GetPrimaryEntity(prefab);
        _random.InitState(12345);
    }
}

public class MissileCollisionSystem : JobComponentSystem
{
    BeginInitializationEntityCommandBufferSystem _entityCommandBufferSystem;
    EntityQuery _query;

    protected override void OnCreate()
    {
        _entityCommandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
        _query = GetEntityQuery(new EntityQueryDesc() {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<MissileComponent>(),
                    ComponentType.ReadOnly<CollisionInfoComponent>(),
                },
            });
    }        

    protected override void OnDestroy()
    {
    }

    [BurstCompile]
    struct MyJob : IJobChunk
    {
        public EntityCommandBuffer.Concurrent CommandBuffer;
        [ReadOnly] public ArchetypeChunkEntityType EntityType;
        [ReadOnly] public ArchetypeChunkComponentType<Translation> TranslationType;
        [ReadOnly] public ArchetypeChunkComponentType<CollisionInfoComponent> InfoType;
        public Entity ExplosionPrefab;
        public float Time;

        public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            var entities = chunk.GetNativeArray(EntityType);
            var chunkTranslations = chunk.GetNativeArray(TranslationType);
            var chunkInfos = chunk.GetNativeArray(InfoType);
            for (var i = 0; i < chunk.Count; ++i) {
                ref var info = ref chunkInfos.AsReadOnlyRef(i);
                if (info.HitGeneration != 0)
                {
                    var entity = entities[i];
                    CommandBuffer.DestroyEntity(chunkIndex, entity);
                    ref var translation = ref chunkTranslations.AsReadOnlyRef(i);
                    ExplosionSystem.Instantiate(CommandBuffer, chunkIndex /* jobIndex */, translation.Value, ExplosionPrefab, Time);
                }
            }
        }
    }
    
	protected override unsafe JobHandle OnUpdate(JobHandle handle)
	{
        var commandBuffer = _entityCommandBufferSystem.CreateCommandBuffer().ToConcurrent();
        var job = new MyJob {
            CommandBuffer = commandBuffer,
            EntityType = GetArchetypeChunkEntityType(),
            TranslationType = GetArchetypeChunkComponentType<Translation>(true /* isReadOnly */),
            InfoType = GetArchetypeChunkComponentType<CollisionInfoComponent>(true /* isReadOnly */),
            ExplosionPrefab = ExplosionSystem.PrefabEntity,
            Time = (float)UTJ.Time.GetCurrent(),
        };
        handle = job.Schedule(_query, handle);
        _entityCommandBufferSystem.AddJobHandleForProducer(handle);
        return handle;
    }
}

public class MissileSystem : JobComponentSystem
{
    BeginInitializationEntityCommandBufferSystem _entityCommandBufferSystem;
    EntityQuery _query;

	public static Entity Instantiate(Entity prefab, float3 pos, quaternion rot)
	{
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
		var entity = em.Instantiate(prefab);
#if UNITY_EDITOR
        em.SetName(entity, "missile");
#endif
		em.SetComponentData(entity, new Translation { Value = pos, });
		em.SetComponentData(entity, new Rotation { Value = rot, });
        em.SetComponentData(entity, new MissileComponent { Target = new float3(0, 0, 0), });
        em.SetComponentData(entity, AlivePeriod.Create(UTJ.Time.GetCurrent(), 2f /* period */));
        TrailSystem.Instantiate(entity, pos, 0.5f /* width */, Color.white,
                                new float3(0f, 0f, -0.5f) /* offset */, 4f/60f /* update_interval */);
        return entity;
    }

	public static Entity Instantiate(EntityCommandBuffer.Concurrent ecb,
                                     int jobIndex,
                                     float3 target,
                                     Entity prefab,
                                     Entity prefabTrail,
                                     float3 pos,
                                     float3 vel,
                                     float time)
	{
		var entity = ecb.Instantiate(jobIndex, prefab);
		ecb.SetComponent(jobIndex, entity, new Translation { Value = pos, });
        var rot = quaternion.LookRotationSafe(vel, new float3(0, 1, 0));
		ecb.SetComponent(jobIndex, entity, new Rotation { Value = rot, });
        ecb.SetComponent(jobIndex, entity, new MissileComponent { Target = target, Velocity = vel, });
        ecb.SetComponent(jobIndex, entity, AlivePeriod.Create(time, 2f /* period */));
        TrailSystem.Instantiate(ecb, jobIndex, prefabTrail, entity /* refering_entity */, pos, 0.5f /* width */, new Color(0.8f, 0.8f, 0.8f), time);
        return entity;
    }

    protected override void OnCreate()
    {
        _entityCommandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
        _query = GetEntityQuery(new EntityQueryDesc() {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<Translation>(),
                    ComponentType.ReadOnly<Rotation>(),
                    typeof(PhysicsVelocity),
                    ComponentType.ReadOnly<PhysicsMass>(),
                    typeof(MissileComponent),
                },
            });
    }        

    protected override void OnDestroy()
    {
    }

    [BurstCompile]
    struct MyJob : IJobChunk
    {
        public EntityCommandBuffer.Concurrent CommandBuffer;
        public float Time;
        public float Dt;
        [ReadOnly] public ArchetypeChunkComponentType<Translation> TranslationType;
        [ReadOnly] public ArchetypeChunkComponentType<Rotation> RotationType;
        public ArchetypeChunkComponentType<PhysicsVelocity> PhysicsVelocityType;
        [ReadOnly] public ArchetypeChunkComponentType<PhysicsMass> PhysicsMassType;
        [ReadOnly] public ArchetypeChunkComponentType<MissileComponent> MissileType;
        [ReadOnly] public ArchetypeChunkComponentType<AlivePeriod> AlivePeriodType;
        public Entity ExplosionPrefab;
        
        public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            var chunkTranslations = chunk.GetNativeArray(TranslationType);
            var chunkRotations = chunk.GetNativeArray(RotationType);
            var chunkPhysicsVelocities = chunk.GetNativeArray(PhysicsVelocityType);
            var chunkPhysicsMasses = chunk.GetNativeArray(PhysicsMassType);
            var chunkMissiles = chunk.GetNativeArray(MissileType);
            var chunkAlivePeriods = chunk.GetNativeArray(AlivePeriodType);
            for (var i = 0; i < chunk.Count; ++i) {
                var translation = chunkTranslations[i].Value;
                var rotation = chunkRotations[i].Value;
                ref var pv = ref chunkPhysicsVelocities.AsWritableRef(i);
                var pm = chunkPhysicsMasses[i];
                var missile = chunkMissiles[i];
                var ap = chunkAlivePeriods[i];

                var elapsed = Time - ap.StartTime;
                if (elapsed < 1f) {
                    pv.Linear = missile.Velocity;
                } else {
                    var diff = missile.Target - translation;
                    var relativeTorque = rotation.CalcSpringTorqueRelative(diff, 24f, Dt);
                    pv.ApplyAngularImpulse(pm, relativeTorque);
                    pv.ApplyLinearImpulse(pm, math.mul(rotation, new float3(0, 0, 400f*Dt)));
                }
                if (ap.GetRemainTime(Time) < 0f) {
                    ExplosionSystem.Instantiate(CommandBuffer, chunkIndex /* jobIndex */, translation, ExplosionPrefab, Time);
                }
            }
        }
    }
    
	protected override unsafe JobHandle OnUpdate(JobHandle handle)
	{
        var commandBuffer = _entityCommandBufferSystem.CreateCommandBuffer().ToConcurrent();
        var job = new MyJob {
            CommandBuffer = commandBuffer,
            Time = UTJ.Time.GetCurrent(),
            Dt = UTJ.Time.GetDt(),
            TranslationType = GetArchetypeChunkComponentType<Translation>(true /* isReadOnly */),
            RotationType = GetArchetypeChunkComponentType<Rotation>(true /* isReadOnly */),
            PhysicsVelocityType = GetArchetypeChunkComponentType<PhysicsVelocity>(false /* isReadOnly */),
            PhysicsMassType = GetArchetypeChunkComponentType<PhysicsMass>(true /* isReadOnly */),
            MissileType = GetArchetypeChunkComponentType<MissileComponent>(true /* isReadOnly */),
            AlivePeriodType = GetArchetypeChunkComponentType<AlivePeriod>(true /* isReadOnly */),
            ExplosionPrefab = ExplosionSystem.PrefabEntity,
        };
        handle = job.Schedule(_query, handle);
        _entityCommandBufferSystem.AddJobHandleForProducer(handle);
        return handle;
    }
}

} // namespace UTJ {
