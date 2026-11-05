using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using KusakaFactory.Zatools.Ndmf;
using KusakaFactory.Zatools.Ndmf.Core;
using KusakaFactory.Zatools.Runtime;
using UnityObject = UnityEngine.Object;

namespace KusakaFactory.Zatools.Tests.Ndmf.Core
{
    public sealed class ObjectReferenceTests
    {
        private readonly List<UnityObject> _createdObjects = new List<UnityObject>();

        [TearDown]
        public void DestroyCreatedObjects()
        {
            foreach (var obj in _createdObjects) UnityObject.DestroyImmediate(obj);
            _createdObjects.Clear();
        }

        private GameObject CreateAvatar()
        {
            var avatar = new GameObject("Avatar");
            avatar.AddComponent<VRCAvatarDescriptor>();
            _createdObjects.Add(avatar);
            return avatar;
        }

        private GameObject CreateObject(string name, Transform parent = null)
        {
            var obj = new GameObject(name);
            if (parent != null) obj.transform.SetParent(parent, false);
            else _createdObjects.Add(obj);
            return obj;
        }

        private SkinnedMeshRenderer CreateRenderer(string name, Transform parent = null)
        {
            return CreateObject(name, parent).AddComponent<SkinnedMeshRenderer>();
        }

        private static SkinnedMeshRendererReference Reference(SkinnedMeshRenderer target, string path)
        {
            return new SkinnedMeshRendererReference { Target = target, Path = path };
        }

        private static ResolvedReference<SkinnedMeshRenderer> Resolve(
            Component owner,
            SkinnedMeshRendererReference reference,
            SkinnedMeshRenderer legacy = null
        )
        {
            return ObjectReferenceResolver.Resolve(owner, reference, legacy);
        }

        [Test]
        public void EmptyReferenceIsUnset()
        {
            var owner = CreateObject("Owner", CreateAvatar().transform).transform;

            var resolved = Resolve(owner, Reference(null, ""));

            Assert.That(resolved.State, Is.EqualTo(ReferenceState.Unset));
            Assert.That(resolved.Target == null);
        }

        [Test]
        public void LegacyTakesPrecedenceOverReference()
        {
            var avatar = CreateAvatar().transform;
            var owner = CreateObject("Owner", avatar).transform;
            var legacy = CreateRenderer("Legacy", avatar);
            var current = CreateRenderer("Current", avatar);

            var resolved = Resolve(owner, Reference(current, "Current"), legacy);

            Assert.That(resolved.State, Is.EqualTo(ReferenceState.Resolved));
            Assert.That(resolved.Target, Is.EqualTo(legacy));
        }

        [Test]
        public void TargetInsideAvatarTakesPrecedenceOverPath()
        {
            var avatar = CreateAvatar().transform;
            var owner = CreateObject("Owner", avatar).transform;
            var target = CreateRenderer("Target", avatar);
            CreateRenderer("Other", avatar);

            var resolved = Resolve(owner, Reference(target, "Other"));

            Assert.That(resolved.Target, Is.EqualTo(target));
        }

        [Test]
        public void TargetOutsideAvatarFallsBackToPath()
        {
            var firstAvatar = CreateAvatar().transform;
            var secondAvatar = CreateAvatar().transform;
            var owner = CreateObject("Owner", secondAvatar).transform;
            var stale = CreateRenderer("Body", firstAvatar);
            var expected = CreateRenderer("Body", secondAvatar);

            var resolved = Resolve(owner, Reference(stale, "Body"));

            Assert.That(resolved.State, Is.EqualTo(ReferenceState.Resolved));
            Assert.That(resolved.Target, Is.EqualTo(expected));
        }

        [Test]
        public void TargetOutsideAvatarWithoutPathIsKept()
        {
            var owner = CreateObject("Owner", CreateAvatar().transform).transform;
            var target = CreateRenderer("Outside");

            var resolved = Resolve(owner, Reference(target, ""));

            Assert.That(resolved.State, Is.EqualTo(ReferenceState.Resolved));
            Assert.That(resolved.Target, Is.EqualTo(target));
        }

        [Test]
        public void NestedPathIsResolved()
        {
            var avatar = CreateAvatar().transform;
            var owner = CreateObject("Owner", avatar).transform;
            var expected = CreateRenderer("Body", CreateObject("Meshes", avatar).transform);

            var resolved = Resolve(owner, Reference(null, "Meshes/Body"));

            Assert.That(resolved.Target, Is.EqualTo(expected));
        }

        [Test]
        public void MissingPathIsUnresolved()
        {
            var owner = CreateObject("Owner", CreateAvatar().transform).transform;

            var resolved = Resolve(owner, Reference(null, "Missing"));

            Assert.That(resolved.State, Is.EqualTo(ReferenceState.Unresolved));
            Assert.That(resolved.Target == null);
            Assert.That(resolved.Path, Is.EqualTo("Missing"));
        }

        [Test]
        public void PathToObjectWithoutComponentIsUnresolved()
        {
            var avatar = CreateAvatar().transform;
            var owner = CreateObject("Owner", avatar).transform;
            CreateObject("Empty", avatar);

            var resolved = Resolve(owner, Reference(null, "Empty"));

            Assert.That(resolved.State, Is.EqualTo(ReferenceState.Unresolved));
        }

        [Test]
        public void OutsideAvatarOnlyTargetIsUsable()
        {
            var root = CreateObject("Root").transform;
            var owner = CreateObject("Owner", root).transform;
            var target = CreateRenderer("Target", root);

            Assert.That(Resolve(owner, Reference(target, "Target")).Target, Is.EqualTo(target));
            Assert.That(Resolve(owner, Reference(null, "Target")).State, Is.EqualTo(ReferenceState.Unresolved));
        }

        [Test]
        public void ResolveAllCombinesLegacyAndReferences()
        {
            var avatar = CreateAvatar().transform;
            var owner = CreateObject("Owner", avatar).transform;
            var legacy = CreateRenderer("Legacy", avatar);
            var current = CreateRenderer("Current", avatar);

            var resolved = ObjectReferenceResolver.ResolveAll(
                owner,
                new[] { Reference(current, "Current"), Reference(null, ""), Reference(null, "Missing") },
                new SkinnedMeshRenderer[] { legacy, null }
            );

            Assert.That(resolved.Select((r) => r.State), Is.EqualTo(new[]
            {
                ReferenceState.Resolved,
                ReferenceState.Resolved,
                ReferenceState.Unresolved,
            }));
            Assert.That(resolved.Select((r) => r.Target).Take(2), Is.EqualTo(new[] { legacy, current }));
        }

        [Test]
        public void AssignStoresPathInsideAvatar()
        {
            var avatar = CreateAvatar().transform;
            var owner = CreateObject("Owner", avatar).transform;
            var target = CreateRenderer("Body", CreateObject("Meshes", avatar).transform);
            var reference = Reference(null, "");

            ObjectReferenceResolver.Assign(owner, reference, target);

            Assert.That(reference.Target, Is.EqualTo(target));
            Assert.That(reference.Path, Is.EqualTo("Meshes/Body"));
        }

        [Test]
        public void AssignStoresOnlyTargetOutsideAvatar()
        {
            var owner = CreateObject("Owner", CreateAvatar().transform).transform;
            var target = CreateRenderer("Outside");
            var reference = Reference(null, "Stale");

            ObjectReferenceResolver.Assign(owner, reference, target);

            Assert.That(reference.Target, Is.EqualTo(target));
            Assert.That(reference.Path, Is.Empty);
        }

        [Test]
        public void FixerUpdatesPathAfterRename()
        {
            var avatar = CreateAvatar().transform;
            var target = CreateRenderer("Body", avatar);
            var reference = Reference(target, "Body");
            target.gameObject.name = "Renamed";

            var resolved = ZatoolsObjectReferenceFixer.FindRepairTarget(avatar, reference);

            Assert.That(ZatoolsObjectReferenceFixer.NeedsRepair(avatar, reference, resolved), Is.True);
            ZatoolsObjectReferenceFixer.Repair(avatar, reference, resolved);
            Assert.That(reference.Path, Is.EqualTo("Renamed"));
            Assert.That(ZatoolsObjectReferenceFixer.NeedsRepair(avatar, reference, resolved), Is.False);
        }

        [Test]
        public void FixerFillsTargetForPathOnlyReference()
        {
            var avatar = CreateAvatar().transform;
            var target = CreateRenderer("Body", avatar);
            var reference = Reference(null, "Body");

            var resolved = ZatoolsObjectReferenceFixer.FindRepairTarget(avatar, reference);

            Assert.That(ZatoolsObjectReferenceFixer.NeedsRepair(avatar, reference, resolved), Is.True);
            ZatoolsObjectReferenceFixer.Repair(avatar, reference, resolved);
            Assert.That(reference.Target, Is.EqualTo(target));
        }

        [Test]
        public void FixerIgnoresReferenceWithoutPath()
        {
            var avatar = CreateAvatar().transform;
            var target = CreateRenderer("Body", avatar);

            Assert.That(ZatoolsObjectReferenceFixer.FindRepairTarget(avatar, Reference(target, "")) == null);
        }

        [Test]
        public void FixerIgnoresUnresolvedReference()
        {
            var avatar = CreateAvatar().transform;

            Assert.That(ZatoolsObjectReferenceFixer.FindRepairTarget(avatar, Reference(null, "Missing")) == null);
        }
    }
}
