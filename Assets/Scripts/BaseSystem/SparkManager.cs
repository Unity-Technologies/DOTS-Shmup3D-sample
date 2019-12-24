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
using UnityEngine.Serialization;
using Random = Unity.Mathematics.Random;

namespace UTJ {

[RequiresEntityConversion]
public class SparkManager : MonoBehaviour, IDeclareReferencedPrefabs, IConvertGameObjectToEntity
{
    public GameObject prefab;
    public new Camera camera;
    public Material materialSpark;
    private Random _random = new Random();

    static Entity _prefabEntity;
    public static Entity Prefab => _prefabEntity;

    public void DeclareReferencedPrefabs(List<GameObject> gameObjects)
    {
        gameObjects.Add(prefab);
    }

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        _prefabEntity = conversionSystem.GetPrimaryEntity(prefab);
        _random.InitState(12345);
        RenderSparkSystem.Initialize(camera, materialSpark);
    }

    // void Update()
    // {
    //     var pos = random_.NextFloat3Direction();
    //     var norm = math.normalize(pos);
    //     var col = new Color(1,1,1,1);
    //     SparkSystem.instantiate(prefab_entity_, pos, norm, ref col);
    // }
}

public class SparkSystem : JobComponentSystem
{
    EntityQuery _query;
    NativeList<Matrix4x4> _batchMatrices;
    public NativeList<Matrix4x4> BatchMatrices => _batchMatrices;
    RenderSparkSystem _renderSparkSystem;

	public static Entity Instantiate(EntityCommandBuffer.Concurrent ecb, int jobIndex,
                                     Entity prefabEntity,
                                     float3 pos, float3 norm, in Color col, float time)
	{
        return Instantiate(ecb, jobIndex, prefabEntity, pos, norm, Utility.ConvColorBitPattern(in col), time);
    }
    
	public static Entity Instantiate(EntityCommandBuffer.Concurrent ecb, int jobIndex,
                                     Entity prefabEntity,
                                     float3 pos, float3 norm, float colorBitPattern, float time)
    {
		var entity = ecb.Instantiate(jobIndex, prefabEntity);
		ecb.SetComponent(jobIndex, entity, new Translation { Value = pos, });
        var up = new float3(0, 1, 0);
		ecb.SetComponent(jobIndex, entity, new Rotation { Value = quaternion.LookRotationSafe(norm, up), });
		ecb.SetComponent(jobIndex, entity, new AlivePeriod { StartTime = time, Period = 0.5f, });
		ecb.SetComponent(jobIndex, entity, new SparkComponent { ColorBitPattern = colorBitPattern, });
        return entity;
	}

    protected override void OnCreate()
    {
        _query = GetEntityQuery(new EntityQueryDesc() {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<Translation>(),
                    ComponentType.ReadOnly<Rotation>(),
                    ComponentType.ReadOnly<AlivePeriod>(),
                    ComponentType.ReadOnly<SparkComponent>(),
                },
            });
        _batchMatrices = new NativeList<Matrix4x4>(RenderSparkSystem.BatchNum*Cv.InstanceLimit, Allocator.Persistent);
        _renderSparkSystem = World.GetOrCreateSystem<RenderSparkSystem>();
    }        

    protected override void OnDestroy()
    {
        _batchMatrices.Dispose();
    }

    [BurstCompile]
    struct MyJob : IJob
    {
        [ReadOnly] public ArchetypeChunkComponentType<Translation> TranslationType;
        [ReadOnly] public ArchetypeChunkComponentType<Rotation> RotationType;
        [ReadOnly] public ArchetypeChunkComponentType<AlivePeriod> AlivePeriodType;
        [ReadOnly] public ArchetypeChunkComponentType<SparkComponent> SparkType;
        [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<ArchetypeChunk> ChunkArray;
        public NativeList<Matrix4x4> Matrices;
        
        public void Execute()
        {
            for (var j = 0; j < ChunkArray.Length; ++j) {
                var chunk = ChunkArray[j];
                var translations = chunk.GetNativeArray(TranslationType);
                var rotations = chunk.GetNativeArray(RotationType);
                var alivePeriods = chunk.GetNativeArray(AlivePeriodType);
                var sparks = chunk.GetNativeArray(SparkType);
                for (var i = 0; i < chunk.Count; ++i) {
                    var mat = Matrix4x4.TRS((Vector3)translations[i].Value, (Quaternion)rotations[i].Value, Vector3.one);
                    mat[3, 0] = sparks[i].ColorBitPattern;
                    mat[3, 1] = (float)alivePeriods[i].StartTime;
                    Matrices.Add(mat);
                }
            }
        }
    }
    
	protected override JobHandle OnUpdate(JobHandle handle)
	{
        _batchMatrices.Clear();
        var chunkArray = _query.CreateArchetypeChunkArray(Allocator.TempJob);
        var job = new MyJob {
            TranslationType = GetArchetypeChunkComponentType<Translation>(),
            RotationType = GetArchetypeChunkComponentType<Rotation>(),
            AlivePeriodType = GetArchetypeChunkComponentType<AlivePeriod>(),
            SparkType = GetArchetypeChunkComponentType<SparkComponent>(),
            ChunkArray = chunkArray,
            Matrices = _batchMatrices,
        };
        handle = job.Schedule(handle);
        _renderSparkSystem.AddJobHandleForProducer(handle);
        return handle;
    }
}

[UpdateInGroup(typeof(PresentationSystemGroup))]
public class RenderSparkSystem : ComponentSystem
{
	const int PARTICLE_NUM = 16;
    static Mesh _mesh;
	static Material _material;
    static Camera _camera;
	static readonly int MaterialCurrentTime = Shader.PropertyToID("_CurrentTime");
	static readonly int MaterialPreviousTime = Shader.PropertyToID("_PreviousTime");
	static readonly int MaterialPrevInvMatrix = Shader.PropertyToID("_PrevInvMatrix");
    public const int BatchNum = 32;

    EntityQuery _query;
    SparkSystem _sparkSystem;
    Matrix4x4 _prevViewMatrix;
    bool _justAfterReset;
    Matrix4x4[][] _matricesInRenderer;
    
    JobHandle _producerHandle;

    public static void Initialize(Camera camera, Material material)
    {
        var random = new Random();
        random.InitState(12345);
        _mesh = CreateMesh(material, random);
        _camera = camera;
    }

    static Mesh CreateMesh(Material material, Random random)
    {
		var vertices = new Vector3[PARTICLE_NUM*2];
		for (var i = 0; i < PARTICLE_NUM; ++i) {
            float3 point = random.NextFloat3Direction();
            point.z = math.abs(point.z);
			vertices[i*2+0] = point;
			vertices[i*2+1] = point;
		}
		var indices = new int[PARTICLE_NUM*2];
		for (var i = 0; i < PARTICLE_NUM*2; ++i) {
			indices[i] = i;
		}
		var uvs = new Vector2[PARTICLE_NUM*2];
		for (var i = 0; i < PARTICLE_NUM; ++i) {
			uvs[i*2+0] = new Vector2(1f, 0f);
			uvs[i*2+1] = new Vector2(0f, 1f);
		}

		// mesh setup
		var mesh = new Mesh();
		mesh.name = "spark";
		mesh.vertices = vertices;
		mesh.uv = uvs;
		mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 99999999);
		mesh.SetIndices(indices, MeshTopology.Lines, 0);
		_material = material;

        return mesh;
    }

    protected override void OnCreate()
    {
        _query = GetEntityQuery(new EntityQueryDesc() {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<SparkComponent>(),
                },
            });
        _sparkSystem = World.GetOrCreateSystem<SparkSystem>();
        _matricesInRenderer = new Matrix4x4[BatchNum][];
        for (var i = 0; i < BatchNum; ++i) {
            _matricesInRenderer[i] = new Matrix4x4[Cv.InstanceLimit];
        }
		_justAfterReset = true;
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
		if (_justAfterReset) {
			_justAfterReset = false;
			_prevViewMatrix = _camera.worldToCameraMatrix;
			return;
		}

		var matrix = _prevViewMatrix * _camera.cameraToWorldMatrix; // prev-view * inverted-cur-view
        var currentTime = UTJ.Time.GetCurrent();
		_material.SetFloat(MaterialCurrentTime, currentTime);
		_material.SetFloat(MaterialPreviousTime, currentTime - UTJ.Time.GetDt());
		_material.SetMatrix(MaterialPrevInvMatrix, matrix);
        _prevViewMatrix = _camera.worldToCameraMatrix;
        
        Sync();
        var batchMatrices = _sparkSystem.BatchMatrices;
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
