using UnityEngine;
using UnityEngine.Assertions;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Burst;
using Unity.Physics;
using Unity.Physics.Extensions;

namespace UTJ {

[RequiresEntityConversion]
public class CameraManager : MonoBehaviour, IDeclareReferencedPrefabs, IConvertGameObjectToEntity
{
    public GameObject prefab;
    Entity _prefabEntity;

    public void DeclareReferencedPrefabs(List<GameObject> gameObjects)
    {
        gameObjects.Add(prefab);
    }

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        Assert.IsTrue(_prefabEntity == Entity.Null);
        _prefabEntity = conversionSystem.GetPrimaryEntity(prefab);
    }

    void Start()
    {
        var pos = new float3(0, 0, -10);
        var rot = quaternion.identity;
        CameraSystem.Instantiate(_prefabEntity,
                                 GetComponent<UnityEngine.Transform>(), pos, rot);
    }

    void OnDestroy()
    {
        CameraSystem.DestroyTransform(GetComponent<UnityEngine.Transform>());
    }
}

public class CameraSystem : JobComponentSystem
{
    EntityQuery _query;
    
    // CustomCopyTransformToGameObjectSystem m_CustomCopyTransformToGameObjectSystem;
    FighterSystem _fighterSystem;

	public static Entity Instantiate(Entity prefab,
                                     UnityEngine.Transform transform,
                                     float3 pos,
                                     quaternion rot)
	{
        var customCopyTransformToGameObjectSystem = World.Active.GetOrCreateSystem<CustomCopyTransformToGameObjectSystem>();
        customCopyTransformToGameObjectSystem.AddTransform(transform);
        var em = World.Active.EntityManager;
		var entity = em.Instantiate(prefab);
#if UNITY_EDITOR
        em.SetName(entity, "camera");
#endif
		em.SetComponentData(entity, new Translation { Value = pos, });
		em.SetComponentData(entity, new Rotation { Value = rot, });
        return entity;
    }

    public static void DestroyTransform(UnityEngine.Transform transform)
    {
        var customCopyTransformToGameObjectSystem = World.Active.GetOrCreateSystem<CustomCopyTransformToGameObjectSystem>();
        customCopyTransformToGameObjectSystem.RemoveTransform(transform);
    }

    protected override void OnCreate()
    {
        _fighterSystem = World.GetOrCreateSystem<FighterSystem>();
        _query = GetEntityQuery(new EntityQueryDesc() {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<CameraComponent>(),
                },
            });
    }        

    protected override void OnDestroy()
    {
    }

    [BurstCompile]
    struct Job : IJobChunk
    {
        public CameraParameter Param;
        public float Dt;
        public Entity TargetEntity;
        [ReadOnly] public ComponentDataFromEntity<Translation> Transforms;
        [ReadOnly] public ComponentDataFromEntity<Rotation> Rotations;
        [ReadOnly] public ArchetypeChunkComponentType<Translation> TranslationType;
        [ReadOnly] public ArchetypeChunkComponentType<Rotation> RotationType;
        [ReadOnly] public ArchetypeChunkComponentType<PhysicsMass> PhysicsMassType;
        [ReadOnly] public ArchetypeChunkComponentType<GroundHeightComponent> GroundHeightType;
        public ArchetypeChunkComponentType<PhysicsDamping> PhysicsDampingType;
        public ArchetypeChunkComponentType<PhysicsVelocity> PhysicsVelocityType;

        public unsafe void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            var chunkTranslations = chunk.GetNativeArray(TranslationType);
            var chunkRotations = chunk.GetNativeArray(RotationType);
            var chunkGroundHeights = chunk.GetNativeArray(GroundHeightType);
            var chunkPhysicsMasses = chunk.GetNativeArray(PhysicsMassType);
            var chunkPhysicsDampings = chunk.GetNativeArray(PhysicsDampingType);
            var chunkPhysicsVelocities = chunk.GetNativeArray(PhysicsVelocityType);
            for (var i = 0; i < chunk.Count; ++i) {
                var translation = chunkTranslations[i];
                var rotation = chunkRotations[i];
                var pm = chunkPhysicsMasses[i];
                var groundHeight = chunkGroundHeights[i];
                ref var pd = ref chunkPhysicsDampings.AsWritableRef(i);
                ref var pv = ref chunkPhysicsVelocities.AsWritableRef(i);
                
                // update physics params
                {
                    pd.Linear = Param.DampingLinear;
                    pd.Angular = Param.DampingAngular;
                }
                
                var targetPos = Transforms[TargetEntity];
                //Debug.Log(targetPos.Value.ToString());
                var targetRot = Rotations[TargetEntity];
                {
                    var targetTail = math.mul(targetRot.Value, new float3(0, 2, -2)) + targetPos.Value;
                    var diff = targetTail - translation.Value;
                    pv.ApplyLinearImpulse(pm, diff * (Param.LinearSpring * Dt));
                }
                {
                    var targetHead = math.mul(targetRot.Value, new float3(0, 0, 16)) + targetPos.Value;
                    var diff = targetHead - translation.Value;
                    var relativeTorque = rotation.Value.CalcSpringTorqueRelative(diff,
                                                                                  Param.AngularSpring,
                                                                                  Dt,
                                                                                  false /* relative_up */);
                    pv.ApplyAngularImpulse(pm, relativeTorque);
                }
                if (groundHeight.Height < 16f) {
                    pv.ApplyLinearImpulse(pm, new float3(0, (16f-groundHeight.Height)*20f*Dt, 0));
                }
            }
        }
    }

	protected override unsafe JobHandle OnUpdate(JobHandle handle)
	{
        var targetEntity = _fighterSystem.PrimaryEntity;
        if (targetEntity == Entity.Null) {
            return handle;
        }
            
        var job = new Job {
            Param = ParameterManager.Parameter.CameraParmeter,
            TargetEntity = _fighterSystem.PrimaryEntity,
            Dt = Time.GetDt(),
            Transforms = GetComponentDataFromEntity<Translation>(true /* readOnly */),
            Rotations = GetComponentDataFromEntity<Rotation>(true /* readOnly */),
            TranslationType = GetArchetypeChunkComponentType<Translation>(true /* isReadOnly */),
            RotationType = GetArchetypeChunkComponentType<Rotation>(true /* isReadOnly */),
            GroundHeightType = GetArchetypeChunkComponentType<GroundHeightComponent>(true /* isReadOnly */),
            PhysicsMassType = GetArchetypeChunkComponentType<PhysicsMass>(true /* isReadOnly */),
            PhysicsVelocityType = GetArchetypeChunkComponentType<PhysicsVelocity>(false /* isReadOnly */),
            PhysicsDampingType = GetArchetypeChunkComponentType<PhysicsDamping>(false /* isReadOnly */),
        };
        handle = job.Schedule(_query, handle);

        return handle;
    }
}

} // namespace UTJ {
