using UnityEngine;
using Unity.Entities;

namespace UTJ {

public class HexpedThighAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public unsafe void Convert(Entity entity, EntityManager dstManager,
                               GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new HexpedThighComponent());
    }
}

} // namespace UTJ {
