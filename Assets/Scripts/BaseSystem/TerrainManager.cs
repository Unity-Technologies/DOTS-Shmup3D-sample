/* -*- mode:CSharp; coding:utf-8-with-signature -*-
 */

using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Burst;
using Unity.Physics;
using Unity.Rendering;
using UnityEngine.Serialization;
using Random = Unity.Mathematics.Random;

namespace UTJ {

[RequiresEntityConversion]
public class TerrainManager : MonoBehaviour, IDeclareReferencedPrefabs, IConvertGameObjectToEntity
{
    public GameObject prefab;
    public Entity prefabEntity;

    public void DeclareReferencedPrefabs(List<GameObject> gameObjects)
    {
        gameObjects.Add(prefab);
    }

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        prefabEntity = conversionSystem.GetPrimaryEntity(prefab);
    }

    void Start()
    {
        TerrainSystem.Instantiate(prefabEntity);
    }
}

public class TerrainSystem : ComponentSystem
{
	public static Entity Instantiate(Entity prefab)
	{
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
		var entity = entityManager.Instantiate(prefab);
        return entity;
    }

    protected override void OnUpdate()
    {
    }
}

} // namespace UTJ {
