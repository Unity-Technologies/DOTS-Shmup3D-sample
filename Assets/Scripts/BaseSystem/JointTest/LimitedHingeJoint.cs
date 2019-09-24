using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Unity.Physics.Authoring
{
    public class LimitedHingeJoint : FreeHingeJoint
    {
        // Editor only settings
        [HideInInspector]
        public bool editPivots;
        [HideInInspector]
        public bool editAxes;
        [HideInInspector]
        public bool editLimits;

        public float3 perpendicularAxisLocal;
        public float3 perpendicularAxisInConnectedEntity;
        public float minAngle;
        public float maxAngle;

        public override unsafe void Create(EntityManager entityManager)
        {
            if (autoSetConnected)
            {
                RigidTransform bFromA = math.mul(math.inverse(WorldFromB), WorldFromA);
                positionInConnectedEntity = math.transform(bFromA, positionLocal);
                hingeAxisInConnectedEntity = math.mul(bFromA.rot, hingeAxisLocal);
                perpendicularAxisInConnectedEntity = math.mul(bFromA.rot, perpendicularAxisLocal);
            }

            CreateJointEntity(JointData.CreateLimitedHinge(
                    positionLocal, positionInConnectedEntity, 
                    math.normalize(hingeAxisLocal), math.normalize(hingeAxisInConnectedEntity),
                    math.normalize(perpendicularAxisLocal), math.normalize(perpendicularAxisInConnectedEntity), 
                    math.radians(minAngle), math.radians(maxAngle)),
                    entityManager);
        }
    }
}
