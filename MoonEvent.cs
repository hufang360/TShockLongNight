using System;
using Terraria;
using TShockAPI;

namespace LongNight;

/// <summary>
/// 月亮样式（<see cref="Main.moonType"/>）事件。
/// <para>参考 TShockWorldModify 的 MoonHelper.ChangeMoonStyle。</para>
/// </summary>
public static class MoonEvent
{
    /// <summary>9 种月亮样式，下标即 Main.moonType</summary>
    public static readonly string[] Names =
    {
        "正常",
        "火星样式",
        "土星样式",
        "秘银风格",
        "明亮的偏蓝白色",
        "绿色",
        "糖果",
        "金星样式",
        "紫色的三重月亮",
    };

    static readonly Random _rnd = new();

    /// <summary>当前月亮样式名</summary>
    public static string CurrentName => Names[Math.Clamp(Main.moonType, 0, Names.Length - 1)];

    /// <summary>样式数量</summary>
    public static int Count => Names.Length;

    /// <summary>
    /// 按顺序轮换到下一种月亮样式（0→1→…→8→0）。
    /// </summary>
    public static void Next()
    {
        Set((Main.moonType + 1) % Count);
    }

    /// <summary>
    /// 随机换一种月亮样式（与当前不同的概率更高，但不强制）。
    /// </summary>
    public static void RandomStyle()
    {
        Set(_rnd.Next(Count));
    }

    /// <summary>
    /// 指定月亮样式并同步给客户端。
    /// </summary>
    /// <param name="moonType">0~8</param>
    public static void Set(int moonType)
    {
        int target = Math.Clamp(moonType, 0, Count - 1);
        if (Main.moonType == target)
            return;

        // moonType 在 WorldInfo 包里下发给客户端，改完必须补发一次
        Main.moonType = target;
        TSPlayer.All.SendData(PacketTypes.WorldInfo);
    }

    /// <summary>
    /// 按配置执行月亮样式事件。
    /// </summary>
    /// <returns>新的月亮样式名；未更换时返回 null</returns>
    public static string Run()
    {
        var cfg = LongNightPlugin.Conf.DawnEvent.MoonStyle;
        if (!cfg.Enable)
            return null;

        switch ((cfg.Mode ?? "cycle").Trim().ToLowerInvariant())
        {
            case "off":
            case "none":
                return null;

            case "random":
                RandomStyle();
                break;

            case "cycle":
            default:
                Next();
                break;
        }

        return CurrentName;
    }
}
