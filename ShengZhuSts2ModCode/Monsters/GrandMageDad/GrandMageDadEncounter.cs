using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;

/// <summary>
/// 大法师老爹独立 Boss 遭遇：只注册模型，不加入任何正式楼层池。
/// </summary>
public class GrandMageDadEncounter : CustomEncounterModel
{
    private const string StateSaveKey = "GrandMageDadState";

    private GrandMageDadState _state = new();

    /// <summary>
    /// 构造 Boss 遭遇，并让 BaseLib 记录自定义 Encounter。
    /// </summary>
    public GrandMageDadEncounter() : base(RoomType.Boss)
    {
        MainFile.Logger.Info($"【大法师老爹】GrandMageDadEncounter 构造完成：遭遇ID={Id}，房间类型={RoomType}。");
    }

    /// <summary>
    /// 当前老爹战斗状态，供后续 UI、符咒封存和存档恢复使用。
    /// </summary>
    public GrandMageDadState State => _state;

    /// <summary>
    /// 本阶段不发放战斗奖励，避免开发入口污染正常跑团收益。
    /// </summary>
    public override bool ShouldGiveRewards => false;

    /// <summary>
    /// 老爹遭遇可能出现的怪物列表。
    /// </summary>
    public override IEnumerable<MonsterModel> AllPossibleMonsters
    {
        get
        {
            MainFile.Logger.Info("【大法师老爹】GM 或遭遇池正在读取 AllPossibleMonsters，准备通过 ModelDb.Monster<GrandMageDad>() 获取怪物。");
            MonsterModel monster = ModelDb.Monster<GrandMageDad>();
            MainFile.Logger.Info($"【大法师老爹】AllPossibleMonsters 读取成功：怪物ID={monster.Id}，怪物类型={monster.GetType().FullName}。");
            return new[] { monster };
        }
    }

    /// <summary>
    /// 本阶段不挂载到任何章节，只有外部开发入口显式创建时才会进入战斗。
    /// </summary>
    /// <param name="act">当前章节模型。</param>
    /// <returns>始终返回 false。</returns>
    public override bool IsValidForAct(ActModel act)
    {
        return false;
    }

    /// <summary>
    /// 生成老爹 Boss 怪物；槽位交给默认遭遇布局处理。
    /// </summary>
    /// <returns>怪物和槽位列表。</returns>
    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        MainFile.Logger.Info("【大法师老爹】开始 GenerateMonsters，准备生成老爹怪物。");
        MonsterModel monster = ModelDb.Monster<GrandMageDad>();
        MainFile.Logger.Info($"【大法师老爹】GenerateMonsters 获取怪物成功：怪物ID={monster.Id}，怪物类型={monster.GetType().FullName}。");
        return new List<(MonsterModel, string?)>
        {
            (monster.ToMutable(), null)
        };
    }

    /// <summary>
    /// 保存老爹战斗状态。
    /// </summary>
    /// <returns>自定义存档键值。</returns>
    public override Dictionary<string, string> SaveCustomState()
    {
        return new Dictionary<string, string>
        {
            [StateSaveKey] = _state.ToJson()
        };
    }

    /// <summary>
    /// 读取老爹战斗状态。
    /// </summary>
    /// <param name="state">存档键值。</param>
    public override void LoadCustomState(Dictionary<string, string> state)
    {
        _state = state.TryGetValue(StateSaveKey, out string? json)
            ? GrandMageDadState.FromJson(json)
            : new GrandMageDadState();
    }
}
