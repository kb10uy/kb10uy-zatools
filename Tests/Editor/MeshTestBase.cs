using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace KusakaFactory.Zatools.Tests
{
    public abstract class MeshTestBase
    {
        protected const float Tolerance = 1e-4f;

        private readonly List<UnityObject> _trackedObjects = new List<UnityObject>();

        [TearDown]
        public void DestroyTrackedObjects()
        {
            foreach (var trackedObject in _trackedObjects)
            {
                if (trackedObject != null) UnityObject.DestroyImmediate(trackedObject);
            }
            _trackedObjects.Clear();
        }

        protected T Track<T>(T trackedObject) where T : UnityObject
        {
            _trackedObjects.Add(trackedObject);
            return trackedObject;
        }

        protected static void AssumeComputeShaders()
        {
            Assume.That(SystemInfo.supportsComputeShaders, Is.True, "compute shaders are unavailable (running with -nographics?)");
        }

        /// <summary>
        /// Builds a strip of unit quads on the XY plane, centered on X = 0.
        /// Vertex 2i is (i - quadCount / 2, 0, 0) and vertex 2i + 1 is (i - quadCount / 2, 1, 0).
        /// Quad q consists of vertices 2q to 2q + 3; quads are split into submeshes by <paramref name="quadsPerSubMesh"/>.
        /// </summary>
        protected Mesh CreateQuadStrip(int quadCount, int quadsPerSubMesh = int.MaxValue)
        {
            var mesh = Track(new Mesh { name = "Quad Strip" });
            var columnCount = quadCount + 1;
            var vertices = new Vector3[columnCount * 2];
            var uvs = new Vector2[columnCount * 2];
            var normals = new Vector3[columnCount * 2];
            var tangents = new Vector4[columnCount * 2];
            for (var i = 0; i < columnCount; ++i)
            {
                var x = i - quadCount / 2.0f;
                vertices[i * 2] = new Vector3(x, 0, 0);
                vertices[i * 2 + 1] = new Vector3(x, 1, 0);
                uvs[i * 2] = new Vector2((float)i / quadCount, 0);
                uvs[i * 2 + 1] = new Vector2((float)i / quadCount, 1);
            }
            for (var i = 0; i < vertices.Length; ++i)
            {
                normals[i] = new Vector3(0, 0, -1);
                tangents[i] = new Vector4(1, 0, 0, 1);
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.tangents = tangents;

            var perSubMesh = Math.Min(quadsPerSubMesh, quadCount);
            var subMeshCount = (quadCount + perSubMesh - 1) / perSubMesh;
            mesh.subMeshCount = subMeshCount;
            for (var sm = 0; sm < subMeshCount; ++sm)
            {
                var triangles = new List<int>();
                var firstQuad = sm * perSubMesh;
                for (var q = firstQuad; q < Math.Min(firstQuad + perSubMesh, quadCount); ++q) triangles.AddRange(QuadTriangles(q));
                mesh.SetTriangles(triangles, sm);
            }
            return mesh;
        }

        protected static int[] QuadTriangles(int quad)
        {
            var b = quad * 2;
            return new[] { b, b + 1, b + 2, b + 1, b + 3, b + 2 };
        }

        /// <summary>
        /// Builds the eight corners of the unit cube (vertex i is at (i &amp; 1, (i &gt;&gt; 1) &amp; 1, (i &gt;&gt; 2) &amp; 1))
        /// followed by <paramref name="interiorPoints"/>, with a dummy triangle list referencing every vertex.
        /// </summary>
        protected Mesh CreateCubeCloud(params Vector3[] interiorPoints)
        {
            var mesh = Track(new Mesh { name = "Cube Cloud" });
            var vertices = Enumerable.Range(0, 8).Select((i) => new Vector3(i & 1, (i >> 1) & 1, (i >> 2) & 1)).Concat(interiorPoints).ToArray();
            mesh.vertices = vertices;
            mesh.normals = vertices.Select((v) => (v - new Vector3(0.5f, 0.5f, 0.5f)).normalized).ToArray();
            mesh.uv = vertices.Select((v) => new Vector2(v.x, v.y)).ToArray();

            var triangles = new List<int>();
            for (var i = 0; i + 2 < vertices.Length; ++i) triangles.AddRange(new[] { i, i + 1, i + 2 });
            mesh.SetTriangles(triangles, 0);
            return mesh;
        }

        protected static void AddShape(Mesh mesh, string name, Func<int, Vector3> deltaVertex, float frameWeight = 100.0f)
        {
            AddShape(mesh, name, deltaVertex, (_) => Vector3.zero, (_) => Vector3.zero, frameWeight);
        }

        protected static void AddShape(
            Mesh mesh,
            string name,
            Func<int, Vector3> deltaVertex,
            Func<int, Vector3> deltaNormal,
            Func<int, Vector3> deltaTangent,
            float frameWeight = 100.0f)
        {
            var count = mesh.vertexCount;
            mesh.AddBlendShapeFrame(
                name,
                frameWeight,
                Enumerable.Range(0, count).Select(deltaVertex).ToArray(),
                Enumerable.Range(0, count).Select(deltaNormal).ToArray(),
                Enumerable.Range(0, count).Select(deltaTangent).ToArray());
        }

        protected static (Vector3[] Vertices, Vector3[] Normals, Vector3[] Tangents) ShapeDeltas(Mesh mesh, string name, int frame = 0)
        {
            var index = mesh.GetBlendShapeIndex(name);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), $"BlendShape {name} not found");
            var vertices = new Vector3[mesh.vertexCount];
            var normals = new Vector3[mesh.vertexCount];
            var tangents = new Vector3[mesh.vertexCount];
            mesh.GetBlendShapeFrameVertices(index, frame, vertices, normals, tangents);
            return (vertices, normals, tangents);
        }

        protected static string[] ShapeNames(Mesh mesh)
        {
            return Enumerable.Range(0, mesh.blendShapeCount).Select(mesh.GetBlendShapeName).ToArray();
        }

        protected static void SetSingleBoneWeights(Mesh mesh, Func<int, int> boneOf, int boneCount)
        {
            mesh.boneWeights = Enumerable.Range(0, mesh.vertexCount).Select((i) => new BoneWeight { boneIndex0 = boneOf(i), weight0 = 1.0f }).ToArray();
            mesh.bindposes = Enumerable.Repeat(Matrix4x4.identity, boneCount).ToArray();
        }

        protected SkinnedMeshRenderer CreateRenderer(Mesh mesh, params Material[] materials)
        {
            var gameObject = Track(new GameObject("Renderer"));
            var renderer = gameObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.sharedMaterials = materials;
            return renderer;
        }

        protected Transform[] CreateBones(int count)
        {
            return Enumerable.Range(0, count).Select((i) => Track(new GameObject($"Bone{i}")).transform).ToArray();
        }

        protected Material CreateMaterial(string name)
        {
            return Track(new Material(Shader.Find("Hidden/InternalErrorShader")) { name = name });
        }

        /// <summary>
        /// Creates a point-filtered linear float texture. <paramref name="pixels"/> is row-major from the bottom row.
        /// </summary>
        protected Texture2D CreateTexture(int width, int height, params Color[] pixels)
        {
            var texture = Track(new Texture2D(width, height, TextureFormat.RGBAFloat, false, true)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            });
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        protected static void AssertVector(Vector3 expected, Vector3 actual, string message = null)
        {
            Assert.That((actual - expected).magnitude, Is.LessThan(Tolerance), $"{message} expected {expected:F5} but was {actual:F5}");
        }

        protected static void AssertVector(Vector4 expected, Vector4 actual, string message = null)
        {
            Assert.That((actual - expected).magnitude, Is.LessThan(Tolerance), $"{message} expected {expected:F5} but was {actual:F5}");
        }

        protected static void AssertVectors(IReadOnlyList<Vector3> expected, IReadOnlyList<Vector3> actual)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count));
            for (var i = 0; i < expected.Count; ++i) AssertVector(expected[i], actual[i], $"[{i}]");
        }

        protected static void AssertVectors(IReadOnlyList<Vector4> expected, IReadOnlyList<Vector4> actual)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count));
            for (var i = 0; i < expected.Count; ++i) AssertVector(expected[i], actual[i], $"[{i}]");
        }
    }
}
