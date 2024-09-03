using UnityEngine.UIElements;
using UnityEditor;
using KusakaFactory.Zatools.Localization;

namespace KusakaFactory.Zatools.Inspector
{
    internal abstract class ZatoolEditorBase : Editor
    {
        private bool _initialized = false;

        protected virtual VisualElement CreateInspectorGUIImpl()
        {
            return null;
        }

        public sealed override VisualElement CreateInspectorGUI()
        {
            if (!_initialized)
            {
                ZatoolLocalization.OnNdmfLanguageChanged += RebuildUI;
                _initialized = true;
            }

            return CreateInspectorGUIImpl();
        }

        protected virtual void OnDestroy()
        {
            ZatoolLocalization.OnNdmfLanguageChanged -= RebuildUI;
            _initialized = false;
        }

        private void RebuildUI()
        {
            CreateInspectorGUI();
        }
    }
}
