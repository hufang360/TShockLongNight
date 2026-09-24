# TShockLongNight

#### 介绍

会出现连续 8 个夜晚，这期间称为“永夜”，永夜之后是一个白天，然后再次进入“永夜”。
“永夜”期间会更换渔夫任务，但是不会结束血月。

月亮固定为**满月**，并在每个夜晚的**黎明前几分钟（游戏内 04:25~04:29）**触发「黎明事件」：

- **更换月亮样式**：按顺序轮流更换（正常 → 火星 → 土星 → 秘银 → 偏蓝白 → 绿色 → 糖果 → 金星 → 三重月亮 → 循环）
- **更换彩蛋特性**：从池子里随机开启一个秘密世界 / SecretSeed 特性
- **触发世界事件**：从池子里随机触发一个天气 / 天象 / 派对事件

三者会并成一行播报，例如：`今天是 紫色的三重月亮, [i:5597]吸血鬼, [i:4271]血月`

---

## 配置

首次运行会自动生成 `<TShock.SavePath>/LongNight/config.json`：

```jsonc
{
  "Enabled": true,              // 插件总开关
  "LongNight": {
    "Enable": true,             // 永夜（连续夜晚循环）开关；关闭后昼夜恢复自然长度，黎明事件照常触发
    "NightTotal": 8             // 一轮永夜的夜晚总数
  },
  "AlwaysFullMoon": true,       // 始终满月
  "DawnEvent": {
    "Enable": true,             // 黎明事件总开关
    "MoonStyle": {
      "Enable": true,
      "Mode": "cycle"           // cycle=按顺序轮流 / random=随机 / off=不更换
    },
    "SecretSeed": {
      "Enable": true,
      "RestorePrevious": true,  // 每次事件前先关掉上一次开启的特性，避免叠加
      "Pool": ["eh", "ec", "ml", "nl", "gl", "rain"]
    },
    "WorldEvent": {
      "Enable": true,
      "RestorePrevious": true,  // 每次事件前先关掉上一次开启的事件，避免叠加
      "Pool": ["meteor", "party", "storm", "blood", "lantern", "starfall", "eclipse", "slime", "sand"]
    }
  },
  "RestoreOnFinish": true,      // 终章时把事件开启的彩蛋特性 / 世界事件恢复原状
  "RestoreOnStartup": true      // 重启后发现上次事件开启的项还没恢复（崩溃 / 强杀）时，启动自动补一次
}
```

进度另存于 `<TShock.SavePath>/LongNight/state.json`，与配置分开，方便手改配置：

```jsonc
{
  "NightCurrent": 3,                        // 当前第几夜
  "TouchedSeeds": { "va": false },          // 事件改动过的彩蛋：key -> 事件前的状态
  "TouchedWorld": { "blood": false }        // 事件改动过的世界事件
}
```

> `TouchedSeeds` / `TouchedWorld` 持久化是必须的：否则服务器重启后 `RestorePrevious` 和终章恢复都会找不到
> “上一次开了什么”，特性会一晚一晚叠加上去。

---

## 指令

权限节点：`longnight`

| 指令 | 说明 |
| --- | --- |
| `/ln help` | 帮助 |
| `/ln info` | 查看 永夜 / 满月 / 月相 / 黎明事件 状态 |
| `/ln true` / `/ln false` | 开启 / 关闭 永夜（写入配置） |
| `/ln total <number>` | 设置 永夜循环天数（≥2） |
| `/ln current <number>` | 设置 当前处于永夜的第几天 |
| `/ln event <on/off>` | 查看 / 开关 黎明事件 |
| `/ln moon <on/off>` | 查看 / 开关 始终满月 |
| `/ln style [1-9]` | 查看 / 手动设置 月亮样式 |
| `/ln seed [random/关键字]` | 查看 / 随机 / 切换 彩蛋特性 |
| `/ln world [random/关键字]` | 查看 / 随机 / 切换 世界事件 |
| `/ln restore` | 恢复 黎明事件开启的彩蛋特性 / 世界事件 |
| `/ln reload` | 重新加载 配置与状态 |

---

## 彩蛋特性关键字

`WorldGen.SecretSeed.Enable()` 只改内部标志，**不会**自动同步到 `Main.*`（原版是在世界加载期做的映射），
所以插件里每一项都自己把 `Main.*` 一起改掉，否则运行时看不到效果。

**运行时有效（默认随机池）**

| 关键字 | 名称 |
| --- | --- |
| `eh` | 无尽万圣 |
| `ec` | 无尽圣诞 |
| `ml` | 更多闪电 |
| `nl` | 无闪电 |
| `gl` | 绿色闪电（更多闪电 + 无闪电） |
| `rain` | 一年的雨量 |

**世界属性（会写进 .wld 存档，默认不进池子）**

`2020` 醉酒世界 / `2021` 十周年 / `ftw` / `ntb` / `dst` 饥荒 / `remix` / `nt` / `zenith` /
`sky` 空岛 / `va` 吸血鬼 / `infect` 感染世界

> 这些是**世界属性**，改了会被保存进存档。需要时再自行加进 `Pool`，或直接用 `/ln seed <关键字>` 手动切换。

---

## 世界事件关键字

| 关键字 | 名称 | 适用 |
| --- | --- | --- |
| `meteor` | 陨石 | 通用 |
| `party` | 派对 | 通用 |
| `storm` | 暴风雨 | 通用 |
| `blood` | 血月 | 夜晚类 |
| `lantern` | 灯笼夜 | 夜晚类 |
| `starfall` | 流星雨 | 夜晚类 |
| `eclipse` | 日食 | 白天类 |
| `slime` | 史莱姆雨 | 白天类 |
| `sand` | 沙尘暴 | 白天类 |

> **为什么分昼夜？** 事件触发在 04:25，之后 5 分钟内就会天亮。
> 永夜**开着**时，插件会把时间拉回夜晚起点，所以只有夜晚类事件有意义；
> 永夜**关着**时，接下来是白天，只有白天类事件有意义。
> 随机时会按当前永夜开关自动过滤，不会抽到无效事件。

---

## 时间参考

| 阶段 | 区间 | Main.time |
| --- | --- | --- |
| 白天 | 04:30 → 19:30 | 0 → 54000 |
| 夜晚 | 19:30 → 04:30 | 0 → 32400 |

- 入夜窗口：`Main.time ∈ [60, 300]` = 19:31~19:35
- 黎明窗口：`Main.time ∈ [32100, 32340]` = 04:25~04:29
