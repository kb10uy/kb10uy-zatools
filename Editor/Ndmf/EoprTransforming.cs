using System.Collections.Generic;
using UnityEngine;
using nadena.dev.ndmf;

namespace KusakaFactory.Zatools.Ndmf
{
    internal sealed class EoprTransforming : Pass<EoprTransforming>
    {
        protected override void Execute(BuildContext context)
        {
            var state = context.GetState(EoprState.Initializer);
        }
    }
}
