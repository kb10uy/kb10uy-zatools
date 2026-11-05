using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using nadena.dev.ndmf.runtime;
using KusakaFactory.Zatools.Localization;
using KusakaFactory.Zatools.Ndmf.Core;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Ndmf.Inspector
{
    internal static class ZatoolsObjectReferenceField
    {
        internal static void Attach<T>(ObjectField field, SerializedProperty reference) where T : Component
        {
            var serializedObject = reference.serializedObject;
            var owner = (Component)serializedObject.targetObject;
            var path = reference.FindPropertyRelative(nameof(ZatoolsObjectReference.Path));
            var target = reference.FindPropertyRelative(nameof(ZatoolsObjectReference<T>.Target));

            var unresolvedPath = new Label();
            unresolvedPath.AddToClassList("zatools-object-reference__path");
            var clearButton = new Button(() =>
            {
                path.stringValue = "";
                target.objectReferenceValue = null;
                serializedObject.ApplyModifiedProperties();
            })
            {
                text = "×",
            };
            var unresolved = new VisualElement();
            unresolved.AddToClassList("zatools-object-reference__unresolved");
            unresolved.Add(unresolvedPath);
            unresolved.Add(clearButton);
            field.parent.Insert(field.parent.IndexOf(field) + 1, unresolved);

            void Refresh()
            {
                if (owner == null) return;
                var avatarRoot = RuntimeUtil.FindAvatarInParents(owner.transform);
                var (state, _) = ObjectReferenceResolver.ResolveUntyped(
                    avatarRoot,
                    target.objectReferenceValue as Component,
                    path.stringValue,
                    typeof(T)
                );
                unresolved.style.display = state == ReferenceState.Unresolved ? DisplayStyle.Flex : DisplayStyle.None;
                unresolvedPath.text = ZatoolsLocalization.FormatLocalized("reference.inspector.unresolved", path.stringValue);
            }

            field.RegisterCallback<ChangeEvent<UnityEngine.Object>>((e) =>
            {
                if (e.newValue == target.objectReferenceValue) return;
                path.stringValue = ObjectReferenceResolver.PathFor(owner, e.newValue as Component);
                serializedObject.ApplyModifiedProperties();
            }, TrickleDown.TrickleDown);
            unresolved.TrackPropertyValue(path, (_) => Refresh());
            unresolved.TrackPropertyValue(target, (_) => Refresh());
            Refresh();
        }

        internal static void AttachLegacy(
            VisualElement referenceSection,
            VisualElement legacySection,
            Button convertButton,
            SerializedProperty legacy,
            Action convert
        )
        {
            void Refresh()
            {
                var hasLegacy = legacy.isArray ? legacy.arraySize > 0 : legacy.objectReferenceValue != null;
                referenceSection.style.display = hasLegacy ? DisplayStyle.None : DisplayStyle.Flex;
                legacySection.style.display = hasLegacy ? DisplayStyle.Flex : DisplayStyle.None;
            }

            convertButton.clicked += () =>
            {
                var owner = (Component)legacy.serializedObject.targetObject;
                Undo.RecordObject(owner, ZatoolsLocalization.NdmfLocalizer.GetLocalizedString("reference.inspector.convert"));
                convert();
                PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
                legacy.serializedObject.Update();
                Refresh();
            };
            legacySection.TrackSerializedObjectValue(legacy.serializedObject, (_) => Refresh());
            Refresh();
        }
    }

    internal abstract class ZatoolsObjectReferenceDrawer<T> : PropertyDrawer where T : Component
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();
            var field = new ObjectField(property.displayName) { objectType = typeof(T), allowSceneObjects = true };
            container.Add(field);
            field.BindProperty(property.FindPropertyRelative(nameof(ZatoolsObjectReference<T>.Target)));
            ZatoolsObjectReferenceField.Attach<T>(field, property.Copy());
            return container;
        }
    }

    [CustomPropertyDrawer(typeof(SkinnedMeshRendererReference))]
    internal sealed class SkinnedMeshRendererReferenceDrawer : ZatoolsObjectReferenceDrawer<SkinnedMeshRenderer>
    {
    }
}
