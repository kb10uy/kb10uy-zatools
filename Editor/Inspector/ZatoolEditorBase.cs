using UnityEngine.UIElements;
using UnityEditor;
using KusakaFactory.Zatools.Localization;

namespace KusakaFactory.Zatools.Inspector
{
    internal abstract class ZatoolEditorBase : Editor
    {
        private VisualElement _boundElementRoot;

        protected virtual VisualElement CreateInspectorGUIImpl()
        {
            return null;
        }

        public sealed override VisualElement CreateInspectorGUI()
        {
            if (_boundElementRoot == null)
            {
                _boundElementRoot = new VisualElement();
                ZatoolLocalization.OnNdmfLanguageChanged += RebuildUI;
            }
            _boundElementRoot.Clear();
            _boundElementRoot.Add(CreateInspectorGUIImpl());

            return _boundElementRoot;
        }

        protected virtual void OnDestroy()
        {
            ZatoolLocalization.OnNdmfLanguageChanged -= RebuildUI;
        }

        private void RebuildUI()
        {
            CreateInspectorGUI();
        }
    }
}
