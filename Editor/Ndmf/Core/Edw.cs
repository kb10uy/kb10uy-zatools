using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using KusakaFactory.Zatools.Foundation;
using KusakaFactory.Zatools.Foundation.Mesh;
using KusakaFactory.Zatools.Runtime;
using UnityObject = UnityEngine.Object;
using Mesh = UnityEngine.Mesh;

namespace KusakaFactory.Zatools.Ndmf.Core
{
    internal static class Edw
    {
        /// <summary>
        /// メイン処理
        /// </summary>
        /// <param name="referenceRenderer">Mesh の参照元の SkinnedMeshRenderer</param>
        /// <param name="modifyingMesh">対象の Mesh</param>
        /// <param name="parameters">固定されたパラメーター</param>
        /// <returns>このマスクによって影響を受ける頂点にウェイトがかかっているボーンのインデックス(順不同)</returns>
        /// <exception cref="ArgumentException">頂点数が一致しない場合</exception>
        internal static void Process(SkinnedMeshRenderer referencingRenderer, Mesh modifyingMesh, FixedParameters parameters, Material wrapperMaterial)
        {
            if (referencingRenderer.sharedMesh.vertexCount != modifyingMesh.vertexCount) throw new ArgumentException("different mesh vertex count");

            var blendShapeIndex = modifyingMesh.GetBlendShapeIndex(parameters.BlinkBlendShapeName);
            if (blendShapeIndex == -1 || parameters.Threshold < 0.0f || parameters.WithdrawalLimit < 0.0f) return;

            var vertices = modifyingMesh.vertices;
            var normals = modifyingMesh.normals;
            var tangents = modifyingMesh.tangents;
            var uvs = modifyingMesh.uv;
            var boneWeights = modifyingMesh.boneWeights;
            var vertexCount = modifyingMesh.vertexCount;
            var validTangents = tangents.Length == vertexCount;
            var hasBoneWeights = boneWeights != null;
            var deltaVertices = new Vector3[vertexCount];
            var _deltaNormals = new Vector3[vertexCount];
            var _deltaTangents = new Vector3[vertexCount];
            modifyingMesh.GetBlendShapeFrameVertices(
                blendShapeIndex,
                modifyingMesh.GetBlendShapeFrameCount(blendShapeIndex) - 1,
                deltaVertices,
                _deltaNormals,
                _deltaTangents
            );

            var smrFromBasis = referencingRenderer.transform.worldToLocalMatrix * parameters.Basis.localToWorldMatrix;
            var basisFromSmr = smrFromBasis.inverse;

            var smrRelativeDeformedMesh = new Mesh();
            referencingRenderer.BakeMesh(smrRelativeDeformedMesh);
            var smrRelativeVertices = new List<Vector3>(smrRelativeDeformedMesh.vertexCount);
            smrRelativeDeformedMesh.GetVertices(smrRelativeVertices);
            UnityObject.DestroyImmediate(smrRelativeDeformedMesh);
            var bakedVerticesInSmr = smrRelativeVertices.ToImmutableArray();
            var bakedVerticesInBasis = smrRelativeVertices.Select(v => basisFromSmr.MultiplyPoint(v)).ToImmutableArray();

            // 外周隣接の面法線計算のため、全 submesh の三角形を結合した配列を構築する。
            var allTrianglesList = new List<int>();
            for (var sm = 0; sm < modifyingMesh.subMeshCount; ++sm) allTrianglesList.AddRange(modifyingMesh.GetTriangles(sm));
            var allTriangles = allTrianglesList.ToArray();

            var blinkMovingIndices = deltaVertices
                .Select((d, i) => (Delta: basisFromSmr.MultiplyVector(d), Index: i))
                .Where(t => t.Delta.sqrMagnitude > Mathf.Pow(parameters.Threshold, 2.0f))
                .Select(t => t.Index)
                .ToHashSet();
            if (blinkMovingIndices.Count == 0) return;
            var blinkMaxZ = blinkMovingIndices.Select((i) => bakedVerticesInBasis[i]).Max((v) => v.z) - parameters.EyelashCut;

            // Basis forward 向きの面に属する頂点だけを採用するためのフィルタ集合を構築する。
            // 裏向き(まつ毛裏など)の頂点は hull に含めない。
            var centroidNormalSmr = smrFromBasis.MultiplyVector(Vector3.forward).normalized;
            var frontFacingVertexIndices = ComputeFrontFacingVertexSet(allTriangles, bakedVerticesInSmr, centroidNormalSmr);

            var blinkHullFilteredIndices = blinkMovingIndices.Where((i) =>
            {
                if (!frontFacingVertexIndices.Contains(i)) return false;
                var deltaZ = blinkMaxZ - bakedVerticesInBasis[i].z;
                return deltaZ > 0.0f && deltaZ < parameters.WithdrawalLimit;
            }).ToImmutableArray();
            var (leftConvexHull, rightConvexHull) = ComputeConvexHulls(bakedVerticesInBasis, blinkHullFilteredIndices);

            var centroidPushVector = smrFromBasis.MultiplyVector(Vector3.forward) * parameters.CentroidPush;
            IDomeMeshGenerator generator = parameters.GeneratorKind switch
            {
                DomeGeneratorKind.Fan => new FanDomeMeshGenerator(),
                DomeGeneratorKind.Hermite => new HermiteDomeMeshGenerator(),
                _ => new HermiteDomeMeshGenerator(),
            };
            var context = new DomeBuildContext(
                vertices,
                normals,
                boneWeights,
                hasBoneWeights,
                allTriangles,
                bakedVerticesInSmr,
                centroidNormalSmr,
                centroidPushVector,
                parameters.Subdivisions,
                parameters.TangentScale,
                vertexCount,
                generator
            );
            var accumulator = new DomeAccumulator();
            AppendDome(leftConvexHull, context, accumulator);
            AppendDome(rightConvexHull, context, accumulator);

            Array.Resize(ref vertices, vertexCount + accumulator.Vertices.Count);
            Array.Resize(ref normals, vertexCount + accumulator.Vertices.Count);
            if (validTangents) Array.Resize(ref tangents, vertexCount + accumulator.Vertices.Count);
            Array.Resize(ref uvs, vertexCount + accumulator.Vertices.Count);
            if (hasBoneWeights) Array.Resize(ref boneWeights, vertexCount + accumulator.Vertices.Count);
            for (var i = 0; i < accumulator.Vertices.Count; ++i)
            {
                vertices[vertexCount + i] = accumulator.Vertices[i];
                normals[vertexCount + i] = accumulator.Normals[i];
                // ShadowCaster専用のため tangent / uv は don't care: ゼロ埋め
                if (validTangents) tangents[vertexCount + i] = Vector4.zero;
                uvs[vertexCount + i] = Vector2.zero;
                if (hasBoneWeights) boneWeights[vertexCount + i] = accumulator.BoneWeights[i];
            }

            modifyingMesh.vertices = vertices;
            modifyingMesh.normals = normals;
            if (validTangents) modifyingMesh.tangents = tangents;
            modifyingMesh.uv = uvs;
            if (hasBoneWeights) modifyingMesh.boneWeights = boneWeights;

            var originalSubMeshCount = modifyingMesh.subMeshCount;
            var savedTriangles = new int[originalSubMeshCount][];
            for (int i = 0; i < originalSubMeshCount; i++) savedTriangles[i] = modifyingMesh.GetTriangles(i);
            modifyingMesh.subMeshCount = originalSubMeshCount + 1;
            for (int i = 0; i < originalSubMeshCount; i++) modifyingMesh.SetTriangles(savedTriangles[i], i);
            modifyingMesh.SetTriangles(accumulator.Triangles, originalSubMeshCount);
            modifyingMesh.RecalculateBounds();

            var originalMaterials = referencingRenderer.sharedMaterials;
            var newMaterials = new Material[originalMaterials.Length + 1];
            originalMaterials.CopyTo(newMaterials, 0);
            newMaterials[originalMaterials.Length] = wrapperMaterial;
            referencingRenderer.sharedMaterials = newMaterials;
        }

        /// <summary>
        /// 与えられた convex hull に対して指定された IDomeMeshGenerator でドーム状メッシュを生成し、
        /// アキュムレータにジオメトリを追記する。
        /// </summary>
        private static void AppendDome(ImmutableArray<int> hull, DomeBuildContext context, DomeAccumulator accumulator)
        {
            if (hull.Length < 3) return;

            var outerPositions = hull.Select(i => context.Vertices[i]).ToImmutableArray();
            var outerNormals = ComputeFrontFacingNormals(
                hull,
                context.AllTriangles,
                context.BakedVerticesInSmr,
                context.CentroidNormalSmr,
                context.Normals
            );
            var outerBoneWeights = context.HasBoneWeights
                ? hull.Select(i => context.BoneWeights[i]).ToImmutableArray()
                : ImmutableArray<BoneWeight>.Empty;

            var centroidPosition = outerPositions.Aggregate(Vector3.zero, (s, p) => s + p) / outerPositions.Length;
            centroidPosition += context.CentroidPushVector;
            var centroidBoneWeight = context.HasBoneWeights ? context.BoneWeights[hull[0]] : default;

            var input = new DomeMeshInput(
                outerPositions,
                outerNormals,
                outerBoneWeights,
                centroidPosition,
                context.CentroidNormalSmr,
                centroidBoneWeight,
                context.Subdivisions,
                context.TangentScale
            );
            var result = context.Generator.Generate(input);
            if (result.Positions.Length == 0) return;

            var indexOffset = context.BaseVertexCount + accumulator.Vertices.Count;
            for (var i = 0; i < result.Positions.Length; ++i)
            {
                accumulator.Vertices.Add(result.Positions[i]);
                accumulator.Normals.Add(result.Normals[i]);
                if (context.HasBoneWeights) accumulator.BoneWeights.Add(result.BoneWeights[i]);
            }
            foreach (var triangleIndex in result.Triangles) accumulator.Triangles.Add(triangleIndex + indexOffset);
        }

        /// <summary>
        /// Basis forward 方向を向いた隣接三角形を 1 つ以上持つ頂点のインデックス集合を返す。
        /// 裏向き(まつ毛裏など)の頂点を hull 計算から除外するために使用する。
        /// </summary>
        /// <param name="allTriangles">メッシュ全 submesh を結合した三角形インデックス配列。</param>
        /// <param name="bakedVerticesInSmr">ベイク済み変形メッシュの頂点位置(SMR空間)。</param>
        /// <param name="basisForwardInSmr">Basis の forward 方向を SMR 空間に変換したベクトル。</param>
        private static HashSet<int> ComputeFrontFacingVertexSet(
            int[] allTriangles,
            ImmutableArray<Vector3> bakedVerticesInSmr,
            Vector3 basisForwardInSmr
        )
        {
            var frontFacingSet = new HashSet<int>();
            for (var t = 0; t < allTriangles.Length; t += 3)
            {
                var a = allTriangles[t];
                var b = allTriangles[t + 1];
                var c = allTriangles[t + 2];
                var pa = bakedVerticesInSmr[a];
                var pb = bakedVerticesInSmr[b];
                var pc = bakedVerticesInSmr[c];
                var crossVec = Vector3.Cross(pb - pa, pc - pa);
                var area = crossVec.magnitude;
                if (area < 1e-10f) continue;
                var faceNormal = crossVec / area;
                if (Vector3.Dot(faceNormal, basisForwardInSmr) <= 0.0f) continue;

                frontFacingSet.Add(a);
                frontFacingSet.Add(b);
                frontFacingSet.Add(c);
            }
            return frontFacingSet;
        }

        /// <summary>
        /// 外周頂点ごとに Basis forward 方向を向いた隣接三角形のみを採用して面法線の面積重み付き平均を計算する。
        /// 該当する三角形が見つからなかった頂点については fallbackNormals を採用する。
        /// </summary>
        /// <param name="hull">外周頂点インデックス列。</param>
        /// <param name="allTriangles">メッシュ全 submesh を結合した三角形インデックス配列(3個ずつで1三角形)。</param>
        /// <param name="bakedVerticesInSmr">ベイク済み変形メッシュの頂点位置(SMR空間)。</param>
        /// <param name="basisForwardInSmr">Basis の forward 方向を SMR 空間に変換したベクトル。</param>
        /// <param name="fallbackNormals">前向き隣接面が見つからなかった頂点のためのフォールバック法線配列(メッシュ頂点法線)。</param>
        private static ImmutableArray<Vector3> ComputeFrontFacingNormals(
            ImmutableArray<int> hull,
            int[] allTriangles,
            ImmutableArray<Vector3> bakedVerticesInSmr,
            Vector3 basisForwardInSmr,
            Vector3[] fallbackNormals
        )
        {
            var hullIndexLookup = new Dictionary<int, int>(hull.Length);
            for (var i = 0; i < hull.Length; ++i) hullIndexLookup[hull[i]] = i;

            var accumulators = new Vector3[hull.Length];
            for (var t = 0; t < allTriangles.Length; t += 3)
            {
                var a = allTriangles[t];
                var b = allTriangles[t + 1];
                var c = allTriangles[t + 2];

                var hasA = hullIndexLookup.TryGetValue(a, out var idxA);
                var hasB = hullIndexLookup.TryGetValue(b, out var idxB);
                var hasC = hullIndexLookup.TryGetValue(c, out var idxC);
                if (!hasA && !hasB && !hasC) continue;

                var pa = bakedVerticesInSmr[a];
                var pb = bakedVerticesInSmr[b];
                var pc = bakedVerticesInSmr[c];
                var crossVec = Vector3.Cross(pb - pa, pc - pa); // 長さ = 面積 × 2
                var area = crossVec.magnitude;
                if (area < 1e-10f) continue;
                var faceNormal = crossVec / area;

                // Basis forward 向きの面のみ採用(まつ毛側の内向き面は dot < 0 で自然に除外)
                if (Vector3.Dot(faceNormal, basisForwardInSmr) <= 0.0f) continue;

                // crossVec をそのまま積算することで面積重み付き平均になる
                if (hasA) accumulators[idxA] += crossVec;
                if (hasB) accumulators[idxB] += crossVec;
                if (hasC) accumulators[idxC] += crossVec;
            }

            var result = ImmutableArray.CreateBuilder<Vector3>(hull.Length);
            for (var i = 0; i < hull.Length; ++i)
            {
                var acc = accumulators[i];
                result.Add(acc.sqrMagnitude < 1e-12f ? fallbackNormals[hull[i]] : acc.normalized);
            }
            return result.MoveToImmutable();
        }

        private static (ImmutableArray<int> Left, ImmutableArray<int> Right) ComputeConvexHulls(ImmutableArray<Vector3> vertices, ImmutableArray<int> movingIndices)
        {
            var leftMapping = new List<int>(movingIndices.Length / 2);
            var leftPoints = new List<Vector2>(movingIndices.Length / 2);
            var rightMapping = new List<int>(movingIndices.Length / 2);
            var rightPoints = new List<Vector2>(movingIndices.Length / 2);

            foreach (int i in movingIndices)
            {
                if (vertices[i].x < 0f)
                {
                    leftMapping.Add(i);
                    leftPoints.Add(vertices[i]);
                }
                else
                {
                    rightMapping.Add(i);
                    rightPoints.Add(vertices[i]);
                }
            }

            var leftHullIndices = ConvexHull.ComputeAndrews(leftPoints);
            var rightHullIndices = ConvexHull.ComputeAndrews(rightPoints);
            var leftHullVertexIndices = leftHullIndices.Select(li => leftMapping[li]).ToImmutableArray();
            var rightHullVertexIndices = rightHullIndices.Select(ri => rightMapping[ri]).ToImmutableArray();
            return (leftHullVertexIndices, rightHullVertexIndices);
        }

        /// <summary>
        /// AppendDome へ渡すドーム生成コンテキスト。左右の hull で共通する入力をまとめる。
        /// </summary>
        private readonly struct DomeBuildContext
        {
            public Vector3[] Vertices { get; }
            public Vector3[] Normals { get; }
            public BoneWeight[] BoneWeights { get; }
            public bool HasBoneWeights { get; }
            public int[] AllTriangles { get; }
            public ImmutableArray<Vector3> BakedVerticesInSmr { get; }
            public Vector3 CentroidNormalSmr { get; }
            public Vector3 CentroidPushVector { get; }
            public int Subdivisions { get; }
            public float TangentScale { get; }
            public int BaseVertexCount { get; }
            public IDomeMeshGenerator Generator { get; }

            public DomeBuildContext(
                Vector3[] vertices,
                Vector3[] normals,
                BoneWeight[] boneWeights,
                bool hasBoneWeights,
                int[] allTriangles,
                ImmutableArray<Vector3> bakedVerticesInSmr,
                Vector3 centroidNormalSmr,
                Vector3 centroidPushVector,
                int subdivisions,
                float tangentScale,
                int baseVertexCount,
                IDomeMeshGenerator generator
            )
            {
                Vertices = vertices;
                Normals = normals;
                BoneWeights = boneWeights;
                HasBoneWeights = hasBoneWeights;
                AllTriangles = allTriangles;
                BakedVerticesInSmr = bakedVerticesInSmr;
                CentroidNormalSmr = centroidNormalSmr;
                CentroidPushVector = centroidPushVector;
                Subdivisions = subdivisions;
                TangentScale = tangentScale;
                BaseVertexCount = baseVertexCount;
                Generator = generator;
            }
        }

        /// <summary>
        /// AppendDome から追記される新規ジオメトリの蓄積先。
        /// </summary>
        private sealed class DomeAccumulator
        {
            public List<Vector3> Vertices { get; } = new List<Vector3>();
            public List<Vector3> Normals { get; } = new List<Vector3>();
            public List<BoneWeight> BoneWeights { get; } = new List<BoneWeight>();
            public List<int> Triangles { get; } = new List<int>();
        }

        internal struct FixedParameters : IEquatable<FixedParameters>
        {
            internal string BlinkBlendShapeName;
            internal float Threshold;
            internal float EyelashCut;
            internal float WithdrawalLimit;
            internal Transform Basis;
            internal float CentroidPush;
            internal int Subdivisions;
            internal float TangentScale;
            internal DomeGeneratorKind GeneratorKind;

            internal static FixedParameters FixFromComponent(Transform defaultBasis, EyeholeDepthWrapper component)
            {
                var basisSource = component.Basis != null ? component.Basis : defaultBasis;
                return new FixedParameters()
                {
                    BlinkBlendShapeName = component.BlinkBlendShapeName,
                    Threshold = component.Threshold,
                    EyelashCut = component.EyelashCut,
                    WithdrawalLimit = component.WithdrawalLimit,
                    Basis = basisSource,
                    CentroidPush = component.CentroidPush,
                    Subdivisions = Mathf.Max(1, component.Subdivisions),
                    TangentScale = component.TangentScale,
                    GeneratorKind = component.GeneratorKind,
                };
            }

            public bool Equals(FixedParameters other)
            {
                return Basis.worldToLocalMatrix == other.Basis.worldToLocalMatrix &&
                    BlinkBlendShapeName == other.BlinkBlendShapeName &&
                    Mathf.Approximately(Threshold, other.Threshold) &&
                    Mathf.Approximately(EyelashCut, other.EyelashCut) &&
                    Mathf.Approximately(WithdrawalLimit, other.WithdrawalLimit) &&
                    Mathf.Approximately(CentroidPush, other.CentroidPush) &&
                    Subdivisions == other.Subdivisions &&
                    Mathf.Approximately(TangentScale, other.TangentScale) &&
                    GeneratorKind == other.GeneratorKind;
            }

            public override bool Equals(object obj) => obj is FixedParameters && Equals((FixedParameters)obj);

            public override int GetHashCode() => (Basis, BlinkBlendShapeName).GetHashCode();

            public static bool operator ==(FixedParameters lhs, FixedParameters rhs) => lhs.Equals(rhs);

            public static bool operator !=(FixedParameters lhs, FixedParameters rhs) => !(lhs == rhs);
        }
    }
}
