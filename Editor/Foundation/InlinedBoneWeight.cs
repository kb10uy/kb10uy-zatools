using System.Runtime.InteropServices;
using UnityEngine;
using Unity.Mathematics;

namespace KusakaFactory.Zatools.Foundation
{
    /// <summary>
    /// Stores the indices and weights of a four-bone influence in vector form.
    /// </summary>
    /// <remarks>
    /// This type is public only so that it can be used by external in-house assemblies.
    /// It is not a stable public API and may change or be removed without notice between releases.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    public struct InlinedBoneWeight
    {
        public int4 Indices;
        public float4 Weights;

        public static InlinedBoneWeight FromBoneWeight(BoneWeight weight)
        {
            return new InlinedBoneWeight
            {
                Indices = new int4(weight.boneIndex0, weight.boneIndex1, weight.boneIndex2, weight.boneIndex3),
                Weights = new float4(weight.weight0, weight.weight1, weight.weight2, weight.weight3),
            };
        }
    }
}
