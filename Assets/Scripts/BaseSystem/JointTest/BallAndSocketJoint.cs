using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Unity.Physics.Authoring
{
    public class BallAndSocketJoint : BaseJoint
    {
        [Tooltip("If checked, PositionLocal will snap to match PositionInConnectedEntity")]
        public bool autoSetConnected = true;

        public float3 positionLocal;
        public float3 positionInConnectedEntity;

        public override unsafe void Create(EntityManager entityManager)
        {
            if (autoSetConnected)
            {
                RigidTransform bFromA = math.mul(math.inverse(WorldFromB), WorldFromA);
                positionInConnectedEntity = math.transform(bFromA, positionLocal);
            }

            CreateJointEntity(JointData.CreateBallAndSocket(positionLocal, positionInConnectedEntity), entityManager);
        }
    }
}
