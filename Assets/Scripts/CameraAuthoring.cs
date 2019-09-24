using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Physics;
using Unity.Mathematics;

namespace UTJ {

public struct CameraComponent : IComponentData
{
    public Entity TargetEntity;
}

public class CameraAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public unsafe void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new CameraComponent());
        dstManager.AddComponentData(entity, new CustomCopyTransformToGameObject());
    }
}

} // namespace UTJ {
