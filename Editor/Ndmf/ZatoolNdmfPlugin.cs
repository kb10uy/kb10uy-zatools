using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;

[assembly: ExportsPlugin(typeof(KusakaFactory.Zatools.Ndmf.ZatoolsPlugin))]
namespace KusakaFactory.Zatools.Ndmf
{
    internal sealed class ZatoolsPlugin : Plugin<ZatoolsPlugin>
    {
        public override string QualifiedName => "org.kb10uy.zatools";
        public override string DisplayName => "kb10uy's Various Tools";

        protected override void Configure()
        {
            var bari = new BariTransforming();
            var ahbsm = new AhbsmTransforming();
            var eepi = new EepiTransforming();
            var pbfctt = new PbfcttTransforming();

            // Before MA
            InPhase(BuildPhase.Transforming)
                .BeforePlugin("nadena.dev.modular-avatar")
                .Run(bari)
                .Then.Run(ahbsm);

            // Before MA with VirtualControllerContext
            InPhase(BuildPhase.Transforming)
                .BeforePlugin("nadena.dev.modular-avatar")
                .WithRequiredExtension(typeof(VirtualControllerContext), (seq) =>
                {
                    seq.Run(eepi);
                });

            // After MA
            InPhase(BuildPhase.Transforming)
                .AfterPlugin("nadena.dev.modular-avatar")
                .Run(pbfctt);
        }
    }
}
