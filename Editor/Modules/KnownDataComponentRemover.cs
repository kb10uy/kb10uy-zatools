using System;
using System.Linq;
using UnityEngine;
using nadena.dev.ndmf;

namespace KusakaFactory.Zatools.Modules
{
    internal sealed class KnownDataComponentRemover : Pass<KnownDataComponentRemover>
    {
        public override string QualifiedName => nameof(KnownDataComponentRemover);
        public override string DisplayName => "Remove known data components";

        protected override void Execute(BuildContext context)
        {
            var typeNames = LoadComponentNames();
            var types = LoadTypes(typeNames);
            Debug.Log($"KnownDataComponentRemover: {typeNames.Length} type(s) defined, {types.Length} type(s) found");

            foreach (var componentType in types)
            {
                var components = context.AvatarRootObject.GetComponentsInChildren(componentType);
                foreach (var component in components) UnityEngine.Object.DestroyImmediate(component);
                Debug.Log($"KnownDataComponentRemover: removed {componentType.FullName}");
            }
        }

        private static string[] LoadComponentNames()
        {
            return Resources.LoadTextAsset("KnownDataComponents.txt")
                .Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select((l) => l.Trim())
                .Where((l) => !l.StartsWith("#"))
                .ToArray();
        }

        private static Type[] LoadTypes(string[] typeNames)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            return typeNames
                .Select((tn) => assemblies.Select((a) => a.GetType(tn, false)).FirstOrDefault((t) => t != null))
                .ToArray();
        }
    }
}
