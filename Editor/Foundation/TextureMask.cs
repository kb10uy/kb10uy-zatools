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

        internal float Take(Vector2 uv)
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
}
