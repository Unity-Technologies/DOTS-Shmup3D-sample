using UnityEngine;
using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;

namespace UTJ {

public struct CustomCopyTransformToGameObject : IComponentData {}

[UnityEngine.ExecuteAlways]
[UpdateInGroup(typeof(TransformSystemGroup))]
[UpdateAfter(typeof(EndFrameLocalToParentSystem))]
public class CustomCopyTransformToGameObjectSystem : JobComponentSystem
{
    [BurstCompile]
    struct CopyTransformsJob : IJobParallelForTransform
    {
        [DeallocateOnJobCompletion]
        [ReadOnly] public NativeArray<LocalToWorld> LocalToWorlds;

        public void Execute(int index, TransformAccess transform)
        {
            var value = LocalToWorlds[index];
            transform.position = value.Position;
            transform.rotation = new quaternion(value.Value);
        }
    }

    System.Collections.Generic.List<UnityEngine.Transform> _transformList;
    EntityQuery _query;
    TransformAccessArray _transformAa;

    protected override void OnCreate()
    {
        _query = GetEntityQuery(ComponentType.ReadOnly<CustomCopyTransformToGameObject>(), ComponentType.ReadOnly<LocalToWorld>());
        _transformList = new System.Collections.Generic.List<UnityEngine.Transform>();
    }

    public void AddTransforms(UnityEngine.Transform[] transforms)
    {
        foreach (var tfm in transforms) {
            _transformList.Add(tfm);
        }
        _transformAa = new TransformAccessArray(_transformList.ToArray());
    }
    public void AddTransform(UnityEngine.Transform transform)
    {
        _transformList.Add(transform);
        _transformAa = new TransformAccessArray(_transformList.ToArray());
    }

    public void RemoveTransform(UnityEngine.Transform transform)
    {
        _transformList.Remove(transform);
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var copyTransformsJob = new CopyTransformsJob
        {
            LocalToWorlds = _query.ToComponentDataArray<LocalToWorld>(Allocator.TempJob, out inputDeps),
        };
        return copyTransformsJob.Schedule(_transformAa, inputDeps);
    }
}

} // namespace UTJ {
