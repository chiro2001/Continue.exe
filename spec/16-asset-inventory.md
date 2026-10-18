# 16 GamePC 可用素材盘点（建筑/场景等）

2026-10-16 新批次与 Shader 复核见 [22](22-new-assets-review.md)，包含 Modern 包的可拆物件、几何/贴图统计，以及新旧 VRS 家具包的哈希去重结果。

盘点日期：2026-10-15。机器：GamePC（`192.168.101.5`，ssh 别名 `gpu5080`）。
范围：`D:\BaiduNetdiskDownload`、`E:\BaiduNetdiskDownload`（用户自购/付费下载，可直接使用）。
方法：目录递归扫描 + 解包 `.unitypackage`（即 gzip tar）读取内部 `pathname` 清单；**没有运行任何可执行文件**。
机器可读清单见 [data/gamepc-assets.json](../data/gamepc-assets.json)；重扫用 `python3 tools/scan_gamepc_assets.py list`。

使用方向已更新：画面主要由本项目自绘内容、三维面片与简单模型构成，下面的库存用于挑选环境、实体和功能支撑，具体家族与场景组合见 [17 美术制作计划](17-art-production-plan.md)。本文件的部分用途建议沿用早期施工单；当前剧情、片尾和空间调度以 `spec/15`、`spec/17` 为准。包内有文件或曾在其他工程导入，均不等于当前工程中的功能已验证。

## 一、结论先说

两个「网盘下载」目录里，E 盘是真正有货的一侧：

| 类别 | 找到 | 说明 |
| --- | --- | --- |
| 现成建筑/室内场景 | 1 个大的 + 1 个小的 | Room01 室内房间（775 MB）、LakeStar 小场景 |
| 景观/户外 | 1 个 | Okaeri 营地（帐篷、营地建筑） |
| 家具与道具库 | 1 个巨型包 | VRS Asset Pack 2022（1229 项，含 udon 脚本） |
| 天空与环境 | 2 个 | 55 Skybox pack（55 套天空盒）、城市夜景天空盒 zrain |
| 科幻装置 | 1 个 | ISOLATION-CUBE（封闭舱，材质/UI 齐全） |
| 世界功能件 | 11 项 | 照片墙 ×30、来访者信息板、人数计数器、视频/音乐播放器、钟、镜子、QvPen、亮度调节、Bakery |
| 风格/道具 | 约 25 项 | 全息/故障/磨砂玻璃等着色器，便签、可扔错误框、手机、扫描仪等 |

需要纠正一个预设：**名字像「学校」「花园」「夏日」的包其实是服装**——
`AoharuSchool_FullPack` 是校服、`xiari/清凉` 是夏日衣服、`LibraryGarden` 是 Lop 的衣装。
真正的建筑只有上表前两项与营地/隔离舱那几件。

## 二、可直接用作空间本体的asset

### Room01（室内房间场景）★ 首选

- 位置：`E:\BaiduNetdiskDownload\6370315 room1\Room_-01-\Room01.unitypackage`（775.2 MB，198 项）
- 内容：`Room01.unity` 场景、`Scene/model/Room01.fbx`、59 个材质、60 张贴图、烘焙好的 Lightmap 与 5 个 ReflectionProbe、`Post-process Volume Profile`、`Room01 Variant.prefab`
- 家具都在模型里：木地板/地毯/沙发/坐垫/书架/相框/吊灯/盆栽/冰箱/瓷杯等
- 依据：包内含 `SerializedUdonPrograms`，是**世界用**资产（不是 Avatar 场景）
- 交叉验证：同一套 Room01 已经导入过 `D:\Projects\VRChatProjects\GiantSleepWorld\Assets\Room01\`（目录结构、材质名一致），说明导入与本机可用性没问题，也可以参考那个工程的导入设置
- 用途建议：给「黄金段」或「归档」段做一个可以走进去的房间——旧电脑桌、堆叠的盒子、墙上挂着本片要用的截图，与 [03-world-design](03-world-design.md) 里「节点悬在格栅上」的主舞台形成室内/室外对比

### VRS Asset Pack 2022（家具包）★ 最有用

- 位置：`E:\BaiduNetdiskDownload\家具包\Furniture_Asset_Pack_2022_VRS.zip` → 内含 `Furniture_Asset_Pack_BOOTH_2022_VRS.unitypackage`（939.3 MB，1229 项）
- 内容：257 个 fbx、272 个 prefab、643 张贴图、22 段动画、16 组粒子、9 个 shader、3 段音频、**3 个 udon 脚本**、2 个场景
- 覆盖：椅/沙发/床/上下铺/吧台/吊灯/咖啡机/书堆/浴室用品/餐饮器具等
- 用途建议：七个分区的室内外细节全靠它；`udon scripts` 说明它自带可交互件（门/开关类），可省自研
- 注意：939 MB 导入后贴图内存很大，需按 [04-unity-implementation](04-unity-implementation.md) 的性能预算裁剪，只留用到的 prefab

### 55 Skybox pack + 城市夜景天空盒

- `E:\BaiduNetdiskDownload\地图基本功能合集-初音的VRC小店\天空盒贴图合集\天空盒贴图合集.rar` → `55 Skybox pack.unitypackage`（905.9 MB，443 项：55 材质 + 385 贴图）
- `E:\BaiduNetdiskDownload\4922509城市天空盒【Skybox】zrain\【Skybox】zrain.unitypackage`（62.8 MB，5 项，含 `night.mat` 与 `Fog.mat`）
- 用途建议：夜景城市天空盒直接对应「旧网页在深夜」的基调，优先用它；55 套里挑低对比度、低饱和的备用

### Okaeri Ultimate Camp（营地）

- 位置：`E:\BaiduNetdiskDownload\Okaeri_UltimateCamp_1.0\Okaeri_UltimateCamp_1.0.unitypackage`（91.6 MB，80 项）
- 内容：`Camp.fbx`、`Tent.fbx`、`Chair.fbx`、`Chop.fbx`、`Fork.fbx`、`UltimateCamp_Content.prefab`、`World.prefab` + 英/日说明书
- 用途建议：做「开源荒原」那一侧的开阔落脚点；也可以只取帐篷与餐具，避免整包风格抢戏

### ISOLATION-CUBE（隔离立方）

- 位置：`E:\BaiduNetdiskDownload\ISOLATION-CUBE(1)\ISOLATION-CUBE1_ver1.3.2.zip` → `ISOLATION-CUBE.unitypackage`（145 项）
- 内容：`isolation_cube.prefab`、`blackout/coldzone/heatzone/sky_cloaking/window/singularity_point` 材质、开机 UI 与准星贴图、`操作解説.mp4`、说明书 PDF
- 用途建议：现成的「封闭舱」——适合冷启动段落的开机自检，或把「被摘除/失效」的节点收进隔离舱作为视觉隐喻

## 三、世界功能件（直接对应施工单里的道具）

来自 `E:\BaiduNetdiskDownload\地图基本功能合集-初音的VRC小店\`（1.65 GB，92 个文件）：

| 资产 | 位置（相对该目录） | 对应 [06 施工单](06-build-sheet.md) 的 |
| --- | --- | --- |
| 照片墙 Photo Clip ×30 | `6608076照片墙 Photo Clip ×30\` | `scanline_row`（缩略图扫描成相）、案例墙 |
| 来访者信息板 v1.02 中文版 | `5403376来访者信息板\v1.02中文版\` | 名片卡、片尾展示 |
| 人数计数器 MIKI（SDK3） | `2588663人数计数器\` | `visitor_counter`（8 位数字归零） |
| 钟 AzhkwidClock v1.0.1 | `2225650钟\` | `date_banner`（「最后提交：四年前」） |
| VizVid 视频播放器 v1.5.2 | `5056077Vizvid视频播放器\` | 世界内的屏幕（放参考片/证据视频） |
| SVMusicPlayer | `2876742音乐播放器\` | 点唱机/曲目展示（正式 BGM 仍走 FC14 母带） |
| 镜子 3 件（通用镜/床镜） | `镜子3件\` | VRChat 早期 Hub 的 Avatar Hall 镜子 |
| QvPen v3.3.6 + 3 款皮肤 | `笔4件\` | 涂鸦板/留言墙（观众留字） |
| WorldLighting 亮度调节 | `4726955亮度调节\` | 暗场景可见度与玩家亮度开关 |
| 开关 Yodo_HapticController v2.4 | `开关\` | 交互反馈开关 |
| Bakery 烘焙器 v1.96 | `Bakery光照烘焙\` | 烘焙光照（编辑器工具，不进世界） |

其中 QvPen 与 VizVid 包内含 `SerializedUdonPrograms` / C# 源码，是世界用实现，可直接采用。

## 四、风格与道具（按主题契合度排序）

| 资产 | 位置 | 用法 |
| --- | --- | --- |
| **投げれるエラー**（可扔的错误框） | `E:\BaiduNetdiskDownload\投げれるエラーv1.0.1.rar` | 全片讲「失效的链接」，错误弹窗能直接扔出去——第二幕 A 的弹窗墙现成互动 |
| 假掉线系统 | `插件道具合集…\插件17件\假掉线系统\` | 「掉线/断网」的现成装置 |
| ArtBoardGimmick v1.12（可换图挂画） | `插件道具合集…\道具22件\5774152奇怪镜子\` | 任意图换进画框——挂旧网页截图、海报 |
| fusenmemo 便签 | `E:\BaiduNetdiskDownload\fusenmemo.unitypackage` | 帖子纸片，可铺成论坛墙面 |
| CyberGlitchParticles | `E:\BaiduNetdiskDownload\CyberGlitchParticles.zip` | 数据损坏/复制粘贴的故障粒子 |
| 全息 Hologram Shader V1.3 | `E:\BaiduNetdiskDownload\全息 着色器 Hologram Shader for VRChat\` | 点阵/扫描线全息态 |
| 磨砂玻璃 FrostedGlassShader | `E:\BaiduNetdiskDownload\3260764磨砂玻璃着色器…` | 旧 CRT/玻璃层，压掉现代感 |
| Touch Glow System | `E:\BaiduNetdiskDownload\Touch Glow System\` | 节点被触碰点亮，对应节点六态 |
| 卡带音乐播放器 / RK Player / SVMusicPlayer | `插件道具合集…\道具14件\8476612…`、`…\道具22件\6834553…` | 磁带意象回应模块音乐年代（三选一） |
| BloomPhone2 手机 | `E:\BaiduNetdiskDownload\BloomPhone2_ver1.0.1.zip` | 手机屏幕段落 |
| BT Odradek 扫描仪（含 12 段音效） | `E:\BaiduNetdiskDownload\BT Odradek_V1.1\` | 检索式道具，音效可作检索反馈 |
| DangerousBox 纸箱 / Time Bomb / 铁盆 / 小猪叠叠乐 / 消しゴム | 见 JSON 清单 | 杂物与喜剧节拍点（是否入片另议） |

## 五、不要用的部分

- `插件道具合集…\ERP用12件\`：成人向内容，全部排除。
- 各 Avatar 服饰/眼睛/发型包（Kipfel、Mamehinata、Nemesis 系列）：演员外观已定，除「服装作为历史服装参考」外不需要。
- 名字像场景但实为服装：`AoharuSchool_FullPack`（校服）、`xiari/清凉`（夏装）、`LibraryGarden`（Lop 衣装）。
- D 盘网盘的绝大多数内容是电子/嵌入式资料、乐谱、软件，与本片无关；`低预算VR.rar` 只是 vJoy/Trinus 等手机转 VR 方案，不含美术资产。

## 六、待确认

1. 标注「Avatar 向」的道具（投げれるエラー、fusenmemo、BloomPhone2、Odradek、DangerousBox、假掉线、Time Bomb、ArtBoard、VRoomba）需要在 Unity 里确认是 Udon 世界装置还是 Avatar 装置；若是 Avatar 件，要么改造，要么只借外观。
2. Room01 与家具包的体量（775 MB / 939 MB）要求先定性能预算与目标平台，再决定整包导入还是按需提取。
3. 授权：均为用户自购素材；正式发布前按各包 readme/利用规约确认是否允许在世界中公开展示（部分日系素材限个人使用）。
