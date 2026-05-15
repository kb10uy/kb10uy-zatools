using System.Linq;
using UnityEngine;
using UnityEditor;

namespace KusakaFactory.Zatools.EditorExtension
{
    internal static class GameObjectActions
    {
        private const string GOA_MENU_PREFIX = "GameObject/Zatools: kb10uy's Various Tools/";

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
    }
}
