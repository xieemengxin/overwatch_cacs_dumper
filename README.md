# Overwatch CASC Dumper

Automated toolchain to extract game data from Overwatch's CASC storage and generate version-specific SDK header files.

## Architecture

```
Game Update → keydump.dll (inject) → keydump.json
                                          ↓
                    CASC Reader (dotnet) ← Overwatch.keyring
                          ↓
                    dump_json/ (heroes, loadouts, statescript)
                          ↓
                    generate_sdk.py
                          ↓
                    hero_data_gen.hpp + var_data_gen.hpp
```

## Components

### keydump_dll/
DLL injected into the running game to extract CASC encryption keys via signature scanning.
- Finds the 512-byte CMF/TRG keytable via `AND eax, 800001FFh` pattern
- Extracts Key()/IV() algorithm constants
- Scans for Salsa20 keyring entries
- Writes `keydump.json` next to game exe, then auto-unloads

### casc_reader/
.NET console app that reads Overwatch's CASC storage and extracts structured game data.
- Decrypts CMF/TRG manifests using version-specific crypto
- Extracts hero definitions (STUHero) with names and loadouts
- Extracts ability definitions (STULoadout) with categories and buttons
- Extracts statescript graphs with node types, sync var bindings, and schema entries
- Outputs detailed JSON dumps for analysis

### scripts/
- `generate_sdk.py` — Generates C++ header files from JSON dump data

### output/
- `dump_json/` — Full JSON dumps (heroes, loadouts, statescript data)
- `hero_data_gen.hpp` — Hero names, ability mappings
- `var_data_gen.hpp` — State variable context (node types, roles, hero counts)

## Setup

```bash
# 1. Clone with submodule
git clone --recursive <this-repo>
cd overwatch_casc_dumper

# 2. Add OWLib as dependency
git submodule add https://github.com/overtools/OWLib.git deps/OWLib

# 3. Copy crypto files for your game version into OWLib
cp casc_reader/crypto/ProCMF_*.cs deps/OWLib/TACTLib/TACTLib/Core/Product/Tank/CMF/
cp casc_reader/crypto/ProTRG_*.cs deps/OWLib/TACTLib/TACTLib/Core/Product/Tank/TRG/

# 4. Build and run
cd casc_reader
dotnet build -c Release
dotnet run -c Release -- "/path/to/Overwatch" "../output/dump_json"

# 5. Generate SDK headers
cd ../scripts
python3 generate_sdk.py ../output/dump_json/ ../output/
```

## Version Update Workflow

When the game updates:
1. Inject `keydump.dll` → get new crypto keys
2. Create new `ProCMF_<version>.cs` / `ProTRG_<version>.cs` from the keys
3. Run `casc_reader` → fresh JSON dumps
4. Run `generate_sdk.py` → new SDK headers
5. Drop headers into your project

## Supported Versions

| Build | Version | Status |
|-------|---------|--------|
| 148494 | 2.22.0.0.148915N (CN) | ✅ Fully supported |

## License

For research and educational purposes only.
