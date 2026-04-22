// 自动生成 - 由 generate_sdk.py 从 CASC 数据 dump 产生
// 包含：英雄 ID → 中文名映射、每个英雄的技能列表
// 游戏版本：CN build
#pragma once

#include <cstdint>

namespace ow_v2 {
namespace gen {

/// 英雄 ID → 中文名（从 CASC STUHero.m_0EDCE350 提取）
inline const char* HeroNameZhCN(uint32_t hero_id) noexcept {
    switch (hero_id) {
    case 0x2: return "死神";
    case 0x3: return "猎空";
    case 0x4: return "天使";
    case 0x5: return "半藏";
    case 0x6: return "托比昂";
    case 0x7: return "莱因哈特";
    case 0x8: return "法老之鹰";
    case 0x9: return "温斯顿";
    case 0xA: return "黑百合";
    case 0x15: return "堡垒";
    case 0x16: return "秩序之光";
    case 0x20: return "禅雅塔";
    case 0x29: return "源氏";
    case 0x40: return "路霸";
    case 0x42: return "卡西迪";
    case 0x65: return "狂鼠";
    case 0x68: return "查莉娅";
    case 0x6E: return "士兵：76";
    case 0x79: return "卢西奥";
    case 0x7A: return "D.Va";
    case 0xDD: return "美";
    case 0x12E: return "黑影";
    case 0x12F: return "末日铁拳";
    case 0x13B: return "安娜";
    case 0x13E: return "奥丽莎";
    case 0x195: return "布丽吉塔";
    case 0x1A2: return "莫伊拉";
    case 0x1CA: return "破坏球";
    case 0x1EC: return "索杰恩";
    case 0x200: return "艾什";
    case 0x206: return "回声";
    case 0x221: return "巴蒂斯特";
    case 0x231: return "雾子";
    case 0x236: return "渣客女王";
    case 0x23B: return "西格玛";
    case 0x28D: return "拉玛刹";
    case 0x291: return "生命之梭";
    case 0x30A: return "毛加";
    case 0x31C: return "伊拉锐";
    case 0x32A: return "弗蕾娅";
    case 0x32B: return "探奇";
    case 0x362: return "骇灾";
    case 0x365: return "朱诺";
    case 0x3C3: return "无漾";
    case 0x472: return "斩仇";
    case 0x4C4: return "金驭";
    case 0x4D8: return "埃姆雷";
    case 0x4DD: return "安燃";
    case 0x4E3: return "瑞稀";
    case 0x516: return "飞天猫";
    default: return "?";
    }
}

/// 技能信息结构
struct AbilityInfo {
    const char* name;      // 技能中文名（从 CASC STULoadout.m_name 提取）
    const char* category;  // 类型: Weapon/Ability/UltimateAbility/PassiveAbility
    const char* button;    // 按键绑定: 主要攻击模式/技能1/技能2/技能3/辅助攻击模式
};

/// 死神 (hero_id=0x2) 的技能列表
inline constexpr AbilityInfo kAbilities_2[] = {
    {"死亡绽放", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"地狱火霰弹枪", "Weapon", "主要攻击模式"}, // LMB
    {"暗影步", "Ability", "技能 2"}, // E
    {"收割", "PassiveAbility", ""}, // 被动
    {"幽灵形态", "Ability", "技能 1"}, // Shift
    {"恐怖扳机", "Ability", "辅助攻击模式"}, // RMB
};

/// 猎空 (hero_id=0x3) 的技能列表
inline constexpr AbilityInfo kAbilities_3[] = {
    {"闪现", "Ability", "技能 1"}, // Shift
    {"闪回", "Ability", "技能 2"}, // E
    {"脉冲炸弹", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"脉冲双枪", "Weapon", "主要攻击模式"}, // LMB
};

/// 天使 (hero_id=0x4) 的技能列表
inline constexpr AbilityInfo kAbilities_4[] = {
    {"天使之杖", "Weapon", "主要攻击模式"}, // LMB
    {"守护天使", "Ability", "技能 1"}, // Shift
    {"天使之杖", "Weapon", "辅助攻击模式"}, // RMB
    {"重生", "Ability", "技能 2"}, // E
    {"快速治疗", "Ability", "装填弹药"},
    {"天使冲击枪", "Weapon", "主要攻击模式"}, // LMB
    {"天使降临", "PassiveAbility", "跳跃"}, // 被动
    {"迅捷恢复", "PassiveAbility", ""}, // 被动
    {"女武神", "UltimateAbility", "技能 3"}, // Q (终极技能)
};

/// 半藏 (hero_id=0x5) 的技能列表
inline constexpr AbilityInfo kAbilities_5[] = {
    {"竜", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"音", "Ability", "技能 1"}, // Shift
    {"岚", "Ability", "技能 2"}, // E
    {"风", "Weapon", "主要攻击模式"}, // LMB
    {"攀", "PassiveAbility", "跳跃"}, // 被动
    {"跃", "Ability", "跳跃"},
};

/// 托比昂 (hero_id=0x6) 的技能列表
inline constexpr AbilityInfo kAbilities_6[] = {
    {"部署炮台", "Ability", "技能 1"}, // Shift
    {"热力过载", "Ability", "技能 2"}, // E
    {"熔火核心", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"铆钉枪", "Weapon", "主要攻击模式"}, // LMB
    {"辅助攻击模式", "Weapon", "辅助攻击模式"}, // RMB
    {"锻造锤", "Weapon", "主要攻击模式"}, // LMB
};

/// 莱因哈特 (hero_id=0x7) 的技能列表
inline constexpr AbilityInfo kAbilities_7[] = {
    {"冲锋", "Ability", "技能 1"}, // Shift
    {"裂地猛击", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"烈焰打击", "Ability", "技能 2"}, // E
    {"火箭重锤", "Weapon", "主要攻击模式"}, // LMB
    {"屏障力场", "Ability", "辅助攻击模式"}, // RMB
    {"职责：重装", "PassiveAbility", ""}, // 被动
};

/// 法老之鹰 (hero_id=0x8) 的技能列表
inline constexpr AbilityInfo kAbilities_8[] = {
    {"火箭弹幕", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"推进背包", "Ability", "技能 1"}, // Shift
    {"震荡冲击", "Ability", "技能 2"}, // E
    {"火箭发射器", "Weapon", "主要攻击模式"}, // LMB
    {"悬浮背包", "PassiveAbility", "跳跃"}, // 被动
    {"疾冲背包", "Ability", "辅助攻击模式"}, // RMB
};

/// 温斯顿 (hero_id=0x9) 的技能列表
inline constexpr AbilityInfo kAbilities_9[] = {
    {"喷射背包", "Ability", "技能 1"}, // Shift
    {"原始暴怒", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"屏障发射器", "Ability", "技能 2"}, // E
    {"特斯拉炮", "Weapon", "主要攻击模式"}, // LMB
    {"辅助攻击模式", "Weapon", "辅助攻击模式"}, // RMB
    {"职责：重装", "PassiveAbility", ""}, // 被动
};

/// 黑百合 (hero_id=0xA) 的技能列表
inline constexpr AbilityInfo kAbilities_A[] = {
    {"抓钩", "Ability", "技能 1"}, // Shift
    {"红外侦测", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"剧毒诡雷", "Ability", "技能 2"}, // E
    {"黑百合之吻", "Weapon", "主要攻击模式"}, // LMB
    {"瞄准射击", "Weapon", "辅助攻击模式"}, // RMB
};

/// 堡垒 (hero_id=0x15) 的技能列表
inline constexpr AbilityInfo kAbilities_15[] = {
    {"A-36战术榴弹", "Ability", "辅助攻击模式"}, // RMB
    {"切换模式", "Ability", "技能 1"}, // Shift
    {"侦察模式", "Weapon", "主要攻击模式"}, // LMB
    {"强攻模式", "Weapon", "主要攻击模式"}, // LMB
    {"火炮模式", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"抗击装甲", "PassiveAbility", ""}, // 被动
};

/// 秩序之光 (hero_id=0x16) 的技能列表
inline constexpr AbilityInfo kAbilities_16[] = {
    {"光子发射器", "Weapon", "主要攻击模式"}, // LMB
    {"辅助攻击模式", "Weapon", "辅助攻击模式"}, // RMB
    {"哨戒炮", "Ability", "技能 1"}, // Shift
    {"光子屏障", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"传送面板", "Ability", "技能 2"}, // E
};

/// 禅雅塔 (hero_id=0x20) 的技能列表
inline constexpr AbilityInfo kAbilities_20[] = {
    {"灭", "Weapon", "主要攻击模式"}, // LMB
    {"辅助攻击模式", "Weapon", "辅助攻击模式"}, // RMB
    {"乱", "Ability", "技能 2"}, // E
    {"谐", "Ability", "技能 1"}, // Shift
    {"圣", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"蹬", "PassiveAbility", "快速近身攻击"}, // 被动
};

/// 源氏 (hero_id=0x29) 的技能列表
inline constexpr AbilityInfo kAbilities_29[] = {
    {"斩", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"闪", "Ability", "技能 2"}, // E
    {"影", "Ability", "技能 1"}, // Shift
    {"镖", "Weapon", "主要攻击模式"}, // LMB
    {"辅助攻击模式", "Weapon", "辅助攻击模式"}, // RMB
    {"灵", "PassiveAbility", "跳跃"}, // 被动
};

/// 路霸 (hero_id=0x40) 的技能列表
inline constexpr AbilityInfo kAbilities_40[] = {
    {"鸡飞狗跳", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"爆裂枪", "Weapon", "主要攻击模式"}, // LMB
    {"辅助攻击模式", "Weapon", "辅助攻击模式"}, // RMB
    {"链钩", "Ability", "技能 1"}, // Shift
    {"呼吸器", "Ability", "技能 2"}, // E
    {"职责：重装", "PassiveAbility", ""}, // 被动
};

/// 卡西迪 (hero_id=0x42) 的技能列表
inline constexpr AbilityInfo kAbilities_42[] = {
    {"神射手", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"战术翻滚", "Ability", "技能 1"}, // Shift
    {"闪光弹", "Ability", "技能 2"}, // E
    {"维和者", "Weapon", "主要攻击模式"}, // LMB
    {"连射", "Weapon", "辅助攻击模式"}, // RMB
};

/// 狂鼠 (hero_id=0x65) 的技能列表
inline constexpr AbilityInfo kAbilities_65[] = {
    {"榴弹发射器", "Weapon", "主要攻击模式"}, // LMB
    {"震荡地雷", "Ability", "技能 1"}, // Shift
    {"捕兽夹", "Ability", "技能 2"}, // E
    {"炸弹轮胎", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"临别礼物", "PassiveAbility", ""}, // 被动
};

/// 查莉娅 (hero_id=0x68) 的技能列表
inline constexpr AbilityInfo kAbilities_68[] = {
    {"重力喷涌", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"粒子屏障", "Ability", "技能 1"}, // Shift
    {"投射屏障", "Ability", "技能 2"}, // E
    {"粒子炮", "Weapon", "主要攻击模式"}, // LMB
    {"辅助攻击模式", "Weapon", "辅助攻击模式"}, // RMB
    {"能量转换", "PassiveAbility", ""}, // 被动
    {"职责：重装", "PassiveAbility", ""}, // 被动
};

/// 士兵：76 (hero_id=0x6E) 的技能列表
inline constexpr AbilityInfo kAbilities_6E[] = {
    {"疾跑", "Ability", "技能 1"}, // Shift
    {"生物力场", "Ability", "技能 2"}, // E
    {"重型脉冲步枪", "Weapon", "主要攻击模式"}, // LMB
    {"螺旋飞弹", "Ability", "辅助攻击模式"}, // RMB
    {"战术目镜", "UltimateAbility", "技能 3"}, // Q (终极技能)
};

/// 卢西奥 (hero_id=0x79) 的技能列表
inline constexpr AbilityInfo kAbilities_79[] = {
    {"切歌", "Ability", "技能 1"}, // Shift
    {"强音", "Ability", "技能 2"}, // E
    {"音速扩音器", "Weapon", "主要攻击模式"}, // LMB
    {"音波", "Ability", "辅助攻击模式"}, // RMB
    {"音障", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"滑墙", "PassiveAbility", "跳跃"}, // 被动
};

/// D.Va (hero_id=0x7A) 的技能列表
inline constexpr AbilityInfo kAbilities_7A[] = {
    {"推进器", "Ability", "技能 1"}, // Shift
    {"防御矩阵", "Ability", "辅助攻击模式"}, // RMB
    {"自毁", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"聚变机炮", "Weapon", "主要攻击模式"}, // LMB
    {"微型飞弹", "Ability", "技能 2"}, // E
    {"弹射！", "PassiveAbility", ""}, // 被动
    {"呼叫机甲", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"光枪", "Weapon", "主要攻击模式"}, // LMB
    {"职责：重装", "PassiveAbility", ""}, // 被动
};

/// 美 (hero_id=0xDD) 的技能列表
inline constexpr AbilityInfo kAbilities_DD[] = {
    {"暴雪", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"急冻", "Ability", "技能 1"}, // Shift
    {"冰墙", "Ability", "技能 2"}, // E
    {"冰霜冲击枪", "Weapon", "主要攻击模式"}, // LMB
    {"冰锥", "Weapon", "辅助攻击模式"}, // RMB
};

/// 黑影 (hero_id=0x12E) 的技能列表
inline constexpr AbilityInfo kAbilities_12E[] = {
    {"自动手枪", "Weapon", "主要攻击模式"}, // LMB
    {"黑客入侵", "Ability", "辅助攻击模式"}, // RMB
    {"病毒侵染", "Ability", "技能 1"}, // Shift
    {"位移传动", "Ability", "技能 2"}, // E
    {"电磁脉冲", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"相时而动", "PassiveAbility", ""}, // 被动
};

/// 末日铁拳 (hero_id=0x12F) 的技能列表
inline constexpr AbilityInfo kAbilities_12F[] = {
    {"裂地重拳", "Ability", "技能 1"}, // Shift
    {"悍猛格挡", "Ability", "技能 2"}, // E
    {"火箭重拳", "Ability", "辅助攻击模式"}, // RMB
    {"毁天灭地", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"手炮", "Weapon", "主要攻击模式"}, // LMB
    {"最佳防守……", "PassiveAbility", ""}, // 被动
    {"职责：重装", "PassiveAbility", ""}, // 被动
};

/// 安娜 (hero_id=0x13B) 的技能列表
inline constexpr AbilityInfo kAbilities_13B[] = {
    {"麻醉镖", "Ability", "技能 1"}, // Shift
    {"生物步枪", "Weapon", "主要攻击模式"}, // LMB
    {"生物手雷", "Ability", "技能 2"}, // E
    {"纳米激素", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"瞄准射击", "Weapon", "辅助攻击模式"}, // RMB
};

/// 奥丽莎 (hero_id=0x13E) 的技能列表
inline constexpr AbilityInfo kAbilities_13E[] = {
    {"能量标枪", "Ability", "辅助攻击模式"}, // RMB
    {"强固防御", "Ability", "技能 1"}, // Shift
    {"标枪旋击", "Ability", "技能 2"}, // E
    {"撼地猛刺", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"强化聚变驱动器", "Weapon", "主要攻击模式"}, // LMB
    {"职责：重装", "PassiveAbility", ""}, // 被动
};

/// 布丽吉塔 (hero_id=0x195) 的技能列表
inline constexpr AbilityInfo kAbilities_195[] = {
    {"集结号令", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"鼓舞士气", "PassiveAbility", ""}, // 被动
    {"恢复包", "Ability", "技能 2"}, // E
    {"流星飞锤", "Ability", "技能 1"}, // Shift
    {"火箭连枷", "Weapon", "主要攻击模式"}, // LMB
    {"屏障护盾", "Ability", "辅助攻击模式"}, // RMB
    {"能量盾击", "Ability", "主要攻击模式"}, // LMB
};

/// 莫伊拉 (hero_id=0x1A2) 的技能列表
inline constexpr AbilityInfo kAbilities_1A2[] = {
    {"生化之触", "Weapon", "主要攻击模式"}, // LMB
    {"辅助攻击模式", "Weapon", "辅助攻击模式"}, // RMB
    {"生化之球", "Ability", "技能 2"}, // E
    {"消散", "Ability", "技能 1"}, // Shift
    {"聚合射线", "UltimateAbility", "技能 3"}, // Q (终极技能)
};

/// 破坏球 (hero_id=0x1CA) 的技能列表
inline constexpr AbilityInfo kAbilities_1CA[] = {
    {"四联火炮", "Weapon", "主要攻击模式"}, // LMB
    {"地雷禁区", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"工程抓钩", "Ability", "辅助攻击模式"}, // RMB
    {"动力铁球", "Ability", "技能 1"}, // Shift
    {"重力坠击", "Ability", "下蹲"},
    {"感应护盾", "Ability", "技能 2"}, // E
    {"职责：重装", "PassiveAbility", ""}, // 被动
};

/// 索杰恩 (hero_id=0x1EC) 的技能列表
inline constexpr AbilityInfo kAbilities_1EC[] = {
    {"机动滑铲", "Ability", "技能 1"}, // Shift
    {"干扰弹", "Ability", "技能 2"}, // E
    {"电磁炮", "Weapon", "主要攻击模式"}, // LMB
    {"充能射击", "Weapon", "辅助攻击模式"}, // RMB
    {"机体超频", "UltimateAbility", "技能 3"}, // Q (终极技能)
};

/// 艾什 (hero_id=0x200) 的技能列表
inline constexpr AbilityInfo kAbilities_200[] = {
    {"短筒猎枪", "Ability", "技能 1"}, // Shift
    {"召唤鲍勃", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"延时雷管", "Ability", "技能 2"}, // E
    {"毒蛇", "Weapon", "主要攻击模式"}, // LMB
    {"瞄准射击", "Weapon", "辅助攻击模式"}, // RMB
};

/// 回声 (hero_id=0x206) 的技能列表
inline constexpr AbilityInfo kAbilities_206[] = {
    {"黏性炸弹", "Ability", "辅助攻击模式"}, // RMB
    {"人格复制", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"三角射击", "Weapon", "主要攻击模式"}, // LMB
    {"飞行", "Ability", "技能 1"}, // Shift
    {"聚焦光线", "Ability", "技能 2"}, // E
    {"滑翔", "PassiveAbility", "跳跃"}, // 被动
};

/// 巴蒂斯特 (hero_id=0x221) 的技能列表
inline constexpr AbilityInfo kAbilities_221[] = {
    {"愈合冲击", "Ability", "技能 1"}, // Shift
    {"维生力场", "Ability", "技能 2"}, // E
    {"增幅矩阵", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"生化榴弹枪", "Weapon", "主要攻击模式"}, // LMB
    {"辅助攻击模式", "Weapon", "辅助攻击模式"}, // RMB
    {"动力战靴", "PassiveAbility", "下蹲"}, // 被动
};

/// 雾子 (hero_id=0x231) 的技能列表
inline constexpr AbilityInfo kAbilities_231[] = {
    {"狐", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"符", "Weapon", "主要攻击模式"}, // LMB
    {"锥", "Weapon", "辅助攻击模式"}, // RMB
    {"瞬", "Ability", "技能 1"}, // Shift
    {"铃", "Ability", "技能 2"}, // E
    {"攀", "PassiveAbility", "跳跃"}, // 被动
};

/// 渣客女王 (hero_id=0x236) 的技能列表
inline constexpr AbilityInfo kAbilities_236[] = {
    {"锯齿利刃（格雷西）", "Ability", "辅助攻击模式"}, // RMB
    {"命令怒吼", "Ability", "技能 1"}, // Shift
    {"散弹枪", "Weapon", "主要攻击模式"}, // LMB
    {"血斩", "Ability", "技能 2"}, // E
    {"轰翻天", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"狂血奔涌", "PassiveAbility", ""}, // 被动
    {"职责：重装", "PassiveAbility", ""}, // 被动
};

/// 西格玛 (hero_id=0x23B) 的技能列表
inline constexpr AbilityInfo kAbilities_23B[] = {
    {"动能俘获", "Ability", "技能 1"}, // Shift
    {"质量吸附", "Ability", "技能 2"}, // E
    {"引力乱流", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"超能之球", "Weapon", "主要攻击模式"}, // LMB
    {"实验屏障", "Ability", "辅助攻击模式"}, // RMB
    {"职责：重装", "PassiveAbility", ""}, // 被动
};

/// 拉玛刹 (hero_id=0x28D) 的技能列表
inline constexpr AbilityInfo kAbilities_28D[] = {
    {"虚空加速器（智械形态）", "Weapon", "主要攻击模式"}, // LMB
    {"猛拳（天罚形态）", "Weapon", "主要攻击模式"}, // LMB
    {"虚空屏障（智械形态）", "Ability", "辅助攻击模式"}, // RMB
    {"天罚形态", "Ability", "技能 1"}, // Shift
    {"铁臂（天罚形态）", "Ability", "辅助攻击模式"}, // RMB
    {"吞噬漩涡", "Ability", "技能 2"}, // E
    {"诛", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"职责：重装", "PassiveAbility", ""}, // 被动
};

/// 生命之梭 (hero_id=0x291) 的技能列表
inline constexpr AbilityInfo kAbilities_291[] = {
    {"愈疗灵花", "Weapon", "主要攻击模式"}, // LMB
    {"棘刺箭雨", "Weapon", "辅助攻击模式"}, // RMB
    {"花瓣平台", "Ability", "技能 1"}, // Shift
    {"回春疾行", "Ability", "跳跃"},
    {"生命之握", "Ability", "技能 2"}, // E
    {"生命之树", "UltimateAbility", "技能 3"}, // Q (终极技能)
};

/// 毛加 (hero_id=0x30A) 的技能列表
inline constexpr AbilityInfo kAbilities_30A[] = {
    {"蛮力冲撞", "Ability", "技能 1"}, // Shift
    {"心脏过载", "Ability", "技能 2"}, // E
    {"笼中斗", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"燃火链式机枪（老枪）", "Weapon", "主要攻击模式"}, // LMB
    {"爆烈链式机枪（恰恰）", "Weapon", "辅助攻击模式"}, // RMB
    {"狂战士", "PassiveAbility", ""}, // 被动
    {"职责：重装", "PassiveAbility", ""}, // 被动
};

/// 伊拉锐 (hero_id=0x31C) 的技能列表
inline constexpr AbilityInfo kAbilities_31C[] = {
    {"烈日冲击", "Ability", "技能 1"}, // Shift
    {"治疗光塔", "Ability", "技能 2"}, // E
    {"桎梏灼日", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"阳焰步枪", "Weapon", "主要攻击模式"}, // LMB
    {"辅助攻击模式", "Weapon", "辅助攻击模式"}, // RMB
};

/// 弗蕾娅 (hero_id=0x32A) 的技能列表
inline constexpr AbilityInfo kAbilities_32A[] = {
    {"速射弩", "Weapon", "主要攻击模式"}, // LMB
    {"瞄准射击", "Weapon", "辅助攻击模式"}, // RMB
    {"疾冲", "Ability", "技能 1"}, // Shift
    {"上升气流", "Ability", "技能 2"}, // E
    {"流星索", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"赏金狩猎", "PassiveAbility", ""}, // 被动
};

/// 探奇 (hero_id=0x32B) 的技能列表
inline constexpr AbilityInfo kAbilities_32B[] = {
    {"智能挖掘钻", "Weapon", "主要攻击模式"}, // LMB
    {"钻地", "Ability", "技能 1"}, // Shift
    {"钻头突刺", "Ability", "辅助攻击模式"}, // RMB
    {"地壳震击", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"猛钻", "PassiveAbility", "快速近身攻击"}, // 被动
    {"探险家的决心", "PassiveAbility", ""}, // 被动
};

/// 骇灾 (hero_id=0x362) 的技能列表
inline constexpr AbilityInfo kAbilities_362[] = {
    {"千针雨", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"骨刺", "Weapon", "主要攻击模式"}, // LMB
    {"尖刺护体", "Ability", "辅助攻击模式"}, // RMB
    {"狂跃", "Ability", "技能 1"}, // Shift
    {"尖刺墙", "Ability", "技能 2"}, // E
    {"翻越", "PassiveAbility", "跳跃"}, // 被动
    {"职责：重装", "PassiveAbility", ""}, // 被动
};

/// 朱诺 (hero_id=0x365) 的技能列表
inline constexpr AbilityInfo kAbilities_365[] = {
    {"医疗冲击枪", "Weapon", "主要攻击模式"}, // LMB
    {"脉冲星飞雷", "Ability", "辅助攻击模式"}, // RMB
    {"滑翔推进", "Ability", "技能 1"}, // Shift
    {"超能环域", "Ability", "技能 2"}, // E
    {"轨道射线", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"火星套靴", "PassiveAbility", "跳跃"}, // 被动
};

/// 无漾 (hero_id=0x3C3) 的技能列表
inline constexpr AbilityInfo kAbilities_3C3[] = {
    {"玄武杖", "Weapon", "主要攻击模式"}, // LMB
    {"翻江浪", "Ability", "技能 2"}, // E
    {"飞流步", "Ability", "技能 1"}, // Shift
    {"养神泉", "Weapon", "辅助攻击模式"}, // RMB
    {"惊涛破", "UltimateAbility", "技能 3"}, // Q (终极技能)
};

/// 斩仇 (hero_id=0x472) 的技能列表
inline constexpr AbilityInfo kAbilities_472[] = {
    {"旋风疾步", "Ability", "技能 1"}, // Shift
    {"飞空斩击", "Ability", "技能 2"}, // E
    {"帕拉蒂尼之牙", "Weapon", "主要攻击模式"}, // LMB
    {"招架姿态", "Weapon", "辅助攻击模式"}, // RMB
    {"锋锐剑气", "Ability", "主要攻击模式"}, // LMB
    {"斩地巨剑", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"越战越勇", "PassiveAbility", ""}, // 被动
};

/// 金驭 (hero_id=0x4C4) 的技能列表
inline constexpr AbilityInfo kAbilities_4C4[] = {
    {"光子马格南", "Weapon", "主要攻击模式"}, // LMB
    {"屏障阵列", "Ability", "辅助攻击模式"}, // RMB
    {"音速斥力场", "Ability", "技能 1"}, // Shift
    {"爆能水晶", "Ability", "技能 2"}, // E
    {"全景牢笼", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"护盾重构", "PassiveAbility", ""}, // 被动
    {"职责：重装", "PassiveAbility", ""}, // 被动
};

/// 埃姆雷 (hero_id=0x4D8) 的技能列表
inline constexpr AbilityInfo kAbilities_4D8[] = {
    {"合成连发步枪", "Weapon", "主要攻击模式"}, // LMB
    {"瞄准射击", "Weapon", "辅助攻击模式"}, // RMB
    {"虹吸冲击枪", "Ability", "技能 1"}, // Shift
    {"赛博手雷", "Ability", "技能 2"}, // E
    {"覆盖协议", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"机变体征", "PassiveAbility", ""}, // 被动
};

/// 安燃 (hero_id=0x4DD) 的技能列表
inline constexpr AbilityInfo kAbilities_4DD[] = {
    {"朱雀扇", "Weapon", "主要攻击模式"}, // LMB
    {"煽火风", "Weapon", "辅助攻击模式"}, // RMB
    {"怒炎冲", "Ability", "技能 1"}, // Shift
    {"熠闪舞", "Ability", "技能 2"}, // E
    {"朱羽焚", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"朱魂返", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"焚身焰", "PassiveAbility", ""}, // 被动
};

/// 瑞稀 (hero_id=0x4E3) 的技能列表
inline constexpr AbilityInfo kAbilities_4E3[] = {
    {"御魂镰刃", "Weapon", "主要攻击模式"}, // LMB
    {"疗魂斗笠", "Ability", "辅助攻击模式"}, // RMB
    {"缚魂锁链", "Ability", "技能 2"}, // E
    {"替魂纸人", "Ability", "技能 1"}, // Shift
    {"护魂结界", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"安魂灵气", "PassiveAbility", ""}, // 被动
};

/// 飞天猫 (hero_id=0x516) 的技能列表
inline constexpr AbilityInfo kAbilities_516[] = {
    {"生物猫爪弹", "Weapon", ""},
    {"咻咻飞", "Ability", "辅助攻击模式"}, // RMB
    {"救生索", "Ability", "技能 1"}, // Shift
    {"呼噜噜", "Ability", "技能 2"}, // E
    {"猫猫劫", "UltimateAbility", "技能 3"}, // Q (终极技能)
    {"喷气背包", "PassiveAbility", ""}, // 被动
};

/// 根据 hero_id 获取技能列表指针和数量
inline bool GetHeroAbilities(uint32_t hero_id, const AbilityInfo*& out, int& count) noexcept {
    switch (hero_id) {
    case 0x2: out = kAbilities_2; count = 6; return true;
    case 0x3: out = kAbilities_3; count = 4; return true;
    case 0x4: out = kAbilities_4; count = 9; return true;
    case 0x5: out = kAbilities_5; count = 6; return true;
    case 0x6: out = kAbilities_6; count = 6; return true;
    case 0x7: out = kAbilities_7; count = 6; return true;
    case 0x8: out = kAbilities_8; count = 6; return true;
    case 0x9: out = kAbilities_9; count = 6; return true;
    case 0xA: out = kAbilities_A; count = 5; return true;
    case 0x15: out = kAbilities_15; count = 6; return true;
    case 0x16: out = kAbilities_16; count = 5; return true;
    case 0x20: out = kAbilities_20; count = 6; return true;
    case 0x29: out = kAbilities_29; count = 6; return true;
    case 0x40: out = kAbilities_40; count = 6; return true;
    case 0x42: out = kAbilities_42; count = 5; return true;
    case 0x65: out = kAbilities_65; count = 5; return true;
    case 0x68: out = kAbilities_68; count = 7; return true;
    case 0x6E: out = kAbilities_6E; count = 5; return true;
    case 0x79: out = kAbilities_79; count = 6; return true;
    case 0x7A: out = kAbilities_7A; count = 9; return true;
    case 0xDD: out = kAbilities_DD; count = 5; return true;
    case 0x12E: out = kAbilities_12E; count = 6; return true;
    case 0x12F: out = kAbilities_12F; count = 7; return true;
    case 0x13B: out = kAbilities_13B; count = 5; return true;
    case 0x13E: out = kAbilities_13E; count = 6; return true;
    case 0x195: out = kAbilities_195; count = 7; return true;
    case 0x1A2: out = kAbilities_1A2; count = 5; return true;
    case 0x1CA: out = kAbilities_1CA; count = 7; return true;
    case 0x1EC: out = kAbilities_1EC; count = 5; return true;
    case 0x200: out = kAbilities_200; count = 5; return true;
    case 0x206: out = kAbilities_206; count = 6; return true;
    case 0x221: out = kAbilities_221; count = 6; return true;
    case 0x231: out = kAbilities_231; count = 6; return true;
    case 0x236: out = kAbilities_236; count = 7; return true;
    case 0x23B: out = kAbilities_23B; count = 6; return true;
    case 0x28D: out = kAbilities_28D; count = 8; return true;
    case 0x291: out = kAbilities_291; count = 6; return true;
    case 0x30A: out = kAbilities_30A; count = 7; return true;
    case 0x31C: out = kAbilities_31C; count = 5; return true;
    case 0x32A: out = kAbilities_32A; count = 6; return true;
    case 0x32B: out = kAbilities_32B; count = 6; return true;
    case 0x362: out = kAbilities_362; count = 7; return true;
    case 0x365: out = kAbilities_365; count = 6; return true;
    case 0x3C3: out = kAbilities_3C3; count = 5; return true;
    case 0x472: out = kAbilities_472; count = 7; return true;
    case 0x4C4: out = kAbilities_4C4; count = 7; return true;
    case 0x4D8: out = kAbilities_4D8; count = 6; return true;
    case 0x4DD: out = kAbilities_4DD; count = 7; return true;
    case 0x4E3: out = kAbilities_4E3; count = 6; return true;
    case 0x516: out = kAbilities_516; count = 6; return true;
    default: return false;
    }
}

} // namespace gen
} // namespace ow_v2
