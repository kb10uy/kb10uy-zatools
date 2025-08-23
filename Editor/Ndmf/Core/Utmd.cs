using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using KusakaFactory.Zatools.Foundation;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Ndmf.Core
{
    internal static class Utmd
    {
        internal static void Process(Mesh modifyingMesh, FixedParameters parameters)
        {
            var uvs = new List<Vector4>(modifyingMesh.vertexCount);
            modifyingMesh.GetUVs((int)parameters.Source, uvs);

            // AsyncGPUReadback cannot use Temp memory as input since the result may only become available at an unspecified point in the future.
            var sourceUvs = new NativeArray<float4>(uvs.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var targetUvs = new NativeArray<float4>(uvs.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var sourceColors = new NativeArray<float4>(uvs.Count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            for (var i = 0; i < sourceUvs.Length; ++i) sourceUvs[i] = uvs[i];
            NativeTextureSampler.SampleByComputeShader(parameters.TileMap, ref sourceUvs, ref sourceColors);
            var job = new MoveUvTileJob
            {
                TargetUvs = targetUvs,
                SourceUvs = sourceUvs,
                Colors = sourceColors,
                Distribution = parameters.Distribution,
            };
            var handle = job.Schedule(sourceUvs.Length, 4);
            handle.Complete();

            modifyingMesh.SetUVs((int)parameters.Target, targetUvs);

            sourceUvs.Dispose();
            sourceColors.Dispose();
            targetUvs.Dispose();
        }

        internal struct FixedParameters : IEquatable<FixedParameters>
        {
            internal UvChannel Source;
            internal UvChannel Target;
            internal TileDistribution Distribution;
            internal Texture2D TileMap;

            internal static FixedParameters FixFromComponent(UvTileMapDistribution component)
            {
                return new FixedParameters()
                {
                    Source = component.Source,
                    Target = component.Target,
                    Distribution = component.Distribution,
                    TileMap = component.TileMap,
                };
            }

            public bool Equals(FixedParameters other)
            {
                return Source == other.Source
                    && Target == other.Target
                    && Distribution == other.Distribution
                    && TileMap == other.TileMap;
            }

            public override bool Equals(object obj) => obj is FixedParameters && Equals((FixedParameters)obj);

            public override int GetHashCode() => (Source, Target, Distribution, TileMap).GetHashCode();

            public static bool operator ==(FixedParameters lhs, FixedParameters rhs) => lhs.Equals(rhs);

            public static bool operator !=(FixedParameters lhs, FixedParameters rhs) => !(lhs == rhs);
        }

        [BurstCompile]
        internal struct MoveUvTileJob : IJobParallelFor
        {
            internal NativeArray<float4> TargetUvs;
            [ReadOnly] internal NativeArray<float4> SourceUvs;
            [ReadOnly] internal NativeArray<float4> Colors;
            [ReadOnly] internal TileDistribution Distribution;

            public void Execute(int index)
            {
                var source = SourceUvs[index];
                var (tileX, tileY) = Distribution switch
                {
                    TileDistribution.RedGreen => TileByRedGreen(Colors[index]),
                    TileDistribution.Ansi16 => TileByAnsi16(Colors[index]),
                    _ => (0, 0),
                };
                TargetUvs[index] = new float4(source.x + tileX, source.y + tileY, source.z, source.w);
            }

            public (int X, int Y) TileByRedGreen(float4 color)
            {
                return ((int)(color.x * 3.995), (int)(color.y * 3.995));
            }

            public (int X, int Y) TileByAnsi16(float4 color)
            {
                var isBright = math.max(math.max(color.x, color.y), color.z) >= 0.75f;
                var threshold = isBright ? 0.75f : 0.25f;
                var relativeColor = color - threshold;

                int x = (relativeColor.y >= 0 ? 2 : 0) + (relativeColor.x >= 0 ? 1 : 0);
                int y = (isBright ? 2 : 0) + (relativeColor.z >= 0 ? 1 : 0);
                return (x, y);
            }
        }
    }
}
