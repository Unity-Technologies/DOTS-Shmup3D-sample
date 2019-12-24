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
using Unity.Rendering;
using UnityEngine.Serialization;
using Random = Unity.Mathematics.Random;

namespace UTJ {

[RequiresEntityConversion]
public class ExplosionManager : MonoBehaviour, IDeclareReferencedPrefabs, IConvertGameObjectToEntity
{
    public GameObject prefab;
    public UnityEngine.Material materialExplosion;

    public void DeclareReferencedPrefabs(List<GameObject> gameObjects)
    {
        gameObjects.Add(prefab);
    }

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        var prefabEntity = conversionSystem.GetPrimaryEntity(prefab);
        ExplosionSystem.Initialize(prefabEntity);
        RenderExplosionSystem.Initialize(materialExplosion);
    }
}

public class ExplosionSystem : JobComponentSystem
{
    static Entity _prefabEntity;
    public static Entity PrefabEntity { get { return _prefabEntity; } }

    EntityQuery _query;
    NativeList<Matrix4x4> _batchMatrices;
    public NativeList<Matrix4x4> BatchMatrices => _batchMatrices;
    RenderExplosionSystem _renderExplosionSystem;

    public static void Initialize(Entity prefabEntity)
    {
        _prefabEntity = prefabEntity;
    }

	public static Entity Instantiate(EntityCommandBuffer.Concurrent ecb, int jobIndex, float3 pos, Entity prefab, float time)
	{
		var entity = ecb.Instantiate(jobIndex, prefab);
		ecb.SetComponent(jobIndex, entity, new AlivePeriod { StartTime = time, Period = 1f, });

        var rot = quaternion.identity;
        var mat = new float4x4(rot, pos);
        mat.c0.w = time;
        var random = new Random((uint)pos.GetHashCode());
        var rotZ = random.NextFloat() * math.PI * 2f;
        mat.c1.w = rotZ;
		ecb.SetComponent(jobIndex, entity, new ExplosionComponent { Matrix = mat, });
        return entity;
    }

	public static Entity Instantiate(float3 pos)
	{
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
		var entity = entityManager.Instantiate(_prefabEntity);
#if UNITY_EDITOR
        entityManager.SetName(entity, "explosion");
#endif
		entityManager.SetComponentData(entity, new AlivePeriod { StartTime = (float)UTJ.Time.GetCurrent(), Period = 1f, });

        var rot = quaternion.identity;
        var mat = new float4x4(rot, pos);
        mat.c0.w = UTJ.Time.GetCurrent();
        var random = new Random((uint)pos.GetHashCode());
        var rotZ = random.NextFloat() * math.PI * 2f;
        mat.c1.w = rotZ;
		entityManager.SetComponentData(entity, new ExplosionComponent { Matrix = mat, });
        return entity;
    }

    protected override void OnCreate()
    {
        _query = GetEntityQuery(new EntityQueryDesc() {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<ExplosionComponent>(),
                },
            });
        _batchMatrices = new NativeList<Matrix4x4>(RenderExplosionSystem.BatchNum*Cv.InstanceLimit, Allocator.Persistent);
        _renderExplosionSystem = World.GetOrCreateSystem<RenderExplosionSystem>();
    }        

    protected override void OnDestroy()
    {
        _batchMatrices.Dispose();
    }

    [BurstCompile]
    struct MyJob : IJob
    {
        public float Time;
        [ReadOnly] public ArchetypeChunkComponentType<ExplosionComponent> ExplosionType;
        [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<ArchetypeChunk> ChunkArray;
        public NativeList<Matrix4x4> Matrices;
        
        public void Execute()
        {
            for (var j = 0; j < ChunkArray.Length; ++j) {
                var chunk = ChunkArray[j];
                var explosions = chunk.GetNativeArray(ExplosionType);
                for (var i = 0; i < chunk.Count; ++i) {
                    var mat = explosions[i].Matrix;
                    Matrices.Add(mat);
                }
            }
        }
    }
    
	protected override unsafe JobHandle OnUpdate(JobHandle handle)
	{
        _batchMatrices.Clear();

        var chunkArray = _query.CreateArchetypeChunkArray(Allocator.TempJob);
        var job = new MyJob {
            Time = UTJ.Time.GetCurrent(),
            ExplosionType = GetArchetypeChunkComponentType<ExplosionComponent>(),
            ChunkArray = chunkArray,
            Matrices = _batchMatrices,
        };
        handle = job.Schedule(handle);
        _renderExplosionSystem.AddJobHandleForProducer(handle);
        return handle;
    }
}

[UpdateInGroup(typeof(PresentationSystemGroup))]
public class RenderExplosionSystem : ComponentSystem
{
    public const int BatchNum = 32;
    static UnityEngine.Mesh _mesh;
	static UnityEngine.Material _material;
	static readonly int MaterialCurrentTime = Shader.PropertyToID("_CurrentTime");

    EntityQuery _query;
    ExplosionSystem _explosionSystem;
    Matrix4x4[][] _matricesInRenderer;
    JobHandle _producerHandle;

    public static void Initialize(UnityEngine.Material material)
    {
        _mesh = CreateMesh();
		_material = material;
    }

    static UnityEngine.Mesh CreateMesh()
    {
		var vertices = new Vector3[4] {
            new Vector3(0f, 0f, 0f),
            new Vector3(0f, 0f, 0f),
            new Vector3(0f, 0f, 0f),
            new Vector3(0f, 0f, 0f),
        };
		var uvs = new Vector2[4] {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
        };
        var triangles = new int[6] {
            0, 1, 2, 2, 1, 3,
        };

		var mesh = new UnityEngine.Mesh();
		mesh.name = "explosion";
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
                    ComponentType.ReadOnly<ExplosionComponent>(),
                },
            });
        _explosionSystem = World.GetOrCreateSystem<ExplosionSystem>();
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
        _producerHandle = JobHandle.CombineDependencies(_producerHandle, producerJob);
    }

    void Sync()
    {
        _producerHandle.Complete();
        _producerHandle = new JobHandle();
    }

	protected override void OnUpdate()
	{
        var currentTime = UTJ.Time.GetCurrent();
		_material.SetFloat(MaterialCurrentTime, currentTime);

        Sync();
        var batchMatrices = _explosionSystem.BatchMatrices;
        int num = batchMatrices.Length;
        var matrices = batchMatrices.AsArray();
        int idx = 0;
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
    }
}

} // namespace UTJ {
