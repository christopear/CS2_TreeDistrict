using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Game.Effects;
using HarmonyLib;

namespace TreeDistrict
{
    // Diagnostic only: does not change culling, complete extra jobs or inspect entity populations.
    internal static class LightCullingTimings
    {
        private const string Owner = "TreeDistrict.Diagnostics.LightCulling";
        private static Harmony harmony;
        internal static void Install()
        {
            try
            {
                harmony = new Harmony(Owner);
                foreach (string name in new[] { "OnUpdate", "ComputeLightEffectCullData" })
                {
                    var target = AccessTools.DeclaredMethod(typeof(LightCullingSystem), name);
                    if (target == null) throw new MissingMethodException(typeof(LightCullingSystem).FullName, name);
                    harmony.Patch(target, transpiler: new HarmonyMethod(typeof(LightCullingTimings), nameof(Transpile)));
                }
                Mod.Log.Info("LightCulling detail timers installed: original calls preserved; active only during placement and its diagnostic tail.");
            }
            catch (Exception ex)
            {
                Uninstall();
                Mod.Log.Warn("LightCulling detail timers unavailable; original game behavior retained: " + ex);
            }
        }
        internal static void Uninstall()
        {
            harmony?.UnpatchAll(Owner);
            harmony = null;
        }
        private static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var code = instructions.ToList();
            // Fail closed if the installed game no longer has the expected methods/call sites.
            var calls = code.Where(c => c.opcode == OpCodes.Call || c.opcode == OpCodes.Callvirt)
                .Select(c => c.operand).OfType<MethodInfo>().ToList();
            string required = __originalMethod.Name == "OnUpdate" ? "Complete" : "CompleteDependency";
            if (calls.Count(m => m.Name == required) != 1)
                throw new InvalidOperationException("Unexpected LightCulling call layout: " + __originalMethod.Name);
            if (__originalMethod.Name == "OnUpdate" && calls.Count(m => m.Name == "ComputeLightEffectCullData") != 1)
                throw new InvalidOperationException("Missing light prefab preparation call.");
            return CallTimingInstrumentation.Wrap(code, m => Label(__originalMethod.Name, m),
                AccessTools.Method(typeof(NativePlacementTimings), "Start"),
                AccessTools.Method(typeof(NativePlacementTimings), "End")).ToList();
        }
        private static string Label(string parent, MethodInfo method)
        {
            // Avoid adding timers to per-prefab loop bodies. The preparation parent covers that work.
            switch (method.Name)
            {
                case "Complete":
                case "ClearFrameLightData":
                case "GetLodParameters":
                case "ComputeLightEffectCullData":
                case "CalculateFrustumPlanes":
                case "GetEnabledData":
                case "Schedule":
                case "ToArchetypeChunkArray":
                case "CompleteDependency":
                case "CalculateEntityCount":
                    return "LightCulling.Detail." + parent + "." + method.DeclaringType.Name + "." + method.Name;
                default: return null;
            }
        }
    }
}
