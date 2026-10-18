# 04 Unity / UdonSharp 实现方案

目标工程：`D:\Projects\VRChatProjects2\vrc-old-web-map`（Unity 2022.3.22f1 + VRChat SDK3 Worlds + UdonSharp）。
操作规范沿用已有项目：SSH 到 `192.168.101.5` → PowerShell → `unity` CLI → Unity Pipeline；序列化内容一律走 Pipeline，纯文本（`.cs`、`.md`、`.json`）可直接编辑。

## 场景结构

```
WorldRoot
├─ Stage                    演出总控（段落时间基准、同步、导演模式）
│  ├─ BootSequence          冷启动：CMOS 字幕、启动音、第一枚光点
│  ├─ Act1_GoldenMap        点阵地图主舞台：Nodes / Links / Grid / 名片卡
│  ├─ Act2A_Netdisk         限速走廊、弹窗墙、秒传报错
│  ├─ Act2B_OpenSource      仓库墓碑林、issue 洪水、赏金公告
│  ├─ Act2C_VRChat          世界被摘除、框选复制、封禁公告
│  └─ Act3_Hope             希望之河、复亮点、案例墙
├─ LyricTrack               下缘 HUD 歌词带 + greetz 滚动
├─ HUD                      访客计数器、状态条、`C:\>_`
├─ Audio                    音乐源 + 环境源 + 音效锚点
├─ Lighting                 烘焙光照、体积雾、后处理 Volume
└─ PlayerSpawn              入口、`Best viewed in 800×600` 铭牌
```

## 数据流

```
spec/data/*.json  ──(编辑器导入脚本，经 Pipeline 执行)──►  ScriptableObject 资产  ──►  UdonSharp 组件
```

Udon 侧不解析 JSON，避免运行时开销与版本漂移；导入脚本只在编辑器里跑一次，产物是可调参的资产。

| JSON | 资产 | 消费者 |
| --- | --- | --- |
| `data/timeline.json` | `TimelineData` | `TimelineDirector` |
| `data/lyrics.json` | `LyricTrackData` | `LyricTrackPlayer`、`GreetzRoll` |
| `data/quotes.json` | `QuoteSet` | `QuoteCardSpawner` |
| `data/worlds.schema.json` 对应的策展数据 | `MapNodeSet` | `MapNodeController`、`MapNodeLabel` |

## 组件清单（UdonSharp）

| 组件 | 职责 | 关键字段 |
| --- | --- | --- |
| `TimelineDirector` | 段落时间基准、切段事件、暂停/跳段、多人广播 | `_sections[]`、`_currentIndex`、`_startTime`、`OnSectionChanged` |
| `SyncClock` | 以实例主的时间为基准对齐所有玩家 | `_syncedStartTime`、`_paused` |
| `MapNodeController` | 六态切换：材质参数、贴图、标签、音效 | `_state`、`SetState(int, float)` |
| `MapNodeLabel` | 名片卡文字（名称/作者/年份/访问量/状态文案） | `_nodeId`、`_titles[]` |
| `LinkBeam` | 节点间连线的揭示与断裂 | `_lineRenderer`、`BreakAt(float)` |
| `LyricTrackPlayer` | 逐字高亮、句间淡出、副歌切换 greetz | `_lines[]`、`_charRevealCurve` |
| `GreetzRoll` | 彩色滚动名单 | `_entries[]`、`_scrollSpeed` |
| `QuoteCardSpawner` | 引文卡生成与回收 | `_quotes[]`、`_lifetime` |
| `VisitorCounter` | 数字牌滚动并归零 | `_digits[]`、`CountTo(int)` |
| `SectionTrigger` | 自由模式下走进区域触发段落 | `_sectionId`、`_playerOnly` |
| `DirectorPanel` | 播放/暂停/跳段（仅实例主） | `_allowList` |
| `ExitPortal` | 片尾传送回入口 | `_spawnPoint` |

实现纪律：

- 状态切换一律事件驱动，不在 `Update()` 里逐帧写材质或同步变量。
- 大数量元素（弹窗墙、issue 洪水、案例墙）用粒子/合并网格 + 少量参数控制，不同步单个元素。
- 每个组件都要在 Inspector 里能调参，方便后期改节奏而不改代码。

## Pipeline 操作清单

1. 确认远端 Unity 与 Pipeline 可达（`unity pipeline list --json`），必要时按已有项目的兼容适配包安装 `com.unity.pipeline`。
2. 创建或切换目标场景，搭出上表的根节点结构（场景内 GameObject 的创建与层级改动必须走 Pipeline）。
3. 导入纹理、材质与字体（外部资源导入与导入设置走 Pipeline；批量导入时先复制到目标目录再刷新）。
4. 生成 ScriptableObject 资产（由导入脚本产出），并在 Inspector 里绑定到对应组件。
5. 保存场景，停 Play Mode，等待编译完成后做一次性能检查（面数、材质数、纹理内存、音频源数量）。
6. 双端同步前检查两边 Git 状态，本地改动先提交，再走项目已有的快照/同步脚本。

> Pipeline 的具体命令与参数以当前 `unity command` 列表为准，不凭记忆写死；不确定时先 `--help`。

## 音乐与卡点接入

本节按用户的分轨空间音频方向更新为实现候选，尚未在 Unity/VRChat 中完成。

1. 音源已确认为 FC14，一轮 256 秒；保留原混音作参考，从已有可重组的四轨准备同起点、同长度、统一增益的单声道素材。试听 WAV 含旧左右声像和下一循环约 0.1 秒，不能直接当最终空间素材。
2. 原有行号与音符表作为精确时间输入；区分 80 ms 行、2.56 秒序列段与待试听确认的音乐重拍，`audio/beats.json` 仍为待制作的运行输入。
3. 四个世界空间音源共用同一音乐时钟；本地四轨起点与相位关系单独验证，不能只用视觉 80 ms 容差评估音轨同步。
4. 常态四个音乐源集中于前方、距离和高度接近，优先整体融合；只有选定高潮才明显展开一至两条合适声部，之后回到前方。音源位置、音量与预设声场参与调度；`VRC_SpatialAudioSource` 空间化属性在初始化设定，不假定可以用动画逐拍修改范围/体积半径。
5. 已选空间变化为主、少量动态：保留源音符、速度与音色，附加处理以局部增益和淡入淡出为主，具体数值待听感；多人站位、晚加入、传送、隔离、复播的平衡和恢复都需验证。原型试听布局、交接规则与素材测量见 [spec/14](14-fc14-music-visual-score.md)。

## 里程碑

| 阶段 | 内容 | 完成判据 |
| --- | --- | --- |
| M1 空间骨架 | 点阵地图、相机路径、音乐播放 | 能走完第一幕，音乐与光点同步 |
| M2 状态机 | 六态切换 + 四条动线 | 一整个节点生命周期可演示 |
| M3 双线 | 歌词带 + greetz + 引文卡 | 13 句按段出现，无错位 |
| M4 第三幕与片尾 | 希望之河 + 计数器归零 | 全片能一次跑完 |
| M5 打磨 | 后处理、音效、性能优化 | 达到 PC 性能预算 |
| M6 上传验证 | 多人同步、新玩家入房对齐、首次上传 | 两人以上实测通过 |

## 验收清单

- 全片 3 分钟可一次跑完，PC 上不出现明显掉帧。
- 音乐与画面卡点误差小于 80 ms。
- 新加入的玩家在 1 秒内对齐到当前段落与节点状态。
- 性能等级不低于 PC Medium；面数、材质、纹理、音频源都在预算内。
- 片尾署名完整：模块作者、模板来源、素材来源；无真实受害者点名。

## 工程协作约定

- 中文回复；临时文件放 `~/tmp/vrc-old-web-map/`，Windows 侧放 `%USERPROFILE%\tmp\vrc-old-web-map\`。
- Unity 相关操作先读 `unity-pipeline` 技能，再动手；一次改动批量执行并统一验证，避免逐项往返。
- 本仓库的 `spec/` 与 `data/` 是创意与数据的事实源，Unity 侧只做消费；改创意先改这里。
