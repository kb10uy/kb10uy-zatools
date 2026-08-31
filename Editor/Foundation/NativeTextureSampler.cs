using System;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Mathematics;

namespace KusakaFactory.Zatools.Foundation
{
    /// <summary>
    /// Relative luminance in Rec. 709.
    /// </summary>
    /// <remarks>
    /// This type is public only so that it can be used by external in-house assemblies.
    /// It is not a stable public API and may change or be removed without notice between releases.
    /// </remarks>
    public static class Luminance
    {
        public const float Rec709Red = 0.2126f;
        public const float Rec709Green = 0.7152f;
        public const float Rec709Blue = 0.0722f;

        public static float3 Rec709Coefficient => new float3(Rec709Red, Rec709Green, Rec709Blue);

        public static float Rec709(float3 color) => math.dot(color, Rec709Coefficient);
    }

    /// <summary>
    /// Samples textures into native arrays using a compute shader.
    /// </summary>
    /// <remarks>
    /// This type is public only so that it can be used by external in-house assemblies.
    /// It is not a stable public API and may change or be removed without notice between releases.
    /// </remarks>
    public static class NativeTextureSampler
    {
        public enum MaskMode
        {
            TakeWhite,
            TakeBlack,
        }

        private const int ThreadGroupSize = 64;

        /// <remarks>
        /// colorsOutput should be allocated with Allocator.Persistent.
        /// </remarks>
        public static void SampleByComputeShader(Texture2D texture, ref NativeArray<float4> uvs, ref NativeArray<float4> colorsOutput)
        {
            if (uvs.Length == 0) return;

            var computeShader = ZatoolsResources.LoadComputeShaderByGuid("69529c6a64173b142a4966bbb00ea374");
            var computeKernelId = computeShader.FindKernel("SampleColorsByUv");

            using var uvBuffer = new ComputeBuffer(uvs.Length, 16);
            using var colorBuffer = new ComputeBuffer(uvs.Length, 16);
            uvBuffer.SetData(uvs);

            computeShader.SetTexture(computeKernelId, "SourceTexture", texture);
            computeShader.SetBuffer(computeKernelId, "SamplingUvs", uvBuffer);
            computeShader.SetBuffer(computeKernelId, "SampledColors", colorBuffer);
            computeShader.SetInt("SamplingCount", uvs.Length);
            computeShader.Dispatch(computeKernelId, (uvs.Length + ThreadGroupSize - 1) / ThreadGroupSize, 1, 1);

            var request = AsyncGPUReadback.RequestIntoNativeArray(ref colorsOutput, colorBuffer);
            request.WaitForCompletion();
        }

        /// <summary>
        /// Samples a texture as a grayscale mask.
        /// </summary>
        /// <remarks>
        /// maskOutput may be allocated with any allocator.
        /// </remarks>
        public static void SampleMaskByComputeShader(Texture2D texture, MaskMode mode, ref NativeArray<float4> uvs, ref NativeArray<float> maskOutput)
        {
            var takeBlack = mode switch
            {
                MaskMode.TakeWhite => false,
                MaskMode.TakeBlack => true,
                _ => throw new InvalidOperationException("unknown mode"),
            };

            if (texture == null)
            {
                var uniformValue = takeBlack ? 0.0f : 1.0f;
                for (var i = 0; i < maskOutput.Length; ++i) maskOutput[i] = uniformValue;
                return;
            }

            var sampledColors = new NativeArray<float4>(uvs.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            try
            {
                SampleByComputeShader(texture, ref uvs, ref sampledColors);

                for (var i = 0; i < maskOutput.Length; ++i)
                {
                    var luminance = Luminance.Rec709(sampledColors[i].xyz);
                    maskOutput[i] = takeBlack ? 1.0f - luminance : luminance;
                }
            }
            finally
            {
                sampledColors.Dispose();
            }
        }
    }
}
