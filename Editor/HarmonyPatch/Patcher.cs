using UnityEditor;
using HarmonyLib;

namespace KusakaFactory.Zatools.HarmonyPatch
{
    internal static class Patcher
    {
        private const string HarmonyId = "org.kb10uy.zatools";

        [InitializeOnLoadMethod]
        private static void ApplyPatches()
        {
            var harmony = new Harmony(HarmonyId);
            AssemblyReloadEvents.beforeAssemblyReload += () => harmony.UnpatchAll(HarmonyId);
        }
    }
}
