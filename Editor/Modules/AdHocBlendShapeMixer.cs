using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using nadena.dev.ndmf;
using KusakaFactory.Zatools.Runtime;
using UnityObject = UnityEngine.Object;

namespace KusakaFactory.Zatools.Modules
{
    internal sealed class AdHocBlendShapeMixer : Pass<AdHocBlendShapeMixer>
    {
        public override string QualifiedName => nameof(AdHocBlendShapeMixer);
        public override string DisplayName => "Mix BlendShapes";

        protected override void Execute(BuildContext context)
        {
            var mixComponents = context.AvatarRootObject.GetComponentsInChildren<AdHocBlendShapeMix>();
            foreach (var mixComponent in mixComponents) ProcessFor(mixComponent, mixComponent.GetComponent<SkinnedMeshRenderer>());
        }

        private void ProcessFor(AdHocBlendShapeMix mixComponent, SkinnedMeshRenderer skinnedMeshRenderer)
        {
            var originalMesh = skinnedMeshRenderer.sharedMesh;
            if (originalMesh == null) return;
            var blendShapeIndices = Enumerable.Range(0, originalMesh.blendShapeCount)
                .ToDictionary((i) => originalMesh.GetBlendShapeName(i), (i) => i);

            var resolvedMixDefinitions = AggregateDefinitions(mixComponent.MixDefinitions, blendShapeIndices);
            if (resolvedMixDefinitions.Count == 0) return;

            var modifiedMesh = mixComponent.Replace ?
                ProcessOverwrite(originalMesh, resolvedMixDefinitions) :
                ProcessAppend(originalMesh, resolvedMixDefinitions);

            skinnedMeshRenderer.sharedMesh = modifiedMesh;
            ObjectRegistry.RegisterReplacedObject(originalMesh, modifiedMesh);
            UnityObject.DestroyImmediate(mixComponent);
        }

        private List<AggregatedDefinition> AggregateDefinitions(
            BlendShapeMixDefinition[] definitions,
            Dictionary<string, int> blendShapeIndices
        )
        {
            return definitions
                .GroupBy((d) => d.ToBlendShape)
                .Where((dg) => blendShapeIndices.ContainsKey(dg.Key))
                .Select((dg) => new AggregatedDefinition
                {
                    TargetIndex = blendShapeIndices[dg.Key],
                    TargetName = dg.Key,
                    Sources = dg
                        .GroupBy((d) => d.FromBlendShape)
                        .Where((sdg) => blendShapeIndices.ContainsKey(sdg.Key))
                        .Select((sdg) => (blendShapeIndices[sdg.Key], sdg.Key, sdg.Sum((d) => d.MixWeight)))
                        .ToArray()
                })
                .OrderBy((ad) => ad.TargetIndex)
                .ToList();
        }

        /// <summary>
        /// 上書きモード
        /// </summary>
        private Mesh ProcessOverwrite(Mesh originalMesh, IEnumerable<AggregatedDefinition> resolvedMixDefinitions)
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

            var modifiedMesh = UnityObject.Instantiate(originalMesh);
            modifiedMesh.ClearBlendShapes();
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
                    Debug.Log($"AdHocBlendShapeMixer: mixing '{name}' from {nextDefinition.Sources.Length} BlendShape(s)");
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

                modifiedMesh.AddBlendShapeFrame(name, maxWeight, positionsResultBuffer, normalsResultBuffer, tangentsResultBuffer);
            }

            return modifiedMesh;
        }

        /// <summary>
        /// 追加モード
        /// </summary>
        private Mesh ProcessAppend(Mesh originalMesh, IEnumerable<AggregatedDefinition> resolvedMixDefinitions)
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

            var modifiedMesh = UnityObject.Instantiate(originalMesh);
            modifiedMesh.ClearBlendShapes();
            foreach (var definition in resolvedMixDefinitions)
            {
                originalMesh.GetBlendShapeFrameVertices(definition.TargetIndex, 0, positionsResultBuffer, normalsResultBuffer, tangentsResultBuffer);
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

                var newName = $"{definition.TargetName}_[{string.Join(',', definition.Sources.Select((s) => s.SourceName))}]";
                var maxWeight = originalMesh.GetBlendShapeFrameWeight(definition.TargetIndex, 0);
                modifiedMesh.AddBlendShapeFrame(newName, maxWeight, positionsResultBuffer, normalsResultBuffer, tangentsResultBuffer);
            }

            return modifiedMesh;
        }

        class AggregatedDefinition
        {
            internal int TargetIndex;
            internal string TargetName;
            internal (int SourceIndex, string SourceName, float TotalWeight)[] Sources;
        }
    }
}
