using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Physics;
using Unity.Mathematics;

namespace UTJ {

public struct HexpedHitComponent : IComponentData
{
    public int HitGeneration;
}

public struct HexpedComponent : IComponentData
{
    public Hexped Hexped;
}

public static class HexpedConfig
{
    public const int Six = 6;
}


[InternalBufferCapacity(HexpedConfig.Six)]
public struct LegTransform : IBufferElementData
{
    public RigidTransform Groin;
    public RigidTransform Thigh;
    public RigidTransform Shin;
}

public struct HexpedGroinComponent : IComponentData
{
    public int Id;
    public Entity Parent;
}

public struct HexpedThighComponent : IComponentData
{
    public int Id;
    public Entity Parent;
}

public struct HexpedShinComponent : IComponentData
{
    public int Id;
    public Entity Parent;
}


public class HexpedAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public unsafe void Convert(Entity entity, EntityManager dstManager,
                               GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new HexpedComponent());
        dstManager.AddComponentData(entity, new HexpedHitComponent { HitGeneration = 0, });
		dstManager.AddBuffer<LegTransform>(entity);
		dstManager.AddComponentData(entity, new FighterTargetable());
    }
}

} // namespace UTJ {
