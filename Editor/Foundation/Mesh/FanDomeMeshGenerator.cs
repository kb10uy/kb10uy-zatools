using System.Collections.Immutable;
using UnityEngine;

namespace KusakaFactory.Zatools.Foundation.Mesh
{
    internal sealed class FanDomeMeshGenerator : IDomeMeshGenerator
    {
        public DomeMeshResult Generate(in DomeMeshInput input)
        {
            var outerPositions = input.OuterPositions;
            var outerNormals = input.OuterNormals;
            var outerBoneWeights = input.OuterBoneWeights;
            var m = outerPositions.Length;
            if (m < 3)
            {
                return new DomeMeshResult(
                    ImmutableArray<Vector3>.Empty,
                    ImmutableArray<Vector3>.Empty,
                    ImmutableArray<BoneWeight>.Empty,
                    ImmutableArray<int>.Empty
                );
            }

            var hasBoneWeights = !outerBoneWeights.IsDefault && outerBoneWeights.Length == m;

            // 頂点レイアウト: [outer(M), centroid(1)] (Subdivisions=0 の Hermite と同型)
            var totalVertexCount = m + 1;
            var positions = ImmutableArray.CreateBuilder<Vector3>(totalVertexCount);
            var normals = ImmutableArray.CreateBuilder<Vector3>(totalVertexCount);
            var boneWeights = ImmutableArray.CreateBuilder<BoneWeight>(totalVertexCount);

            for (var i = 0; i < m; ++i)
            {
                positions.Add(outerPositions[i]);
                normals.Add(outerNormals[i]);
                boneWeights.Add(hasBoneWeights ? outerBoneWeights[i] : default);
            }
            positions.Add(input.CentroidPosition);
            normals.Add(input.CentroidNormal);
            boneWeights.Add(input.CentroidBoneWeight);

            // 三角形: 重心 - outer[i] - outer[i+1] の fan
            var triangles = ImmutableArray.CreateBuilder<int>(m * 3);
            var centroidIndex = m;
            for (var i = 0; i < m; ++i)
            {
                var iNext = (i + 1) % m;
                triangles.Add(centroidIndex);
                triangles.Add(i);
                triangles.Add(iNext);
            }

            return new DomeMeshResult(
                positions.MoveToImmutable(),
                normals.MoveToImmutable(),
                boneWeights.MoveToImmutable(),
                triangles.MoveToImmutable()
            );
        }
    }
}
