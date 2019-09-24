using UnityEngine;
using Unity.Entities;
using Unity.Physics;

namespace UTJ {

public struct ObstacleDistanceSettingComponent : IComponentData
{
    public CollisionFilter Filter;
}

public struct ObstacleDistanceComponent : IComponentData
{
    public float Distance;
}

public class ObstacleDistanceAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public unsafe void Convert(Entity entity, EntityManager dstManager,
                               GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new ObstacleDistanceSettingComponent {
                Filter = new CollisionFilter {
                    BelongsTo = (1<<0), // player
                    CollidesWith = (1<<2), // enemy
                    GroupIndex = 0,
                }
            });
        dstManager.AddComponentData(entity, new ObstacleDistanceComponent {
                Distance = float.MaxValue,
            });
    }
}

} // namespace UTJ {
