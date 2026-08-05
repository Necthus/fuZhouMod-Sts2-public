using Godot;
using HarmonyLib;
using BaseLib.Patches.Localization;
using MegaCrit.Sts2.Core.Modding;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Character;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Keywords;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "ShengZhuSts2Mod"; //Used for resource filepath
    public const string ResPath = $"res://{ModId}";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        // 启用 BaseLib 简化本地化语法，统一支持高亮、升级切换和旧版变量占位写法。
        SimpleLoc.EnableSimpleLoc(ModId);

        // 初始化自定义关键字注册表，加载 card_keywords.json 中的关键字数据。
        KeywordRegistry.Initialize();

        // 注册大法师老爹 Boss 本体和独立遭遇；本阶段不挂载到任何正式楼层。
        GrandMageDadRegistry.Initialize();

        // 初始化十二符咒觉醒的玩家级存档字段，保证 SL 和联机存档能记住奖励是否已经发放。
        TalismanAwakeningHelper.InitializeSavedFields();

        // 将圣主角色本体注册为战斗监听者，用于处理不属于遗物的角色机制。
        ModHelper.SubscribeForCombatStateHooks($"{ModId}:ShengZhuCharacterHooks", combatState => combatState.Players.Select(player => player.Character).OfType<ShengZhu>());

        Harmony harmony = new(ModId);

        harmony.PatchAll();
    }
}
