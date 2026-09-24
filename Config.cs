using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TShockAPI;

namespace LongNight;

/// <summary>
/// 插件配置。
/// <para>
/// 默认值就是下面这些字段的初始化器；首次运行会把它们写一份到
/// <c>&lt;TShock.SavePath&gt;/LongNight/config.json</c> 供服主修改。
/// </para>
/// </summary>
public class Config
{
    // 插件总开关（false = 完全不工作）
    public bool Enabled = true;

    // 永夜（连续夜晚循环）
    public LongNightSettings LongNight = new();

    // 始终满月
    public bool AlwaysFullMoon = true;

    // 黎明前事件
    public DawnEventSettings DawnEvent = new();

    // 终章时把「黎明事件」开启的彩蛋特性恢复原状
    public bool RestoreOnFinish = true;

    // 服务器重启后，若发现上次事件开启的项还没恢复（崩溃 / 强杀），启动时自动补一次恢复
    public bool RestoreOnStartup = true;

    // 配置目录：`<TShock.SavePath>/LongNight`
    public static string Dir => Path.Combine(TShock.SavePath, "LongNight");

    // 配置文件路径
    public static string FilePath => Path.Combine(Dir, "config.json");

    // 加载配置；文件不存在时先生成一份带默认值的
    public static Config Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(new Config(), Formatting.Indented));
            }

            return JsonConvert.DeserializeObject<Config>(File.ReadAllText(FilePath), new JsonSerializerSettings
            {
                Error = (_, e) => e.ErrorContext.Handled = true
            }) ?? new Config();
        }
        catch (Exception ex)
        {
            TShock.Log.Error($"读取配置失败，改用默认配置：{ex.Message}");
            return new Config();
        }
    }

    // 写回配置
    public static void Save(Config cfg)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(cfg, Formatting.Indented));
        }
        catch (Exception ex)
        {
            TShock.Log.Error($"保存配置失败：{ex.Message}");
        }
    }
}

/// <summary>永夜（连续夜晚循环）设置。</summary>
public class LongNightSettings
{
    // 是否开启永夜。关闭后昼夜恢复自然长度，但黎明事件照常触发
    public bool Enable = false;

    // 一轮永夜包含的夜晚总数
    public int NightTotal = 8;
}

/// <summary>黎明前事件设置（游戏内 04:25~04:29 触发）。</summary>
public class DawnEventSettings
{
    // 事件总开关
    public bool Enable = true;

    // 月亮样式事件
    public MoonStyleSettings MoonStyle = new();

    // 彩蛋特性事件
    public SecretSeedSettings SecretSeed = new();

    // 世界事件（天气 / 天象 / 派对…）
    public WorldEventSettings WorldEvent = new();
}

/// <summary>月亮样式事件设置。</summary>
public class MoonStyleSettings
{
    // 是否启用
    public bool Enable = true;

    /// <summary>
    /// 更换方式：<c>cycle</c>=按顺序轮流（默认），<c>random</c>=随机，<c>off</c>=不更换
    /// </summary>
    public string Mode = "cycle";
}

/// <summary>彩蛋特性事件设置。</summary>
public class SecretSeedSettings
{
    // 是否启用
    public bool Enable = true;

    // 每次事件前，先把上一次事件开启的特性关掉（避免叠加）
    public bool RestorePrevious = true;

    /// <summary>
    /// 随机池。写 <see cref="SecretSeedEvent"/> 的关键字。
    /// <para>
    /// 默认只收「运行时确实有表现」的项：
    /// eh 无尽万圣 / ec 无尽圣诞 / ml 更多闪电 / nl 无闪电 / gl 绿色闪电 / rain 一年的雨量。
    /// </para>
    /// <para>
    /// 其余关键字（2020 / 2021 / ftw / dst / remix / zenith / va / ntb / nt / sky / infect）
    /// 属于<b>世界属性</b>，改了会被写进 .wld 存档，默认不放进池子，需要时自行添加。
    /// </para>
    /// </summary>
    // 必须显式声明 Replace：Newtonsoft 默认会「往已初始化的集合里追加」，会导致每次 Load 池子翻倍
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<string> Pool = new()
    {
        "eh",   // 无尽万圣
        "ec",   // 无尽圣诞
        "ml",   // 更多闪电
        "nl",   // 无闪电
        "gl",   // 绿色闪电（更多闪电 + 无闪电）
        "rain", // 一年的雨量
        "ftw", // ftw
        "dst", // 饥荒
        "va", // 吸血鬼
    };
}

/// <summary>世界事件设置。</summary>
public class WorldEventSettings
{
    // 是否启用
    public bool Enable = true;

    // 每次事件前，先把上一次事件开启的项关掉（避免叠加）
    public bool RestorePrevious = true;

    /// <summary>
    /// 随机池。写 <see cref="WorldEvent"/> 的关键字。
    /// <para>
    /// 通用：meteor 陨石 / party 派对 / storm 暴风雨。<br/>
    /// 夜晚类（需要永夜开启）：blood 血月 / lantern 灯笼夜 / starfall 流星雨。<br/>
    /// 白天类（需要永夜关闭）：eclipse 日食 / slime 史莱姆雨 / sand 沙尘暴。
    /// </para>
    /// <para>
    /// 随机时会按当前昼夜自动过滤：永夜开着只会抽夜晚类 + 通用，关着只会抽白天类 + 通用。
    /// </para>
    /// </summary>
    // 必须显式声明 Replace：Newtonsoft 默认会「往已初始化的集合里追加」，会导致每次 Load 池子翻倍
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<string> Pool = new()
    {
        "meteor",  // 陨石
        "party",   // 派对
        "storm",   // 暴风雨
        "blood",   // 血月
        "lantern", // 灯笼夜
        "starfall",// 流星雨
        "eclipse", // 日食
        "slime",   // 史莱姆雨
        "sand",    // 沙尘暴
    };
}

/// <summary>
/// 运行状态。与配置分开存放，避免服主手改配置时把进度冲掉。
/// </summary>
public class State
{
    // 当前处于永夜的第几天（0 基）
    public int NightCurrent = 0;

    /// <summary>
    /// 「黎明事件」改动过的彩蛋特性：key -> 事件前的状态。
    /// <para>持久化，否则服务器重启后 RestorePrevious / 终章恢复都会失效，特性会一直叠加。</para>
    /// </summary>
    // 必须显式声明 Replace，避免每次 Load 往已初始化的集合里追加
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public Dictionary<string, bool> TouchedSeeds = new();

    /// <summary>
    /// 「黎明事件」改动过的世界事件：key -> 事件前的状态。
    /// </summary>
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public Dictionary<string, bool> TouchedWorld = new();

    // 状态文件路径
    public static string FilePath => Path.Combine(Config.Dir, "state.json");

    // 加载状态；文件不存在或损坏时返回默认值
    public static State Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new State();

            return JsonConvert.DeserializeObject<State>(File.ReadAllText(FilePath), new JsonSerializerSettings
            {
                Error = (_, e) => e.ErrorContext.Handled = true
            }) ?? new State();
        }
        catch (Exception ex)
        {
            TShock.Log.Error($"读取状态失败，改用默认状态：{ex.Message}");
            return new State();
        }
    }

    // 写回状态
    public static void Save(State state)
    {
        try
        {
            Directory.CreateDirectory(Config.Dir);
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(state, Formatting.Indented));
        }
        catch (Exception ex)
        {
            TShock.Log.Error($"保存状态失败：{ex.Message}");
        }
    }
}
