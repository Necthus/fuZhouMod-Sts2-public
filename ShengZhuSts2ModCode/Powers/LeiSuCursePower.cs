using BaseLib.Hooks;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

/// <summary>
/// 雷苏诅咒能力（减益）：施加在敌方身上。
/// 核心机制：当诅咒层数 >= 目标当前生命值时，立即斩杀目标（复用灾厄的死亡特效）。
/// 回合结束前也会进行一次斩杀检查。
/// 部分黑影兵团牌会根据诅咒层数触发额外效果。
/// </summary>
public class LeiSuCursePower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 诅咒在血条上的显示颜色，使用偏脏的黄褐色，与原生灾厄的紫色区分。
    /// </summary>
    private static readonly Color CurseHealthBarColor = new("B08A2A");

    /// <summary>
    /// 诅咒血条动效材质：黄褐色底色上叠加厚重蠕动斑块，表现被邪咒缠住的感觉。
    /// </summary>
    private static readonly ShaderMaterial CurseHealthBarMaterial = CreateCurseHealthBarMaterial();

    /// <summary>
    /// 诅咒属于减益能力。
    /// </summary>
    public override PowerType Type => PowerType.Debuff;

    /// <summary>
    /// 诅咒使用计数器堆叠方式。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 给 BaseLib 血条预测层提供诅咒标记段，让诅咒像灾厄一样按层数占用目标血条百分比。
    /// </summary>
    /// <param name="context">血条预测上下文。</param>
    /// <returns>从血条左侧开始绘制的诅咒标记段。</returns>
    public override IEnumerable<HealthBarForecastSegment> GetHealthBarForecastSegments(HealthBarForecastContext context)
    {
        if (Owner == null || context.Creature != Owner || Amount <= 0)
        {
            return [];
        }

        return [new HealthBarForecastSegment(Amount, CurseHealthBarColor, HealthBarForecastDirection.FromLeft, 0, CurseHealthBarMaterial, Colors.White)];
    }

    /// <summary>
    /// 创建诅咒血条专用 ShaderMaterial，使用 TIME 让暗色斑块和吸盘亮点持续缓慢游动。
    /// </summary>
    /// <returns>诅咒血条材质。</returns>
    private static ShaderMaterial CreateCurseHealthBarMaterial()
    {
        Shader shader = new()
        {
            Code = """
                shader_type canvas_item;

                uniform vec4 base_color : source_color = vec4(0.70, 0.53, 0.12, 1.0);
                uniform vec4 shadow_color : source_color = vec4(0.18, 0.08, 0.02, 1.0);
                uniform vec4 sucker_color : source_color = vec4(0.96, 0.78, 0.28, 1.0);
                uniform float speed = 0.65;

                float hash(vec2 p) {
                    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
                }

                float noise(vec2 p) {
                    vec2 i = floor(p);
                    vec2 f = fract(p);
                    vec2 u = f * f * (3.0 - 2.0 * f);
                    return mix(
                        mix(hash(i + vec2(0.0, 0.0)), hash(i + vec2(1.0, 0.0)), u.x),
                        mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x),
                        u.y
                    );
                }

                float soft_band(float y, float center, float width, float feather) {
                    return 1.0 - smoothstep(width, width + feather, abs(y - center));
                }

                float sucker_row(vec2 uv, float center, float crawl, float offset) {
                    float cell = fract(uv.x * 8.0 + offset + crawl * 0.18) - 0.5;
                    float distance_to_center = length(vec2(cell * 0.95, (uv.y - center) * 2.8));
                    float outer = 1.0 - smoothstep(0.16, 0.25, distance_to_center);
                    float inner = 1.0 - smoothstep(0.045, 0.11, distance_to_center);
                    return max(outer * 0.42 - inner * 0.22, 0.0);
                }

                void fragment() {
                    vec4 source = texture(TEXTURE, UV) * COLOR;
                    float crawl = TIME * speed;
                    float center_a = 0.54 + sin(UV.x * 7.5 - crawl * 2.4) * 0.20 + sin(UV.x * 18.0 + crawl) * 0.07;
                    float center_b = 0.34 + sin(UV.x * 10.5 + crawl * 2.0) * 0.16 + sin(UV.x * 24.0 - crawl * 1.3) * 0.05;
                    float body_a = soft_band(UV.y, center_a, 0.18, 0.08);
                    float body_b = soft_band(UV.y, center_b, 0.12, 0.07);
                    float body = clamp(max(body_a, body_b * 0.85), 0.0, 1.0);
                    float blotch = noise(vec2(UV.x * 9.0 - crawl * 1.7, UV.y * 4.0 + crawl * 0.6));
                    float veins = smoothstep(0.45, 0.86, body * (0.55 + blotch * 0.75));
                    float suckers = sucker_row(UV, center_a - 0.06, crawl, 0.1) + sucker_row(UV, center_b + 0.04, crawl, 0.55);
                    float edge_fade = smoothstep(0.0, 0.10, UV.y) * (1.0 - smoothstep(0.90, 1.0, UV.y));
                    float pulse = 0.90 + 0.10 * sin(crawl * 6.0 + UV.x * 9.0);

                    vec3 color = mix(base_color.rgb, shadow_color.rgb, clamp(veins * 0.78 * edge_fade, 0.0, 0.86));
                    color = mix(color, sucker_color.rgb, clamp(suckers * body * edge_fade, 0.0, 0.55));
                    color += base_color.rgb * noise(vec2(UV.x * 18.0 + crawl, UV.y * 8.0)) * 0.08;

                    COLOR = vec4(color * pulse, source.a);
                }
                """
        };

        return new ShaderMaterial { Shader = shader };
    }

    /// <summary>
    /// 诅咒层数变化后，立即检查是否满足斩杀条件。
    /// 当层数 >= 目标当前生命值时，使用灾厄特效斩杀目标。
    /// </summary>
    /// <param name="power">发生变化的能力。</param>
    /// <param name="amount">变化量。</param>
    /// <param name="applier">施加者。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <returns>异步任务。</returns>
    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        // 只关心自身层数变化
        if (!ReferenceEquals(power, this))
        {
            return;
        }

        await TryInstantKill();
    }

    /// <summary>
    /// 回合结束前检查斩杀条件（与灾厄相同时机：BeforeTurnEnd）。
    /// 确保即使施加时未触发，回合结束时也能正确斩杀。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="side">当前结束回合的阵营。</param>
    /// <returns>异步任务。</returns>
    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        if (side != Owner?.Side)
        {
            return;
        }

        await TryInstantKill();
    }

    /// <summary>
    /// 尝试斩杀：若诅咒层数 >= 目标当前生命值，则使用灾厄的死亡特效击杀目标。
    /// </summary>
    private async Task TryInstantKill()
    {
        if (Owner == null || Owner.IsDead || !Owner.IsAlive)
        {
            return;
        }

        int currentHp = Owner.CurrentHp;
        if (currentHp <= 0)
        {
            return;
        }

        if (Amount >= currentHp)
        {
            Flash();
            MainFile.Logger.Info($"【雷苏诅咒】斩杀触发：目标当前HP={currentHp}，诅咒层数={Amount}，使用灾厄特效执行斩杀");
            await ResolveBountyBeforeKill();
            // 复用灾厄（Doom）的死亡特效和击杀逻辑
            await DoomPower.DoomKill(new[] { Owner });
        }
    }

    /// <summary>
    /// 雷苏诅咒走灾厄斩杀流程，主动补一次悬赏结算，避免死亡清理时黑手先消失导致漏金币。
    /// </summary>
    private async Task ResolveBountyBeforeKill()
    {
        if (Owner?.Player != null)
        {
            return;
        }

        var combatState = Owner?.CombatState;
        if (combatState == null)
        {
            return;
        }

        foreach (Creature creature in combatState.Creatures)
        {
            var player = creature.Player;
            if (player?.Creature == null)
            {
                continue;
            }

            await BountyPower.ResolveRewardForTarget(new ThrowingPlayerChoiceContext(), player, Owner, true);
        }
    }
}
