using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using nadena.dev.ndmf;
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

            var components = context.AvatarRootObject.GetComponentsInChildren<GmpoComponent>();
            foreach (var component in components)
            {
                var compiled = CompileFor(component);
                if (compiled.Pattern != null) compiledOverrides.Add(compiled);
            }
            UpdateMaterials(context.AvatarRootTransform, compiledOverrides);
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

        private void UpdateMaterials(Transform root, IReadOnlyList<(Regex Pattern, ImmutableList<Gmpo.FixedOverride> Overrides)> compiledOverrides)
        {
            // TODO: アニメーションで変わるマテリアルも拾わなければならない
            var overriddenMaterials = new Dictionary<Material, Material>();
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                var sharedMaterials = new List<Material>();
                renderer.GetSharedMaterials(sharedMaterials);

                foreach (var material in sharedMaterials)
                {
                    if (material == null) continue;
                    if (overriddenMaterials.ContainsKey(material)) continue;
                    overriddenMaterials.Add(material, null);
                }
            }

            var originalMaterials = overriddenMaterials.Keys.ToImmutableList();
            foreach (var original in originalMaterials)
            {
                var shaderName = original.shader.name;
                var matchedOverrides = compiledOverrides.Where((p) => p.Pattern.IsMatch(shaderName));
                if (!matchedOverrides.Any()) continue;

                Material overridden;
                if ((overridden = overriddenMaterials[original]) == null)
                {
                    overriddenMaterials[original] = overridden = UnityObject.Instantiate(original);
                }

                var floatNames = original.GetPropertyNames(MaterialPropertyType.Float);
                var intNames = original.GetPropertyNames(MaterialPropertyType.Int);
                var vectorNames = original.GetPropertyNames(MaterialPropertyType.Vector);
                foreach (var fixedOverride in matchedOverrides.SelectMany((p) => p.Overrides))
                {
                    // TODO: Shader.PropertyToID を使う
                    switch (fixedOverride.TargetType)
                    {
                        case MaterialPropertyOverrideType.Float:
                            if (!floatNames.Contains(fixedOverride.Name)) continue;
                            overridden.SetFloat(fixedOverride.Name, fixedOverride.FloatValue);
                            Debug.LogError($"Override {fixedOverride.Name} applied ({fixedOverride.FloatValue}) for {overridden.name}");
                            break;
                        case MaterialPropertyOverrideType.Int:
                            if (!intNames.Contains(fixedOverride.Name)) continue;
                            overridden.SetInteger(fixedOverride.Name, fixedOverride.IntValue);
                            break;
                        case MaterialPropertyOverrideType.Vector:
                            if (!vectorNames.Contains(fixedOverride.Name)) continue;
                            overridden.SetVector(fixedOverride.Name, fixedOverride.VectorValue);
                            break;
                    }
                }
            }

            foreach (var renderer in renderers)
            {
                var sharedMaterials = new List<Material>();
                renderer.GetSharedMaterials(sharedMaterials);

                for (var i = 0; i < sharedMaterials.Count; ++i)
                {
                    var originalMaterial = sharedMaterials[i];
                    if (originalMaterial == null) continue;
                    var updatedMaterial = overriddenMaterials[originalMaterial];
                    if (updatedMaterial == null) continue;
                    sharedMaterials[i] = updatedMaterial;
                }

                renderer.SetSharedMaterials(sharedMaterials);
            }

            foreach ((var original, var updated) in overriddenMaterials)
            {
                if (updated == null) continue;
                ObjectRegistry.RegisterReplacedObject(original, updated);
            }
        }
    }
}
