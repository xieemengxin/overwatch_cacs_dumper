#!/usr/bin/env python3
"""从 CASC dump JSON 生成面向实战的 gameplay 数据 header。

输出: gameplay_data_gen.hpp 包含：
  1. WeaponInfo: 射击参数（弹速/间隔/射程/弹体/类型）
  2. SkillInfo: 技能参数（CD/层数/ult充能/激活延迟）
  3. BuffInfo: 全局 buff/debuff 变量表

这些是 CASC 静态定义值。运行时当前状态（正在开火、CD剩余等）
通过 state variable 系统读取。
"""

import json, os, sys
from collections import defaultdict

def clean(s):
    if not s: return ""
    return s.replace('\x00','').strip()

def main():
    dump_dir = sys.argv[1] if len(sys.argv) > 1 else "./dump_json"
    out_dir = sys.argv[2] if len(sys.argv) > 2 else "./output"
    os.makedirs(out_dir, exist_ok=True)

    heroes_data = json.load(open(os.path.join(dump_dir, "heroes.json"), encoding="utf-8"))
    node_data = json.load(open(os.path.join(dump_dir, "graph_nodes.json"), encoding="utf-8"))
    sync_data = json.load(open(os.path.join(dump_dir, "graph_sync_vars.json"), encoding="utf-8"))

    # hero_id → hero info
    hero_map = {}
    for h in heroes_data:
        if not h.get("is_hero"): continue
        hid = h["hero_id"]
        hero_map[hid] = {
            "name": clean(h.get("name_zhCN","")),
            "loadouts": h.get("loadouts", []),
        }

    # ====== 1. 提取 Weapon 数据 ======
    weapon_list = []
    for h in node_data:
        hid = h["hero_id"]
        if hid not in hero_map: continue
        hname = hero_map[hid]["name"]

        for gi, g in enumerate(h.get("graphs", [])):
            for n in g.get("node_var_refs", []):
                if "WeaponVolley" not in n.get("node_type", ""): continue
                cfg = {}
                for c in n.get("config", []):
                    f = c["field"]
                    if "Plug" in f or "parentNode" in f: continue
                    cfg[f] = c

                # 提取已知字段
                proj_speed = cfg.get("m_projectileSpeed", {}).get("value")
                proj_lifetime = cfg.get("m_projectileLifetime", {}).get("value")
                shots_per_sec = cfg.get("m_numShotsPerSecond", {}).get("value")
                aim_id = cfg.get("m_aimID", {}).get("value")
                fec_range = cfg.get("m_FEC435C6", {}).get("value")
                has_proj_motion = any("m_projectileMotions" in c["field"] for c in n.get("config",[]))

                # sync var writes (哪些运行时变量被这个 volley 读写)
                writes = [w["identifier_index"] for w in n.get("writes", [])]
                reads = [r["identifier_index"] for r in n.get("reads", [])]

                weapon_list.append({
                    "hero_id": hid, "hero_name": hname, "graph": gi,
                    "type": "projectile" if has_proj_motion else "hitscan",
                    "proj_speed": proj_speed,
                    "proj_lifetime": proj_lifetime,
                    "shots_per_sec": shots_per_sec,
                    "range": fec_range,
                    "writes": writes, "reads": reads,
                })

    # ====== 2. 提取 Skill/Ability 数据 ======
    skill_list = []
    for h in node_data:
        hid = h["hero_id"]
        if hid not in hero_map: continue
        hname = hero_map[hid]["name"]

        for gi, g in enumerate(h.get("graphs", [])):
            for n in g.get("node_var_refs", []):
                if n.get("node_type") != "STUStatescriptStateAbility": continue
                cfg = {}
                for c in n.get("config", []):
                    f = c["field"]
                    if "Plug" in f or "parentNode" in f: continue
                    cfg[f] = c

                activation_delay = cfg.get("m_246438AD", {}).get("value")

                # CD var 和 stack var 从 sync var writes 获取
                cd_var = None
                stack_var = None
                for c in n.get("config", []):
                    if c["field"] == "m_out_CooldownVar" and c.get("value") != "(bound)":
                        cd_var = c.get("value")
                    if c["field"] == "m_out_StackVar" and c.get("value") != "(bound)":
                        stack_var = c.get("value")

                writes = [w["identifier_index"] for w in n.get("writes", [])]

                skill_list.append({
                    "hero_id": hid, "hero_name": hname, "graph": gi,
                    "activation_delay": activation_delay,
                    "cd_var": cd_var, "stack_var": stack_var,
                    "writes": writes,
                })

    # ====== 3. 提取 Buff/Debuff 数据（从跨英雄 sync var 分析）======
    # 通用变量（40+英雄共用）中，布尔型的很可能是 buff/debuff
    universal_vars = defaultdict(lambda: {"heroes": set(), "node_types": set()})
    for h in node_data:
        hid = h["hero_id"]
        for g in h.get("graphs", []):
            for n in g.get("node_var_refs", []):
                ntype = n.get("node_type", "")
                for w in n.get("writes", []):
                    vid = w.get("identifier_index")
                    if vid:
                        universal_vars[vid]["heroes"].add(hid)
                        universal_vars[vid]["node_types"].add(ntype)

    # 已知的 buff/debuff 变量
    KNOWN_BUFFS = {
        "0x200A": ("ANTI_HEAL", "反治疗（安娜瓶）"),
        "0x200B": ("HEAL_BOOST", "治疗加成（安娜瓶）"),
        "0x2018": ("SLEEP", "沉睡（安娜催眠）"),
        "0x2001": ("NANO_BOOST", "纳米激素"),
        "0x1F2E": ("REVEALED", "显形（被探测）"),
        "0x1DB5": ("HACKED", "骇入（黑影）"),
        "0x085F": ("SLOW", "减速"),
        "0x0E44": ("KIRIKO_RUSH", "狐步急袭"),
        "0x0212": ("UNTARGETABLE", "不可选中"),
        "0x097B": ("MEI_SLOW", "美的冰冻减速"),
        "0x09A8": ("MEI_FREEZE", "美的完全冰冻"),
        "0x1038": ("ZEN_DISCORD", "禅雅塔不和之珠"),
        "0x1155": ("ZEN_HARMONY", "禅雅塔和谐之珠"),
        "0xAE41": ("FIRE_CHARGE", "火力值充能"),
        "0x4C87": ("ON_FIRE", "火力全开"),
        "0x28E3": ("ABILITY_SHIFT", "Shift技能状态"),
        "0x28E9": ("ABILITY_E", "E技能状态"),
        "0x02B1": ("ABILITY_LMOUSE", "左键状态"),
        "0x02B2": ("ABILITY_RMOUSE", "右键状态"),
        "0x0156": ("ABILITY_ULT", "终极技能状态"),
        "0x189C": ("CD_SHIFT", "Shift冷却"),
        "0x1F89": ("CD_E", "E冷却"),
        "0x18D6": ("CD_RMOUSE", "右键冷却"),
        "0x1E32": ("ULT_CHARGE", "终极技能充能百分比"),
        "0x1E6A": ("MAX_HP_HEALTH", "基础生命上限"),
        "0x1E6B": ("MAX_HP_ARMOR", "护甲上限"),
        "0x1E6C": ("MAX_HP_SHIELD", "护盾上限"),
        "0x90E2": ("CUR_HP_HEALTH", "当前基础生命"),
        "0x90E3": ("CUR_HP_ARMOR", "当前护甲"),
        "0x2537": ("IS_ALIVE", "存活状态"),
    }

    # ====== 生成 gameplay_data_gen.hpp ======
    with open(os.path.join(out_dir, "gameplay_data_gen.hpp"), "w", encoding="utf-8") as f:
        f.write("// 自动生成 - 面向实战的 gameplay 数据\n")
        f.write("// 数据来源: CASC statescript graph 节点\n")
        f.write("// 静态参数从 CASC 提取，运行时状态通过 state variable 读取\n")
        f.write("#pragma once\n\n#include <cstdint>\n\nnamespace ow_v2 {\nnamespace gen {\n\n")

        # ---- 1. 全局 State Variable 字典 ----
        f.write("// ========================================================\n")
        f.write("// 1. 全局状态变量字典 (State Variables)\n")
        f.write("//    运行时通过 SKILL 组件的 16-slot 哈希表读取\n")
        f.write("//    slot = var_id & 0xF, 每个 entry 16字节: [u16 var_id][pad][u64 ptr]\n")
        f.write("//    ptr 指向 184字节结构: status@+0x48, value(f32)@+0x60\n")
        f.write("// ========================================================\n\n")

        f.write("struct StateVarDef {\n")
        f.write("    uint16_t    var_id;       // 运行时哈希表键\n")
        f.write("    const char* name;         // 英文标识名\n")
        f.write("    const char* description;  // 中文说明\n")
        f.write("};\n\n")

        f.write("/// 已知的通用状态变量（所有英雄共用）\n")
        f.write("inline constexpr StateVarDef kKnownStateVars[] = {\n")
        f.write("    // ---- buff/debuff ----\n")
        for vid_str, (name, desc) in sorted(KNOWN_BUFFS.items(), key=lambda x: int(x[0],16)):
            vid = int(vid_str, 16)
            f.write(f'    {{0x{vid:04X}, "{name}", "{desc}"}},\n')
        f.write("};\n")
        f.write(f"inline constexpr int kKnownStateVarCount = {len(KNOWN_BUFFS)};\n\n")

        # 查找函数
        f.write("/// 根据 var_id 查找已知变量名\n")
        f.write("inline const char* StateVarName(uint16_t var_id) noexcept {\n")
        f.write("    switch (var_id) {\n")
        for vid_str, (name, desc) in sorted(KNOWN_BUFFS.items(), key=lambda x: int(x[0],16)):
            vid = int(vid_str, 16)
            f.write(f'    case 0x{vid:04X}: return "{name}"; // {desc}\n')
        f.write('    default: return nullptr;\n    }\n}\n\n')

        # ---- 2. Weapon 数据 ----
        f.write("// ========================================================\n")
        f.write("// 2. 武器数据 (从 STUStatescriptStateWeaponVolley 节点提取)\n")
        f.write("//    注意: 多数参数在运行时通过 ConfigVar 动态绑定\n")
        f.write("//    这里列出的是 CASC 中的静态默认值\n")
        f.write("// ========================================================\n\n")

        f.write("struct WeaponVolleyDef {\n")
        f.write("    const char* hero_name;       // 所属英雄\n")
        f.write("    int         graph_index;      // 所在 statescript graph 索引\n")
        f.write("    const char* fire_type;        // 射击类型: \"projectile\" 或 \"hitscan\"\n")
        f.write("    int         proj_speed;       // 子弹速度 (m_projectileSpeed)\n")
        f.write("    float       proj_lifetime;    // 子弹存活时间秒 (m_projectileLifetime)\n")
        f.write("    int         shots_per_sec;    // 每秒射击次数 (999=连射)\n")
        f.write("    float       range;            // 射击距离 (m_FEC435C6)\n")
        f.write("};\n\n")

        for hid, info in sorted(hero_map.items(), key=lambda x: int(x[0], 16)):
            hid_int = int(hid, 16)
            hname = info["name"]
            hero_weapons = [w for w in weapon_list if w["hero_id"] == hid]
            if not hero_weapons: continue

            f.write(f"/// {hname} (0x{hid_int:X}) 的武器数据\n")
            f.write(f"inline constexpr WeaponVolleyDef kWeapons_{hid_int:X}[] = {{\n")
            for w in hero_weapons:
                ps = w["proj_speed"] if w["proj_speed"] is not None else 0
                pl = w["proj_lifetime"] if w["proj_lifetime"] is not None else 0
                sps = w["shots_per_sec"] if w["shots_per_sec"] is not None else 0
                rng = w["range"] if w["range"] is not None else 0
                f.write(f'    {{"{hname}", {w["graph"]}, "{w["type"]}", {ps}, {pl}f, {sps}, {rng}f}},\n')
            f.write("};\n\n")

        # ---- 3. Skill/Ability 数据 ----
        f.write("// ========================================================\n")
        f.write("// 3. 技能数据 (从 STUStatescriptStateAbility 节点提取)\n")
        f.write("//    CD 和层数通过绑定的 state variable 在运行时读取\n")
        f.write("//    activation_delay = 按键到生效的延迟时间\n")
        f.write("// ========================================================\n\n")

        f.write("struct AbilityNodeDef {\n")
        f.write("    const char* hero_name;         // 所属英雄\n")
        f.write("    int         graph_index;        // 所在 graph 索引（可对应到 ability loadout）\n")
        f.write("    float       activation_delay;   // 激活延迟(秒)\n")
        f.write("};\n\n")

        for hid, info in sorted(hero_map.items(), key=lambda x: int(x[0], 16)):
            hid_int = int(hid, 16)
            hname = info["name"]
            hero_skills = [s for s in skill_list if s["hero_id"] == hid]
            if not hero_skills: continue

            f.write(f"/// {hname} (0x{hid_int:X}) 的技能节点\n")
            f.write(f"inline constexpr AbilityNodeDef kAbilityNodes_{hid_int:X}[] = {{\n")
            for s in hero_skills:
                ad = s["activation_delay"] if s["activation_delay"] is not None else 0
                f.write(f'    {{"{hname}", {s["graph"]}, {ad}f}},\n')
            f.write("};\n\n")

        # ---- 4. 运行时读取指南 ----
        f.write("// ========================================================\n")
        f.write("// 4. 运行时数据读取指南\n")
        f.write("// ========================================================\n")
        f.write("//\n")
        f.write("// 读取 state variable 的方法:\n")
        f.write("//   1. 获取 entity 的 SKILL 组件: DecryptComponent(entity, 0x37)\n")
        f.write("//   2. 定位哈希表: skill_base + 0xD0 + 0x4E0 + 32*(var_id & 0xF + 1)\n")
        f.write("//   3. 遍历 slot entries (16字节): RPM<u16>(entry) == var_id\n")
        f.write("//   4. 读取值: ptr = RPM<u64>(entry+8); status = RPM<u8>(ptr+0x48); value = RPM<f32>(ptr+0x60)\n")
        f.write("//\n")
        f.write("// 判断武器状态:\n")
        f.write("//   - 开火中: kKnownStateVars ABILITY_LMOUSE(0x02B1) status==1\n")
        f.write("//   - 副射击: kKnownStateVars ABILITY_RMOUSE(0x02B2) status==1\n")
        f.write("//   - Shift技能: ABILITY_SHIFT(0x28E3) status==1\n")
        f.write("//   - E技能: ABILITY_E(0x28E9) status==1\n")
        f.write("//   - 大招激活: ABILITY_ULT(0x0156) status==1\n")
        f.write("//\n")
        f.write("// 判断充能/CD:\n")
        f.write("//   - 大招充能%: ULT_CHARGE(0x1E32) value (0~100)\n")
        f.write("//   - Shift CD: CD_SHIFT(0x189C) status==1 时在冷却, value=剩余秒数\n")
        f.write("//   - E CD: CD_E(0x1F89) status==1 时在冷却\n")
        f.write("//\n")
        f.write("// 判断 buff/debuff:\n")
        f.write("//   - 反治疗: ANTI_HEAL(0x200A) status==1\n")
        f.write("//   - 沉睡: SLEEP(0x2018) status==1\n")
        f.write("//   - 骇入: HACKED(0x1DB5) status==1\n")
        f.write("//   - 纳米: NANO_BOOST(0x2001) status==1\n")
        f.write("//   - 不和: ZEN_DISCORD(0x1038) status==1\n")
        f.write("//\n")
        f.write("// HP:\n")
        f.write("//   - 当前HP: CUR_HP_HEALTH(0x90E2) value\n")
        f.write("//   - 最大HP: MAX_HP_HEALTH(0x1E6A) value\n")
        f.write("//   - 护甲: MAX_HP_ARMOR(0x1E6B) / CUR_HP_ARMOR(0x90E3)\n")
        f.write("//   - 存活: IS_ALIVE(0x2537) status==1\n\n")

        f.write("} // namespace gen\n} // namespace ow_v2\n")

    print(f"生成: {out_dir}/gameplay_data_gen.hpp")
    print(f"  武器: {len(weapon_list)} 个 WeaponVolley 节点")
    print(f"  技能: {len(skill_list)} 个 Ability 节点")
    print(f"  已知变量: {len(KNOWN_BUFFS)} 个")

if __name__ == "__main__":
    main()
