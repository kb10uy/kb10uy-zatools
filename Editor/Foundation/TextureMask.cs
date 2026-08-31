using System;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Mathematics;

namespace KusakaFactory.Zatools.Foundation
{
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

        private static readonly float3 LuminanceCoefficient = new float3(0.213f, 0.715f, 0.072f);

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

                // GetPixels32 と異なりサンプラーは sRGB テクスチャを線形化するので、CPU 実装と同じ値に戻す
                var encodeToGamma = texture.isDataSRGB && QualitySettings.activeColorSpace == ColorSpace.Linear;
                for (var i = 0; i < maskOutput.Length; ++i)
                {
                    var color = sampledColors[i].xyz;
                    if (encodeToGamma)
                    {
                        color = new float3(
                            Mathf.LinearToGammaSpace(color.x),
                            Mathf.LinearToGammaSpace(color.y),
                            Mathf.LinearToGammaSpace(color.z));
                    }

                    var luminance = math.dot(color, LuminanceCoefficient);
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
            var luma1000 = 213 * sample.r + 715 * sample.g + 72 * sample.b;
            return _mode switch
            {
                Mode.TakeWhite => luma1000 / 255000.0f,
                Mode.TakeBlack => (255000 - luma1000) / 255000.0f,
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
