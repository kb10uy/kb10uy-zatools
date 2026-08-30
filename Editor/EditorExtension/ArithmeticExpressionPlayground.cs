using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using Unity.Mathematics;
using KusakaFactory.Zatools.Foundation.Arithmetic;
using KusakaFactory.Zatools.Localization;

namespace KusakaFactory.Zatools.EditorExtension
{
    internal sealed class ArithmeticExpressionPlayground : EditorWindow
    {
        private const int ComponentCount = 4;

        [MenuItem("Window/Zatools: kb10uy's Various Tools/Arithmetic Expression Playground")]
        internal static void OpenWindow()
        {
            var window = GetWindow<ArithmeticExpressionPlayground>("Arithmetic Expression Playground");
            window.minSize = new Vector2(460.0f, 520.0f);
        }

        private readonly List<VariableEntry> _variables = new List<VariableEntry>();
        private readonly List<ZaxDiagnostic> _diagnostics = new List<ZaxDiagnostic>();

        private TextField _expression;
        private Label _status;
        private Label _result;
        private TextField _disassembly;
        private ListView _variablesList;

        internal void CreateGUI()
        {
            var visualTree = ZatoolsResources.LoadVisualTreeByGuid("935ecee5773c46b68f8da83071c50a50");
            visualTree.CloneTree(rootVisualElement);
            ZatoolsLocalization.UILocalizer.ApplyLocalizationFor(rootVisualElement);

            _expression = rootVisualElement.Q<TextField>("FieldExpression");
            _status = rootVisualElement.Q<Label>("LabelStatus");
            _result = rootVisualElement.Q<Label>("LabelResult");
            _disassembly = rootVisualElement.Q<TextField>("FieldDisassembly");
            _variablesList = rootVisualElement.Q<ListView>("ListVariables");

            _variables.Clear();
            _variables.Add(new VariableEntry { Name = "pos", Type = ZaxValueType.Float3, Value = { [0] = 1.0f, [1] = 2.0f, [2] = 3.0f } });

            _variablesList.itemsSource = _variables;
            _variablesList.makeItem = MakeVariableItem;
            _variablesList.bindItem = BindVariableItem;
            _variablesList.selectionType = SelectionType.Single;
            _variablesList.reorderable = true;
            _variablesList.reorderMode = ListViewReorderMode.Animated;
            _variablesList.showAddRemoveFooter = true;
            _variablesList.showBoundCollectionSize = false;
            _variablesList.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
            _variablesList.fixedItemHeight = 22.0f;

            _variablesList.itemsAdded += (indices) =>
            {
                foreach (var index in indices) _variables[index] = new VariableEntry();
                ReevaluateDeferred();
            };
            _variablesList.itemsRemoved += (indices) => ReevaluateDeferred();
            _variablesList.itemIndexChanged += (from, to) => ReevaluateDeferred();

            _expression.RegisterValueChangedCallback((e) => Reevaluate());
            ZatoolsLocalization.OnNdmfLanguageChanged += Reevaluate;

            _variablesList.RefreshItems();
            _expression.value = "@pos normalize";
            Reevaluate();
        }

        private void OnDestroy()
        {
            ZatoolsLocalization.OnNdmfLanguageChanged -= Reevaluate;
        }

        private VisualElement MakeVariableItem()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var name = new TextField { name = "ItemName" };
            name.style.width = 96.0f;
            name.RegisterValueChangedCallback((e) =>
            {
                if (!TryResolveEntry(row, out var entry)) return;
                entry.Name = e.newValue;
                Reevaluate();
            });
            row.Add(name);

            var type = new EnumField(ZaxValueType.Float) { name = "ItemType" };
            type.style.width = 80.0f;
            type.RegisterValueChangedCallback((e) =>
            {
                if (!TryResolveEntry(row, out var entry)) return;
                entry.Type = (ZaxValueType)e.newValue;
                UpdateComponentVisibility(row, entry.Type);
                Reevaluate();
            });
            row.Add(type);

            for (var i = 0; i < ComponentCount; ++i)
            {
                var componentIndex = i;
                var component = new FloatField { name = $"ItemComponent{i}" };
                component.style.width = 56.0f;
                component.RegisterValueChangedCallback((e) =>
                {
                    if (!TryResolveEntry(row, out var entry)) return;
                    entry.Value[componentIndex] = e.newValue;
                    Reevaluate();
                });
                row.Add(component);
            }

            return row;
        }

        private void BindVariableItem(VisualElement element, int index)
        {
            if (index < 0 || index >= _variables.Count) return;
            if (_variables[index] == null) _variables[index] = new VariableEntry();

            var entry = _variables[index];
            element.userData = index;

            element.Q<TextField>("ItemName").SetValueWithoutNotify(entry.Name);
            element.Q<EnumField>("ItemType").SetValueWithoutNotify(entry.Type);
            for (var i = 0; i < ComponentCount; ++i)
            {
                element.Q<FloatField>($"ItemComponent{i}").SetValueWithoutNotify(entry.Value[i]);
            }
            UpdateComponentVisibility(element, entry.Type);
        }

        private bool TryResolveEntry(VisualElement row, out VariableEntry entry)
        {
            entry = null;
            if (!(row.userData is int index)) return false;
            if (index < 0 || index >= _variables.Count) return false;

            entry = _variables[index];
            return entry != null;
        }

        private static void UpdateComponentVisibility(VisualElement row, ZaxValueType type)
        {
            var dimension = type.Dimension();
            for (var i = 0; i < ComponentCount; ++i)
            {
                row.Q<FloatField>($"ItemComponent{i}").style.display =
                    i < dimension ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void ReevaluateDeferred()
        {
            rootVisualElement.schedule.Execute(Reevaluate);
        }

        private void Reevaluate()
        {
            if (_expression == null) return;

            var source = _expression.value;
            if (string.IsNullOrWhiteSpace(source))
            {
                SetStatus(string.Empty, false);
                _result.text = string.Empty;
                _disassembly.SetValueWithoutNotify(string.Empty);
                return;
            }

            var declared = _variables.Where((v) => v != null).ToArray();
            var variables = declared.Select((v) => v.ToVariable()).ToArray();

            _diagnostics.Clear();
            if (!ZaxCompiler.TryCompile(source, variables, null, _diagnostics, out var program))
            {
                SetStatus(ZatoolsLocalization.LocalizeZaxDiagnostic(_diagnostics[0]), true);
                _result.text = string.Empty;
                _disassembly.SetValueWithoutNotify(string.Empty);
                return;
            }

            SetStatus($"→ {program.ResultType.DisplayName()}", false);
            var values = declared.Select((v) => v.ToValue()).ToArray();
            _result.text = ZaxEvaluator.Evaluate(program, values).ToString();
            _disassembly.SetValueWithoutNotify(program.Disassemble());
        }

        private void SetStatus(string text, bool error)
        {
            _status.text = text;
            _status.style.color = error
                ? new StyleColor(EditorGUIUtility.isProSkin ? new Color(1.0f, 0.45f, 0.45f) : new Color(0.65f, 0.0f, 0.0f))
                : new StyleColor(StyleKeyword.Null);
        }

        private sealed class VariableEntry
        {
            internal string Name = "value";
            internal ZaxValueType Type = ZaxValueType.Float;
            internal readonly float[] Value = new float[ComponentCount];

            internal ZaxVariable ToVariable() => new ZaxVariable(Name ?? string.Empty, Type);

            internal ZaxValue ToValue()
            {
                switch (Type)
                {
                    case ZaxValueType.Int:
                        return ZaxValue.FromInt((int)Value[0]);
                    case ZaxValueType.Float:
                        return ZaxValue.FromFloat(Value[0]);
                    case ZaxValueType.Float2:
                        return ZaxValue.FromFloat2(new float2(Value[0], Value[1]));
                    case ZaxValueType.Float3:
                        return ZaxValue.FromFloat3(new float3(Value[0], Value[1], Value[2]));
                    default:
                        return ZaxValue.FromFloat4(new float4(Value[0], Value[1], Value[2], Value[3]));
                }
            }
        }
    }
}
