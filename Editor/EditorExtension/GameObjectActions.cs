using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using KusakaFactory.Zatools.Foundation;
using KusakaFactory.Zatools.Ndmf.Preview;

namespace KusakaFactory.Zatools.EditorExtension
{
    internal static class GameObjectActions
    {
        private const string GOA_MENU_PREFIX = "GameObject/Zatools: kb10uy's Various Tools/";
        private const string GENERATED_MESH_PATH_PREFIX = "Assets/";

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

        [MenuItem(GOA_MENU_PREFIX + "Generate Convex Hull Renderer", false, 31)]
        private static void GenerateConvexHullRenderer()
        {
            var targets = Selection.gameObjects
                .Select((go) => go.GetComponent<SkinnedMeshRenderer>())
                .Where((smr) => smr != null && smr.sharedMesh != null)
                .Distinct()
                .ToArray();

            var materialPath = AssetDatabase.GUIDToAssetPath(EdwRenderFilterNode.WrapperPreviewMaterialGuid);
            var previewMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (previewMaterial == null)
            {
                Debug.LogError("Failed to load EyeholeDepthWrapper preview material.");
                return;
            }

            foreach (var target in targets)
            {
                var hullMesh = BuildConvexHullMesh(target.sharedMesh);
                if (hullMesh == null)
                {
                    Debug.LogWarning($"Convex hull generation skipped: {target.name}");
                    continue;
                }

                var meshAssetPath = AssetDatabase.GenerateUniqueAssetPath($"{GENERATED_MESH_PATH_PREFIX}{target.sharedMesh.name}_ConvexHull.asset");
                AssetDatabase.CreateAsset(hullMesh, meshAssetPath);

                var hullObject = new GameObject($"{target.name}_ConvexHull");
                Undo.RegisterCreatedObjectUndo(hullObject, "Create Convex Hull Renderer");
                Undo.SetTransformParent(hullObject.transform, target.transform, "Create Convex Hull Renderer");
                hullObject.transform.localPosition = Vector3.zero;
                hullObject.transform.localRotation = Quaternion.identity;
                hullObject.transform.localScale = Vector3.one;

                var hullRenderer = Undo.AddComponent<SkinnedMeshRenderer>(hullObject);
                hullRenderer.sharedMesh = hullMesh;
                hullRenderer.rootBone = target.rootBone;
                hullRenderer.bones = target.bones;
                hullRenderer.sharedMaterial = previewMaterial;
                hullRenderer.updateWhenOffscreen = target.updateWhenOffscreen;

                if (!EditorUtility.IsPersistent(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                if (!EditorUtility.IsPersistent(hullObject)) PrefabUtility.RecordPrefabInstancePropertyModifications(hullObject);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem(GOA_MENU_PREFIX + "Generate Convex Hull Renderer", true, 31)]
        private static bool GenerateConvexHullRendererCheck()
        {
            return Selection.gameObjects.Any((go) =>
            {
                var smr = go.GetComponent<SkinnedMeshRenderer>();
                return smr != null && smr.sharedMesh != null;
            });
        }

        private static Mesh BuildConvexHullMesh(Mesh source)
        {
            var vertices = source.vertices;
            if (vertices == null || vertices.Length < 4) return null;

            var sourceBoneWeights = source.boneWeights;
            var hasBoneWeights = sourceBoneWeights != null && sourceBoneWeights.Length == vertices.Length;

            var hullTriangles = ConvexHull.ComputeQuickHull3D(vertices);
            if (hullTriangles.Length < 12) return null;

            var vertexMap = new Dictionary<int, int>();
            var remappedVertices = new List<Vector3>();
            var remappedBoneWeights = hasBoneWeights ? new List<BoneWeight>() : null;
            var remappedTriangles = new int[hullTriangles.Length];

            for (int i = 0; i < hullTriangles.Length; i++)
            {
                var originalIndex = hullTriangles[i];
                if (!vertexMap.TryGetValue(originalIndex, out var newIndex))
                {
                    newIndex = remappedVertices.Count;
                    vertexMap.Add(originalIndex, newIndex);
                    remappedVertices.Add(vertices[originalIndex]);
                    if (hasBoneWeights) remappedBoneWeights.Add(sourceBoneWeights[originalIndex]);
                }
                remappedTriangles[i] = newIndex;
            }

            var hullMesh = new Mesh
            {
                name = $"{source.name}_ConvexHull"
            };
            hullMesh.SetVertices(remappedVertices);
            hullMesh.SetTriangles(remappedTriangles, 0);
            if (hasBoneWeights)
            {
                hullMesh.boneWeights = remappedBoneWeights.ToArray();
                hullMesh.bindposes = source.bindposes;
            }
            hullMesh.RecalculateNormals();
            hullMesh.RecalculateBounds();
            return hullMesh;
        }
    }
}
