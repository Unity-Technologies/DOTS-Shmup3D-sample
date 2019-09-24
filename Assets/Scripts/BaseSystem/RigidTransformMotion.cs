using System.Runtime.InteropServices;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Physics;

namespace UTJ {

public struct RigidTransformMotion
{
    const float LINEAR_DAMPER = 1f;
    const float ANGULAR_DAMPER = 4f;
    public float3 Linear;
    public float3 Angular;
    
    public void Update(ref RigidTransform rt, float dt)
    {
        Linear -= Linear * (LINEAR_DAMPER * dt);
        rt.pos += Linear * dt;

        Angular -= Angular * (ANGULAR_DAMPER * dt);
        var n = math.mul(rt.rot, Angular) * dt;
        var len2 = math.lengthsq(n);
        var w = math.sqrt(1f - len2);
        var q = new quaternion(n.x, n.y, n.z, w);
        rt.rot = math.mul(q, rt.rot);
        rt.rot = math.normalize(rt.rot);
    }

    void AddImpulse(float3 impulseWorld)
    {
        Linear += impulseWorld;
    }

    public void AddTorqueImpulseLocal(float3 impulseLocal)
    {
        Angular += impulseLocal;
    }

    public void AddSpringForce(in RigidTransform rt, float3 target, float ratio, float dt)
    {
        var diff = target - rt.pos;
        var impulse = diff * (ratio * dt);
        AddImpulse(impulse);
    }

    public void AddSpringTorqueHorizontal(in RigidTransform rt, float ratio, float dt)
    {
        var worldForward = math.mul(rt.rot, new float3(0, 0, 1));
        var horizontalForward = new float3(worldForward.x, 0, worldForward.z);
        var relativeTorque = rt.rot.CalcSpringTorqueRelative(horizontalForward, ratio, dt, false /* relative_up */);
        AddTorqueImpulseLocal(relativeTorque);
    }
}

} // namespace UTJ {
