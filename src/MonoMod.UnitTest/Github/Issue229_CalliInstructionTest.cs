using MonoMod.Utils;
using Xunit;
using Mono.Cecil.Cil;
using MonoMod.Core.Platforms;
using System;

namespace MonoMod.UnitTest.Github {
    public class Issue229_CalliInstructionTest {
        [Fact]
        public void CalliInstructionShouldCompileWithoutException() {
            DynamicMethodDefinition dmd = new("a", null, new[] { typeof(nint), typeof(valid) });
            var il = dmd.GetILProcessor();
            var c = new Mono.Cecil.CallSite(dmd.Module.ImportReference(typeof(void)));
            c.Parameters.Add(new(dmd.Module.ImportReference(typeof(valid))));
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Calli, c);
            il.Emit(OpCodes.Ret);

            // This should not throw an exception
            var exception = Record.Exception(() => PlatformTriple.Current.Compile(dmd.Generate()));
            Assert.Null(exception);
        }

        public struct valid { }
    }
}