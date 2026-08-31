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
    /// Samples a texture as a grayscale mask.
    /// </summary>
    /// <remarks>
    /// This type is public only so that it can be used by external in-house assemblies.
    /// It is not a stable public API and may change or be removed without notice between releases.
    /// </remarks>
    public sealed class TextureMask
    {
        public enum Mode
        {
            TakeWhite,
            TakeBlack,
        }

        private readonly Color32[] _pixels;
        private readonly int _width;
        private readonly int _height;
        private readonly Mode _mode;

        public TextureMask(Texture2D texture, Mode mode)
        {
            if (texture != null && texture.isReadable)
            {
                _pixels = texture.GetPixels32(0);
                _width = texture.width;
                _height = texture.height;
            }
            else
            {
                _pixels = new[] { new Color32(255, 255, 255, 255) };
                _width = 1;
                _height = 1;
            }
            _mode = mode;
        }

        /// <summary>
        /// Samples a texture as a grayscale mask without requiring it to be readable.
        /// </summary>
        /// <remarks>
        /// maskOutput may be allocated with any allocator.
        /// </remarks>
        public static void SampleByComputeShader(Texture2D texture, Mode mode, ref NativeArray<float4> uvs, ref NativeArray<float> maskOutput)
        {
            var takeBlack = mode switch
            {
                Mode.TakeWhite => false,
                Mode.TakeBlack => true,
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
                NativeTextureSampler.SampleByComputeShader(texture, ref uvs, ref sampledColors);

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

        public float Take(Vector2 uv)
        {
            var sample = SampleByUv(uv);
            var luminance = Luminance.Rec709(new float3(sample.r, sample.g, sample.b) / 255.0f);
            return _mode switch
            {
                Mode.TakeWhite => luminance,
                Mode.TakeBlack => 1.0f - luminance,
                _ => throw new InvalidOperationException("unknown mode"),
            };
        }

        private Color32 SampleByUv(Vector2 uv)
        {
            // point sampling
            var canonicalU = uv.x - Mathf.Floor(uv.x);
            var canonicalV = uv.y - Mathf.Floor(uv.y);
            var pixelX = (int)(canonicalU * _width);
            var pixelY = (int)(canonicalV * _height);
            var index = pixelY * _width + pixelX;
            return _pixels[index];
        }
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
    }
}
