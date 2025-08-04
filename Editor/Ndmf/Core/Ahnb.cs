using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KusakaFactory.Zatools.Foundation;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Ndmf.Core
{
    internal static class Ahnb
    {
        /// <summary>
        /// メイン処理
        /// </summary>
        /// <param name="referencingRenderer">Mesh の参照元の SkinnedMeshRenderer</param>
        /// <param name="modifyingMesh">対象の Mesh</param>
        /// <param name="parameters">固定されたパラメーター</param>
        /// <exception cref="InvalidOperationException"></exception>
        internal static void Process(SkinnedMeshRenderer referencingRenderer, Mesh modifyingMesh, FixedParameters parameters)
        {
            // TODO: BoneWeight1 を使う
            var normals = new List<Vector3>(modifyingMesh.vertexCount);
            var uvs = new List<Vector2>(modifyingMesh.vertexCount);
            var boneWeights = new List<BoneWeight>(modifyingMesh.vertexCount);
            var bindposes = new List<Matrix4x4>(modifyingMesh.bindposeCount);
            modifyingMesh.GetNormals(normals);
            modifyingMesh.GetUVs(0, uvs);
            modifyingMesh.GetBoneWeights(boneWeights);
            modifyingMesh.GetBindposes(bindposes);
            var bones = referencingRenderer.bones;

            var mask = new TextureMask(parameters.MaskTexture, parameters.MaskMode switch
            {
                NormalBendMaskMode.White => TextureMask.Mode.TakeWhite,
                NormalBendMaskMode.Black => TextureMask.Mode.TakeBlack,
                _ => throw new InvalidOperationException("unknown mode"),
            });

            // 現在の各ボーンの変換行列
            var currentBoneDeforms = Enumerable.Range(0, bones.Length)
                .Select((i) => bones[i] != null ? bones[i].localToWorldMatrix * bindposes[i] : Matrix4x4.identity)
                .ToList();

            for (var i = 0; i < normals.Count; ++i)
            {
                if (!mask.Take(uvs[i].x, uvs[i].y)) continue;

                // ボーン変形を受ける行列を計算
                // BlendShape は線形にしか移動しないのでこの場合は無視してよいものとする
                var boneWeight = boneWeights[i];
                var inverseMatrix = ExtraMath.BlendMatrices(
                    currentBoneDeforms,
                    (boneWeight.boneIndex0, boneWeight.weight0),
                    (boneWeight.boneIndex1, boneWeight.weight1),
                    (boneWeight.boneIndex2, boneWeight.weight2),
                    (boneWeight.boneIndex3, boneWeight.weight3)
                ).inverse;

                var originalNormal = Quaternion.LookRotation(normals[i]);
                var directedNormal = Quaternion.LookRotation(inverseMatrix.MultiplyVector(parameters.WorldSpaceForward));
                var bentNormal = Quaternion.Lerp(originalNormal, directedNormal, parameters.Weight);
                normals[i] = bentNormal * Vector3.forward;
            }

            modifyingMesh.SetNormals(normals);
        }

        internal struct FixedParameters : IEquatable<FixedParameters>
        {
            internal Vector3 WorldSpaceForward;
            internal float Weight;
            internal Texture2D MaskTexture;
            internal bool CanReadMask;
            internal NormalBendMaskMode MaskMode;

            internal static FixedParameters FixFromComponent(AdHocNormalBending component)
            {
                var directionSource = component.Direction != null ? component.Direction : component.transform;
                return new FixedParameters()
                {
                    WorldSpaceForward = directionSource.forward,
                    Weight = component.Weight,
                    MaskTexture = component.Mask,
                    MaskMode = component.Mode,
                    CanReadMask = component.Mask != null && component.Mask.isReadable,
                };
            }

            public bool Equals(FixedParameters other)
            {
                return (WorldSpaceForward - other.WorldSpaceForward).sqrMagnitude < 0.0001f
                    && Mathf.Approximately(Weight, other.Weight)
                    && MaskTexture == other.MaskTexture
                    && CanReadMask == other.CanReadMask
                    && MaskMode == other.MaskMode;
            }

            public override bool Equals(object obj) => obj is FixedParameters && Equals((FixedParameters)obj);

            public override int GetHashCode() => (WorldSpaceForward, MaskTexture, MaskMode).GetHashCode();

            public static bool operator ==(FixedParameters lhs, FixedParameters rhs) => lhs.Equals(rhs);

            public static bool operator !=(FixedParameters lhs, FixedParameters rhs) => !(lhs == rhs);
        }
    }
}
