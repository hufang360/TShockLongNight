using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.GameContent.Events;
using TShockAPI;

namespace LongNight;

/// <summary>
/// 世界事件（天气 / 天象 / 派对…）。
/// <para>参考 TShockWorldModify 的 WMEventTool。</para>
/// <para>
/// 触发点在「黎明前」的 04:25~04:29，此时还要看永夜开关：
/// 永夜开着 → 黎明后会被拉回夜晚起点，所以只有<b>夜晚类</b>事件有意义；
/// 永夜关着 → 5 分钟后进入白天，只有<b>白天类</b>事件有意义。
/// 因此每条事件标了 <see cref="Entry.NightOnly"/> / <see cref="Entry.DayOnly"/>，
/// 随机时会按当前昼夜自动过滤。
/// </para>
/// </summary>
public static class WorldEvent
{
    /// <summary>一条世界事件。</summary>
    public sealed class Entry
    {
        /// <summary>关键字（指令 / 配置里用）</summary>
        public string Key = "";

        /// <summary>显示名（可带 [i:物品id] 图标）</summary>
        public string Name = "";

        /// <summary>只在「黎明后仍是夜晚」（永夜开启）时有意义</summary>
        public bool NightOnly;

        /// <summary>只在「黎明后进入白天」（永夜关闭）时有意义</summary>
        public bool DayOnly;

        /// <summary>读取当前是否生效</summary>
        public Func<bool> Get = () => false;

        /// <summary>设置开关</summary>
        public Action<bool> Set = _ => { };
    }

    static readonly Random _rnd = new();

    // 本次事件改动过的项：key -> 事件前的状态
    // 直接读写 state.json 里的那份（重启后仍能正确恢复，不会叠加）
    static Dictionary<string, bool> _touched => LongNightPlugin.RunState.TouchedWorld;

    static void SetStorm(bool on)
    {
        if (on)
        {
            Main.StartRain(instant: true, strengthOverride: 0.9f);
            Main.windSpeedTarget = 0.5f;
        }
        else
        {
            Main.StopRain(instant: true);
            Main.windSpeedTarget = 0f;
        }
    }

    static void SetSlimeRain(bool on)
    {
        Main.slimeRain = on;
        Main.slimeRainKillCount = 0;
        Main.slimeRainTime = on ? 54000.0 : 0.0;
    }

    /// <summary>全部可用的世界事件</summary>
    public static readonly List<Entry> All = new()
    {
        #region 通用（昼夜都能生效）

        new Entry
        {
            Key = "meteor", Name = "[i:117]陨石",
            Get = () => WorldGen.spawnMeteor,
            Set = on => WorldGen.spawnMeteor = on,
        },
        new Entry
        {
            Key = "party", Name = "[i:1000]派对",
            Get = () => BirthdayParty.ManualParty,
            Set = on => BirthdayParty.ManualParty = on,
        },
        new Entry
        {
            Key = "storm", Name = "[i:1244]暴风雨",
            Get = () => Main.IsItStorming,
            Set = SetStorm,
        },

        #endregion

        #region 夜晚类（需要永夜开启）

        new Entry
        {
            Key = "blood", Name = "[i:4271]血月", NightOnly = true,
            Get = () => Main.bloodMoon,
            Set = on => Main.bloodMoon = on,
        },
        new Entry
        {
            Key = "lantern", Name = "[i:344]灯笼夜", NightOnly = true,
            Get = () => LanternNight.LanternsUp,
            Set = on => LanternNight.ManualLanterns = on,
        },
        new Entry
        {
            Key = "starfall", Name = "[i:75]流星雨", NightOnly = true,
            Get = () => Star.starfallBoost > 3f,
            Set = on => Star.starfallBoost = on ? 5f : 1f,
        },

        #endregion

        #region 白天类（需要永夜关闭）

        new Entry
        {
            Key = "eclipse", Name = "[i:2767]日食", DayOnly = true,
            Get = () => Main.eclipse,
            Set = on => Main.eclipse = on,
        },
        new Entry
        {
            Key = "slime", Name = "[i:560]史莱姆雨", DayOnly = true,
            Get = () => Main.slimeRain,
            Set = SetSlimeRain,
        },
        new Entry
        {
            Key = "sand", Name = "[i:857]沙尘暴", DayOnly = true,
            Get = () => Sandstorm.Happening,
            Set = on =>
            {
                Sandstorm.Happening = on;
                Sandstorm.IntendedSeverity = on ? 1f : 0f;
            },
        },

        #endregion
    };

    /// <summary>按关键字查找（忽略大小写）</summary>
    public static Entry Find(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        return All.FirstOrDefault(e => string.Equals(e.Key, key.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>当前生效的世界事件（中文名），没有则返回空串</summary>
    public static string ActiveDesc
    {
        get
        {
            var on = All.Where(e => SafeGet(e)).Select(e => e.Name).ToList();
            return on.Count == 0 ? "" : string.Join(", ", on);
        }
    }

    /// <summary>本次事件改动过的项，用于「终章恢复」</summary>
    public static bool HasTouched => _touched.Count > 0;

    /// <summary>本次事件改动过的项数</summary>
    public static int TouchedCount => _touched.Count;

    static bool SafeGet(Entry e)
    {
        try { return e.Get(); }
        catch { return false; }
    }

    /// <summary>
    /// 该事件是否适合「接下来是夜晚 / 白天」。
    /// </summary>
    static bool Allow(Entry e, bool nightNext)
    {
        if (e.NightOnly)
            return nightNext;
        if (e.DayOnly)
            return !nightNext;
        return true;
    }

    /// <summary>
    /// 恢复本次事件改动过的所有世界事件。
    /// </summary>
    /// <param name="broadcast">是否全服播报</param>
    public static void RestoreAll(bool broadcast = true)
    {
        if (_touched.Count == 0)
            return;

        foreach (var kv in _touched.ToList())
        {
            var e = Find(kv.Key);
            if (e == null)
                continue;

            try { e.Set(kv.Value); }
            catch (Exception ex) { TShock.Log.Error($"恢复世界事件 [{kv.Key}] 失败：{ex.Message}"); }
        }

        _touched.Clear();
        LongNightPlugin.SaveState();
        TSPlayer.All.SendData(PacketTypes.WorldInfo);

        if (broadcast)
            TSPlayer.All.SendInfoMessage("世界事件 已恢复原状");
    }

    /// <summary>
    /// 随机开启一个世界事件。
    /// </summary>
    /// <returns>被开启的事件名；未开启时返回 null</returns>
    public static string Run()
    {
        var cfg = LongNightPlugin.Conf.DawnEvent.WorldEvent;
        if (!cfg.Enable)
            return null;

        // 永夜开着 → 黎明后会被拉回夜晚起点；关着 → 5 分钟后进入白天
        bool nightNext = LongNightPlugin.Conf.LongNight.Enable;

        // 解析随机池（跳过配置里写错的关键字，以及与当前昼夜不符的项）
        var pool = cfg.Pool.Select(Find).Where(e => e != null && Allow(e, nightNext)).ToList();
        if (pool.Count == 0)
        {
            TShock.Log.ConsoleError("『 黎明事件 』世界事件池为空或与当前昼夜不符，已跳过");
            return null;
        }

        // 先恢复上一次，避免事件叠加
        if (cfg.RestorePrevious)
            RestoreAll(broadcast: false);

        // 优先挑一个当前没开启的，最多试 5 次
        Entry pick = null;
        for (int i = 0; i < 5 && pick == null; i++)
        {
            var candidate = pool[_rnd.Next(pool.Count)];
            if (!SafeGet(candidate))
                pick = candidate;
        }
        // 池子里全都开着（或随机 5 次都撞车）就随便取一个
        pick ??= pool[_rnd.Next(pool.Count)];

        if (!_touched.ContainsKey(pick.Key))
        {
            _touched[pick.Key] = SafeGet(pick);
            LongNightPlugin.SaveState();
        }

        try
        {
            pick.Set(true);
        }
        catch (Exception ex)
        {
            TShock.Log.Error($"开启世界事件 [{pick.Key}] 失败：{ex.Message}");
            _touched.Remove(pick.Key);
            LongNightPlugin.SaveState();
            return null;
        }

        TSPlayer.All.SendData(PacketTypes.WorldInfo);
        return pick.Name;
    }
}
