using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using KusakaFactory.Zatools.Localization;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Ndmf.Inspector
{
    [CustomEditor(typeof(AdHocBlendShapeMix))]
    internal sealed class AhbsmInspector : ZatoolsInspector
    {
        protected override VisualElement CreateInspectorGUIImpl()
        {
            var blendShapeNames = FetchBlendShapeNames();
            var visualTree = ZatoolsResources.LoadVisualTreeByGuid("3ae61e75b4b01d348968e3115c250141");
            var visualTreeItem = ZatoolsResources.LoadVisualTreeByGuid("93fb3d754041c7242b94a55a4aea9bae");

            var inspector = visualTree.CloneTree();
            ZatoolsLocalization.UILocalizer.ApplyLocalizationFor(inspector);
            inspector.Bind(serializedObject);

            var definitionsList = inspector.Q<ListView>("FieldMixDefinitions");
            definitionsList.makeItem = () => MakeDefinitionItem(visualTreeItem, blendShapeNames);

            var saveButton = inspector.Q<Button>("ButtonSaveToJson");
            var loadButton = inspector.Q<Button>("ButtonLoadFromJson");
            saveButton.clicked += SaveToJson;
            loadButton.clicked += LoadFromJson;

            var fromRegexField = inspector.Q<TextField>("FieldFromRegex");
            var ToRegexField = inspector.Q<TextField>("FieldToRegex");
            var appendRegexButton = inspector.Q<Button>("ButtonAppendWithRegex");
            appendRegexButton.clicked += () => AppendWithRegex(
                blendShapeNames,
                FetchBlendShapeRelativeWeights(),
                fromRegexField.value,
                ToRegexField.value
            );

            return inspector;
        }

        private void SaveToJson()
        {
            var component = target as Runtime.AdHocBlendShapeMix;
            var pathToSave = EditorUtility.SaveFilePanel(
                "Save Mix Definitions as JSON",
                "",
                $"{component.gameObject.name}-BlendShapeMix.json",
                "json"
            );
            if (pathToSave.Length == 0) return;

            var json = JsonConvert.SerializeObject(component.MixDefinitions, Formatting.Indented);
            File.WriteAllText(pathToSave, json, new UTF8Encoding(false));
        }

        private void LoadFromJson()
        {
            var pathToLoad = EditorUtility.OpenFilePanel("Load Mix Definitions from JSON", "", "json");
            if (pathToLoad.Length == 0) return;

            var jsonText = File.ReadAllText(pathToLoad, new UTF8Encoding(false));
            var loadedArray = JsonConvert.DeserializeObject<BlendShapeMixDefinition[]>(jsonText);

            // defs に objectReference を設定すると undo が効かない
            var definitions = serializedObject.FindProperty(nameof(Runtime.AdHocBlendShapeMix.MixDefinitions));
            serializedObject.Update();
            definitions.ClearArray();
            AppendDefinitionItems(loadedArray);
            serializedObject.ApplyModifiedProperties();
        }

        private void AppendWithRegex(IList<string> names, IList<float> weights, string fromRegex, string toRegex)
        {
            if (fromRegex == "" || toRegex == "") return;

            var fromPattern = new Regex(fromRegex);
            var toPattern = new Regex(toRegex);

            var sources = names
                .Select((n, i) => (Name: n, Index: i))
                .Where((p) => fromPattern.IsMatch(p.Name) && weights[p.Index] > Mathf.Epsilon)
                .Select((p) => (From: p.Name, Weight: -weights[p.Index]))
                .ToArray();

            var definitions = names
                .Where((n) => toPattern.IsMatch(n))
                .SelectMany((n) => sources.Select((sp) => new BlendShapeMixDefinition
                {
                    FromBlendShape = sp.From,
                    ToBlendShape = n,
                    MixWeight = sp.Weight,
                }))
                .ToArray();

            serializedObject.Update();
            AppendDefinitionItems(definitions);
            serializedObject.ApplyModifiedProperties();
        }

        private List<string> FetchBlendShapeNames()
        {
            var component = target as Runtime.AdHocBlendShapeMix;
            return ZatoolsBlendShapeSelector.FetchBlendShapeNames(component.GetComponent<SkinnedMeshRenderer>().sharedMesh);
        }

        private List<float> FetchBlendShapeRelativeWeights()
        {
            var component = target as Runtime.AdHocBlendShapeMix;
            var targetSkinnedMesh = component.GetComponent<SkinnedMeshRenderer>();
            var sharedMesh = targetSkinnedMesh.sharedMesh;
            if (sharedMesh == null) return new List<float>();

            return Enumerable.Range(0, sharedMesh.blendShapeCount)
                .Select((i) => targetSkinnedMesh.GetBlendShapeWeight(i) / sharedMesh.GetBlendShapeFrameWeight(i, 0))
                .ToList();
        }

        private void AppendDefinitionItems(IEnumerable<BlendShapeMixDefinition> items)
        {
            var definitions = serializedObject.FindProperty(nameof(Runtime.AdHocBlendShapeMix.MixDefinitions));
            var nextIndex = definitions.arraySize;
            foreach (var item in items)
            {
                definitions.InsertArrayElementAtIndex(nextIndex);
                var elem = definitions.GetArrayElementAtIndex(nextIndex);
                elem.FindPropertyRelative(nameof(BlendShapeMixDefinition.FromBlendShape)).stringValue = item.FromBlendShape;
                elem.FindPropertyRelative(nameof(BlendShapeMixDefinition.ToBlendShape)).stringValue = item.ToBlendShape;
                elem.FindPropertyRelative(nameof(BlendShapeMixDefinition.MixWeight)).floatValue = item.MixWeight;
                ++nextIndex;
            }
        }

        private static VisualElement MakeDefinitionItem(VisualTreeAsset visualTreeItem, List<string> blendShapeNames)
        {
            var item = visualTreeItem.CloneTree();
            ZatoolsLocalization.UILocalizer.ApplyLocalizationFor(item);

            var fromField = item.Q<TextField>("FieldFromBlendShape");
            var toField = item.Q<TextField>("FieldToBlendShape");
            ZatoolsBlendShapeSelector.AttachTo(item.Q<Button>("ButtonOpenFromBlendShapePanel"), fromField, blendShapeNames);
            ZatoolsBlendShapeSelector.AttachTo(item.Q<Button>("ButtonOpenToBlendShapePanel"), toField, blendShapeNames);

            return item;
        }
    }
}
