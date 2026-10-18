# 21 头部摄像机气氛 FX（玩家视野整体层）

日期：2026-10-16。上游：用户要求「做一套应用在玩家头部摄像机的整体 shader，用于气氛渲染和烘托」。
本稿记录 VRChat 侧的两条可行路线、我们实现的三个文件、参数与分段用法。

同日静态复核见 [22](22-new-assets-review.md)。下文 PC/Quest、性能与覆盖范围的判断仍需目标客户端实测，已有编辑器编译记录不能代替这些结果。常态头锁气氛与旧分区用法是本稿研究建议；当前项目已选方向仍以局部年代载体、原生空间和世界运动为准。

配套工件（均已导入远端 Unity 工程并通过编译校验）：

| 文件 | 说明 |
| --- | --- |
| `unity/Assets/OldWebMap/Shaders/OWHeadAtmosphere.shader` | 常态气氛层：四角压暗、色调、颗粒、黑边 + 扫描线、脉冲闪光与扫描条 |
| `unity/Assets/OldWebMap/Shaders/OWHeadSignalNoise.shader` | 信号丢失层：雪花噪点、丢行、滚动亮带、扫描缺口 |
| `unity/Assets/OldWebMap/Scripts/OWHeadOverlayRig.cs` | 头锁跟随 + 参数驱动 + 性能开关（UdonSharp） |
| `unity/AgentScripts/OWHeadFxVerify.cs` | Pipeline 校验脚本（`run_script` 调用，位于 Assets 外） |

## 一、VRChat 的硬约束：世界拿不到玩家摄像机

这是必须先说清的一点，它决定了实现路线：

- 世界（Udon）**不能**给玩家摄像机挂 `OnRenderImage`/`CommandBuffer`，也没有官方的相机访问接口。
- 能走的路只有两条：

**路线 A：官方后处理（Post Process Volume）**

世界侧放置全局 `PostProcessVolume`，由客户端把它作用到玩家摄像机上。
适合：Bloom、Color Grading（含 tonemapping/饱和度/对比度）、Vignette、Grain、
Lens Distortion、Chromatic Aberration、Dithering 等。
限制：VR 下景深与运动模糊通常不生效；Quest 端一般不支持后处理；
效果清单随客户端版本变化，**以实现时的客户端实测为准**。

**路线 B：头锁面片 + 自定义着色器（本稿实现）**

用 `VRCPlayerApi.GetTrackingData(Head)` 把一块面片固定在本地玩家视野前方，
面片上的着色器画扫描线、雪花、丢行、黑边、脉冲等。
这条路能做官方后处理做不到的「旧屏幕语言」，而且：

- 每客户端各摆各的，**不占同步量**，其他玩家看不到你眼前这一层（不是同步物体）；
- 不需要抓屏、不需要相机深度；PC 与 Quest 的双眼、覆盖范围和性能仍待客户端实测；
- 每个效果有两遍绘制，两种同时开启共四遍；是否比某组后处理轻，需在同一设备与画面下测量。

候选分工：**路线 A 负责基础影调，路线 B 负责特定事件的视野效果**，按需要叠加并实测。
本稿提供路线 B 的研究实现；当前整体美术以年代内容载体和原生空间为主，未决定全片常开头锁层。

## 二、我们的实现

### 2.1 OWHeadAtmosphere（常态气氛 + 脉冲）

两遍绘制：

| 遍 | 混合 | 做什么 |
| --- | --- | --- |
| `BASE` | `Blend DstColor Zero`（乘法） | 四角压暗 `_VignetteStrength/_VignetteRadius/_VignetteSoftness/_Aspect`、色调 `_TintColor/_TintStrength`、颗粒 `_GrainTex/_GrainStrength/_GrainScale/_GrainSpeed`、上下黑边 `_Letterbox/_LetterboxSoftness` |
| `DETAIL` | `Blend One One`（加色） | 扫描线 `_ScanlineColor/_ScanlineCount/_ScanlineStrength/_ScanlineSpeed`、脉冲闪光 `_Pulse/_PulseFlash`、上移扫描条 `_PulseScanBar/_PulseScanSpeed`、数字噪块 `_PulseBlocks` |

`_Intensity` 是总强度（0~2），常态建议 0.3~0.6；`_Pulse` 由事件推到 1 后自动衰减。
`ZTest Always` + `Queue=Overlay`，保证画在所有几何与粒子之上。

### 2.2 OWHeadSignalNoise（信号丢失）

| 遍 | 混合 | 做什么 |
| --- | --- | --- |
| `SNOW` | `Blend SrcAlpha OneMinusSrcAlpha` | 高频雪噪 `_SnowTex/_SnowScale/_SnowSpeed/_SnowThreshold/_SnowSoftness`，阈值化后成为半透明雪花 |
| `SPARK` | `Blend One One` | 丢行 `_DropAmount/_DropSpeed/_DropThickness`、自下而上的滚动亮带 `_RollAmount/_RollSpeed/_RollSharpness`、扫描缺口 `_GapAmount/_GapCount` |

只有一个驱动参数 `_Intensity`（0~1）：0 = 完全干净，1 = 雪花屏。
适合「链接失效」「归档中断」「被摘除」这类需要瞬时压制画面的时刻。

### 2.3 OWHeadOverlayRig（UdonSharp）

- `LateUpdate` 里取本地玩家头部姿态，把面片放到 `head.position + head.rotation * (0,0,distance)`，
  朝向与头部一致；`followSpeed = 0` 时硬跟随（与既有 `OWHeadHud` 一致）。
- `ApplyScale()` 按视角算尺寸：`height = 2 × distance × tan(FOV/2) × coverageBoost`，
  宽 = 高 × `aspect`（桌面约 1.6~1.8，VR 单眼约 1.0）。
- 对外接口：`SetIntensity(float)`、`SetPulse(float)`、`FirePulse()`、`ApplyScale()`，
  可直接被时间轴导演在段落切换时调用。
- 性能：`intensity` 与 `pulse` 都低于 `hideThreshold` 时关闭渲染器，不产生全屏填充。
- 材质参数名可配置（`intensityProperty`/`pulseProperty`），因此同一脚本能驱动两个着色器。

## 三、装配方式（下一步执行）

按项目规范，面片与材质必须走 Pipeline 在编辑器里做好，不在运行时新建：

1. 建一个 `Assets/OldWebMap/Prefabs/OWHeadFxOverlay.prefab`：单个 Quad（无碰撞体），
   `ZWrite Off`、`Queue Overlay` 的材质各一份（Atmosphere / SignalNoise 两个子物体）。
2. 挂 `OWHeadOverlayRig`，指向子物体的 `Renderer`。
3. 材质默认值：Atmosphere `_Intensity = 0.45`、`_Pulse = 0`；
   SignalNoise `_Intensity = 0`（平时不可见）。
4. 场景里放一份，位置无所谓（脚本每帧接管）。

## 四、分段用法建议（对着 [spec/15](15-integrated-show-outline.md) 与 [spec/14](14-fc14-music-visual-score.md)）

| 段落 | 气氛层 | 信号层 | 说明 |
| --- | --- | --- | --- |
| 冷启动 | `_Intensity` 0.2 → 0.5，扫描条扫过 | 0 | 开机自检感；扫描条 `_PulseScanSpeed` 与自检节奏对齐 |
| 黄金段 | 0.35，偏暖 `_TintColor` | 0 | 全片最亮，颗粒最小，不做脉冲 |
| 网盘峡 | 0.5，偏冷 | 失效瞬间 `FirePulse` 到 1 再衰减 | 扫描线加强，配合 `progress_bar` 卡住 |
| 开源荒原 | 0.45 | 偶发 0.3 闪一下 | 丢行模拟「归档中断」 |
| VRChat 城 | 0.55，黑边起 | 摘除瞬间 1，随后衰减 | 与整片熄灭同步，最重的一段 |
| 希望之河 | 0.3，偏青绿 | 0 | 四角压暗减弱，颗粒减半，表示回暖 |
| 片尾 | 0.15 → 0 | 0 | 完全收干净，留给 `C:\>_` |

脉冲落点建议只挑少数几个：开机、第一次失效、整体熄灭、第一次重亮。
**不要每个小节都打脉冲**——这是给高潮用的，不是节拍器。

## 五、性能与舒适度

- 全屏覆盖层的成本主要看填充率：PC 上两遍全屏很轻，但**Quest 建议只保留 Atmosphere**，
  并把 `_GrainStrength`、`_ScanlineStrength` 减半。
- VR 舒适度：扫描线频率越高越容易在边缘产生「纱窗」感，`_ScanlineCount` 建议 240~360；
  脉冲持续时间控制在 0.2~0.4 秒（`pulseDecayPerSecond ≈ 2.5~5`），避免频闪。
- 该面片是真实物体，因此 **VRChat 相机/推流会拍到这层效果，镜子里也能看到贴在脸上的面片**。
  如果某个镜头不能带效果，演出时用导演模式临时把 `SetIntensity(0)`。
- 与后处理叠加时先压低 `_Emission` 与 Bloom 阈值，避免过曝。

## 六、验证记录（2026-10-16）

远端 Unity 2022.3.22f1（`D:\Projects\VRChatProjects2\vrc-old-web-map`）：

| 检查 | 结果 |
| --- | --- |
| `list_shaders --filter Head` | `OldWebMap/Head Atmosphere Overlay`、`OldWebMap/Head Signal Noise` 均 `isSupported = true` |
| `run_script AgentScripts/OWHeadFxVerify.cs` | `atmosphereFound / signalFound / rigTypeFound = true`；`_Intensity/_VignetteStrength/_ScanlineStrength/_Pulse/_PulseFlash/_Letterbox`、`_SnowTex/_DropAmount/_RollAmount/_GapAmount` 属性齐备；`OWHeadOverlayRig` 的 `SetIntensity/SetPulse/FirePulse/ApplyScale` 四个方法均在 |
| Console | `compilationFailed = false`，errors 0，warnings 0 |

途中修掉的问题：`[Header(Base - multiply)]` 的连字符会让 ShaderLab 解析报
`unexpected $undefined ... expecting TVAL_ID`，改为纯字母与空格即可（已记录，避免再犯）。

## 七、待办

1. 用 Pipeline 建 `OWHeadFxOverlay.prefab` 与两份材质，接进 `OWM_Main`（需确认后执行）。
2. 在时间轴导演里接 `SetIntensity/SetPulse`，落点按第四节表格。
3. 试一版官方后处理配置（Bloom 阈值、Color Grading、Vignette、Grain、Lens Distortion、CA），
   与本文的气味层叠加后截图对拍；确认当前客户端实际生效的效果清单。
