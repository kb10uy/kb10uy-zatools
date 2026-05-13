using System;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using KusakaFactory.Zatools.Foundation;
using KusakaFactory.Zatools.Runtime;
using UnityObject = UnityEngine.Object;

namespace KusakaFactory.Zatools.Ndmf.Core
{
    internal static class Cdw
    {
        /// <summary>
        /// メイン処理
        /// </summary>
        /// <param name="referenceRenderer">Mesh の参照元の SkinnedMeshRenderer</param>
        /// <param name="modifyingMesh">対象の Mesh</param>
        /// <param name="parameters">固定されたパラメーター</param>
        /// <param name="wrapperMaterial">割り当てるマテリアル</param>
        /// <exception cref="ArgumentException">頂点数が一致しない場合</exception>
        internal static void Process(SkinnedMeshRenderer referencingRenderer, Mesh modifyingMesh, FixedParameters parameters)
        {
            if (referencingRenderer.sharedMesh.vertexCount != modifyingMesh.vertexCount) throw new ArgumentException("different mesh vertex count");

            var baked = new Mesh();
            referencingRenderer.BakeMesh(baked);
            var bakedVertices = baked.vertices;
            if (bakedVertices == null || bakedVertices.Length < 4) return;
            UnityObject.DestroyImmediate(baked);

            ImmutableArray<int> hullTriangles = ConvexHull.ComputeQuickHull3D(bakedVertices);
            if (hullTriangles.Length < 12) return;

            var originalSubMeshCount = modifyingMesh.subMeshCount;
            var savedTriangles = new int[originalSubMeshCount][];
            for (int i = 0; i < originalSubMeshCount; i++) savedTriangles[i] = modifyingMesh.GetTriangles(i);

            modifyingMesh.subMeshCount = originalSubMeshCount + 1;
            for (int i = 0; i < originalSubMeshCount; i++) modifyingMesh.SetTriangles(savedTriangles[i], i);
            modifyingMesh.SetTriangles(hullTriangles.ToArray(), originalSubMeshCount);
        }

        internal struct FixedParameters : IEquatable<FixedParameters>
        {
            internal static FixedParameters FixFromComponent(ConvexDepthWrapper component)
            {
                return new FixedParameters();
            }

            public bool Equals(FixedParameters other)
            {
                return true;
            }

            public override bool Equals(object obj) => obj is FixedParameters && Equals((FixedParameters)obj);

            public override int GetHashCode() => 0.GetHashCode();

            public static bool operator ==(FixedParameters lhs, FixedParameters rhs) => lhs.Equals(rhs);

            public static bool operator !=(FixedParameters lhs, FixedParameters rhs) => !(lhs == rhs);
        }
    }
}
