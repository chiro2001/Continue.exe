# Unity 侧源码镜像

最新版为 `Assets/Scenes/OWM_Episode123_v5.unity`（前三段 V5），由保存后的 V4 场景复制后增量制作，资源在 `Episode123/Revision5/`，方案见 [spec/42](../spec/42-episode123-v5-look-and-throughline.md)。构建顺序：`OWEpisodeV5Build.CreateScene` → `PrepareImport` → `tools/sync_episode123_v5.py` → `ResumeImport` → `RepairPrograms` → `OWEpisodeV5Atmosphere.Build`、`OWEpisodeV5Signal.BuildPhosphor/BuildTracker`、`OWEpisodeV5Identity.BuildCracktro/BuildGreetz`、`OWEpisodeV5Build.ApplyLayout`、`OWEpisodeV5Foley.Import/Build`、`OWEpisodeV5Cast.Apply`、`OWEpisodeV5Build.ApplyText` → 各模块 `Validate` → `OWEpisodeV5Build.FinalizeAndSave`；Play Mode 中运行 `OWEpisodeV5Validation.InteractionChecks/LiveValidation`；停止后 `CaptureAll`、`StartMovie`、`SdkBuildOnly`、`InspectBuiltBundle`、`ReopenCheck`。V5 运行时新增 `OWV5Atmosphere`、`OWV5Tracker`、`OWV5Greetz`，其余沿用 V4 组件。

最新优化版为 `Assets/Scenes/OWM_Episode123_v2.unity`，验收与用户原话见 [spec/27](../spec/27-episode123-review-and-revision.md)，[新版影片与来源图库](/home/chiro/tmp/vrc-old-web-map/episode123-v2/review/index.html)。`OWEpisodeRevisionBuild` 在用户已保存的原场景副本上增量制作，资源放 `Episode123/Revision2/`。原版场景与下方第一版记录保留。

第二版通过射线 Canvas 开始/重播，播放中收起面板，不提供暂停；回车仅开始。运行组件 `OWEpisodeMontage` 采样0.64秒主切换、0.32秒闪回和4×16 FFT带数据，`OWEpisodeCaption` 跟随本地视点提供不遮挡字幕。

重建入口：`PreserveUserBaseline`（仅首次）、`RepairPrograms`、`ImportImages`、`Build`、`RefineKeyword`、`ViewpointChecks`、`CaptureAll`；Play Mode 用 `InteractionChecks` / `LiveValidation`；停止后 `Save`，再 `StartMovie` / `MovieStatus` 与 `SdkBuildOnly`。批次同步脚本 `tools/sync_episode123_v2.py` 仍需配合原 Builder 的 `PrepareImport` / `ResumeImport`，序列化资源全部经 Pipeline。

当前自主制作版本是前三段（0—133.12 秒），独立场景为 `Assets/Scenes/OWM_Episode123.unity`，进度与验收范围见 [spec/26](../spec/26-episode123-production.md)。旧主场景和下方 180 秒原型说明保留。

## 前三段演出

验收资料见 [影片、12 场面截图与本地测试包](/home/chiro/tmp/vrc-old-web-map/episode123/review/index.html)。已完成六轮迭代、完整 Udon / ClientSim 连续运行和官方 SDK 本地构建；真实头显与多客户端体验尚未实测。

本仓库的 `art/source/episode123/` 为可编辑自绘源，`art/export/episode123/` 为 PNG 导出；`data/scene-score.json` 和 `data/episode123-*.json` 记录场面、素材、音符/包络、操作音效及制作状态。远端 Unity 序列化资源统一位于 `Assets/OldWebMap/Episode123/`，由 `AgentScripts/OWEpisodeBuild.cs` 经 Pipeline 制作。

| 入口 | 用途 |
| --- | --- |
| `OWEpisodeBuild.ImportAssets` / `ImportFoley` | 纹理与音频导入设置、字体准备。保留原图像素尺寸，经典 UI 与插画分别采样 |
| `OWEpisodeBuild.RepairPrograms` | Udon 程序生成与编译 |
| `OWEpisodeBuild.Build` | 在自有层级制作 12 场面、群像动作、设备/局部空间与四轨播放，保存独立场景 |
| `OWEpisodeBuild.CaptureAll` | 逐场固定视点截图，结束时恢复起始状态 |
| `OWEpisodeBuild.MotionChecks` | 两种 Humanoid 外观共用动作的位移与基本接触范围 |
| `OWEpisodeBuild.ViewpointAndShaderChecks` | 左右与高低视点、Page Surface 立体变体编译 |
| `OWEpisodeBuild.LiveSmoke` / `LiveButtons` | Play Mode 中执行真实 Udon 的播放、暂停、跳段、重播与交互入口 |
| `OWEpisodeBuild.LiveEndurance` | 整段连续运行、收到锚点后的本地恢复、四轨与输出检测，记录瞬时/持续采样差和更新耗时 |
| `OWEpisodeBuild.StartMovie` / `MovieStatus` | 编辑器固定视点离线参考帧；25 fps，末尾附 1.92 秒验收提示 |
| `OWEpisodeBuild.SetupSdkLayers` | 在 SDK 验证要求时，通过官方方法配置图层与碰撞矩阵，保留前后记录 |
| `OWEpisodeBuild.SdkBuildOnly` | 调用官方 SDK 的本地 Build API，输出本地测试包 |

播放器入口：进入 Play Mode 后按回车播放／暂停，左侧控制台可重播及跳至三段起点。运行时 `OWEpisodeDirector` 同步播放锚点；`OWEpisodeStage` 采样预制场面与骨骼占位动作；`OWEpisodeMusic` 以同一个 DSP 时刻启动四源并驱动包络；`OWEpisodeFoley` 触发原创非语音反馈；`OWEpisodeButton` 提供世界内交互。

同步源文件使用 `tools/sync_episode123.py`，批量复制前先经 Pipeline `PrepareImport`，完成后 `ResumeImport`；新增运行类型时使用 `--defer-builder`，待编译完成再复制新版 builder，避免尚未导入的类型阻塞导入恢复。只改图可用 `--art-only`。源码/素材复制不代替 Pipeline 导入与验证。重建前应保留当前编辑内容；`Build` 会拒绝带有未保存场景的切换。

素材与迭代检查入口：`tools/draw_episode123.py`、`prepare_episode123_audio.py`、`make_episode123_foley.py`、`review_episode123.py`、`validate_episode123.py`。原始系统字体留任务 tmp，运行素材仅包含栅格结果；Unity 世界空间文字使用项目已有的 Noto 字体资源。

人物资产与导入流程见 [spec/07-characters.md](../spec/07-characters.md)，当前资产记录见 [data/actors.json](../data/actors.json)。角色外观、材质、Humanoid Avatar、动作和场景由 Pipeline 在 Windows 工程中制作，本目录只保留源码镜像。

角色预览场景：`Assets/Scenes/OWM_Actors.unity`。`OWActorRig` 是可替换外观与演出逻辑之间的骨骼接口；`AgentScripts/OWActorSetup.cs` 负责导入、绑定和验证。已有预览场景不重复重建，使用 `Pose`、`FramePreview` 等入口做定向调整。

本目录只放 Unity 工程的**纯文本源码镜像**（`.cs`）。场景、材质、字体、Udon 程序资产等序列化内容留在远端工程里，由 Pipeline 生成与维护，不在这里重复。

| 项 | 值 |
| --- | --- |
| 远端工程 | `192.168.101.5` → `D:\Projects\VRChatProjects2\vrc-old-web-map` |
| Unity | 2022.3.22f1 + VRChat SDK 3.10.5 |
| 镜像内容 | `Assets/OldWebMap/Scripts/*.cs`、`AgentScripts/OWMBuild.cs` |
| 同步方式 | `scp` 覆盖脚本 → 远端 `unity command recompile` |
| 调用工具 | `python tools/unity_remote.py <Pipeline 命令>`（默认已指向本工程） |

## 构建入口

在远端工程根目录执行：

```bash
# 字体 + 材质 + 数据组件 + 场景骨架，一次重建（可反复执行）
unity command run_script --file AgentScripts/OWMBuild.cs --entry OWMBuild.All

# 脚本用文件同步进来后，补齐 UdonSharp 程序资产并编译
unity command run_script --file AgentScripts/OWMBuild.cs --entry OWMBuild.RepairPrograms

# 诊断：场景里的演出对象与绑定关系 / JSON 是否解析正常
unity command run_script --file AgentScripts/OWMBuild.cs --entry OWMBuild.Inspect
unity command run_script --file AgentScripts/OWMBuild.cs --entry OWMBuild.JsonCheck
```

数据源是本仓库的 `data/*.json`；构建前同步到远端 `AgentScripts/data/`：

| 文件 | 用途 |
| --- | --- |
| `timeline.json` | 段落与事件 → `OWTimelineData` |
| `lyrics.json` | 歌词与 greetz → `OWLyricData` |
| `stage.json` | 分区坐标、22 个节点、21 条连线、卡片尺寸、出生点 |
| `labels.json` | 全部上屏文字（铭牌、节点名片、状态文案） |

Udon 侧不做运行时 JSON 解析，结构化数值全部由 builder 烘进场景组件。

> 朝向约定：施工单里的 `rotation_y` 表示「牌面朝向玩家」；Unity 里可读朝向与之相反，
> 因此 builder 统一加 180°。新增朝向相关的道具时沿用这条规则。

## 运行时组件

| 组件 | 脚本 | 职责 |
| --- | --- | --- |
| `OWTimelineDirector` | `OWTimelineDirector.cs` | 段落时间基准、事件派发、导演接口（播放/暂停/跳段），只同步「播放锚点」 |
| `OWTimelineData` | `OWTimelineData.cs` | 段落与事件数据（并行数组） |
| `OWLyricData` | `OWLyricData.cs` | 13 句歌词与 greetz 名单 |
| `OWLyricTrackPlayer` | `OWLyricTrackPlayer.cs` | 按段落切句、逐字高亮、句末淡出 |
| `OWGreetzRoll` | `OWGreetzRoll.cs` | 彩色 greetz 滚动（位置由时间推导，天然同步） |
| `OWHeadHud` | `OWHeadHud.cs` | 把歌词带固定在本地玩家视野下缘 |
| `OWStageBeacon` | `OWStageBeacon.cs` | 段落进入时点亮对应舞台区（换材质 + 开关子物体） |
| `OWVisitorCounter` | `OWVisitorCounter.cs` | 访客计数器累加，片尾归零 |

## 场景

`Assets/Scenes/OWM_Main.unity`（由默认世界场景派生，保留 `VRCSceneDescriptor`）：

```
OWM_WorldRoot
├─ Stage
│  ├─ Data                 OWTtimelineData + OWLyricData
│  ├─ Director             OWTimelineDirector
│  ├─ Zones                7 个分区（Boot/Golden/Netdisk/Opensource/Vrchat/Hope/Outro）
│  │                       每区：6 m 宽地板 + 右侧信标柱（OWStageBeacon）+ 过渡桥
│  ├─ Nodes                22 个节点：状态色坐标点 + 名片卡（标题/上线时间/访客/状态行）
│  └─ Links                21 条 LineRenderer 连线（宽 0.06 m）
├─ LyricTrack              OWHeadHud + 世界空间画布（歌词行 + greetz 滚带）
├─ HUD                     门头标题 + 访客计数器
├─ Audio                   AudioSource 占位（等音乐源定稿）
└─ PlayerSpawn             入口出生点（坐标取 stage.json 的 player.spawn）与入口铭牌
```

走廊半宽 3 m，沿 +Z 从 −8 m 铺到 150 m，分区之间用 2.4 m 宽的桥连起来，玩家可以一路走完全片；每个分区右侧一根信标柱，进入该段落时点亮。

未实现的部分（下一步）：17 个道具与特效、12 个相机关键帧轨道、节点六态的动态切换。

## 验证方式

编辑器 Play Mode 下 SDK 自带 ClientSim 会生成本地玩家，可直接验证：歌词带随段落推进、greetz 切换、信标点亮、片尾计数器归零。运行截图保存在远端工程 `Screenshots/`。
