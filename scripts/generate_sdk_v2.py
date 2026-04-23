#!/usr/bin/env python3
"""SDK v2 generator — outputs 4 files based on var dictionary + heroes/loadouts dump.

Usage:
  python3 generate_sdk_v2.py [dump_dir] [dict_path] [out_dir]

Defaults:
  dump_dir  = ../output/dump_json
  dict_path = ../blueprint_editor/output/var_dictionary.json
  out_dir   = ../output

Outputs:
  out_dir/vars.csv          — flat CSV of all named statescript vars
  out_dir/namedump.hpp      — hero_id → name + helpers
  out_dir/abilitydump.hpp   — loadout_id → name + slot enum + per-hero list + helpers
  out_dir/statevardump.hpp  — var_id → name + kind(Status/Value/Both) + domain + helpers
"""
import csv
import json
import os
import re
import sys
from collections import defaultdict


# ---------------------------------------------------------------------------
# helpers
# ---------------------------------------------------------------------------
def clean(s):
    if not s:
        return ""
    return s.replace("\x00", "").strip()


def short_desc(s, limit=120):
    """Normalize whitespace + truncate. Quote-escaping is left to cpp_str()."""
    if not s:
        return ""
    s = re.sub(r"\s+", " ", s).strip()
    if len(s) > limit:
        s = s[: limit - 1] + "…"
    return s


def cpp_str(s):
    """Escape a string for use as a C++ literal (UTF-8 中文 OK)."""
    if s is None:
        s = ""
    return '"' + s.replace("\\", "\\\\").replace('"', '\\"') + '"'


# ---------------------------------------------------------------------------
# kind classification: Status / Value / Both
# ---------------------------------------------------------------------------
# STATUS  — bool flag (true/false: 处于某状态/技能激活中/被某 buff 影响)
# VALUE   — numeric (int/float: 血量、充能进度、计时、速率)
# BOTH    — has both meaningful active-flag and numeric value (rare)

STATUS_NAMES = {
    "ABILITY_DEFLECT_PROJECTILES",
    "ABILITY_FRAME_ACTIVE_HANDLE",
    "ABILITY_FRAME_PASSIVE_HANDLE",
    "ABILITY_INPUT_ACTIVE",
    "ABILITY_E",
    "ABILITY_LMOUSE",
    "ABILITY_PASSIVE_ACTIVE",
    "ABILITY_PRIMARY_AVAILABLE",
    "ABILITY_RMOUSE",
    "ABILITY_SELF_PROTECTION_ACTIVE",
    "ABILITY_SHIFT",
    "ABILITY_ULT",
    "ABILITY_JAVELIN",
    "ZARYA_PARTICLE_BARRIER_STATE",
    "AIMING",
    "ANTI_HEAL",
    "GENERIC_INVIS",
    "HACKED",
    "HEAL_BOOST",
    "IS_ALIVE",
    "MEI_FREEZE",
    "MEI_SLOW",
    "NANO_BOOST",
    "ON_FIRE",
    "RELOADING",
    "REVEALED",
    "SLEEP",
    "SLOW",
    "STATE_PRESENCE_FLAG",
    "ULT_ACTIVE",
    "UNTARGETABLE",
    "ZEN_DISCORD",
    "ZEN_HARMONY",
    "RAMATTRA_MODE",
    "WEAPON_MODE",
}

BOTH_NAMES = {
    # vars that hold numeric value but value=0 means inactive (e.g. timers / charges)
    "HUD_ABILITY_PROGRESS_BAR",
    "HP_BUFF_GENERIC_HANDLE",
    "MAX_HP_BUFF_HANDLE",
    "JUNKER_QUEEN_CRASH_CHARGE",
    "WEAPON_CHARGE_ATTACK",
}

# Patterns for VALUE: numeric vars
VALUE_PATTERNS = [
    r"_AMOUNT",
    r"_CHARGE($|_)",
    r"_RATE",
    r"_TIME",
    r"_SPREAD",
    r"_SPEED",
    r"_DAMAGE",
    r"_GAUGE",
    r"_TARGET_ID",
    r"_OVERRIDE",
    r"_SCALAR",
    r"_VALUE",
    r"^MAX_",
    r"^CUR_",
    r"^TOTAL_",
    r"^CD_",
    r"^OVERHEAL",
    r"AMMO",
    r"SNIPER_CHARGE",
    r"WEAPON_CHARGE",
    r"WEAPON_GAUGE",
    r"BASE_HP",
    r"_DEF$",
    r"^POSITION_",
    r"REMAINING_TIME",
    r"LAST_SHOT_TIME",
    r"FIRE_CHARGE",
    r"^TEAM_ID",
    r"ENEMY_TEAM_ID",
    r"ULT_CHARGE",
    r"WEAVER_HEAL_CHARGE",
    r"ABILITY_CHARGE",
    r"ABILITY_RESERVE",
    r"HELD_SHIELD",
    r"MEI_ICICLE",
    r"JAVELIN_SPEED",
    r"KIRIKO_RUSH",
    r"BOUND_EFFECT_ASSET",
    r"EFFECT_ENTITY_REF",
    r"EFFECT_ENTITY_REF_SECONDARY",
    r"_INPUT_AMOUNT",
]


def classify_kind(name, domain, desc):
    """Return Status/Value/Both for a var."""
    if name in STATUS_NAMES:
        return "Status"
    if name in BOTH_NAMES:
        return "Both"
    for pat in VALUE_PATTERNS:
        if re.search(pat, name):
            return "Value"
    if domain in {"ABILITY_STATE", "DEBUFF_HOSTILE", "BUFF_BENEFIT", "HITBOX_DEFLECT", "LIFECYCLE"}:
        return "Status"
    if domain in {
        "HEALTH_POOL",
        "CHASE_VAR",
        "WEAPON_PARAM",
        "DAMAGE_MODIFIER",
        "ULT_CHARGE",
        "POSITION",
        "PHYSICS",
        "EFFECT",
        "SHIELD_BARRIER",
        "ABILITY_COOLDOWN",
    }:
        return "Value"
    if domain == "STACK":
        # Stack vars: sometimes flag (push 1=active), sometimes value (counter)
        # Default to Value; specific names are caught above
        return "Value"
    return "Value"


# ---------------------------------------------------------------------------
# slot button → enum name mapping
# ---------------------------------------------------------------------------
SLOT_BUTTON_MAP = {
    "主要攻击模式": "LMB",
    "辅助攻击模式": "RMB",
    "技能 1": "Shift",   # Shift / E1
    "技能 2": "E",       # E / E2
    "技能 3": "Ult",     # Q / Ultimate
    "跳跃": "Jump",
    "下蹲": "Crouch",
    "蹲姿切换": "CrouchToggle",
    "互动": "Interact",
    "快速近身攻击": "Melee",
    "装填弹药": "Reload",
    "下一武器": "NextWeapon",
    "装备武器 1": "EquipWeapon1",
    None: "None",
    "": "None",
}

CATEGORY_MAP = {
    "Weapon": "Weapon",
    "Ability": "Ability",
    "UltimateAbility": "UltimateAbility",
    "PassiveAbility": "Passive",
    "Perk": "Perk",
    "HeroStats": "HeroStats",
    "Subrole": "Subrole",
}

ALL_SLOTS = [
    "None",
    "LMB",
    "RMB",
    "Shift",
    "E",
    "Ult",
    "Passive",
    "Jump",
    "Crouch",
    "CrouchToggle",
    "Interact",
    "Melee",
    "Reload",
    "NextWeapon",
    "EquipWeapon1",
]
ALL_CATEGORIES = ["Weapon", "Ability", "UltimateAbility", "Passive", "Perk", "HeroStats", "Subrole"]
ALL_KINDS = ["Status", "Value", "Both", "Unknown"]
ALL_DOMAINS_KNOWN = [
    "ABILITY_STATE",
    "ABILITY_COOLDOWN",
    "BUFF_BENEFIT",
    "DEBUFF_HOSTILE",
    "DAMAGE_MODIFIER",
    "HITBOX_DEFLECT",
    "HEALTH_POOL",
    "SHIELD_BARRIER",
    "CHASE_VAR",
    "STACK",
    "WEAPON_PARAM",
    "ULT_CHARGE",
    "POSITION",
    "PHYSICS",
    "EFFECT",
    "UI_UX",
    "TEAM",
    "LIFECYCLE",
    "UNCLASSIFIED",
]


# ---------------------------------------------------------------------------
# Generators
# ---------------------------------------------------------------------------
HEADER_PREAMBLE = """// ============================================================================
// AUTO-GENERATED by scripts/generate_sdk_v2.py
// DO NOT EDIT BY HAND. Source of truth:
//   - blueprint_editor/output/var_dictionary.json (statescript var names)
//   - output/dump_json/heroes.json + loadouts.json (CASC dump)
// Regenerate: python3 scripts/generate_sdk_v2.py
// ============================================================================
#pragma once

#include <cstdint>
#include <cstddef>

"""

NAMESPACE_OPEN = "namespace ow {\nnamespace sdk {\n\n"
NAMESPACE_CLOSE = "\n} // namespace sdk\n} // namespace ow\n"


def gen_namedump(heroes, out_path):
    """hero_id → name (中文) + helpers."""
    playable = [h for h in heroes if h.get("is_hero")]
    playable.sort(key=lambda x: int(x["hero_id"], 16))

    L = []
    L.append(HEADER_PREAMBLE.rstrip())
    L.append("")
    L.append("// namedump.hpp — Hero ID → display name (zh-CN)")
    L.append(f"// {len(playable)} playable heroes (is_hero=true) from CASC STUHero")
    L.append("")
    L.append(NAMESPACE_OPEN.rstrip())
    L.append("")

    L.append("/// Hero metadata: id + display name (zh-CN) + attributes from CASC STUHero")
    L.append("struct HeroInfo {")
    L.append("    uint32_t    id;          // STUHero hero_id (e.g. 0x2 = Reaper)")
    L.append("    const char* name_zhCN;   // 中文名 (display)")
    L.append("    const char* gender;      // Male / Female / Other")
    L.append("    const char* size;        // Normal / Large / Small")
    L.append("    const char* color;       // hex color used by HUD")
    L.append("};")
    L.append("")

    # constexpr table
    L.append(f"inline constexpr HeroInfo kHeroes[] = {{")
    for h in playable:
        hid = int(h["hero_id"], 16)
        nm = clean(h.get("name_zhCN", ""))
        gn = clean(h.get("gender", ""))
        sz = clean(h.get("size", ""))
        cl = clean(h.get("color", ""))
        L.append(f"    {{0x{hid:X}u, {cpp_str(nm)}, {cpp_str(gn)}, {cpp_str(sz)}, {cpp_str(cl)}}},")
    L.append("};")
    L.append(f"inline constexpr std::size_t kHeroCount = {len(playable)};")
    L.append("")

    # helpers
    L.append("/// Lookup by id. Returns nullptr if not found.")
    L.append("inline const HeroInfo* FindHero(uint32_t id) noexcept {")
    L.append("    for (const auto& h : kHeroes) {")
    L.append("        if (h.id == id) return &h;")
    L.append("    }")
    L.append("    return nullptr;")
    L.append("}")
    L.append("")
    L.append("/// Get display name (zh-CN). Returns \"?\" if unknown.")
    L.append("inline const char* HeroName(uint32_t id) noexcept {")
    L.append("    const auto* h = FindHero(id);")
    L.append("    return h ? h->name_zhCN : \"?\";")
    L.append("}")
    L.append("")
    L.append("/// True iff id is a known playable hero.")
    L.append("inline bool IsPlayableHero(uint32_t id) noexcept {")
    L.append("    return FindHero(id) != nullptr;")
    L.append("}")
    L.append("")

    # iteration helpers
    L.append("/// Iterator helpers for range-for / std::for_each.")
    L.append("inline const HeroInfo* HeroesBegin() noexcept { return kHeroes; }")
    L.append("inline const HeroInfo* HeroesEnd()   noexcept { return kHeroes + kHeroCount; }")
    L.append("")

    L.append(NAMESPACE_CLOSE.lstrip())

    with open(out_path, "w", encoding="utf-8") as f:
        f.write("\n".join(L))


def detect_slot(button, category):
    """Return (slot_enum_name)."""
    if category == "PassiveAbility":
        return "Passive"
    return SLOT_BUTTON_MAP.get(button, "None")


def gen_abilitydump(heroes, loadouts, out_path):
    """loadout_id → name + category + slot enum + per-hero lookup."""
    playable = [h for h in heroes if h.get("is_hero")]
    playable.sort(key=lambda x: int(x["hero_id"], 16))

    # flatten all loadouts (loadouts.json) for the global table
    all_loadouts = sorted(loadouts, key=lambda x: int(x["loadout_id"], 16))

    L = []
    L.append(HEADER_PREAMBLE.rstrip())
    L.append("")
    L.append("// abilitydump.hpp — Loadout (ability) ID → name + slot")
    L.append(f"// {len(all_loadouts)} loadouts across {len(playable)} playable heroes")
    L.append("// Slot mapping: 主要攻击=LMB, 辅助攻击=RMB, 技能1=Shift, 技能2=E, 技能3=Ult, 被动=Passive")
    L.append("")
    L.append(NAMESPACE_OPEN.rstrip())
    L.append("")

    # AbilitySlot enum
    L.append("/// Default keybind / slot for an ability. None = no key bound (HeroStats / Subrole).")
    L.append("enum class AbilitySlot : std::uint8_t {")
    for i, s in enumerate(ALL_SLOTS):
        L.append(f"    {s} = {i},")
    L.append("};")
    L.append("")

    # AbilityCategory enum
    L.append("/// Loadout category from STULoadout.")
    L.append("enum class AbilityCategory : std::uint8_t {")
    for i, c in enumerate(ALL_CATEGORIES):
        L.append(f"    {c} = {i},")
    L.append("};")
    L.append("")

    # Ability struct
    L.append("/// One ability/loadout entry.")
    L.append("struct Ability {")
    L.append("    uint32_t        loadout_id;   // STULoadout guid_index")
    L.append("    uint32_t        hero_id;      // owning hero (0 if loadout is shared/orphan)")
    L.append("    const char*     name_zhCN;    // 中文显示名")
    L.append("    const char*     desc_zhCN;    // 简短中文描述")
    L.append("    AbilityCategory category;")
    L.append("    AbilitySlot     slot;         // 默认按键槽")
    L.append("    const char*     button_raw;   // 原始 button string (e.g. \"技能 1\")")
    L.append("    bool            hidden;       // is_hidden flag from CASC")
    L.append("};")
    L.append("")

    # Build per-hero list (we use heroes[].loadouts since they include button)
    hero_abilities = {}  # hero_id_int → list[Ability dict]
    for h in playable:
        hid = int(h["hero_id"], 16)
        items = []
        for lo in h.get("loadouts", []):
            cat = lo.get("category", "")
            if cat in {"HeroStats", "Subrole"}:
                continue  # framework loadouts, not user-facing
            slot = detect_slot(lo.get("button"), cat)
            items.append({
                "loadout_id": int(lo["guid_index"], 16),
                "hero_id": hid,
                "name": clean(lo.get("name_zhCN", "")),
                "desc": clean(lo.get("description_zhCN", "")),
                "category": CATEGORY_MAP.get(cat, "Ability"),
                "slot": slot,
                "button": lo.get("button"),
                "hidden": bool(lo.get("is_hidden", False)),
            })
        hero_abilities[hid] = items

    # Per-hero arrays
    for hid in sorted(hero_abilities.keys()):
        items = hero_abilities[hid]
        if not items:
            continue
        hname = next((clean(h["name_zhCN"]) for h in playable if int(h["hero_id"], 16) == hid), "?")
        L.append(f"// {hname} (0x{hid:X}) — {len(items)} abilities")
        L.append(f"inline constexpr Ability kAbilities_{hid:X}[] = {{")
        for ab in items:
            btn = ab["button"] if ab["button"] is not None else ""
            L.append(
                f"    {{0x{ab['loadout_id']:X}u, 0x{ab['hero_id']:X}u, "
                f"{cpp_str(ab['name'])}, {cpp_str(short_desc(ab['desc'], 60))}, "
                f"AbilityCategory::{ab['category']}, AbilitySlot::{ab['slot']}, "
                f"{cpp_str(btn)}, {'true' if ab['hidden'] else 'false'}}},"
            )
        L.append("};")
        L.append(f"inline constexpr std::size_t kAbilities_{hid:X}_count = {len(items)};")
        L.append("")

    # Master lookup: hero_id → (ptr, count)
    L.append("/// Get the abilities for a hero. Returns false if unknown hero.")
    L.append("inline bool GetHeroAbilities(uint32_t hero_id, const Ability*& out_ptr, std::size_t& out_count) noexcept {")
    L.append("    switch (hero_id) {")
    for hid in sorted(hero_abilities.keys()):
        if not hero_abilities[hid]:
            continue
        L.append(f"    case 0x{hid:X}u: out_ptr = kAbilities_{hid:X}; out_count = kAbilities_{hid:X}_count; return true;")
    L.append("    default: out_ptr = nullptr; out_count = 0; return false;")
    L.append("    }")
    L.append("}")
    L.append("")

    # Find ability by slot for a given hero
    L.append("/// Find a hero's ability bound to a specific slot. Returns nullptr if none.")
    L.append("/// Useful: e.g. FindAbilityBySlot(0x2, AbilitySlot::Shift) → Reaper Wraith Form.")
    L.append("inline const Ability* FindAbilityBySlot(uint32_t hero_id, AbilitySlot slot) noexcept {")
    L.append("    const Ability* arr = nullptr; std::size_t n = 0;")
    L.append("    if (!GetHeroAbilities(hero_id, arr, n)) return nullptr;")
    L.append("    for (std::size_t i = 0; i < n; ++i) {")
    L.append("        if (arr[i].slot == slot) return &arr[i];")
    L.append("    }")
    L.append("    return nullptr;")
    L.append("}")
    L.append("")

    # Find ability by loadout_id (global)
    L.append("/// Linear search across all heroes for a loadout_id. O(N) — for one-off lookup only.")
    L.append("inline const Ability* FindAbilityByLoadoutId(uint32_t loadout_id) noexcept {")
    L.append("    const Ability* arr = nullptr; std::size_t n = 0;")
    L.append("    // iterate every hero's table")
    L.append("    for (uint32_t hid = 0; hid < 0x600; ++hid) {")
    L.append("        if (!GetHeroAbilities(hid, arr, n)) continue;")
    L.append("        for (std::size_t i = 0; i < n; ++i) {")
    L.append("            if (arr[i].loadout_id == loadout_id) return &arr[i];")
    L.append("        }")
    L.append("    }")
    L.append("    return nullptr;")
    L.append("}")
    L.append("")

    # Slot/category names
    L.append("inline const char* SlotName(AbilitySlot s) noexcept {")
    L.append("    switch (s) {")
    for s in ALL_SLOTS:
        L.append(f"    case AbilitySlot::{s}: return \"{s}\";")
    L.append("    }")
    L.append("    return \"?\";")
    L.append("}")
    L.append("")
    L.append("inline const char* CategoryName(AbilityCategory c) noexcept {")
    L.append("    switch (c) {")
    for c in ALL_CATEGORIES:
        L.append(f"    case AbilityCategory::{c}: return \"{c}\";")
    L.append("    }")
    L.append("    return \"?\";")
    L.append("}")
    L.append("")

    L.append(NAMESPACE_CLOSE.lstrip())

    with open(out_path, "w", encoding="utf-8") as f:
        f.write("\n".join(L))


def gen_statevardump(named_vars, out_path):
    """var_id → name + kind + domain + description + helpers.

    named_vars: list of (vid_int, name, kind, domain, confidence, description) sorted by vid
    """
    named_vars = sorted(named_vars, key=lambda x: x[0])

    L = []
    L.append(HEADER_PREAMBLE.rstrip())
    L.append("")
    L.append("// statevardump.hpp — Statescript Variable ID → semantic name")
    L.append(f"// {len(named_vars)} named state variables")
    L.append("//")
    L.append("// Kind classification:")
    L.append("//   Status — bool flag (true=in this state / ability active / debuff applied)")
    L.append("//             例: ABILITY_DEFLECT_PROJECTILES, GENERIC_INVIS, HACKED, IS_ALIVE, RELOADING")
    L.append("//   Value  — numeric (int/float: HP, charge, timer, rate, position…)")
    L.append("//             例: CUR_HP_HEALTH, MAX_HP_SHIELD, SNIPER_CHARGE, REMAINING_TIME")
    L.append("//   Both   — meaningful as both flag (value!=0) AND numeric value")
    L.append("//             例: HUD_ABILITY_PROGRESS_BAR, HP_BUFF_GENERIC_HANDLE")
    L.append("//")
    L.append("// var_id 是 statescript 哈希表中的 16-bit 键。读取路径(论坛实证):")
    L.append("//   Component 0x37 (Statescript) + 0x110     → pStateScriptsBase")
    L.append("//   pStateScriptsBase[ContainerID]           → StateScript instance")
    L.append("//   StateScript + 0x4A0                      → bucket_table[16]")
    L.append("//   bucket = table + (var_id & 0xF + 1)*0x20 → linked-list of StateVar*")
    L.append("//   StateVar + 0x48 → bool active, +0x60 → current value (frame-checked)")
    L.append("")
    L.append(NAMESPACE_OPEN.rstrip())
    L.append("")

    # Kind enum
    L.append("/// Whether the var carries a bool state, a numeric value, or both.")
    L.append("enum class StateVarKind : std::uint8_t {")
    for i, k in enumerate(ALL_KINDS):
        L.append(f"    {k} = {i},")
    L.append("};")
    L.append("")

    # Domain enum
    L.append("/// High-level category of the var (from analyzer.classify_domain).")
    L.append("enum class StateVarDomain : std::uint8_t {")
    for i, d in enumerate(ALL_DOMAINS_KNOWN):
        L.append(f"    {d} = {i},")
    L.append("};")
    L.append("")

    # Struct
    L.append("struct StateVar {")
    L.append("    uint32_t        var_id;       // statescript key (20+ bits observed)")
    L.append("    const char*     name;          // SCREAMING_SNAKE_CASE identifier")
    L.append("    StateVarKind    kind;          // Status / Value / Both")
    L.append("    StateVarDomain  domain;        // category")
    L.append("    const char*     desc_zhCN;     // 简短描述 (truncated)")
    L.append("};")
    L.append("")

    # Sorted table
    L.append(f"inline constexpr StateVar kStateVars[] = {{")
    for vid_int, name, kind, domain, conf, desc in named_vars:
        L.append(
            f"    {{0x{vid_int:X}u, {cpp_str(name)}, "
            f"StateVarKind::{kind}, StateVarDomain::{domain}, "
            f"{cpp_str(short_desc(desc))}}},"
        )
    L.append("};")
    L.append(f"inline constexpr std::size_t kStateVarCount = {len(named_vars)};")
    L.append("")

    # binary search since ids are sorted
    L.append("/// Binary search by var_id. Returns nullptr if not named.")
    L.append("inline const StateVar* FindStateVar(uint32_t var_id) noexcept {")
    L.append("    std::size_t lo = 0, hi = kStateVarCount;")
    L.append("    while (lo < hi) {")
    L.append("        std::size_t mid = (lo + hi) / 2;")
    L.append("        if (kStateVars[mid].var_id == var_id) return &kStateVars[mid];")
    L.append("        if (kStateVars[mid].var_id < var_id) lo = mid + 1;")
    L.append("        else hi = mid;")
    L.append("    }")
    L.append("    return nullptr;")
    L.append("}")
    L.append("")
    L.append("inline const char* StateVarName(uint32_t var_id) noexcept {")
    L.append("    const auto* v = FindStateVar(var_id);")
    L.append("    return v ? v->name : \"?\";")
    L.append("}")
    L.append("")
    L.append("inline bool IsKnownStateVar(uint32_t var_id) noexcept {")
    L.append("    return FindStateVar(var_id) != nullptr;")
    L.append("}")
    L.append("")
    L.append("inline bool IsStatusVar(uint32_t var_id) noexcept {")
    L.append("    const auto* v = FindStateVar(var_id);")
    L.append("    return v && (v->kind == StateVarKind::Status || v->kind == StateVarKind::Both);")
    L.append("}")
    L.append("")
    L.append("inline bool IsValueVar(uint32_t var_id) noexcept {")
    L.append("    const auto* v = FindStateVar(var_id);")
    L.append("    return v && (v->kind == StateVarKind::Value || v->kind == StateVarKind::Both);")
    L.append("}")
    L.append("")
    L.append("inline const char* KindName(StateVarKind k) noexcept {")
    L.append("    switch (k) {")
    for k in ALL_KINDS:
        L.append(f"    case StateVarKind::{k}: return \"{k}\";")
    L.append("    }")
    L.append("    return \"?\";")
    L.append("}")
    L.append("")
    L.append("inline const char* DomainName(StateVarDomain d) noexcept {")
    L.append("    switch (d) {")
    for d in ALL_DOMAINS_KNOWN:
        L.append(f"    case StateVarDomain::{d}: return \"{d}\";")
    L.append("    }")
    L.append("    return \"?\";")
    L.append("}")
    L.append("")

    # iteration helpers
    L.append("inline const StateVar* StateVarsBegin() noexcept { return kStateVars; }")
    L.append("inline const StateVar* StateVarsEnd()   noexcept { return kStateVars + kStateVarCount; }")
    L.append("")

    L.append(NAMESPACE_CLOSE.lstrip())

    with open(out_path, "w", encoding="utf-8") as f:
        f.write("\n".join(L))


def gen_csv(named_vars, out_path):
    """Flat CSV of all named state vars."""
    with open(out_path, "w", encoding="utf-8", newline="") as f:
        w = csv.writer(f)
        w.writerow(["var_id_hex", "var_id_dec", "name", "kind", "domain", "confidence", "description"])
        for vid_int, name, kind, domain, conf, desc in sorted(named_vars, key=lambda x: x[0]):
            w.writerow([f"0x{vid_int:X}", vid_int, name, kind, domain, conf, desc])


# ---------------------------------------------------------------------------
# entitydump (non-playable entities: turrets / projectiles / summons / bots)
# ---------------------------------------------------------------------------
ENTITY_TYPES = ["Turret", "Projectile", "Summon", "Bot", "Decoy", "Other"]

# Name-based heuristic classification
ENTITY_TURRET_KW = ["炮台", "塔", "炮"]
ENTITY_PROJECTILE_KW = ["雷", "陷阱", "弹", "炸药", "轮胎", "鱼雷", "炸", "轰炸"]
ENTITY_SUMMON_KW = ["鲍勃", "克隆"]
ENTITY_BOT_KW = ["机器人", "僵尸", "归零", "猛兽", "机兵", "TS-1", "雷吉", "阿尔伯特", "克莱尔",
                 "切割者", "抑制者", "飞空者", "飞行机", "追猎者", "轰炮者", "轰击者",
                 "冲锋者", "秃鹫", "泰坦", "榴弹", "标准", "重装", "攻击", "治疗", "近战",
                 "训练", "友方"]
ENTITY_DECOY_KW = ["敌方"]


def classify_entity_type(name):
    """Heuristic name-based entity type classification."""
    if not name:
        return "Other"
    # Order matters: more specific first
    for kw in ENTITY_PROJECTILE_KW:
        if kw in name:
            return "Projectile"
    for kw in ENTITY_TURRET_KW:
        if kw in name:
            return "Turret"
    for kw in ENTITY_SUMMON_KW:
        if kw in name:
            return "Summon"
    for kw in ENTITY_DECOY_KW:
        if kw in name:
            return "Decoy"
    for kw in ENTITY_BOT_KW:
        if kw in name:
            return "Bot"
    return "Other"


# ---------------------------------------------------------------------------
# CSV override loader — manual patches that win over auto-dump.
# ---------------------------------------------------------------------------
def _parse_int(s, default=0):
    if s is None: return default
    s = s.strip()
    if not s or s == "*": return default
    try: return int(s, 16) if s.lower().startswith("0x") else int(s)
    except ValueError: return default


def _parse_strip_quotes(s):
    """Strip surrounding whitespace + a single pair of quotes."""
    if s is None: return ""
    s = s.strip()
    if len(s) >= 2 and s[0] == s[-1] and s[0] in ('"', "'"):
        s = s[1:-1]
    return s


def load_csv_overrides(path, required_key="entity_id"):
    """Generic CSV loader — returns list of dicts, skipping commented/empty lines.

    The first non-comment row is treated as the header. Cells containing only "*"
    are treated as "inherit / skip" sentinels (caller decides).
    """
    if not os.path.exists(path):
        return []
    rows = []
    with open(path, encoding="utf-8") as f:
        reader = csv.reader(f)
        header = None
        for raw in reader:
            # Skip empty rows and comment-only rows (first cell starts with #)
            if not raw: continue
            first = (raw[0] or "").strip()
            if not first: continue
            if first.startswith("#"): continue
            if header is None:
                header = [c.strip().strip("#").strip() for c in raw]
                continue
            row = {}
            for i, col in enumerate(header):
                if i >= len(raw): break
                row[col] = _parse_strip_quotes(raw[i])
            if row.get(required_key): rows.append(row)
    return rows


def load_entity_overrides_csv(path):
    """Load entities CSV → {entity_id_int: partial_entry_dict}.
    Cells with "*" are NOT set on the partial (so auto-dump value stays)."""
    out = {}
    if not os.path.exists(path):
        return out
    rows = load_csv_overrides(path, required_key="entity_id")
    for r in rows:
        eid = _parse_int(r.get("entity_id"), 0)
        if not eid: continue
        entry = {}
        if r.get("name") and r["name"] != "*": entry["name"] = r["name"]
        if r.get("type") and r["type"] != "*": entry["type"] = r["type"]
        if r.get("owner_hero_id") and r["owner_hero_id"] != "*":
            entry["hero_id"] = _parse_int(r["owner_hero_id"])
        if r.get("owner_hero_name") and r["owner_hero_name"] != "*":
            entry["hero_name"] = r["owner_hero_name"]
        if r.get("loadout_id") and r["loadout_id"] != "*":
            entry["loadout_id"] = _parse_int(r["loadout_id"])
        if r.get("loadout_name") and r["loadout_name"] != "*":
            entry["loadout_name"] = r["loadout_name"]
        if r.get("button_raw") and r["button_raw"] != "*":
            entry["button_raw"] = r["button_raw"]
        if r.get("slot") and r["slot"] != "*":
            entry["slot"] = r["slot"]
        out[eid] = entry
    return out


def load_statevar_overrides_csv(path):
    """Load statevars CSV → {var_id_int: {name, kind, domain}}."""
    out = {}
    if not os.path.exists(path):
        return out
    rows = load_csv_overrides(path, required_key="var_id")
    for r in rows:
        vid = _parse_int(r.get("var_id"), 0)
        if not vid: continue
        entry = {}
        if r.get("name") and r["name"] != "*": entry["name"] = r["name"]
        if r.get("kind") and r["kind"] != "*": entry["kind"] = r["kind"]
        if r.get("domain") and r["domain"] != "*": entry["domain"] = r["domain"]
        out[vid] = entry
    return out


def load_hero_overrides_csv(path):
    out = {}
    if not os.path.exists(path):
        return out
    rows = load_csv_overrides(path, required_key="hero_id")
    for r in rows:
        hid = _parse_int(r.get("hero_id"), 0)
        if not hid: continue
        entry = {}
        if r.get("name") and r["name"] != "*": entry["name"] = r["name"]
        out[hid] = entry
    return out


def build_entity_name_overrides(heroes, abilities, entity_origins, loadouts=None):
    """Build entity_id_int → rich binding dict from CASC-dumped data.

    Each entry is:
      {
        "name": "死神_恐怖扳机_Projectile",  # display name
        "hero_id": 0x2,                      # 0 if not hero-bound
        "hero_name": "死神",
        "loadout_id": 0xE82,                 # 0 if no specific ability
        "loadout_name": "恐怖扳机",
        "button_raw": "辅助攻击模式",         # original CASC button text
        "slot": "RMB",                       # normalized: LMB/RMB/Shift/E/Ult/Passive/None
      }

    Sources in priority order:
      1. hero.gameplay_entity → 3P pawn
      2. abilities.json weapon_volleys / create_entities with associated_loadout_id
      3. entity_origins.json (slot-based fallback when no loadout association)
      4. OWLib GUIDNames.csv (rare, last resort)
    """
    overrides = {}
    hero_by_id = {int(h["hero_id"], 16): clean(h.get("name_zhCN") or "")
                  for h in heroes if clean(h.get("name_zhCN") or "")}
    loadout_button_by_id = {}
    loadout_category_by_id = {}
    if loadouts:
        for l in loadouts:
            try: lid = int(l["loadout_id"], 16)
            except (ValueError, KeyError): continue
            loadout_button_by_id[lid] = l.get("button") or ""
            loadout_category_by_id[lid] = l.get("category") or ""

    def entry(name, hero_id=0, hero_name="", loadout_id=0, loadout_name="", button_raw="", category=""):
        return {
            "name": name,
            "hero_id": hero_id,
            "hero_name": hero_name,
            "loadout_id": loadout_id,
            "loadout_name": loadout_name,
            "button_raw": button_raw,
            "slot": detect_slot(button_raw, category) if (button_raw or category) else "None",
        }

    # 1. Hero gameplay_entity → "<HeroName>_Pawn"
    for h in heroes:
        if not h.get("is_hero"): continue
        hid = int(h["hero_id"], 16)
        hname = hero_by_id.get(hid, "")
        ge = h.get("gameplay_entity") or {}
        eid_str = ge.get("entity_guid") or ""
        try:
            eid = int(eid_str, 16) if eid_str.startswith("0x") else int(eid_str)
            if hname and eid:
                overrides[eid] = entry(f"{hname}_Pawn", hero_id=hid, hero_name=hname)
        except (ValueError, TypeError):
            pass

    # 2. abilities.json — associated_loadout_id+name on each graph. Best data.
    if abilities:
        for h in abilities:
            hid = coerce_id(h.get("hero_id"))
            if hid is None or hid not in hero_by_id: continue
            hname = hero_by_id[hid]
            for g in h.get("graphs", []):
                slot_idx = g.get("slot_index", -1)
                lo_id_str = g.get("associated_loadout_id")
                lo_name = g.get("associated_loadout_name") or ""
                lo_id = 0
                if lo_id_str:
                    try: lo_id = int(lo_id_str, 16)
                    except ValueError: pass
                button_raw = loadout_button_by_id.get(lo_id, "") if lo_id else ""
                category = loadout_category_by_id.get(lo_id, "") if lo_id else ""

                for wv in g.get("weapon_volleys", []):
                    pid_str = wv.get("projectile_entity_id")
                    if not pid_str: continue
                    try: pid = int(pid_str, 16)
                    except (ValueError, TypeError): continue
                    if pid in overrides: continue
                    name_str = f"{hname}_{lo_name}_Projectile" if lo_name else f"{hname}_slot{slot_idx}_Projectile"
                    overrides[pid] = entry(name_str, hero_id=hid, hero_name=hname,
                                           loadout_id=lo_id, loadout_name=lo_name,
                                           button_raw=button_raw, category=category)
                for ce in g.get("create_entities", []):
                    eid_str = ce.get("entity_id")
                    if not eid_str: continue
                    try: eid = int(eid_str, 16)
                    except (ValueError, TypeError): continue
                    if eid in overrides: continue
                    name_str = f"{hname}_{lo_name}_Spawned" if lo_name else f"{hname}_slot{slot_idx}_Spawned"
                    overrides[eid] = entry(name_str, hero_id=hid, hero_name=hname,
                                           loadout_id=lo_id, loadout_name=lo_name,
                                           button_raw=button_raw, category=category)

    # 3. entity_origins.json — catches refs we didn't cover (e.g. CosmeticEntity subclass fields).
    if entity_origins:
        for e in entity_origins:
            try: eid = int(e["entity_id"], 16)
            except (ValueError, KeyError): continue
            if eid in overrides: continue
            cand = e.get("name_candidate")
            if not cand: continue
            # Extract hero from origins
            orig_hero_id = 0
            orig_hero_name = ""
            origins = e.get("origins") or []
            if origins:
                first = origins[0]
                hi_str = first.get("hero_id") or ""
                try: orig_hero_id = int(hi_str, 16) if hi_str.startswith("0x") else int(hi_str)
                except (ValueError, TypeError): orig_hero_id = 0
                orig_hero_name = first.get("hero_name") or ""
            overrides[eid] = entry(cand, hero_id=orig_hero_id, hero_name=orig_hero_name)

    # 4. OWLib GUIDNames.csv — last-resort for entries the game data didn't label.
    guid_path = "deps/OWLib/DataTool/Static/GUIDNames.csv"
    if os.path.exists(guid_path):
        for line in open(guid_path, encoding="utf-8"):
            parts = [p.strip() for p in line.split(",")]
            if len(parts) < 3: continue
            try:
                idx = int(parts[0], 16)
                tp = int(parts[1], 16)
            except ValueError: continue
            if tp != 0x003: continue
            name = parts[2]
            if idx not in overrides:
                overrides[idx] = entry(name)

    return overrides


def gen_entitydump(heroes, out_path, all_entities=None, name_overrides=None):
    """Non-playable entities — uses two sources:
    1. all_entities.json (full STUEntityDefinition scan, type 0x003: 1957 filtered entities)
       Each has a `tag` field: Projectile / Pawn / Scripted / Destructible / Other
    2. heroes.json non-hero entries (named STUHero with is_hero=false: PVE units)

    Output deduplicates by entity id. all_entities takes precedence.
    """
    # Map all_entities tag → our EntityType
    TAG_TO_TYPE = {
        "Projectile": "Projectile",
        "Pawn": "Bot",         # Pawn = Bot/Turret/Summon (need finer split with m_isBot)
        "Scripted": "Other",   # Has logic but no health (effects/spawners)
        "Destructible": "Bot", # Has health, no logic
        "Other": "Other",
    }

    classified = []
    seen = set()
    overrides = name_overrides or {}

    # Helper: pull a binding dict out of overrides. Each entry has name + hero + loadout + slot.
    def _binding_for(eid, name_fallback=""):
        b = overrides.get(eid) if isinstance(overrides.get(eid), dict) else None
        if b: return b
        # legacy string override (shouldn't happen after refactor, but keep safe)
        if isinstance(overrides.get(eid), str):
            return {"name": overrides[eid], "hero_id": 0, "hero_name": "",
                    "loadout_id": 0, "loadout_name": "", "button_raw": "", "slot": "None"}
        return {"name": name_fallback, "hero_id": 0, "hero_name": "",
                "loadout_id": 0, "loadout_name": "", "button_raw": "", "slot": "None"}

    def _pick_type(b, default):
        """Honor __type_override__ on a binding; otherwise return default."""
        if isinstance(b, dict) and b.get("__type_override__") in ENTITY_TYPES:
            return b["__type_override__"]
        return default

    # Source 1: full all_entities.json — but ONLY keep entries that have a real name.
    if all_entities:
        for e in all_entities:
            try: eid = int(e.get("entity_id", "0x0"), 16)
            except (ValueError, TypeError): continue
            extracted_name = clean(e.get("name_inferred") or "")
            ent_tag = e.get("tag") or "Other"
            if eid in overrides:
                b = _binding_for(eid)
            elif extracted_name:
                b = _binding_for(eid, extracted_name)
            else:
                continue
            etype = _pick_type(b, TAG_TO_TYPE.get(ent_tag, classify_entity_type(b["name"])))
            if eid in seen: continue
            seen.add(eid)
            classified.append((eid, b, etype, e))

    # Source 2: heroes.json non-hero entries (PVE units with proper names).
    for h in heroes:
        if h.get("is_hero"): continue
        name = clean(h.get("name_zhCN") or "")
        if not name: continue
        eid = int(h["hero_id"], 16)
        if eid in seen:
            for i, (e_id, _, _, _) in enumerate(classified):
                if e_id == eid:
                    etype = classify_entity_type(name)
                    classified[i] = (eid, _binding_for(eid, name), etype, h)
                    break
            continue
        seen.add(eid)
        etype = classify_entity_type(name)
        classified.append((eid, _binding_for(eid, name), etype, h))

    # Source 3: name-override entries not yet classified (OWLib static CSV, user CSV).
    for eid, b in (overrides or {}).items():
        if eid in seen: continue
        # Handle legacy string values defensively.
        if isinstance(b, str):
            b = _binding_for(eid, b)
        name = b.get("name", "")
        if not name: continue
        seen.add(eid)
        forced_type = "Projectile"
        if any(k in name for k in ["Turret", "Pylon", "Drone", "BOB"]): forced_type = "Bot"
        elif "MEKA" in name or "PlayerModel" in name: forced_type = "Bot"
        elif name.endswith("_Pawn"): forced_type = "Bot"
        elif "Wall" in name or "Barrier" in name: forced_type = "Other"
        forced_type = _pick_type(b, forced_type)
        classified.append((eid, b, forced_type, {"_synthetic": True}))

    classified.sort(key=lambda x: x[0])

    L = []
    L.append(HEADER_PREAMBLE.rstrip())
    L.append("")
    L.append("// entitydump.hpp — Non-playable entities (turrets, projectiles, summons, AI bots)")
    L.append(f"// {len(classified)} entries, classified by name heuristic")
    L.append("//")
    L.append("// Use cases:")
    L.append("//   - aimbot: skip target if entity is friendly turret/summon")
    L.append("//   - ESP: filter out PvE bot mode entities")
    L.append("//   - projectile detection: identify thrown ult entities (轮胎/雷/炸药)")
    L.append("")
    L.append(NAMESPACE_OPEN.rstrip())
    L.append("")

    L.append("/// Coarse-grained entity classification.")
    L.append("enum class EntityType : std::uint8_t {")
    for i, t in enumerate(ENTITY_TYPES):
        L.append(f"    {t} = {i},")
    L.append("};")
    L.append("")

    L.append("struct EntityInfo {")
    L.append("    uint32_t    id;              // STU entity definition id")
    L.append("    const char* name_zhCN;       // 中文名")
    L.append("    EntityType  type;            // heuristic classification")
    L.append("    uint32_t    owner_hero_id;   // 0 if not hero-bound")
    L.append("    const char* owner_hero_name; // '' if not hero-bound")
    L.append("    uint32_t    loadout_id;      // 0 if no specific ability")
    L.append("    const char* loadout_name;    // ability 中文名 (e.g. \"恐怖扳机\")")
    L.append("    const char* button_raw;      // raw CASC button text (e.g. \"辅助攻击模式\")")
    L.append("    const char* slot;            // LMB/RMB/Shift/E/Ult/Passive/None — normalized keybind slot")
    L.append("};")
    L.append("")

    # Sorted constexpr table
    L.append("inline constexpr EntityInfo kNonHeroEntities[] = {")
    for eid, b, etype, _ in classified:
        L.append(
            f"    {{0x{eid:X}u, {cpp_str(b['name'])}, EntityType::{etype}, "
            f"0x{b.get('hero_id', 0):X}u, {cpp_str(b.get('hero_name', ''))}, "
            f"0x{b.get('loadout_id', 0):X}u, {cpp_str(b.get('loadout_name', ''))}, "
            f"{cpp_str(b.get('button_raw', ''))}, {cpp_str(b.get('slot', 'None'))}}},"
        )
    L.append("};")
    L.append(f"inline constexpr std::size_t kNonHeroEntityCount = {len(classified)};")
    L.append("")

    # Helpers
    L.append("/// Linear lookup by id. (50–100 entries, O(N) is fine).")
    L.append("inline const EntityInfo* FindNonHeroEntity(uint32_t id) noexcept {")
    L.append("    for (const auto& e : kNonHeroEntities) {")
    L.append("        if (e.id == id) return &e;")
    L.append("    }")
    L.append("    return nullptr;")
    L.append("}")
    L.append("")
    L.append("inline const char* NonHeroEntityName(uint32_t id) noexcept {")
    L.append("    const auto* e = FindNonHeroEntity(id);")
    L.append("    return e ? e->name_zhCN : nullptr;")
    L.append("}")
    L.append("")
    L.append("inline bool IsNonHeroEntity(uint32_t id) noexcept { return FindNonHeroEntity(id) != nullptr; }")
    L.append("")
    L.append("inline bool IsTurretEntity(uint32_t id) noexcept {")
    L.append("    const auto* e = FindNonHeroEntity(id); return e && e->type == EntityType::Turret;")
    L.append("}")
    L.append("inline bool IsProjectileEntity(uint32_t id) noexcept {")
    L.append("    const auto* e = FindNonHeroEntity(id); return e && e->type == EntityType::Projectile;")
    L.append("}")
    L.append("inline bool IsSummonEntity(uint32_t id) noexcept {")
    L.append("    const auto* e = FindNonHeroEntity(id); return e && e->type == EntityType::Summon;")
    L.append("}")
    L.append("inline bool IsBotEntity(uint32_t id) noexcept {")
    L.append("    const auto* e = FindNonHeroEntity(id); return e && e->type == EntityType::Bot;")
    L.append("}")
    L.append("")
    L.append("/// Iterate all entities spawned by this hero. Caller supplies a functor.")
    L.append("template <typename F> inline void ForEachEntityOfHero(uint32_t hero_id, F&& fn) noexcept {")
    L.append("    for (const auto& e : kNonHeroEntities) if (e.owner_hero_id == hero_id) fn(e);")
    L.append("}")
    L.append("")
    L.append("/// Find the first entity spawned by this hero on a given keybind slot.")
    L.append("/// e.g. FindEntityBySlot(0x2, \"Ult\") → \"死亡绽放\" spawn (if any).")
    L.append("inline const EntityInfo* FindEntityBySlot(uint32_t hero_id, const char* slot) noexcept {")
    L.append("    if (!slot) return nullptr;")
    L.append("    for (const auto& e : kNonHeroEntities) {")
    L.append("        if (e.owner_hero_id != hero_id) continue;")
    L.append("        if (e.slot[0] != slot[0]) continue;   // fast first-char reject")
    L.append("        // exact match (ASCII-safe)")
    L.append("        const char* a = e.slot; const char* b = slot;")
    L.append("        while (*a && *a == *b) { ++a; ++b; }")
    L.append("        if (*a == 0 && *b == 0) return &e;")
    L.append("    }")
    L.append("    return nullptr;")
    L.append("}")
    L.append("")
    L.append("/// Quick counters for a hero: number of entity bindings per slot.")
    L.append("inline std::size_t CountHeroEntities(uint32_t hero_id) noexcept {")
    L.append("    std::size_t n = 0;")
    L.append("    for (const auto& e : kNonHeroEntities) if (e.owner_hero_id == hero_id) ++n;")
    L.append("    return n;")
    L.append("}")
    L.append("")
    L.append("inline const char* EntityTypeName(EntityType t) noexcept {")
    L.append("    switch (t) {")
    for t in ENTITY_TYPES:
        L.append(f"    case EntityType::{t}: return \"{t}\";")
    L.append("    }")
    L.append("    return \"?\";")
    L.append("}")
    L.append("")
    L.append("inline const EntityInfo* NonHeroEntitiesBegin() noexcept { return kNonHeroEntities; }")
    L.append("inline const EntityInfo* NonHeroEntitiesEnd()   noexcept { return kNonHeroEntities + kNonHeroEntityCount; }")
    L.append("")

    # Note: forum-known hardcoded map has been retired — all entity names now come from
    # abilities.json (hero graph → spawned entity) + entity_origins.json (reverse-traced
    # CreateEntity / WeaponVolley / CosmeticEntity refs). See build_entity_name_overrides().
    L.append("/// AnyEntityName is retained for compat. Now just wraps FindNonHeroEntity.")
    L.append("inline const char* AnyEntityName(uint32_t id) noexcept {")
    L.append("    if (const auto* e = FindNonHeroEntity(id)) return e->name_zhCN;")
    L.append("    return nullptr;")
    L.append("}")
    L.append("")

    L.append(NAMESPACE_CLOSE.lstrip())

    with open(out_path, "w", encoding="utf-8") as f:
        f.write("\n".join(L))


# ---------------------------------------------------------------------------
# weapondump (per-graph WeaponVolley ballistic data)
# ---------------------------------------------------------------------------
WEAPON_FIELDS = {
    # field path → semantic key
    "m_projectileSpeed": "speed",
    "m_projectileLifetime": "lifetime",
    "m_numProjectilesPerShot": "pellets",
    "m_numShotsPerSecond": "fire_rate",
    "m_projectileEntity.m_entityDef": "proj_entity",
    "m_aimID": "aim_id",
    "m_FEC435C6": "spread_or_size",  # often appears as projectile size
}


def coerce_int(v):
    """Return int value or -1 if dynamic/unknown."""
    if isinstance(v, bool):
        return int(v)
    if isinstance(v, (int, float)):
        return int(v)
    if isinstance(v, str) and v.startswith("0x"):
        try:
            return int(v, 16)
        except ValueError:
            return -1
    return -1  # "(bound)" or anything else


def coerce_id(v):
    """Coerce hero_id/graph_index that may be int or hex string."""
    if isinstance(v, int):
        return v
    if isinstance(v, str):
        try:
            return int(v, 16) if v.startswith("0x") else int(v)
        except ValueError:
            return None
    return None


def classify_ballistic(data):
    """Return BallisticType based on extracted fields.

    Heuristic:
      - projectile_entity present (any value) → Projectile (entity is the spawned projectile)
      - projectile_lifetime > 0 (or dynamic with field present) → Projectile (hitscan has no lifetime)
      - projectile_speed literal > 0 → Projectile
      - projectile_speed literal == 0 → HitScan
      - has fire_rate but none of speed/lifetime/proj_entity → HitScan
    """
    has_proj_entity = data.get("proj_entity") is not None and data.get("proj_entity") != ""
    has_speed_field = data.get("speed") is not None
    has_lifetime_field = data.get("lifetime") is not None
    speed_val = coerce_int(data.get("speed"))
    lifetime_val = coerce_int(data.get("lifetime"))

    if has_proj_entity:
        return "Projectile"
    # lifetime field implies projectile system (hitscan has no flight time)
    if has_lifetime_field and (lifetime_val > 0 or lifetime_val == -1):
        return "Projectile"
    if has_speed_field and speed_val > 0:
        return "Projectile"
    if has_speed_field and speed_val == 0:
        return "HitScan"
    # No projectile-related field at all
    if not has_speed_field and not has_lifetime_field and data.get("fire_rate") is not None:
        return "HitScan"
    return "Unknown"


BALLISTIC_TYPES = ["HitScan", "Projectile", "Beam", "Melee", "Unknown"]


def gen_weapondump(heroes, graph_nodes, out_path):
    """Per-(hero, graph) weapon ballistic data extracted from STUStatescriptStateWeaponVolley."""

    # hero_id_int → name
    hero_name = {int(h["hero_id"], 16): clean(h.get("name_zhCN", "")) for h in heroes if h.get("is_hero")}

    # hero_id_int → list of (loadout_id, name, slot, category) for Weapon + UltimateAbility (deflectable)
    hero_weapons = {}  # for cross-ref summary
    for h in heroes:
        if not h.get("is_hero"):
            continue
        hid = int(h["hero_id"], 16)
        items = []
        for lo in h.get("loadouts", []):
            cat = lo.get("category", "")
            if cat in {"HeroStats", "Subrole", "PassiveAbility", "Perk"}:
                continue
            slot = detect_slot(lo.get("button"), cat)
            items.append({
                "loadout_id": int(lo["guid_index"], 16),
                "name": clean(lo.get("name_zhCN", "")),
                "slot": slot,
                "category": cat,
            })
        hero_weapons[hid] = items

    weapons = []  # list of dicts

    def _emit(hid, gidx, ni, node_type, bt, data, proj_entity_raw):
        weapons.append({
            "hero_id": hid,
            "hero_name": hero_name.get(hid, "?"),
            "graph_index": gidx,
            "node_index": ni,
            "node_type": node_type,
            "ballistic": bt,
            "speed": coerce_int(data.get("speed")),
            "lifetime": coerce_int(data.get("lifetime")),
            "pellets": coerce_int(data.get("pellets")),
            "fire_rate": coerce_int(data.get("fire_rate")),
            "proj_entity": proj_entity_raw or "",
        })

    for h in graph_nodes:
        hid = coerce_id(h.get("hero_id"))
        if hid is None or hid not in hero_name:
            continue
        for g in h.get("graphs", []):
            gidx = coerce_id(g.get("graph_index"))
            if gidx is None:
                continue
            for ni, n in enumerate(g.get("node_var_refs", [])):
                nt = n.get("node_type", "")
                cfg = n.get("config", [])
                if not isinstance(cfg, list):
                    cfg = []

                # --- STUStatescriptStateWeaponVolley — traditional volley weapon ---
                if "WeaponVolley" in nt:
                    data = {}
                    proj_entity_raw = None
                    for f in cfg:
                        fn = f.get("field", "")
                        if fn in WEAPON_FIELDS and "." not in fn.split(".m_")[0]:
                            data[WEAPON_FIELDS[fn]] = f.get("value")
                        if fn == "m_projectileEntity.m_entityDef":
                            proj_entity_raw = f.get("value")
                    if proj_entity_raw is not None:
                        data["proj_entity"] = proj_entity_raw
                    if data:
                        bt = classify_ballistic(data)
                        _emit(hid, gidx, ni, "WeaponVolley", bt, data, proj_entity_raw)
                    continue

                # --- STUStatescriptActionEffect — spawn-effect / ult-projectile ---
                # Filter to nodes that WRITE a var (= they publish a spawned-entity handle)
                if nt == "STUStatescriptActionEffect":
                    writes = n.get("writes", [])
                    if not writes:
                        continue
                    # entity-ref target: first written var id (often 0xB092 GENERIC_EFFECT_ENTITY_REF, 0x214, 0xA655)
                    proj_entity_raw = writes[0].get("identifier_index", "") if writes else ""
                    _emit(hid, gidx, ni, "ActionEffect", "Projectile", {}, proj_entity_raw)
                    continue

                # --- STUStatescriptStateTrackTargets — beam-style continuous weapon ---
                # Examples: Mercy heal/damage beam, Symmetra primary beam, Moira grasp beam
                if nt == "STUStatescriptStateTrackTargets":
                    _emit(hid, gidx, ni, "TrackTargets", "Beam", {}, "")
                    continue

    weapons.sort(key=lambda x: (x["hero_id"], x["graph_index"], x["node_index"]))

    L = []
    L.append(HEADER_PREAMBLE.rstrip())
    L.append("")
    L.append("// weapondump.hpp — Per-(hero, graph, node) weapon ballistic data")
    L.append(f"// {len(weapons)} WeaponVolley entries across {len(set(w['hero_id'] for w in weapons))} heroes")
    L.append("//")
    L.append("// Source: STUStatescriptStateWeaponVolley nodes from graph_nodes.json")
    L.append("// All numeric fields: -1 means dynamic/bound (value comes from a statescript var at runtime)")
    L.append("//")
    L.append("// Ballistic classification (deflect-relevant):")
    L.append("//   HitScan    — instant hit (机枪/狙击/散弹/卡西迪) → 不可被反弹")
    L.append("//   Projectile — physical projectile (Pharah/Junkrat/Hanzo/Genji 手里剑) → 可被反弹")
    L.append("//   Beam       — continuous beam (Mercy/Sym/Moira) → 不可被反弹")
    L.append("//   Melee      — 近战 → 不可被反弹")
    L.append("//   Unknown    — 数据不足 / 节点字段动态绑定")
    L.append("//")
    L.append("// 关联到具体 ability:用 (hero_id, graph_index) 反查 abilitydump 中的 ability 是哪个 graph 触发的。")
    L.append("// (graph_index 与 loadout guid_index 不直接相等,但同一英雄 graph 数量不多,人工对应即可)")
    L.append("")
    L.append(NAMESPACE_OPEN.rstrip())
    L.append("")

    L.append("enum class BallisticType : std::uint8_t {")
    for i, t in enumerate(BALLISTIC_TYPES):
        L.append(f"    {t} = {i},")
    L.append("};")
    L.append("")

    L.append("struct WeaponEntry {")
    L.append("    uint32_t      hero_id;")
    L.append("    const char*   hero_name;          // 中文英雄名 (inline for convenience)")
    L.append("    uint32_t      graph_index;        // owning graph — cross-ref with abilitydump or HeroWeaponList")
    L.append("    uint16_t      node_index;         // node index inside the graph")
    L.append("    const char*   source_node;        // statescript node type: WeaponVolley / ActionEffect / TrackTargets")
    L.append("    BallisticType ballistic;          // hitscan / projectile / beam / melee")
    L.append("    int           projectile_speed;   // m/s, -1 = dynamic/unknown")
    L.append("    int           projectile_lifetime;// sec or ticks, -1 = dynamic/unknown")
    L.append("    int           pellets;            // 每发射出几颗弹丸, -1 = dynamic/unknown")
    L.append("    int           fire_rate;          // 每秒射击数 shots/sec, -1 = dynamic/unknown")
    L.append("    const char*   projectile_entity;  // entity ref or var_id that holds spawned-entity handle")
    L.append("                                      //   WeaponVolley: STU entity ref (e.g. \"0x12.0AB\")")
    L.append("                                      //   ActionEffect: var_id where the spawned entity handle goes (e.g. \"0xB092\")")
    L.append("                                      //   TrackTargets: \"\"")
    L.append("};")
    L.append("")

    # Per-hero weapon list (loadout cross-reference) — full Weapon+Ability+Ult names per slot
    L.append("/// Per-hero weapon/ability registry (Weapon + Ability + UltimateAbility entries from loadouts).")
    L.append("/// Use this to resolve which loadout a graph_index belongs to (graph→loadout is")
    L.append("/// not 1:1, but per-hero you can typically infer by slot + ballistic match).")
    L.append("struct HeroWeaponEntry {")
    L.append("    uint32_t    loadout_id;")
    L.append("    const char* name_zhCN;")
    L.append("    AbilitySlot slot;")
    L.append("    const char* category;   // Weapon / Ability / UltimateAbility")
    L.append("};")
    L.append("")
    for hid in sorted(hero_weapons.keys()):
        items = hero_weapons[hid]
        if not items:
            continue
        hname = hero_name.get(hid, "?")
        L.append(f"// {hname} (0x{hid:X}) — {len(items)} weapon/ability loadouts")
        L.append(f"inline constexpr HeroWeaponEntry kHeroWeapons_{hid:X}[] = {{")
        for it in items:
            L.append(
                f"    {{0x{it['loadout_id']:X}u, {cpp_str(it['name'])}, "
                f"AbilitySlot::{it['slot']}, {cpp_str(it['category'])}}},"
            )
        L.append("};")
        L.append(f"inline constexpr std::size_t kHeroWeapons_{hid:X}_count = {len(items)};")
        L.append("")

    L.append("/// Get the per-hero weapon/ability list. Returns false if hero unknown.")
    L.append("inline bool GetHeroWeaponList(uint32_t hero_id, const HeroWeaponEntry*& out_ptr, std::size_t& out_count) noexcept {")
    L.append("    switch (hero_id) {")
    for hid in sorted(hero_weapons.keys()):
        if not hero_weapons[hid]:
            continue
        L.append(f"    case 0x{hid:X}u: out_ptr = kHeroWeapons_{hid:X}; out_count = kHeroWeapons_{hid:X}_count; return true;")
    L.append("    default: out_ptr = nullptr; out_count = 0; return false;")
    L.append("    }")
    L.append("}")
    L.append("")
    L.append("/// Find this hero's weapon/ability bound to a slot (e.g. LMB = 主武器).")
    L.append("inline const HeroWeaponEntry* FindHeroWeaponBySlot(uint32_t hero_id, AbilitySlot slot) noexcept {")
    L.append("    const HeroWeaponEntry* arr = nullptr; std::size_t n = 0;")
    L.append("    if (!GetHeroWeaponList(hero_id, arr, n)) return nullptr;")
    L.append("    for (std::size_t i = 0; i < n; ++i) if (arr[i].slot == slot) return &arr[i];")
    L.append("    return nullptr;")
    L.append("}")
    L.append("")
    L.append("/// Get hero's primary (LMB) weapon name. Returns nullptr if no LMB weapon.")
    L.append("inline const char* GetHeroPrimaryWeaponName(uint32_t hero_id) noexcept {")
    L.append("    const auto* w = FindHeroWeaponBySlot(hero_id, AbilitySlot::LMB);")
    L.append("    return w ? w->name_zhCN : nullptr;")
    L.append("}")
    L.append("")

    # Per-hero weapon-list summary as a comment block (people-readable cross-ref)
    L.append("/* === HERO → WEAPON QUICK REFERENCE ===")
    for hid in sorted(hero_weapons.keys()):
        items = hero_weapons[hid]
        if not items:
            continue
        hname = hero_name.get(hid, "?")
        L.append(f"  {hname} (0x{hid:X}):")
        for it in items:
            L.append(f"    {it['slot']:<8} 0x{it['loadout_id']:<5X} {it['name']}  [{it['category']}]")
    L.append("*/")
    L.append("")

    L.append(f"inline constexpr WeaponEntry kWeapons[] = {{")
    for w in weapons:
        L.append(
            f"    {{0x{w['hero_id']:X}u, {cpp_str(w['hero_name'])}, "
            f"0x{w['graph_index']:X}u, {w['node_index']}u, "
            f"{cpp_str(w['node_type'])}, "
            f"BallisticType::{w['ballistic']}, "
            f"{w['speed']}, {w['lifetime']}, {w['pellets']}, {w['fire_rate']}, "
            f"{cpp_str(w['proj_entity'])}}},"
        )
    L.append("};")
    L.append(f"inline constexpr std::size_t kWeaponCount = {len(weapons)};")
    L.append("")

    # Helpers
    L.append("/// Iterate all weapons of a given hero. Returns first matching entry, count via continuation.")
    L.append("inline std::size_t CountHeroWeapons(uint32_t hero_id) noexcept {")
    L.append("    std::size_t n = 0;")
    L.append("    for (const auto& w : kWeapons) if (w.hero_id == hero_id) ++n;")
    L.append("    return n;")
    L.append("}")
    L.append("")
    L.append("/// Find any weapon for a hero matching ballistic type. nullptr if none.")
    L.append("inline const WeaponEntry* FindHeroWeaponByBallistic(uint32_t hero_id, BallisticType bt) noexcept {")
    L.append("    for (const auto& w : kWeapons) if (w.hero_id == hero_id && w.ballistic == bt) return &w;")
    L.append("    return nullptr;")
    L.append("}")
    L.append("")
    L.append("/// True if any of this hero's weapons fire deflect-able projectiles.")
    L.append("/// Useful: judge if Genji deflect / D.Va matrix can stop this enemy's shots.")
    L.append("inline bool HeroCanBeDeflected(uint32_t hero_id) noexcept {")
    L.append("    return FindHeroWeaponByBallistic(hero_id, BallisticType::Projectile) != nullptr;")
    L.append("}")
    L.append("")
    L.append("inline bool BallisticIsDeflectable(BallisticType bt) noexcept {")
    L.append("    return bt == BallisticType::Projectile;")
    L.append("}")
    L.append("")
    L.append("inline const char* BallisticName(BallisticType bt) noexcept {")
    L.append("    switch (bt) {")
    for t in BALLISTIC_TYPES:
        L.append(f"    case BallisticType::{t}: return \"{t}\";")
    L.append("    }")
    L.append("    return \"?\";")
    L.append("}")
    L.append("")
    L.append("inline const WeaponEntry* WeaponsBegin() noexcept { return kWeapons; }")
    L.append("inline const WeaponEntry* WeaponsEnd()   noexcept { return kWeapons + kWeaponCount; }")
    L.append("")

    L.append(NAMESPACE_CLOSE.lstrip())

    with open(out_path, "w", encoding="utf-8") as f:
        f.write("\n".join(L))


# ---------------------------------------------------------------------------
# abilities.json consumers (preferred path)
# ---------------------------------------------------------------------------
def _load_json_or_none(path):
    if not os.path.exists(path):
        return None
    try:
        return json.load(open(path, encoding="utf-8"))
    except Exception as ex:
        print(f"  [warn] failed to load {path}: {ex}")
        return None


def _cv_literal(desc, default=-1):
    """Extract a numeric literal from a DescribeConfigVar result, else return default."""
    if not isinstance(desc, dict):
        return default
    if desc.get("kind") == "literal":
        v = desc.get("value")
        try:
            return int(v) if isinstance(v, (int, float, bool)) else default
        except (TypeError, ValueError):
            return default
    return default


def _cv_text(desc):
    """Human-readable one-liner for a DescribeConfigVar dict."""
    if not isinstance(desc, dict):
        return "?"
    k = desc.get("kind")
    if k == "literal":
        return f"{desc.get('value')}"
    if k == "dynamic":
        return f"var:{desc.get('var_id') or '?'}"
    if k == "var_ref":
        return f"var:{desc.get('var_id') or '?'}"
    if k == "asset_ref":
        return f"asset:{desc.get('guid') or '?'}"
    if k == "null":
        return "null"
    return desc.get("cv_type") or "?"


def _ballistic_from_wv(wv):
    """Classify a weapon_volley entry based on its structured fields."""
    has_proj = bool(wv.get("projectile_entity_id"))
    speed = wv.get("speed") or {}
    lifetime = wv.get("lifetime") or {}
    speed_lit = _cv_literal(speed, -999)
    # Projectile: has a spawned entity or a nonzero speed.
    if has_proj: return "Projectile"
    if speed.get("kind") == "dynamic" or lifetime.get("kind") == "dynamic": return "Projectile"
    if speed.get("kind") == "literal" and speed_lit > 0: return "Projectile"
    if speed.get("kind") == "literal" and speed_lit == 0: return "HitScan"
    # No ranged characteristics at all — could be a melee trigger
    return "Unknown"


def analyze_var_usage(abilities, sync_var_ids=None):
    """Build per-var usage metadata from abilities.json.

    Returns a dict keyed by var_id (int) with per-var data including:
      - writer/reader sets at loadout + graph + hero granularity
      - chase_dest / setvar_out / healthpool_writes / createentity_writes counts
      - role_hint : "per_hero_global" | "per_ability" | "engine_global" | ...
      - usage_class: "sync_var" | "state_chase" | "state_flag" | "cross_graph_state"
                     | "single_ability_state" | "temp" | "unused"
      - is_sync_var: bool (from graph_sync_vars.json)

    Distinguishing state vs temp:
      * sync_var  — network-replicated, always state (highest-confidence signal)
      * chase_dest ≥ 1 — tracked continuously → timer/charge progress
      * setvar_out ≥ 3 — multiple bool toggles → status flag
      * graph_count ≥ 2 — used across state boundaries → state
      * else (single graph, few writes) — likely temp/intermediate calculation
    """
    usage = {}
    sync_var_ids = sync_var_ids or set()

    for h in abilities:
        hid = coerce_id(h.get("hero_id"))
        if hid is None: continue
        for g in h.get("graphs", []) or []:
            lo_id_str = g.get("associated_loadout_id")
            lo_name = g.get("associated_loadout_name")
            lo_id = None
            if lo_id_str:
                try: lo_id = int(lo_id_str, 16)
                except ValueError: pass
            gidx_str = g.get("graph_index") or "0"
            try: gidx = int(gidx_str, 16)
            except ValueError: gidx = 0

            def slot(vid_hex):
                try: vid = int(vid_hex, 16)
                except (ValueError, TypeError): return None
                if vid not in usage:
                    usage[vid] = {
                        "writers": [], "readers": [],
                        "writer_loadouts": set(), "reader_loadouts": set(),
                        "writer_heroes": set(), "reader_heroes": set(),
                        "writer_graphs": set(), "reader_graphs": set(),
                        "chase_dest": 0, "setvar_out": 0,
                        "healthpool_writes": 0, "createentity_writes": 0,
                        "is_sync_var": False,
                    }
                return usage[vid]

            for vid_hex in g.get("writes_vars", []) or []:
                s = slot(vid_hex)
                if s is None: continue
                s["writers"].append((hid, lo_id, lo_name, gidx))
                if lo_id: s["writer_loadouts"].add(lo_id)
                s["writer_heroes"].add(hid)
                s["writer_graphs"].add(gidx)
            for vid_hex in g.get("reads_vars", []) or []:
                s = slot(vid_hex)
                if s is None: continue
                s["readers"].append((hid, lo_id, lo_name, gidx))
                if lo_id: s["reader_loadouts"].add(lo_id)
                s["reader_heroes"].add(hid)
                s["reader_graphs"].add(gidx)

            for cv in g.get("chase_vars", []) or []:
                d = cv.get("destination") or {}
                if d.get("kind") == "dynamic" and d.get("var_id"):
                    s = slot(d["var_id"])
                    if s is not None: s["chase_dest"] += 1
            for sv in g.get("set_vars", []) or []:
                o = sv.get("out_var") or {}
                if o.get("kind") == "dynamic" and o.get("var_id"):
                    s = slot(o["var_id"])
                    if s is not None: s["setvar_out"] += 1

    # Mark sync vars (network-replicated → always state, never temp)
    for vid, u in usage.items():
        if vid in sync_var_ids:
            u["is_sync_var"] = True

    # Classify role_hint (scope-ish, from writer/reader cardinality)
    for vid, u in usage.items():
        nh = len(u["writer_heroes"] | u["reader_heroes"])
        nl = len(u["writer_loadouts"] | u["reader_loadouts"])
        if nh >= 4: u["role_hint"] = "engine_global"
        elif nh == 1 and nl >= 3: u["role_hint"] = "per_hero_global"
        elif nh == 1 and nl <= 2 and nl >= 1: u["role_hint"] = "per_ability"
        elif nh == 0: u["role_hint"] = "unused"
        else: u["role_hint"] = "multi_hero_shared"
        if u["chase_dest"] >= 3:
            u["role_hint"] += "|chased_numeric"
        elif u["setvar_out"] >= 3 and u["chase_dest"] == 0:
            u["role_hint"] += "|status_flag"

    # Assign usage_class — the single most actionable tag for downstream consumers.
    # Classification is compound: base_class describes persistence (sync/cross-graph/single),
    # and the role suffix (_chase/_flag) describes the write pattern.
    # This preserves the sync_var signal while letting chase/flag refine it further.
    for vid, u in usage.items():
        all_graphs = u["writer_graphs"] | u["reader_graphs"]
        all_loadouts = u["writer_loadouts"] | u["reader_loadouts"]
        ng = len(all_graphs)
        nl = len(all_loadouts)

        # base — how "sticky" is this var
        if u["is_sync_var"]:
            base = "sync"                    # network-replicated — definitely state
        elif ng >= 2:
            base = "cross_graph"             # touched by ≥ 2 distinct graphs — persists across state boundaries
        elif ng == 1 and u["chase_dest"] == 0 and u["setvar_out"] <= 1:
            base = "temp"                    # single graph, no chase tracking, ≤ 1 SetVar write — likely intermediate
        elif ng == 0 and nl == 0:
            base = "unused"
        else:
            base = "local"                   # single graph but has chase/flag signal

        # role — what pattern the writes show
        if u["chase_dest"] >= 1:
            role = "chase"                   # continuous numeric tracking: CD timer, charge bar, HP
        elif u["setvar_out"] >= 3:
            role = "flag"                    # bool flipped 3+ times: ability active / buff applied
        elif u["setvar_out"] >= 1:
            role = "writable"                # at least one SetVar — genuinely state but not a pattern we recognize
        else:
            role = ""                        # only read / no direct write node captured

        cls = f"{base}_{role}" if role else base
        u["usage_class"] = cls
        u["base_class"] = base               # for easy filtering
        u["role_class"] = role or "none"

    return usage


def load_sync_var_ids(dump_dir):
    """Read graph_sync_vars.json and extract SyncVar identifier_index values where flag != 0.

    The graph sync_vars array contains slot placeholders AND actually-replicated vars.
    Only flag != 0 means the var is actively tracked for network replication — those are
    high-confidence "real state" signals. Slots with flag == 0 are noise."""
    path = os.path.join(dump_dir, "graph_sync_vars.json")
    if not os.path.exists(path):
        return set()
    try:
        data = json.load(open(path, encoding="utf-8"))
    except Exception:
        return set()
    ids = set()
    for h in data or []:
        for g in h.get("graphs", []) or []:
            for sv in g.get("sync_vars", []) or []:
                idx_str = sv.get("identifier_index")
                flag = sv.get("flag", 0)
                if not idx_str or not flag: continue
                try:
                    ids.add(int(idx_str, 16))
                except (ValueError, TypeError):
                    pass
    return ids


# ---------------------------------------------------------------------------
# scope classification: single_ability / single_hero / multi_hero / global
# ---------------------------------------------------------------------------
# Caller-facing scope tagging. Distinct from role_hint — scope answers
# "WHO uses this var", role answers "HOW is it used".
#   single_ability : exactly 1 loadout touches it (read or write)
#   single_hero    : exactly 1 hero, but ≥ 2 of that hero's loadouts
#   multi_hero     : 2..ENGINE_GLOBAL_HERO_THRESHOLD-1 distinct heroes
#   global         : ≥ ENGINE_GLOBAL_HERO_THRESHOLD distinct heroes
#   unused         : no writes or reads in any loadout (rare — data-driven only)
ENGINE_GLOBAL_HERO_THRESHOLD = 5


def _mk_loadout_detail(lid, loadout_by_id, loadout_button_by_id, loadout_category_by_id):
    return {
        "loadout_id": f"0x{lid:X}",
        "name": loadout_by_id.get(lid, ""),
        "button": loadout_button_by_id.get(lid, ""),
        "category": loadout_category_by_id.get(lid, ""),
    }


def classify_var_scope(u, hero_by_id, loadout_by_id, loadout_owner_hero,
                       loadout_button_by_id, loadout_category_by_id):
    """Return {"scope", "scope_detail"} for a var usage record.

    scope_detail is context tailored to the scope:
      single_ability → one loadout descriptor (hero_id, hero_name, loadout, button, category)
      single_hero    → {hero_id, hero_name, loadouts: [loadout desc x N]}
      multi_hero     → heroes: [{id, name, loadouts: [desc]}]
      global         → {hero_count, example_heroes: [name x 5], example_loadouts: [name x 5]}
    """
    all_loadouts = (u.get("writer_loadouts") or set()) | (u.get("reader_loadouts") or set())
    all_heroes = (u.get("writer_heroes") or set()) | (u.get("reader_heroes") or set())
    # Derive hero set from loadouts (more reliable than writer_heroes/reader_heroes
    # which only gets set when the loadout was associated to a hero — see heroLoadoutsMap).
    heroes_via_loadouts = set()
    for lid in all_loadouts:
        oh = loadout_owner_hero.get(lid)
        if oh is not None:
            heroes_via_loadouts.add(oh)
    heroes = all_heroes | heroes_via_loadouts

    def lodesc(lid):
        d = _mk_loadout_detail(lid, loadout_by_id, loadout_button_by_id, loadout_category_by_id)
        oh = loadout_owner_hero.get(lid)
        if oh is not None:
            d["hero_id"] = f"0x{oh:X}"
            d["hero_name"] = hero_by_id.get(oh, "")
        return d

    if len(all_loadouts) == 0 and len(heroes) == 0:
        return {"scope": "unused", "scope_detail": {}}

    if len(all_loadouts) == 1:
        lid = next(iter(all_loadouts))
        return {"scope": "single_ability", "scope_detail": lodesc(lid)}

    if len(heroes) == 1:
        hid = next(iter(heroes))
        hero_loadouts = [lodesc(l) for l in sorted(all_loadouts) if loadout_owner_hero.get(l) == hid or not loadout_owner_hero.get(l)]
        # If the hero has ≥ 2 loadouts using this var, it's hero-scope
        return {
            "scope": "single_hero",
            "scope_detail": {
                "hero_id": f"0x{hid:X}",
                "hero_name": hero_by_id.get(hid, ""),
                "loadouts": hero_loadouts,
            },
        }

    if len(heroes) < ENGINE_GLOBAL_HERO_THRESHOLD:
        # Group loadouts by hero
        by_hero = {}
        for l in sorted(all_loadouts):
            oh = loadout_owner_hero.get(l)
            if oh is None: oh = -1
            by_hero.setdefault(oh, []).append(lodesc(l))
        hero_blocks = []
        for hid in sorted(by_hero.keys()):
            if hid == -1:
                continue
            hero_blocks.append({
                "hero_id": f"0x{hid:X}",
                "hero_name": hero_by_id.get(hid, ""),
                "loadouts": by_hero[hid],
            })
        # orphans (loadouts whose owner hero we couldn't resolve)
        orphans = by_hero.get(-1, [])
        detail = {"heroes": hero_blocks}
        if orphans: detail["orphan_loadouts"] = orphans
        return {"scope": "multi_hero", "scope_detail": detail}

    # Global — don't enumerate everything, just give a summary
    example_heroes = [hero_by_id.get(hid, "") for hid in list(sorted(heroes))[:6]]
    example_loadouts = [lodesc(l) for l in list(sorted(all_loadouts))[:6]]
    return {
        "scope": "global",
        "scope_detail": {
            "hero_count": len(heroes),
            "loadout_count": len(all_loadouts),
            "example_heroes": example_heroes,
            "example_loadouts": example_loadouts,
        },
    }


def classify_var_from_usage(u):
    """Return (StateVarKind, StateVarDomain) for a var based on its usage profile."""
    role = u.get("role_hint", "")
    # Numeric signals first (chase = continuous numeric tracking)
    if "chased_numeric" in role:
        if "engine_global" in role: return ("Value", "CHASE_VAR")
        if "per_hero_global" in role: return ("Value", "ULT_CHARGE")  # typical per-hero numeric
        return ("Value", "ABILITY_COOLDOWN")
    if "status_flag" in role:
        if "per_ability" in role: return ("Status", "ABILITY_STATE")
        if "per_hero_global" in role: return ("Status", "BUFF_BENEFIT")
        return ("Status", "LIFECYCLE")
    # Fall back based on scope
    if "engine_global" in role: return ("Value", "UNCLASSIFIED")
    if "per_hero_global" in role: return ("Value", "UNCLASSIFIED")
    if "per_ability" in role: return ("Value", "ABILITY_STATE")
    return ("Unknown", "UNCLASSIFIED")


def name_var_from_usage(vid, u, hero_by_id, loadout_by_id):
    """Best-effort naming when no displayName was dumped.
    Uses writer loadouts + role to produce a SCREAMING_SNAKE identifier.

    Rules:
      - Only 1 writer_loadout + per_ability   → VAR_<abilityname>_<role>
      - Only 1 writer_hero + per_hero_global  → VAR_<heroname>_GLOBAL_<vid>
      - Engine-global                         → VAR_ENGINE_<vid>
    """
    role = u.get("role_hint", "")
    hero_ids = u.get("writer_heroes") or set()
    lo_ids = u.get("writer_loadouts") or set()
    role_suffix = "CHASE" if "chased_numeric" in role else ("FLAG" if "status_flag" in role else "")

    def _ident(s):
        if not s: return ""
        # keep Chinese, strip punctuation
        return re.sub(r"[\s\(\)\[\]\{\},:;\"\']", "_", s).upper()

    if "engine_global" in role:
        return f"VAR_ENGINE_{vid:X}_{role_suffix}".rstrip("_")
    if "per_hero_global" in role and len(hero_ids) == 1:
        hid = next(iter(hero_ids))
        hname = _ident(hero_by_id.get(hid, "?"))
        return f"VAR_{hname}_GLOBAL_{role_suffix}_{vid:X}".replace("__", "_").rstrip("_")
    if "per_ability" in role and len(lo_ids) == 1:
        lid = next(iter(lo_ids))
        lname = _ident(loadout_by_id.get(lid, "?"))
        return f"VAR_{lname}_{role_suffix}_{vid:X}".replace("__", "_").rstrip("_")
    # Mixed / multi-loadout within one hero
    if len(hero_ids) == 1:
        hid = next(iter(hero_ids))
        hname = _ident(hero_by_id.get(hid, "?"))
        return f"VAR_{hname}_SHARED_{vid:X}".rstrip("_")
    return f"VAR_UNKNOWN_{vid:X}".rstrip("_")


def gen_weapondump_from_abilities(heroes, abilities, name_overrides, out_path):
    """Generate weapondump.hpp directly from abilities.json's weapon_volleys arrays.
    Keeps the same struct shape as gen_weapondump so downstream code is compatible."""
    hero_name = {int(h["hero_id"], 16): clean(h.get("name_zhCN", "")) for h in heroes if h.get("is_hero")}
    hero_weapons = {}
    for h in heroes:
        if not h.get("is_hero"): continue
        hid = int(h["hero_id"], 16)
        items = []
        for lo in h.get("loadouts", []):
            cat = lo.get("category", "")
            if cat in {"HeroStats", "Subrole", "PassiveAbility", "Perk"}: continue
            slot = detect_slot(lo.get("button"), cat)
            items.append({
                "loadout_id": int(lo["guid_index"], 16),
                "name": clean(lo.get("name_zhCN", "")),
                "slot": slot,
                "category": cat,
            })
        hero_weapons[hid] = items

    weapons = []  # list of dicts matching old schema
    for h in abilities:
        hid = coerce_id(h.get("hero_id"))
        if hid is None or hid not in hero_name: continue
        hn = hero_name[hid]
        for g in h.get("graphs", []):
            gidx = coerce_id(g.get("graph_index"))
            slot_idx = g.get("slot_index", -1)
            for wv in g.get("weapon_volleys", []) or []:
                bt = _ballistic_from_wv(wv)
                pid_str = wv.get("projectile_entity_id") or ""
                pname = wv.get("projectile_entity_name") or ""
                # Prefer the name_overrides dict if we have one for this entity.
                if pid_str:
                    try:
                        pid_int = int(pid_str, 16)
                        b = name_overrides.get(pid_int)
                        if isinstance(b, dict): pname = b.get("name") or pname
                        elif isinstance(b, str): pname = b
                    except ValueError: pass
                weapons.append({
                    "hero_id": hid,
                    "hero_name": hn,
                    "graph_index": gidx if gidx is not None else 0,
                    "slot_index": slot_idx,
                    "node_index": wv.get("node_id", 0),
                    "node_type": "WeaponVolley",
                    "ballistic": bt,
                    "speed": _cv_literal(wv.get("speed")),
                    "lifetime": _cv_literal(wv.get("lifetime")),
                    "pellets": _cv_literal(wv.get("pellets")),
                    "fire_rate": _cv_literal(wv.get("fire_rate")),
                    "proj_entity_id": pid_str,
                    "proj_entity_name": pname,
                })
    weapons.sort(key=lambda x: (x["hero_id"], x["graph_index"], x["node_index"]))

    L = []
    L.append(HEADER_PREAMBLE.rstrip())
    L.append("")
    L.append("// weapondump.hpp — Per-(hero, graph, node) weapon ballistic data (from abilities.json)")
    L.append(f"// {len(weapons)} WeaponVolley entries across {len(set(w['hero_id'] for w in weapons))} heroes")
    L.append("//")
    L.append("// Numeric fields: -1 means dynamic/bound at runtime (driven by a state var).")
    L.append("// Projectile entity is resolved to a name via abilities.json + entity_origins.json.")
    L.append("")
    L.append(NAMESPACE_OPEN.rstrip())
    L.append("")
    L.append("enum class BallisticType : std::uint8_t {")
    for i, t in enumerate(BALLISTIC_TYPES):
        L.append(f"    {t} = {i},")
    L.append("};")
    L.append("")
    L.append("struct WeaponEntry {")
    L.append("    uint32_t      hero_id;")
    L.append("    const char*   hero_name;")
    L.append("    uint32_t      graph_index;       // owning graph slot")
    L.append("    int16_t       slot_index;        // position in hero's graph list (proxy for keybind)")
    L.append("    uint16_t      node_index;        // node index inside the graph")
    L.append("    BallisticType ballistic;")
    L.append("    int           projectile_speed;   // m/s, -1 = dynamic")
    L.append("    int           projectile_lifetime;// -1 = dynamic")
    L.append("    int           pellets;")
    L.append("    int           fire_rate;")
    L.append("    uint32_t      projectile_entity_id;   // 0 if none")
    L.append("    const char*   projectile_entity_name; // resolved via abilities + origins; \"\" if unknown")
    L.append("};")
    L.append("")
    L.append("inline constexpr WeaponEntry kWeapons[] = {")
    for w in weapons:
        pid_hex = 0
        try:
            pid_hex = int(w["proj_entity_id"], 16) if w["proj_entity_id"] else 0
        except ValueError: pid_hex = 0
        L.append(
            f"    {{0x{w['hero_id']:X}u, {cpp_str(w['hero_name'])}, "
            f"0x{w['graph_index']:X}u, {w['slot_index']}, {w['node_index']}u, "
            f"BallisticType::{w['ballistic']}, "
            f"{w['speed']}, {w['lifetime']}, {w['pellets']}, {w['fire_rate']}, "
            f"0x{pid_hex:X}u, {cpp_str(w['proj_entity_name'])}}},"
        )
    L.append("};")
    L.append(f"inline constexpr std::size_t kWeaponCount = {len(weapons)};")
    L.append("")
    L.append("inline const WeaponEntry* WeaponsBegin() noexcept { return kWeapons; }")
    L.append("inline const WeaponEntry* WeaponsEnd()   noexcept { return kWeapons + kWeaponCount; }")
    L.append("")
    L.append("inline std::size_t CountHeroWeapons(uint32_t hero_id) noexcept {")
    L.append("    std::size_t n = 0;")
    L.append("    for (const auto& w : kWeapons) if (w.hero_id == hero_id) ++n;")
    L.append("    return n;")
    L.append("}")
    L.append("")
    L.append("inline const WeaponEntry* FindHeroWeaponByBallistic(uint32_t hero_id, BallisticType bt) noexcept {")
    L.append("    for (const auto& w : kWeapons) if (w.hero_id == hero_id && w.ballistic == bt) return &w;")
    L.append("    return nullptr;")
    L.append("}")
    L.append("")
    L.append("inline bool BallisticIsDeflectable(BallisticType bt) noexcept {")
    L.append("    return bt == BallisticType::Projectile;")
    L.append("}")
    L.append("")
    L.append("inline bool HeroCanBeDeflected(uint32_t hero_id) noexcept {")
    L.append("    return FindHeroWeaponByBallistic(hero_id, BallisticType::Projectile) != nullptr;")
    L.append("}")
    L.append("")
    L.append("inline const char* BallisticName(BallisticType bt) noexcept {")
    L.append("    switch (bt) {")
    for t in BALLISTIC_TYPES:
        L.append(f"    case BallisticType::{t}: return \"{t}\";")
    L.append("    }")
    L.append("    return \"?\";")
    L.append("}")
    L.append("")
    L.append(NAMESPACE_CLOSE.lstrip())
    with open(out_path, "w", encoding="utf-8") as f:
        f.write("\n".join(L))


def gen_herokitdump(heroes, abilities, name_overrides, out_path):
    """Per-hero rollup combining: loadouts (with slot/category), graph summaries
    (weapon volleys, create_entity, modify_health, deflect, set_var, writes_vars),
    and the inferred link between them via slot_index.

    This is the one-stop reference for cheat code: given a hero_id, what can they do?"""
    playable_names = {int(h["hero_id"], 16): clean(h.get("name_zhCN", ""))
                      for h in heroes if h.get("is_hero")}

    L = []
    L.append(HEADER_PREAMBLE.rstrip())
    L.append("")
    L.append("// herokitdump.hpp — Per-hero kit rollup: loadouts + graph actions + var footprint")
    L.append(f"// {len(abilities)} heroes, derived from abilities.json (no hardcoded knowledge).")
    L.append("//")
    L.append("// Use this to answer questions like:")
    L.append("//   'Which state vars does Genji Deflect write?'  → kHeroKit_5.graphs[n].write_var_ids")
    L.append("//   'What entity does Ashe's Dynamite spawn?'     → kHeroKit_N.graphs[n].spawned_entity_id")
    L.append("//   'Does this hero have a deflect?'              → kHeroKit_N.has_deflect")
    L.append("")
    L.append(NAMESPACE_OPEN.rstrip())
    L.append("")
    L.append("struct HeroKitGraph {")
    L.append("    uint32_t    graph_index;")
    L.append("    int16_t     slot_index;")
    L.append("    uint16_t    total_nodes;")
    L.append("    uint16_t    weapon_volley_count;")
    L.append("    uint16_t    modify_health_count;")
    L.append("    uint16_t    create_entity_count;")
    L.append("    uint16_t    set_var_count;")
    L.append("    uint16_t    chase_var_count;")
    L.append("    uint16_t    deflect_count;")
    L.append("    uint16_t    health_pool_count;")
    L.append("    uint32_t    first_projectile_entity_id;   // 0 if none")
    L.append("    const char* first_projectile_name;        // first named projectile from this graph")
    L.append("    const char* likely_loadout_name;          // positional guess by slot_index")
    L.append("};")
    L.append("")
    L.append("struct HeroKit {")
    L.append("    uint32_t          hero_id;")
    L.append("    const char*       hero_name;")
    L.append("    uint16_t          graph_count;")
    L.append("    bool              has_deflect;             // any graph with a DeflectProjectiles node")
    L.append("    bool              has_projectile_weapon;   // any WeaponVolley with a spawned entity")
    L.append("    bool              has_beam_weapon;         // any TrackTargets node (heuristic via node histogram)")
    L.append("    const HeroKitGraph* graphs;")
    L.append("};")
    L.append("")

    # Emit per-hero graph arrays + HeroKit entry.
    kit_entries = []  # (hid_int, hero_name, has_deflect, has_proj, has_beam, graph_count)
    for h in abilities:
        hid = coerce_id(h.get("hero_id"))
        if hid is None: continue
        hn = playable_names.get(hid) or h.get("hero_name") or "?"
        real_los = [l for l in (h.get("loadouts") or [])
                    if l.get("category") not in ("HeroStats", "Subrole")]

        has_deflect = False
        has_proj = False
        has_beam = False
        rows = []
        for g in h.get("graphs") or []:
            si = g.get("slot_index", -1)
            wvs = g.get("weapon_volleys") or []
            ces = g.get("create_entities") or []
            first_pid = 0
            first_pname = ""
            def _lookup_name(eid):
                o = name_overrides.get(eid)
                if isinstance(o, dict): return o.get("name", "")
                if isinstance(o, str): return o
                return ""
            for wv in wvs:
                if wv.get("projectile_entity_id"):
                    try:
                        first_pid = int(wv["projectile_entity_id"], 16)
                        first_pname = _lookup_name(first_pid) or wv.get("projectile_entity_name") or ""
                        break
                    except ValueError: pass
            if not first_pid:
                for ce in ces:
                    if ce.get("entity_id"):
                        try:
                            first_pid = int(ce["entity_id"], 16)
                            first_pname = _lookup_name(first_pid)
                            break
                        except ValueError: pass

            deflects = len(g.get("deflect_projectiles") or [])
            has_deflect = has_deflect or deflects > 0
            if wvs and first_pid: has_proj = True
            # beam hint: check node_type_histogram for TrackTargets
            for t in g.get("node_type_histogram") or []:
                if "TrackTargets" in t.get("type", ""):
                    has_beam = True; break

            likely_lo = ""
            if real_los and 0 <= si < len(real_los):
                likely_lo = real_los[si].get("name") or ""

            rows.append({
                "graph_index": coerce_id(g.get("graph_index")) or 0,
                "slot_index": si,
                "total_nodes": g.get("total_nodes", 0),
                "wv": len(wvs),
                "mh": len(g.get("modify_health") or []),
                "ce": len(ces),
                "sv": len(g.get("set_vars") or []),
                "cv": len(g.get("chase_vars") or []),
                "df": deflects,
                "hp": len(g.get("health_pools") or []),
                "first_pid": first_pid,
                "first_pname": first_pname,
                "likely_lo": likely_lo,
            })

        L.append(f"// {hn} (0x{hid:X}) — {len(rows)} graphs")
        L.append(f"inline constexpr HeroKitGraph kHeroKit_{hid:X}_graphs[] = {{")
        for r in rows:
            L.append(
                f"    {{0x{r['graph_index']:X}u, {r['slot_index']}, "
                f"{r['total_nodes']}u, {r['wv']}u, {r['mh']}u, {r['ce']}u, {r['sv']}u, "
                f"{r['cv']}u, {r['df']}u, {r['hp']}u, "
                f"0x{r['first_pid']:X}u, {cpp_str(r['first_pname'])}, {cpp_str(r['likely_lo'])}}},"
            )
        L.append("};")
        L.append(f"inline constexpr std::size_t kHeroKit_{hid:X}_graphs_count = {len(rows)};")
        L.append("")
        kit_entries.append((hid, hn, has_deflect, has_proj, has_beam, len(rows)))

    kit_entries.sort(key=lambda x: x[0])
    L.append("inline constexpr HeroKit kHeroKits[] = {")
    for hid, hn, hd, hp, hb, gc in kit_entries:
        L.append(
            f"    {{0x{hid:X}u, {cpp_str(hn)}, {gc}u, "
            f"{'true' if hd else 'false'}, {'true' if hp else 'false'}, {'true' if hb else 'false'}, "
            f"kHeroKit_{hid:X}_graphs}},"
        )
    L.append("};")
    L.append(f"inline constexpr std::size_t kHeroKitCount = {len(kit_entries)};")
    L.append("")
    L.append("inline const HeroKit* FindHeroKit(uint32_t hero_id) noexcept {")
    L.append("    for (const auto& k : kHeroKits) if (k.hero_id == hero_id) return &k;")
    L.append("    return nullptr;")
    L.append("}")
    L.append("")
    L.append("inline bool HeroHasDeflect(uint32_t hero_id) noexcept {")
    L.append("    const auto* k = FindHeroKit(hero_id); return k && k->has_deflect;")
    L.append("}")
    L.append("")
    L.append("inline bool HeroHasProjectileWeapon(uint32_t hero_id) noexcept {")
    L.append("    const auto* k = FindHeroKit(hero_id); return k && k->has_projectile_weapon;")
    L.append("}")
    L.append("")
    L.append(NAMESPACE_CLOSE.lstrip())
    with open(out_path, "w", encoding="utf-8") as f:
        f.write("\n".join(L))


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    root = os.path.normpath(os.path.join(here, ".."))

    dump_dir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(root, "output", "dump_json")
    dict_path = sys.argv[2] if len(sys.argv) > 2 else os.path.join(root, "blueprint_editor", "output", "var_dictionary.json")
    out_dir = sys.argv[3] if len(sys.argv) > 3 else os.path.join(root, "output")

    os.makedirs(out_dir, exist_ok=True)

    heroes = json.load(open(os.path.join(dump_dir, "heroes.json"), encoding="utf-8"))
    loadouts = json.load(open(os.path.join(dump_dir, "loadouts.json"), encoding="utf-8"))

    # Load CSV overrides early — hero overrides apply before everything else.
    here = os.path.dirname(os.path.abspath(__file__))
    overrides_dir = os.path.join(here, "overrides")
    entity_csv_overrides = load_entity_overrides_csv(os.path.join(overrides_dir, "entities.csv"))
    statevar_csv_overrides = load_statevar_overrides_csv(os.path.join(overrides_dir, "statevars.csv"))
    hero_csv_overrides = load_hero_overrides_csv(os.path.join(overrides_dir, "heroes.csv"))
    if entity_csv_overrides or statevar_csv_overrides or hero_csv_overrides:
        print(f"  [csv] overrides loaded: {len(entity_csv_overrides)} entities, "
              f"{len(statevar_csv_overrides)} vars, {len(hero_csv_overrides)} heroes")

    # Apply hero overrides early so downstream code sees the patched names.
    for hid, patch in hero_csv_overrides.items():
        for h in heroes:
            try:
                if int(h["hero_id"], 16) == hid and patch.get("name"):
                    h["name_zhCN"] = patch["name"]
                    break
            except (KeyError, ValueError):
                continue

    # Load abilities.json upfront — primary semantic source for var classification.
    abilities_preload = _load_json_or_none(os.path.join(dump_dir, "abilities.json"))
    sync_var_ids = load_sync_var_ids(dump_dir)
    if sync_var_ids:
        print(f"  [info] loaded {len(sync_var_ids)} sync var IDs from graph_sync_vars.json")
    var_usage = analyze_var_usage(abilities_preload, sync_var_ids) if abilities_preload else {}
    hero_by_id_z = {int(h["hero_id"], 16): clean(h.get("name_zhCN") or "") for h in heroes}
    loadout_by_id_z = {int(l["loadout_id"], 16): clean(l.get("name_zhCN") or "") for l in loadouts}
    loadout_button_by_id = {int(l["loadout_id"], 16): (l.get("button") or "") for l in loadouts}
    loadout_category_by_id = {int(l["loadout_id"], 16): (l.get("category") or "") for l in loadouts}
    # loadout_id → owner hero_id (derived from heroes[].loadouts[].guid_index mapping).
    loadout_owner_hero = {}
    for h in heroes:
        if not h.get("is_hero"): continue
        hid = int(h["hero_id"], 16)
        for lo in h.get("loadouts", []):
            try:
                lid = int(lo["guid_index"], 16)
            except (ValueError, KeyError):
                continue
            # First winner (a loadout shared across heroes is rare)
            loadout_owner_hero.setdefault(lid, hid)

    # Now tag every var with scope + detail (single_ability / single_hero / multi_hero / global)
    for vid, u in var_usage.items():
        sc = classify_var_scope(u, hero_by_id_z, loadout_by_id_z, loadout_owner_hero,
                                 loadout_button_by_id, loadout_category_by_id)
        u["scope"] = sc["scope"]
        u["scope_detail"] = sc["scope_detail"]

    named_vars = []
    if os.path.exists(dict_path):
        dictionary = json.load(open(dict_path, encoding="utf-8"))
        raw_vars = dictionary.get("vars", {})
        for vid_hex, info in raw_vars.items():
            try:
                vid_int = int(vid_hex, 16)
            except ValueError:
                continue
            name = info.get("name", "")
            if not name:
                continue
            domain = info.get("domain") or "UNCLASSIFIED"
            if domain not in ALL_DOMAINS_KNOWN:
                domain = "UNCLASSIFIED"
            kind = classify_kind(name, domain, info.get("description", ""))
            named_vars.append((
                vid_int, name, kind, domain,
                info.get("confidence", "low"),
                info.get("description", ""),
            ))
    else:
        # No var_dictionary: synthesize entries using usage analysis + loadout association.
        # Union var IDs across var_names.json (sources) and var_usage (graph writes/reads).
        var_names_raw = _load_json_or_none(os.path.join(dump_dir, "var_names.json")) or []
        vn_by_id = {}
        for v in var_names_raw:
            try: vn_by_id[int(v["var_id"], 16)] = v
            except (ValueError, KeyError): pass
        all_vids = set(vn_by_id.keys()) | set(var_usage.keys())

        # SDK policy: only emit vars that carry real semantic value — skip noise.
        #   - base "temp"      : single-graph intermediate calculation, not visible outside the graph
        #   - base "unused"    : no writer or reader signal at all
        #   - VAR_UNKNOWN_XXX with no interesting role : pure junk (unused/local/temp with no signal)
        skipped_temp = 0
        skipped_unused = 0
        skipped_noise = 0
        for vid_int in sorted(all_vids):
            u = var_usage.get(vid_int, {})
            vn = vn_by_id.get(vid_int, {})
            dn = vn.get("display_name")

            base = u.get("base_class", "unused")
            role = u.get("role_class", "none")
            if base == "temp":
                skipped_temp += 1
                continue
            if base == "unused" and not dn:
                skipped_unused += 1
                continue

            if dn:
                name = dn
            elif u:
                name = name_var_from_usage(vid_int, u, hero_by_id_z, loadout_by_id_z)
            else:
                name = f"VAR_{vid_int:X}"

            # Drop entries whose name collapsed to VAR_UNKNOWN_<hex> without any role signal.
            # Keep VAR_UNKNOWN with role=chase or role=flag — those are still useful state markers.
            if name.startswith("VAR_UNKNOWN_") and role in ("none", "writable"):
                skipped_noise += 1
                continue

            kind, domain = classify_var_from_usage(u) if u else ("Unknown", "UNCLASSIFIED")
            desc_parts = [f"[{u.get('usage_class', 'unknown')}]"]
            sc = u.get("scope")
            sd = u.get("scope_detail") or {}
            if sc == "single_ability":
                hn = sd.get("hero_name") or ""
                ln = sd.get("name") or ""
                bt = sd.get("button") or ""
                desc_parts.append(f"{hn}/{ln}" + (f" @ {bt}" if bt else ""))
            elif sc == "single_hero":
                hn = sd.get("hero_name") or ""
                los = ", ".join((l.get("name") or "?") for l in (sd.get("loadouts") or [])[:3])
                desc_parts.append(f"{hn}: {los}")
            elif sc == "multi_hero":
                hs = ", ".join((hb.get("hero_name") or "?") for hb in (sd.get("heroes") or [])[:4])
                desc_parts.append(f"heroes: {hs}")
            elif sc == "global":
                desc_parts.append(f"{sd.get('hero_count', '?')} heroes, {sd.get('loadout_count', '?')} loadouts")
            if u.get("role_hint"): desc_parts.append(u["role_hint"])
            desc = " ".join(desc_parts)
            named_vars.append((vid_int, name, kind, domain, "low", desc))
        print(f"  [info] synthesized {len(named_vars)} state vars from abilities.json "
              f"(skipped: {skipped_temp} temp, {skipped_unused} unused, {skipped_noise} noise)")

    # Apply statevar CSV overrides — these win over auto-names/kinds/domains.
    if statevar_csv_overrides:
        by_vid = {v[0]: (idx, v) for idx, v in enumerate(named_vars)}
        patched = 0
        added = 0
        for vid, patch in statevar_csv_overrides.items():
            if vid in by_vid:
                idx, tup = by_vid[vid]
                # tuple(vid_int, name, kind, domain, conf, desc)
                new_name = patch.get("name", tup[1])
                new_kind = patch.get("kind", tup[2])
                new_domain = patch.get("domain", tup[3])
                new_desc = tup[5] + "; [csv-override]" if "[csv-override]" not in tup[5] else tup[5]
                named_vars[idx] = (tup[0], new_name, new_kind, new_domain, "manual", new_desc)
                patched += 1
            else:
                # Insert a new var from CSV (wasn't in any auto-source)
                named_vars.append((
                    vid,
                    patch.get("name", f"VAR_{vid:X}"),
                    patch.get("kind", "Unknown"),
                    patch.get("domain", "UNCLASSIFIED"),
                    "manual",
                    "[csv-only]",
                ))
                added += 1
        print(f"  [csv] statevar overrides applied: {patched} patched, {added} added new")
        named_vars.sort(key=lambda x: x[0])

    # var_usage.json side-file: enables offline audit + downstream tooling.
    if var_usage:
        usage_path = os.path.join(out_dir, "var_usage.json")
        usage_out = []
        for vid in sorted(var_usage):
            u = var_usage[vid]
            usage_out.append({
                "var_id": f"0x{vid:X}",
                "usage_class": u.get("usage_class"),  # sync_var | state_chase | state_flag | cross_graph_state | temp | unused | minor_state
                "is_sync_var": u.get("is_sync_var", False),
                "scope": u.get("scope"),              # single_ability | single_hero | multi_hero | global | unused
                "scope_detail": u.get("scope_detail"),  # rich binding (hero + loadout + button)
                "role_hint": u.get("role_hint"),      # usage pattern: chased_numeric, status_flag, etc.
                "graph_count": len(u["writer_graphs"] | u["reader_graphs"]),
                "writer_loadout_count": len(u["writer_loadouts"]),
                "reader_loadout_count": len(u["reader_loadouts"]),
                "writer_hero_count": len(u["writer_heroes"]),
                "reader_hero_count": len(u["reader_heroes"]),
                "chase_dest": u.get("chase_dest"),
                "setvar_out": u.get("setvar_out"),
                # Legacy fields kept for back-compat
                "writer_loadouts": sorted(u["writer_loadouts"]),
                "reader_loadouts": sorted(u["reader_loadouts"]),
                "writer_loadout_names": [loadout_by_id_z.get(l) for l in sorted(u["writer_loadouts"])][:8],
                "reader_loadout_names": [loadout_by_id_z.get(l) for l in sorted(u["reader_loadouts"])][:8],
                "writer_hero_names": [hero_by_id_z.get(hi) for hi in sorted(u["writer_heroes"])],
            })
        with open(usage_path, "w", encoding="utf-8") as f:
            json.dump(usage_out, f, ensure_ascii=False, indent=2)
        print(f"  {usage_path} — {len(usage_out)} vars with usage profile")

    # write CSV
    csv_path = os.path.join(out_dir, "vars.csv")
    gen_csv(named_vars, csv_path)
    print(f"  {csv_path} — {len(named_vars)} named vars")

    # write hpp
    namedump_path = os.path.join(out_dir, "namedump.hpp")
    gen_namedump(heroes, namedump_path)
    print(f"  {namedump_path} — {sum(1 for h in heroes if h.get('is_hero'))} playable heroes")

    abilitydump_path = os.path.join(out_dir, "abilitydump.hpp")
    gen_abilitydump(heroes, loadouts, abilitydump_path)
    print(f"  {abilitydump_path} — {len(loadouts)} total loadouts")

    statevardump_path = os.path.join(out_dir, "statevardump.hpp")
    gen_statevardump(named_vars, statevardump_path)
    n_status = sum(1 for v in named_vars if v[2] == "Status")
    n_value = sum(1 for v in named_vars if v[2] == "Value")
    n_both = sum(1 for v in named_vars if v[2] == "Both")
    print(f"  {statevardump_path} — {len(named_vars)} vars (Status={n_status} Value={n_value} Both={n_both})")

    # Load the enrichment dumps once, reuse across entitydump/weapondump/herokitdump.
    all_ent_path = os.path.join(dump_dir, "all_entities.json")
    abilities_path = os.path.join(dump_dir, "abilities.json")
    entity_origins_path = os.path.join(dump_dir, "entity_origins.json")
    gn_path_for_ent = os.path.join(dump_dir, "graph_nodes.json")
    all_ent = _load_json_or_none(all_ent_path)
    abilities = _load_json_or_none(abilities_path)
    entity_origins = _load_json_or_none(entity_origins_path)
    gn_for_ent = _load_json_or_none(gn_path_for_ent)

    # entitydump — name_overrides now derived entirely from CASC dumps.
    entitydump_path = os.path.join(out_dir, "entitydump.hpp")
    name_overrides = build_entity_name_overrides(heroes, abilities, entity_origins, loadouts=loadouts)

    # Apply entity CSV overrides: merge or create entries.
    for eid, patch in entity_csv_overrides.items():
        base = name_overrides.get(eid)
        if isinstance(base, dict):
            base.update(patch)   # partial merge — "*" cells inherit
        else:
            # Manually-added entity that wasn't auto-discovered — compose a full entry
            base = {
                "name": patch.get("name", f"ENTITY_{eid:X}"),
                "hero_id": patch.get("hero_id", 0),
                "hero_name": patch.get("hero_name", ""),
                "loadout_id": patch.get("loadout_id", 0),
                "loadout_name": patch.get("loadout_name", ""),
                "button_raw": patch.get("button_raw", ""),
                "slot": patch.get("slot", "None"),
            }
            # Attach type_override so gen_entitydump knows to force the classification
            if "type" in patch: base["__type_override__"] = patch["type"]
            name_overrides[eid] = base
        # If type was overridden on an existing auto entry, stash it too
        if "type" in patch and isinstance(name_overrides.get(eid), dict):
            name_overrides[eid]["__type_override__"] = patch["type"]
    gen_entitydump(heroes, entitydump_path, all_entities=all_ent, name_overrides=name_overrides)
    print(f"  [entity name overrides] {len(name_overrides)} cross-referenced names (from abilities+origins, no forum map)")
    nonp = [h for h in heroes if not h.get("is_hero") and clean(h.get("name_zhCN") or "")]
    if all_ent:
        print(f"  {entitydump_path} — {len(all_ent)} entities from all_entities.json + {len(nonp)} from heroes.json")
    else:
        print(f"  {entitydump_path} — {len(nonp)} non-hero entities (run reader to get all_entities.json for full coverage)")

    # weapondump — prefer abilities.json when available (structured data beats string-matching graph_nodes)
    weapondump_path = os.path.join(out_dir, "weapondump.hpp")
    if abilities:
        gen_weapondump_from_abilities(heroes, abilities, name_overrides, weapondump_path)
        wv_count = sum(len(g.get("weapon_volleys") or []) for h in abilities for g in h.get("graphs") or [])
        print(f"  {weapondump_path} — WeaponVolley entries from abilities.json: {wv_count}")
    else:
        # Fall back to legacy graph_nodes scan
        gn_path = os.path.join(dump_dir, "graph_nodes.json")
        if os.path.exists(gn_path):
            graph_nodes = json.load(open(gn_path, encoding="utf-8"))
            gen_weapondump(heroes, graph_nodes, weapondump_path)
            wv = ae = tt = 0
            for h in graph_nodes:
                for g in h.get("graphs", []):
                    for n in g.get("node_var_refs", []):
                        nt = n.get("node_type", "")
                        if "WeaponVolley" in nt: wv += 1
                        elif nt == "STUStatescriptActionEffect" and n.get("writes"): ae += 1
                        elif nt == "STUStatescriptStateTrackTargets": tt += 1
            print(f"  {weapondump_path} — WeaponVolley={wv} ActionEffect={ae} TrackTargets={tt}")
        else:
            print(f"  [skip] abilities.json and graph_nodes.json both missing — weapondump.hpp not generated")

    # herokitdump — new file: per-hero rollup (loadouts + graph summaries + var usage)
    if abilities:
        herokitdump_path = os.path.join(out_dir, "herokitdump.hpp")
        gen_herokitdump(heroes, abilities, name_overrides, herokitdump_path)
        print(f"  {herokitdump_path} — {len(abilities)} hero kit summaries")


if __name__ == "__main__":
    main()
