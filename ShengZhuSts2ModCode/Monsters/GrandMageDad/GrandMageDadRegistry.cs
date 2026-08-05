namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;

/// <summary>
/// 大法师老爹注册入口：集中触发怪物和遭遇模型注册。
/// </summary>
public static class GrandMageDadRegistry
{
    private static GrandMageDadEncounter? _encounter;

    /// <summary>
    /// 确保大法师老爹相关模型被 ModelDb 和 BaseLib 注册。
    /// </summary>
    public static void Initialize()
    {
        bool isCreated = _encounter == null;
        _encounter ??= new GrandMageDadEncounter();
        MainFile.Logger.Info($"【大法师老爹】独立 Boss 遭遇注册检查完成：本次新建={isCreated}，遭遇类型={_encounter.GetType().FullName}，遭遇ID={_encounter.Id}，房间类型={_encounter.RoomType}，当前不会自动加入任何楼层。");
    }
}
