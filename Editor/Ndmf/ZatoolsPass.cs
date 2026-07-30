using nadena.dev.ndmf;

namespace KusakaFactory.Zatools.Ndmf
{
    /// <summary>
    /// Base class for NDMF passes implemented by Zatools.
    /// </summary>
    /// <remarks>
    /// This type is public only so that passes in external in-house assemblies can inherit from it.
    /// It is not a stable public API and may change or be removed without notice between releases.
    /// </remarks>
    public abstract class ZatoolsPass<T> : Pass<T> where T : ZatoolsPass<T>, new()
    {
        protected abstract string ZatoolsPassName { get; }
        protected abstract string ZatoolsPassDescription { get; }

        public override string QualifiedName => $"org.kb10uy.zatools.pass.{ZatoolsPassName}";
        public override string DisplayName => $"{ZatoolsPassName} ({ZatoolsPassDescription})";
    }
}
