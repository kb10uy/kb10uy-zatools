using UnityEngine;
using UnityEditor;

namespace KusakaFactory.Zatools.EditorExtension
{
    internal static class AnimationAnalysis
    {
        [MenuItem("Assets/kb10uy's Various Tools/List property paths on log", validate = true)]
        private static bool CanListPropertyPaths() => Selection.activeObject is AnimationClip;

        [MenuItem("Assets/kb10uy's Various Tools/List property paths on log")]
        private static void ListPropertyPaths()
        {
            var animationClip = Selection.activeObject as AnimationClip;
            if (animationClip == null) return;

            foreach (var curveBinding in AnimationUtility.GetCurveBindings(animationClip))
            {
                Debug.Log(curveBinding.path);
            }

            foreach (var objectReferenceCurveBinding in AnimationUtility.GetObjectReferenceCurveBindings(animationClip))
            {
                Debug.Log(objectReferenceCurveBinding.path);
            }
        }
    }
}
