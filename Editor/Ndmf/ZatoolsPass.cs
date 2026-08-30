using System.Collections.Generic;
using nadena.dev.ndmf;
using KusakaFactory.Zatools.Foundation.Arithmetic;
using KusakaFactory.Zatools.Localization;
using UnityObject = UnityEngine.Object;

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

        protected static bool TryCompileZaxExpression(
            UnityObject target,
            string source,
            IReadOnlyList<ZaxVariable> variables,
            ZaxValueType? expectedType,
            out ZaxProgram program)
        {
            var diagnostics = new List<ZaxDiagnostic>();
            if (ZaxCompiler.TryCompile(source, variables, expectedType, diagnostics, out program)) return true;

            foreach (var diagnostic in diagnostics)
            {
                ErrorReport.ReportError(new ZatoolsNdmfError(
                    target,
                    ErrorSeverity.Error,
                    "zax.report.compile-error",
                    source ?? string.Empty,
                    ZatoolsLocalization.LocalizeZaxDiagnostic(diagnostic)));
            }

            return false;
        }
    }
}
