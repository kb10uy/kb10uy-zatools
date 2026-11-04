#if ZATOOLS_HAS_VRCSDK_FACE_TRACKING
using System.Collections.Generic;
using VRC.SDK3.Avatars.Components;
using nadena.dev.ndmf;
using KusakaFactory.Zatools.Ndmf.Core;
using UnityObject = UnityEngine.Object;
using FtdiComponent = KusakaFactory.Zatools.Runtime.FaceTrackingDataInstaller;

namespace KusakaFactory.Zatools.Ndmf.Pass
{
    [RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
    internal sealed class FtdiTransforming : ZatoolsPass<FtdiTransforming>
    {
        protected override string ZatoolsPassName => nameof(FtdiTransforming);
        protected override string ZatoolsPassDescription => "Install face tracking data";

        protected override void Execute(BuildContext context)
        {
            var components = context.AvatarRootObject.GetComponentsInChildren<FtdiComponent>();
            if (components.Length == 0) return;

            try
            {
                Install(context, components);
            }
            finally
            {
                foreach (var component in components) UnityObject.DestroyImmediate(component);
            }
        }

        private static void Install(BuildContext context, FtdiComponent[] components)
        {
            var descriptor = context.AvatarRootObject.GetComponent<VRCAvatarDescriptor>();
            if (descriptor == null) return;
            if (Ftdi.HasExistingConfiguration(descriptor))
            {
                ErrorReport.ReportError(new ZatoolsNdmfError(descriptor, ErrorSeverity.Error, "ftdi.report.existing-configuration"));
                return;
            }

            var succeeded = true;
            var sources = new List<Ftdi.Source>();
            foreach (var component in components)
            {
                if (component.Definition == null)
                {
                    ErrorReport.ReportError(new ZatoolsNdmfError(component, ErrorSeverity.Error, "ftdi.report.missing-definition"));
                    succeeded = false;
                    continue;
                }

                var diagnostics = new List<FtdiDiagnostic>();
                var definition = FtdiJson.Parse(component.Definition.text, diagnostics);
                if (diagnostics.Count > 0)
                {
                    foreach (var diagnostic in diagnostics) Report(diagnostic.WithContext(component));
                    succeeded = false;
                    continue;
                }
                sources.Add(new Ftdi.Source { Context = component, Definition = definition, Meshes = component.Meshes });
            }
            if (!succeeded) return;

            var mergeDiagnostics = new List<FtdiDiagnostic>();
            var configuration = Ftdi.Merge(sources, descriptor.useAllTrackingKeys, mergeDiagnostics);
            if (mergeDiagnostics.Count > 0)
            {
                foreach (var diagnostic in mergeDiagnostics) Report(diagnostic.Context != null ? diagnostic : diagnostic.WithContext(descriptor));
                return;
            }

            Ftdi.Apply(descriptor, configuration);
        }

        private static void Report(FtdiDiagnostic diagnostic)
        {
            var name = diagnostic.Context is FtdiComponent component && component.Definition != null ? component.Definition.name : "";
            var message = diagnostic.Localize();
            ErrorReport.ReportError(new ZatoolsNdmfError(diagnostic.Context, ErrorSeverity.Error, "ftdi.report.invalid-definition", name.Length > 0 ? $"{name}: {message}" : message));
        }
    }
}
#endif
