using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Physics;
using Unity.Mathematics;

namespace UTJ {

public struct MissileComponent : IComponentData
{
    public float3 Target;
    public float3 Velocity;
}

public class MissileAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public unsafe void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new MissileComponent());
    }
}

} // namespace UTJ {
