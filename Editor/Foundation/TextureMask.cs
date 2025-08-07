using System;
using UnityEngine;

namespace KusakaFactory.Zatools.Foundation
{
    internal sealed class TextureMask
    {
        internal enum Mode
        {
            TakeWhite,
            TakeBlack,
        }

        private readonly Color32[] _pixels;
        private readonly int _width;
        private readonly int _height;
        private readonly Mode _mode;

        internal TextureMask(Texture2D texture, Mode mode)
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

        internal bool Take(float u, float v)
        {
            var sample = SampleByUv(u, v);
            var luma1000 = 213 * sample.r + 715 * sample.g + 72 * sample.b;
            return _mode switch
            {
                Mode.TakeWhite => luma1000 >= 127500,
                Mode.TakeBlack => luma1000 < 127500,
                _ => throw new InvalidOperationException("unknown mode"),
            };
        }

        private Color32 SampleByUv(float u, float v)
        {
            // point sampling
            var canonicalU = u - Mathf.Floor(u);
            var canonicalV = v - Mathf.Floor(v);
            var pixelX = (int)(canonicalU * _width);
            var pixelY = (int)(canonicalV * _height);
            var index = pixelY * _width + pixelX;
            return _pixels[index];
        }
    }
}
