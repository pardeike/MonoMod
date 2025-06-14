using MonoMod.Core.Utils;
using MonoMod.Utils;
using System;

namespace MonoMod.Core.Platforms.Architectures
{
    internal sealed class Arm64Arch : IArchitecture
    {
        public ArchitectureKind Target => ArchitectureKind.Arm64;

        public ArchitectureFeature Features => ArchitectureFeature.Immediate64;

        private BytePatternCollection? lazyKnownMethodThunks;
        public BytePatternCollection KnownMethodThunks => Helpers.GetOrInit(ref lazyKnownMethodThunks, CreateKnownMethodThunks);

        public IAltEntryFactory AltEntryFactory => null!;

        private readonly ISystem System;

        public Arm64Arch(ISystem system)
        {
            System = system;
        }

        public NativeDetourInfo ComputeDetourInfo(IntPtr from, IntPtr target, int maxSizeHint)
        {
            // Should work for arm64 as well
            x86Shared.FixSizeHint(ref maxSizeHint);

            if (maxSizeHint < BranchRegisterKind.Instance.Size)
            {
                MMDbgLog.Warning($"Size too small for all known detour kinds! Defaulting to BranchRegister. provided size: {maxSizeHint}");
            }

            return new(from, target, BranchRegisterKind.Instance, null);
        }

        public int GetDetourBytes(NativeDetourInfo info, Span<byte> buffer, out IDisposable? allocationHandle)
        {
            return DetourKindBase.GetDetourBytes(info, buffer, out allocationHandle);
        }

        public NativeDetourInfo ComputeRetargetInfo(NativeDetourInfo detour, IntPtr target, int maxSizeHint = -1)
        {
            // Should work for arm64 as well
            x86Shared.FixSizeHint(ref maxSizeHint);

            if (DetourKindBase.TryFindRetargetInfo(detour, target, maxSizeHint, out var retarget))
            {
                // the detour knows how to retarget itself, we'll use that
                return retarget;
            }

            // the detour doesn't know how to retarget itself, lets just compute a new detour to our new target
            return ComputeDetourInfo(detour.From, target, maxSizeHint);
        }

        public int GetRetargetBytes(NativeDetourInfo original, NativeDetourInfo retarget, Span<byte> buffer,
            out IDisposable? allocationHandle, out bool needsRepatch, out bool disposeOldAlloc)
        {
            return DetourKindBase.DoRetarget(original, retarget, buffer, out allocationHandle, out needsRepatch, out disposeOldAlloc);
        }

        public ReadOnlyMemory<IAllocatedMemory> CreateNativeVtableProxyStubs(IntPtr vtableBase, int vtableSize)
        {
            ReadOnlySpan<byte> stubData = [
                0x00, 0x04, 0x40, 0xF9, // ldr x0, [x0, #8]
                0x08, 0x00, 0x40, 0xF9, // ldr x8, [x0]
                0x8F, 0x00, 0x00, 0x18, // ldr w15, _offset
                0x08, 0x01, 0x0F, 0x8B, // add x8, x8, x15
                0x08, 0x01, 0x40, 0xF9, // ldr x8, [x8]
                0x00, 0x01, 0x1F, 0xD6, // br x8
                0x00, 0x00, 0x00, 0x00, // _offset: .word 0x0
            ];

            return Shared.CreateVtableStubs(System, vtableBase, vtableSize, stubData, 24, true);
        }

        public IAllocatedMemory CreateSpecialEntryStub(IntPtr target, IntPtr argument)
        {
            // CreateNativeExceptionHelper should be implemented first

            throw new NotImplementedException();
        }

        private static BytePatternCollection CreateKnownMethodThunks()
        {
            const byte Bn = BytePattern.BAnyValue;
            const byte Bd = BytePattern.BAddressValue;

            if (PlatformDetection.Runtime is RuntimeKind.Framework or RuntimeKind.CoreCLR)
            {
                return new BytePatternCollection(
                    // .NET 6 Support
                    //
                    // StubPrecode
                    new BytePattern(new AddressMeaning(AddressKind.Abs64), mustMatchAtStart: true,
                        new byte[]
                        {
                            0xff, 0xff, 0xff, 0xff,
                            0xff, 0xff, 0xff, 0xff,
                            0xff, 0xff, 0xff, 0xff,
                            0x00, 0x00, 0x00, 0x00,
                            0x00, 0x00, 0x00, 0x00,
                            0x00, 0x00, 0x00, 0x00,
                        },
                        new byte[]
                        {
                            0x89, 0x00, 0x00, 0x10, // adr x9, #16
                            0x2a, 0x31, 0x40, 0xa9, // ldp x10,x12,[x9]      ; =m_pTarget,m_pMethodDesc
                            0x40, 0x01, 0x1f, 0xd6, // br x10
                              Bn,   Bn,   Bn,   Bn,
                              Bd,   Bd,   Bd,   Bd,
                              Bd,   Bd,   Bd,   Bd
                        }
                    ),
                    // NDirectImportPrecode
                    new BytePattern(new AddressMeaning(AddressKind.Abs64), mustMatchAtStart: true,
                        new byte[]
                        {
                            0xff, 0xff, 0xff, 0xff,
                            0xff, 0xff, 0xff, 0xff,
                            0xff, 0xff, 0xff, 0xff,
                            0x00, 0x00, 0x00, 0x00,
                            0x00, 0x00, 0x00, 0x00,
                            0x00, 0x00, 0x00, 0x00,
                        },
                        new byte[]
                        {
                            0x8b, 0x00, 0x00, 0x10, // adr x11, #16             ; Notice that x11 register is used to differentiate the stub from StubPrecode which uses x9
                            0x6a, 0x31, 0x40, 0xa9, // ldp x10,x12,[x11]      ; =m_pTarget,m_pMethodDesc
                            0x40, 0x01, 0x1f, 0xd6, // br  x10
                              Bn,   Bn,   Bn,   Bn,
                              Bd,   Bd,   Bd,   Bd,
                              Bd,   Bd,   Bd,   Bd
                        }
                    ),
                    // FixupPrecode 
                    new BytePattern(new AddressMeaning(AddressKind.Abs64), mustMatchAtStart: true,
                        new byte[]
                        {
                            0xff, 0xff, 0xff, 0xff,
                            0xff, 0xff, 0xff, 0xff,
                            0xff, 0xff, 0xff, 0xff,
                            0x00, 0x00, 0x00, 0x00,
                            0x00, 0x00, 0x00, 0x00,
                            0x00, 0x00, 0x00, 0x00,
                        },
                        new byte[]
                        {
                            0x0c, 0x00, 0x00, 0x10, // adr x12, #0
                            0x6b, 0x00, 0x00, 0x58, // ldr x11, [pc, #12]     ; =m_pTarget
                            0x60, 0x01, 0x1f, 0xd6, // br  x11
                              Bn,   Bn,   Bn,   Bn,
                              Bd,   Bd,   Bd,   Bd,
                              Bd,   Bd,   Bd,   Bd
                        }
                    ),
                    // ThisPtrRetBufPrecode
                    new BytePattern(new AddressMeaning(AddressKind.Abs64), mustMatchAtStart: true,
                        new byte[]
                        {
                            0xff, 0xff, 0xff, 0xff,
                            0xff, 0xff, 0xff, 0xff,
                            0xff, 0xff, 0xff, 0xff,
                            0xff, 0xff, 0xff, 0xff,
                            0xff, 0xff, 0xff, 0xff,
                            0x00, 0x00, 0x00, 0x00,
                            0x00, 0x00, 0x00, 0x00,
                            0x00, 0x00, 0x00, 0x00,
                        },
                        new byte[]
                        {
                            0x10, 0x00, 0x00, 0x91, // mov x16, x0
                            0x20, 0x00, 0x00, 0x91, // mov x0, x1
                            0x01, 0x02, 0x00, 0x91, // mov x1, x16
                            0x70, 0x00, 0x00, 0x58, // ldr x16, [pc, #12]
                            0x00, 0x02, 0x1f, 0xd6, // br  x16
                              Bn,   Bn,   Bn,   Bn,
                              Bd,   Bd,   Bd,   Bd,
                              Bd,   Bd,   Bd,   Bd
                        }
                    ),
                    // .NET 8 Support
                    //
                    // #define STUB_PAGE_SIZE 16384
                    // #define DATA_SLOT(stub, field) (stub##Code + STUB_PAGE_SIZE + stub##Data__##field)
                    //
                    // FixupPrecodeCode
                    new BytePattern(
                        new AddressMeaning(AddressKind.Rel64 | AddressKind.Constant | AddressKind.Indirect, 0, 0x4000), mustMatchAtStart: true,
                        new byte[]
                        {
                            0xff, 0xff, 0xff, 0xff,
                            0xff, 0xff, 0xff, 0xff,
                            0xff, 0xff, 0xff, 0xff,
                            0xff, 0xff, 0xff, 0xff,
                            0xff, 0xff, 0xff, 0xff,
                        },
                        new byte[]
                        {
                            0x0b, 0x00, 0x02, 0x58, // ldr x11, DATA_SLOT(FixupPrecode, Target)
                            0x60, 0x01, 0x1f, 0xd6, // br x11
                            0x0c, 0x00, 0x02, 0x58, // ldr x12, DATA_SLOT(FixupPrecode, MethodDesc)
                            0x2b, 0x00, 0x02, 0x58, // ldr x11, DATA_SLOT(FixupPrecode, PrecodeFixupThunk)
                            0x60, 0x01, 0x1f, 0xd6, // br x11
                        }
                    ),
                    // FixupPrecodeCode ThePreStub entry point
                    new BytePattern(
                        new AddressMeaning(AddressKind.PrecodeFixupThunkRel64 | AddressKind.Constant | AddressKind.Indirect, 0, 0x4000), mustMatchAtStart: true,
                        new byte[]
                        {
                            0xff, 0xff, 0xff, 0xff,
                            0xff, 0xff, 0xff, 0xff,
                            0xff, 0xff, 0xff, 0xff,
                        },
                        new byte[]
                        {
                            0x0c, 0x00, 0x02, 0x58, // ldr x12, DATA_SLOT(FixupPrecode, MethodDesc)
                            0x2b, 0x00, 0x02, 0x58, // ldr x11, DATA_SLOT(FixupPrecode, PrecodeFixupThunk)
                            0x60, 0x01, 0x1f, 0xd6, // br x11
                        }
                    ),
                    // CallCountingStubCode
                    new BytePattern(
                        new AddressMeaning(AddressKind.Rel64 | AddressKind.Constant | AddressKind.Indirect, 0, 0x4008), mustMatchAtStart: true,
                        new byte[]
                        {
                            0xff, 0xff, 0xff, 0xff,
                            0xff, 0xff, 0xff, 0xff,
                            0xff, 0xff, 0xff, 0xff,
                            0xff, 0xff, 0xff, 0xff,
                            0xff, 0xff, 0xff, 0xff,
                            0xff, 0xff, 0xff, 0xff,
                            0xff, 0xff, 0xff, 0xff,
                            0xff, 0xff, 0xff, 0xff,
                            0xff, 0xff, 0xff, 0xff,
                        },
                        new byte[]
                        {
                            0x09, 0x00, 0x02, 0x58, // ldr  x9, DATA_SLOT(CallCountingStub, RemainingCallCountCell)
                            0x2a, 0x01, 0x40, 0x79, // ldrh w10, [x9]
                            0x4a, 0x05, 0x00, 0x71, // subs w10, w10, #1
                            0x2a, 0x01, 0x00, 0x79, // strh w10, [x9]
                            0x60, 0x00, 0x00, 0x54, // beq CountReachedZero
                            0xa9, 0xff, 0x01, 0x58, // ldr  x9, DATA_SLOT(CallCountingStub, TargetForMethod)
                            0x20, 0x01, 0x1f, 0xd6, // br   x9
                                                    // CountReachedZero:
                            0xaa, 0xff, 0x01, 0x58, // ldr  x10, DATA_SLOT(CallCountingStub, TargetForThresholdReached)
                            0x40, 0x01, 0x1F, 0xD6, // br   x10
                        }
                    )
                );
            }
            else
            {
                // TODO: Mono
                return new();
            }
        }

        private sealed class BranchRegisterKind : DetourKindBase
        {
            public static readonly BranchRegisterKind Instance = new();

            public override int Size => 4 + 4 + 8;

            public override int GetBytes(IntPtr from, IntPtr to, Span<byte> buffer, object? data, out IDisposable? allocHandle)
            {
                // ldr x9, _target
                buffer[0] = 0x49;
                buffer[1] = 0x00;
                buffer[2] = 0x00;
                buffer[3] = 0x58;
                // br x9
                buffer[4] = 0x20;
                buffer[5] = 0x01;
                buffer[6] = 0x1F;
                buffer[7] = 0xD6;
                // _target: .quad 0x0
                Unsafe.WriteUnaligned(ref buffer[8], (ulong)to);

                allocHandle = null;
                
                MMDbgLog.Trace($"Detouring arm64 from 0x{from:X16} to 0x{to:X16}");

                return Size;
            }

            public override bool TryGetRetargetInfo(NativeDetourInfo orig, IntPtr to, int maxSize, out NativeDetourInfo retargetInfo)
            {
                // we can always trivially retarget an abs64 detour (change the absolute constant)
                retargetInfo = orig with { To = to };
                return true;
            }


            public override int DoRetarget(NativeDetourInfo origInfo, IntPtr to, Span<byte> buffer, object? data,
                out IDisposable? allocationHandle, out bool needsRepatch, out bool disposeOldAlloc)
            {
                needsRepatch = true;
                disposeOldAlloc = true;
                // the retarget logic for rel32 is just the same as the normal patch
                // the patcher should re-patch the target method with the new bytes, and dispose the old allocation, if present
                return GetBytes(origInfo.From, to, buffer, data, out allocationHandle);
            }
        }
    }
}