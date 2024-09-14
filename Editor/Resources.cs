using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using System.Collections.Generic;

namespace KusakaFactory.Zatools
{
    internal static class Resources
    {
        private readonly static Dictionary<string, VisualTreeAsset> VisualTreeAssetCache = new Dictionary<string, VisualTreeAsset>();

        [MenuItem("Tools/kb10uy's Various Tools/Reload VisualTree Assets")]
        internal static void InvalidateVisualTreeAssetCache()
        {
            VisualTreeAssetCache.Clear();
        }

        internal static VisualTreeAsset LoadVisualTreeByGuid(string guid)
        {
            if (VisualTreeAssetCache.TryGetValue(guid, out var vta)) return vta;

            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (assetPath == "") return null;
            vta = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(assetPath);
            VisualTreeAssetCache.Add(guid, vta);
            return vta;
        }

        internal static string LoadTextAssetByGuid(string guid)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            return textAsset != null ? textAsset.text : null;
        }

        internal static ComputeShader LoadComputeShaderByGuid(string guid)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(assetPath);
            return shader;
        }
    }
}
