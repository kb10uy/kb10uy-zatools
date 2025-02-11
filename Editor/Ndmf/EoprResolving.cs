using UnityEngine;
using nadena.dev.ndmf;
using KusakaFactory.Zatools.Localization;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Ndmf
{
    internal sealed class EoprResolving : Pass<EoprResolving>
    {
        protected override void Execute(BuildContext context)
        {
            var state = context.GetState(EoprState.Initializer);
            var components = context.AvatarRootObject.GetComponentsInChildren<EditorOnlyPropertyReplicator>();
            foreach (var component in components)
            {
                // EditorOnly 以外からは作用させない
                if (!component.gameObject.CompareTag(Resources.TagEditorOnly))
                {
                    ErrorReport.ReportError(ZatoolLocalization.NdmfLocalizer, ErrorSeverity.NonFatal, "eopr.report.not-editor-only", component.gameObject.name);
                    Object.DestroyImmediate(component);
                    return;
                }

                state.AddForComponent(component);
                Object.DestroyImmediate(component);
            }
        }
    }
}
