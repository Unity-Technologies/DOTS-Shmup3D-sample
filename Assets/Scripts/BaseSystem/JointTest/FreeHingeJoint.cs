using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Unity.Physics.Authoring
{
    public class FreeHingeJoint : BaseJoint
    {
        [Tooltip("If checked, PositionInConnectedEntity and HingeAxisInConnectedEntity will be set to match PositionLocal and HingeAxisLocal")]
        public bool autoSetConnected = true;

        public float3 positionLocal;
        public float3 positionInConnectedEntity;
        public float3 hingeAxisLocal;
        public float3 hingeAxisInConnectedEntity;

        public override unsafe void Create(EntityManager entityManager)
        {
            if (autoSetConnected)
            {
                RigidTransform bFromA = math.mul(math.inverse(WorldFromB), WorldFromA);
                positionInConnectedEntity = math.transform(bFromA, positionLocal);
                hingeAxisInConnectedEntity = math.mul(bFromA.rot, hingeAxisLocal);
            }

            CreateJointEntity(JointData.CreateHinge(
                positionLocal, positionInConnectedEntity, 
                hingeAxisLocal, hingeAxisInConnectedEntity),
                entityManager);
        }
    }
}
