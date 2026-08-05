using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Networking;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

/// <summary>
/// 黑影令牌：圣主专用遗物，用于切换黑影兵团烧牌挡位。
/// 计数器为0时自动随机烧牌，计数器为1时手动选择烧牌，计数器为2时伊卡不烧牌。
/// </summary>
public class ShadowKhanToken : ShengZhuSts2ModRelic, IRelicRightClickable
{
    /// <summary>
    /// 黑影兵团烧牌挡位。
    /// 通过存档保存，保证本次游玩SL后仍读取玩家设置的挡位。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public int ExhaustMode
    {
        get => _exhaustMode;
        set => _exhaustMode = Math.Clamp(value, 0, 2);
    }

    private int _exhaustMode = 1;

    /// <summary>
    /// 遗物稀有度：无稀有度，只能通过圣主初始遗物直接发放。
    /// </summary>
    public override RelicRarity Rarity => RelicRarity.None;

    /// <summary>
    /// 显示当前挡位计数器。
    /// </summary>
    public override bool ShowCounter => true;

    /// <summary>
    /// 计数器显示值：0=自动随机，1=手动选牌，2=明塔自动烧牌且伊卡不烧牌。
    /// </summary>
    public override int DisplayAmount => _exhaustMode;

    /// <summary>
    /// 右键点击：切换黑影兵团烧牌挡位。
    /// </summary>
    /// <param name="context">玩家选择上下文。</param>
    /// <returns>异步任务。</returns>
    public Task OnRightClick(PlayerChoiceContext context)
    {
        return RelicInteractionSyncService.RequestShadowKhanTokenMode(this, (_exhaustMode + 1) % 3);
    }

    /// <summary>
    /// 执行同步后的黑影令牌烧牌挡位设置。
    /// </summary>
    /// <param name="exhaustMode">目标烧牌挡位。</param>
    internal void RunSyncedSetExhaustMode(int exhaustMode)
    {
        _exhaustMode = Math.Clamp(exhaustMode, 0, 2);
        InvokeDisplayAmountChanged();
        Flash();
        MainFile.Logger.Info($"【黑影令牌】切换黑影兵团烧牌挡位：{GetModeName(_exhaustMode)}");
    }

    /// <summary>
    /// 判断指定玩家当前是否使用手动烧牌挡位。
    /// 未持有黑影令牌时默认返回true，避免影响异常来源的黑影兵团牌。
    /// </summary>
    /// <param name="player">要检查的玩家。</param>
    /// <returns>true表示手动选牌，false表示自动随机。</returns>
    public static bool IsManualExhaustMode(Player? player)
    {
        return GetExhaustMode(player) == 1;
    }

    /// <summary>
    /// 判断指定玩家当前是否使用伊卡白板挡位。
    /// 未持有黑影令牌时默认返回false，避免影响异常来源的黑影兵团牌。
    /// </summary>
    /// <param name="player">要检查的玩家。</param>
    /// <returns>true表示伊卡只造成基础伤害，不烧牌。</returns>
    public static bool IsYiKaBlankMode(Player? player)
    {
        return GetExhaustMode(player) == 2;
    }

    /// <summary>
    /// 读取指定玩家当前黑影兵团烧牌挡位。
    /// 未持有黑影令牌时默认返回手动挡，避免影响异常来源的黑影兵团牌。
    /// </summary>
    /// <param name="player">要检查的玩家。</param>
    /// <returns>0=自动随机，1=手动选牌，2=明塔自动烧牌且伊卡不烧牌。</returns>
    private static int GetExhaustMode(Player? player)
    {
        if (player == null)
        {
            return 1;
        }

        ShadowKhanToken? token = player.Relics.OfType<ShadowKhanToken>().FirstOrDefault();
        return token?.ExhaustMode ?? 1;
    }

    /// <summary>
    /// 获取挡位日志名称。
    /// </summary>
    /// <param name="mode">黑影兵团烧牌挡位。</param>
    /// <returns>用于日志显示的挡位名称。</returns>
    private static string GetModeName(int mode)
    {
        return mode switch
        {
            0 => "0-自动随机",
            1 => "1-手动选牌",
            2 => "2-明塔自动烧牌，伊卡白板打出",
            _ => "1-手动选牌"
        };
    }
}
