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
using UnityEngine.Serialization;
using Random = Unity.Mathematics.Random;

namespace UTJ {

[RequiresEntityConversion]
public class TrailManager : MonoBehaviour, IDeclareReferencedPrefabs, IConvertGameObjectToEntity
{
    public GameObject prefab;
    public Material materialTrail;
    static Entity _prefabEntity;
    public static Entity Prefab => _prefabEntity;

    Random _random;

    public void DeclareReferencedPrefabs(List<GameObject> gameObjects)
    {
        gameObjects.Add(prefab);
    }

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        _prefabEntity = conversionSystem.GetPrimaryEntity(prefab);
        _random.InitState(12345678u);
        TrailSystem.Initialize(_prefabEntity);
        RenderTrailSystem.Initialize(materialTrail);
    }
}

public class TrailSystem : JobComponentSystem
{
    static Entity _prefabEntity;
    EntityQuery _query;
    NativeList<Matrix4x4> _batchMatrices;
    public NativeList<Matrix4x4> BatchMatrices => _batchMatrices;
    RenderTrailSystem _renderTrailSystem;
    NativeArray<TrailPoint> _trailBuffer;
    public NativeArray<TrailPoint> TrailBuffer => _trailBuffer;

    public static void Initialize(Entity prefabEntity)
    {
        _prefabEntity = prefabEntity;
    }

	public static Entity Instantiate(Entity referingEntity, float3 pos, float width, Color color, float3 offset,
                                     float updateInterval = 1f/60f)
	{
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
		var entity = em.Instantiate(_prefabEntity);
#if UNITY_EDITOR
        em.SetName(entity, "trail");
#endif
        em.SetComponentData(entity, new TrailComponent {
                ReferingEntity = referingEntity,
                UpdateInterval = updateInterval,
                Offset = offset,
                ColorBitPattern = Utility.ConvColorBitPattern(in color),
                Width = width,
                PointIndex = 0,
            });
        var buffer = em.GetBuffer<TrailPoint>(entity);
        for (var i = 0; i < TrailConfig.NodeNum; ++i)
            buffer.Add(new TrailPoint { Position = pos, Time = 0, });
		em.SetComponentData(entity, AlivePeriod.Create(UTJ.Time.GetCurrent(), TrailConfig.AliveTime));
        return entity;
	}

	public static Entity Instantiate(EntityCommandBuffer.Concurrent ecb, int jobIndex, Entity prefab, Entity referingEntity, float3 pos, float width, Color color, float time, float updateInterval = 1f/60f)
	{
		var entity = ecb.Instantiate(jobIndex, prefab);
        ecb.SetComponent(jobIndex, entity, new TrailComponent {
            ReferingEntity = referingEntity,
            UpdateInterval = updateInterval,
            ColorBitPattern = Utility.ConvColorBitPattern(in color),
            Width = width,
            PointIndex = 0,
        });
        var buffer = ecb.SetBuffer<TrailPoint>(jobIndex, entity);
        for (var i = 0; i < TrailConfig.NodeNum; ++i)
            buffer.Add(new TrailPoint { Position = pos, Time = time, });
		ecb.SetComponent(jobIndex, entity, AlivePeriod.Create(time, TrailConfig.AliveTime));
        return entity;
	}

    protected override void OnCreate()
    {
        _query = GetEntityQuery(new EntityQueryDesc() {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<TrailComponent>(),
                },
            });
        _batchMatrices = new NativeList<Matrix4x4>(RenderTrailSystem.BatchNum * Cv.InstanceLimit, Allocator.Persistent);
        _renderTrailSystem = World.GetOrCreateSystem<RenderTrailSystem>();
        _trailBuffer = new NativeArray<TrailPoint>(RenderTrailSystem.BatchNum * Cv.InstanceLimit * TrailConfig.NodeNum, Allocator.Persistent);
    }        

    protected override void OnDestroy()
    {
        _trailBuffer.Dispose();
        _batchMatrices.Dispose();
    }

    struct MyJob : IJob
    {
        public float Time;
        [ReadOnly] public ComponentDataFromEntity<Translation> TranslationsFromEntity;
        [ReadOnly] public ComponentDataFromEntity<Rotation> RotationsFromEntity;
        public ArchetypeChunkComponentType<TrailComponent> MTrailComponentType;
        public ArchetypeChunkBufferType<TrailPoint> MTrailPointsBufferType;
        public ArchetypeChunkComponentType<AlivePeriod> MAlivePeriodType;
        [DeallocateOnJobCompletion] public NativeArray<ArchetypeChunk> ChunkArray;
        public NativeArray<TrailPoint> TrailBuffer;
        public NativeList<Matrix4x4> Matrices;
        
        public void Execute()
        {
            var instanceID = 0;
            for (var j = 0; j < ChunkArray.Length; ++j) {
                var chunk = ChunkArray[j];
                var trails = chunk.GetNativeArray(MTrailComponentType);
                var trailPointsBuffers = chunk.GetBufferAccessor(MTrailPointsBufferType);
                var alivePeriods = chunk.GetNativeArray(MAlivePeriodType);
                for (var i = 0; i < chunk.Count; ++i) {
                    ref var trail = ref trails.AsWritableRef(i);

                    // record trail
                    DynamicBuffer<TrailPoint> trailPoints = trailPointsBuffers[i];
                    var trailPoint = trailPoints[trail.PointIndex];
                    float3 referingPos;
                    if (TranslationsFromEntity.Exists(trail.ReferingEntity)) {
                        var referRot = RotationsFromEntity[trail.ReferingEntity].Value;
                        var referPos = TranslationsFromEntity[trail.ReferingEntity].Value;
                        referingPos = math.mul(referRot, trail.Offset) + referPos;
                        ref var ap = ref alivePeriods.AsWritableRef(i);
                        ap.Reset(Time);
                    } else {
                        referingPos = trailPoint.Position;
                    }

                    if (Time - trailPoint.Time >= trail.UpdateInterval) {
                        ++trail.PointIndex;
                        if (trail.PointIndex >= TrailConfig.NodeNum)
                            trail.PointIndex = 0;
                        trailPoint.Position = referingPos;
                        trailPoint.Time = Time;
                    }　else {
                        trailPoint.Position = referingPos;
                    }
                    trailPoints[trail.PointIndex] = trailPoint;

                    var hidx = trail.PointIndex;

                    // buffer copy
                    var buf = trailPoints.AsNativeArray();
                    NativeArray<TrailPoint>.Copy(buf, 0,
                                                 TrailBuffer, Matrices.Length * TrailConfig.NodeNum,
                                                 TrailConfig.NodeNum);

                    // draw matrix
                    var mat = Matrix4x4.identity;
                    mat[0, 3] = math.asfloat(instanceID%Cv.InstanceLimit);
                    mat[3, 0] = trail.ColorBitPattern;
                    mat[3, 1] = math.asfloat(hidx);
                    mat[3, 2] = trail.Width;
                    mat[3, 3] = math.asfloat((int)((Matrices.Length+1) / Cv.InstanceLimit)); // bulk

                    Matrices.Add(mat);
                    ++instanceID;
                }
            }
        }
    }
    
	protected override JobHandle OnUpdate(JobHandle handle)
	{
        _batchMatrices.Clear();

        var chunkArray = _query.CreateArchetypeChunkArray(Allocator.TempJob, out var gatherJobHandle);
        handle = JobHandle.CombineDependencies(gatherJobHandle, handle);
        var job = new MyJob {
            Time = UTJ.Time.GetCurrent(),
            TranslationsFromEntity = GetComponentDataFromEntity<Translation>(true /* isReadOnly */),
            RotationsFromEntity = GetComponentDataFromEntity<Rotation>(true /* isReadOnly */),
            MTrailComponentType = GetArchetypeChunkComponentType<TrailComponent>(false /* isReadOnly */),
            MTrailPointsBufferType = GetArchetypeChunkBufferType<TrailPoint>(false /* isReadOnly */),
            MAlivePeriodType = GetArchetypeChunkComponentType<AlivePeriod>(false /* isReadOnly */),
            ChunkArray = chunkArray,
            TrailBuffer = _trailBuffer,
            Matrices = _batchMatrices,
        };
        handle = job.Schedule(handle);
        _renderTrailSystem.AddJobHandleForProducer(handle);
        return handle;
    }
}

[UpdateInGroup(typeof(PresentationSystemGroup))]
public class RenderTrailSystem : ComponentSystem
{
    public const int BatchNum = 32;
    static Mesh _mesh;
	static Material _material;
	static readonly int MaterialCurrentTime = Shader.PropertyToID("_CurrentTime");
    static ComputeBuffer _trailCbuffer; // これがあるので現状はWorldの多重化はできない

    EntityQuery _query;
    TrailSystem _trailSystem;
    Matrix4x4[][] _matricesInRenderer;
    JobHandle _mProducerHandle;

    public static void Initialize(Material material)
    {
        _mesh = CreateMesh();
        _material = material;
        _material.SetInt("_NodeNum", TrailConfig.NodeNum);
        _material.SetBuffer("_TrailBuffer", _trailCbuffer);
        _material.SetFloat("_AliveTimeR", 1.0f/TrailConfig.AliveTime);
    }

    static Mesh CreateMesh()
    {
		var vertices = new Vector3[TrailConfig.NodeNum*2];
		for (var i = 0; i < TrailConfig.NodeNum*2; ++i) {
			vertices[i] = new Vector3(0f, 0f, 0f);
		}
		var uvs = new Vector2[TrailConfig.NodeNum*2];
		for (var i = 0; i < TrailConfig.NodeNum; ++i) {
            if (i == 0) {
                uvs[i*2+0] = new Vector2(0f, 0f);
                uvs[i*2+1] = new Vector2(1f, 0f);
            } else if (i != TrailConfig.NodeNum-1) {
                uvs[i*2+0] = new Vector2(0f, 0.5f);
                uvs[i*2+1] = new Vector2(1f, 0.5f);
            } else {
                uvs[i*2+0] = new Vector2(0f, 1f);
                uvs[i*2+1] = new Vector2(1f, 1f);
            }
		}
		var triangles = new int[(TrailConfig.NodeNum-1)*6];
		for (var i = 0; i < TrailConfig.NodeNum-1; ++i) {
            triangles[i*6+0] = (i+0)*2+0;
            triangles[i*6+1] = (i+0)*2+1;
            triangles[i*6+2] = (i+1)*2+0;
            triangles[i*6+3] = (i+1)*2+0;
            triangles[i*6+4] = (i+0)*2+1;
            triangles[i*6+5] = (i+1)*2+1;
		}

		// mesh setup
		var mesh = new Mesh();
		mesh.name = "trail";
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
                    ComponentType.ReadOnly<TrailComponent>(),
                },
            });
        _trailSystem = World.GetOrCreateSystem<TrailSystem>();
        _matricesInRenderer = new Matrix4x4[BatchNum][];
        for (var i = 0; i < BatchNum; ++i) {
            _matricesInRenderer[i] = new Matrix4x4[Cv.InstanceLimit];
        }
        _trailCbuffer = new ComputeBuffer(BatchNum * Cv.InstanceLimit * TrailConfig.NodeNum, System.Runtime.InteropServices.Marshal.SizeOf(typeof(TrailPoint)));
    }        

    protected override void OnDestroy()
    {
        Sync();
        _trailCbuffer.Release();
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
        Sync();
        var batchMatrices = _trailSystem.BatchMatrices;
        int num = batchMatrices.Length;
        var currentTime = UTJ.Time.GetCurrent();
		_material.SetFloat(MaterialCurrentTime, currentTime);
        var trailBuffer = _trailSystem.TrailBuffer;
        _trailCbuffer.SetData(trailBuffer, 0 /* managedBufferStartIndex */, 0 /* computeBufferStartIndex */, num * TrailConfig.NodeNum);
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
