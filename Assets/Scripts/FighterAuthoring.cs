using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Physics;

namespace UTJ {

public static class FighterConfig
{
    public const int SearchNum = 7;
    public static readonly float3 BurnerLeft = new float3(0.36f, 0.102f, -1.06f);
    public static readonly float3 BurnerRight = new float3(-0.36f, 0.102f, -1.06f);
}

public struct FighterTargetable : IComponentData
{
}

public struct FighterComponent : IComponentData
{
    public Entity TargetEntity;
    public float3 Target;
    public float LastBeamFired;
    public float LastDistortionEmitted;
    public ControllerReplay ControllerReplay;
}

#if SEARCHING
[InternalBufferCapacity(FighterConfig.SEARCH_NUM)]
public struct FighterSearchInputBuffer : IBufferElementData
{
    public ColliderCastInput Value;
}

[InternalBufferCapacity(FighterConfig.SEARCH_NUM)]
public struct FighterSearchHitDistanceBuffer : IBufferElementData
{
    public float Value;
}
#endif

public class FighterAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public unsafe void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new FighterComponent());

#if SEARCHING
        dstManager.AddBuffer<FighterSearchInputBuffer>(entity);
        {
            var buffer = dstManager.GetBuffer<FighterSearchInputBuffer>(entity);
            for (var i = 0; i < FighterConfig.SEARCH_NUM; ++i)
                buffer.Add(new FighterSearchInputBuffer());
        }
        dstManager.AddBuffer<FighterSearchHitDistanceBuffer>(entity);
        {
            var buffer = dstManager.GetBuffer<FighterSearchHitDistanceBuffer>(entity);
            for (var i = 0; i < FighterConfig.SEARCH_NUM; ++i)
                buffer.Add(new FighterSearchHitDistanceBuffer());
        }
#endif
    }
}

} // namespace UTJ {
