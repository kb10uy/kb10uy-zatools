using UnityEngine.UIElements;
using UnityEditor;
using KusakaFactory.Zatools.Localization;

namespace KusakaFactory.Zatools.Ndmf.Inspector
{
    internal abstract class ZatoolInspector : Editor
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
                ZatoolsLocalization.OnNdmfLanguageChanged += RebuildUI;
            }
            _boundElementRoot.Clear();
            _boundElementRoot.Add(CreateInspectorGUIImpl());

            return _boundElementRoot;
        }

        protected virtual void OnDestroy()
        {
            ZatoolsLocalization.OnNdmfLanguageChanged -= RebuildUI;
        }

        private void RebuildUI()
        {
            CreateInspectorGUI();
        }
    }
}
