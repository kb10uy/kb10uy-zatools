using System;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Mathematics;

namespace KusakaFactory.Zatools.Foundation
{
    /// <summary>
    /// Samples a readable texture as a grayscale mask.
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
        /// <remarks>
        /// colorsOutput should be allocated with Allocator.Persistent.
        /// </remarks>
        public static void SampleByComputeShader(Texture2D texture, ref NativeArray<float4> uvs, ref NativeArray<float4> colorsOutput)
        {
            var computeShader = ZatoolsResources.LoadComputeShaderByGuid("69529c6a64173b142a4966bbb00ea374");
            var computeKernelId = computeShader.FindKernel("SampleColorsByUv");

            using var uvBuffer = new ComputeBuffer(uvs.Length, 16);
            using var colorBuffer = new ComputeBuffer(uvs.Length, 16);
            uvBuffer.SetData(uvs);

            computeShader.SetTexture(computeKernelId, "SourceTexture", texture);
            computeShader.SetBuffer(computeKernelId, "SamplingUvs", uvBuffer);
            computeShader.SetBuffer(computeKernelId, "SampledColors", colorBuffer);
            computeShader.Dispatch(computeKernelId, uvs.Length, 1, 1);

            var request = AsyncGPUReadback.RequestIntoNativeArray(ref colorsOutput, colorBuffer);
            request.WaitForCompletion();
        }
    }
}
