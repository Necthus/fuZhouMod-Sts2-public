using BaseLib.Abstracts;
using BaseLib.Utils.NodeFactories;
using ShengZhuSts2Mod.ShengZhuSts2ModCode;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Extensions;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Character;

// 圣主角色模型：先用石像静态模型与选人背景，保证首批迁移内容可见可用。
public class ShengZhu : PlaceholderCharacterModel
{
    public const string CharacterId = "ShengZhu";
    private const string DefaultVisualPath = $"{MainFile.ResPath}/scenes/shengzhu/shengzhu.tscn";
    private const string RestSiteVisualPath = $"{MainFile.ResPath}/scenes/shengzhu/shengzhu_rest_site.tscn";
    private const string MerchantVisualPath = $"{MainFile.ResPath}/scenes/shengzhu/shengzhu_merchant.tscn";
    private const string SkinVisualPath = "res://ShengZhuSkinMod/scenes/shengzhu/shengzhu.tscn";
    private const string SkinRestSiteVisualPath = "res://ShengZhuSkinMod/scenes/shengzhu/shengzhu_rest_site.tscn";
    private const string SkinMerchantVisualPath = "res://ShengZhuSkinMod/scenes/shengzhu/shengzhu_merchant.tscn";

    // 圣主主题色：取能量图标的金铜主色，统一角色名、卡框与小卡图标颜色。
    public static readonly Color Color = new("B8860B");

    public override Color NameColor => Color;
    public override CharacterGender Gender => CharacterGender.Neutral;
    public override int StartingHp => 80;
    /// <summary>
    /// 圣主角色本体需要接收战斗钩子，用于挂载非遗物来源的角色机制。
    /// </summary>
    public override bool ShouldReceiveCombatHooks => true;

    public override IEnumerable<CardModel> StartingDeck =>
    [
        ModelDb.Card<BlackHandAhFen>(),
        ModelDb.Card<BlackHandAhFen>(),
        ModelDb.Card<BlackHandAhFen>(),
        ModelDb.Card<BlackHandChow>(),
        ModelDb.Card<BlackHandChow>(),
        ModelDb.Card<BlackHandChow>(),
        ModelDb.Card<BlackHandRatso>(),
        ModelDb.Card<LaserEyes>(),
        ModelDb.Card<BlackHandRatso>(),
        ModelDb.Card<NiJiaMask>()
    ];

    public override IReadOnlyList<RelicModel> StartingRelics =>
    [
        ModelDb.Relic<TalismanLocator>(),
        ModelDb.Relic<ShadowKhanToken>()
    ];

    public override CardPoolModel CardPool => ModelDb.CardPool<ShengZhuSts2ModCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<ShengZhuSts2ModRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<ShengZhuSts2ModPotionPool>();

    // 自定义角色头像控件。
    public override Control CustomIcon
    {
        get
        {
            var icon = NodeFactory<Control>.CreateFromResource(CustomIconTexturePath);
            icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            return icon;
        }
    }

    public override string CustomIconTexturePath => "character_icon_shengzhu.png".CharacterUiPath();
    public override string CustomCharacterSelectIconPath => "char_select_shengzhu.png".CharacterUiPath();
    public override string CustomCharacterSelectLockedIconPath => "char_select_char_name_locked.png".CharacterUiPath();
    public override string CustomMapMarkerPath => "map_marker_char_name.png".CharacterUiPath();
    // 角色选择页背景场景。
    public override string CustomCharacterSelectBg => $"{MainFile.ResPath}/scenes/shengzhu/char_select_bg_shengzhu.tscn";
    // 角色图标场景。
    public override string CustomIconPath => $"{MainFile.ResPath}/scenes/shengzhu/shengzhu_icon.tscn";
    // 战斗角色模型场景：有皮肤资源时优先使用皮肤，没有则回退到圣主本体。
    public override string CustomVisualPath => ResourceLoader.Exists(SkinVisualPath) ? SkinVisualPath : DefaultVisualPath;
    // 休息处专用展示场景：固定使用圣主本体资源，避免皮肤 Mod 的普通 Node2D 场景被当作休息处角色节点加载。
    public override string CustomRestSiteAnimPath => RestSiteVisualPath;
    //public override string CustomRestSiteAnimPath => ResourceLoader.Exists(SkinRestSiteVisualPath) ? SkinRestSiteVisualPath : RestSiteVisualPath;
    // 商店专用展示场景：固定使用圣主本体资源，避免皮肤 Mod 的普通 Node2D 场景被当作商店角色节点加载。
    public override string CustomMerchantAnimPath => MerchantVisualPath;
    //public override string CustomMerchantAnimPath => ResourceLoader.Exists(SkinMerchantVisualPath) ? SkinMerchantVisualPath : MerchantVisualPath;

    /// <summary>
    /// 玩家首回合开始前，为没有鼠符咒的圣主挂载石像形态。
    /// 同时兜底检查无尽黑暗觉醒条件（正常情况下在获得面具牌时已触发）。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="side">即将开始回合的一方。</param>
    /// <param name="combatState">当前战斗状态。</param>
    /// <returns>异步任务。</returns>
    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Player || combatState.RoundNumber != 1)
        {
            return;
        }

        ShadowKhanHelper.ResetCombatStateForCombat(combatState);
        BlackHandCardHelper.ResetCombatStateForCombat(combatState);
        AttackCardPlayHelper.ResetCombatStateForCombat(combatState);
        await TalismanImbalanceService.ApplyForCombatStart(combatState);

        foreach (Player player in GetShengZhuPlayers(combatState))
        {
            // 兜底检查无尽黑暗觉醒条件（正常由 EndlessDarknessPatch 在获得面具牌时触发）
            EndlessDarknessHelper.CheckAndTriggerAwakening(player);

            await ShengZhuFormService.ApplyCombatStartForm(player);
        }
    }

    /// <summary>
    /// 任意卡牌打出后统一记录黑手帮/阿福出牌统计，供瓦龙和阿福成长读取。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="cardPlay">刚完成打出的卡牌信息。</param>
    /// <returns>异步任务。</returns>
    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner?.Character is not ShengZhu)
        {
            return Task.CompletedTask;
        }

        BlackHandCardHelper.RecordPlayedCard(cardPlay.Card);
        AttackCardPlayHelper.RecordPlayedCard(cardPlay.Card);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取当前由这个圣主角色模型控制的玩家。
    /// </summary>
    /// <param name="combatState">当前战斗状态。</param>
    /// <returns>圣主玩家列表。</returns>
    private IEnumerable<Player> GetShengZhuPlayers(ICombatState combatState)
    {
        return combatState.Players
            .Where(player => player.Character is ShengZhu)
            .OrderBy(player => player.NetId);
    }

}
