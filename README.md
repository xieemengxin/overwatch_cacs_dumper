# Overwatch CASC Dumper

把《守望先锋》CASC 资源库里的英雄 / 技能 / 弹药 / 状态变量完整 dump 出来,生成版本对应的 C++ SDK 头文件。

## 核心能力

- **全自动提取**:50 个可选英雄、793 个 loadout、1957 个 entity 定义、5594 个 state var 使用画像。
- **精确关联**:通过 `STUConfigVarLoadout` 扫描把 graph slot 映射到具体 ability(死神 6/6 技能命中率 100%),通过 `CollectEntityAssetRefsDeep` 反射链扫出 510 个 hero-bound entity。
- **语义分类**:state var 自动分配 `scope`(single_ability / single_hero / multi_hero / global)× `usage_class`(sync_chase / sync_flag / cross_graph / temp 等)两维标签。
- **Slot 级对齐**:每个 global flag/chase var 按 writer loadout 的 button 分布判定归属 slot(LMB/RMB/Shift/E/Ult/Passive),直接命名 `ABILITY_RMB` / `SHIFT_COOLDOWN` / `ULT_FRAME_ACTIVE` 等。
- **CSV 手工覆盖**:`scripts/overrides/` 下三个 CSV 可以补充 CASC 里没有的条目(血包、毒蝶雷、legacy ID 等),或修正 auto-name。
- **零社区枚举**:旧版 generator 里大量 `KNOWN_FORUM_ENTITIES` / `STATUS_NAMES` / `VALUE_PATTERNS` 硬编码全部移除,所有语义来自数据 + 可审计的 CSV。

## 数据流

```
Game Update
   │
   ↓ (inject)
keydump.dll  ─→  keydump.json  (CMF/TRG 密钥)
                        │
                        ↓
              casc_reader  (dotnet 扫描 CASC)
                   │
                   ├─ 输出 JSON 原始 dump ────┐
                   │                          │
                   ↓                          ↓
         dump_json/ (8 个 JSON)        docs/data/ (GitHub Pages)
                   │
                   ↓
            generate_sdk_v2.py
                   │     ↑
                   │   scripts/overrides/*.csv
                   ↓
          output/*.hpp  (6 个 SDK 头文件)
```

## 组件

### `keydump_dll/`
注入游戏进程,通过签名扫描提取 CMF/TRG 解密密钥:
- 用 `AND eax, 800001FFh` pattern 找到 512 字节 keytable
- 提取 Key()/IV() 算法常量
- 扫 Salsa20 keyring
- 把 `keydump.json` 写到游戏 exe 旁边,自动卸载

### `casc_reader/`
.NET 控制台程序,读取并解密 CASC storage,产出结构化 JSON:

| 输出 | 内容 |
|-----|------|
| `heroes.json` | 130 个英雄(50 可选 + 80 PvE/bot),含 loadouts、gameplay_entity、components |
| `loadouts.json` | 793 个 loadout,带 category/button/texture |
| `entity_components.json` | 每个英雄的 component 字段 dump(近 1MB) |
| `graph_nodes.json` | statescript graph 所有 node 的 var refs + config,80MB |
| `graph_sync_vars.json` | 每个 graph 的 SyncVar 列表(identifier + flag) |
| `graph_topology/*.json` | 每英雄一份完整 graph 拓扑(节点 + 连线 + plug) |
| `all_entities.json` | 所有 STUEntityDefinition 扫描结果(1957 条) |
| `entity_origins.json` | 反查 entity spawn 源头 — 哪个 hero 在哪个 graph 创建它(510 条) |
| `abilities.json` | 每 hero 每 graph 的语义聚合:WeaponVolley / ModifyHealth / Deflect / ChaseVar / SetVar / CreateEntity / writes_vars / reads_vars + **associated_loadout_id** |
| `var_names.json` | 从 loadout 配置 slot 发现的 var 及其 source(107 条)|
| `all_locale_strings.json` | 所有 type=0x7C locale string(60K+ 条)|

### `scripts/`

- `generate_sdk_v2.py` — 主生成器,消费上面的 JSON + CSV overrides 产出 SDK。
- `overrides/entities.csv` — 手工补全 entity 条目(血包 / legacy ID / 重命名)。
- `overrides/statevars.csv` — state var 命名覆盖(slot-level 精确命名 + 社区实证 var)。
- `overrides/heroes.csv` — hero 名字覆盖(目前无用,留作兼容)。

### `output/`

**JSON(运行数据)**:同 `casc_reader/` 输出,generator 里会读取。

**SDK 头文件**(项目里直接用):

| 头文件 | 内容 |
|-------|------|
| `namedump.hpp` | 50 个可选英雄的 `HeroInfo`,含 zh-CN 名、性别、size、color |
| `abilitydump.hpp` | 793 个 loadout 的 `Ability` 表,按 hero 分组,含 slot / category |
| `entitydump.hpp` | `EntityInfo { id, name, type, owner_hero_id, owner_hero_name, loadout_id, loadout_name, button_raw, slot }` —— **每个 entity 直接知道是哪个 hero 的哪个 ability 的哪个 slot** |
| `statevardump.hpp` | `StateVar { var_id (uint32_t), name, kind (Status/Value/Both), domain, desc }` —— 4567 条,desc 里带 `[scope][hero/ability][usage_class]` 证据链 |
| `weapondump.hpp` | `WeaponEntry` 弹道参数(speed / lifetime / pellets / fire_rate / projectile_entity_id + resolved name) |
| `herokitdump.hpp` | per-hero 汇总:graph 数量 / has_deflect / has_projectile_weapon / has_beam_weapon / 每个 graph 的 node_type_histogram |

**辅助 CSV/JSON**:
- `vars.csv` —— statevardump 对应的 flat 表(4567 行),excel 打开可排序筛选。
- `var_usage.json` —— 每个 var 的完整 usage profile(writer/reader loadouts + heroes + scope_detail)。4.5MB,给离线审计用。

## 关键增强(相比初版)

### 1. Graph → Loadout 精确关联
`STUStatescriptComponent.m_B634821A` 是 graph 数组,按 slot_index 排列。过去用 slot_index 做 fallback 命名(`死神_slot8`),**不够精确**。现在扫 graph 里所有 `STUConfigVarLoadout` 实例的 `m_loadout`,反查到 hero 的 m_heroLoadout 列表中,得到具体 `associated_loadout_id` + `associated_loadout_name`。

命中率:565 个 graph 里 186 个直接命中(33%),其中**英雄 specific ability graph 100% 命中**(死神 6/6、法老之鹰 5/5 等)。`shared` graph(移动/死亡/通用逻辑)保持 slot_index 作为 fallback。

### 2. Entity 反向追踪
老版只扫 `ActionCreateEntity` / `StateCreateEntity` / `WeaponVolley.m_projectileEntity`,漏掉大量 spawn 路径。现在 `CollectEntityAssetRefsDeep` 反射 walker 覆盖:
- STUHero 自己的 7 个 entity 字段 (`m_322C521A` / `m_26D71549` / `m_8125713E` 等)
- STUEntityDefinition componentMap 里每个 component 的 entity ref 字段(`m_child` / `m_entity`)
- STULoadout 的所有 ConfigVar slots
- WeaponVolley 的 `STU_8556841E.m_entityDef` 等 subclass 字段(reflection 反射而非硬写字段名)

效果:entity origin 从 **211 → 510**,覆盖率从 42% 到 100%(社区实证的 18 个 entity 全部能自动或 CSV 命名)。

### 3. State var 使用分析
每个 var 得到一个 `(base, role)` 复合标签:

| base | 含义 | 示例 |
|------|------|------|
| `sync` | SyncVar(flag != 0 网络同步) | ABILITY_SHIFT |
| `cross_graph` | 跨 graph 读/写,非 sync | 部分 ability transition var |
| `temp` | 单 graph 内中间计算 | 已从 SDK 过滤掉,不入 hpp |
| `unused` | 无 writer/reader 信号 | 过滤掉 |
| `local` | 单 graph 但有 chase/flag 信号 | 少数 |

| role | 含义 | 示例 |
|------|------|------|
| `chase` | ChaseVar 追踪 ≥ 1 次(连续数值) | CD timer / charge |
| `flag` | SetVar 写 ≥ 3 次(bool 翻转) | ability active |
| `writable` | SetVar 1-2 次 | 一次性赋值 |

配合 scope (single_ability/single_hero/multi_hero/global) 可直接判断:**属于哪个 ability 的哪个 slot**。

### 4. Slot 级 ability var 对齐
Overwatch 的 state var 不是 per-ability,而是 **slot 级**:所有 hero 的 Shift 技能共享同一个 `ABILITY_SHIFT` 和 `SHIFT_COOLDOWN` var。现在按每个 var 的 writer loadout 的 button 分布,top% > 60% 就判定归属:

```
0x28E3  ABILITY_SHIFT       → Shift=91%, 54 loadouts (45 heroes)
0x189C  SHIFT_COOLDOWN      → Shift=94%, 48 loadouts (45 heroes)
0x28E9  ABILITY_E           → E=76%,    55 loadouts
0x1F89  E_COOLDOWN          → E=93%,    43 loadouts
0x83F8  ABILITY_RMB         → RMB=71%,  14 loadouts (社区误名 JAVELIN)
0x18D6  ABILITY_RMB_SECONDARY → RMB=90%, 10 loadouts
0x34A   ULT_ACTIVE          → Ult=68%,  71 loadouts
0x1E32  ULT_CHARGE          → ...
0x152   ABILITY_ULT_FRAME_ACTIVE → Ult=98%, 50 loadouts
0x156   ABILITY_ULT_CAST_HANDLE  → Ult=98%, 50 loadouts
```

LMB 和 Passive 没有统一 active var —— 这是 OW 设计决定,不是 dump 遗漏。

### 5. CSV 手工覆盖管道
`scripts/overrides/` 下三个 CSV 文件让用户手工补充 / 修正 SDK 条目。**生成时 CSV 优先于 auto-dump**,并且 CSV 里的字段用 `*` 或空代表"继承自动值"。

```csv
# entities.csv
entity_id, name, type, owner_hero_id, owner_hero_name, loadout_id, loadout_name, button_raw, slot, notes
0x5F,   大血包,      Other, 0,    "",      0, "",      "",     None,  map pickup
0x2658, 秩序之光_哨戒炮台, Turret, 0x16, 秩序之光, 0x36, 哨戒炮台, "技能 1", Shift, legacy ID
```

```csv
# statevars.csv
var_id, name, kind, domain, notes
0x83F8, ABILITY_RMB, Status, ABILITY_STATE, RMB slot active handle (社区旧名 JAVELIN 误导)
```

日志示例:
```
[csv] overrides loaded: 15 entities, 32 vars, 0 heroes
[csv] statevar overrides applied: 22 patched, 10 added new
```

### 6. 社区数据 gap 覆盖
从 `../docs/posts-200-312.csv` 提取社区 21 个实证 state var:
- **12 个** auto dump 命中(fan-out 正确,名字重命名为社区标准)
- **9 个 CSV-only**(native C++ 驱动,不在 statescript 里):AIMING / SNIPER_CHARGE / MEI_ICICLE / RAMATTRA_MODE / WEAPON_MODE / JAVELIN_SPEED / WEAPON_CHARGE / LAST_SHOT_TIME / ABILITY_STATE

以及 18 个社区 entity enum 全部覆盖(9 auto + 9 CSV)。

## Setup

```bash
# 1. 克隆含子模块
git clone --recursive <this-repo>
cd overwatch_casc_dumper

# 2. OWLib 子模块
git submodule add https://github.com/overtools/OWLib.git deps/OWLib

# 3. 把当前游戏版本的 crypto 拷到 OWLib 对应位置
cp casc_reader/crypto/ProCMF_*.cs deps/OWLib/TACTLib/TACTLib/Core/Product/Tank/CMF/
cp casc_reader/crypto/ProTRG_*.cs deps/OWLib/TACTLib/TACTLib/Core/Product/Tank/TRG/

# 4. 编译 + 运行 casc_reader
cd casc_reader
dotnet build -c Release
dotnet run -c Release -- "/path/to/Overwatch" "../output/dump_json"

# 5. 生成 SDK 头文件
cd ..
python3 scripts/generate_sdk_v2.py

# 6.(可选)编辑 scripts/overrides/*.csv 手工补全,再跑一次 generator
```

## 版本更新流程

游戏更新后:
1. 注入 `keydump.dll` → 拿新 crypto key
2. 创建新 `ProCMF_<build>.cs` / `ProTRG_<build>.cs` 扔进 OWLib
3. 跑 `casc_reader` → 新鲜 JSON
4. 跑 `generate_sdk_v2.py` → 新 SDK header
5. 丢进你的项目

## 支持的版本

| Build | Version | 状态 |
|-------|---------|------|
| 148494 | 2.22.0.0.148915N (CN) | ✅ 完整支持 |

## GitHub Pages

`docs/` 下有一个静态站,展示所有 dump 的内容,可直接本地浏览或部署:

```bash
cd docs && python3 -m http.server 8000
# 访问 http://localhost:8000
```

或推到 GitHub 后打开 repo Settings → Pages → Source 选 `docs/`。

## License

For research and educational purposes only.
