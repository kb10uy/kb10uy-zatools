using nadena.dev.ndmf;

[assembly: ExportsPlugin(typeof(KusakaFactory.Zatools.ZatoolsNdmfPlugin))]
namespace KusakaFactory.Zatools
{
    internal sealed class ZatoolsNdmfPlugin : Plugin<ZatoolsNdmfPlugin>
    {
        public override string QualifiedName => "org.kb10uy.zatools";
        public override string DisplayName => "kb10uy's Various Tools";

        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .BeforePlugin("nadena.dev.modular-avatar")
                .Run(new Modules.BoneArrayRotationInfluenceApplier())
                .Then.Run(new Modules.AdHocBlendShapeMixer())
                .Then.Run(new Modules.EnhancedEyePointerInstaller.EepiTransforming());
        }
    }
}
