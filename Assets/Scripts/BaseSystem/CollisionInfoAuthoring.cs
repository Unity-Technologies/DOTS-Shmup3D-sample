using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Physics;
using Unity.Mathematics;

namespace UTJ {

public enum OpponentType
{
    None,
    Bullet,
    Missile,
    Hexped,
}

public struct CollisionInfoSettingComponent : IComponentData
{
    public bool NeedPositionNormal;
    public OpponentType Type;
}

public struct CollisionInfoComponent : IComponentData
{
    public int HitGeneration;
    public Entity Opponent;
    public OpponentType OpponentType;
    public float3 Position;
    public float3 Normal;
}

public class CollisionInfoAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public OpponentType Type;
    public bool NeedPositionNormal;

    public unsafe void Convert(Entity entity, EntityManager dstManager,
                               GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new CollisionInfoSettingComponent {
                NeedPositionNormal = NeedPositionNormal,
                Type = Type,
            });
        dstManager.AddComponentData(entity, new CollisionInfoComponent { HitGeneration = 0, });
    }
}

} // namespace UTJ {
