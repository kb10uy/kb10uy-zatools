using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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

            return inspector;
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

        private sealed class VariableComparer : IEqualityComparer<ZaxVariable>
        {
            internal static readonly VariableComparer Instance = new VariableComparer();

            public bool Equals(ZaxVariable x, ZaxVariable y) => x.Name == y.Name && x.Type == y.Type;

            public int GetHashCode(ZaxVariable obj) => (obj.Name, obj.Type).GetHashCode();
        }
    }
}
