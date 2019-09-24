using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine.Serialization;

namespace UTJ {

public static class TrailConfig
{
    public const int NodeNum = 16; // must be power of two.
    public const float UpdateDt = 1f/60f;
    public const float AliveTime = UpdateDt * (float)NodeNum;
}

public struct TrailComponent : IComponentData
{
    public Entity ReferingEntity;
    public float UpdateInterval;
    public float3 Offset;
    public float ColorBitPattern;
    public float Width;
    public int PointIndex;
}

[InternalBufferCapacity(TrailConfig.NodeNum)]
public struct TrailPoint : IBufferElementData
{
    public float3 Position;
    public float Time;
}

public class TrailAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public Color color;
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        var data = new TrailComponent { ColorBitPattern = Utility.ConvColorBitPattern(in color), };
        dstManager.AddComponentData(entity, data);

        DynamicBuffer<TrailPoint> buf = dstManager.AddBuffer<TrailPoint>(entity);
        for (var i = 0; i < buf.Length; ++i) {
            buf[i] = new TrailPoint { Position = float3.zero, };
        }

        dstManager.RemoveComponent(entity, typeof(Unity.Transforms.Translation));
        dstManager.RemoveComponent(entity, typeof(Unity.Transforms.Rotation));
        dstManager.RemoveComponent(entity, typeof(Unity.Transforms.LocalToWorld));
    }
}

} // namespace UTJ {
