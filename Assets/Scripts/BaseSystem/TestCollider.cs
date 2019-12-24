using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using Unity.Physics.Authoring;

public class TestCollider : MonoBehaviour
{
    Entity _entity;

    void Start()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityArchetype colliderArchetype = em.CreateArchetype(typeof(Translation)
                                                                , typeof(Rotation)
                                                                , typeof(PhysicsCollider)
                                                                // , typeof(PhysicsMass)
                                                                , typeof(PhysicsVelocity)
                                                                // , typeof(PhysicsDamping)
                                                                // , typeof(PhysicsGravityFactor)
                                                                );
        var entity = em.CreateEntity(colliderArchetype);
        _entity = entity;
#if UNITY_EDITOR
        em.SetName(entity, "MyTestCollider");
#endif
        em.SetComponentData(entity, new Translation { Value = new float3(-0.5f, 23.81f, 0.5f), });
        var colli = new PhysicsCollider {
            Value = Unity.Physics.SphereCollider.Create(new SphereGeometry { Center = new float3(0,0,0), Radius = 0.5f, },
            new Unity.Physics.CollisionFilter { BelongsTo = ~0u, CollidesWith = ~0u, }, new Unity.Physics.Material {
                Friction = 0,
                FrictionCombinePolicy = Unity.Physics.Material.CombinePolicy.Maximum,
                Restitution = 0f,
                RestitutionCombinePolicy = Unity.Physics.Material.CombinePolicy.Maximum,
                Flags = 0,
            }),
        };
        em.SetComponentData(entity, colli);
        em.SetComponentData(entity, new Rotation { Value = new quaternion(0f, 0f, 0f, 1f), });
        // em.SetComponentData(entity, PhysicsMass.CreateKinematic(MassProperties.UnitSphere));
        // em.SetComponentData(entity, PhysicsMass.CreateDynamic(MassProperties.UnitSphere, 1f));
        // em.SetComponentData(entity, new PhysicsVelocity { Linear = new float3(0,0,0), Angular = new float3(0,0,0), });
    }

    void LateUpdate()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        em.SetComponentData(_entity, new Translation { Value = new float3(Mathf.Sin(Time.time)-0.5f, 23.81f, 0.5f), });
    }
}
