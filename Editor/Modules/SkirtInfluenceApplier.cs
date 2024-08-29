#if KZT_NDMF

using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Constraint.Components;
using nadena.dev.ndmf;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Modules
{
    internal sealed class BoneArrayRotationInfluenceApplier : Pass<BoneArrayRotationInfluenceApplier>
    {
        public override string QualifiedName => nameof(BoneArrayRotationInfluenceApplier);
        public override string DisplayName => "Apply skirt PhysBone influence";

        protected override void Execute(BuildContext context)
        {
            var components = context.AvatarRootObject.GetComponentsInChildren<BoneArrayRotationInfluence>();
            foreach (var component in components) Apply(context, component);
        }

        private void Apply(BuildContext context, BoneArrayRotationInfluence influenceSettings)
        {
            var chainsCount = influenceSettings.ChainRoots.Length;
            var virtualStart = influenceSettings.CloseLoop ? chainsCount : 0;
            var virtualEnd = influenceSettings.CloseLoop ? virtualStart + chainsCount + 1 : virtualStart + chainsCount;

            for (int i = virtualStart; i < virtualStart + chainsCount; ++i)
            {
                var center = influenceSettings.ChainRoots[i % chainsCount];

                // Rotation Constraint 用の親を挿入
                var fakeParent = new GameObject($"{center.Root.gameObject.name}_Parent");
                var parentTransform = fakeParent.transform;
                parentTransform.parent = center.Root;
                parentTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                parentTransform.localScale = Vector3.one;
                parentTransform.localPosition -= Vector3.up * influenceSettings.ParentOffsetDistance;
                parentTransform.SetParent(center.Root.parent, true);
                center.Root.transform.SetParent(parentTransform, true);

                // 影響元ボーン
                var hasPrev = i - 1 >= 0;
                var hasNext = i + 1 < virtualEnd;
                var prev = hasPrev ? influenceSettings.ChainRoots[(i - 1) % chainsCount].Root : parentTransform;
                var next = hasNext ? influenceSettings.ChainRoots[(i + 1) % chainsCount].Root : parentTransform;

                // RotationOffset の計算
                var middleQuaternion = Quaternion.Slerp(prev.rotation, next.rotation, 0.5f);
                var rotationDiff = Quaternion.Inverse(middleQuaternion) * parentTransform.rotation;

                // RotationConstraint の設定
                var rotationConstraint = fakeParent.AddComponent<VRCRotationConstraint>();
                rotationConstraint.RotationAtRest = parentTransform.localEulerAngles;
                rotationConstraint.RotationOffset = rotationDiff.eulerAngles;
                if (hasPrev) rotationConstraint.Sources.Add(new VRCConstraintSource(prev, 0.5f, Vector3.zero, Vector3.zero));
                if (hasNext) rotationConstraint.Sources.Add(new VRCConstraintSource(next, 0.5f, Vector3.zero, Vector3.zero));
                rotationConstraint.GlobalWeight = center.Influence;
                rotationConstraint.IsActive = true;
            }

            Object.DestroyImmediate(influenceSettings);
        }
    }
}

#endif
