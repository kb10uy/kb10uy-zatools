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
        /// テスト用処理
        /// </summary>
        /// <param name="modifyingMesh"></param>
        /// <param name="maskTexture">マスクテクスチャ。isReadable でなければならない</param>
        internal static void ProcessTest1(SkinnedMeshRenderer referencingRenderer, Mesh modifyingMesh, Texture2D maskTexture, NormalBendMaskMode mode)
        {
            // TODO: BoneWeight1 を使う
            var vertices = new List<Vector3>(modifyingMesh.vertexCount);
            var normals = new List<Vector3>(modifyingMesh.vertexCount);
            var uvs = new List<Vector2>(modifyingMesh.vertexCount);
            var boneWeights = new List<BoneWeight>(modifyingMesh.vertexCount);
            var bindposes = new List<Matrix4x4>(modifyingMesh.bindposeCount);
            modifyingMesh.GetVertices(vertices);
            modifyingMesh.GetNormals(normals);
            modifyingMesh.GetUVs(0, uvs);
            modifyingMesh.GetBoneWeights(boneWeights);
            modifyingMesh.GetBindposes(bindposes);
            var bones = referencingRenderer.bones;

            var mask = new TextureMask(maskTexture, mode switch
            {
                NormalBendMaskMode.TakeWhite => TextureMask.Mode.TakeWhite,
                NormalBendMaskMode.TakeBlack => TextureMask.Mode.TakeBlack,
                _ => throw new InvalidOperationException("unknown mode"),
            });

            // 現在の各ボーンの変換行列
            var currentBoneDeforms = Enumerable.Range(0, bones.Length)
                .Select((i) => bones[i] != null ? bones[i].localToWorldMatrix * bindposes[i] : Matrix4x4.identity)
                .ToList();

            var worldSpaceDirection = new Vector4(0.0f, 0.05f, 0.0f, 0.0f);

            for (var i = 0; i < vertices.Count; ++i)
            {
                if (!mask.Take(uvs[i].x, uvs[i].y)) continue;

                // ボーン変形を受ける行列を計算
                // BlendShape は線形にしか移動しないのでこの場合は無視してよいものとする
                var boneWeight = boneWeights[i];
                var matrix = ExtraMath.BlendMatrices(
                    currentBoneDeforms,
                    (boneWeight.boneIndex0, boneWeight.weight0),
                    (boneWeight.boneIndex1, boneWeight.weight1),
                    (boneWeight.boneIndex2, boneWeight.weight2),
                    (boneWeight.boneIndex3, boneWeight.weight3)
                );
                var inverseMatrix = matrix.inverse;
                var delta = inverseMatrix * worldSpaceDirection;

                vertices[i] += new Vector3(delta.x, delta.y, delta.z);
            }

            modifyingMesh.SetVertices(vertices);
        }
    }
}
