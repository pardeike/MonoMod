.arch armv8-a

#if defined(__APPLE__)
.build_version macos, 12, 0 sdk_version 12, 0
.subsections_via_symbols
#endif

#if defined(__APPLE__)
#define C_FUNC(name) _##name
#define EXTERNAL_C_FUNC(name) C_FUNC(name)
#define LOCAL_LABEL(name) L##name
#define PERSONALITY(name) .cfi_personality 0x9b, name
#define LSDA(name) .cfi_lsda 0x10, name
#else
#define C_FUNC(name) name
#define EXTERNAL_C_FUNC(name) C_FUNC(name)
#define LOCAL_LABEL(name) .L##name
#define PERSONALITY(name) .cfi_personality 0x9b, _ref##name
#define LSDA(name) .cfi_lsda 0x1b, name
#endif

.macro LOAD_EXTERNAL_VAR Reg, Name
#if defined(__APPLE__)
    adrp \Reg, C_FUNC(\Name)@GOTPAGE
    ldr \Reg, [\Reg, C_FUNC(\Name)@GOTPAGEOFF]
#else
    adrp \Reg, C_FUNC(\Name)
    add \Reg, \Reg, :lo12:C_FUNC(\Name)
#endif
.endm
