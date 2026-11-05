using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using nadena.dev.ndmf;
using nadena.dev.ndmf.preview;
using KusakaFactory.Zatools.Runtime;
using KusakaFactory.Zatools.Ndmf.Core;
using UnityObject = UnityEngine.Object;

namespace KusakaFactory.Zatools.Ndmf.Preview
{
    internal sealed class AhamdRenderFilter : ZatoolsRenderFilter<AdHocAdvancedMeshDuplication, AhamdRenderFilterNode>
    {
        private static readonly TogglablePreviewNode _previewNode = CreateTogglablePreviewNode("Ad-hoc Advanced Mesh Duplication", "ad-hoc-advanced-mesh-duplication", false);

        protected override TogglablePreviewNode PreviewNode => _previewNode;
        internal static TogglablePreviewNode SwitchingPreviewNode => _previewNode;
    }

    internal sealed class AhamdRenderFilterNode : ZatoolsRenderFilterNode<AdHocAdvancedMeshDuplication, AhamdRenderFilterNode>
    {
        private Mesh _generatedMesh = null;
        private List<Material> _materials = null;
        private SkinnedMeshRenderer _source = null;

        protected override RenderAspects ReadAspects => RenderAspects.Mesh | RenderAspects.Material | RenderAspects.Shapes;
        protected override RenderAspects WrittenAspects => RenderAspects.Mesh | RenderAspects.Material | RenderAspects.Shapes;

        protected internal override ValueTask Initialize(
            SkinnedMeshRenderer original,
            SkinnedMeshRenderer proxyed,
            AdHocAdvancedMeshDuplication[] components,
            ComputeContext context
        )
        {
            // AdHocAdvancedMeshDuplication has DisallowMultipleComponent, a renderer will have at most one.
            var observedParameter = context.Observe(components[0], Ahamd.FixedParameters.FixFromComponent, (op, np) => op == np);
            _source = observedParameter.Source;
            if (_source != null) context.Observe(_source, (s) => s.sharedMesh);
            if (observedParameter.SelectionTexture != null) context.Observe(observedParameter.SelectionTexture, ObserveTextureContents);
            foreach (var modification in observedParameter.Modifications)
            {
                if (modification.ExtraTexture != null) context.Observe(modification.ExtraTexture, ObserveTextureContents);
            }

            if (proxyed == null || proxyed.sharedMesh != null) return default;

            if (!Ahamd.TryCompilePrograms(observedParameter, Ahamd.TryCompileSilently, out var programs)) return default;

            var generatedMesh = new Mesh { name = $"Advanced Mesh Duplication for Preview" };
            Ahamd.Process(proxyed, generatedMesh, observedParameter, programs);

            _generatedMesh = generatedMesh;
            _materials = proxyed.sharedMaterials.ToList();
            proxyed.sharedMesh = generatedMesh;

            return default;
        }

        private static (int Width, int Height, Hash128 ContentsHash) ObserveTextureContents(Texture2D texture) =>
            (texture.width, texture.height, texture.imageContentsHash);

        protected override void ZatoolsOnFrame(Renderer original, Renderer proxy)
        {
            if (_generatedMesh == null || _materials == null || _source == null) return;
            if (proxy is SkinnedMeshRenderer proxyedSkinnedMeshRenderer)
            {
                proxyedSkinnedMeshRenderer.sharedMesh = _generatedMesh;
                proxyedSkinnedMeshRenderer.sharedMaterials = _materials.ToArray();
                proxyedSkinnedMeshRenderer.bones = _source.bones;
                proxyedSkinnedMeshRenderer.rootBone = _source.rootBone;
                proxyedSkinnedMeshRenderer.probeAnchor = _source.probeAnchor;
                proxyedSkinnedMeshRenderer.localBounds = _source.localBounds;
            }
        }

        protected override void ZatoolsDispose()
        {
            if (_generatedMesh == null) return;

            UnityObject.DestroyImmediate(_generatedMesh);
            _generatedMesh = null;
            _materials = null;
        }
    }
}
