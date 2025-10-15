using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.UIElements;
using nadena.dev.ndmf.preview;
using KusakaFactory.Zatools.Localization;

namespace KusakaFactory.Zatools.Ndmf.Inspector
{
    internal sealed class ZatoolsPreviewSwitcher : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<ZatoolsPreviewSwitcher, UxmlTraits>
        {
        }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            UxmlTypeAttributeDescription<IRenderFilter> _targetFilterType = new UxmlTypeAttributeDescription<IRenderFilter> { name = "target-filter-type" };

            public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription
            {
                get
                {
                    yield break;
                }
            }

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                var switcher = ve as ZatoolsPreviewSwitcher;
                switcher._targetFilterType = _targetFilterType.GetValueFromBag(bag, cc);
            }
        }

        private Type _targetFilterType;

        private TogglablePreviewNode _targetPreviewNode;
        private Label _state;
        private Button _toggler;

        public ZatoolsPreviewSwitcher()
        {
            var visualTree = ZatoolsResources.LoadVisualTreeByGuid("3f9faa4e18db6bf42aa8b36d312b2939");
            var element = visualTree.CloneTree();
            // 追加される CustomInspector 側で 適用されるのでここでは実行しない
            // ZatoolsLocalization.UILocalizer.ApplyLocalizationFor(element);
            Add(element);

            _state = element.Q<Label>("LabelState");
            _toggler = element.Q<Button>("ButtonSwitch");

            RegisterCallback<AttachToPanelEvent>(RegisterPreviewNode);
            _toggler.clicked += ToggleState;
        }

        private void RegisterPreviewNode(AttachToPanelEvent e)
        {
            if (_targetFilterType == null) return;
            var previewNodeProperty = _targetFilterType.GetProperty("SwitchingPreviewNode", BindingFlags.Static | BindingFlags.NonPublic);
            if (previewNodeProperty == null) return;
            _targetPreviewNode = previewNodeProperty.GetValue(_targetFilterType, BindingFlags.Static, null, null, null) as TogglablePreviewNode;
            if (previewNodeProperty == null) return;
            
            _targetPreviewNode.IsEnabled.OnChange += OnPublishedValueChanged;
            UpdateStateLabel();
        }

        private void OnPublishedValueChanged(bool value)
        {
            // OnChange のたびに event がリセットされるので登録しなおす
            UpdateStateLabel();
            _targetPreviewNode.IsEnabled.OnChange += OnPublishedValueChanged;
        }

        private void UpdateStateLabel()
        {
            if (_targetPreviewNode == null)
            {
                _state.text = "";
                return;
            }

            var state = _targetPreviewNode.IsEnabled.Value;
            _state.text = state ? "_.inspector.enabled" : "_.inspector.disabled";
            ZatoolsLocalization.UILocalizer.ApplyLocalizationFor(_state, false);
        }

        private void ToggleState()
        {
            if (_targetPreviewNode == null) return;

            var currentValue = _targetPreviewNode.IsEnabled.Value;
            _targetPreviewNode.IsEnabled.Value = !currentValue;
            // UpdateStateLabel();
        }
    }
}
