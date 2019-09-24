using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Serialization;

namespace UTJ {

public struct SparkComponent : IComponentData
{
    public float ColorBitPattern;
}

public class SparkAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public Color color;
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        var data = new SparkComponent { ColorBitPattern = Utility.ConvColorBitPattern(in color), };
        dstManager.AddComponentData(entity, data);
        dstManager.RemoveComponent(entity, typeof(Unity.Transforms.LocalToWorld));
    }
}

} // namespace UTJ {
