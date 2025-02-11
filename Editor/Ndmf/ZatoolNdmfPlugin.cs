using nadena.dev.ndmf;

[assembly: ExportsPlugin(typeof(KusakaFactory.Zatools.Ndmf.ZatoolsPlugin))]
namespace KusakaFactory.Zatools.Ndmf
{
    internal sealed class ZatoolsPlugin : Plugin<ZatoolsPlugin>
    {
        public override string QualifiedName => "org.kb10uy.zatools";
        public override string DisplayName => "kb10uy's Various Tools";

        protected override void Configure()
        {
            InPhase(BuildPhase.Resolving)
                .BeforePlugin("nadena.dev.modular-avatar")
                .Run(new EoprResolving());

            InPhase(BuildPhase.Transforming)
                .BeforePlugin("nadena.dev.modular-avatar")
                .Run(new BariTransforming())
                .Then.Run(new EoprTransforming())
                .Then.Run(new AhbsmTransforming())
                .Then.Run(new EepiTransforming());
        }
    }
}
