using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;
using Random = Unity.Mathematics.Random;

namespace UTJ {

public static class Debris
{
    static Random _random = new Random();
	static Matrix4x4 _prevViewMatrix;
	static int _delayStartCount = 2;
    static Matrix4x4[] _matricesInRenderer;

	static readonly int MaterialTargetPosition = Shader.PropertyToID("_TargetPosition");
	static readonly int MaterialPrevInvMatrix = Shader.PropertyToID("_PrevInvMatrix");
	static readonly int MaterialColor = Shader.PropertyToID("_BaseColor");
    static Mesh _mesh;
    static Material _material;

    const int POINT_MAX = 1024*8;
    const float RANGE = 32f;

    public static void Initialize(Material material)
    {
        // pool_ = new ObjectPool<Spark>(NUM*BATCH_NUM);
        _random.InitState(12345);
        _mesh = CreateMesh(material);
        _matricesInRenderer = new Matrix4x4[1] { Matrix4x4.identity, };
    }

    static Mesh CreateMesh(Material material)
    {
        var random = new Random();
        random.InitState(12345);

		var vertices = new Vector3[POINT_MAX*2];
		for (var i = 0; i < POINT_MAX; ++i) {
			float x = random.NextFloat(-RANGE, RANGE);
			float y = random.NextFloat(-RANGE, RANGE);
			float z = random.NextFloat(-RANGE, RANGE);
			var point = new Vector3(x, y, z);
			vertices[i*2+0] = point;
			vertices[i*2+1] = point;
		}
		var indices = new int[POINT_MAX*2];
		for (var i = 0; i < POINT_MAX*2; ++i) {
			indices[i] = i;
		}
		var colors = new Color[POINT_MAX*2];
		for (var i = 0; i < POINT_MAX; ++i) {
			colors[i*2+0] = new Color(0f, 0f, 0f /* not used */, 1f);
			colors[i*2+1] = new Color(0f, 0f, 0f /* not used */, 0f);
		}
		var uvs = new Vector2[POINT_MAX*2];
		for (var i = 0; i < POINT_MAX; ++i) {
			uvs[i*2+0] = new Vector2(1f, 0f);
			uvs[i*2+1] = new Vector2(0f, 1f);
		}

        var mesh = new Mesh();
		mesh.name = "debris";
		mesh.vertices = vertices;
		mesh.colors = colors;
		mesh.uv = uvs;
		mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 99999999);
		mesh.SetIndices(indices, MeshTopology.Lines, 0);
		mesh.UploadMeshData(true /* markNoLogerReadable */);

        _material = material;
		material.SetFloat("_Range", RANGE);
		material.SetFloat("_RangeR", 1f/RANGE);
        const float cpower = 0.4f;
		material.SetColor(MaterialColor, new Color(cpower, cpower, cpower));

        // mesh_ = mesh;
        return mesh;
    }

    public static void Render(Camera camera)
	{
		if (_delayStartCount > 0) {
			_prevViewMatrix = camera.worldToCameraMatrix;
			--_delayStartCount;
			return;
		}
		var targetPosition = camera.transform.TransformPoint(new Vector3(0f, 0f, RANGE*0.5f));
		var matrix = _prevViewMatrix * camera.cameraToWorldMatrix; // prev-view * inverted-cur-view
		_material.SetVector(MaterialTargetPosition, targetPosition);
		_material.SetMatrix(MaterialPrevInvMatrix, matrix);
		_prevViewMatrix = camera.worldToCameraMatrix;
        Graphics.DrawMeshInstanced(_mesh, 0, _material,
                                   _matricesInRenderer, 1 /* count */,
                                   null, ShadowCastingMode.Off, false /* receive shadows */,
                                   0 /* layer */, null /* camera */, LightProbeUsage.BlendProbes,
                                   null /* lightProbeProxyVolume */);
    }
}

} // namespace UTJ {
