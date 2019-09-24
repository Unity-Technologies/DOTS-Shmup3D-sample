using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Physics;
using Unity.Mathematics;

namespace UTJ {

public struct ExplosionComponent : IComponentData
{
    public float4x4 Matrix;
}

public class ExplosionAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        var data = new ExplosionComponent {
            Matrix = new float4x4(1, 0, 0, 0,
                                   0, 1, 0, 0,
                                   0, 0, 1, 0,
                                   0, 0, 0, 1),
        };
        dstManager.AddComponentData(entity, data);
        dstManager.RemoveComponent(entity, typeof(Unity.Transforms.Translation));
        dstManager.RemoveComponent(entity, typeof(Unity.Transforms.Rotation));
        dstManager.RemoveComponent(entity, typeof(Unity.Transforms.LocalToWorld));
    }
}

} // namespace UTJ {
