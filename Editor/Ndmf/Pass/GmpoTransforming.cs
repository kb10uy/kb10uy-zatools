using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using KusakaFactory.Zatools.Runtime;
using KusakaFactory.Zatools.Ndmf.Core;
using UnityObject = UnityEngine.Object;
using GmpoComponent = KusakaFactory.Zatools.Runtime.GlobalMaterialPropertyOverride;

namespace KusakaFactory.Zatools.Ndmf.Pass
{
    internal sealed class GmpoTransforming : Pass<GmpoTransforming>
    {
        public override string QualifiedName => nameof(GmpoTransforming);
        public override string DisplayName => "Override Material Properties";

        protected override void Execute(BuildContext context)
        {
            var compiledOverrides = new List<(Regex, ImmutableList<Gmpo.FixedOverride>)>();

            var virtualControllerContext = context.Extension<VirtualControllerContext>();
            var components = context.AvatarRootObject.GetComponentsInChildren<GmpoComponent>();
            foreach (var component in components)
            {
                var compiled = CompileFor(component);
                if (compiled.Pattern != null) compiledOverrides.Add(compiled);
            }
            UpdateMaterials(context.AvatarRootTransform, virtualControllerContext, compiledOverrides);
        }

        private (Regex Pattern, ImmutableList<Gmpo.FixedOverride> Overrides) CompileFor(GmpoComponent component)
        {
            Regex regex;
            try
            {
                if (string.IsNullOrWhiteSpace(component.ShaderNamePattern)) throw new Exception();
                regex = new Regex(component.ShaderNamePattern);
            }
            catch (Exception)
            {
                ErrorReport.ReportError(new ZatoolsNdmfError(component, ErrorSeverity.NonFatal, "gmpo.report.invalid-regex"));
                UnityObject.DestroyImmediate(component);
                return (null, ImmutableList<Gmpo.FixedOverride>.Empty);
            }

            var overrides = component.Overrides.Select(Gmpo.FixedOverride.Fix).ToImmutableList();
            UnityObject.DestroyImmediate(component);
            return (regex, overrides);
        }

        private void UpdateMaterials(
            Transform root,
            VirtualControllerContext virtualControllerContext,
            IReadOnlyList<(Regex Pattern, ImmutableList<Gmpo.FixedOverride> Overrides)> compiledOverrides
        )
        {
            var materialCache = new Gmpo.MaterialCache();
            var renderers = root.GetComponentsInChildren<Renderer>(true);

            Gmpo.RegisterMaterialsFromRenderers(materialCache, renderers);

            // TODO: アニメーションで変わるマテリアルも拾わなければならない
            var reachableClips = virtualControllerContext
                .GetAllControllers()
                .SelectMany((vc) => vc.AllReachableNodes().Where((n) => n is VirtualClip))
                .Cast<VirtualClip>();
            Debug.LogError($"{reachableClips.Count()} clips reachable");
            foreach (var clip in reachableClips)
            {
                var bindings = clip.GetObjectCurveBindings();
                foreach (var binding in bindings)
                {
                    Debug.LogError($"{clip.Name} : {binding.path}[{binding.propertyName}] / {binding.type.Name}");
                }
            }

            Gmpo.ApplyOverrides(materialCache, compiledOverrides);

            Gmpo.ReplaceOverriddenMaterialsForRenderers(materialCache, renderers);

            foreach ((var original, var updated) in materialCache.EnumerateOverriddenMaterials()) ObjectRegistry.RegisterReplacedObject(original, updated);
        }
    }
}
