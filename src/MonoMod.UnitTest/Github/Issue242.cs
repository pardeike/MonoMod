using System;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace MonoMod.UnitTest.Github
{
    public class Issue242 : TestBase
    {
        public Issue242(ITestOutputHelper helper) : base(helper)
        {
        }

        // Reproduce the exact struct from the issue
        public struct SomeStruct
        {
            public double n1;
            public double n2;
            public double n3;
            public double n4;
        }

        public class Mainclass
        {
            public virtual SomeStruct Method(string s)
            {
                Console.WriteLine("Mainclass.Method called");
                return default;
            }
        }

        public class Subclass : Mainclass
        {
            public override SomeStruct Method(string s)
            {
                Console.WriteLine("Subclass.Method called");
                var me = this;
                Console.WriteLine("this = " + me);
                
                // This is the critical test - if 'this' is null, this would throw NullReferenceException
                // We're testing that this SHOULD NOT throw
                if (this == null)
                {
                    throw new InvalidOperationException("this instance became null - this is the bug!");
                }
                
                return default;
            }
        }

        [Fact]
        public void InstanceMethodReturningStructShouldNotMakeThisNull()
        {
            var instance = new Subclass();

            Console.WriteLine("### WITHOUT PATCH");
            // This should work fine without any patches
            _ = instance.Method("test");

            // Now apply an empty Harmony patch (which should not change behavior)
            // Use reflection to access Harmony since we need to avoid namespace issues
            var harmonyAssembly = Assembly.LoadFrom("lib/0Harmony.dll");
            var harmonyType = harmonyAssembly.GetType("HarmonyLib.Harmony") ?? harmonyAssembly.GetType("Harmony.HarmonyInstance") ?? harmonyAssembly.GetType("Harmony");
            Assert.NotNull(harmonyType);
            
            // Set DEBUG = true
            var debugField = harmonyType.GetField("DEBUG", BindingFlags.Static | BindingFlags.Public);
            if (debugField != null)
            {
                debugField.SetValue(null, true);
            }
            
            // Create harmony instance
            var harmony = Activator.CreateInstance(harmonyType, "test");
            Assert.NotNull(harmony);

            // Find AccessTools type and DeclaredMethod
            var accessToolsType = harmonyAssembly.GetType("HarmonyLib.AccessTools") ?? harmonyAssembly.GetType("Harmony.AccessTools");
            Assert.NotNull(accessToolsType);
            
            var declaredMethodMethod = accessToolsType.GetMethod("DeclaredMethod", new[] { typeof(Type), typeof(string) });
            Assert.NotNull(declaredMethodMethod);
            
            var original = (MethodInfo)declaredMethodMethod.Invoke(null, new object[] { typeof(Subclass), nameof(Subclass.Method) });
            Assert.NotNull(original);
            
            // Apply empty patch (no prefix, postfix, transpiler - just like the issue description)
            var patchMethod = harmonyType.GetMethod("Patch", new[] { typeof(MethodInfo) });
            Assert.NotNull(patchMethod);
            patchMethod.Invoke(harmony, new object[] { original });

            Console.WriteLine("### WITH PATCH");
            // Test for correct behavior: this should NOT throw NullReferenceException
            // If the bug exists, this will fail because 'this' becomes null
            // If the bug is fixed, this will pass because 'this' remains valid
            var exception = Record.Exception(() => instance.Method("test"));
            
            // Assert that no exception was thrown (correct behavior)
            Assert.Null(exception);
        }

        [Fact]
        public void MinimalReproExampleFromPardeikeShouldNotThrow()
        {
            // This is the more minimal example from @pardeike mentioned in the issue
            var instance = new Subclass();

            Console.WriteLine("### WITHOUT PATCH");
            _ = instance.Method("test");

            // Use reflection approach for Harmony to avoid namespace/reference issues
            var harmonyAssembly = Assembly.LoadFrom("lib/0Harmony.dll");
            var harmonyType = harmonyAssembly.GetType("HarmonyLib.Harmony") ?? harmonyAssembly.GetType("Harmony.HarmonyInstance") ?? harmonyAssembly.GetType("Harmony");
            Assert.NotNull(harmonyType);
            
            // Set DEBUG = true  
            var debugField = harmonyType.GetField("DEBUG", BindingFlags.Static | BindingFlags.Public);
            if (debugField != null)
            {
                debugField.SetValue(null, true);
            }
            
            var harmony = Activator.CreateInstance(harmonyType, "test");
            var accessToolsType = harmonyAssembly.GetType("HarmonyLib.AccessTools") ?? harmonyAssembly.GetType("Harmony.AccessTools");
            var declaredMethodMethod = accessToolsType.GetMethod("DeclaredMethod", new[] { typeof(Type), typeof(string) });
            var original = (MethodInfo)declaredMethodMethod.Invoke(null, new object[] { typeof(Subclass), nameof(Subclass.Method) });
            
            var patchMethod = harmonyType.GetMethod("Patch", new[] { typeof(MethodInfo) });
            patchMethod.Invoke(harmony, new object[] { original });

            Console.WriteLine("### WITH PATCH");
            
            // The issue mentions that this throws AccessViolationException on osx-x64
            // We test that it should NOT throw any exception (correct behavior)
            var exception = Record.Exception(() => instance.Method("test"));
            Assert.Null(exception);
        }
    }
}