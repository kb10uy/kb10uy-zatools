using System.Linq;
using UnityEngine;
using UnityEditor;
using VRC.SDK3.Avatars.ScriptableObjects;
using nadena.dev.modular_avatar.core;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.EditorExtension
{
    internal static class GameObjectActions
    {
        private const string GOA_MENU_PREFIX = "GameObject/Zatools: kb10uy's Various Tools/";
        internal static readonly string WrapperMaterialGuid = "c40c829946d3b494d807a73fd79af5c5";
        internal static readonly string DisabledWrapperMaterialGuid = "f82d4e77c1b79ed44820a78807676cd0";


        [MenuItem(GOA_MENU_PREFIX + "Disable EditorOnly GameObjects", false, 30)]
        private static void DisableEditorOnlyObjects()
        {
            var targets = Selection.gameObjects
                .SelectMany((sel) => sel.GetComponentsInChildren<Transform>().Where((t) => t.CompareTag("EditorOnly")))
                .Select((t) => t.gameObject)
                .ToArray();

            Undo.RecordObjects(targets, "Disable EditorOnly GameObjects");
            foreach (var target in targets)
            {
                target.SetActive(false);
                if (!EditorUtility.IsPersistent(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }
        }

        [MenuItem(GOA_MENU_PREFIX + "Disable EditorOnly GameObjects", true, 30)]
        private static bool DisableEditorOnlyObjectsCheck()
        {
            return Selection.gameObjects.Length > 0;
        }

        [MenuItem(GOA_MENU_PREFIX + "Add MA Material Swap for Depth Wrapper", false, 30)]
        private static void AddDepthWrapperMaterialSwap()
        {
            if (Selection.gameObjects.Length != 1) return;
            var target = Selection.activeGameObject;

            var materialSwap = target.AddComponent<ModularAvatarMaterialSwap>();
            materialSwap.Swaps.Add(new MatSwap
            {
                From = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(WrapperMaterialGuid)),
                To = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(DisabledWrapperMaterialGuid)),
            });
            Undo.RegisterCreatedObjectUndo(materialSwap, "Setup Inverted Convex Depth Wrapper");
            if (!EditorUtility.IsPersistent(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        [MenuItem(GOA_MENU_PREFIX + "Add MA Material Swap for Depth Wrapper", true, 30)]
        private static bool AddDepthWrapperMaterialSwapCheck()
        {
            return Selection.gameObjects.Length == 1;
        }

        [MenuItem(GOA_MENU_PREFIX + "(Advanced) Inverted Convex Depth Wrapper Setup with Toggle", false, 30)]
        private static void InvertedConvexDepthWrapperSetupWithToggle()
        {
            if (Selection.gameObjects.Length != 1) return;
            var target = Selection.activeGameObject;
            if (target.GetComponent<SkinnedMeshRenderer>() == null) return;

            var wrapperMaterial = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(WrapperMaterialGuid));
            var disabledWrapperMaterial = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(DisabledWrapperMaterialGuid));

            var cdw = target.AddComponent<ConvexDepthWrapper>();
            if (cdw == null) return;
            cdw.MaterialOverride = disabledWrapperMaterial;
            Undo.RegisterCreatedObjectUndo(cdw, "Setup Inverted Convex Depth Wrapper");
            if (!EditorUtility.IsPersistent(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);

            var toggleObject = new GameObject("DepthWrapperToggle");
            toggleObject.transform.parent = target.transform;
            toggleObject.AddComponent<ModularAvatarMenuInstaller>();
            var menuItem = toggleObject.AddComponent<ModularAvatarMenuItem>();
            menuItem.Control = new VRCExpressionsMenu.Control { type = VRCExpressionsMenu.Control.ControlType.Toggle };
            menuItem.isSaved = false;
            menuItem.automaticValue = true;
            menuItem.label = "Fix Depth";
            var materialSwap = toggleObject.AddComponent<ModularAvatarMaterialSwap>();
            materialSwap.Swaps.Add(new MatSwap
            {
                From = disabledWrapperMaterial,
                To = wrapperMaterial,
            });
            Undo.RegisterCreatedObjectUndo(toggleObject, "Setup Inverted Convex Depth Wrapper");
            PrefabUtility.RecordPrefabInstancePropertyModifications(toggleObject);
        }

        [MenuItem(GOA_MENU_PREFIX + "(Advanced) Inverted Convex Depth Wrapper Setup with Toggle", true, 30)]
        private static bool InvertedConvexDepthWrapperSetupWithToggleCheck()
        {
            if (Selection.gameObjects.Length != 1) return false;
            var target = Selection.activeGameObject;
            return target.GetComponent<SkinnedMeshRenderer>() != null;
        }
    }
}
