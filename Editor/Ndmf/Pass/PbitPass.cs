using System.Collections.Immutable;
using System.Linq;
using nadena.dev.ndmf;
using UnityObject = UnityEngine.Object;
using PbitComponent = KusakaFactory.Zatools.Runtime.PBIgnoredTransform;

#if ZATOOLS_HAS_VRCSDK
using VRC.SDK3.Dynamics.PhysBone.Components;
#endif

namespace KusakaFactory.Zatools.Ndmf.Pass
{
    internal sealed class PbitTransforming : ZatoolsPass<PbitTransforming>
    {
        protected override string ZatoolsPassName => nameof(PbitTransforming);

        protected override string ZatoolsPassDescription => "Ignore themselves from PhysBones";

        protected override void Execute(BuildContext context)
        {
            var avatarRoot = context.AvatarRootTransform;

            var components = avatarRoot.GetComponentsInChildren<PbitComponent>();
            if (components.Length == 0) return;

#if ZATOOLS_HAS_VRCSDK
            var effectivePBMap = avatarRoot.GetComponentsInChildren<VRCPhysBone>(true)
                .Select((pb) => (Transform: pb.GetRootTransform(), Component: pb))
                .GroupBy((p) => p.Transform)
                .ToImmutableDictionary((g) => g.Key, (g) => g.Select((p) => p.Component).ToImmutableList());

            foreach (var component in components)
            {
                var pbTarget = component.transform.parent;
                var effectivePBs = ImmutableList<VRCPhysBone>.Empty;
                while (pbTarget != null && pbTarget != avatarRoot)
                {
                    if (effectivePBMap.TryGetValue(pbTarget, out effectivePBs)) break;
                    pbTarget = pbTarget.parent;
                }

                foreach (var effectivePB in effectivePBs)
                {
                    effectivePB.ignoreTransforms.Add(component.transform);
                }
                UnityObject.DestroyImmediate(component);
            }
#else
            foreach (var component in components) UnityObject.DestroyImmediate(component);
#endif
        }
    }
}
