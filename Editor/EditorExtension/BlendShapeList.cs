using System.Linq;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using KusakaFactory.Zatools.Localization;

namespace KusakaFactory.Zatools.EditorExtension
{
    internal sealed class BlendShapeList : EditorWindow
    {
        [MenuItem("Window/Zatools: kb10uy's Various Tools/BlendShape List")]
        internal static void OpenWindow()
        {
            GetWindow<BlendShapeList>();
        }

        internal void CreateGUI()
        {
            var visualTree = ZatoolsResources.LoadVisualTreeByGuid("1fc5e174481e55c469b0104ab3b7dc2c");
            visualTree.CloneTree(rootVisualElement);
            ZatoolsLocalization.UILocalizer.ApplyLocalizationFor(rootVisualElement);

            var rendererField = rootVisualElement.Q<ObjectField>("FieldSkinnedMeshRenderer");
            var namesField = rootVisualElement.Q<TextField>("FieldNamesText");
            rendererField.RegisterValueChangedCallback((e) => namesField.value = OnUpdateRenderer(e.newValue as SkinnedMeshRenderer));
            namesField.SetVerticalScrollerVisibility(ScrollerVisibility.Auto);

            var saveButton = rootVisualElement.Q<Button>("ButtonSave");
            saveButton.clicked += () => SaveAsFile(rendererField.value as SkinnedMeshRenderer);
        }

        private static void SaveAsFile(SkinnedMeshRenderer renderer)
        {
            if (renderer == null) return;

            var pathToSave = EditorUtility.SaveFilePanel(
                "Save BlendShapes to File",
                "",
                $"{renderer.gameObject.name}-BlendShapes.txt",
                "txt"
            );
            if (pathToSave.Length == 0) return;

            File.WriteAllText(pathToSave, OnUpdateRenderer(renderer), new UTF8Encoding(false));
        }

        private static string OnUpdateRenderer(SkinnedMeshRenderer renderer)
        {
            if (renderer == null) return "";
            return string.Join(
                '\n',
                Enumerable.Range(0, renderer.sharedMesh.blendShapeCount).Select((i) => renderer.sharedMesh.GetBlendShapeName(i))
            );
        }
    }
}
