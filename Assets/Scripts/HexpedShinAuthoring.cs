using UnityEngine;
using Unity.Entities;

namespace UTJ {

public class HexpedShinAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public unsafe void Convert(Entity entity, EntityManager dstManager,
                               GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new HexpedShinComponent());
    }
}

} // namespace UTJ {
