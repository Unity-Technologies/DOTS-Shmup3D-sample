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

#if false

namespace UTJ {

public class TestSystem : JobComponentSystem
{
    EntityQuery m_Query;

    protected override void OnCreate()
    {
        m_Query = GetEntityQuery(new EntityQueryDesc() {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<Translation>(),
                },
            });
    }

    // [BurstCompile]
    public struct MyJob : IJobChunk
    {
        [ReadOnly] public ArchetypeChunkComponentType<Translation> TranslationType;
        
        public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            var chunkTranslations = chunk.GetNativeArray(TranslationType);
            Debug.Log(chunk.Count);
            Debug.Log(chunkTranslations.Length);
            Debug.Log(chunkIndex);
            Debug.Log(firstEntityIndex);
        }
    }

	protected override JobHandle OnUpdate(JobHandle handle)
	{
        var job = new MyJob {
            TranslationType = GetArchetypeChunkComponentType<Translation>(true /* isReadOnly */),
        };
        handle = job.Schedule(m_Query, handle);
        return handle;
    }
}

} // namespace UTJ {

#endif
