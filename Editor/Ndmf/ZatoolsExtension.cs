using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KusakaFactory.Zatools.Ndmf.Framework
{
    internal static class ZatoolsExtension
    {
        public static IEnumerable<Transform> EnumerateDirectChildren(this Transform parent)
        {
            return Enumerable.Range(0, parent.childCount).Select(parent.GetChild);
        }
    }
}
