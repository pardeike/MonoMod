// clang -O3 -dynamiclib helper_macos_arm64.cc -o helper_macos_arm64.dylib -std=c++20 -lc++ -Wall

#include <cstdint>
#include <thread>
#include <pthread.h>

// TODO: When adding the next JitHooksXX, consider templating the implementions to share common code.
namespace JitHooks60
{
    struct AllocMemArgs
    {
        // Input arguments
        uint32_t hotCodeSize;
        uint32_t coldCodeSize;
        uint32_t roDataSize;
        uint32_t xcptnsCount;
        uint32_t flag;

        // Output arguments
        void* hotCodeBlock;
        void* hotCodeBlockRW;
        void* coldCodeBlock;
        void* coldCodeBlockRW;
        void* roDataBlock;
        void* roDataBlockRW;
    };

    typedef int (*ICoreJitCompiler_compileMethod)(void* pThis, void* comp, void* info, unsigned flags, uint8_t** nativeEntry, uint32_t* nativeSizeOfCode);
    typedef int (*ICoreJitCompiler_compileMethod_hook_post)(void* pThis, void* comp, void* info, unsigned flags, uint8_t** nativeEntry, uint32_t* nativeSizeOfCode, int res, AllocMemArgs* pArgs);
    typedef void (*ICorJitInfo_allocMem)(void* pThis, struct AllocMemArgs* pArgs);

    struct JitHookConfig
    {
        ICoreJitCompiler_compileMethod compileMethod;
        ICoreJitCompiler_compileMethod compileMethodHook;
        ICoreJitCompiler_compileMethod_hook_post compileMethodHookPost;
        ICorJitInfo_allocMem allocMem;
        ICorJitInfo_allocMem allocMemHook;
    };

    static int ICoreJitCompiler_compileMethod_hook(void* pThis, void* comp, void* info, unsigned flags, uint8_t** nativeEntry, uint32_t* nativeSizeOfCode);
    static void ICorJitInfo_allocMem_hook(void* pThis, struct AllocMemArgs* pArgs);

    static thread_local int compileMethod_Entrancy = 0;
    static thread_local struct AllocMemArgs allocMem_Args = { };
    static struct JitHookConfig jitHookConfig =
    {
        .compileMethodHook = &ICoreJitCompiler_compileMethod_hook,
        .allocMemHook = &ICorJitInfo_allocMem_hook
    };

    // Tracks hook entrancy and saves/restores errno safely through unwinds
    class CompileMethodHookTracker
    {
    private:
        int lastErrNo;

    public:
        CompileMethodHookTracker() noexcept
        {
            lastErrNo = errno;
            ++compileMethod_Entrancy;
        }

        ~CompileMethodHookTracker() noexcept
        {
            --compileMethod_Entrancy;
            errno = lastErrNo;
        }

        int entrancy() noexcept
        {
            return compileMethod_Entrancy;
        }
    };

    static int ICoreJitCompiler_compileMethod_hook(void* pThis, void* comp, void* info, unsigned flags, uint8_t** nativeEntry, uint32_t* nativeSizeOfCode)
    {
        CompileMethodHookTracker tracker;
        int entrancy = tracker.entrancy();
        if (entrancy == 1)
            memset(&allocMem_Args, 0, sizeof(allocMem_Args));

        int res = jitHookConfig.compileMethod(pThis, comp, info, flags, nativeEntry, nativeSizeOfCode);
        if (entrancy == 1)
        {
            try
            {
                // Since the MAP_JIT W^X write protection is thread local, we can use a new thread to invoke the managed post method.

                // TODO: Consider instead running this on the same thread but checking the PAL_JitWriteProtect TLS enabledCount var (tricky to locate)
                // if (enabledCount > 0) { pthread_jit_write_protect_np(1); .HookPost(...); pthread_jit_write_protect_np(0); }

                struct AllocMemArgs args = allocMem_Args;
                std::thread hook_post_thread([&]()
                {
                    try
                    {
                        CompileMethodHookTracker tracker;
                        res = jitHookConfig.compileMethodHookPost(pThis, comp, info, flags, nativeEntry, nativeSizeOfCode, res, &args);
                    }
                    catch (...)
                    {
                        // Just catch and eat all exceptions, we don't want this thread to abort unexpectedly
                    }
                });

                hook_post_thread.join();
            }
            catch (...)
            {
                // Just catch and eat all exceptions as we don't want anything making their way back to the compileMethod callers
            }
        }

        return res;
    }

    static void ICorJitInfo_allocMem_hook(void* pThis, struct AllocMemArgs* pArgs)
    {
        jitHookConfig.allocMem(pThis, pArgs);

        if (compileMethod_Entrancy == 1)
            allocMem_Args = *pArgs;
    }
}

extern "C" void* mmch_jit_hook_config(int runtimeMajMin)
{
    switch (runtimeMajMin)
    {
    case 60:
        return &JitHooks60::jitHookConfig;
    default:
        return nullptr;
    }
}

extern "C" void mmch_jit_memcpy(void* dst, const void* src, size_t n)
{
    pthread_jit_write_protect_np(0);
    memcpy(dst, src, n);
    pthread_jit_write_protect_np(1);
}
