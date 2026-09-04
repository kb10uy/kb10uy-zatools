using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using KusakaFactory.Zatools.Foundation;
using KusakaFactory.Zatools.Runtime;
using nadena.dev.ndmf;

namespace KusakaFactory.Zatools.Ndmf.Core
{
    internal static class Ahms
    {
        /// <summary>
        /// メイン処理
        /// </summary>
        /// <param name="referencingRenderer">Mesh の参照元の SkinnedMeshRenderer</param>
        /// <param name="modifyingMesh">対象の Mesh (直接変更される)</param>
        /// <param name="parameters">固定されたパラメーター</param>
        internal static void Process(SkinnedMeshRenderer referencingRenderer, Mesh modifyingMesh, FixedParameters parameters)
        {
            var maskMode = parameters.MaskMode switch
            {
                MeshSplitMaskMode.White => NativeTextureSampler.MaskMode.TakeWhite,
                MeshSplitMaskMode.Black => NativeTextureSampler.MaskMode.TakeBlack,
                _ => throw new InvalidOperationException("unknown mode"),
            };

            var vertexCount = modifyingMesh.vertexCount;
            var uvs = new List<Vector2>(vertexCount);
            modifyingMesh.GetUVs(0, uvs);
            while (uvs.Count < vertexCount) uvs.Add(Vector2.zero);

            var vertexSelections = new bool[vertexCount];
            var nativeMaskUvs = new NativeArray<float4>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var nativeMaskValues = new NativeArray<float>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            try
            {
                for (var i = 0; i < vertexCount; ++i) nativeMaskUvs[i] = new float4(uvs[i].x, uvs[i].y, 0.0f, 0.0f);
                NativeTextureSampler.SampleMaskByComputeShader(parameters.MaskTexture, maskMode, ref nativeMaskUvs, ref nativeMaskValues);
                for (var i = 0; i < vertexCount; ++i) vertexSelections[i] = nativeMaskValues[i] >= 0.5f;
            }
            finally
            {
                nativeMaskUvs.Dispose();
                nativeMaskValues.Dispose();
            }

            var originalSubMeshCount = modifyingMesh.subMeshCount;
            var originalMaterials = referencingRenderer.sharedMaterials;
            var subMeshes = new List<List<int>>();
            var materials = new List<Material>();
            var splitIndices = new List<int>();
            for (var sm = 0; sm < originalSubMeshCount; ++sm)
            {
                var originalMaterial = sm < originalMaterials.Length ? originalMaterials[sm] : null;
                materials.Add(originalMaterial);

                var originalIndices = new List<int>();
                modifyingMesh.GetTriangles(originalIndices, sm);
                if (parameters.FilteringMaterial == null || parameters.FilteringMaterial == originalMaterial)
                {
                    var remainingIndices = new List<int>();
                    for (var i = 0; i + 2 < originalIndices.Count; i += 3)
                    {
                        var i0 = originalIndices[i];
                        var i1 = originalIndices[i + 1];
                        var i2 = originalIndices[i + 2];

                        if (vertexSelections[i0] && vertexSelections[i1] && vertexSelections[i2])
                        {
                            splitIndices.Add(i0);
                            splitIndices.Add(i1);
                            splitIndices.Add(i2);
                        }
                        else
                        {
                            remainingIndices.Add(i0);
                            remainingIndices.Add(i1);
                            remainingIndices.Add(i2);
                        }
                    }
                    subMeshes.Add(remainingIndices);
                }
                else
                {
                    subMeshes.Add(originalIndices);
                }
            }
            subMeshes.Add(splitIndices);
            materials.Add(parameters.SplitMaterial);

            modifyingMesh.subMeshCount = subMeshes.Count;
            for (var sm = 0; sm < subMeshes.Count; ++sm) modifyingMesh.SetTriangles(subMeshes[sm], sm);
            referencingRenderer.sharedMaterials = materials.ToArray();
        }

        internal struct FixedParameters : IEquatable<FixedParameters>
        {
            internal Texture2D MaskTexture;
            internal MeshSplitMaskMode MaskMode;
            internal Material FilteringMaterial;
            internal Material SplitMaterial;

            internal static FixedParameters FixFromComponent(AdHocMeshSplit component)
            {
                var filteringMaterialReference = component.FilteringMaterial != null ?
                    ObjectRegistry.GetReference(component.FilteringMaterial) :
                    null;
                return new FixedParameters()
                {
                    MaskTexture = component.Mask,
                    MaskMode = component.Mode,
                    FilteringMaterial = filteringMaterialReference?.Object as Material,
                    SplitMaterial = component.SplitMaterial,
                };
            }

            public bool Equals(FixedParameters other)
            {
                return MaskTexture == other.MaskTexture
                    && MaskMode == other.MaskMode
                    && FilteringMaterial == other.FilteringMaterial
                    && SplitMaterial == other.SplitMaterial;
            }

            public override bool Equals(object obj) => obj is FixedParameters && Equals((FixedParameters)obj);

            public override int GetHashCode() => (MaskTexture, MaskMode, FilteringMaterial, SplitMaterial).GetHashCode();

            public static bool operator ==(FixedParameters lhs, FixedParameters rhs) => lhs.Equals(rhs);

            public static bool operator !=(FixedParameters lhs, FixedParameters rhs) => !(lhs == rhs);
        }
    }
}
