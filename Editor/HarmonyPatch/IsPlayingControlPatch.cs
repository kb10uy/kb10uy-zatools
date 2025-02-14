using System;
using System.Collections.Concurrent;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace KusakaFactory.Zatools.HarmonyPatch
{
    internal static class IsPlayingControlPatch
    {
        private static ConcurrentStack<bool> OverriddenValues = new ConcurrentStack<bool>();
        private static Type UnityApplicationType = null;
        private static MethodInfo UnityIsPlayingGetter = null;
        private static MethodInfo OverriddenIsPlayingMethod = null;

        public static void With(bool value, Action action)
        {
            OverriddenValues.Push(value);
            action();
            if (!OverriddenValues.TryPop(out var popped) || popped != value)
            {
                throw new InvalidOperationException("Application.isPlaying control failure");
            }
        }

        public static bool IsPlayingOverridden()
        {
#pragma warning disable CS0436
            // この Application は UnityEntgine.CoreModule.dll ではなく kb10uy-zatools のものを指す
            return OverriddenValues.TryPeek(out var topValue) ? topValue : Application.isPlaying;
#pragma warning restore CS0436
        }

        internal static void Apply(Harmony harmony)
        {
            PrepareReflections();
            Memory.DetourMethod(UnityIsPlayingGetter, OverriddenIsPlayingMethod);
        }

        private static void PrepareReflections()
        {
            // UnityEntgine.CoreModule.dll の方を参照するために当該アセンブリ内の適当な型を経由する
            UnityApplicationType = typeof(Time).Assembly.GetType("UnityEngine.Application");
            UnityIsPlayingGetter = AccessTools.PropertyGetter(UnityApplicationType, "isPlaying");
            OverriddenIsPlayingMethod = AccessTools.Method(typeof(IsPlayingControlPatch), nameof(IsPlayingOverridden));
        }
    }
}
