using System;
using UnityEngine;
using nadena.dev.ndmf;

namespace KusakaFactory.Zatools.Runtime
{
    public abstract class ZatoolsComponent : MonoBehaviour, INDMFEditorOnly
    {
    }

    public abstract class ZatoolsMeshEditingComponent : ZatoolsComponent
    {
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class InspectorLabelAttribute : Attribute
    {
        public readonly string Label;

        public InspectorLabelAttribute(string label)
        {
            Label = label;
        }
    }
}
