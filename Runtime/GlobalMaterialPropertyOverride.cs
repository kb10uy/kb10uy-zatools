using System;
using UnityEngine;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Zatools Override Material Property Globally")]
    [Icon("Packages/org.kb10uy.zatools/Resources/Icon.png")]
    public sealed class GlobalMaterialPropertyOverride : ZatoolsComponent
    {
        public string ShaderNamePattern = "";
        public MaterialPropertyOverride[] Overrides = new MaterialPropertyOverride[] { };
    }

    [Serializable]
    public sealed class MaterialPropertyOverride
    {
        public string Name = "";
        public MaterialPropertyOverrideType TargetType = MaterialPropertyOverrideType.Float;
        public float FloatValue = 0.0f;
        public int IntValue = 0;
        public Vector4 VectorValue = Vector4.zero;
    }

    public enum MaterialPropertyOverrideType
    {
        Float,
        Int,
        Vector,
    }
}
