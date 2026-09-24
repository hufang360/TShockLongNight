using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using TShockAPI;

namespace LongNight;

/// <summary>
/// 彩蛋特性（秘密世界 / SecretSeed）事件。
/// <para>参考 TShockWorldModify 的 WMSecretSeedTool。</para>
/// <para>
/// 注意：<c>WorldGen.SecretSeed.Enable()</c> 只改内部标志，<b>不会</b>自动同步到 <c>Main.*</c>；
/// 原版是在 <c>WorldGen.SecretSeed.InitializeSecretSeeds()</c>（世界加载期）做的映射。
/// 所以这里每一项都要自己把 <c>Main.*</c> 一起改掉，否则运行时看不到任何效果。
/// </para>
/// </summary>
public static class SecretSeedEvent
{
    /// <summary>一条彩蛋特性。</summary>
    public sealed class Entry
    {
        /// <summary>关键字（指令 / 配置里用）</summary>
        public string Key = "";

        /// <summary>中文名</summary>
        public string Name = "";

        /// <summary>运行时是否真的有表现</summary>
        public bool RuntimeSafe;

        /// <summary>读取当前是否生效</summary>
        public Func<bool> Get = () => false;

        /// <summary>设置开关</summary>
        public Action<bool> Set = _ => { };
    }

    static readonly Random _rnd = new();

    // 本次事件改动过的项：key -> 事件前的状态
    // 直接读写 state.json 里的那份（重启后仍能正确恢复，不会叠加）
    static Dictionary<string, bool> _touched => LongNightPlugin.RunState.TouchedSeeds;

    static void Seed(WorldGen.SecretSeed seed, bool on)
    {
        if (on)
            WorldGen.SecretSeed.Enable(seed, playSound: false);
        else
            WorldGen.SecretSeed.Disable(seed);
    }

    /// <summary>全部可用的彩蛋特性</summary>
    public static readonly List<Entry> All = new()
    {
        #region 运行时有效（默认随机池）

        new Entry
        {
            Key = "eh", Name = "[i:1844]无尽万圣", RuntimeSafe = true,
            Get = () => Main.forceHalloweenForever,
            Set = on =>
            {
                Main.forceHalloweenForever = on;
                Seed(WorldGen.SecretSeed.endlessHalloween, on);
            },
        },
        new Entry
        {
            Key = "ec", Name = "[i:588]无尽圣诞", RuntimeSafe = true,
            Get = () => Main.forceXMasForever,
            Set = on =>
            {
                Main.forceXMasForever = on;
                Seed(WorldGen.SecretSeed.endlessChristmas, on);
            },
        },
        new Entry
        {
            Key = "ml", Name = "[i:6155]更多闪电", RuntimeSafe = true,
            Get = () => Main.moreLightningSeed,
            Set = on =>
            {
                Main.moreLightningSeed = on;
                Seed(WorldGen.SecretSeed.moreLightning, on);
            },
        },
        new Entry
        {
            Key = "nl", Name = "无闪电", RuntimeSafe = true,
            Get = () => Main.noLightningSeed,
            Set = on =>
            {
                Main.noLightningSeed = on;
                Seed(WorldGen.SecretSeed.noLightning, on);
            },
        },
        new Entry
        {
            Key = "gl", Name = "[i:3297]绿色闪电", RuntimeSafe = true,
            Get = () => Main.moreLightningSeed && Main.noLightningSeed,
            Set = on =>
            {
                Main.moreLightningSeed = on;
                Main.noLightningSeed = on;
                Seed(WorldGen.SecretSeed.moreLightning, on);
                Seed(WorldGen.SecretSeed.noLightning, on);
            },
        },
        new Entry
        {
            Key = "rain", Name = "[i:3031]一年的雨量", RuntimeSafe = true,
            Get = () => Main.IsRainingForever,
            Set = on =>
            {
                Seed(WorldGen.SecretSeed.rainsForAYear, on);
                if (on)
                {
                    if (!Main.IsRainingForever)
                        WorldGen.SecretSeed.DoRainsForAYear();
                }
                else if (Main.IsRainingForever)
                {
                    Main.raining = false;
                    Main.rainTime = 0;
                    Main.numClouds = 0;
                }
            },
        },

        new Entry { Key = "ftw", Name = "[i:678]for the worthy", Get = () => Main.getGoodWorld, Set = on => Main.getGoodWorld = on },
        new Entry { Key = "dst", Name = "[i:5091]永恒领域（饥荒联动）", Get = () => Main.dontStarveWorld, Set = on => Main.dontStarveWorld = on },
        new Entry { Key = "va", Name = "[i:5597]吸血鬼", Get = () => Main.vampireSeed, Set = on => Main.vampireSeed = on },

        #endregion

        #region 世界属性（会写进 .wld 存档，默认不进随机池）

        new Entry { Key = "2020", Name = "05162020 醉酒世界", Get = () => Main.drunkWorld, Set = on => Main.drunkWorld = on },
        new Entry { Key = "2021", Name = "05162021 十周年庆典", Get = () => Main.tenthAnniversaryWorld, Set = on => Main.tenthAnniversaryWorld = on },
        new Entry { Key = "ntb", Name = "not the bees", Get = () => Main.notTheBeesWorld, Set = on => Main.notTheBeesWorld = on },
        new Entry { Key = "remix", Name = "Remix（don't dig up）", Get = () => Main.remixWorld, Set = on => Main.remixWorld = on },
        new Entry { Key = "nt", Name = "No Traps", Get = () => Main.noTrapsWorld, Set = on => Main.noTrapsWorld = on },
        new Entry { Key = "zenith", Name = "Zenith 天顶", Get = () => Main.zenithWorld, Set = on => Main.zenithWorld = on },
        new Entry { Key = "sky", Name = "空岛", Get = () => Main.skyblockWorld, Set = on => Main.skyblockWorld = on },
        new Entry { Key = "infect", Name = "感染世界", Get = () => Main.infectedSeed, Set = on => Main.infectedSeed = on },

        #endregion
    };

    /// <summary>按关键字查找（忽略大小写）</summary>
    public static Entry Find(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        return All.FirstOrDefault(e => string.Equals(e.Key, key.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>当前生效的彩蛋特性（中文名），没有则返回空串</summary>
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
    /// 恢复本次事件改动过的所有彩蛋特性。
    /// </summary>
    /// <param name="broadcast">是否全服播报</param>
    public static void RestoreAll(bool broadcast = true)
    {
        if (_touched.Count == 0)
            return;

        // 复制一份再遍历：Set 内部可能间接影响其它项
        foreach (var kv in _touched.ToList())
        {
            var e = Find(kv.Key);
            if (e == null)
                continue;

            try { e.Set(kv.Value); }
            catch (Exception ex) { TShock.Log.Error($"恢复彩蛋特性 [{kv.Key}] 失败：{ex.Message}"); }
        }

        _touched.Clear();
        LongNightPlugin.SaveState();
        TSPlayer.All.SendData(PacketTypes.WorldInfo);

        if (broadcast)
            TSPlayer.All.SendInfoMessage("彩蛋特性 已恢复原状");
    }

    /// <summary>
    /// 随机开启一个彩蛋特性。
    /// </summary>
    /// <returns>被开启的特性名；未开启时返回 null</returns>
    public static string Run()
    {
        var cfg = LongNightPlugin.Conf.DawnEvent.SecretSeed;
        if (!cfg.Enable)
            return null;

        // 解析随机池（跳过配置里写错的关键字）
        var pool = cfg.Pool.Select(Find).Where(e => e != null).ToList();
        if (pool.Count == 0)
        {
            TShock.Log.ConsoleError("『 黎明事件 』彩蛋随机池为空或关键字无效，已跳过");
            return null;
        }

        // 先恢复上一次，避免特性叠加
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
            TShock.Log.Error($"开启彩蛋特性 [{pick.Key}] 失败：{ex.Message}");
            _touched.Remove(pick.Key);
            LongNightPlugin.SaveState();
            return null;
        }

        TSPlayer.All.SendData(PacketTypes.WorldInfo);
        return pick.Name;
    }
}
