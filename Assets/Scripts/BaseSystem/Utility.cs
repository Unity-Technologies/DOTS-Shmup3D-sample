using System.Runtime.InteropServices;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Physics;

namespace UTJ {

public static class Utility {

	public struct WaitForSeconds
	{
		private float _period;
		private double _start;
		public WaitForSeconds(float period, double updateTime)
		{
			_period = period;
			_start = updateTime;
		}
		public bool End(double updateTime)
		{
			return updateTime - _start > _period;
		}
	}

//	public static void MirrorX(ref Quaternion q)
//	{
//		q.y = -q.y;
//		q.z = -q.z;
//	}

	public static Quaternion Inverse(in Quaternion q)
	{
		return new Quaternion(-q.x, -q.y, -q.z, q.w);
	}

	public static Color Lerp3FacttorUnclamped(in Color a, ref Color b, float t)
	{
		return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, 1f);		
	}


    [StructLayout(LayoutKind.Explicit)]
    struct Bytes
    {
        [FieldOffset(0)]
        public uint ivalue;
        
        [FieldOffset(0)]
        public float fvalue;
    }

    public static float ConvColorBitPattern(in Color color)
    {
        var tb = new Bytes();
        tb.ivalue = (uint)((((byte)(color.r*255f) & 0xff)<<0) |
                           (((byte)(color.g*255f) & 0xff)<<8) |
                           (((byte)(color.b*255f) & 0xff)<<16) |
                           (((byte)(color.a*255f) & 0xff)<<24));
        return tb.fvalue;
    }

    public static float3 CalcSpringTorqueRelative(this quaternion rotation, float3 diff, float ratio, float dt, bool relativeUp = true)
    {
        var up = relativeUp ? math.mul(rotation, new float3(0, 1, 0)) : new float3(0, 1, 0);
        var rot = quaternion.LookRotation(math.normalizesafe(diff, new float3(0, 0, 1) /* defaultValue */),
                                          up);
        var inv = math.conjugate(rotation);
        rot = math.mul(rot, inv);
        if (rot.value.w < 0f) {
            rot.value = -rot.value;
        }
        var relativeTorque = math.mul(inv, rot.value.xyz * (ratio * dt));
        return relativeTorque;
    }

    public static bool RaycastGround(CollisionWorld world, float3 target, out float3 hitPos)
    {
        var ray = new RaycastInput {
            Start = target + new float3(0, 10000, 0),
            End = target - new float3(0, 10000, 0),
            Filter = new CollisionFilter {
                BelongsTo = ~0u,
                CollidesWith = 1<<4, // ground
                GroupIndex = 0
            },
        };
        Unity.Physics.RaycastHit hit;
        hitPos = target;
        bool hitted = world.CastRay(ray, out hit);
        if (hitted) {
            hitPos = hit.Position;
        }
        return hitted;
    }

    public static unsafe ref T AsWritableRef<T>(this NativeArray<T> na, int index) where T : struct
    {
        ref var elem = ref UnsafeUtilityEx.ArrayElementAsRef<T>(NativeArrayUnsafeUtility.GetUnsafePtr<T>(na), index);
        return ref elem;
    }

    public static unsafe ref T AsReadOnlyRef<T>(this NativeArray<T> na, int index) where T : struct
    {
        ref var elem = ref UnsafeUtilityEx.ArrayElementAsRef<T>(NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr<T>(na), index);
        return ref elem;
    }
}

} // namespace UTJ {
