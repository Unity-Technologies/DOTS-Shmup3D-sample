using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Assertions;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Burst;
using Unity.Physics;
using UnityEngine.Serialization;

namespace UTJ {

[RequiresEntityConversion]
public class BeamManager : MonoBehaviour, IDeclareReferencedPrefabs, IConvertGameObjectToEntity
{
    public GameObject prefab;
    public UnityEngine.Material materialBeam;

    static Entity _prefabEntity;
    public static Entity Prefab => _prefabEntity;

    public void DeclareReferencedPrefabs(List<GameObject> gameObjects)
    {
        gameObjects.Add(prefab);
    }

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        _prefabEntity = conversionSystem.GetPrimaryEntity(prefab);
        RenderBeamSystem.Initialize(materialBeam);
    }

}

public class BeamSystem : JobComponentSystem
{
    EntityQuery _query;
    BeginInitializationEntityCommandBufferSystem _entityCommandBufferSystem;
    NativeList<Matrix4x4> _batchMatrices;
    public NativeList<Matrix4x4> BatchMatrices => _batchMatrices;
    RenderBeamSystem _renderBeamSystem;

	public static Entity Instantiate(EntityCommandBuffer.Concurrent ecb, int jobIndex, Entity prefab, float3 pos, quaternion rot, float velocity)
	{
        var vel = math.mul(rot, new float3(0, 0, velocity));
        return Instantiate(ecb, jobIndex, prefab, pos, rot, vel);
    }
	public static Entity Instantiate(EntityCommandBuffer.Concurrent ecb, int jobIndex, Entity prefab, float3 pos, float3 vel)
	{
        var up = new float3(0f, 1f, 0f);
        var rot = quaternion.LookRotationSafe(vel, up);
        return Instantiate(ecb, jobIndex, prefab, pos, rot, vel);
    }
    static Entity Instantiate(EntityCommandBuffer.Concurrent ecb, int jobIndex, Entity prefab, float3 pos, quaternion rot, float3 vel)
	{
		var entity = ecb.Instantiate(jobIndex, prefab);
		ecb.SetComponent(jobIndex, entity, new Translation { Value = pos, });
		ecb.SetComponent(jobIndex, entity, new Rotation { Value = rot, });
        ecb.SetComponent(jobIndex, entity, new PhysicsVelocity() { Linear = vel, });
		ecb.SetComponent(jobIndex, entity, new AlivePeriod { StartTime = (float)Time.GetCurrent(), Period = 2f, });
        ecb.SetComponent(jobIndex, entity, new CachedBeamMatrix {
                Matrix = new float4x4(rot, pos),
            });
        return entity;
    }

    public static Entity Instantiate(EntityManager em, Entity prefab, float3 pos, float3 vel)
	{
        var up = new float3(0, 1, 0);
        var rot = quaternion.LookRotationSafe(vel, up);
		var entity = em.Instantiate(prefab);
		em.SetComponentData(entity, new Translation { Value = pos, });
		em.SetComponentData(entity, new Rotation { Value = rot, });
        em.SetComponentData(entity, new PhysicsVelocity() { Linear = vel, });
		em.SetComponentData(entity, new AlivePeriod { StartTime = (float)Time.GetCurrent(), Period = 3f, });
        em.SetComponentData(entity, new CachedBeamMatrix {
                Matrix = new float4x4(rot, pos),
            });
        return entity;
    }

    protected override void OnCreate()
    {
        _query = GetEntityQuery(new EntityQueryDesc() {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<BeamComponent>(),
                },
            });
        _entityCommandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
        _batchMatrices = new NativeList<Matrix4x4>(RenderBeamSystem.BatchNum*Cv.InstanceLimit, Allocator.Persistent);
        _renderBeamSystem = World.GetOrCreateSystem<RenderBeamSystem>();
    }        

    protected override void OnDestroy()
    {
        _batchMatrices.Dispose();
    }

    [BurstCompile]
    struct Job : IJob
    {
        [ReadOnly] public ArchetypeChunkComponentType<Translation> TranslationType;
        [ReadOnly] public ArchetypeChunkComponentType<CachedBeamMatrix> CachedBeamMatrixType;
        [ReadOnly] public ArchetypeChunkComponentType<BeamComponent> BeamType;
        [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<ArchetypeChunk> ChunkArray;
        public NativeList<Matrix4x4> Matrices;
        
        public void Execute()
        {
            for (var j = 0; j < ChunkArray.Length; ++j) {
                var chunk = ChunkArray[j];
                var translations = chunk.GetNativeArray(TranslationType);
                var mats = chunk.GetNativeArray(CachedBeamMatrixType);
                var beams = chunk.GetNativeArray(BeamType);
                for (var i = 0; i < chunk.Count; ++i) {
                    var mat = mats[i].Matrix;
                    mat.c3 = new float4(translations[i].Value, 1);
                    mat.c0.w = beams[i].ColorBitPattern;
                    mat.c1.w = beams[i].Width;
                    mat.c2.w = beams[i].Length;
                    Matrices.Add(mat);
                }
            }
        }
    }
    
	protected override JobHandle OnUpdate(JobHandle handle)
	{
        _batchMatrices.Clear();

        var chunkArray = _query.CreateArchetypeChunkArray(Allocator.TempJob);
        var job = new Job {
            TranslationType = GetArchetypeChunkComponentType<Translation>(),
            CachedBeamMatrixType = GetArchetypeChunkComponentType<CachedBeamMatrix>(),
            BeamType = GetArchetypeChunkComponentType<BeamComponent>(),
            ChunkArray = chunkArray,
            Matrices = _batchMatrices,
        };
        handle = job.Schedule(handle);
        _renderBeamSystem.AddJobHandleForProducer(handle);

        return handle;
    }
}

public class BeamCollisionSystem : JobComponentSystem
{
    EntityQuery _query;
    BeginInitializationEntityCommandBufferSystem _entityCommandBufferSystem;

    protected override void OnCreate()
    {
        _query = GetEntityQuery(new EntityQueryDesc() {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<BeamComponent>(),
                    ComponentType.ReadOnly<CollisionInfoComponent>(),
                },
            });
        _entityCommandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
    }        

    // [BurstCompile]
    struct Job : IJobChunk
    {
        public EntityCommandBuffer.Concurrent CommandBuffer;
        public Entity SparkPrefabEntity;
        [ReadOnly] public ArchetypeChunkEntityType EntityType;
        [ReadOnly] public ArchetypeChunkComponentType<CollisionInfoComponent> InfoType;
        [ReadOnly] public ArchetypeChunkComponentType<BeamComponent> BeamType;

        public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            var entities = chunk.GetNativeArray(EntityType);
            var chunkBeams = chunk.GetNativeArray(BeamType);
            var chunkInfos = chunk.GetNativeArray(InfoType);
            for (var i = 0; i < chunk.Count; ++i)
            {
                ref var info = ref chunkInfos.AsReadOnlyRef(i);
                if (info.HitGeneration != 0)
                {
                    var entity = entities[i];
                    CommandBuffer.DestroyEntity(chunkIndex, entity);
                    ref var beam = ref chunkBeams.AsReadOnlyRef(i);
                    SparkSystem.Instantiate(CommandBuffer, 0 /* jobIndex */,
                                            SparkPrefabEntity,
                                            info.Position, info.Normal, beam.ColorBitPattern);
                }
            }
        }
    }

	protected override JobHandle OnUpdate(JobHandle handle)
	{
        var job = new Job {
            CommandBuffer = _entityCommandBufferSystem.CreateCommandBuffer().ToConcurrent(),
            SparkPrefabEntity = SparkManager.Prefab,
            EntityType = GetArchetypeChunkEntityType(),
            InfoType = GetArchetypeChunkComponentType<CollisionInfoComponent>(),
            BeamType = GetArchetypeChunkComponentType<BeamComponent>(),
        };
        handle = job.Schedule(_query, handle);
        _entityCommandBufferSystem.AddJobHandleForProducer(handle);
        return handle;
    }
}

[UpdateInGroup(typeof(PresentationSystemGroup))]
public class RenderBeamSystem : ComponentSystem
{
    public const int BatchNum = 64;
    static UnityEngine.Mesh _mesh;
	static UnityEngine.Material _material;

    EntityQuery _query;
    BeamSystem _beamSystem;
    Matrix4x4[][] _matricesInRenderer;
    JobHandle _mProducerHandle;

    public static void Initialize(UnityEngine.Material material)
    {
        _mesh = CreateMesh();
		_material = material;
    }

    static UnityEngine.Mesh CreateMesh()
    {
		var vertices = new Vector3[4];
        vertices[0] = new Vector3(0f, 0f, 1f);
        vertices[1] = new Vector3(0f, 0f, 1f);
        vertices[2] = new Vector3(0f, 0f, 0f);
        vertices[3] = new Vector3(0f, 0f, 0f);
		var uvs = new Vector2[4];
        uvs[0] = new Vector2(0f, 0f);
        uvs[1] = new Vector2(1f, 0f);
        uvs[2] = new Vector2(0f, 1f);
        uvs[3] = new Vector2(1f, 1f);
        var triangles = new int[6];
        triangles[0] = 0;
        triangles[1] = 1;
        triangles[2] = 2;
        triangles[3] = 2;
        triangles[4] = 1;
        triangles[5] = 3;

		var mesh = new UnityEngine.Mesh();
		mesh.name = "beam";
		mesh.vertices = vertices;
		mesh.uv = uvs;
        mesh.triangles = triangles;
		mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 99999999);
        return mesh;
    }

    protected override void OnCreate()
    {
        _query = GetEntityQuery(new EntityQueryDesc() {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<BeamComponent>(),
                },
            });
        _beamSystem = World.GetOrCreateSystem<BeamSystem>();
        _matricesInRenderer = new Matrix4x4[BatchNum][];
        for (var i = 0; i < BatchNum; ++i) {
            _matricesInRenderer[i] = new Matrix4x4[Cv.InstanceLimit];
        }
    }        

    protected override void OnDestroy()
    {
        Sync();
        _matricesInRenderer = null;
    }

    public void AddJobHandleForProducer(JobHandle producerJob)
    {
        _mProducerHandle = JobHandle.CombineDependencies(_mProducerHandle, producerJob);
    }

    void Sync()
    {
        _mProducerHandle.Complete();
        _mProducerHandle = new JobHandle();
    }

	protected override void OnUpdate()
	{
        UnityEngine.Profiling.Profiler.BeginSample("BeamSync");
        Sync();
        UnityEngine.Profiling.Profiler.EndSample(); // BeamSync
        var batchMatrices = _beamSystem.BatchMatrices;
        int num = batchMatrices.Length;
        var matrices = batchMatrices.AsArray();
        int idx = 0;
        UnityEngine.Profiling.Profiler.BeginSample("BeamDrawMeshInstanced");
        while (num > 0) {
            int cnum = num >= Cv.InstanceLimit ? Cv.InstanceLimit : num;
            NativeArray<Matrix4x4>.Copy(matrices, idx*Cv.InstanceLimit, _matricesInRenderer[idx], 0 /* dstIndex */, cnum);
            Graphics.DrawMeshInstanced(_mesh, 0, _material,
                                       _matricesInRenderer[idx], cnum,
                                       null, ShadowCastingMode.Off, false /* receive shadows */,
                                       0 /* layer */, null /* camera */, LightProbeUsage.BlendProbes,
                                       null /* lightProbeProxyVolume */);
            num -= cnum;
            ++idx;
        }
        UnityEngine.Profiling.Profiler.EndSample(); // BeamDrawMeshInstanced
    }
}

} // namespace UTJ {
/*
 * End of BeamManager.cs
 */
