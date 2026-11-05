using System;
using System.Collections.Generic;
using UnityEngine;

namespace KusakaFactory.Zatools.Runtime
{
    [Serializable]
    public abstract class ZatoolsObjectReference
    {
        public string Path = "";

        internal abstract Component UntypedTarget { get; set; }
        internal abstract Type TargetType { get; }
    }

    [Serializable]
    public abstract class ZatoolsObjectReference<T> : ZatoolsObjectReference where T : Component
    {
        public T Target = null;

        internal override Component UntypedTarget
        {
            get => Target;
            set => Target = (T)value;
        }

        internal override Type TargetType => typeof(T);
    }

    [Serializable]
    public sealed class SkinnedMeshRendererReference : ZatoolsObjectReference<SkinnedMeshRenderer>
    {
    }

    internal interface IZatoolsObjectReferenceHolder
    {
        IEnumerable<ZatoolsObjectReference> GetObjectReferences();
    }
}
