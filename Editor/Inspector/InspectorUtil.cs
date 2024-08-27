using UnityEngine.UIElements;
using UnityEditor;

namespace KusakaFactory.Zatools.Inspector
{
    internal static class InspectorUtil
    {
        private readonly static string UxmlRoot = "Packages/org.kb10uy.zatools/Editor/Inspector";

        internal static VisualTreeAsset LoadInspectorVisualTree(string fileStem)
        {
            return AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{UxmlRoot}/{fileStem}.uxml");
        }
    }
}
