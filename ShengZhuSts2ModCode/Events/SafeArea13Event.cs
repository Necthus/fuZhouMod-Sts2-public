using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Events;

/// <summary>
/// 最"安全"的十三区事件：按一代事件发放符咒、面具、最大生命或本体遗物奖励。
/// </summary>
public class SafeArea13Event : CustomEventModel
{
    /// <summary>
    /// 十三区事件使用 Mod 目录下的事件图，避免默认事件图路径指向游戏根目录。
    /// </summary>
    public override string? CustomInitialPortraitPath => $"{MainFile.ResPath}/images/events/shengzhusts2mod-safe_area_13.png";

    /// <summary>
    /// 事件全角色可见，保持一代注册规则。
    /// </summary>
    /// <param name="runState">当前爬塔状态。</param>
    /// <returns>始终返回 true。</returns>
    public override bool IsAllowed(IRunState runState)
    {
        return true;
    }

    /// <summary>
    /// 生成事件初始选项；符咒和面具按缺失状态控制，补给和仓库按一代实际逻辑始终可选。
    /// </summary>
    /// <returns>当前页事件选项。</returns>
    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        bool hasMissingTalisman = SafeArea13RewardHelper.HasMissingTalisman(Owner);
        bool hasMissingMask = SafeArea13RewardHelper.HasMissingMask(Owner);

        MainFile.Logger.Info($"【最安全的十三区】进入事件：缺失符咒={hasMissingTalisman}，缺失面具={hasMissingMask}");

        return
        [
            hasMissingTalisman
                ? new EventOption(this, SeizeTalisman, OptionKey("SEIZE_TALISMAN"))
                : new EventOption(this, null, OptionKey("SEIZE_TALISMAN_LOCKED")),
            hasMissingMask
                ? new EventOption(this, RaidMaskVault, OptionKey("RAID_MASK_VAULT"))
                : new EventOption(this, null, OptionKey("RAID_MASK_VAULT_LOCKED")),
            new EventOption(this, TakeEmergencySupply, OptionKey("EMERGENCY_SUPPLY")),
            new EventOption(this, LootVault, OptionKey("LOOT_VAULT")),
            new EventOption(this, Leave, OptionKey("LEAVE"))
        ];
    }

    /// <summary>
    /// 强抢符咒：获得一个未拥有符咒，并尝试获得一瓶本体随机药水。
    /// </summary>
    private async Task SeizeTalisman()
    {
        if (Owner == null)
        {
            SetEventFinished(L10NLookup($"{Id.Entry}.pages.NO_OWNER.description"));
            return;
        }

        await SafeArea13RewardHelper.GiveMissingTalismanAndPotion(Owner);
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.SEIZE_TALISMAN.description"));
    }

    /// <summary>
    /// 搬空面具库：获得一个未拥有面具，药水栏位加二，并尝试获得一瓶本体随机药水。
    /// </summary>
    private async Task RaidMaskVault()
    {
        if (Owner == null)
        {
            SetEventFinished(L10NLookup($"{Id.Entry}.pages.NO_OWNER.description"));
            return;
        }

        await SafeArea13RewardHelper.GiveMissingMaskExpandAndPotion(Owner);
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.RAID_MASK_VAULT.description"));
    }

    /// <summary>
    /// 应急补给：最大生命值增加 8。
    /// </summary>
    private async Task TakeEmergencySupply()
    {
        if (Owner == null)
        {
            SetEventFinished(L10NLookup($"{Id.Entry}.pages.NO_OWNER.description"));
            return;
        }

        await SafeArea13RewardHelper.GainMaxHp(Owner, 8);
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.EMERGENCY_SUPPLY.description"));
    }

    /// <summary>
    /// 洗劫仓库：获得两件本体非 Boss 遗物。
    /// </summary>
    private async Task LootVault()
    {
        if (Owner == null)
        {
            SetEventFinished(L10NLookup($"{Id.Entry}.pages.NO_OWNER.description"));
            return;
        }

        await SafeArea13RewardHelper.GiveBaseGameRelics(Owner, 2);
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.LOOT_VAULT.description"));
    }

    /// <summary>
    /// 不拿奖励，直接离开事件。
    /// </summary>
    private Task Leave()
    {
        MainFile.Logger.Info("【最安全的十三区】玩家选择直接离开。");
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.LEFT.description"));
        return Task.CompletedTask;
    }

    /// <summary>
    /// 生成当前事件选项文本键。
    /// </summary>
    /// <param name="optionName">选项名称。</param>
    /// <returns>完整本地化键。</returns>
    private string OptionKey(string optionName)
    {
        return $"{Id.Entry}.pages.INITIAL.options.{optionName}";
    }
}
