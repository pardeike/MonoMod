using Microsoft.Win32.SafeHandles;
using MonoMod.Core.Interop;
using MonoMod.Utils;
using System;
using System.Buffers;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text;

namespace MonoMod.Core.Platforms.Systems
{
    /// <summary>
    /// Native helper for macOS arm64
    /// </summary>
    public class MacOSArm64Helper
    {
        /// <summary>
        /// Singleton instance created by a call to <see cref="Initialize"/>.
        /// </summary>
        public static MacOSArm64Helper? Instance { get; private set; }
        
        private const string TempFileNameTmpl = "/tmp/mm-macos-silicon-helper.dylib.XXXXXX";
        private const string LogicalName = "helper_macos_arm64.dylib";
        private const string JitMemCpyExport = "mmch_jit_memcpy";
        private const string PrecompileICoreJitCompiler21CompileMethodExport = "mmch_precompile_icorejitcompiler21_compilemethod";
        private const string PrecompileICoreJitInfo60AllocMemExport = "mmch_precompile_icorejitinfo60_allocmem";
        
        private readonly IntPtr _handle;
        private readonly IntPtr _jitMemCpy;
        private readonly IntPtr _precompileICoreJitCompiler21CompileMethod;
        private readonly IntPtr _precompileICoreJitInfo60AllocMem;
        
        private MacOSArm64Helper(string fileName)
        {
            _handle = DynDll.OpenLibrary(fileName);
            
            try
            {
                _jitMemCpy = DynDll.GetExport(_handle, JitMemCpyExport);
                Helpers.Assert(_jitMemCpy != IntPtr.Zero);

                _precompileICoreJitCompiler21CompileMethod = DynDll.GetExport(_handle, PrecompileICoreJitCompiler21CompileMethodExport);
                Helpers.Assert(_precompileICoreJitCompiler21CompileMethod != IntPtr.Zero);

                _precompileICoreJitInfo60AllocMem = DynDll.GetExport(_handle, PrecompileICoreJitInfo60AllocMemExport);
                Helpers.Assert(_precompileICoreJitInfo60AllocMem != IntPtr.Zero);
            }
            catch
            {
                DynDll.CloseLibrary(_handle);
                throw;
            }
        }

        /// <summary>
        /// JIT_MAP (RWX) safe memory copy
        /// </summary>
        /// <param name="dst">Destination</param>
        /// <param name="src">Source</param>
        /// <param name="size">Size of memory that should be copied from dst to src</param>
        public unsafe void JitMemCpy(IntPtr dst, IntPtr src, ulong size)
        {
            var fnPtr = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, ulong, void>)_jitMemCpy;
            
            fnPtr(dst, src, size);
        }

        /// <summary>
        /// Precompiles an ICoreJitCompiler.CompileMethod hook method.
        /// </summary>
        /// <param name="cmPtr">Function pointer to ICoreJitCompiler.CompileMethod hook to be compiled.</param>
        public unsafe void PrecompileICoreJitCompiler21CompileMethod(IntPtr cmPtr)
        {
            var fnPtr = (delegate* unmanaged[Cdecl]<IntPtr, void>)_precompileICoreJitCompiler21CompileMethod;

            fnPtr(cmPtr);
        }

        /// <summary>
        /// Precompiles an ICoreJitInfo.AllocMem hook method.
        /// </summary>
        /// <param name="amPtr">Function pointer to ICoreJitInfo.AllocMem hook to be compiled.</param>
        public unsafe void PrecompileICoreJitInfo60AllocMem(IntPtr amPtr)
        {
            var fnPtr = (delegate* unmanaged[Cdecl]<IntPtr, void>)_precompileICoreJitInfo60AllocMem;

            fnPtr(amPtr);
        }

        private static void CreateTempFile(string tmpl, out int fd, out string fileName)
        {
            var tmplName = ArrayPool<byte>.Shared.Rent(tmpl.Length + 1);
            
            try
            {
                unsafe
                {
                    tmplName.AsSpan().Clear();
                    Encoding.UTF8.GetBytes(tmpl).AsSpan().CopyTo(tmplName);

                    fixed (byte* tmplNamePtr = tmplName)
                        fd = OSX.MkSTemp(tmplNamePtr);

                    if (fd == -1)
                    {
                        var lastError = OSX.Errno;
                        var ex = new Win32Exception(lastError);
                        MMDbgLog.Error($"Could not create temp file for NativeExceptionHelper: {lastError} {ex}");
                        throw ex;
                    }

                    fileName = Encoding.UTF8.GetString(tmplName, 0, tmpl.Length);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(tmplName);
            }
        }

        private static void ExtractEmbeddedResourcesToTempFile(int fd, string logicalName)
        {
            using (var handle = new SafeFileHandle((IntPtr)fd, true))
            using (var stream = new FileStream(handle, FileAccess.Write))
            {
                using var embedded = Assembly.GetExecutingAssembly().GetManifestResourceStream(logicalName);
                Helpers.Assert(embedded is not null);

                embedded.CopyTo(stream);
            }
        }

        private static string GetTempFileFromEmbeddedResources(string logicalName)
        {
            CreateTempFile(TempFileNameTmpl, out var fd, out var fileName);
            ExtractEmbeddedResourcesToTempFile(fd, logicalName);

            return fileName;
        }
        
        /// <summary>
        /// Creates Native Helper
        /// </summary>
        /// <returns>MacOSArm64Helper</returns>
        public static void Initialize()
        {
            Helpers.Assert(Instance is null);
            
            MMDbgLog.Trace($"{nameof(MacOSArm64Helper)} has been initialized.");

            var fileName = GetTempFileFromEmbeddedResources(LogicalName);

            Instance = new MacOSArm64Helper(fileName);
        }
    }
}
