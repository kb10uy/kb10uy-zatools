using System.Collections.Generic;
using UnityEngine;
using nadena.dev.ndmf;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Ndmf
{
    internal sealed class EoprState
    {
        private List<Pair> _pairs = new List<Pair>();

        private EoprState(BuildContext context) { }

        public static EoprState Initializer(BuildContext context) => new EoprState(context);

        public void AddForComponent(EditorOnlyPropertyReplicator replicator)
        {
            var source = replicator.gameObject;
            foreach (var target in replicator.Targets)
            {
                _pairs.Add(new Pair
                {
                    Source = source,
                    Target = target,
                    CopyBlendShapes = replicator.EnableSmrBlendShapes,
                    BlendShapesExclusion = replicator.SmrBlendShapesExclusion,
                });
            }
        }

        public sealed class Pair
        {
            public GameObject Source;
            public GameObject Target;
            public bool CopyBlendShapes;
            public string[] BlendShapesExclusion;
        }
    }
}
