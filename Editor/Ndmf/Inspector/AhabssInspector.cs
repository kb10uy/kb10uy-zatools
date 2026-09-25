using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using KusakaFactory.Zatools.Foundation.Arithmetic;
using KusakaFactory.Zatools.Localization;
using KusakaFactory.Zatools.Ndmf.Core;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Ndmf.Inspector
{
    [CustomEditor(typeof(AdHocAdvancedBlendShapeSynthesis))]
    internal sealed class AhabssInspector : ZatoolsInspector
    {
        private ImmutableArray<ZaxVariable> _entryVariables = ImmutableArray<ZaxVariable>.Empty;
        private readonly List<ZatoolsZaxExpressionField> _entryExpressionFields = new List<ZatoolsZaxExpressionField>();

        protected override VisualElement CreateInspectorGUIImpl()
        {
            var blendShapeNames = FetchBlendShapeNames();
            var visualTree = ZatoolsResources.LoadVisualTreeByGuid("b524fe7c232948bf842dded020757c61");
            var visualTreeSourceItem = ZatoolsResources.LoadVisualTreeByGuid("0a0a62bee17d452a819a1516cf4a7411");
            var visualTreeItem = ZatoolsResources.LoadVisualTreeByGuid("7095b39550a946b5a3974de7e307c855");

            var inspector = visualTree.CloneTree();
            ZatoolsLocalization.UILocalizer.ApplyLocalizationFor(inspector);

            // string 配列の要素に独自アイテムを使うので、バインド前に bindItem を差し替えておく
            var sourcesList = inspector.Q<ListView>("FieldSourceBlendShapes");
            sourcesList.makeItem = () => MakeSourceItem(visualTreeSourceItem, blendShapeNames);
            sourcesList.bindItem = BindSourceItem;
            sourcesList.unbindItem = UnbindSourceItem;
            sourcesList.itemsAdded += ResetAddedSources;

            inspector.Bind(serializedObject);

            inspector.Q<Label>("LabelVariables").text = FormatVariables(Ahabss.BaseVariables);

            var sourcesProperty = serializedObject.FindProperty(nameof(AdHocAdvancedBlendShapeSynthesis.SourceBlendShapes));
            _entryExpressionFields.Clear();
            UpdateEntryVariables(sourcesProperty.arraySize);
            inspector.TrackPropertyValue(sourcesProperty, (p) => UpdateEntryVariables(p.arraySize));

            var entriesList = inspector.Q<ListView>("FieldEntries");
            entriesList.makeItem = () => MakeEntryItem(visualTreeItem);
            entriesList.itemsAdded += ResetAddedEntries;

            inspector.Q<Button>("ButtonSaveToJson").clicked += SaveToJson;
            inspector.Q<Button>("ButtonLoadFromJson").clicked += LoadFromJson;

            return inspector;
        }

        private void SaveToJson()
        {
            var component = target as AdHocAdvancedBlendShapeSynthesis;
            var pathToSave = EditorUtility.SaveFilePanel(
                "Save Advanced BlendShape Synthesis Definitions as JSON",
                "",
                $"{component.gameObject.name}-AdvancedBlendShapeSynthesis.json",
                "json"
            );
            if (pathToSave.Length == 0) return;

            var definition = new JsonDefinition
            {
                Sources = component.SourceBlendShapes.ToArray(),
                Entries = component.Entries
                    .Where((e) => e != null)
                    .Select((e) => new JsonEntry { Name = e.Name, Value = e.Value, Expression = e.Expression })
                    .ToArray(),
            };
            var json = JsonConvert.SerializeObject(definition, Formatting.Indented);
            File.WriteAllText(pathToSave, json, new UTF8Encoding(false));
        }

        private void LoadFromJson()
        {
            var pathToLoad = EditorUtility.OpenFilePanel("Load Advanced BlendShape Synthesis Definitions from JSON", "", "json");
            if (pathToLoad.Length == 0) return;

            var jsonText = File.ReadAllText(pathToLoad, new UTF8Encoding(false));
            var definition = JsonConvert.DeserializeObject<JsonDefinition>(jsonText);
            if (definition == null) return;

            // SerializedProperty 経由で書き込まないと undo が効かない
            serializedObject.Update();

            var sources = serializedObject.FindProperty(nameof(AdHocAdvancedBlendShapeSynthesis.SourceBlendShapes));
            var loadedSources = definition.Sources ?? new string[0];
            sources.arraySize = loadedSources.Length;
            for (var i = 0; i < loadedSources.Length; ++i)
            {
                sources.GetArrayElementAtIndex(i).stringValue = loadedSources[i] ?? string.Empty;
            }

            var entries = serializedObject.FindProperty(nameof(AdHocAdvancedBlendShapeSynthesis.Entries));
            var loadedEntries = (definition.Entries ?? new JsonEntry[0]).Where((e) => e != null).ToArray();
            entries.arraySize = loadedEntries.Length;
            for (var i = 0; i < loadedEntries.Length; ++i)
            {
                var element = entries.GetArrayElementAtIndex(i);
                element.FindPropertyRelative(nameof(AhabssEntry.Name)).stringValue = loadedEntries[i].Name ?? string.Empty;
                element.FindPropertyRelative(nameof(AhabssEntry.Value)).floatValue = loadedEntries[i].Value;
                element.FindPropertyRelative(nameof(AhabssEntry.Expression)).stringValue = loadedEntries[i].Expression ?? string.Empty;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private List<string> FetchBlendShapeNames()
        {
            var component = target as AdHocAdvancedBlendShapeSynthesis;
            return ZatoolsBlendShapeSelector.FetchBlendShapeNames(component.GetComponent<SkinnedMeshRenderer>().sharedMesh);
        }

        private static VisualElement MakeSourceItem(VisualTreeAsset visualTreeSourceItem, List<string> blendShapeNames)
        {
            var item = visualTreeSourceItem.CloneTree();
            ZatoolsBlendShapeSelector.AttachTo(item.Q<Button>("ButtonOpenBlendShapeNamePanel"), item.Q<TextField>("FieldName"), blendShapeNames);
            return item;
        }

        private void BindSourceItem(VisualElement item, int index)
        {
            var sources = serializedObject.FindProperty(nameof(AdHocAdvancedBlendShapeSynthesis.SourceBlendShapes));
            if (index < 0 || index >= sources.arraySize) return;
            item.Q<Label>("LabelIndex").text = $"#{index}";
            item.Q<TextField>("FieldName").BindProperty(sources.GetArrayElementAtIndex(index));
        }

        private static void UnbindSourceItem(VisualElement item, int index)
        {
            item.Q<TextField>("FieldName").Unbind();
        }

        private void ResetAddedSources(IEnumerable<int> indices)
        {
            var sources = serializedObject.FindProperty(nameof(AdHocAdvancedBlendShapeSynthesis.SourceBlendShapes));
            foreach (var index in indices)
            {
                sources.GetArrayElementAtIndex(index).stringValue = string.Empty;
            }
            serializedObject.ApplyModifiedProperties();
        }

        private VisualElement MakeEntryItem(VisualTreeAsset visualTreeItem)
        {
            var item = visualTreeItem.CloneTree();
            ZatoolsLocalization.UILocalizer.ApplyLocalizationFor(item);

            var expressionField = item.Q<ZatoolsZaxExpressionField>("FieldExpression");
            expressionField.Configure(Ahabss.ResultTypes, _entryVariables, false);
            _entryExpressionFields.Add(expressionField);

            return item;
        }

        private void UpdateEntryVariables(int sourceCount)
        {
            var variables = Ahabss.VariablesFor(sourceCount);
            if (variables.SequenceEqual(_entryVariables, VariableComparer.Instance) && !_entryVariables.IsEmpty) return;

            _entryVariables = variables;
            foreach (var field in _entryExpressionFields)
            {
                field.Configure(Ahabss.ResultTypes, _entryVariables, false);
            }
        }

        private static string FormatVariables(IEnumerable<ZaxVariable> variables)
        {
            return string.Join("\n", variables.Select((v) => $"@{v.Name}: {v.Type.DisplayName()}"));
        }

        private void ResetAddedEntries(IEnumerable<int> indices)
        {
            var entries = serializedObject.FindProperty(nameof(AdHocAdvancedBlendShapeSynthesis.Entries));
            foreach (var index in indices)
            {
                entries.GetArrayElementAtIndex(index).boxedValue = new AhabssEntry();
            }
            serializedObject.ApplyModifiedProperties();
        }

        private sealed class JsonDefinition
        {
            [JsonProperty("sources")]
            public string[] Sources;

            [JsonProperty("entries")]
            public JsonEntry[] Entries;
        }

        private sealed class JsonEntry
        {
            [JsonProperty("name")]
            public string Name;

            [JsonProperty("value")]
            public float Value;

            [JsonProperty("expr")]
            public string Expression;
        }

        private sealed class VariableComparer : IEqualityComparer<ZaxVariable>
        {
            internal static readonly VariableComparer Instance = new VariableComparer();

            public bool Equals(ZaxVariable x, ZaxVariable y) => x.Name == y.Name && x.Type == y.Type;

            public int GetHashCode(ZaxVariable obj) => (obj.Name, obj.Type).GetHashCode();
        }
    }
}
