using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Ndmf.Core
{
    internal static class Gmpo
    {
        internal static void RegisterMaterialsFromRenderers(MaterialCache materialCache, IEnumerable<Renderer> renderers)
        {
            var sharedMaterials = new List<Material>();
            foreach (var renderer in renderers)
            {
                renderer.GetSharedMaterials(sharedMaterials);
                foreach (var material in sharedMaterials) materialCache.RegisterOriginal(material);
            }
        }

        internal static void ReplaceOverriddenMaterialsForRenderers(MaterialCache materialCache, IEnumerable<Renderer> renderers)
        {
            var sharedMaterials = new List<Material>();
            foreach (var renderer in renderers)
            {
                renderer.GetSharedMaterials(sharedMaterials);
                for (var i = 0; i < sharedMaterials.Count; ++i) sharedMaterials[i] = materialCache.GetResult(sharedMaterials[i]);
                renderer.SetSharedMaterials(sharedMaterials);
            }
        }

        internal static void ApplyOverrides(MaterialCache materialCache, IReadOnlyList<(Regex Pattern, ImmutableList<FixedOverride> Overrides)> compiledOverrides)
        {
            foreach (var original in materialCache.CurrentOriginalMaterials())
            {
                var shaderName = original.shader.name;
                var matchedOverrides = compiledOverrides.Where((p) => p.Pattern.IsMatch(shaderName));
                if (!matchedOverrides.Any()) continue;

                var overridden = materialCache.GetModifiable(original);

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
        }

        internal sealed class MaterialCache
        {
            private Dictionary<Material, Material> _cache = new Dictionary<Material, Material>();

            internal IEnumerable<(Material Original, Material Overridden)> EnumerateOverriddenMaterials() =>
                _cache.Where((kvp) => kvp.Value != null).Select((kvp) => (kvp.Key, kvp.Value));

            internal ImmutableList<Material> CurrentOriginalMaterials() => _cache.Keys.ToImmutableList();

            internal void RegisterOriginal(Material material)
            {
                if (material == null) return;
                if (_cache.ContainsKey(material)) return;
                _cache.Add(material, null);
            }

            internal Material GetModifiable(Material original)
            {
                if (original == null || !_cache.ContainsKey(original)) return null;

                Material overridden;
                if ((overridden = _cache[original]) == null)
                {
                    _cache[original] = overridden = Object.Instantiate(original);
                }
                return overridden;
            }

            internal Material GetResult(Material original)
            {
                if (original == null) return null;
                if (!_cache.ContainsKey(original) || _cache[original] == null) return original;
                return _cache[original];
            }
        }

        internal struct FixedOverride
        {
            internal string Name;
            internal MaterialPropertyOverrideType TargetType;
            internal float FloatValue;
            internal int IntValue;
            internal Vector4 VectorValue;

            internal static FixedOverride Fix(MaterialPropertyOverride propertyOverride)
            {
                return new FixedOverride
                {
                    Name = propertyOverride.Name,
                    TargetType = propertyOverride.TargetType,
                    FloatValue = propertyOverride.FloatValue,
                    IntValue = propertyOverride.IntValue,
                    VectorValue = propertyOverride.VectorValue,
                };
            }
        }
    }
}