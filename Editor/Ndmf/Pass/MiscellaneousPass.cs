using nadena.dev.ndmf;

namespace KusakaFactory.Zatools.Ndmf.Pass
{
    // Vsvc stands for VrchatSdkVersionCheck
    internal sealed class VsvcResolving : ZatoolsPass<VsvcResolving>
    {
        protected override string ZatoolsPassName => nameof(VsvcResolving);
        protected override string ZatoolsPassDescription => "Check VRCSDK version";

        internal static readonly string SupportedVrchatSdkVersion = "3.10.0";

        protected override void Execute(BuildContext context)
        {
#if ZATOOLS_FOUND_ANY_VRCSDK
            // Current environment has VRCSDK package
#if ZATOOLS_HAS_VRCSDK
            // VRCSDK version requirement is met
#else
            // Incompatible version of VRCSDK detected
            ErrorReport.ReportError(new ZatoolsNdmfError(context.AvatarRootObject, ErrorSeverity.Error, "vsvc.report.incompatible-version", SupportedVrchatSdkVersion));
#endif
#endif
        }
    }
}
