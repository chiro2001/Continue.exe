# 51 全片过载与失效模块

日期：2026-10-17。授权范围为以 V5 完成度延续全片。本模块覆盖 **133.12—186.88 秒**，承接 `handoff/02` 的场面 14—17 和 `spec/48` 的音乐与史料修正。此前 V5 源码和资源不修改。

## 演出内容

| 时窗 | 中央锚点与变化 | 周边层次 |
| --- | --- | --- |
| 133.12—143.36 | 原创共享文件夹召回前半的漫画、字幕、Avatar 工作流；可读的“翻译·校对·嵌字”署名牌于 135.68 秒开始下落 | 144 份副本在两侧和远景推进；每份原有署名牌依次脱落；32 个复制光标及点击脉冲持续工作 |
| 143.36—153.60 | 搜索框明确在找“那份可编辑的原文件”，结果几乎一样；中央尝试打开不同结果，得到等待图标 | 相似结果、重复消息和光标沿两侧压近；外围染成警示色；保留中央可读窗口和身体尺度参照 |
| 153.60—163.84 | 链接检查队列中，一个字幕工程缺少路径；159.36 秒重新连接成功，显示“预览已恢复／完成 1 项”，其余队列继续 | 维护工位有桌面、键盘、显示器支架和折臂灯；队列、旧社区版式与“沙发／顶／已阅”重复推进 |
| 163.84—166.40 | 队列冻结，屏幕降至 5.5%，工位几何降至 10%，保留暖灯 | 周围队列降至 1.8%；这里表达视觉密度撤去，不声称音乐静音 |
| 166.40—168.96 | 熟悉的社区布局返回，内容位置均为“加载中” | 队列从冻结时刻继续；光标仍只得到等待 |
| 168.96—173.44 | 文件还在，播放器没有响应 | 旧副本和加载卡片渐少 |
| 173.44—177.92 | 正文还在，图片没有回来 | 点击只留下沙漏／等待 |
| 177.92—182.40 | 熟悉的入口暂时无法访问，保留书签与重试 | 不画成永久消失；外围继续变暗 |
| 182.40—186.88 | 工程与资源仍在，需要重新导入、检查接口、适配 | 最后 1.28 秒中央屏幕略退远；近景于 186.88 秒撤去，交给主代理的骤空模块 |

四类失效各 4.48 秒，扣除淡入淡出仍有超过 3.68 秒的稳定阅读时长。中央界面只有场面内操作文字，不重复主调度的下方关键句。

## 素材与史料边界

- 两张新图集均为代码绘制的原创解释界面。它们表达一般使用条件，不冒充 Flash、天涯或某版 VRChat SDK 的事件复刻。
- 三张召回工作流直接合成自 `art/export/episode123-v4-workflows/workflow-{manga,subtitles,avatar}-3.png`，机器可读索引保留在 `data/fullfilm-overload.json`；原始来源与匿名约定继续沿用前半数据。
- 示例地址使用保留域名 `community.example`，无真实跳转；界面不出现受害者姓名、下载入口或破解功能。
- 本模块不控制音乐、示波器、tracker、greetz、人物、天穹或全局光照。163.84 秒的视觉抽空不以空 note-on 行推断静音。

## 文件归属与构建接口

本模块新增并独占以下文件，不修改其他模块：

- `unity/Assets/OldWebMap/Scripts/OWFullOverload.cs`
- `unity/AgentScripts/OWFullOverloadBuild.cs`
- `unity/Assets/OldWebMap/Shaders/OWFullOverloadSurface.shader`
- `unity/Assets/OldWebMap/Shaders/OWFullOverloadField.shader`
- `tools/draw_fullfilm_overload.py`
- `data/fullfilm-overload.json`
- `art/export/fullfilm/overload/overload-focus-atlas.png`
- `art/export/fullfilm/overload/overload-field-atlas.png`

主代理在 Pipeline 中统一导入并构建：

1. 新场景为 `Assets/Scenes/OWM_FullFilm_v1.unity`；场景中先有公共根 `FullFilm Continuation`，其世界位置／旋转／缩放均为单位变换。
2. 两张 PNG 复制到 `Assets/OldWebMap/FullFilm/Revision1/Overload/Textures/`，由 Pipeline 导入。无需复制 JSON 到 Unity；JSON 是本仓库的说明与索引，运行参数由 builder 明确写入。
3. 编译运行时和着色器后，通过 Pipeline 执行 `OWFullOverloadBuild.Build()`。它只重建 `FullFilm Continuation/FullFilm Overload` 及本模块资源，mark dirty，不调用场景保存。
4. 执行 `OWFullOverloadBuild.Validate()`。验证本模块的界外显隐、单场激活、29 个时点的前进与逆序 seek 一致性、无音源／碰撞体及中央阅读锥。
5. 主调度持有 `OldWebMap.OWFullOverload` 并调用 `Sample(float time)` 或 `Seek(float time)`。组件在始终激活的模块根，`autoSample=false`，`director=null`；`contentRoot` 自行控制时窗显隐。

`Build()` 不修改导演、主调度、音乐或任何旧场景对象。新材质和 Mesh 经 AssetDatabase 在 Pipeline 执行时创建，未手写任何 Unity 序列化文件。

## 空间与运行开销

- 固定参考眼 `(0, 1.35, -2.8)`，正面 `+Z`。中央屏幕 5.4 × 3.0375 m，基准中心 `(0, 2.2, 5.4)`；搜索段只沿 Z 轴缓慢压近 0.9 m。
- 副本阵列每组只有一个合并 Mesh 和材质；卡片、署名牌、光标与点击脉冲的元数据烘在 UV1／UV2／顶点色中。运行时不创建 GameObject、Mesh、材质或字符串。
- 每场 144 份卡片、32 个光标与 32 个点击脉冲。场面 14 另有 144 份署名牌；其他场面不创建多余署名几何。
- 最近阵列 Z = 4.5 m；最近且最大搜索卡片的内侧仍在参考眼约 25° 以外。中央至少保留 ±20° 阅读锥；不使用贴头画面、全视野验证墙或个人隔离。
- 不新增实时 Light、AudioSource、Collider 或粒子系统。暖灯使用有形灯架、灯泡与柔光面片。
- 焦点图集 2560 × 3600，9 帧；周边图集 2048 × 1280，16 格。导入使用 mipmap、Trilinear、CompressedHQ、4096 上限，保留 alpha。PNG 合计约 944 KB；实际显存与字形清晰度以 Unity 导入结果为准。

## 已完成的离线检查与集成后验收

离线可重复入口：

```text
python tools/draw_fullfilm_overload.py
python tools/draw_fullfilm_overload.py --check
```

已检查两张图集尺寸、9 个焦点帧、16 格周边素材、7 个阅读锚点、时间连续性、80 ms 网格、三张召回依赖存在和无历史事实声明。C# 经 tree-sitter 语法解析；主代理仍须进行真实 Unity／Udon 编译、shader 编译和场景截图。这些离线检查不代替 Unity 或头显验收。

本机界面接触表位于 `~/tmp/vrc-old-web-map/fullfilm-overload/contact.jpg`。推荐主代理批次截图时点为：`134.40, 136.96, 140.80, 145.92, 150.40, 156.16, 160.00, 165.12, 167.68, 171.20, 175.68, 180.16, 184.64, 186.80`。

集成时重点实看：中央作品和署名掉落能否回认、侧面压力是否足够、搜索文字未被卡片盖住、一次修复确实显出成功、冻结时暖灯可见、四类失效可分别读懂，以及 186.88 秒衔接没有近景残留。所有实际电脑的播放与参考采样须保持系统静音；本子任务未操作远端 Unity，未播放音频。
