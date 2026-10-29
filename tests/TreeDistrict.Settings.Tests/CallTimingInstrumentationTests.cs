using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Xunit;

namespace TreeDistrict.Tests
{
    public class CallTimingInstrumentationTests
    {
        private static readonly List<string> Events = new List<string>();
        public static void Start(string name) => Events.Add("start:" + name);
        public static void End(string name) => Events.Add("end:" + name);
        public static int Double(int value) { Events.Add("original"); return value * 2; }
        private static MethodInfo Method(string name) => typeof(CallTimingInstrumentationTests).GetMethod(name)!;

        [Fact]
        public void TimersPreserveArgumentsReturnValuesAndBranchTargets()
        {
            Events.Clear();
            var dynamic = new DynamicMethod("Timed", typeof(int), new[] { typeof(int) });
            var il = dynamic.GetILGenerator();
            var label = il.DefineLabel();
            var call = new CodeInstruction(OpCodes.Call, Method(nameof(Double)));
            call.labels.Add(label);
            var original = new[] { new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Br, label), call, new CodeInstruction(OpCodes.Ret) };
            var wrapped = CallTimingInstrumentation.Wrap(original, _ => "double", Method(nameof(Start)), Method(nameof(End))).ToList();
            foreach (var instruction in wrapped)
            {
                foreach (var target in instruction.labels) il.MarkLabel(target);
                if (instruction.operand is MethodInfo method) il.Emit(instruction.opcode, method);
                else if (instruction.operand is string text) il.Emit(instruction.opcode, text);
                else if (instruction.operand is Label target) il.Emit(instruction.opcode, target);
                else il.Emit(instruction.opcode);
            }
            var run = (Func<int,int>)dynamic.CreateDelegate(typeof(Func<int,int>));
            Assert.Equal(42, run(21));
            Assert.Equal(new[] { "start:double", "original", "end:double" }, Events);
            Assert.Single(call.labels); // The original IL was not mutated.
        }

        [Fact]
        public void UnselectedCallsAreUnchanged()
        {
            var original = new CodeInstruction(OpCodes.Call, Method(nameof(Double)));
            var wrapped = CallTimingInstrumentation.Wrap(new[] { original }, _ => null!, Method(nameof(Start)), Method(nameof(End))).ToList();
            Assert.Single(wrapped);
            Assert.Equal(original.operand, wrapped[0].operand);
        }

        [Fact]
        public void UnexpectedExceptionBoundariesRejectInstrumentation()
        {
            var call = new CodeInstruction(OpCodes.Call, Method(nameof(Double)));
            call.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
            Assert.Throws<InvalidOperationException>(() => CallTimingInstrumentation.Wrap(new[] { call }, _ => "double", Method(nameof(Start)), Method(nameof(End))).ToList());
        }
    }
}
