// keydump.dll — inject into Overwatch, auto-extract CASC crypto keys, write JSON, self-unload.
//
// Signature-based: scans .text for CMF/TRG Key/IV functions via stable byte patterns.
// Output: keydump.json next to the game exe, containing keytable + algorithm constants.
//
// Build: cl /LD /O2 keydump.cpp /link /OUT:keydump.dll

#include <windows.h>
#include <cstdio>
#include <cstdint>
#include <cstring>
#include <string>
#include <vector>

// ============================================================================
// PE helpers
// ============================================================================

static uint8_t* GetModuleText(HMODULE mod, size_t& textSize) {
    auto dos = (IMAGE_DOS_HEADER*)mod;
    auto nt = (IMAGE_NT_HEADERS*)((uint8_t*)mod + dos->e_lfanew);
    auto sec = IMAGE_FIRST_SECTION(nt);
    for (WORD i = 0; i < nt->FileHeader.NumberOfSections; ++i, ++sec) {
        if (memcmp(sec->Name, ".text", 5) == 0) {
            textSize = sec->Misc.VirtualSize;
            return (uint8_t*)mod + sec->VirtualAddress;
        }
    }
    textSize = 0;
    return nullptr;
}

// ============================================================================
// Pattern scanner
// ============================================================================

struct ScanResult { uint8_t* addr; };

static std::vector<ScanResult> ScanPattern(uint8_t* base, size_t size, const uint8_t* pat, const char* mask, size_t patLen) {
    std::vector<ScanResult> results;
    for (size_t i = 0; i + patLen <= size; ++i) {
        bool match = true;
        for (size_t j = 0; j < patLen; ++j) {
            if (mask[j] == 'x' && base[i+j] != pat[j]) { match = false; break; }
        }
        if (match) results.push_back({base + i});
    }
    return results;
}

// Resolve RIP-relative LEA: LEA rXX, [rip+disp32]
static uint8_t* ResolveLEA(uint8_t* leaInstr) {
    // LEA with RIP-relative: 4C 8D 1D/05/0D/15/25/2D/35/3D xx xx xx xx (7 bytes)
    // or 48 8D ... (7 bytes)
    int32_t disp = *(int32_t*)(leaInstr + 3);
    return leaInstr + 7 + disp;
}

// ============================================================================
// Extract Key() function parameters
// ============================================================================

struct KeyFuncParams {
    uint8_t keytable[512];
    uint64_t keytable_rva;
    int32_t  big_constant;    // e.g. 148494
    bool     found;
};

static KeyFuncParams ExtractKeyFunc(uint8_t* textBase, size_t textSize, HMODULE mod) {
    KeyFuncParams result = {};

    // Pattern: AND eax, 800001FFh = 25 FF 01 00 80
    // This appears in Key() function loop body
    uint8_t andPat[] = { 0x25, 0xFF, 0x01, 0x00, 0x80 };
    auto hits = ScanPattern(textBase, textSize, andPat, "xxxxx", 5);

    for (auto& hit : hits) {
        // Search backwards (up to 64 bytes) for LEA rXX, [rip+disp32] to keytable
        // LEA r11, [...] = 4C 8D 1D xx xx xx xx
        for (int back = 8; back < 80; ++back) {
            uint8_t* p = hit.addr - back;
            if (p < textBase) break;
            // 4C 8D 1D = LEA r11, [rip+disp]
            // 4C 8D 05 = LEA r8, [rip+disp]
            // 48 8D 1D = LEA rbx, [rip+disp]
            if ((p[0] == 0x4C || p[0] == 0x48) && p[1] == 0x8D &&
                (p[2] == 0x1D || p[2] == 0x05 || p[2] == 0x0D || p[2] == 0x15 || p[2] == 0x2D || p[2] == 0x35)) {
                uint8_t* target = ResolveLEA(p);
                // Validate: keytable should be in .rdata (readable, 512 bytes of non-zero data)
                if (IsBadReadPtr(target, 512)) continue;
                int nonzero = 0;
                for (int i = 0; i < 512; ++i) if (target[i]) nonzero++;
                if (nonzero < 200) continue; // too sparse, not a keytable

                memcpy(result.keytable, target, 512);
                result.keytable_rva = (uint64_t)(target - (uint8_t*)mod);
                result.found = true;
                break;
            }
        }
        if (!result.found) continue;

        // Search forward from AND for MOV eax, CONSTANT (the big constant like 148494)
        // MOV eax, imm32 = B8 xx xx xx xx
        for (int fwd = 0; fwd < 64; ++fwd) {
            uint8_t* p = hit.addr + fwd;
            if (p[0] == 0xB8) { // MOV eax, imm32
                result.big_constant = *(int32_t*)(p + 1);
                if (result.big_constant > 10000 && result.big_constant < 300000) {
                    // Plausible build-related constant
                    return result;
                }
            }
        }
        if (result.found) return result; // found keytable even without big_constant
    }
    return result;
}

// ============================================================================
// Extract IV() function parameters
// ============================================================================

struct IVFuncParams {
    int32_t branch0_add;     // case 0: kidx += N (e.g. 103 or 341)
    int32_t branch1_mul;     // case 1: kidx = (N * kidx) % header_field
    int32_t branch2_sub;     // case 2: kidx -= N (e.g. 1 or 13)
    uint32_t header_offset;  // which header DWORD is used (0 for CMF, 4 for TRG)
    bool found;
};

static IVFuncParams ExtractIVFunc(uint8_t* funcAddr, size_t maxScan) {
    IVFuncParams result = {};

    // Look for the branch structure: after the %3 computation,
    // case 0: ADD r8d, IMM8 or ADD r8d, IMM32
    // case 1: LEA eax, [r8*N] (where N=4 or 7)
    // case 2: SUB r8d, IMM8 or DEC r8d

    for (size_t i = 0; i < maxScan - 4; ++i) {
        uint8_t* p = funcAddr + i;

        // ADD r8d, imm8: 41 83 C0 xx
        if (p[0] == 0x41 && p[1] == 0x83 && p[2] == 0xC0 && !result.found) {
            result.branch0_add = (int8_t)p[3];
            result.found = true;
        }

        // LEA eax, [r8*4]: 42 8D 04 85 (or similar SIB with scale)
        // LEA eax, ds:0[r8*4]: 42 8D 04 85 00 00 00 00
        if (p[0] == 0x42 && p[1] == 0x8D && p[2] == 0x04) {
            uint8_t sib = p[3];
            int scale = 1 << ((sib >> 6) & 3); // 00=1, 01=2, 10=4, 11=8
            if (scale >= 2 && scale <= 8) {
                result.branch1_mul = scale;
            }
        }

        // DEC r8d: 41 FF C8
        if (p[0] == 0x41 && p[1] == 0xFF && p[2] == 0xC8) {
            result.branch2_sub = 1;
        }

        // SUB r8d, imm8: 41 83 E8 xx
        if (p[0] == 0x41 && p[1] == 0x83 && p[2] == 0xE8) {
            result.branch2_sub = (int8_t)p[3];
        }

        // AND eax, 1FFh near start = initial index uses [r14] (offset 0)
        // AND with [r14+4] would use offset 4
    }

    // Determine header offset by checking the initial AND:
    // CMF: mov eax, [r14] → AND eax, 1FFh → uses offset 0 = m_buildVersion
    // TRG: mov eax, [r14+4] → AND eax, 1FFh → uses offset 4 = m_buildVersion (different header)
    for (size_t i = 0; i < 80; ++i) {
        uint8_t* p = funcAddr + i;
        // AND eax, 1FFh = 25 FF 01 00 00
        if (p[0] == 0x25 && p[1] == 0xFF && p[2] == 0x01 && p[3] == 0x00 && p[4] == 0x00) {
            // Look backwards for MOV eax, [rXX] or MOV eax, [rXX+4]
            for (int back = 1; back < 12; ++back) {
                uint8_t* q = p - back;
                // MOV eax, [r14] = 41 8B 06
                if (q[0] == 0x41 && q[1] == 0x8B && q[2] == 0x06) {
                    result.header_offset = 0;
                    break;
                }
                // MOV eax, [r14+4] = 41 8B 46 04
                if (q[0] == 0x41 && q[1] == 0x8B && q[2] == 0x46 && q[3] == 0x04) {
                    result.header_offset = 4;
                    break;
                }
                // mov eax, dword ptr [rdx+4]
                if (q[0] == 0x8B && q[1] == 0x42 && q[2] == 0x04) {
                    result.header_offset = 4;
                    break;
                }
                // mov eax, dword ptr [rdx]
                if (q[0] == 0x8B && q[1] == 0x02) {
                    result.header_offset = 0;
                    break;
                }
            }
            break;
        }
    }

    return result;
}

// ============================================================================
// Find CMF/TRG decrypt functions via string references
// ============================================================================

struct DecryptFuncInfo {
    uint8_t* cmf_decrypt;
    uint8_t* trg_decrypt;
    uint8_t* cmf_iv_func;
    uint8_t* trg_iv_func;
    uint8_t* shared_key_func;
};

static DecryptFuncInfo FindDecryptFunctions(uint8_t* textBase, size_t textSize, HMODULE mod) {
    DecryptFuncInfo info = {};

    // The CMF decrypt function checks: (value & 0xFFFFFF) == 0x666D63 ("cmf")
    // In assembly: AND ecx, 00FFFFFFh; CMP ecx, 666D63h
    // Search for CMP with 0x666D63
    uint8_t cmfMagic[] = { 0x63, 0x6D, 0x66, 0x00 }; // "cmf\0" LE
    // Actually search for: 81 F9 63 6D 66 00 (CMP ecx, 666D63h) or similar
    // More reliable: search for the combined pattern 66 6D 63 which is "fmc" in LE

    // For now, we use the Key function location as anchor (it's called by both CMF and TRG decrypt)
    // The Key function is the shared function we already found.
    // We trace xrefs to find the callers.

    // Simpler approach: Key function found → scan forward/backward in nearby code for CALL instructions
    // pointing to IV functions (which have IMUL 55555556h pattern)

    return info;
}

// ============================================================================
// Main dump logic
// ============================================================================

static void DumpKeys(HMODULE gameMod) {
    char logPath[MAX_PATH];
    GetModuleFileNameA(gameMod, logPath, MAX_PATH);
    // Replace exe name with keydump.json
    char* lastSlash = strrchr(logPath, '\\');
    if (lastSlash) strcpy(lastSlash + 1, "keydump.json");
    else strcpy(logPath, "keydump.json");

    FILE* log = fopen(logPath, "w");
    if (!log) return;

    size_t textSize = 0;
    uint8_t* textBase = GetModuleText(gameMod, textSize);
    if (!textBase || textSize < 1024) {
        fprintf(log, "{\"error\":\"no .text section\"}\n");
        fclose(log);
        return;
    }

    fprintf(log, "{\n");
    fprintf(log, "  \"image_base\": \"0x%llX\",\n", (unsigned long long)gameMod);
    fprintf(log, "  \"text_base_rva\": \"0x%llX\",\n", (unsigned long long)(textBase - (uint8_t*)gameMod));
    fprintf(log, "  \"text_size\": %llu,\n", (unsigned long long)textSize);

    // 1. Extract Key function (shared by CMF and TRG)
    auto keyParams = ExtractKeyFunc(textBase, textSize, gameMod);
    fprintf(log, "  \"key_func\": {\n");
    fprintf(log, "    \"found\": %s,\n", keyParams.found ? "true" : "false");
    fprintf(log, "    \"keytable_rva\": \"0x%llX\",\n", (unsigned long long)keyParams.keytable_rva);
    fprintf(log, "    \"big_constant\": %d,\n", keyParams.big_constant);
    fprintf(log, "    \"keytable\": [\n");
    for (int row = 0; row < 32; ++row) {
        fprintf(log, "      ");
        for (int col = 0; col < 16; ++col) {
            fprintf(log, "%s0x%02X", (row*16+col > 0) ? ", " : "", keyParams.keytable[row*16+col]);
        }
        fprintf(log, "%s\n", row < 31 ? "," : "");
    }
    fprintf(log, "    ]\n");
    fprintf(log, "  },\n");

    // 2. Find CMF IV function
    // Search for functions that: have IMUL 55555556h + ADD with small constant (103 or similar)
    // Pattern: IMUL with 55555556h = 69 xx 56 55 55 55 or similar
    uint8_t imulPat[] = { 0x56, 0x55, 0x55, 0x55 }; // 55555556h LE bytes (part of IMUL encoding)
    auto imulHits = ScanPattern(textBase, textSize, imulPat, "xxxx", 4);

    fprintf(log, "  \"iv_candidates\": [\n");
    bool firstIV = true;
    for (auto& hit : imulHits) {
        // Verify this is in an IMUL context
        if (hit.addr[-2] != 0xB8 && hit.addr[-1] != 0x56) continue; // not preceded by MOV eax, 55555556h
        // The IV function should be within ~200 bytes before this IMUL
        uint8_t* funcStart = hit.addr - 200;
        if (funcStart < textBase) funcStart = textBase;

        auto ivParams = ExtractIVFunc(funcStart, hit.addr - funcStart + 100);
        if (!ivParams.found) continue;

        if (!firstIV) fprintf(log, ",\n");
        firstIV = false;
        fprintf(log, "    {\n");
        fprintf(log, "      \"addr_rva\": \"0x%llX\",\n", (unsigned long long)(hit.addr - (uint8_t*)gameMod));
        fprintf(log, "      \"header_offset\": %u,\n", ivParams.header_offset);
        fprintf(log, "      \"branch0_add\": %d,\n", ivParams.branch0_add);
        fprintf(log, "      \"branch1_mul\": %d,\n", ivParams.branch1_mul);
        fprintf(log, "      \"branch2_sub\": %d\n", ivParams.branch2_sub);
        fprintf(log, "    }");
    }
    fprintf(log, "\n  ]\n");
    fprintf(log, "}\n");
    fclose(log);

    char msg[512];
    snprintf(msg, sizeof(msg), "KeyDump written to:\n%s\nKeytable found: %s\nBig constant: %d\nIV candidates: %d",
        logPath, keyParams.found ? "YES" : "NO", keyParams.big_constant, (int)imulHits.size());
    MessageBoxA(nullptr, msg, "KeyDump", MB_OK | MB_ICONINFORMATION);
}

// ============================================================================
// DLL entry
// ============================================================================

static DWORD WINAPI DumpThread(LPVOID) {
    Sleep(2000); // wait for game to fully load
    HMODULE gameMod = GetModuleHandleA(nullptr);
    DumpKeys(gameMod);
    // Self-unload
    HMODULE self;
    GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
        (LPCSTR)DumpThread, &self);
    FreeLibraryAndExitThread(self, 0);
    return 0;
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID) {
    if (reason == DLL_PROCESS_ATTACH) {
        DisableThreadLibraryCalls(hModule);
        CreateThread(nullptr, 0, DumpThread, nullptr, 0, nullptr);
    }
    return TRUE;
}
