using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace KusakaFactory.Zatools
{
    internal static class Resources
    {
        private readonly static string InspectorRoot = "Packages/org.kb10uy.zatools/Editor/Inspector";
        private readonly static string EditorExtensionRoot = "Packages/org.kb10uy.zatools/Editor/EditorExtension";
        private readonly static string ResourceRoot = "Packages/org.kb10uy.zatools/Resources";

        internal static VisualTreeAsset LoadInspectorVisualTree(string fileStem)
        {
            return AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{InspectorRoot}/{fileStem}");
        }

        internal static VisualTreeAsset LoadEditorExtensionVisualTree(string fileStem)
        {
            return AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{EditorExtensionRoot}/{fileStem}");
        }

        internal static string LoadTextAsset(string relativePath)
        {
            var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>($"{ResourceRoot}/{relativePath}");
            return textAsset.text;
        }
    }
}
