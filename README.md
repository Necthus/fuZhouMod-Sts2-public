# 圣主历险记：beta 兼容修复版

这是圣主历险记公开源码的 fork，保留原作者黑白大彩电的角色、卡牌、遗物、事件和资源，修复消耗卡牌卡死、击杀后溅射遗漏等问题，并适配《杀戮尖塔 2》`public-beta v0.111.0`。

当前修复版本：`1.0.10-beta.2`，验证依赖：BaseLib `3.4.7`。

[完整修复记录](BETA_FIX_NOTES.md)说明了根因、接口变更、23 项验证场景及测试范围。[变更日志](CHANGELOG.md)记录版本变化。

## 本次修复

- **消耗其他卡牌时卡死**：适配 beta 更改后的 `CardCmd.Exhaust` 返回类型，覆盖明塔、伊卡、摄魂、独木桥、先古拉苏等消耗调用。
- **猪符咒击杀后漏掉溅射**：出牌前记录相邻敌人，主目标死亡并被移除后仍可结算；重放和嵌套出牌分别记录目标。
- **电眼逼人和亥猪的群体效果**：使用新版伤害接口及本次出牌上下文，保留对剩余敌人的伤害和后续效果，跳过死亡目标。
- **面具十合一（无尽黑暗）**：直接用新版能力命令施加十种面具，验证普通版、重复施放和升级版的容量、叠层与兵团生成标记。
- **鼠符咒变化诅咒/状态牌卡死**：修正消耗接口，仅在消耗成功且仍属于同一场有效战斗时生成零费消耗攻击牌。
- **选人界面事件设置被绕过**：五种剧情事件的自然抽取和首个问号兜底共用本局配置，未勾选不出现，勾选仅在所选幕出现；界面用“第1/2/3幕”明确章节含义。
- **其他适配**：直接移除猪符咒目标格挡，适配伤害修正钩子、卯兔回合结束钩子、西瓦手镯/独木桥复制牌接口，以及生命之杯药水重放的异步结束流程。

验证结果：beta.2 构建 0 错误，完整 23 项检查、794 个游戏/依赖成员引用及 1007 个虚方法覆写全部通过。事件回归覆盖未勾选、幕数、自然抽取、兜底替换、存档恢复和混合角色跑团；此前使用者已反馈初版战斗修复可用。尚未逐项验证所有动画、选牌界面、其他 mod 组合和多人联机场景。

## 选择角色界面的事件规则

选择圣主时，可设置远古封印、恶魔小龙、刀龙黑气、西瓦的手镯、岁月史书这五种剧情事件。

- 幕数指第 1、2、3 幕，不是地图上的第几个房间。
- 未勾选的事件不会进入该圣主跑团的自然事件池，也不会被首个问号兜底选中。
- 勾选的事件仅在所选幕可出现，并继续要求满足原有条件，如远古封印要求有玩家尚未持有潘库宝盒。
- 所选幕首个实际进入的问号事件房，从符合条件的勾选事件中兜底出现一个；勾选多个并不保证全部出现。
- 五项全部取消时，关闭这五种剧情事件的自然出现和兜底替换；其它事件不受该列表影响。
- 开局后配置保存于符咒探测仪，读档继续使用该局配置；重新打开选人界面不会改变已有跑团。没有圣主的跑团保留这些事件原有的出现规则。

beta.1 及更早版本的勾选只控制兜底替换，自然事件可以绕过勾选和幕数。这也是未勾选岁月史书却在第 1 幕遇到它的原因。升级不会重写已经进入的事件房或撤销已获得的奖励。

## 构建

需要 .NET 9 SDK、已安装的游戏和 BaseLib。Godot .NET SDK 会由 NuGet 还原；构建 DLL 无需安装 Godot 编辑器。

从仓库根目录运行：

```powershell
.\Build-Beta.ps1
```

DLL 和 manifest 输出到仓库内 `dist/beta-build/ShengZhuSts2Mod/`。此脚本不会替换已安装的 mod。

如果游戏路径不同：

```powershell
.\Build-Beta.ps1 -Sts2Path 'D:\SteamLibrary\steamapps\common\Slay the Spire 2' -BaseLibDll 'D:\SteamLibrary\steamapps\workshop\content\2868840\3737335127\BaseLib\BaseLib.dll'
```

## 安装

已有圣主 mod 时，退出游戏后备份原文件，再使用构建出的 DLL 和 JSON。资源 PCK 可以继续使用已订阅的圣主 mod 资源。

`Install-BetaFix.ps1` 用于发行包：将脚本、构建出的 DLL 和 JSON 放在同一目录，然后运行脚本。它会优先替换工坊订阅版，备份原 DLL 和 JSON，保留自定义配置；没有订阅版时安装到游戏的本地 `mods/ShengZhuSts2Mod`。首次安装还需要同目录中的资源 PCK。Steam 工坊更新可能覆盖手动替换的文件。

仓库不包含发行包、游戏程序集或 BaseLib 程序集。完整资源包需要通过 Godot 导出，或复用已安装的原版圣主 PCK。

## 验证

以下命令从仓库根目录执行，先运行构建脚本。

```powershell
dotnet run --project tools/ShengZhuRegression

dotnet run --project tools/BetaAudit -- 'dist/beta-build/ShengZhuSts2Mod/ShengZhuSts2Mod.dll' 'C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64' 'C:\Program Files (x86)\Steam\steamapps\workshop\content\2868840\3737335127\BaseLib'
```

回归工具使用游戏的测试模式和真实战斗逻辑，隔离原生呈现与存档访问。它不打开游戏、不修改玩家存档；实际动画、选牌界面及多人网络仍需游戏内验证。

自定义路径可这样传递给回归工具：

```powershell
dotnet run --project tools/ShengZhuRegression -p:Sts2DataDir='D:\SteamLibrary\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64' -p:BaseLibDir='D:\SteamLibrary\steamapps\workshop\content\2868840\3737335127\BaseLib' -- 'D:\SteamLibrary\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64'
```
