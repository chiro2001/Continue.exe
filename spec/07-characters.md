# 07 人物资产与可替换骨架

## 已确认的方向（2026-10-15）

本次作品是在 VRChat 中实时演出的 3D MV，可以使用完整人物资产。先使用用户电脑中最近修改的 Kipfel 和 Nemesis；后期可以更换 Avatar。角色动作以骨骼为接口，不把情节或动作绑定到某款模型的对象名、服装菜单和 Avatar FX 控制器。

人物尚未分配固定的故事身份。`actor-a`、`actor-b` 是首批演出槽位，Kipfel、Nemesis 是当前外观，两者分别维护。用户确认人物数量可以增加，情节不受两名演员限制；同一外观可以用于多个独立演员实例。

2026-10-15 用户进一步确认「群像为主，几位人物反复出现」。具体人物建议见 [spec/12 的群像段落](12-creative-proposal-v3.md#已确认群像为主几位人物反复出现)：先以四位熟面孔的动作、习惯、重复场所与彼此回应形成记忆。四位设定仍为讨论稿，不将其与两款外观一一绑定；剧情人物身份、演员实例和外观分别管理。换外观时保留人物的动作习惯，多个独立人物共用外观时以站位、行动和可选配件区分。

用户随后提出主题可能偏移，四位人物设定与日常/告别微情节暂缓细化。群像怎样呈现共享与创造文化的变化，先按 `spec/12` 的主题校准重新评估；已导入的人物资产与骨骼接口继续作为可用制作条件。

用户已有动作捕捉设备，精细动作可以等用户后续有时间时录制。当前阶段继续推进情节、场面调度与占位动作，预留替换动捕的接口。

## 源版本

按工作场景的最后修改时间选择，而不是按原厂 Prefab 的时间选择：

| 当前外观 | Windows 源工程 | 最新工作场景 | 修改时间（北京时间） |
| --- | --- | --- | --- |
| Kipfel | `D:\Projects\VRChatProjects2\MyKipfel1.2.0` | `Assets/Avatas/Lite-2-Pink.unity` | 2026-09-28 02:23:48 |
| Nemesis | `D:\Projects\VRChatProjects2\Nemesis` | `Assets/Main-2.unity` | 2026-10-03 14:04:01 |

两工程均为 Unity 2022.3.22f1。源场景只读；在临时副本上处理 Modular Avatar / NDMF，再提取当前外观、蒙皮网格、材质、贴图、Humanoid Avatar 与骨架。导出与导入均通过明确指定工程的 Unity Pipeline 执行。

## 世界角色的边界

- 原始 Avatar 与世界中的演员是不同资产。演员预制件保留渲染与动画所需内容，移除 Avatar Descriptor、上传 ID、菜单、追踪、音频和玩具脚本等 Avatar 专用组件。
- 保留当前可见的服装和形态；服装合并在源工程完成，世界工程不依赖整套 Avatar 创作插件。
- Humanoid 骨骼映射是主要接口，头、手、脚和道具挂点由编辑器记录；动作不写死 `Armature/...` 路径。
- 根节点负责演员站位、朝向与移动，外观子节点保留原比例。更换外观后重新绑定并检查身高、脚底与手部道具位置。
- 手指、耳朵、尾巴、表情是可选扩展，不作为基础演出的硬依赖。原 Avatar 的 PhysBone、追踪和 FX 不会自动成为世界演员的功能。
- 多人同步沿用演出时间基准，由本地 Animator / 骨骼动作推算；不逐根同步骨骼。

## 动作制作与后续动捕

先确定每场人物的意图、站位、动作起止与关键接触时刻，用简单姿态、通用动作和移动轨迹验证叙事与音乐节奏；后续把关键表演替换为用户录制的动捕片段。动作片段独立于演员外观，角色根节点的场面调度也独立于身体动作。

- 动作按独立片段管理：准备、开始、关键动作、停留、收势。镜头与音乐引用动作事件及关键时刻，替换录制素材后可调整片段起点、裁切与过渡。
- 需要脚底固定、落座、打字、递交或共同操作道具的片段，预留脚、座椅、桌面和手部目标点；换人物或换动捕后校正接触与身高差。
- 多人互动可分角色分别录制，按统一空间基准与动作事件对齐。实际录制流程与格式在录制阶段根据设备确认。
- 优先录制决定情绪的少数动作，如递交后的停顿、无人回应时收回手、维护者停下工作、再次伸手接住。背景人群先复用基础动作，通过时序与姿态变化形成差异。
- 手指、表情与眼神的采集能力尚未确认，保留为可选细化内容。

上述是制作流程约定，具体场面及人物关系仍需继续讨论；尚未制作最终动捕素材或完整角色表演。

## 验证

导入后验证 Humanoid 有效性、必需骨骼、蒙皮引用、材质与着色器、缺失脚本及默认外观；用两种外观验证同一骨骼接口，并保存角色预览场景与截图。实际路径和验证结果记录到 `data/actors.json`，导出中间包与截图位于任务临时目录。

2026-10-15 已完成：

| 当前外观 | Humanoid 映射 | 三角面 | Renderer | 材质 | 贴图 |
| --- | --- | --- | --- | --- | --- |
| Kipfel | 51 | 45,721 | 10 | 8 | 14 |
| Nemesis | 53 | 83,496 | 12 | 11 | 10 |

- 纯外观预制件：`Assets/OldWebActors/Imported/{Kipfel,Nemesis}/`；带演出接口的预制件：`Assets/OldWebActors/Cast/Actor_A.prefab`、`Actor_B.prefab`。
- `OWActorRig` 记录角色槽位、当前外观、Animator、Humanoid 骨骼数组及头手脚引用；`GetBone(int)` 的索引采用 `HumanBodyBones`，不依赖模型骨骼名。
- 两角色共用 `Motion/ActorMotion.controller` 与 `ActorReach.anim`。编辑模式与 Play Mode 均验证右手发生有效位移；Udon 虚拟机分别执行 `ResetPreview` 成功，测试窗口无新增错误。
- 预览动作从导入模型的参考站姿生成，已校正脚底接地。预览场景为 `Assets/Scenes/OWM_Actors.unity`，不替换主场景或改动 Build Settings。
- 保留原角色使用的 lilToon 2.3.4，在世界工程的 `LocalPackages/jp.lilxyzw.liltoon` 保存独立副本，通过 Pipeline 的 Package Manager 命令接入。
- 原始工作场景 SHA-256 与导出前一致。为导出，在源工程安装了 Pipeline；Nemesis 另补齐了本地已有的 FBX 依赖。没有保存源工作场景。
- 已停止 Play Mode，保存演员预制件及预览场景。当前完成的是 Unity 内验证，尚未做 VRChat 客户端验证。

两个人物合计 129,217 三角面，已超过旧稿的“全场景 5 万面”假设。后续性能预算需把人物单独计入；本轮保留外观，未降低贴图或简化模型。

## 导出与复现

`AgentScripts/OWAvatarTransfer.cs` 在源工程通过 Pipeline `run_script` 执行：`Inspect` 定位场景中的 Avatar，`Export` 在隔离副本上烘焙并制作原生资产，`Repackage` 从已生成的资产重新导出。它使用 Unity AssetDatabase 创建新的 GUID 和依赖，不手工修改序列化文件。

`unity/AgentScripts/OWActorSetup.cs` 镜像到世界工程的 `AgentScripts/`，提供 `Import`、`InspectImported`、`BuildPreview`、`CalibratePreview`、`ValidateMotion`、`ValidateRuntime`、`Pose`、`FramePreview`。导入包事件及 Udon 编译均等待完成再进行绑定。

复用动作时，更换外观后需重新运行骨骼绑定并检查脚底与道具接触；`OWActorRig` 不会仅凭替换 `visualRoot` 字段自动重新绑定。
