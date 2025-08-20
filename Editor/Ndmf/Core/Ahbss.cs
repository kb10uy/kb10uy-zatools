using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using KusakaFactory.Zatools.Runtime;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace KusakaFactory.Zatools.Ndmf.Core
{
    internal static class Ahbss
    {
        internal static void AddSplitShapes(SkinnedMeshRenderer referenceRenderer, Mesh modifyingMesh, FixedParameters parameters)
        {
            if (referenceRenderer.sharedMesh.vertexCount != modifyingMesh.vertexCount) throw new ArgumentException("different mesh vertex count");

            // 現在の変形状態を固定して左右判定をする
            // BlendShape = 0 状態を取ったほうがいい気もするがまあ速そうだし……
            var smrRelativeDeformedMesh = new Mesh();
            var smrRelativeVertices = new List<Vector3>(smrRelativeDeformedMesh.vertexCount);
            referenceRenderer.BakeMesh(smrRelativeDeformedMesh);
            smrRelativeDeformedMesh.GetVertices(smrRelativeVertices);
            UnityObject.DestroyImmediate(smrRelativeDeformedMesh);

            var vertexCount = modifyingMesh.vertexCount;
            var originalVertices = new Vector3[vertexCount];
            var originalNormals = new Vector3[vertexCount];
            var originalTangents = new Vector3[vertexCount];
            var originalWeight = 0.0f;
            var leftVertices = new Vector3[vertexCount];
            var leftNormals = new Vector3[vertexCount];
            var leftTangents = new Vector3[vertexCount];
            var rightVertices = new Vector3[vertexCount];
            var rightNormals = new Vector3[vertexCount];
            var rightTangents = new Vector3[vertexCount];

            var nameToIndex = Enumerable.Range(0, modifyingMesh.blendShapeCount).ToDictionary((i) => modifyingMesh.GetBlendShapeName(i));
            foreach (var targetShape in parameters.TargetShapes)
            {
                if (!nameToIndex.TryGetValue(targetShape, out var shapeIndex)) continue;
                if (modifyingMesh.GetBlendShapeFrameCount(shapeIndex) != 1) continue;


                modifyingMesh.GetBlendShapeFrameVertices(shapeIndex, 0, originalVertices, originalNormals, originalTangents);
                originalWeight = modifyingMesh.GetBlendShapeFrameWeight(shapeIndex, 0);

                for (var i = 0; i < vertexCount; ++i)
                {
                    // left-handed, forwarding
                    var deformedVertex = smrRelativeVertices[i];
                    var isRightSide = deformedVertex.x >= 0;
                    leftVertices[i] = !isRightSide ? originalVertices[i] : Vector3.zero;
                    leftNormals[i] = !isRightSide ? originalNormals[i] : Vector3.zero;
                    leftTangents[i] = !isRightSide ? originalTangents[i] : Vector3.zero;
                    rightVertices[i] = isRightSide ? originalVertices[i] : Vector3.zero;
                    rightNormals[i] = isRightSide ? originalNormals[i] : Vector3.zero;
                    rightTangents[i] = isRightSide ? originalTangents[i] : Vector3.zero;
                }
                modifyingMesh.AddBlendShapeFrame($"{targetShape}_sL", originalWeight, leftVertices, leftNormals, leftTangents);
                modifyingMesh.AddBlendShapeFrame($"{targetShape}_sR", originalWeight, rightVertices, rightNormals, rightTangents);
            }
        }

        internal struct FixedParameters : IEquatable<FixedParameters>
        {
            internal Matrix4x4 Basis;
            internal ImmutableArray<string> TargetShapes;

            internal static FixedParameters FixFromComponent(Transform defaultDirection, AdHocBlendShapeSplit component)
            {
                var basisSource = component.Basis != null ? component.Basis : defaultDirection;
                return new FixedParameters()
                {
                    // TODO: 正しい basis にする
                    Basis = basisSource.localToWorldMatrix,
                    TargetShapes = component.TargetShapes.ToImmutableArray(),
                };
            }

            public bool Equals(FixedParameters other)
            {
                return Basis == other.Basis && TargetShapes.SequenceEqual(other.TargetShapes);
            }

            public override bool Equals(object obj) => obj is FixedParameters && Equals((FixedParameters)obj);

            public override int GetHashCode() => (Basis, TargetShapes).GetHashCode();

            public static bool operator ==(FixedParameters lhs, FixedParameters rhs) => lhs.Equals(rhs);

            public static bool operator !=(FixedParameters lhs, FixedParameters rhs) => !(lhs == rhs);
        }
    }
}
