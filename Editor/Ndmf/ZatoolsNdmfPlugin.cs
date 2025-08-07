using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using KusakaFactory.Zatools.Ndmf.Pass;
using KusakaFactory.Zatools.Ndmf.Preview;

[assembly: ExportsPlugin(typeof(KusakaFactory.Zatools.Ndmf.ZatoolsNdmfPlugin))]
namespace KusakaFactory.Zatools.Ndmf
{
    internal sealed class ZatoolsNdmfPlugin : Plugin<ZatoolsNdmfPlugin>
    {
        public override string QualifiedName => "org.kb10uy.zatools";
        public override string DisplayName => "kb10uy's Various Tools";

        protected override void Configure()
        {
            // Resolving
            InPhase(BuildPhase.Resolving)
                .Run(new AsvResolving());

            // Transforming before MA
            InPhase(BuildPhase.Transforming)
                .BeforePlugin("nadena.dev.modular-avatar")
                .Run(new BariTransforming())
                .Then.Run(new AhnbTransforming()).PreviewingWith(new AhnbRenderFilter())
                .Then.Run(new AhbsmTransforming()).PreviewingWith(new AhbsmRenderFilter())
                .Then.Run(new NpeTransforming()).PreviewingWith(new NpeRenderFilter());

            // Transforming before MA with VirtualControllerContext
            InPhase(BuildPhase.Transforming)
                .BeforePlugin("nadena.dev.modular-avatar")
                .WithRequiredExtension(typeof(VirtualControllerContext), (seq) =>
                {
                    seq.Run(new EepiTransforming());
                });

            // Transforming after MA
            InPhase(BuildPhase.Transforming)
                .AfterPlugin("nadena.dev.modular-avatar")
                .Run(new PbfcttTransforming());

            // Transforming after MA with VirtualControllerContext
            InPhase(BuildPhase.Transforming)
                .AfterPlugin("nadena.dev.modular-avatar")
                .WithRequiredExtension(typeof(VirtualControllerContext), (seq) =>
                {
                    seq.Run(new GmpoTransforming());
                });
        }
    }
}
