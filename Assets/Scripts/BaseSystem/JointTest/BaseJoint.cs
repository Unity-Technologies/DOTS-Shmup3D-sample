using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Unity.Physics.Authoring
{
    public abstract class BaseJoint : MonoBehaviour
    {
        public Unity.Physics.Authoring.PhysicsBodyAuthoring connectedBody;

        public RigidTransform WorldFromA => new RigidTransform(gameObject.transform.rotation, gameObject.transform.position);
        public RigidTransform WorldFromB => (connectedBody == null) ? RigidTransform.identity : new RigidTransform(connectedBody.transform.rotation, connectedBody.transform.position);

        private Entity _mEntityA = Entity.Null;
        public Entity EntityA { get => _mEntityA; set => _mEntityA = value; }

        private Entity _mEntityB = Entity.Null;
        public Entity EntityB { get => _mEntityB; set => _mEntityB = value; }
        public bool enableCollision;

        void OnEnable()
        {
            // included so tick box appears in Editor
        }

        protected unsafe void CreateJointEntity(BlobAssetReference<JointData> jointData, EntityManager entityManager)
        {
            var componentData = new PhysicsJoint
            {
                JointData = jointData,
                EntityA = EntityA,
                EntityB = EntityB,
                EnableCollision = enableCollision ? 1 : 0,
            };

            ComponentType[] componentTypes = new ComponentType[1];
            componentTypes[0] = typeof(PhysicsJoint);
            Entity jointEntity = entityManager.CreateEntity(componentTypes);
#if UNITY_EDITOR
            var nameEntityA = entityManager.GetName(EntityA);
            var nameEntityB = EntityB == Entity.Null ? "PhysicsWorld" : entityManager.GetName(EntityB);
            entityManager.SetName(jointEntity, $"Joining {nameEntityA} + {nameEntityB}");
#endif

            if (!entityManager.HasComponent<PhysicsJoint>(jointEntity))
            {
                entityManager.AddComponentData(jointEntity, componentData);
            }
            else
            {
                entityManager.SetComponentData(jointEntity, componentData);
            }
        }

        public abstract unsafe void Create(EntityManager entityManager);

    }
}
