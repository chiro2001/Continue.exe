# 世界地图 / World Map

一支以 Keil 注册机音乐为底、在 VRChat 里可漫游播放的 3D MV 世界。

当前制作版本为前三段「接入 → 繁荣 → 条件改变」V5，共133.12秒，场景 `Assets/Scenes/OWM_Episode123_v5.unity`。V5 在 V4 内容（374 项素材、580 次事件）之上完善质感与贯穿符号：段落色彩剧本、三层星场与稳定网格地面、焦点屏幕光、荧光示波器、tracker 谱面、cracktro 等待窗、greetz 署名环、8 句关键句与 75 句分期弹幕、年代拟音和人物近场，方案与实施记录见 [spec/42](spec/42-episode123-v5-look-and-throughline.md)；[V5 验收入口](/home/chiro/tmp/vrc-old-web-map/episode123-v5/review/index.html)。V4 及更早场景保留。

Linux 侧创意源与制作数据位于私有 GitHub 仓库 `chiro2001/vrc-old-web-map` 的 `main` 分支。Windows Unity 工程 `D:\Projects\VRChatProjects2\vrc-old-web-map` 目前尚未纳入 Git，仍由 `tools/sync_episode123*.py` / `sync_fullfilm.py` 按文件同步。仓库包含内部研究素材；公开或再分发前仍需按 [spec/05-toolchain-and-m0.md](spec/05-toolchain-and-m0.md) 逐项核对署名与使用权。

**交接：** 前三段 V5 的后续细节调整、完整成片剧本与制作规划见 [handoff/](handoff/README.md)，供接手代理继续。

**暂定片名：《世界地图 / World Map —— 共享精神的颂歌与安魂曲》**，副题「致所有失效的链接」。

已选择互联网与 VRChat 双线交织、群像为主。最新全片串联见 [spec/15](spec/15-integrated-show-outline.md)：把素材、音乐、剧情、情绪与 VR 演出按 4:16 七段组织，共享、创造、控制、维护与传承居于主线。结尾已选择“一起做下去”，关键词为“继续”，片尾采用“那就一起做下去。”。具体分场、其余歌词、史料入选与高潮窗口仍是讨论候选；[spec/12](spec/12-creative-proposal-v3.md) 保留主题复核与此前人物稿。

后段的因果方向：AI 降低部分理解、功能复刻与新作品制作的成本，社交媒体和 VRC 帮助发现同伴与共同试用；有效互助使接收者成为贡献者，新的创作者选择一起做下去。“留下”仍贯穿作品、知识与来源。具体成本范围与演出桥段继续核对和细化。

按用户后续补充，正在扩充软件、Windows UI、病毒历史、新闻、网络梗、汉化/字幕与 BT 传播文化，以及天涯、人人、微博等中文互联网公共生活，素材池见 [spec/10-cultural-material-pool.md](spec/10-cultural-material-pool.md)。

原定 BGM 已确认为 FC14 四通道模块，一轮 4:16；已选「空间变化为主，少量动态处理」，常态四声部在前方轻微分层、整体融合，只有高潮才明显展开。时间分析、试听布局与情绪建议见 [spec/14](spec/14-fc14-music-visual-score.md)。前三段新场景已接入四轨，旧 180 秒运行骨架单独保留。

主要画面量来自自绘界面、排版、插画、图标和短循环，放入分层三维面片，配合简单建模与现有环境/家具复用。制作家族、数量估算、各段组合和试制顺序见 [spec/17](spec/17-art-production-plan.md)；库存依据见 [spec/16](spec/16-asset-inventory.md)。前三段已制作 40 张可编辑 SVG 与 PNG，清单见 [data/episode123-art.json](data/episode123-art.json)。

美术与画面采用“旧内容按年代、设备与软件还原，轻度调色协调；前期屏幕/窗口承载，后期逐渐自由展开”，具体见 [spec/18](spec/18-visual-style-direction.md)。电脑、按键机、早期智能手机和浏览器窗口作为前期载体候选，空间展开后旧内容仍保留自己的字体、边框和像素结构。文字模式依实际年代配置，XP 的字体平滑/ClearType 与点阵表现分别处理。前轮[对照草图](/home/chiro/tmp/vrc-old-web-map/style-discussion-20261015/18-style-directions.png)只作构图与氛围留档，不是旧 UI 复原标准；临时样张继续放任务 tmp 目录。

三维场景按用户补充加入长段无界体验与一段空间关系超越，见 [spec/19](spec/19-spatial-scene-plan.md) 和[阶段预览](/home/chiro/tmp/vrc-old-web-map/spatial-plan-20261015/19-spatial-phases-v2.html)。前三段已实现房间外壳逐步退出、51.20—133.12 秒的无界内容场与 Hub 穿行。再兴段 215.04—235.52 秒的内外互换与远近重构仍为后续候选；结尾保留人尺度操作、外围仍无界。

连续空间演出新增推荐路线：人物保留独立参照，整体移动、缩放和旋转周围场景，形成原地进入与穿行的体验；稳定支撑、接触道具、主音乐与运动景物分别管理，见 [spec/20](spec/20-world-motion-performance.md) 和[参数示意页](/home/chiro/tmp/vrc-old-web-map/world-motion-20261016/20-world-motion.html)。摄像机画面覆盖保留为特定短段备选；示意页不是 Unity 或头显验证。

## 现在这个仓库里有什么

本地目录是**创意与规格的单一事实源**，远端 Unity 工程是**实现载体**。

| 路径 | 内容 |
| --- | --- |
| [spec/00-brief.md](spec/00-brief.md) | 立意、整体结构、形式选择与边界 |
| [spec/01-timeline.md](spec/01-timeline.md) | 旧原型 3 分钟时间轴，待按 FC14 实际结构重排 |
| [spec/02-lyrics-and-quotes.md](spec/02-lyrics-and-quotes.md) | 旧歌词与 greetz 留档、音源归属提示、真实引文与素材考证 |
| [spec/03-world-design.md](spec/03-world-design.md) | 3D 空间设计：动线、地图状态机、视觉与听觉语言 |
| [spec/04-unity-implementation.md](spec/04-unity-implementation.md) | Unity/UdonSharp 实现方案、性能预算、里程碑与验收 |
| [spec/05-toolchain-and-m0.md](spec/05-toolchain-and-m0.md) | M0 音源实证、模块渲染、可复用的既有工具链与署名规则 |
| [spec/06-build-sheet.md](spec/06-build-sheet.md) | 施工单：坐标尺度、节点与连线、17 个道具、相机路径、联调检查表 |
| [spec/07-characters.md](spec/07-characters.md) | Kipfel / Nemesis 的源版本、世界演员预制件与可替换 Humanoid 接口 |
| [spec/08-spatial-performance.md](spec/08-spatial-performance.md) | VR 空间演出：密度、尺度、视点变化、短暂分离与重聚，以及能力边界 |
| [spec/09-creative-proposal-v2.md](spec/09-creative-proposal-v2.md) | 前版讨论稿：人物接力、七场情节、情绪节奏、视觉与歌词方向 |
| [spec/10-cultural-material-pool.md](spec/10-cultural-material-pool.md) | 14 类互联网文化、中文社交、汉化/字幕、分发、事件与网络梗素材池 |
| [data/culture-materials.json](data/culture-materials.json) | 118 条素材候选、主题关系、画面构想与来源核对状态 |
| [spec/11-vrchat-community-history.md](spec/11-vrchat-community-history.md) | VRChat 社区史、旧空间/Avatar/梗、Mod/EAC、资产权益、社群风险与 AI |
| [data/vrchat-materials.json](data/vrchat-materials.json) | 59 条 VRChat 子素材、42 个来源、核对状态与未定位线索 |
| [spec/12-creative-proposal-v3.md](spec/12-creative-proposal-v3.md) | 主题复核与前轮留档：文化史群像、原八段情节与人物候选 |
| [spec/13-chinese-web-history.md](spec/13-chinese-web-history.md) | 中文互联网：天涯/BBS、校内/人人、博客、微博、兴趣社群与公共记忆 |
| [spec/14-fc14-music-visual-score.md](spec/14-fc14-music-visual-score.md) | FC14 音乐结构、三层可视化、节奏尺度与七段情绪建议 |
| [data/music-visual-plan.json](data/music-visual-plan.json) | 音源时间基准、声部接力、信号摘要与讨论用视觉计划 |
| [spec/15-integrated-show-outline.md](spec/15-integrated-show-outline.md) | 最新全片串联：七段演出、双线材料、连续场面、情绪、声画与歌词候选 |
| [spec/25-panorama-scene-treatment.md](spec/25-panorama-scene-treatment.md) | 用户认可回调方向后的 23 个连续场面与观众参与方案 |
| [spec/26-episode123-production.md](spec/26-episode123-production.md) | 前三段自主制作、六轮迭代、运行验证与验收入口 |
| [spec/27-episode123-review-and-revision.md](spec/27-episode123-review-and-revision.md) | 用户七条验收原话、前三段第二版优化与验证 |
| [spec/28-reference-video-style-study.md](spec/28-reference-video-style-study.md) | 原参考片下载、逐段风格与节奏分析 |
| [spec/29-historical-image-sources.md](spec/29-historical-image-sources.md) | 真实图像、原始出处与历史留存网站 |
| [spec/30](spec/30-episode123-v3-production.md)—[spec/41](spec/41-v4-focus-spacing-and-sound.md) | 前三段 V3/V4 的验收意见、制作与验证 |
| [spec/42](spec/42-episode123-v5-look-and-throughline.md) | 前三段 V5 方案：诊断、全片贯穿符号、实施范围与记录 |
| [spec/43](spec/43-v5-atmosphere.md)—[spec/47](spec/47-v5-cast.md) | V5 氛围、信号、身份、拟音、人物各模块说明 |
| [data/episode123-v5-production.json](data/episode123-v5-production.json) | V5 实际制作状态、验证指标和工件路径 |
| [data/episode123-production.json](data/episode123-production.json) | 前三段实际制作状态、验证指标和工件路径 |
| [data/show-outline.json](data/show-outline.json) | 串联讨论数据：材料引用、候选分场/高潮/歌词；不作为运行时间轴 |
| [spec/16-asset-inventory.md](spec/16-asset-inventory.md) | GamePC 本地可用素材盘点：建筑/场景、家具库、天空盒、世界功能件与道具 |
| [data/gamepc-assets.json](data/gamepc-assets.json) | 素材盘点的机器可读清单（路径、体积、内容、可用性结论） |
| [spec/17-art-production-plan.md](spec/17-art-production-plan.md) | 自绘内容、分层面片、简单建模、现有资产挑选与七段复用计划 |
| [spec/20-vfx-reference.md](spec/20-vfx-reference.md) | 特效参考：2026-10-16 新特效资产的技术拆解与我们的四个自写着色器 |
| [spec/21-head-camera-fx.md](spec/21-head-camera-fx.md) | 头部摄像机气氛 FX：头锁全屏层、参数、分段用法与验证记录 |
| [data/art-production-plan.json](data/art-production-plan.json) | 美术家族与数量估算、模型原型、库存引用、场景组合和首批试制计划 |
| [spec/18-visual-style-direction.md](spec/18-visual-style-direction.md) | 年代还原、文字栅格、轻度调色与从屏幕到自由空间的载体递进 |
| [data/visual-style-plan.json](data/visual-style-plan.json) | 已采用的风格原则、载体候选、递进方案及待定细节 |
| [spec/19-spatial-scene-plan.md](spec/19-spatial-scene-plan.md) | 无界空间、内外/远近重构、候选时窗、局部支撑与演后可达范围 |
| [data/spatial-scene-plan.json](data/spatial-scene-plan.json) | 场景模块、候选尺寸、七段调度、位置与既有素材引用，不是运行场景数据 |
| [spec/20-world-motion-performance.md](spec/20-world-motion-performance.md) | 人物稳定、世界运动的参照分组、接触道具、原生立体进入与音频关系 |
| [data/world-motion-plan.json](data/world-motion-plan.json) | 世界运动的分组、变换、演出用途、示意及原型检查计划 |
| [data/actors.json](data/actors.json) | 演出角色槽位、当前外观与源场景记录 |
| [data/timeline.json](data/timeline.json) | 旧 180 秒原型的机器可读运行时间轴 |
| [data/lyrics.json](data/lyrics.json) | 旧原型歌词与 greetz 名单，待按实际音源重排 |
| [data/quotes.json](data/quotes.json) | 引文与素材来源、引用状态 |
| [data/worlds.schema.json](data/worlds.schema.json) | 地图节点数据结构与示例 |
| [data/stage.json](data/stage.json) | 空间规格：7 个分区、22 个节点、21 条连线、17 个道具、12 个相机关键帧 |
| [data/labels.json](data/labels.json) | 全部上屏文字：自检文字墙、铭牌、名片卡、片尾署名 |
| [unity/](unity/README.md) | Unity 侧源码镜像、构建入口与场景结构（UdonSharp 运行时 + builder） |
| [tools/validate_spec.py](tools/validate_spec.py) | 规格自检：分段连续性、事件可解析、歌词归属、连线对端、预算核对 |
| [tools/scan_gamepc_assets.py](tools/scan_gamepc_assets.py) | 通过 SSH 盘点 GamePC 上的 Unity 资产包（只读，解包读 pathname） |

改完 `data/` 下的任一文件，跑一次自检：

```bash
python3 tools/validate_spec.py
```

## Unity 工程

- 远端工程（Windows 主机 `192.168.101.5`）：`D:\Projects\VRChatProjects2\vrc-old-web-map`
- 版本：Unity 2022.3.22f1 + VRChat SDK3 Worlds
- 操作链路与规范参考 `vrc-cpu-map` / `vrc-diffusion` 两个已有项目：SSH → PowerShell → `unity` CLI → Unity Pipeline。
- 创建场景、Prefab、材质、ScriptableObject 等序列化内容必须走 Pipeline；`spec/04` 里给出了操作清单。

## 当前验收与后续

先验收前三段的内容可读性、空间递进、音乐融合与演员调度。真实头显舒适度、多客户端网络体验和精细动捕仍需后续实测与录制；其余四段按全景稿继续制作。下列 180 秒骨架记录单独保留。

## 当前进度（第一版可运行骨架）

远端工程 `D:\Projects\VRChatProjects2\vrc-old-web-map` 已能进 Play Mode 跑完整条时间轴：

- 场景 `Assets/Scenes/OWM_Main.unity`：7 个分区沿 6 m 宽走廊铺开（−8 m → 150 m），22 个节点与 21 条连线按 `data/stage.json` 落位，节点名片文字取自 `data/labels.json`。
- UdonSharp 运行时在 `Assets/OldWebMap/Scripts/`，源码镜像与构建入口见 [unity/README.md](unity/README.md)。
- 已实测：段落推进与事件派发、13 句歌词逐字上屏、greetz 切换、分区信标点亮、片尾计数器归零。
- 待办：音乐源仍是 AudioSource 占位；17 个道具/特效与相机轨道未实现；节点六态目前是静态初始状态。

## 待你决定的问题

| 问题 | 选项 | 影响 |
| --- | --- | --- |
| 平台 | 仅 PC / 兼容 Quest | Quest 需要砍后处理、粒子与面数 |
| 演出方式 | 全自动播放 / 玩家漫游触发 / 两者结合 | 决定 Udon 同步与触发组件 |
| 放映模式 | 单人 / 多人同步 | 多人需要同步时钟与状态位 |
