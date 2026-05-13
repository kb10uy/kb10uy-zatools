using System.Collections.Immutable;
using UnityEngine;

namespace KusakaFactory.Zatools.Foundation.Mesh
{
    internal sealed class HermiteDomeMeshGenerator : IDomeMeshGenerator
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

            var n = Mathf.Max(0, input.Subdivisions);
            var tangentScale = input.TangentScale;
            var centroidPosition = input.CentroidPosition;
            var centroidNormal = input.CentroidNormal;
            var hasBoneWeights = !outerBoneWeights.IsDefault && outerBoneWeights.Length == m;

            var totalVertexCount = m * (n + 1) + 1;
            var positions = ImmutableArray.CreateBuilder<Vector3>(totalVertexCount);
            var normals = ImmutableArray.CreateBuilder<Vector3>(totalVertexCount);
            var boneWeights = ImmutableArray.CreateBuilder<BoneWeight>(totalVertexCount);

            // 頂点法線とループ接線の外積で外周ループに垂直な接線方向を得る
            var outerHermiteTangents = new Vector3[m];
            for (var i = 0; i < m; ++i)
            {
                var pPrev = outerPositions[(i - 1 + m) % m];
                var pNext = outerPositions[(i + 1) % m];
                var loopTangent = (pNext - pPrev).normalized;
                var n0 = outerNormals[i];
                outerHermiteTangents[i] = Vector3.Cross(n0, loopTangent).normalized;
            }

            // 重心側の Hermite 接線ベクトルを各外周頂点ごとに計算する
            var centroidHermiteTangents = new Vector3[m];
            for (var i = 0; i < m; ++i)
            {
                var toOuter = outerPositions[i] - centroidPosition;
                centroidHermiteTangents[i] = Vector3.ProjectOnPlane(toOuter, centroidNormal).normalized;
            }

            // ring の分割をできるだけ均等にするため代表点として index 0 をサンプリングして均等になるようにする
            const int ArclengthSampleCount = 64;
            var reparameterizedTs = new float[n + 1]; // ring k(=0..N) の補正後 t 値
            {
                var representativeIndex = 0;
                var p0 = outerPositions[representativeIndex];
                var p1 = centroidPosition;
                var distance = Vector3.Distance(p0, p1);
                var t0 = outerHermiteTangents[representativeIndex] * (tangentScale * distance);
                var t1 = centroidHermiteTangents[representativeIndex] * (tangentScale * distance);

                // Hermite 曲線を細かくサンプリングして累積弧長を計算
                var cumulativeLengths = new float[ArclengthSampleCount + 1];
                var prevSample = p0;
                cumulativeLengths[0] = 0.0f;
                for (var s = 1; s <= ArclengthSampleCount; ++s)
                {
                    var ts = (float)s / ArclengthSampleCount;
                    var currentSample = Hermite(p0, p1, t0, t1, ts);
                    cumulativeLengths[s] = cumulativeLengths[s - 1] + Vector3.Distance(prevSample, currentSample);
                    prevSample = currentSample;
                }
                var totalLength = cumulativeLengths[ArclengthSampleCount];

                // ring k(=0..N) に対応する等弧長位置の t 値を線形補間で逆引きする
                for (var k = 0; k <= n; ++k)
                {
                    var targetLength = totalLength * k / (n + 1);
                    var s = 1;
                    while (s < ArclengthSampleCount && cumulativeLengths[s] < targetLength) ++s;
                    var l0 = cumulativeLengths[s - 1];
                    var l1 = cumulativeLengths[s];
                    var localT = l1 > l0 ? (targetLength - l0) / (l1 - l0) : 0.0f;
                    reparameterizedTs[k] = ((s - 1) + localT) / ArclengthSampleCount;
                }
            }

            // ring 0..N の頂点を生成 (重心は別途追加)
            for (var k = 0; k <= n; ++k)
            {
                var t = reparameterizedTs[k];
                for (var i = 0; i < m; ++i)
                {
                    var p0 = outerPositions[i];
                    var p1 = centroidPosition;
                    var distance = Vector3.Distance(p0, p1);
                    var t0 = outerHermiteTangents[i] * (tangentScale * distance);
                    var t1 = centroidHermiteTangents[i] * (tangentScale * distance);

                    positions.Add(Hermite(p0, p1, t0, t1, t));
                    normals.Add(Vector3.Slerp(outerNormals[i], centroidNormal, t));
                    boneWeights.Add(hasBoneWeights ? outerBoneWeights[i] : default);
                }
            }

            // 重心頂点
            positions.Add(centroidPosition);
            normals.Add(centroidNormal);
            boneWeights.Add(input.CentroidBoneWeight);

            // 三角形生成
            // ring k と ring k+1 の間に quad strip (2 三角形ずつ)
            // ring N と重心の間に fan
            var triangleCount = m * (2 * n + 1);
            var triangles = ImmutableArray.CreateBuilder<int>(triangleCount * 3);
            for (var k = 0; k < n; ++k)
            {
                for (var i = 0; i < m; ++i)
                {
                    var iNext = (i + 1) % m;
                    var a = RingIndex(k, i, m);
                    var b = RingIndex(k, iNext, m);
                    var c = RingIndex(k + 1, iNext, m);
                    var d = RingIndex(k + 1, i, m);
                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(d);
                }
            }
            var centroidIndex = (n + 1) * m;
            for (var i = 0; i < m; ++i)
            {
                var iNext = (i + 1) % m;
                triangles.Add(centroidIndex);
                triangles.Add(RingIndex(n, i, m));
                triangles.Add(RingIndex(n, iNext, m));
            }

            return new DomeMeshResult(
                positions.MoveToImmutable(),
                normals.MoveToImmutable(),
                boneWeights.MoveToImmutable(),
                triangles.ToImmutable()
            );
        }

        private static int RingIndex(int k, int i, int m) => k * m + i;

        private static Vector3 Hermite(Vector3 p0, Vector3 p1, Vector3 t0, Vector3 t1, float t)
        {
            var t2 = t * t;
            var t3 = t2 * t;
            var h00 = 2.0f * t3 - 3.0f * t2 + 1.0f;
            var h10 = t3 - 2.0f * t2 + t;
            var h01 = -2.0f * t3 + 3.0f * t2;
            var h11 = t3 - t2;
            return h00 * p0 + h10 * t0 + h01 * p1 + h11 * t1;
        }
    }
}
