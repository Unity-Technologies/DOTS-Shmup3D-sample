using System.IO;
using NUnit.Framework;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Assert = NUnit.Framework.Assert;
using Random = Unity.Mathematics.Random;

namespace UTJ {

public class ControllerManagerTests {

	[Test]
	public void ControllerManater_update()
	{
        var random = new Random();
        random.InitState(1234567);

        using (var buffer = new NativeList<ControllerUnit>(ControllerBuffer.MaxFrames, Allocator.Persistent))
        {
            const int NUM = 10;
            double time = 0;
            var file_path0 = Application.dataPath + "/../tmp.txt";
            using (System.IO.StreamWriter sw = new System.IO.StreamWriter(file_path0)) 
            {
                var device = new ControllerDevice(buffer);
                for (var i = 0; i < NUM; ++i) {
                    var ppos = random.NextFloat3();
                    var tpos = random.NextFloat3();
                    device.Update(time, ppos, tpos, true /* testing */);
                    time += 1.0/60.0;
                
                    var unit = device.GetCurrent();
                    sw.WriteLine(unit.ToString());
                }
                Assert.AreEqual(NUM, buffer.Length);

                sw.WriteLine("----");

                for (var i = 0; i < buffer.Length; ++i) {
                    var unit = buffer[i];
                    sw.WriteLine(unit.ToString());
                }

                sw.WriteLine("----");

                var replay = new ControllerReplay();
                for (var i = 0; i < NUM*10; ++i) {
                    var ppos = random.NextFloat3();
                    var tpos = random.NextFloat3();
                    var unit = replay.Step(buffer, ppos, tpos);
                    time += 1.0/60.0;
                    sw.WriteLine(unit.ToString());
                }
            }
        }
	}

	[Test]
	public void ControllerManater_saveload()
	{
        var random = new Random();
        random.InitState(1234567);

        using (var buffer = new NativeList<ControllerUnit>(ControllerBuffer.MaxFrames, Allocator.Persistent)) {
            const int NUM = 10;
            double time = 0;
            var device = new ControllerDevice(buffer);
            for (var i = 0; i < NUM; ++i) {
                var ppos = noise.cnoise(new float2(i, i*2));
                var tpos = noise.cnoise(new float2(i*3, i*4));
                device.Update(time, ppos, tpos, true /* testing */);
                time += 1.0/60.0;
            }
            Assert.AreEqual(NUM, buffer.Length);

            var filename = "test.bin";
            ControllerBuffer.Save(buffer, filename);

            using (var buffer2 = ControllerBuffer.Load<ControllerUnit>(filename))
            {
                for (var i = 0; i < NUM; ++i) {
                    var unitA = buffer[i];
                    var unitB = buffer2[i];
                    Assert.AreEqual(unitA, unitB);
                }
            }
        }
    }

	[Test]
	public void ControllerManater_replay()
	{
        var random = new Random();
        random.InitState(1234567);

        using (var buffer = new NativeList<ControllerUnit>(ControllerBuffer.MaxFrames, Allocator.Persistent)) {
            const int NUM = 10;
            double time = 0;
            var device = new ControllerDevice(buffer);
            for (var i = 0; i < NUM; ++i) {
                var ppos = new float3(1000, 1000, 1000);
                var tpos = new float3(0, 0, 0);
                device.Update(time, ppos, tpos, true /* testing */);
                time += 1.0/60.0;
            }
            Assert.AreEqual(NUM, buffer.Length);

            var filename = "test2.bin";
            ControllerBuffer.Save(buffer, filename);

            using (var buffer2 = ControllerBuffer.Load<ControllerUnit>(filename)) {
                
                //var replay = new ControllerReplay();
                for (var i = 0; i < NUM; ++i) {
                    var ppos = new float3(1000, 1000, 1000);
                    var tpos = new float3(0, 0, 0);
                    var unitA = buffer[i];
                    var unitB = buffer2[i];
                    Assert.AreEqual(unitA, unitB);
                }
            }
        }
    }

	// [Test]
	// public void ControllerManater_repeat()
	// {
    //     var random = new Random();
    //     random.InitState(1234567);

    //     bool differ = false;
    //     var filename = "test3.bin";
    //     using (var buffer = ControllerBuffer.load<ControllerUnit>(filename)) {
    //         var replay = new ControllerReplay();
    //         replay.replay_index_ = 1097;
    //         var ppos = new float3(1000, 1000, 1000);
    //         var tpos = new float3(0, 0, 0);
    //         for (var i = 0; i < 100; ++i) {
    //             var unit = replay.step(buffer, ppos, tpos);
    //             if (replay.replay_index_ != 1097) {
    //                 differ = true;
    //                 break;
    //             }
    //         }
    //     }
    //     Assert.IsTrue(differ);
    // }
}

} // namespace UTJ {
