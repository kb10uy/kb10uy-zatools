using System.Linq;
using UnityEngine;
using UnityEditor;
using nadena.dev.modular_avatar.core;

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

            Undo.RecordObject(target, "Add MA Material Swap for Depth Wrapper");
            var materialSwap = target.AddComponent<ModularAvatarMaterialSwap>();
            materialSwap.Swaps.Add(new MatSwap
            {
                From = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(WrapperMaterialGuid)),
                To = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(DisabledWrapperMaterialGuid)),
            });
            if (!EditorUtility.IsPersistent(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        [MenuItem(GOA_MENU_PREFIX + "Add MA Material Swap for Depth Wrapper", true, 30)]
        private static bool AddDepthWrapperMaterialSwapCheck()
        {
            return Selection.gameObjects.Length == 1;
        }
    }
}
