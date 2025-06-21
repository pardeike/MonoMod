// clang exhelper_linux_arm64.s -o exhelper_linux_arm64.so -shared -Wl,--eh-frame-hdr,-z,now,-z,noexecstack,-x 
.arch armv8-a
.build_version macos, 12, 0 sdk_version 12, 0

.set _UA_SEARCH_PHASE, 0
.set _UA_CLEANUP_PHASE, 1
.set _UA_HANDLER_FRAME, 2
.set _UA_FORCE_UNWIND, 3

.set _URC_HANDLER_FOUND, 6
.set _URC_INSTALL_CONTEXT, 7
.set _URC_CONTINUE_UNWIND, 8

.set DW_REG_x0, 0


.data
.p2align 3

_LSDA_none:
    .word 0

_LSDA_mton:
    .word .Lemtn_landingpad - _LSDA_mton


.tbss _cur_ex_ptr$tlv$init, 4, 2

.section __DATA,__thread_vars,thread_local_variables
.p2align 3
_cur_ex_ptr:
    .quad __tlv_bootstrap
    .quad 0
    .quad _cur_ex_ptr$tlv$init


.text
.global _eh_get_exception_ptr, _eh_has_exception, _eh_managed_to_native, _eh_native_to_managed

_eh_get_exception_ptr:
    .cfi_startproc
    .cfi_lsda 0x10, _LSDA_none
    stp x7, x8, [sp, #-80]!
    stp x5, x6, [sp, #16]
    stp x3, x4, [sp, #32]
    stp x1, x2, [sp, #48]
    stp x29, x30, [sp, #64]
    .cfi_def_cfa_offset 80
    .cfi_offset x30, -8
    .cfi_offset x29, -16
    .cfi_offset x2, -24
    .cfi_offset x1, -32
    .cfi_offset x4, -40
    .cfi_offset x3, -48
    .cfi_offset x6, -56
    .cfi_offset x5, -64
    .cfi_offset x8, -72
    .cfi_offset x7, -80
    add x29, sp, #64
    adrp x0, _cur_ex_ptr@TLVPPAGE
    ldr x0, [x0, _cur_ex_ptr@TLVPPAGEOFF]
    ldr x8, [x0]
    blr x8
    ldp x29, x30, [sp, #64]
    ldp x1, x2, [sp, #48]
    ldp x3, x4, [sp, #32]
    ldp x5, x6, [sp, #16]
    ldp x7, x8, [sp], #80
    .cfi_restore x30
    .cfi_restore x29
    .cfi_restore x2
    .cfi_restore x1
    .cfi_restore x4
    .cfi_restore x3
    .cfi_restore x6
    .cfi_restore x5
    .cfi_restore x8
    .cfi_restore x7
    .cfi_def_cfa_offset 0
    ret
    .cfi_endproc

_eh_has_exception:
    .cfi_startproc
    .cfi_lsda 0x10, _LSDA_none
    stp x29, x30, [sp, #-16]!
    .cfi_def_cfa_offset 16
    .cfi_offset x30, -8
    .cfi_offset x29, -16
    mov x29, sp
    bl _eh_get_exception_ptr
    ldr x0, [x0]
    cmp x0, #0
    cset x0, ne
    ldp x29, x30, [sp], #16
    .cfi_restore x30
    .cfi_restore x29
    .cfi_def_cfa_offset 0
    ret
    .cfi_endproc

_eh_managed_to_native:
    .cfi_startproc
    .cfi_personality 0x9b, _personality
    .cfi_lsda 0x10, _LSDA_mton
    stp x29, x30, [sp, #-16]!
    .cfi_def_cfa_offset 16
    .cfi_offset x30, -8
    .cfi_offset x29, -16
    mov x29, sp
    blr x9
    ldp x29, x30, [sp], #16
    .cfi_remember_state
    .cfi_restore x30
    .cfi_restore x29
    .cfi_def_cfa_offset 0
    ret
.Lemtn_landingpad:
    .cfi_restore_state
    mov x1, x0 
    bl _eh_get_exception_ptr
    str x1, [x0]
    mov x0, xzr
    ldp x29, x30, [sp], #16
    .cfi_restore x30
    .cfi_restore x29
    .cfi_def_cfa_offset 0
    ret
    .cfi_endproc

_eh_native_to_managed:
    .cfi_startproc
    .cfi_personality 0x9b, _personality
    .cfi_lsda 0x10, _LSDA_none
    stp x20, x19, [sp, #-32]!
    stp x29, x30, [sp, #16]
    .cfi_def_cfa_offset 32
    .cfi_offset x30, -8
    .cfi_offset x29, -16
    .cfi_offset x19, -24
    .cfi_offset x20, -32
    add x29, sp, #16
    mov x19, x9
    mov x20, x0
    bl _eh_get_exception_ptr
    str xzr, [x0]
    mov x0, x20
    blr x19
    mov x20, x0
    bl _eh_get_exception_ptr
    ldr x0, [x0]
    cbnz x0, .Lentm_do_rethrow
    mov x0, x20
    ldp x29, x30, [sp, #16]
    ldp x20, x19, [sp], #32
    .cfi_remember_state
    .cfi_restore x30
    .cfi_restore x29
    .cfi_restore x19
    .cfi_restore x20
    .cfi_def_cfa_offset 0
    ret
.Lentm_do_rethrow:
    .cfi_restore_state
    b __Unwind_RaiseException
    brk #1
    .cfi_endproc

_personality:
    .cfi_startproc
    .cfi_lsda 0x10, _LSDA_none
    stp x22, x21, [sp, #-48]!
    stp x20, x19, [sp, #16]
    stp x29, x30, [sp, #32]
    .cfi_def_cfa_offset 48
    .cfi_offset x30, -8
    .cfi_offset x29, -16
    .cfi_offset x19, -24
    .cfi_offset x20, -32
    .cfi_offset x21, -40
    .cfi_offset x22, -48
    add x29, sp, #32
    mov x19, x1
    mov x20, x2
    mov x21, x3
    mov x22, x4
    tbz x19, _UA_FORCE_UNWIND, .Lp_should_process
    mov x0, _URC_CONTINUE_UNWIND
    b .Lp_ret
.Lp_should_process:
    mov x0, x22
    bl __Unwind_GetLanguageSpecificData
    ldrsw x1, [x0]
    tbz x19, _UA_SEARCH_PHASE, .Lp_handler_phase
    cbz x1, .Lp_no_handler
    mov x0, _URC_HANDLER_FOUND
    b .Lp_ret
.Lp_no_handler:
    mov x0, _URC_CONTINUE_UNWIND
    b .Lp_ret
.Lp_handler_phase:
    tbz x19, _UA_HANDLER_FRAME, .Lp_no_handler
    add x1, x1, x0
    mov x0, x22
    bl __Unwind_SetIP
    mov x0, x22
    mov x1, #DW_REG_x0
    mov x2, x21
    bl __Unwind_SetGR
    mov x0, _URC_INSTALL_CONTEXT
.Lp_ret:
    ldp x29, x30, [sp, #32]
    ldp x20, x19, [sp, #16]
    ldp x22, x21, [sp], #48
    .cfi_restore x30
    .cfi_restore x29
    .cfi_restore x19
    .cfi_restore x20
    .cfi_restore x21
    .cfi_restore x22
    .cfi_def_cfa_offset 0
    ret
    .cfi_endproc
