using nadena.dev.ndmf;

namespace KusakaFactory.Zatools.Ndmf.Pass
{
    internal abstract class ZatoolsPass<T> : Pass<T> where T : ZatoolsPass<T>, new()
    {
        internal abstract string ZatoolsPassName { get; }
        internal abstract string ZatoolsPassDescription { get; }

        public override string QualifiedName => $"org.kb10uy.zatools.pass.{ZatoolsPassName}";
        public override string DisplayName => $"{ZatoolsPassName} ({ZatoolsPassDescription})";
    }
}
