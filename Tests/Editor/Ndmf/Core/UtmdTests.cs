using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using KusakaFactory.Zatools.Ndmf.Core;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Tests.Ndmf.Core
{
    public sealed class UtmdTests : MeshTestBase
    {
        [TestCase(0.0f, 0.0f, 0, 0)]
        [TestCase(0.25f, 0.5f, 0, 1)]
        [TestCase(0.26f, 0.76f, 1, 3)]
        [TestCase(0.5f, 0.75f, 1, 2)]
        [TestCase(1.0f, 1.0f, 3, 3)]
        public void RedGreenPicksTileFromQuarters(float r, float g, int x, int y)
        {
            Assert.That(default(Utmd.MoveUvTileJob).TileByRedGreen(new float4(r, g, 0.0f, 1.0f)), Is.EqualTo((x, y)));
        }

        [TestCase(0.0f, 0.0f, 0.0f, 0, 0)]
        [TestCase(0.5f, 0.0f, 0.0f, 1, 0)]
        [TestCase(0.0f, 0.5f, 0.0f, 2, 0)]
        [TestCase(0.5f, 0.5f, 0.0f, 3, 0)]
        [TestCase(0.0f, 0.0f, 0.5f, 0, 1)]
        [TestCase(0.5f, 0.5f, 0.5f, 3, 1)]
        [TestCase(1.0f, 0.0f, 0.0f, 1, 2)]
        [TestCase(1.0f, 0.5f, 0.5f, 1, 2)]
        [TestCase(0.0f, 0.0f, 1.0f, 0, 3)]
        [TestCase(1.0f, 1.0f, 1.0f, 3, 3)]
        public void Ansi16PicksTileFromPalette(float r, float g, float b, int x, int y)
        {
            Assert.That(default(Utmd.MoveUvTileJob).TileByAnsi16(new float4(r, g, b, 1.0f)), Is.EqualTo((x, y)));
        }

        [TestCase(TileDistribution.RedGreen)]
        [TestCase(TileDistribution.Ansi16)]
        public void JobOffsetsUvsByTileAndKeepsZw(TileDistribution distribution)
        {
            var colors = new[] { new float4(0, 0, 0, 1), new float4(1, 0, 0, 1), new float4(0, 1, 0, 1), new float4(1, 1, 1, 1) };
            var sourceUvs = new NativeArray<float4>(colors.Length, Allocator.TempJob);
            var targetUvs = new NativeArray<float4>(colors.Length, Allocator.TempJob);
            var nativeColors = new NativeArray<float4>(colors, Allocator.TempJob);
            try
            {
                for (var i = 0; i < colors.Length; ++i) sourceUvs[i] = new float4(0.1f * i, 0.2f, 7.0f, 8.0f);
                var job = new Utmd.MoveUvTileJob
                {
                    TargetUvs = targetUvs,
                    SourceUvs = sourceUvs,
                    Colors = nativeColors,
                    Distribution = distribution,
                };
                job.Schedule(colors.Length, 1).Complete();

                for (var i = 0; i < colors.Length; ++i)
                {
                    var (x, y) = distribution == TileDistribution.RedGreen ? job.TileByRedGreen(colors[i]) : job.TileByAnsi16(colors[i]);
                    AssertVector(new Vector4(0.1f * i + x, 0.2f + y, 7.0f, 8.0f), targetUvs[i]);
                }
            }
            finally
            {
                sourceUvs.Dispose();
                targetUvs.Dispose();
                nativeColors.Dispose();
            }
        }

        [Test]
        [Category("GPU")]
        public void ProcessWritesTiledUvsToTargetChannel()
        {
            AssumeComputeShaders();

            var tileMap = CreateTexture(2, 2, new Color(0, 0, 0), new Color(1, 0, 0), new Color(0, 1, 0), new Color(0.5f, 0.5f, 0));
            var mesh = Track(new Mesh());
            var sourceUvs = new[] { new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.25f), new Vector2(0.25f, 0.75f), new Vector2(0.75f, 0.75f) };
            mesh.vertices = new Vector3[sourceUvs.Length];
            mesh.uv = sourceUvs;

            Utmd.Process(mesh, new Utmd.FixedParameters
            {
                Source = UvChannel.UV0,
                Target = UvChannel.UV2,
                Distribution = TileDistribution.RedGreen,
                TileMap = tileMap,
            });

            var targetUvs = new List<Vector4>();
            mesh.GetUVs(2, targetUvs);
            AssertVectors(
                new[]
                {
                    new Vector4(0.25f, 0.25f, 0, 0),
                    new Vector4(3.75f, 0.25f, 0, 0),
                    new Vector4(0.25f, 3.75f, 0, 0),
                    new Vector4(1.75f, 1.75f, 0, 0),
                },
                targetUvs);

            var untouched = new List<Vector2>();
            mesh.GetUVs(0, untouched);
            Assert.That(untouched, Is.EqualTo(sourceUvs));
        }
    }
}
