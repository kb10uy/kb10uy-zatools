using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace KusakaFactory.Zatools.Foundation
{
    internal static class MeshManipulation
    {
        private static readonly int MaxBlendShapesPerBatch = 8;
        private static readonly int VerticesPerBatch = 128;

        internal static Vector3[] ComputeBlendShapeAppliedVertices(Mesh mesh, SkinnedMeshRenderer renderer, float epsilon = 1e-4f)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            if (renderer == null) throw new ArgumentNullException(nameof(renderer));

            var baseVertices = mesh.vertices;
            var vertexCount = baseVertices.Length;
            var result = new Vector3[vertexCount];
            Array.Copy(baseVertices, result, vertexCount);
            if (vertexCount == 0 || mesh.blendShapeCount == 0) return result;

            if (renderer.sharedMesh != null && renderer.sharedMesh.vertexCount != vertexCount)
            {
                throw new ArgumentException("different mesh vertex count", nameof(renderer));
            }

            var deltaVertices = new Vector3[vertexCount];
            var _deltaNormals = new Vector3[vertexCount];
            var _deltaTangents = new Vector3[vertexCount];

            var resultNative = new NativeArray<Vector3>(result, Allocator.TempJob);
            var deltaBatchNative = new NativeArray<Vector3>(vertexCount * MaxBlendShapesPerBatch, Allocator.TempJob);
            var weightsNative = new NativeArray<float>(MaxBlendShapesPerBatch, Allocator.TempJob);
            try
            {
                var activeBlendShapeCount = 0;
                var blendShapeCount = mesh.blendShapeCount;
                for (var blendShapeIndex = 0; blendShapeIndex < blendShapeCount; blendShapeIndex++)
                {
                    // TODO: Support multi-frame BlendShapes
                    if (mesh.GetBlendShapeFrameCount(blendShapeIndex) != 1) continue;

                    var weight = renderer.GetBlendShapeWeight(blendShapeIndex) / mesh.GetBlendShapeFrameWeight(blendShapeIndex, 0);
                    if (Mathf.Abs(weight) < epsilon) continue;

                    mesh.GetBlendShapeFrameVertices(blendShapeIndex, 0, deltaVertices, _deltaNormals, _deltaTangents);

                    var nativeSpan = deltaBatchNative.AsSpan().Slice(activeBlendShapeCount * vertexCount, vertexCount);
                    deltaVertices.CopyTo(nativeSpan);
                    weightsNative[activeBlendShapeCount] = weight;
                    ++activeBlendShapeCount;

                    if (activeBlendShapeCount < MaxBlendShapesPerBatch) continue;

                    var applyJob = new ApplyBlendShapeDeltaBatchJob
                    {
                        Result = resultNative,
                        DeltaVertices = deltaBatchNative,
                        Weights = weightsNative,
                        VertexCount = vertexCount,
                        ActiveBlendShapeCount = activeBlendShapeCount,
                    };
                    applyJob.Schedule(vertexCount, VerticesPerBatch).Complete();
                    activeBlendShapeCount = 0;
                }

                if (activeBlendShapeCount > 0)
                {
                    var applyJob = new ApplyBlendShapeDeltaBatchJob
                    {
                        Result = resultNative,
                        DeltaVertices = deltaBatchNative,
                        Weights = weightsNative,
                        VertexCount = vertexCount,
                        ActiveBlendShapeCount = activeBlendShapeCount,
                    };
                    applyJob.Schedule(vertexCount, VerticesPerBatch).Complete();
                }

                return resultNative.ToArray();
            }
            finally
            {
                resultNative.Dispose();
                deltaBatchNative.Dispose();
                weightsNative.Dispose();
            }
        }

        [BurstCompile]
        private struct ApplyBlendShapeDeltaBatchJob : IJobParallelFor
        {
            internal NativeArray<Vector3> Result;
            [ReadOnly] internal NativeArray<Vector3> DeltaVertices;
            [ReadOnly] internal NativeArray<float> Weights;
            internal int VertexCount;
            internal int ActiveBlendShapeCount;

            public void Execute(int index)
            {
                var value = Result[index];
                for (var blendShapeIndex = 0; blendShapeIndex < ActiveBlendShapeCount; blendShapeIndex++)
                {
                    value += DeltaVertices[(blendShapeIndex * VertexCount) + index] * Weights[blendShapeIndex];
                }
                Result[index] = value;
            }
        }
    }
}
