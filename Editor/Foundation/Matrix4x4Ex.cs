using System;
using System.Collections.Generic;
using UnityEngine;

namespace KusakaFactory.Zatools.Foundation
{
    internal static class ExtraMath
    {
        /// <summary>
        /// Matrix4x4 を指定したウェイトで合成する。
        /// </summary>
        internal static Matrix4x4 BlendMatrices(IReadOnlyList<Matrix4x4> sources, params (int Index, float Weight)[] weights)
        {
            var result = Matrix4x4.zero;
            foreach ((var index, var weight) in weights)
            {
                result.m00 += sources[index].m00 * weight;
                result.m01 += sources[index].m01 * weight;
                result.m02 += sources[index].m02 * weight;
                result.m03 += sources[index].m03 * weight;
                result.m10 += sources[index].m10 * weight;
                result.m11 += sources[index].m11 * weight;
                result.m12 += sources[index].m12 * weight;
                result.m13 += sources[index].m13 * weight;
                result.m20 += sources[index].m20 * weight;
                result.m21 += sources[index].m21 * weight;
                result.m22 += sources[index].m22 * weight;
                result.m23 += sources[index].m23 * weight;
                result.m30 += sources[index].m30 * weight;
                result.m31 += sources[index].m31 * weight;
                result.m32 += sources[index].m32 * weight;
                result.m33 += sources[index].m33 * weight;
            }
            return result;
        }
    }
}
