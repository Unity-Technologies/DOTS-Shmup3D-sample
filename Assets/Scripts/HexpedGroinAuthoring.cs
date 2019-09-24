using UnityEngine;
using Unity.Entities;

namespace UTJ {

public class HexpedGroinAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public unsafe void Convert(Entity entity, EntityManager dstManager,
                               GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new HexpedGroinComponent());
    }
}

} // namespace UTJ {
