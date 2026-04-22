#!/usr/bin/env python3
"""从 CASC dump JSON 生成 C++ SDK header 文件。

Usage: python3 generate_sdk.py <dump_dir> <output_dir>

输入:
  - heroes.json: hero_id → 名字 + 技能列表
  - statescript_data.json: hero → graphs → sync_vars + 节点绑定

输出:
  - hero_data_gen.hpp: 英雄名/技能表（含中文注释）
  - var_data_gen.hpp: 状态变量语义表（含节点类型注释）
"""

import json, os, sys
from collections import defaultdict

def clean(s):
    """清理 CASC 字符串：去除 null、空白、unicode 控制符"""
    if not s: return ""
    return s.replace('\x00','').replace('\u0000','').strip()

def short_node(name):
    """缩短节点类型名"""
    return name.replace("STUStatescript","").replace("State","S_").replace("Action","A_")

def main():
    dump_dir = sys.argv[1] if len(sys.argv) > 1 else "./dump_json"
    out_dir = sys.argv[2] if len(sys.argv) > 2 else "./output"
    os.makedirs(out_dir, exist_ok=True)

    heroes = json.load(open(os.path.join(dump_dir, "heroes.json"), encoding="utf-8"))
    ss_data = json.load(open(os.path.join(dump_dir, "statescript_data.json"), encoding="utf-8"))

    # ====== 构建 hero → abilities 映射 ======
    hero_map = {}
    for h in heroes:
        hid = h["hero_id"]
        if not h.get("is_hero"): continue
        name = clean(h.get("name_zhCN",""))
        abilities = []
        for lo in h.get("loadouts", []):
            cat = lo["category"]
            if cat in ("HeroStats", "Subrole"): continue
            abilities.append({
                "name": clean(lo.get("name_zhCN","")),
                "category": cat,
                "button": clean(lo.get("button","")),
                "guid": lo["guid_index"],
            })
        hero_map[hid] = {"name": name, "abilities": abilities}

    # ====== 构建 var_id → 语义上下文 ======
    var_ctx = defaultdict(lambda: {"heroes": set(), "node_types": set(), "roles": set(), "graphs": set(), "labels": set()})

    graph_ability_map = {}  # (hero_id, graph_idx) → ability guess

    for h in ss_data:
        hid = h["hero_id"]
        hname = clean(h.get("hero_name",""))
        for gi, g in enumerate(h.get("graphs", [])):
            gidx = g.get("graph_index", "?")
            node_types_in_graph = set()
            for node in g.get("node_var_refs", []):
                ntype = node.get("node_type", "?")
                label = clean(node.get("label", ""))
                node_types_in_graph.add(ntype)
                for w in node.get("writes", []):
                    vid = w.get("identifier_index")
                    if vid:
                        ctx = var_ctx[vid]
                        ctx["heroes"].add(hid)
                        ctx["node_types"].add(ntype)
                        ctx["roles"].add(f"W:{short_node(ntype)}")
                        ctx["graphs"].add(f"{hid}:G{gi}")
                        if label: ctx["labels"].add(label)
                for r in node.get("reads", []):
                    vid = r.get("identifier_index")
                    if vid:
                        ctx = var_ctx[vid]
                        ctx["heroes"].add(hid)
                        ctx["node_types"].add(ntype)
                        ctx["roles"].add(f"R:{short_node(ntype)}")
                        ctx["graphs"].add(f"{hid}:G{gi}")

    # ====== 生成 hero_data_gen.hpp ======
    with open(os.path.join(out_dir, "hero_data_gen.hpp"), "w", encoding="utf-8") as f:
        f.write("// 自动生成 - 由 generate_sdk.py 从 CASC 数据 dump 产生\n")
        f.write("// 包含：英雄 ID → 中文名映射、每个英雄的技能列表\n")
        f.write("// 游戏版本：CN build\n")
        f.write("#pragma once\n\n#include <cstdint>\n\nnamespace ow_v2 {\nnamespace gen {\n\n")

        # HeroNameZhCN
        f.write("/// 英雄 ID → 中文名（从 CASC STUHero.m_0EDCE350 提取）\n")
        f.write("inline const char* HeroNameZhCN(uint32_t hero_id) noexcept {\n    switch (hero_id) {\n")
        for h in sorted(heroes, key=lambda x: int(x["hero_id"], 16)):
            if not h.get("is_hero"): continue
            hid = int(h["hero_id"], 16)
            name = clean(h.get("name_zhCN",""))
            f.write(f'    case 0x{hid:X}: return "{name}";\n')
        f.write('    default: return "?";\n    }\n}\n\n')

        # AbilityInfo
        f.write("/// 技能信息结构\n")
        f.write("struct AbilityInfo {\n")
        f.write("    const char* name;      // 技能中文名（从 CASC STULoadout.m_name 提取）\n")
        f.write("    const char* category;  // 类型: Weapon/Ability/UltimateAbility/PassiveAbility\n")
        f.write("    const char* button;    // 按键绑定: 主要攻击模式/技能1/技能2/技能3/辅助攻击模式\n")
        f.write("};\n\n")

        for hid, info in sorted(hero_map.items(), key=lambda x: int(x[0], 16)):
            hid_int = int(hid, 16)
            f.write(f"/// {info['name']} (hero_id=0x{hid_int:X}) 的技能列表\n")
            f.write(f"inline constexpr AbilityInfo kAbilities_{hid_int:X}[] = {{\n")
            for ab in info["abilities"]:
                f.write(f'    {{"{ab["name"]}", "{ab["category"]}", "{ab["button"]}"}},')
                # 添加按键说明注释
                btn = ab["button"]
                cat = ab["category"]
                hint = ""
                if "主要" in btn: hint = " // LMB"
                elif "辅助" in btn: hint = " // RMB"
                elif "技能 1" in btn: hint = " // Shift"
                elif "技能 2" in btn: hint = " // E"
                elif "技能 3" in btn: hint = " // Q (终极技能)"
                elif cat == "PassiveAbility": hint = " // 被动"
                f.write(f"{hint}\n")
            f.write("};\n\n")

        # 技能查找函数
        f.write("/// 根据 hero_id 获取技能列表指针和数量\n")
        f.write("inline bool GetHeroAbilities(uint32_t hero_id, const AbilityInfo*& out, int& count) noexcept {\n")
        f.write("    switch (hero_id) {\n")
        for hid, info in sorted(hero_map.items(), key=lambda x: int(x[0], 16)):
            hid_int = int(hid, 16)
            cnt = len(info["abilities"])
            f.write(f"    case 0x{hid_int:X}: out = kAbilities_{hid_int:X}; count = {cnt}; return true;\n")
        f.write("    default: return false;\n    }\n}\n\n")

        f.write("} // namespace gen\n} // namespace ow_v2\n")

    # ====== 生成 var_data_gen.hpp ======
    with open(os.path.join(out_dir, "var_data_gen.hpp"), "w", encoding="utf-8") as f:
        universal = [(vid, ctx) for vid, ctx in var_ctx.items() if len(ctx["heroes"]) >= 40]

        f.write("// 自动生成 - 由 generate_sdk.py 从 CASC 数据 dump 产生\n")
        f.write("// 包含：状态变量（StateVariable）的语义上下文\n")
        f.write("// 每个变量标注了使用它的节点类型（如 HealthPool = 生命值，Ability = 技能状态）\n")
        f.write("#pragma once\n\n#include <cstdint>\n\nnamespace ow_v2 {\nnamespace gen {\n\n")

        # 通用变量
        f.write(f"/// 全局通用变量（{len(universal)} 个，被 40+ 英雄共用）\n")
        f.write("/// 返回使用该变量的节点类型列表，用于推断变量含义\n")
        f.write("/// 例如: HealthPool = 生命值, Ability = 技能状态, WeaponVolley = 武器参数\n")
        f.write("inline const char* UniversalVarContext(uint16_t var_id) noexcept {\n    switch (var_id) {\n")
        for vid, ctx in sorted(universal, key=lambda x: int(x[0], 16)):
            vid_int = int(vid, 16)
            ntypes = ", ".join(sorted(short_node(n) for n in ctx["node_types"]))[:80]
            hcount = len(ctx["heroes"])
            f.write(f'    case 0x{vid_int:04X}: return "{ntypes}"; // {hcount}个英雄共用\n')
        f.write('    default: return nullptr;\n    }\n}\n\n')

        # 完整变量上下文表
        f.write(f"/// 变量上下文表（共 {len(var_ctx)} 个变量）\n")
        f.write("/// var_id: 运行时 StateVariable 哈希表中的键\n")
        f.write("/// hero_count: 使用此变量的英雄数量（越多越通用）\n")
        f.write("/// primary_node: 最常见的使用节点类型（推断变量用途）\n")
        f.write("///   - S_HealthPool = 生命值/护甲/护盾\n")
        f.write("///   - S_Ability = 技能激活/冷却状态\n")
        f.write("///   - S_WeaponVolley = 武器发射参数\n")
        f.write("///   - A_SetVar = 被脚本动态设置的变量\n")
        f.write("///   - S_ChaseVar = 平滑插值变量（如冷却计时）\n")
        f.write("///   - S_BooleanSwitch = 布尔开关条件\n")
        f.write("///   - S_ModifyHealth = 伤害/治疗操作\n")
        f.write("///   - S_DeflectProjectiles = 弹幕偏转（如 DVa 矩阵、Sigma 吸收）\n")
        f.write("///   - S_UXPresenter = UI 显示控制\n")
        f.write("///   - S_CombatModFilter = 战斗增益/减益过滤\n")
        f.write("/// role: 读写角色（W:=写入, R:=读取）\n")
        f.write("struct VarContext {\n")
        f.write("    uint16_t var_id;          // 运行时哈希表键\n")
        f.write("    uint8_t  hero_count;      // 共用英雄数\n")
        f.write("    const char* primary_node; // 主要节点类型\n")
        f.write("    const char* role;         // 读写角色\n")
        f.write("};\n\n")

        f.write(f"inline constexpr VarContext kVarContextTable[] = {{\n")
        for vid, ctx in sorted(var_ctx.items(), key=lambda x: -len(x[1]["heroes"])):
            vid_int = int(vid, 16)
            hcount = min(255, len(ctx["heroes"]))
            prim = sorted(short_node(n) for n in ctx["node_types"])[0] if ctx["node_types"] else "?"
            role = sorted(ctx["roles"])[0] if ctx["roles"] else "?"
            f.write(f'    {{0x{vid_int:04X}, {hcount:3d}, "{prim[:30]}", "{role[:40]}"}},\n')
        f.write("};\n")
        f.write(f"inline constexpr int kVarContextCount = {len(var_ctx)};\n\n")

        f.write("} // namespace gen\n} // namespace ow_v2\n")

    print(f"生成完成:")
    print(f"  {out_dir}/hero_data_gen.hpp — {len(hero_map)} 个英雄 + 技能映射")
    print(f"  {out_dir}/var_data_gen.hpp  — {len(var_ctx)} 个变量 + 语义上下文, {len(universal)} 个全局变量")

if __name__ == "__main__":
    main()
