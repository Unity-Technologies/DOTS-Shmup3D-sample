using UnityEngine;
using Unity.Mathematics;
using Unity.Entities;
using UnityEngine.Serialization;

namespace UTJ {

public struct GroundHeightInfoComponent : IComponentData
{
    public float3 Position;
}

public struct GroundHeightComponent : IComponentData
{
    public float Height;
}

public class GroundHeightAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public float3 position;

    public unsafe void Convert(Entity entity, EntityManager dstManager,
                               GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new GroundHeightInfoComponent { Position = position, });
        dstManager.AddComponentData(entity, new GroundHeightComponent { Height = float.MaxValue, });
    }
}

} // namespace UTJ {
