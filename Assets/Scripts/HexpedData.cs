using System;
using Unity.Collections;
using Unity.Mathematics;

namespace UTJ {

public struct HexpedData : IDisposable
{
    public const float Magnify = 4f;
    public const float LengthBase = 1.5f*Magnify;
    public const float LengthGroin = 1.0f*Magnify;
    public const float LengthThigh = 2.9f*Magnify;
    public const float LengthShin = 5f*Magnify;
    const float LENGTH_REGULAR = (LengthBase + LengthGroin + LengthThigh + LengthShin)/3f*2f;
    public const float HeightRegular = 8f;
    public float3 FromBodyToGroin;
    public float3 FromGroinToThigh;
    public float3 FromThighToShin;
    public NativeArray<float> GroinYaws;
    public NativeArray<quaternion> GroinRotations;
    public NativeArray<float3> GroinPositions;
    public quaternion ThighRotation;
    public quaternion ShinRotation;
    public NativeArray<float3> RegularPoint;
    public NativeArray<float3> WayPointList;

    public static HexpedData Create()
    {
        var result = new HexpedData
        {
            FromBodyToGroin = new float3(0, 0, LengthBase),
            FromGroinToThigh = new float3(0, 0, LengthGroin),
            FromThighToShin = new float3(0, 0, LengthThigh),
            GroinYaws = new NativeArray<float>(6, Allocator.Persistent)
            {
                [0] = math.radians(30),
                [1] = math.radians(90),
                [2] = math.radians(150),
                [3] = math.radians(210),
                [4] = math.radians(270),
                [5] = math.radians(330)
            }
        };
        result.GroinRotations = new NativeArray<quaternion>(6, Allocator.Persistent);
        for (var i = 0; i < 6; ++i) {
            result.GroinRotations[i] = quaternion.Euler(0f, result.GroinYaws[i], 0f);
        }
        result.GroinPositions = new NativeArray<float3>(6, Allocator.Persistent);
        for (var i = 0; i < 6; ++i) {
            result.GroinPositions[i] = math.mul(result.GroinRotations[i], result.FromBodyToGroin);
        }
        result.ThighRotation = quaternion.identity;
        result.ShinRotation = quaternion.identity;
        result.RegularPoint = new NativeArray<float3>(6, Allocator.Persistent);
        for (var i = 0; i < 6; ++i) {
            result.RegularPoint[i] = math.mul(result.GroinRotations[i], new float3(0, -HeightRegular, LENGTH_REGULAR));
        }

        {
            int idx = 0;
            result.WayPointList = new NativeArray<float3>(16, Allocator.Persistent)
            {
                [idx++] = new float3(0, 0, 8),
                [idx++] = new float3(0, 0, 8),
                [idx++] = new float3(0, 0, 8),
                [idx++] = new float3(0, 0, 8),
                [idx++] = new float3(8, 0, 0),
                [idx++] = new float3(8, 0, 0),
                [idx++] = new float3(8, 0, 0),
                [idx++] = new float3(8, 0, 0),
                [idx++] = new float3(0, 0, -8),
                [idx++] = new float3(0, 0, -8),
                [idx++] = new float3(0, 0, -8),
                [idx++] = new float3(0, 0, -8),
                [idx++] = new float3(-8, 0, 0),
                [idx++] = new float3(-8, 0, 0),
                [idx++] = new float3(-8, 0, 0),
                [idx++] = new float3(-8, 0, 0)
            };
        }

        return result;
    }

    public void Dispose()
    {
        RegularPoint.Dispose();
        WayPointList.Dispose();
        GroinPositions.Dispose();
        GroinRotations.Dispose();
        GroinYaws.Dispose();
    }
}

} // namespace UTJ {
