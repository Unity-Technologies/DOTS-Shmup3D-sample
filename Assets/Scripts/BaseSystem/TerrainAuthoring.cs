// #define RECORDING

using System;
using UnityEngine;
using UnityEngine.Assertions;
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Physics;
using Unity.Mathematics;
using UnityEngine.Serialization;

namespace UTJ {

public struct TerrainComponent : IComponentData
{
}

public class TerrainAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public string terrainFilename;

    // td.heightmapResolution : 513
    // td.heightmapScale : (2, 600, 2)
    // td.heightmapTexture : RenderTexture
    // td.heightmapWidth : 513
    // td.heightmapHeight : 513

    struct TerrainInfoHeader
    {
        public const int VERSION = 100;
        public int Version;
        public int Resolution;
        public float3 Scale;
        public int Width;
        public int Height;
        public int Pad;
    }
    struct TerrainInfo : IDisposable
    {
        public TerrainInfoHeader Header;
        public NativeArray<float> Buffer;
        public void Dispose()
        {
            Buffer.Dispose();
        }        
    }

    TerrainInfo ReadTerrainData(string filename)
    {
        var result = new TerrainInfo();
        var path = Application.streamingAssetsPath + "/" + filename;
        using (var reader = new System.IO.BinaryReader(System.IO.File.OpenRead(path))) {
            var header = new TerrainInfoHeader {
                Version = reader.ReadInt32(),
                Resolution = reader.ReadInt32(),
                Scale = new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                Width = reader.ReadInt32(),
                Height = reader.ReadInt32(),
                Pad = reader.ReadInt32(),
            };
            Assert.AreEqual(header.Version, TerrainInfoHeader.VERSION);
            result.Header = header;
            var buffer = new NativeArray<float>(header.Width * header.Height, Allocator.Persistent);
            for (var x = 0; x < header.Width; ++x) {
                for (var y = 0; y < header.Height; ++y) {
                    buffer[x*header.Width + y] = reader.ReadSingle();
                }
            }
            result.Buffer = buffer;
        }
        return result;
    }

#if RECORDING
    void writeTerrainData(TerrainData td, string filename)
    {
        var path = Application.streamingAssetsPath + "/" + filename;
        var width = td.heightmapWidth;
        var height = td.heightmapHeight;
        using (var writer = new System.IO.BinaryWriter(new System.IO.FileStream(path, System.IO.FileMode.Create))) {
            // header
            writer.Write(TerrainInfoHeader.VERSION);
            writer.Write(td.heightmapResolution);
            writer.Write(td.heightmapScale.x);
            writer.Write(td.heightmapScale.y);
            writer.Write(td.heightmapScale.z);
            writer.Write(width);
            writer.Write(height);
            writer.Write(0x5e5e5e5e); // padding

            // body
            var td_heights = td.GetHeights(0 /* xBase */, 0 /* yBase */, width, height);
            for (var x = 0; x < width; ++x) {
                for (var y = 0; y < height; ++y) {
                    writer.Write(td_heights[x, y]);
                }
            }
        }
    }
#endif

    public unsafe void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        var terrain = GetComponent<Terrain>();
#if RECORDING
        if (terrain != null) {
            var td = terrain.terrainData;
            writeTerrainData(td, terrain_filename_);
        }
#endif
        using (var info = ReadTerrainData(terrainFilename)) {
            var filter = new CollisionFilter() {
                BelongsTo = 1<<4,
                CollidesWith = 0xffffffff,
                GroupIndex = 0,
            };
            var colliderData = Unity.Physics.TerrainCollider.Create(info.Buffer,
                                                                    new int2(info.Header.Width, info.Header.Height),
                                                                    info.Header.Scale,
                                                                    Unity.Physics.TerrainCollider.CollisionMethod.VertexSamples,
                                                                    filter,
                                                                    Unity.Physics.Material.Default);
            var colli = new Unity.Physics.PhysicsCollider {
                Value = colliderData,
            };
            dstManager.AddComponentData(entity, colli);
        }

        var data = new TerrainComponent {};
        dstManager.AddComponentData(entity, data);
    }
}

} // namespace UTJ {
