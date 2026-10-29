using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace TreeDistrict
{
    // Adds balanced timer calls without replacing or reordering the original call.
    internal static class CallTimingInstrumentation
    {
        internal static IEnumerable<CodeInstruction> Wrap(IEnumerable<CodeInstruction> source,
            Func<MethodInfo, string> labelFor, MethodInfo start, MethodInfo end)
        {
            foreach (var original in source)
            {
                var instruction = new CodeInstruction(original);
                var method = instruction.operand as MethodInfo;
                string label = (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
                    && method != null ? labelFor(method) : null;
                if (label == null) { yield return instruction; continue; }
                if (instruction.blocks.Count != 0)
                    throw new InvalidOperationException("Unexpected exception boundary on timed call: " + method);
                var begin = new CodeInstruction(OpCodes.Ldstr, label);
                begin.labels.AddRange(instruction.labels);
                instruction.labels.Clear();
                yield return begin;
                yield return new CodeInstruction(OpCodes.Call, start);
                yield return instruction;
                yield return new CodeInstruction(OpCodes.Ldstr, label);
                yield return new CodeInstruction(OpCodes.Call, end);
            }
        }
    }
}
