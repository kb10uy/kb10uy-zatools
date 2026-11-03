#if ZATOOLS_HAS_VRCSDK_FACE_TRACKING
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using nadena.dev.ndmf.runtime;
using VRC.SDK3.Avatars.Components;
using KusakaFactory.Zatools.Localization;
using KusakaFactory.Zatools.Ndmf.Core;
using KusakaFactory.Zatools.Runtime;
using CombineMode = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.ParameterBehaviorCombineMode;
using TargetMode = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.ParameterBehaviorTargetMode;

namespace KusakaFactory.Zatools.Ndmf.Inspector
{
    [CustomEditor(typeof(FaceTrackingDataInstaller))]
    internal sealed class FtdiInspector : ZatoolsInspector
    {
        private const int SummaryInputLimit = 4;

        private FaceTrackingDataInstaller _component;
        private VisualElement _existingSection;
        private Button _saveButton;
        private Label _saveDisabledLabel;
        private HelpBox _statusBox;
        private VisualElement _diagnosticsContainer;
        private VisualElement _summaryContainer;

        private bool _parsed;
        private TextAsset _parsedAsset;
        private string _parsedText;

        protected override VisualElement CreateInspectorGUIImpl()
        {
            var visualTree = ZatoolsResources.LoadVisualTreeByGuid("85f87a584e5c4a639c1da2bb53b466b2");

            var inspector = visualTree.CloneTree();
            ZatoolsLocalization.UILocalizer.ApplyLocalizationFor(inspector);
            inspector.Bind(serializedObject);

            _component = (FaceTrackingDataInstaller)target;
            _existingSection = inspector.Q<VisualElement>("SectionExistingConfiguration");
            _saveButton = inspector.Q<Button>("ButtonSaveExisting");
            _saveDisabledLabel = inspector.Q<Label>("LabelSaveDisabled");
            _statusBox = inspector.Q<HelpBox>("BoxStatus");
            _diagnosticsContainer = inspector.Q<VisualElement>("ContainerDiagnostics");
            _summaryContainer = inspector.Q<VisualElement>("ContainerSummary");

            _saveButton.clicked += SaveExistingConfiguration;
            inspector.TrackSerializedObjectValue(serializedObject, (_) => Refresh(false));
            inspector.RegisterCallback<AttachToPanelEvent>((_) =>
            {
                EditorApplication.projectChanged += OnProjectChanged;
                ObjectChangeEvents.changesPublished += OnObjectChanged;
                Undo.undoRedoPerformed += RefreshExistingConfiguration;
            });
            inspector.RegisterCallback<DetachFromPanelEvent>((_) =>
            {
                EditorApplication.projectChanged -= OnProjectChanged;
                ObjectChangeEvents.changesPublished -= OnObjectChanged;
                Undo.undoRedoPerformed -= RefreshExistingConfiguration;
            });

            Refresh(true);
            return inspector;
        }

        private void OnProjectChanged()
        {
            Refresh(false);
        }

        private void OnObjectChanged(ref ObjectChangeEventStream stream)
        {
            RefreshExistingConfiguration();
        }

        private void Refresh(bool force)
        {
            RefreshExistingConfiguration();
            RefreshDefinition(force);
        }

        private void RefreshExistingConfiguration()
        {
            if (_component == null) return;
            var descriptor = FindDescriptor();
            var existing = descriptor != null && Ftdi.HasExistingConfiguration(descriptor);
            var assigned = _component.Definition != null;
            SetDisplayed(_existingSection, existing);
            _saveButton.SetEnabled(!assigned);
            SetDisplayed(_saveDisabledLabel, assigned);
        }

        private void RefreshDefinition(bool force)
        {
            if (_component == null) return;
            var asset = _component.Definition;
            var text = asset != null ? asset.text : null;
            if (!force && _parsed && asset == _parsedAsset && text == _parsedText) return;
            _parsed = true;
            _parsedAsset = asset;
            _parsedText = text;

            _diagnosticsContainer.Clear();
            _summaryContainer.Clear();
            if (asset == null)
            {
                SetStatus(HelpBoxMessageType.Info, Localized("ftdi.inspector.status-no-definition"));
                SetDisplayed(_summaryContainer, false);
                return;
            }

            var diagnostics = new List<FtdiDiagnostic>();
            var definition = FtdiJson.Parse(text, diagnostics);
            if (diagnostics.Count > 0)
            {
                SetStatus(HelpBoxMessageType.Error, Format("ftdi.inspector.status-failed", diagnostics.Count.ToString()));
                foreach (var diagnostic in diagnostics) _diagnosticsContainer.Add(DiagnosticLabel(asset, diagnostic));
                SetDisplayed(_summaryContainer, false);
                return;
            }

            SetDisplayed(_statusBox, false);
            BuildSummary(definition);
            SetDisplayed(_summaryContainer, true);
        }

        private void BuildSummary(Ftdi.Definition definition)
        {
            var localSets = new HashSet<string>(definition.BlendShapeSets.Select((s) => s.Name));

            var useAllTrackingKeys = definition.UseAllTrackingKeys.HasValue
                ? (definition.UseAllTrackingKeys.Value ? "true" : "false")
                : Localized("ftdi.inspector.unspecified");
            _summaryContainer.Add(SummaryHeading("UseAllTrackingKeys", useAllTrackingKeys));

            _summaryContainer.Add(SummaryHeading("BlendShapeSets", definition.BlendShapeSets.Count.ToString()));
            foreach (var set in definition.BlendShapeSets)
            {
                var weight = set.DefaultWeight.HasValue ? set.DefaultWeight.Value.ToString("0.###") : "-";
                var counts = Format("ftdi.inspector.summary-set-counts", set.TrackingKeys.Count.ToString(), set.Visemes.Count.ToString());
                _summaryContainer.Add(SummaryLabel($"{set.Name}    weight {weight}    {counts}"));
            }

            _summaryContainer.Add(SummaryHeading("ParameterBehaviors", definition.ParameterBehaviors.Count.ToString()));
            for (var i = 0; i < definition.ParameterBehaviors.Count; ++i)
            {
                var behavior = definition.ParameterBehaviors[i];
                var line = new StringBuilder($"#{i + 1}  {behavior.Operation} → {behavior.Target}");
                if (behavior.Operation == TargetMode.ApplyBlendSetWeight && !localSets.Contains(behavior.Target))
                {
                    line.Append(Localized("ftdi.inspector.summary-external"));
                }
                if (behavior.Weight != null)
                {
                    line.Append($" × {behavior.Weight}");
                    if (!localSets.Contains(behavior.Weight)) line.Append(Localized("ftdi.inspector.summary-external"));
                }
                line.Append("    ").Append(DescribeExpression(behavior));
                _summaryContainer.Add(SummaryLabel(line.ToString()));
            }
        }

        private static string DescribeExpression(Ftdi.ParameterBehavior behavior)
        {
            if (behavior.Combine == CombineMode.Custom) return "Custom";

            var inputs = behavior.Inputs
                .Take(SummaryInputLimit)
                .Select((i) => i.Invert ? $"!{Ftdi.InputName(i.Index)}" : Ftdi.InputName(i.Index));
            var rest = behavior.Inputs.Count > SummaryInputLimit ? ", …" : "";
            return $"{behavior.Combine}({string.Join(", ", inputs)}{rest})";
        }

        private void SaveExistingConfiguration()
        {
            var descriptor = FindDescriptor();
            if (descriptor == null || !Ftdi.HasExistingConfiguration(descriptor) || _component.Definition != null) return;

            var title = Localized("ftdi.inspector.save-title");
            var diagnostics = new List<FtdiDiagnostic>();
            var text = FtdiJson.Write(Ftdi.Export(descriptor, diagnostics));
            if (diagnostics.Count == 0) FtdiJson.Parse(text, diagnostics);
            if (diagnostics.Count > 0)
            {
                var lines = string.Join("\n", diagnostics.Select((d) => d.Localize()));
                EditorUtility.DisplayDialog(title, Format("ftdi.inspector.save-invalid", lines), Localized("ftdi.inspector.save-confirm-cancel"));
                return;
            }

            if (!EditorUtility.DisplayDialog(title, Localized("ftdi.inspector.save-confirm"), Localized("ftdi.inspector.save-confirm-ok"), Localized("ftdi.inspector.save-confirm-cancel"))) return;

            var path = EditorUtility.SaveFilePanelInProject(title, $"{descriptor.gameObject.name}-FaceTracking", "json", Localized("ftdi.inspector.save-message"));
            if (string.IsNullOrEmpty(path)) return;

            File.WriteAllText(path, text, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(path);
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(title);
            Undo.RecordObject(_component, title);
            Undo.RecordObject(descriptor, title);

            _component.Definition = asset;
            foreach (var mesh in descriptor.faceTrackingMeshes.Where((m) => m != null))
            {
                if (!_component.Meshes.Contains(mesh)) _component.Meshes.Add(mesh);
            }
            descriptor.faceTrackingMeshes = new List<SkinnedMeshRenderer>();
            descriptor.faceTrackingBlendshapesSetList = new List<VRCAvatarDescriptor.FaceTrackingBlendshapesSet>();
            descriptor.faceTrackingParameterBehaviors = new List<VRCAvatarDescriptor.FaceTrackingParameterBehavior>();

            PrefabUtility.RecordPrefabInstancePropertyModifications(_component);
            PrefabUtility.RecordPrefabInstancePropertyModifications(descriptor);
            Undo.CollapseUndoOperations(undoGroup);

            serializedObject.Update();
            Refresh(true);
        }

        private VRCAvatarDescriptor FindDescriptor()
        {
            var avatarRoot = RuntimeUtil.FindAvatarInParents(_component.transform);
            return avatarRoot != null ? avatarRoot.GetComponent<VRCAvatarDescriptor>() : null;
        }

        private static Label DiagnosticLabel(TextAsset asset, FtdiDiagnostic diagnostic)
        {
            var message = diagnostic.Localize();
            var location = diagnostic.Location;
            var label = new Label(location.IsKnown ? $"L{location.Line}:{location.Column}  {message}" : message);
            label.AddToClassList("ftdi-diagnostic");
            if (location.IsKnown)
            {
                label.tooltip = Localized("ftdi.inspector.open-location");
                label.RegisterCallback<ClickEvent>((_) => AssetDatabase.OpenAsset(asset, location.Line, location.Column));
            }
            return label;
        }

        private static VisualElement SummaryHeading(string title, string value)
        {
            var heading = new VisualElement();
            heading.AddToClassList("ftdi-summary__heading");
            heading.Add(SummaryLabel(title, "ftdi-summary__heading-title"));
            heading.Add(SummaryLabel(value, "ftdi-summary__heading-value"));
            return heading;
        }

        private static Label SummaryLabel(string text, string className = "ftdi-summary__line")
        {
            var label = new Label(text);
            label.AddToClassList(className);
            return label;
        }

        private void SetStatus(HelpBoxMessageType type, string message)
        {
            SetDisplayed(_statusBox, true);
            _statusBox.messageType = type;
            _statusBox.text = message;
        }

        private static string Localized(string key)
        {
            return ZatoolsLocalization.NdmfLocalizer.GetLocalizedString(key);
        }

        private static string Format(string key, params string[] arguments)
        {
            return ZatoolsLocalization.FormatLocalized(key, arguments);
        }
    }
}
#endif
