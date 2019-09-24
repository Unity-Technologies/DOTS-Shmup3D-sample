using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Assertions;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using Random = Unity.Mathematics.Random;

namespace UTJ {

public struct ControllerUnit
{
    public const int Version = 100;
    public float Horizontal;
    public float Vertical;
    public bool FireBullet;
    public bool FireMissile;
    public bool Toward;
    public bool Away;
    public float Condition;
    public float Time;

    public override string ToString()
    {
        return $"toward_:{Toward,-5}, condition_:{Condition,-10}, time_:{Time,-10}";
    }
}

public struct ControllerBuffer
{
    public const int MaxFrames = 30*60; // about 36KiB with 20bytes for each frame.

    public static void Save<T>(NativeList<T> list, string filename) where T : struct
    {
        int unitSize = UnsafeUtility.SizeOf<T>();
        NativeArray<T> buffer = list.AsArray();
        int recordedFrameNum = buffer.Length;
        byte[] buf = buffer.Reinterpret<T, byte>().ToArray();
        var path = Application.streamingAssetsPath + "/" + filename;
        using (var writer = new System.IO.BinaryWriter(new System.IO.FileStream(path, System.IO.FileMode.Create))) {
            writer.Write(ControllerUnit.Version);
            writer.Write(unitSize);
            writer.Write(recordedFrameNum);
            writer.Write(0 /* padding */);
            writer.Write(buf, 0 /* index */, unitSize * recordedFrameNum);
        }
    }

    public static NativeList<T> Load<T>(string filename) where T : struct
    {
        NativeList<T> list;
        int unitSize = UnsafeUtility.SizeOf<T>();
        var path = Application.streamingAssetsPath + "/" + filename;
        using (var reader = new System.IO.BinaryReader(System.IO.File.OpenRead(path))) {
            int version = reader.ReadInt32();
            Assert.AreEqual(version, ControllerUnit.Version);
            int recordedUnitSize = reader.ReadInt32();
            Assert.AreEqual(unitSize, recordedUnitSize);
            int recordedFrameNum = reader.ReadInt32();
            reader.ReadInt32(); /* padding */
            int byteCount = unitSize * recordedFrameNum;
            var buf = reader.ReadBytes(byteCount);
            Assert.AreEqual(byteCount, buf.Length);
            var tmp = new NativeArray<T>(recordedFrameNum, Allocator.Temp);
            var tmpBuffer = tmp.Reinterpret<T, byte>();
            tmpBuffer.CopyFrom(buf);
            list = new NativeList<T>(recordedFrameNum, Allocator.Persistent);
            list.AddRange(tmp);
            tmp.Dispose();
            Assert.AreEqual(list.Length, recordedFrameNum);
        }
        return list;
    }
}

public class ControllerDevice
{
    Random _random;
    NativeList<ControllerUnit> _buffer;
    ControllerUnit _currentUnit;
    double _startTime;

    public ControllerDevice(NativeList<ControllerUnit> buffer)
    {
        Assert.IsTrue(buffer.IsCreated);
        _buffer = buffer;
        _random = new Random();
        _random.InitState(12345);
    }
    public ControllerDevice() {}

    public void Start(double time)
    {
        _startTime = time;
    }

    public ControllerUnit GetCurrent()
    {
        return _currentUnit;
    }

    ControllerUnit fetch_input(double time, float3 playerPosition, float3 targetPosition)
    {
        var unit = new ControllerUnit {
            Horizontal = Input.GetAxisRaw("Horizontal"),
            Vertical = Input.GetAxisRaw("Vertical"),
            FireBullet = Input.GetButton("Fire1"),
            FireMissile = Input.GetButtonDown("Fire2"),
            Toward = Input.GetButton("Toward"),
            Away = Input.GetButton("Away"),
            Condition = math.length(targetPosition - playerPosition),
            Time = (float)(time - _startTime),
        };
        return unit;
    }

    ControllerUnit fetch_random(double time)
    {
        var unit = new ControllerUnit {
            Horizontal = _random.NextFloat(),
            Vertical = _random.NextFloat(),
            FireBullet = _random.NextBool(),
            FireMissile = _random.NextBool(),
            Toward = _random.NextBool(),
            Away = _random.NextBool(),
            Condition = _random.NextFloat(),
            Time = (float)(time - _startTime),
        };
        return unit;
    }

    public void Update(double time, float3 playerPosition, float3 targetPosition, bool testing = false)
    {
        var unit = testing ? fetch_random(time) : fetch_input(time, playerPosition, targetPosition);
        if (_buffer.Length < _buffer.Capacity) {
            _buffer.Add(unit);
        }
        if (Input.GetButtonDown("Submit"))
        {
            var filename = "controller.bin";
            Debug.Log("dump file to:"+ filename);
            ControllerBuffer.Save(_buffer, filename);
            Debug.Log("done.");
        }

        if (!unit.Toward && _buffer.Length >= 2) {
            for (var i = _buffer.Length - 2; i >= 0; --i) {
                var oldUnit = _buffer[i];
                if (oldUnit.Toward) {
                    oldUnit.Condition = unit.Condition;
                    _buffer[i] = oldUnit;
                } else {
                    break;
                }
            }
        }

        _currentUnit = unit;
    }

	public ControllerUnit Update()
	{
		return fetch_input(0 /* time */, float3.zero, float3.zero);
	}
}

public struct ControllerReplay
{
    public int ReplayIndex;
    public int RepeatNum;

    public ControllerUnit Step(NativeList<ControllerUnit> buffer,
                               float3 playerPosition,
                               float3 targetPosition)
    {
        if (buffer.Length <= ReplayIndex)
            ReplayIndex %= buffer.Length;
        var unit = buffer[ReplayIndex];

        ++ReplayIndex;
        if (ReplayIndex >= buffer.Length)
            ReplayIndex -= buffer.Length;

        if (unit.Toward) {
            var length = math.length(targetPosition - playerPosition);
            if (length < unit.Condition) {
                for (var i = 0; i < buffer.Length; ++i) {
                    if (buffer[ReplayIndex].Toward) {
                        ++ReplayIndex;
                        if (ReplayIndex >= buffer.Length)
                            ReplayIndex -= buffer.Length;
                    } else {
                        break;
                    }
                }
                unit = buffer[ReplayIndex];
                RepeatNum = 0;
            } else {
                if (!buffer[ReplayIndex].Toward) {
                    ++RepeatNum;
                    if (RepeatNum < 60) {
                        --ReplayIndex;
                        if (ReplayIndex < 0)
                            ReplayIndex += buffer.Length;
                    }
                }
            }
        } else {
            RepeatNum = 0;
        }

        return unit;
    }
}

} // namespace UTJ {
