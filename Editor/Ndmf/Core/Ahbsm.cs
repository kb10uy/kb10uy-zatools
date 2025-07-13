using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using KusakaFactory.Zatools.Runtime;
using UnityObject = UnityEngine.Object;

namespace KusakaFactory.Zatools.Ndmf.Core
{
    internal static class Ahbsm
    {
        internal static Dictionary<string, int> FetchBlendShapeIndices(Mesh originalMesh) =>
            Enumerable.Range(0, originalMesh.blendShapeCount)
                .ToDictionary(
                    (i) => originalMesh.GetBlendShapeName(i),
                    (i) => i
                );

        internal static List<AggregatedDefinition> AggregateDefinitions(
            ImmutableArray<(string SourceName, string TargetName, float Weight)> definitions,
            Dictionary<string, int> blendShapeIndices
        )
        {
            return definitions
                .GroupBy((d) => d.TargetName)
                .Where((dg) => blendShapeIndices.ContainsKey(dg.Key))
                .Select((dg) => new AggregatedDefinition
                {
                    TargetIndex = blendShapeIndices[dg.Key],
                    TargetName = dg.Key,
                    Sources = dg
                        .GroupBy((d) => d.SourceName)
                        .Where((sdg) => blendShapeIndices.ContainsKey(sdg.Key))
                        .Select((sdg) => (blendShapeIndices[sdg.Key], sdg.Key, sdg.Sum((d) => d.Weight)))
                        .ToArray()
                })
                .OrderBy((ad) => ad.TargetIndex)
                .ToList();
        }

        internal static Mesh ProcessOverwrite(Mesh originalMesh, IEnumerable<AggregatedDefinition> resolvedMixDefinitions)
        {
            var modifiedMesh = UnityObject.Instantiate(originalMesh);
            ProcessOverwrite(originalMesh, modifiedMesh, resolvedMixDefinitions);
            return modifiedMesh;
        }

        internal static void ProcessOverwrite(Mesh originalMesh, Mesh modifyingMesh, IEnumerable<AggregatedDefinition> resolvedMixDefinitions)
        {
            var meshVertices = originalMesh.vertexCount;
            var meshBlendShapes = originalMesh.blendShapeCount;
            var definitionEnumerator = resolvedMixDefinitions.GetEnumerator();
            var nextDefinition = definitionEnumerator.MoveNext() ? definitionEnumerator.Current : null;
            Vector3[] positionsResultBuffer = new Vector3[meshVertices];
            Vector3[] normalsResultBuffer = new Vector3[meshVertices];
            Vector3[] tangentsResultBuffer = new Vector3[meshVertices];
            Vector3[] positionsSourceBuffer = new Vector3[meshVertices];
            Vector3[] normalsSourceBuffer = new Vector3[meshVertices];
            Vector3[] tangentsSourceBuffer = new Vector3[meshVertices];

            modifyingMesh.ClearBlendShapes();
            for (var bs = 0; bs < meshBlendShapes; ++bs)
            {
                // multi-frame BlendShape は本当に無理
                if (originalMesh.GetBlendShapeFrameCount(bs) != 1)
                {
                    throw new Exception("multi-frame BlendShape is unsupported");
                }

                var name = originalMesh.GetBlendShapeName(bs);
                var maxWeight = originalMesh.GetBlendShapeFrameWeight(bs, 0);
                originalMesh.GetBlendShapeFrameVertices(bs, 0, positionsResultBuffer, normalsResultBuffer, tangentsResultBuffer);

                // 次のコピー定義が対象
                if (nextDefinition != null && bs == nextDefinition.TargetIndex)
                {
                    // Debug.Log($"AdHocBlendShapeMixer: mixing '{name}' from {nextDefinition.Sources.Length} BlendShape(s)");
                    foreach (var copySource in nextDefinition.Sources)
                    {
                        originalMesh.GetBlendShapeFrameVertices(copySource.SourceIndex, 0, positionsSourceBuffer, normalsSourceBuffer, tangentsSourceBuffer);
                        for (int i = 0; i < meshVertices; ++i)
                        {
                            positionsResultBuffer[i] += positionsSourceBuffer[i] * copySource.TotalWeight;
                            normalsResultBuffer[i] += normalsSourceBuffer[i] * copySource.TotalWeight;
                            tangentsResultBuffer[i] += tangentsSourceBuffer[i] * copySource.TotalWeight;
                        }
                    }
                    nextDefinition = definitionEnumerator.MoveNext() ? definitionEnumerator.Current : null;
                }

                modifyingMesh.AddBlendShapeFrame(name, maxWeight, positionsResultBuffer, normalsResultBuffer, tangentsResultBuffer);
            }
        }

        internal static Mesh ProcessAppend(Mesh originalMesh, IEnumerable<AggregatedDefinition> resolvedMixDefinitions)
        {
            var modifiedMesh = UnityObject.Instantiate(originalMesh);
            ProcessAppend(originalMesh, modifiedMesh, resolvedMixDefinitions);
            return modifiedMesh;
        }

        internal static void ProcessAppend(Mesh originalMesh, Mesh modifyingMesh, IEnumerable<AggregatedDefinition> resolvedMixDefinitions)
        {
            var meshVertices = originalMesh.vertexCount;
            var meshBlendShapes = originalMesh.blendShapeCount;
            Vector3[] positionsResultBuffer = new Vector3[meshVertices];
            Vector3[] normalsResultBuffer = new Vector3[meshVertices];
            Vector3[] tangentsResultBuffer = new Vector3[meshVertices];
            Vector3[] positionsSourceBuffer = new Vector3[meshVertices];
            Vector3[] normalsSourceBuffer = new Vector3[meshVertices];
            Vector3[] tangentsSourceBuffer = new Vector3[meshVertices];

            foreach (var definition in resolvedMixDefinitions)
            {
                originalMesh.GetBlendShapeFrameVertices(definition.TargetIndex, 0, positionsResultBuffer, normalsResultBuffer, tangentsResultBuffer);
                foreach (var copySource in definition.Sources)
                {
                    originalMesh.GetBlendShapeFrameVertices(copySource.SourceIndex, 0, positionsSourceBuffer, normalsSourceBuffer, tangentsSourceBuffer);
                    for (int i = 0; i < meshVertices; ++i)
                    {
                        positionsResultBuffer[i] += positionsSourceBuffer[i] * copySource.TotalWeight;
                        normalsResultBuffer[i] += normalsSourceBuffer[i] * copySource.TotalWeight;
                        tangentsResultBuffer[i] += tangentsSourceBuffer[i] * copySource.TotalWeight;
                    }
                }

                var newName = $"{definition.TargetName}_[{string.Join(',', definition.Sources.Select((s) => s.SourceName))}]";
                var maxWeight = originalMesh.GetBlendShapeFrameWeight(definition.TargetIndex, 0);
                modifyingMesh.AddBlendShapeFrame(newName, maxWeight, positionsResultBuffer, normalsResultBuffer, tangentsResultBuffer);
            }
        }

        internal static ImmutableArray<(string SourceName, string TargetName, float Weight)> FixSources(this IEnumerable<BlendShapeMixDefinition> sources) =>
            sources
                .Select((d) => (d.FromBlendShape, d.ToBlendShape, d.MixWeight))
                .ToImmutableArray();

        internal class AggregatedDefinition
        {
            internal int TargetIndex;
            internal string TargetName;
            internal (int SourceIndex, string SourceName, float TotalWeight)[] Sources;
        }
    }
}
