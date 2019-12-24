using UnityEngine;
using UnityEngine.Rendering;
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
using Unity.Physics.Systems;
using Unity.Rendering;
using UnityEngine.Serialization;
using Random = Unity.Mathematics.Random;

namespace UTJ {

[RequiresEntityConversion]
public class HexpedManager : MonoBehaviour, IDeclareReferencedPrefabs, IConvertGameObjectToEntity
{
    public GameObject prefabBody;
    public GameObject prefabGroin;
    public GameObject prefabThigh;
    public GameObject prefabShin;
    Entity _prefabEntityBody;
    Entity _prefabEntityGroin;
    Entity _prefabEntityThigh;
    Entity _prefabEntityShin;

    public void DeclareReferencedPrefabs(List<GameObject> gameObjects)
    {
        gameObjects.Add(prefabBody);
        gameObjects.Add(prefabGroin);
        gameObjects.Add(prefabThigh);
        gameObjects.Add(prefabShin);
    }

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        _prefabEntityBody = conversionSystem.GetPrimaryEntity(prefabBody);
        _prefabEntityGroin = conversionSystem.GetPrimaryEntity(prefabGroin);
        _prefabEntityThigh = conversionSystem.GetPrimaryEntity(prefabThigh);
        _prefabEntityShin = conversionSystem.GetPrimaryEntity(prefabShin);
    }

    void Start()
    {
        HexpedSystem.Initialize();

        const float height = 10f;
        HexpedSystem.Instantiate(new RigidTransform(quaternion.identity, new float3(-64, height, 0)),
                                 _prefabEntityBody,
                                 _prefabEntityGroin,
                                 _prefabEntityThigh,
                                 _prefabEntityShin);
        HexpedSystem.Instantiate(new RigidTransform(quaternion.identity, new float3(0, height, 64)),
                                 _prefabEntityBody,
                                 _prefabEntityGroin,
                                 _prefabEntityThigh,
                                 _prefabEntityShin);
        HexpedSystem.Instantiate(new RigidTransform(quaternion.identity, new float3(64, height, 0)),
                                 _prefabEntityBody,
                                 _prefabEntityGroin,
                                 _prefabEntityThigh,
                                 _prefabEntityShin);
        // HexpedSystem.Instantiate(new RigidTransform(quaternion.identity, new float3(0, HEIGHT, -64)),
        //                          _prefabEntityBody,
        //                          _prefabEntityGroin,
        //                          _prefabEntityThigh,
        //                          _prefabEntityShin);
    }
}

public class HexpedSystem : JobComponentSystem
{
    static HexpedData _data;

    public static void Initialize()
    {
        _data = HexpedData.Create();
    }

	public static void Instantiate(RigidTransform transform,
                                   Entity prefabEntityBody,
                                   Entity prefabEntityGroin,
                                   Entity prefabEntityThigh,
                                   Entity prefabEntityShin)
	{
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var hexped = new Hexped();
        hexped.Initialize(in _data,
                          em,
                          transform,
                          prefabEntityBody,
                          prefabEntityGroin,
                          prefabEntityThigh,
                          prefabEntityShin);
        hexped.ApplyTransform(em);
    }

    BuildPhysicsWorld _buildPhysicsWorldSystem;
	CollisionSystem _collisionSystem;
    EntityQuery _query;
    ControllerDevice _controllerDevice;
    
    protected override void OnCreate()
    {
		_controllerDevice = new ControllerDevice();
        _buildPhysicsWorldSystem = World.GetOrCreateSystem<BuildPhysicsWorld>();
        _collisionSystem = World.GetOrCreateSystem<CollisionSystem>();
        _query = GetEntityQuery(new EntityQueryDesc() {
            All = new ComponentType[] {
                ComponentType.ReadWrite<HexpedComponent>(),
            }
        });
    }

    protected override void OnDestroy()
    {
        _data.Dispose();
    }

    [BurstCompile]
    struct MyJob : IJobChunk
    {
		public float Dt;
        public CollisionWorld CollisionWorld;
        public HexpedData Data;
		public ControllerUnit ControllerUnit;
        [ReadOnly] public ArchetypeChunkComponentType<CollisionInfoComponent> InfoType;
        public ArchetypeChunkComponentType<HexpedComponent> HexpedComponentType;
        public ArchetypeChunkBufferType<LegTransform> LegTransformBufferType;

        public unsafe void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            var chunkInfos = chunk.GetNativeArray(InfoType);
            var chunkHexpedComponents = chunk.GetNativeArray(HexpedComponentType);
            var legTransformBuffers = chunk.GetBufferAccessor(LegTransformBufferType);
            for (var i = 0; i < chunk.Count; ++i) {
                ref var info = ref chunkInfos.AsReadOnlyRef(i);
                ref var hexped = ref chunkHexpedComponents.AsWritableRef(i);
                DynamicBuffer<LegTransform> legTransformBuffer = legTransformBuffers[i];
                hexped.Hexped.Update(Dt,
                                     CollisionWorld,
                                     in Data,
                                     in info,
                                     in ControllerUnit,
                                     legTransformBuffer);
            }
        }
    }

    [BurstCompile]
    struct TransformBodyJob : IJobForEach<HexpedComponent, Translation, Rotation>
    {
        public unsafe void Execute([ReadOnly] ref HexpedComponent hexped,
                                   ref Translation translation,
                                   ref Rotation rotation)
        {
			var transform = hexped.Hexped.GetBodyTransform();
            translation = new Translation { Value = transform.pos, };
            rotation = new Rotation { Value = transform.rot, };
        }
    }

    [BurstCompile]
    struct TransformGroinJob : IJobForEach<HexpedGroinComponent, Translation, Rotation>
    {
        [ReadOnly] public BufferFromEntity<LegTransform> BufferFromEntity;

        public unsafe void Execute([ReadOnly] ref HexpedGroinComponent hexpedGroin,
                                   ref Translation translation,
                                   ref Rotation rotation)
        {
            DynamicBuffer<LegTransform> buf = BufferFromEntity[hexpedGroin.Parent];
            var legTransform = buf[hexpedGroin.Id];
            translation = new Translation { Value = legTransform.Groin.pos, };
            rotation = new Rotation { Value = legTransform.Groin.rot, };
        }
    }

    [BurstCompile]
    struct TransformThighJob : IJobForEach<HexpedThighComponent, Translation, Rotation>
    {
        [ReadOnly] public BufferFromEntity<LegTransform> BufferFromEntity;

        public unsafe void Execute([ReadOnly] ref HexpedThighComponent hexpedThigh,
                                   ref Translation translation,
                                   ref Rotation rotation)
        {
            DynamicBuffer<LegTransform> buf = BufferFromEntity[hexpedThigh.Parent];
            var legTransform = buf[hexpedThigh.Id];
            translation = new Translation { Value = legTransform.Thigh.pos, };
            rotation = new Rotation { Value = legTransform.Thigh.rot, };
        }
    }

    [BurstCompile]
    struct TransformShinJob : IJobForEach<HexpedShinComponent, Translation, Rotation>
    {
        [ReadOnly] public BufferFromEntity<LegTransform> BufferFromEntity;

        public unsafe void Execute([ReadOnly] ref HexpedShinComponent hexpedShin,
                                   ref Translation translation,
                                   ref Rotation rotation)
        {
            DynamicBuffer<LegTransform> buf = BufferFromEntity[hexpedShin.Parent];
            var legTransform = buf[hexpedShin.Id];
            translation = new Translation { Value = legTransform.Shin.pos, };
            rotation = new Rotation { Value = legTransform.Shin.rot, };
        }
    }

    protected override JobHandle OnUpdate(JobHandle handle)
    {
        var job = new MyJob {
			Dt = UTJ.Time.GetDt(),
            CollisionWorld = _buildPhysicsWorldSystem.PhysicsWorld.CollisionWorld,
            Data = _data,
			ControllerUnit = _controllerDevice.Update(),
            InfoType = GetArchetypeChunkComponentType<CollisionInfoComponent>(true /* isReadOnly */),
            HexpedComponentType = GetArchetypeChunkComponentType<HexpedComponent>(false /* isReadOnly */),
            LegTransformBufferType = GetArchetypeChunkBufferType<LegTransform>(false /* isReadOnly */),
        };
        handle = job.Schedule(_query, handle);

        var transformBodyJob = new TransformBodyJob {
        };
        handle = transformBodyJob.Schedule(this, handle);

		var bufferFromEntity = GetBufferFromEntity<LegTransform>(true /* isReadOnly */);
        var transformGroinJob = new TransformGroinJob {
            BufferFromEntity = bufferFromEntity,
        };
        handle = transformGroinJob.Schedule(this, handle);

        var transformThighJob = new TransformThighJob {
            BufferFromEntity = bufferFromEntity,
        };
        handle = transformThighJob.Schedule(this, handle);

        var transformShinJob = new TransformShinJob {
            BufferFromEntity = bufferFromEntity,
        };
        handle = transformShinJob.Schedule(this, handle);

		_collisionSystem.AddDependingJobHandle(handle);
        handle.Complete();

        return handle;
    }
}

} // namespace UTJ {
