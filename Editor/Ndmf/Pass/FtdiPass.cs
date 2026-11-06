#if ZATOOLS_HAS_VRCSDK_FACE_TRACKING
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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
                Install(context.AvatarRootObject, components);
            }
            finally
            {
                foreach (var component in components) UnityObject.DestroyImmediate(component);
            }
        }

        private static void Install(GameObject avatarRoot, FtdiComponent[] components)
        {
            var descriptor = avatarRoot.GetComponent<VRCAvatarDescriptor>();
            if (descriptor == null) return;

            var targets = components.Where((c) => c.Definition != null).ToList();
            if (targets.Count == 0) return;

            if (Ftdi.HasExistingConfiguration(descriptor))
            {
                ErrorReport.ReportError(new ZatoolsNdmfError(descriptor, ErrorSeverity.Error, "ftdi.report.existing-configuration"));
                return;
            }

            var succeeded = true;
            var sources = new List<Ftdi.Source>();
            foreach (var component in targets)
            {
                var meshes = ObjectReferenceResolver.ResolveAll(component, component.MeshReferences, component.Meshes);
                foreach (var mesh in meshes.Where((m) => m.State == ReferenceState.Unresolved))
                {
                    ErrorReport.ReportError(new ZatoolsNdmfError(component, ErrorSeverity.Error, "reference.report.unresolved", mesh.Path));
                    succeeded = false;
                }

                var diagnostics = new List<FtdiDiagnostic>();
                var definition = FtdiJson.Parse(component.Definition.text, diagnostics);
                if (diagnostics.Count > 0)
                {
                    foreach (var diagnostic in diagnostics) Report(diagnostic.WithContext(component));
                    succeeded = false;
                    continue;
                }
                sources.Add(new Ftdi.Source
                {
                    Context = component,
                    Definition = definition,
                    Meshes = meshes.Where((m) => m.State == ReferenceState.Resolved).Select((m) => m.Target).ToList(),
                });
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
