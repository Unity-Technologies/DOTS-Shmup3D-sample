using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Physics;
using Unity.Mathematics;
using UnityEngine.Serialization;

namespace UTJ {

public struct BeamComponent : IComponentData
{
    public float ColorBitPattern;
    public float Width;
    public float Length;
}

public struct CachedBeamMatrix : IComponentData
{
    public float4x4 Matrix;
}

public class BeamAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public Color color;
    public float width;
    public float length;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        var cbp = Utility.ConvColorBitPattern(in color);
        var data = new BeamComponent {
            ColorBitPattern = cbp,
            Width = width,
            Length = length,
        };
        dstManager.AddComponentData(entity, data);
        dstManager.AddComponentData(entity, new CachedBeamMatrix { Matrix = float4x4.identity, });
        dstManager.AddComponentData(entity, new PhysicsVelocity() { Linear = float3.zero, });
        dstManager.RemoveComponent(entity, typeof(Unity.Transforms.LocalToWorld));
    }
}

} // namespace UTJ {
