using System.Runtime.InteropServices;
using UnityEngine;
using Unity.Mathematics;

namespace KusakaFactory.Zatools.Foundation
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct InlinedBoneWeight
    {
        public int4 Indices;
        public float4 Weights;

        internal static InlinedBoneWeight FromBoneWeight(BoneWeight weight)
        {
            return new InlinedBoneWeight
            {
                Indices = new int4(weight.boneIndex0, weight.boneIndex1, weight.boneIndex2, weight.boneIndex3),
                Weights = new float4(weight.weight0, weight.weight1, weight.weight2, weight.weight3),
            };
        }
    }
}
