using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace LongNight;

[ApiVersion(2, 1)]
public class LongNightPlugin : TerrariaPlugin
{
    #region Plugin Info
    public override string Author => "hufang360";
    public override string Description => "永夜控制 / 满月 / 黎明事件";
    public override string Name => "LongNight";
    public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;
    #endregion

    // 配置 / 运行状态
    internal static Config Conf = new();
    internal static State RunState = new();

    // 启动后延迟 ~2 秒再播报一次
    private bool isFirstUpdate = false;
    private int firstUpdateDelay = 0;

    // 本轮是否已在入夜时播报过
    private bool isNoticed = false;

    // 本夜是否已触发过黎明事件（永夜关闭时 Main.time 不会归零，必须靠它去重）
    private bool dawnEventDone = false;

    public LongNightPlugin(Main game) : base(game)
    {
    }

    /// <summary>写回运行状态（进度 + 事件改动记录）</summary>
    internal static void SaveState() => State.Save(RunState);

    #region Initialize / Dispose
    public override void Initialize()
    {
        Conf = Config.Load();
        RunState = State.Load();

        Commands.ChatCommands.Add(new Command("longnight", LongNightCommand, "longnight", "ln")
        {
            HelpText = "永夜控制 / 满月 / 黎明事件"
        });

        ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ServerApi.Hooks.GameUpdate.Deregister(this, OnUpdate);
        }
        base.Dispose(disposing);
    }
    #endregion

    #region 主循环
    private void OnUpdate(EventArgs args)
    {
        if (!isFirstUpdate)
        {
            isFirstUpdate = true;
            firstUpdateDelay = 120;
            Console.WriteLine("永夜模式：{0},  夜晚总数：{1}天", Conf.LongNight.Enable ? "已开启" : "已关闭", Conf.LongNight.NightTotal);

            // 上次运行没来得及恢复（崩溃 / 强杀），等世界加载完后补一次
            RestorePendingOnStartup();
        }

        if (!Conf.Enabled)
            return;

        // 白天：复位标记，保证下一个夜晚能重新播报 / 触发事件
        if (Main.dayTime)
        {
            isNoticed = false;
            dawnEventDone = true;
            return;
        }

        // 始终为满月
        if (Conf.AlwaysFullMoon && Main.moonPhase != 0)
        {
            Main.moonPhase = 0;
            TSPlayer.All.SendData(PacketTypes.WorldInfo);
        }

        // 启动后延迟播报
        if (isFirstUpdate && firstUpdateDelay > 0)
        {
            firstUpdateDelay--;
            if (firstUpdateDelay == 0)
            {
                if (Conf.LongNight.Enable)
                {
                    // 标记为本夜已播报，避免紧接着的入夜窗口重复播报
                    isNoticed = true;
                    TSPlayer.All.SendInfoMessage("『 永夜 · 其{0} 』", GetZhNum(RunState.NightCurrent));
                }
                return;
            }
        }

        // 入夜窗口 19:31~19:35
        if (Main.time >= 60 && Main.time <= 300 && !isNoticed)
        {
            isNoticed = true;
            dawnEventDone = false;
            firstUpdateDelay = 0;

            if (Conf.LongNight.Enable)
                TSPlayer.All.SendInfoMessage("『 永夜 · 其{0} 』", GetZhNum(RunState.NightCurrent));
            return;
        }

        // 黎明窗口 04:25~04:29
        if (Main.time >= 32100 && Main.time <= 32340)
        {
            // 事件（每个夜晚只触发一次）
            if (!dawnEventDone)
            {
                dawnEventDone = true;
                RunDawnEvent();
            }

            // 永夜关闭时，不循环夜晚，交给原版进入白天
            if (!Conf.LongNight.Enable)
                return;

            if (RunState.NightCurrent < Conf.LongNight.NightTotal - 1)
            {
                // 推进到下一个夜晚
                RunState.NightCurrent++;

                // 换渔夫任务
                Main.AnglerQuestSwap();

                // 回到夜晚起点（月相固定满月，不再轮换）
                TSPlayer.Server.SetTime(false, 0.0);

                isNoticed = true;      // 新夜晚不再重复入夜播报
                dawnEventDone = false; // 新夜晚允许再次触发黎明事件

                TSPlayer.All.SendInfoMessage("『 永夜 · 其{0} 』", GetZhNum(RunState.NightCurrent));
                State.Save(RunState);
                Console.WriteLine("永夜模式：{0},  夜晚总数：{1}天", Conf.LongNight.Enable ? "已开启" : "已关闭", Conf.LongNight.NightTotal);
            }
            else
            {
                // 终章
                RunState.NightCurrent = 0;
                State.Save(RunState);

                TSPlayer.Server.SetTime(true, 0);
                TSPlayer.All.SendInfoMessage("『 永夜 · 终章 』");

                if (Conf.RestoreOnFinish)
                {
                    SecretSeedEvent.RestoreAll();
                    WorldEvent.RestoreAll();
                }
            }
        }
    }

    /// <summary>
    /// 启动时补恢复：上次运行中开启、却因为崩溃 / 强杀没能恢复的彩蛋特性和世界事件。
    /// <para>放在第一个 GameUpdate 里做，确保世界（.wld）已经加载完毕。</para>
    /// </summary>
    static void RestorePendingOnStartup()
    {
        if (!Conf.RestoreOnStartup)
            return;

        int seeds = SecretSeedEvent.TouchedCount;
        int world = WorldEvent.TouchedCount;
        if (seeds == 0 && world == 0)
            return;

        TShock.Log.ConsoleError($"[LongNight] 检测到上次运行未恢复的事件（彩蛋 {seeds} 项 / 世界事件 {world} 项），正在恢复原状");

        if (seeds > 0)
            SecretSeedEvent.RestoreAll(broadcast: false);
        if (world > 0)
            WorldEvent.RestoreAll(broadcast: false);
    }

    /// <summary>黎明事件调度</summary>
    static void RunDawnEvent()
    {
        var cfg = Conf.DawnEvent;
        if (!cfg.Enable)
            return;

        string moon = MoonEvent.Run();
        string seed = SecretSeedEvent.Run();
        string world = WorldEvent.Run();

        // 并成一行播报，例如：今天是 紫色的三重月亮, 吸血鬼, 血月
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(moon))
            parts.Add(moon);
        if (!string.IsNullOrEmpty(seed))
            parts.Add(seed);
        if (!string.IsNullOrEmpty(world))
            parts.Add(world);

        if (parts.Count == 0)
            return;

        TSPlayer.All.SendInfoMessage("今天是 {0}", string.Join(", ", parts));
    }
    #endregion

    #region 指令
    private void LongNightCommand(CommandArgs args)
    {
        TSPlayer op = args.Player;

        if (args.Parameters.Count == 0)
        {
            ShowHelp(op);
            return;
        }

        switch (args.Parameters[0].ToLowerInvariant())
        {
            case "help":
            case "h":
                ShowHelp(op);
                break;

            #region 永夜开关

            case "true":
            case "on":
            case "0":
                Conf.LongNight.Enable = true;
                Config.Save(Conf);
                op.SendSuccessMessage("永夜模式 已开启");
                break;

            case "false":
            case "off":
            case "1":
                Conf.LongNight.Enable = false;
                Config.Save(Conf);
                op.SendInfoMessage("永夜模式 已关闭（昼夜恢复自然长度，黎明事件照常触发）");
                break;

            #endregion

            #region 信息

            case "info":
            case "i":
                Info(op);
                break;

            #endregion

            #region 夜数

            case "total":
            case "t":
                SetTotal(op, args);
                break;

            case "current":
            case "cur":
            case "c":
                SetCurrent(op, args);
                break;

            #endregion

            #region 月亮样式

            case "style":
            case "s":
                StyleCommand(op, args);
                break;

            #endregion

            #region 彩蛋特性

            case "seed":
            case "se":
                SeedCommand(op, args);
                break;

            case "world":
            case "w":
                WorldCommand(op, args);
                break;

            case "restore":
                if (SecretSeedEvent.HasTouched || WorldEvent.HasTouched)
                {
                    SecretSeedEvent.RestoreAll();
                    WorldEvent.RestoreAll();
                }
                else
                {
                    op.SendInfoMessage("当前没有需要恢复的彩蛋特性 / 世界事件");
                }
                break;

            #endregion

            #region 其他开关

            case "event":
            case "e":
                ToggleEvent(op, args);
                break;

            case "moon":
            case "m":
                ToggleFullMoon(op, args);
                break;

            case "reload":
                Conf = Config.Load();
                RunState = State.Load();
                op.SendSuccessMessage("配置已重新加载");
                break;

            #endregion

            default:
                op.SendErrorMessage("语法错误！，请输入 /ln help 获取帮助");
                break;
        }
    }

    static void ShowHelp(TSPlayer op)
    {
        List<string> lines = new()
        {
            "/ln info，查看 永夜 / 满月 / 事件 状态",
            "/ln true，开启 永夜",
            "/ln false，关闭 永夜",
            "/ln total <number>，设置 永夜循环天数",
            "/ln current <number>，设置 当前处于永夜的第几天",
            "/ln event <on/off>，查看/开关 黎明事件",
            "/ln moon <on/off>，查看/开关 始终满月",
            "/ln style [1-9]，查看/设置 月亮样式",
            "/ln seed [random/关键字]，查看/随机/切换 彩蛋特性",
            "/ln world [random/关键字]，查看/随机/切换 世界事件",
            "/ln restore，恢复 黎明事件开启的彩蛋特性 / 世界事件",
            "/ln reload，重新加载 配置文件",
        };

        op.SendInfoMessage(string.Join("\n", lines));
    }

    static void Info(TSPlayer op)
    {
        op.SendInfoMessage("永夜");
        op.SendInfoMessage("  模式：{0}", Conf.LongNight.Enable ? "已开启" : "已关闭");
        op.SendInfoMessage("  天数：{0}/{1}（{2}）", RunState.NightCurrent + 1, Conf.LongNight.NightTotal, GetZhNum(RunState.NightCurrent));
        op.SendInfoMessage("  满月：{0}", Conf.AlwaysFullMoon ? "已开启" : "已关闭");

        string moon = GetMoon(Main.moonPhase);
        string itemId = Main.anglerQuestItemNetIDs[Main.anglerQuest].ToString();
        List<Item> matchedItems = TShock.Utils.GetItemByIdOrName(itemId);
        if (matchedItems.Count > 0)
            moon += ", 渔夫任务：" + matchedItems[0].Name;
        op.SendInfoMessage("  月相：{0}, 时间：{1}", moon, Main.time.ToString("F0"));

        op.SendInfoMessage("黎明事件");
        op.SendInfoMessage("  事件：{0}，月亮样式：{1}（{2}）",
            Conf.DawnEvent.Enable ? "已开启" : "已关闭",
            MoonEvent.CurrentName,
            Conf.DawnEvent.MoonStyle.Enable ? Conf.DawnEvent.MoonStyle.Mode : "已关闭");

        string active = SecretSeedEvent.ActiveDesc;
        op.SendInfoMessage("  彩蛋：{0}，当前生效：{1}",
            Conf.DawnEvent.SecretSeed.Enable ? "已开启" : "已关闭",
            string.IsNullOrEmpty(active) ? "无" : active);
        op.SendInfoMessage("  随机池：{0}", string.Join(", ", Conf.DawnEvent.SecretSeed.Pool));

        string activeWorld = WorldEvent.ActiveDesc;
        op.SendInfoMessage("  世界事件：{0}，当前生效：{1}",
            Conf.DawnEvent.WorldEvent.Enable ? "已开启" : "已关闭",
            string.IsNullOrEmpty(activeWorld) ? "无" : activeWorld);
        op.SendInfoMessage("  随机池：{0}", string.Join(", ", Conf.DawnEvent.WorldEvent.Pool));
    }

    static void SetTotal(TSPlayer op, CommandArgs args)
    {
        if (args.Parameters.Count < 2)
        {
            op.SendErrorMessage("请输入天数");
            return;
        }

        if (!int.TryParse(args.Parameters[1], out int days))
        {
            op.SendErrorMessage("请输入正确的天数");
            return;
        }

        if (days < 2)
        {
            op.SendErrorMessage("总天数不应小于2天");
            return;
        }

        Conf.LongNight.NightTotal = days;
        Config.Save(Conf);
        op.SendSuccessMessage("永夜总天数 已改为 {0} 天", days);
    }

    static void SetCurrent(TSPlayer op, CommandArgs args)
    {
        if (args.Parameters.Count < 2)
        {
            op.SendErrorMessage("请输入天数");
            return;
        }

        if (!int.TryParse(args.Parameters[1], out int days))
        {
            op.SendErrorMessage("请输入正确的天数");
            return;
        }

        if (days <= 0)
        {
            op.SendErrorMessage("当前天数不能为 0");
            return;
        }

        if (days > Conf.LongNight.NightTotal)
        {
            op.SendErrorMessage("不能超过永夜总天数{0}", Conf.LongNight.NightTotal);
            return;
        }

        RunState.NightCurrent = days - 1;
        State.Save(RunState);
        op.SendSuccessMessage("已改为永夜第 {0} 天", days);
    }

    static void StyleCommand(TSPlayer op, CommandArgs args)
    {
        if (args.Parameters.Count < 2)
        {
            op.SendInfoMessage("当前月亮样式：{0}", MoonEvent.CurrentName);
            op.SendInfoMessage("用法：/ln style <1~9>");
            for (int i = 0; i < MoonEvent.Names.Length; i++)
                op.SendInfoMessage("  {0}. {1}", i + 1, MoonEvent.Names[i]);
            return;
        }

        if (!int.TryParse(args.Parameters[1], out int style) || style < 1 || style > MoonEvent.Count)
        {
            op.SendErrorMessage("请输入 1~{0} 之间的数字", MoonEvent.Count);
            return;
        }

        MoonEvent.Set(style - 1);
        op.SendSuccessMessage("月亮样式已改为 {0}", MoonEvent.Names[style - 1]);
    }

    static void SeedCommand(TSPlayer op, CommandArgs args)
    {
        if (args.Parameters.Count < 2)
        {
            string active = SecretSeedEvent.ActiveDesc;
            op.SendInfoMessage("当前生效的彩蛋特性：{0}", string.IsNullOrEmpty(active) ? "无" : active);
            op.SendInfoMessage("用法：/ln seed random，随机开启一个");
            op.SendInfoMessage("用法：/ln seed <关键字>，切换指定特性");

            var safe = SecretSeedEvent.All.Where(e => e.RuntimeSafe).Select(e => $"{e.Key}={e.Name}");
            op.SendInfoMessage("运行时有效：{0}", string.Join(", ", safe));
            return;
        }

        if (args.Parameters[1].ToLowerInvariant() == "random")
        {
            string picked = SecretSeedEvent.Run();
            if (string.IsNullOrEmpty(picked))
                op.SendErrorMessage("没有可用的彩蛋特性，请检查配置里的 Pool");
            else
                op.SendSuccessMessage("彩蛋特性 → {0}", picked);
            return;
        }

        var entry = SecretSeedEvent.Find(args.Parameters[1]);
        if (entry == null)
        {
            op.SendErrorMessage("未知的彩蛋关键字，输入 /ln seed 查看可用列表");
            return;
        }

        bool on = !entry.Get();
        entry.Set(on);
        TSPlayer.All.SendData(PacketTypes.WorldInfo);
        op.SendSuccessMessage("{0} {1}", on ? "已开启" : "已关闭", entry.Name);
    }

    static void WorldCommand(TSPlayer op, CommandArgs args)
    {
        if (args.Parameters.Count < 2)
        {
            string active = WorldEvent.ActiveDesc;
            op.SendInfoMessage("当前生效的世界事件：{0}", string.IsNullOrEmpty(active) ? "无" : active);
            op.SendInfoMessage("用法：/ln world random，随机开启一个");
            op.SendInfoMessage("用法：/ln world <关键字>，切换指定事件");

            bool nightNext = Conf.LongNight.Enable;
            var ok = WorldEvent.All.Where(e => !e.NightOnly || nightNext)
                                   .Where(e => !e.DayOnly || !nightNext)
                                   .Select(e => $"{e.Key}={e.Name}");
            op.SendInfoMessage("当前可用（{0}）：{1}", nightNext ? "永夜开，偏夜晚" : "永夜关，偏白天", string.Join(", ", ok));
            return;
        }

        if (args.Parameters[1].ToLowerInvariant() == "random")
        {
            string picked = WorldEvent.Run();
            if (string.IsNullOrEmpty(picked))
                op.SendErrorMessage("没有可用的世界事件，请检查配置里的 Pool");
            else
                op.SendSuccessMessage("世界事件 → {0}", picked);
            return;
        }

        var entry = WorldEvent.Find(args.Parameters[1]);
        if (entry == null)
        {
            op.SendErrorMessage("未知的世界事件关键字，输入 /ln world 查看可用列表");
            return;
        }

        bool on = !entry.Get();
        entry.Set(on);
        TSPlayer.All.SendData(PacketTypes.WorldInfo);
        op.SendSuccessMessage("{0} {1}", on ? "已开启" : "已关闭", entry.Name);
    }

    static void ToggleEvent(TSPlayer op, CommandArgs args)
    {
        if (args.Parameters.Count < 2)
        {
            op.SendInfoMessage("黎明事件：{0}（/ln event on|off 开关）", Conf.DawnEvent.Enable ? "已开启" : "已关闭");
            return;
        }

        switch (args.Parameters[1].ToLowerInvariant())
        {
            case "on":
            case "true":
            case "1":
                Conf.DawnEvent.Enable = true;
                Config.Save(Conf);
                op.SendSuccessMessage("黎明事件 已开启");
                break;

            case "off":
            case "false":
            case "0":
                Conf.DawnEvent.Enable = false;
                Config.Save(Conf);
                op.SendInfoMessage("黎明事件 已关闭");
                break;

            default:
                op.SendErrorMessage("用法：/ln event on|off");
                break;
        }
    }

    static void ToggleFullMoon(TSPlayer op, CommandArgs args)
    {
        if (args.Parameters.Count < 2)
        {
            op.SendInfoMessage("始终满月：{0}（/ln moon on|off 开关）", Conf.AlwaysFullMoon ? "已开启" : "已关闭");
            return;
        }

        switch (args.Parameters[1].ToLowerInvariant())
        {
            case "on":
            case "true":
            case "1":
                Conf.AlwaysFullMoon = true;
                Config.Save(Conf);
                op.SendSuccessMessage("始终满月 已开启");
                break;

            case "off":
            case "false":
            case "0":
                Conf.AlwaysFullMoon = false;
                Config.Save(Conf);
                op.SendInfoMessage("始终满月 已关闭（恢复原版月相）");
                break;

            default:
                op.SendErrorMessage("用法：/ln moon on|off");
                break;
        }
    }
    #endregion

    #region 工具
    static string GetZhNum(int index)
    {
        string[] arr = { "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" };
        if (index < 0 || index >= arr.Length)
            return (index + 1).ToString();

        return arr[index];
    }

    static string GetMoon(int index)
    {
        string[] arr = { "满月", "亏凸月", "下弦月", "残月", "新月", "娥眉月", "上弦月", "盈凸月" };
        if (index < 0 || index >= arr.Length)
            return "未知";

        return arr[index];
    }
    #endregion
}
