// #define RECORDING
// #define SEARCHING

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
using Unity.Physics.Systems;
using Unity.Rendering;
using UnityEngine.Serialization;
using Random = Unity.Mathematics.Random;
using Collider = Unity.Physics.Collider;
using SphereCollider = Unity.Physics.SphereCollider;

namespace UTJ {

[RequiresEntityConversion]
public class FighterManager : MonoBehaviour, IDeclareReferencedPrefabs, IConvertGameObjectToEntity
{
    public GameObject prefab;
    static Entity _prefabEntity;
    public static Entity Prefab => _prefabEntity;

    public void DeclareReferencedPrefabs(List<GameObject> gameObjects)
    {
        gameObjects.Add(prefab);
    }

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        _prefabEntity = conversionSystem.GetPrimaryEntity(prefab);
    }
}

public class FighterSystem : JobComponentSystem
{
    public Entity PrimaryEntity { get; set; }

    BeginInitializationEntityCommandBufferSystem _entityCommandBufferSystem;
    BuildPhysicsWorld _physicsWorldSystem;
    EntityQuery _query;
	EntityQuery _targetableQuery;

#if SEARCHING
    BlobAssetReference<Collider> cast_collider_;
    NativeArray<quaternion> local_search_rotations_;
#endif

    NativeArray<float3> _lastPrimaryFighterPos;
    NativeArray<float3> _lastPrimaryTargetPos;
    NativeList<ControllerUnit> _controllerBuffer;
#if RECORDING
    ControllerDevice controller_device_;
#endif
    BuildPhysicsWorld _buildPhysicsWorldSystem;

    // static int dummyCnt = 0;

	public static Entity Instantiate(Entity prefab, float3 pos, quaternion rot, int replayIndex)
	{
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
		var entity = em.Instantiate(prefab);
#if UNITY_EDITOR
        em.SetName(entity, "fighter");
#endif
		em.SetComponentData(entity, new Translation { Value = pos, });
		em.SetComponentData(entity, new Rotation { Value = rot, });
        em.SetComponentData(entity, new FighterComponent {
                Target = new float3(0, 16, 0), // tmp
                LastBeamFired = 0,
                ControllerReplay = new ControllerReplay {
                    ReplayIndex = replayIndex,
                },
            });
        var trailColor = new Color(172f/255f, 255f/255f, 237f/255f);
        TrailSystem.Instantiate(entity, pos, 1f /* width */, trailColor,
                                FighterConfig.BurnerLeft, 1f/60f /* update_interval */);
        TrailSystem.Instantiate(entity, pos, 1f /* width */, trailColor,
                                FighterConfig.BurnerRight, 1f/60f /* update_interval */);
        return entity;
    }

    protected override void OnCreate()
    {
        _entityCommandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
        _buildPhysicsWorldSystem = World.GetOrCreateSystem<BuildPhysicsWorld>();

        _query = GetEntityQuery(new EntityQueryDesc() {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<Translation>(),
                    ComponentType.ReadOnly<Rotation>(),
                    typeof(PhysicsVelocity),
                    ComponentType.ReadOnly<PhysicsMass>(),
                    typeof(FighterComponent),
                },
            });
        _targetableQuery = GetEntityQuery(new EntityQueryDesc() {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<FighterTargetable>(),
                },
            });

#if SEARCHING
        cast_collider_ = SphereCollider.Create(new float3(0, 0, 0) /* center */,
                                               1f /* radius */,
                                               new CollisionFilter {
                                                   BelongsTo = ~0u,
                                                   CollidesWith = ~0u,
                                               },
                                               null /* material */);

        // create approximate hexagonal directions.
        //      2
        //   6     4
        //      0
        //   5     3
        //      1
        local_search_rotations_ = new NativeArray<quaternion>(FighterConfig.SEARCH_NUM, Allocator.Persistent);
        local_search_rotations_[0] = quaternion.Euler(0, 0, 0);
        local_search_rotations_[1] = quaternion.Euler(math.radians(10f), 0, 0);
        local_search_rotations_[2] = quaternion.Euler(math.radians(-10f), 0, 0);
        local_search_rotations_[3] = quaternion.Euler(math.radians(5f), math.radians(5f*1.7320508f), 0);
        local_search_rotations_[4] = quaternion.Euler(math.radians(-5f), math.radians(5f*1.7320508f), 0);
        local_search_rotations_[5] = quaternion.Euler(math.radians(5f), math.radians(-5f*1.7320508f), 0);
        local_search_rotations_[6] = quaternion.Euler(math.radians(-5f), math.radians(-5f*1.7320508f), 0);
#endif

        _lastPrimaryFighterPos = new NativeArray<float3>(1, Allocator.Persistent);
        _lastPrimaryTargetPos = new NativeArray<float3>(1, Allocator.Persistent);
        #if RECORDING
        controller_buffer_ = new NativeList<ControllerUnit>(ControllerBuffer.MAX_FRAMES, Allocator.Persistent);
        controller_device_ = new ControllerDevice(controller_buffer_);
        controller_device_.start(UTJ.Time.GetCurrent());
        #else
        _controllerBuffer = ControllerBuffer.Load<ControllerUnit>("controller.bin");
        #endif
    }        

    protected override void OnDestroy()
    {
        _controllerBuffer.Dispose();
        _lastPrimaryFighterPos.Dispose();
        _lastPrimaryTargetPos.Dispose();
#if SEARCHING
        local_search_rotations_.Dispose();
#endif
    }

#if SEARCHING
    [BurstCompile]
    public struct PrepareCastJob : IJobChunk
    {
        [ReadOnly] public NativeArray<quaternion> local_search_rotations_;
        public BlobAssetReference<Collider> cast_collider_;
        [ReadOnly] public ArchetypeChunkComponentType<Translation> TranslationType;
        [ReadOnly] public ArchetypeChunkComponentType<Rotation> RotationType;
        public ArchetypeChunkBufferType<FighterSearchInputBuffer> FighterSearchInputBufferType;

        public unsafe void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            var chunkTranslations = chunk.GetNativeArray(TranslationType);
            var chunkRotations = chunk.GetNativeArray(RotationType);
            var chunkFighterSearchInputBuffers = chunk.GetBufferAccessor(FighterSearchInputBufferType);
            for (var i = 0; i < chunk.Count; ++i) {
                DynamicBuffer<FighterSearchInputBuffer> buf = chunkFighterSearchInputBuffers[i];
                for (var j = 0; j < buf.Length; ++j) {
                    var orientation = math.mul(chunkRotations[i].Value, local_search_rotations_[j]);
                    buf[j] = new FighterSearchInputBuffer {
                        Value = new ColliderCastInput {
                            Collider = (Collider*)cast_collider_.GetUnsafePtr(),
                            Orientation = orientation,
                            Start = math.mul(orientation, new float3(0, 0, 10f)) + chunkTranslations[i].Value,
                            End = math.mul(orientation, new float3(0, 0, 100f)) + chunkTranslations[i].Value,
                        },
                    };
                }
            }
        }
    }
#endif

#if SEARCHING
    [BurstCompile]
    public struct CastJob : IJobChunk
    {
        public CollisionWorld world;
        [ReadOnly] public ArchetypeChunkComponentType<Translation> TranslationType;
        [ReadOnly] public ArchetypeChunkBufferType<FighterSearchInputBuffer> FighterSearchInputBufferType;
        public ArchetypeChunkBufferType<FighterSearchHitDistanceBuffer> FighterSearchHitDistanceBufferType;

        public unsafe void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            var chunkTranslations = chunk.GetNativeArray(TranslationType);
            var chunkFighterSearchInputBuffers = chunk.GetBufferAccessor(FighterSearchInputBufferType);
            var chunkFighterSearchHitDistanceBuffers = chunk.GetBufferAccessor(FighterSearchHitDistanceBufferType);
            for (var i = 0; i < chunk.Count; ++i) {
                DynamicBuffer<FighterSearchInputBuffer> input_buf = chunkFighterSearchInputBuffers[i];
                DynamicBuffer<FighterSearchHitDistanceBuffer> hit_buf = chunkFighterSearchHitDistanceBuffers[i];
                for (var j = 0; j < input_buf.Length; ++j) {
                    ColliderCastHit hit;
                    bool hitted = world.CastCollider(input_buf[j].Value, out hit);
                    if (hitted) {
                        hit_buf[j] = new FighterSearchHitDistanceBuffer { Value = math.length(hit.Position - chunkTranslations[i].Value), };
                    } else {
                        hit_buf[j] = new FighterSearchHitDistanceBuffer { Value = float.MaxValue, };
                    }
                };
            }
        }
    }
#endif

    [BurstCompile]
    struct MyJob : IJobChunk
    {
        [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Entity> TargetableArray;
        [ReadOnly] public ComponentDataFromEntity<Translation> TargetableTranslations;
        public FighterParameter Param; // comming from ScriptableObject settings.
        public EntityCommandBuffer.Concurrent CommandBuffer;
        public Entity BeamPrefabEntity;
        public Entity MissilePrefabEntity;
        public Entity TrailPrefabEntity;
        public Entity DistortionPrefabEntity;
        public float Time;
        public float Dt;
#if RECORDING
        public NativeArray<float3> last_primary_fighter_pos_;
        public NativeArray<float3> last_primary_target_pos_;
        public ControllerUnit controller_unit_;
#else
        [ReadOnly] public NativeList<ControllerUnit> ControllerBuffer;
#endif
#if SEARCHING
        [ReadOnly] public NativeArray<quaternion> local_search_rotations_;
#endif
        [ReadOnly] public ArchetypeChunkComponentType<Translation> TranslationType;
        [ReadOnly] public ArchetypeChunkComponentType<Rotation> RotationType;
        public ArchetypeChunkComponentType<PhysicsVelocity> PhysicsVelocityType;
        [ReadOnly] public ArchetypeChunkComponentType<PhysicsMass> PhysicsMassType;
        public ArchetypeChunkComponentType<PhysicsDamping> PhysicsDampingType;
#if SEARCHING
        [ReadOnly] public ArchetypeChunkBufferType<FighterSearchHitDistanceBuffer> FighterSearchHitDistanceBufferType;
#endif
        [ReadOnly] public ArchetypeChunkComponentType<GroundHeightComponent> GroundHeightType;
        [ReadOnly] public ArchetypeChunkComponentType<ObstacleDistanceComponent> ObstacleDistanceType;
        public ArchetypeChunkComponentType<FighterComponent> FighterType;
        
        public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            var chunkTranslations = chunk.GetNativeArray(TranslationType);
            var chunkRotations = chunk.GetNativeArray(RotationType);
            var chunkPhysicsVelocities = chunk.GetNativeArray(PhysicsVelocityType);
            var chunkPhysicsDamping = chunk.GetNativeArray(PhysicsDampingType);
            var chunkPhysicsMasses = chunk.GetNativeArray(PhysicsMassType);
            var chunkGroundHeights = chunk.GetNativeArray(GroundHeightType);
            var chunkObstacleDistances = chunk.GetNativeArray(ObstacleDistanceType);
            var chunkFighters = chunk.GetNativeArray(FighterType);
#if SEARCHING
            var chunkFighterSearchHitDistanceBuffers = chunk.GetBufferAccessor(FighterSearchHitDistanceBufferType);
#endif
            for (var i = 0; i < chunk.Count; ++i) {

                var translation = chunkTranslations[i].Value;
                var rotation = chunkRotations[i].Value;
                var irotation = math.conjugate(rotation);
                ref var pv = ref chunkPhysicsVelocities.AsWritableRef(i);
                var pm = chunkPhysicsMasses[i];
                var obstacleDistance = chunkObstacleDistances[i];
                var groundHeight = chunkGroundHeights[i];
                ref var fighter = ref chunkFighters.AsWritableRef(i);
                
                // target search
                {
                    float minDist = float.MaxValue;
                    Entity targetEntity = Entity.Null;
                    foreach (var entity in TargetableArray)
                    {
                        if (TargetableTranslations.Exists(entity)) {
                            var pos = TargetableTranslations[entity].Value;
                            var len2 = math.lengthsq(pos - translation);
                            if (len2 < minDist) {
                                minDist = len2;
                                targetEntity = entity;
                            }
                        }
                    }
                    if (targetEntity != Entity.Null) {
                        fighter.TargetEntity = targetEntity;
                    }
                }
                var targetPos = TargetableTranslations.Exists(fighter.TargetEntity) ?
                    TargetableTranslations[fighter.TargetEntity].Value : fighter.Target;
                fighter.Target = targetPos;

#if RECORDING
                // for input recordings.
                last_primary_fighter_pos_[0] = translation;
                last_primary_target_pos_[0] = fighter.target_;
#endif

                // update physics params with parameters.
                {
                    var pd = chunkPhysicsDamping[i];
                    pd.Linear = Param.DampingLinear;
                    pd.Angular = Param.DampingAngular;
                    chunkPhysicsDamping[i] = pd;
                }

                // target dir
                var targetDir = fighter.Target - translation;
                targetDir = math.normalizesafe(targetDir, new float3(0, 0, 1) /* defaultValue */);

#if SEARCHING
                bool obstacle = false;
                {
                    DynamicBuffer<FighterSearchHitDistanceBuffer> hit_buf = chunkFighterSearchHitDistanceBuffers[i];
                    if (hit_buf[0].Value < 10f) {
                        float min_value = float.MaxValue;
                        int min_idx = -1;
                        for (var j = 0; j < hit_buf.Length; ++j) {
                            if (hit_buf[j].Value < min_value) {
                                min_value = hit_buf[j].Value;
                                min_idx = j;
                            }
                        }
                        float3 omega;
                        if (min_idx == 0) {
                            omega = new float3(1, 0, 0);
                        } else {
                            var local_rot = local_search_rotations_[min_idx];
                            var avoid_dir = math.mul(local_rot, new float3(0, 0, 1));
                            omega = math.normalize(math.cross(new float3(0, 0, 1), avoid_dir));
                        }
                        pv.ApplyAngularImpulse(pm, omega * (10000f * dt_));
                        obstacle = true;
                    }
                }
#endif

#if RECORDING
                var cunit = controller_unit_;
#else
                var cunit = fighter.ControllerReplay.Step(ControllerBuffer,
                                                            translation,
                                                            fighter.Target);
#endif
                // obstacle behavior
                if (obstacleDistance.Distance < 6f) {
                    var relativeTorque = new float3(-1000, 0, 0);
                    pv.ApplyAngularImpulse(pm, relativeTorque);
                }

                // ground height behavior
                if (groundHeight.Height < 6f) {
                    var worldForward = math.mul(rotation, new float3(0, 0, 1));
                    var horizontalForward = new float3(worldForward.x, 0, worldForward.z);
                    var relativeTorque = rotation.CalcSpringTorqueRelative(horizontalForward, Param.GroundAvoidance, Dt, false /* relative_up */);
                    if (relativeTorque.x < 0f) // up turn only
                        pv.ApplyAngularImpulse(pm, relativeTorque);
                }

                // controller
                {
                    var worldLeft = math.mul(rotation, new float3(1, 0, 0));
                    var worldHorizontalLeft = math.normalize(new float3(worldLeft.x, 0, worldLeft.z));
                    var worldForward = math.mul(rotation, new float3(0, 0, 1));
                    var localUp = math.normalize(math.cross(worldForward, worldHorizontalLeft));
                    var worldYawImpulse = localUp * (cunit.Horizontal * Param.YawImpulse);

                    var worldPitchImpulse = worldHorizontalLeft * (-cunit.Vertical * Param.PitchImpulse);
                    var worldImpulse = worldYawImpulse + worldPitchImpulse;
                    
                    var localControlImpulse = math.mul(irotation, worldImpulse);
                    localControlImpulse += new float3(0, 0, -cunit.Horizontal * Param.RollImpulse);

                    pv.ApplyAngularImpulse(pm, localControlImpulse * Dt);
                }

                // horizontal stability
                {
                    var worldForward = math.mul(rotation, new float3(0, 0, 1));
                    var relativeTorque = rotation.CalcSpringTorqueRelative(worldForward, Param.RollStability, Dt, false /* relative_up */);
                    var horizontalForward = new float3(worldForward.x, 0, worldForward.z);
                    relativeTorque += rotation.CalcSpringTorqueRelative(horizontalForward, Param.PitchStability, Dt, true /* relative_up */);
                    pv.ApplyAngularImpulse(pm, relativeTorque);
                }

                // towards
                if (cunit.Toward) {
                    var diff = fighter.Target - translation;
                    var relativeImpulse = rotation.CalcSpringTorqueRelative(diff,
                                                                             Param.TowardSpringTorqueRatio,
                                                                             Dt,
                                                                             true /* relative_up */);
                    relativeImpulse.z = -relativeImpulse.y*0.5f;
                    pv.ApplyAngularImpulse(pm, relativeImpulse);
                }

                // bullets
                if (cunit.FireBullet) {
                    var elapsed = Time - fighter.LastBeamFired;
                    if (elapsed > 0.1f) {
                        var localTargetDir = math.mul(irotation, targetDir);
                        var costheta = math.dot(localTargetDir, new float3(0, 0, 1));
                        if (costheta > 1.7320508f/2f) {
                            var firePos0 = math.mul(rotation, new float3(0.338f, 0.125f, 1.219f));
                            BeamSystem.Instantiate(CommandBuffer,
                                                   chunkIndex /* jobIndex */,
                                                   BeamPrefabEntity,
                                                   translation+firePos0,
                                                   rotation,
                                                   Param.BulletVelocity,
                                                   Time);
                            var firePos1 = math.mul(rotation, new float3(-0.338f, 0.125f, 1.219f));
                            BeamSystem.Instantiate(CommandBuffer,
                                                   chunkIndex /* jobIndex */,
                                                   BeamPrefabEntity,
                                                   translation+firePos1,
                                                   rotation,
                                                   Param.BulletVelocity,
                                                   Time);
                            fighter.LastBeamFired = Time;
                        }
                    }
                }

                // missiles
                if (cunit.FireMissile)
                {
                    var local_target_dir = math.mul(irotation, targetDir);
                    var costheta = math.dot(local_target_dir, new float3(0, 0, 1));
                    if (costheta > 0.5) {
                        var random = new Random((uint)translation.GetHashCode());
                        var speed = math.length(pv.Linear);
                        var dir = math.normalize(new float3(random.NextFloat(-0.5f, 0.5f), random.NextFloat(-0.1f, 0.1f), 1f));
                        var vel = math.mul(rotation, dir) * speed;
                        MissileSystem.Instantiate(CommandBuffer,
                                                  chunkIndex /* jobIndex */,
                                                  targetPos,
                                                  MissilePrefabEntity,
                                                  TrailPrefabEntity,
                                                  translation,
                                                  vel,
                                                  Time);
                    }
                }

                // distortion
                if (Time - fighter.LastDistortionEmitted > 4f/60f)
                {
                    fighter.LastDistortionEmitted = Time;
                    DistortionSystem.Instantiate(CommandBuffer,
                                                 chunkIndex /* jobIndex */,
                                                 DistortionPrefabEntity,
                                                 math.mul(rotation, FighterConfig.BurnerLeft) + translation,
                                                 0.2f /* period */,
                                                 1f /* size */);
                    DistortionSystem.Instantiate(CommandBuffer,
                                                 chunkIndex /* jobIndex */,
                                                 DistortionPrefabEntity,
                                                 math.mul(rotation, FighterConfig.BurnerRight) + translation,
                                                 0.2f /* period */,
                                                 1f /* size */);
                }


#if SEARCHING
                // to target
                if (!obstacle) {
                    var target_rot = quaternion.LookRotation(dir, math.mul(rotation, new float3(0, 1, 0)));
                    var inv = math.conjugate(rotation);
                    var rot = math.mul(target_rot, inv);
                    var relative_torque = math.mul(inv, rot.value.xyz * (200000f * dt_));
                    pv.ApplyAngularImpulse(pm, relative_torque);
                    pv.ApplyAngularImpulse(pm, new float3(0, 0, -rot.value.y*200000f*dt_));
                }
#endif

                // forward
                pv.ApplyLinearImpulse(pm, math.mul(rotation, new float3(0, 0, Param.ForwardImpulse*Dt)));
            }
        }
    }
    
	protected override unsafe JobHandle OnUpdate(JobHandle handle)
	{
        NativeArray<Entity> targetableArray;
        {
            targetableArray = _targetableQuery.ToEntityArray(Allocator.TempJob, out var gather_handle);
            handle = JobHandle.CombineDependencies(handle, gather_handle);
        }

#if SEARCHING
        {
            var prepare_cast_job = new PrepareCastJob {
                local_search_rotations_ = local_search_rotations_,
                cast_collider_ = cast_collider_,
                TranslationType = GetArchetypeChunkComponentType<Translation>(true /* isReadOnly */),
                RotationType = GetArchetypeChunkComponentType<Rotation>(true /* isReadOnly */),
                FighterSearchInputBufferType = GetArchetypeChunkBufferType<FighterSearchInputBuffer>(false /* isReadOnly */),
            };
            handle = prepare_cast_job.Schedule(m_Query, handle);
        }
        {
            if (m_physicsWorldSystem == null)
                m_physicsWorldSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystem<BuildPhysicsWorld>();
            var cast_job = new CastJob {
                world = m_physicsWorldSystem.PhysicsWorld.CollisionWorld,
                TranslationType = GetArchetypeChunkComponentType<Translation>(true /* isReadOnly */),
                FighterSearchInputBufferType = GetArchetypeChunkBufferType<FighterSearchInputBuffer>(true /* isReadOnly */),
                FighterSearchHitDistanceBufferType = GetArchetypeChunkBufferType<FighterSearchHitDistanceBuffer>(false /* isReadOnly */),
            };
            handle = cast_job.Schedule(m_Query, handle);
        }
#endif
        {
#if RECORDING
            if (primary_entity_ != Entity.Null) {
                var ppos = last_primary_fighter_pos_[0];
                var tpos = last_primary_target_pos_[0];
                controller_device_.update(UTJ.Time.GetCurrent(), ppos, tpos);
            }
#endif
            var job = new MyJob {
                TargetableArray = targetableArray,
                TargetableTranslations = GetComponentDataFromEntity<Translation>(),
                Param = ParameterManager.Parameter.FighterParameter,
                CommandBuffer = _entityCommandBufferSystem.CreateCommandBuffer().ToConcurrent(),
                BeamPrefabEntity = BeamManager.Prefab,
                MissilePrefabEntity = MissileManager.Prefab,
                TrailPrefabEntity = TrailManager.Prefab,
                DistortionPrefabEntity = DistortionManager.Prefab,
                Time = UTJ.Time.GetCurrent(),
                Dt = UTJ.Time.GetDt(),
#if RECORDING
                last_primary_fighter_pos_ = last_primary_fighter_pos_,
                last_primary_target_pos_ = last_primary_target_pos_,
                controller_unit_ = controller_device_.getCurrent(),
#else
                ControllerBuffer = _controllerBuffer,
#endif
#if SEARCHING
                local_search_rotations_ = local_search_rotations_,
#endif
                TranslationType = GetArchetypeChunkComponentType<Translation>(true /* isReadOnly */),
                RotationType = GetArchetypeChunkComponentType<Rotation>(true /* isReadOnly */),
                PhysicsVelocityType = GetArchetypeChunkComponentType<PhysicsVelocity>(false /* isReadOnly */),
                PhysicsDampingType = GetArchetypeChunkComponentType<PhysicsDamping>(false /* isReadOnly */),
                PhysicsMassType = GetArchetypeChunkComponentType<PhysicsMass>(true /* isReadOnly */),
#if SEARCHING
                FighterSearchHitDistanceBufferType = GetArchetypeChunkBufferType<FighterSearchHitDistanceBuffer>(true /* isReadOnly */),
#endif
                GroundHeightType = GetArchetypeChunkComponentType<GroundHeightComponent>(true /* isReadOnly */),
                ObstacleDistanceType = GetArchetypeChunkComponentType<ObstacleDistanceComponent>(true /* isReadOnly */),
                FighterType = GetArchetypeChunkComponentType<FighterComponent>(false /* isReadOnly */),
            };
            handle = job.Schedule(_query, handle);
        }
        _entityCommandBufferSystem.AddJobHandleForProducer(handle);
        return handle;
    }
}

} // namespace UTJ {
