# 交接：前三段 V5 细节调整与完整成片

日期：2026-10-17。写给接手的实现代理。本文件夹只做交接与规划：创意的事实源仍是 `spec/` 与 `data/`；动手改创意前，先把结论写回对应 spec，再改数据和实现。

## 2026-10-17 后续制作更新

本文件夹保留原交接稿。全片后半已实现，最新制作状态、音乐事实修正、构建入口和验收证据以 [spec/49](../spec/49-fullfilm-production.md)、[production 数据](../data/fullfilm-v1-production.json) 及 spec/51—55 为准。下文“后半只有剧本”等为交接时历史状态；`full-film-beats.json` 不是当前运行数据，不能恢复其中由空音符行推断静音的描述。

## 当前状态（一句话）

前三段（原曲 0—133.12 秒）V5 已完成并通过编辑器内全部检查。用户看过后认为“效果还可以，细节还需要继续调整”。后半部（133.12—256 秒：过载、骤空、再兴、继续）只有剧本，没有场景实现。

## 阅读顺序

1. `AGENTS.md`、`UNITY-GUIDE.md`、`.agents/skills/unity-pipeline/SKILL.md`：协作与 Unity 操作规则。
2. 本文件夹：
   - [01-v5-adjustments.md](01-v5-adjustments.md)：前三段待调整细节清单，含位置、做法与验收；末尾留了用户补充栏。
   - [02-full-film-script.md](02-full-film-script.md)：完整成片剧本，256 秒共 7 段 24 个场面，逐场写清动作、画面、文字、声音和贯穿符号。
   - [03-production-plan.md](03-production-plan.md)：后半部制作规划，含模块、数据、素材、里程碑与验收。
   - [04-decisions-and-risks.md](04-decisions-and-risks.md)：需要用户拍板的问题，以及风险和实机验证项。
   - [05-pipeline-notes.md](05-pipeline-notes.md)：工程管线、构建顺序与踩过的坑。
   - [full-film-beats.json](full-film-beats.json)：剧本的机器可读草案，供生成数据时起步，不是运行时间轴。
3. 背景 spec：`spec/00`（立意）、`spec/15`（全片串联）、`spec/25`（23 场面）、`spec/14`（音乐结构），以及 `spec/42`—`spec/47`（V5 方案与各模块）。

## 已定共识（不要推翻）

- **形式与内容：** VRChat 世界里的实时 3D MV。互联网与 VRChat 两条文化史交织，群像为主，几位熟面孔可被回认。曾退回的 24 号方案（小作品/修门路线）不要恢复。
- **结尾：** 关键词“继续”，片尾句为“那就一起做下去。”。“留下”贯穿全片，“在一起”通过共同操作体现。
- **后段因果：** AI 降低理解、复刻和制作成本；社交媒体与 VRC 降低找到同伴的门槛；人的主动合作让帮助延续下去。
- **音乐：**
  - FC14 四通道，一轮 256 秒，保留原编配、音符、速度与音色。
  - 常态下四声部在前方融合，只有选定的高潮才展开。
  - 转场和旅行时真实音源位置不动。
- **美术：**
  - 旧内容按年代、设备与软件还原；前期由屏幕承载，后期自由展开。
  - 有一长段无边界空间，再兴段有一次空间关系的超越。
- **用户历次验收的硬要求：**
  - 每拍都有新东西，素材量要足。
  - 中央焦点要停留到看得清。
  - 外围随进度越来越包围。
  - 不出现给 Agent/审核看的说明文字。
  - 弹幕要符合年代。
  - 鼠标点击要落在真实界面上。
  - 世界整体一起移动。
  - 名牌全部显示 `xxxx`。
  - 素材按实际年代出现。
- **边界：**
  - 片内不出现注册机本体，不出现序列号/生成等功能元素，不引导破解。
  - 真实侵权事件只用公开报道，不点名受害者。
  - 不补造未定位的传闻。

## 子代理与操作规则摘要

- 自2026-10-18起，后续子代理统一使用 `gpt-6-luna`，推理强度 `max`（`gpt-6-luna[max]`），以 `AGENTS.md` 为准。实现代理只交付源码与构建入口；远端 Unity 只能由主代理操作，避免多人同时操作一个编辑器。
- 序列化资源（场景、材质、Prefab、导入设置）只走 Pipeline，禁止手改 `.unity/.mat/.asset/.meta`。
- 新版本一律从上一版保存的场景复制，用新的 `RevisionN/` 资源目录，不修改旧版资源：旧场景仍在引用它们。
- 收尾时停止 Play Mode、等编译结束、保存场景，并核对上一版场景字节未变。

## 关键路径

| 项 | 位置 |
| --- | --- |
| V5 场景 | 远端 `Assets/Scenes/OWM_Episode123_v5.unity`；资源在 `Assets/OldWebMap/Episode123/Revision5/` |
| V5 验收页 | `~/tmp/vrc-old-web-map/episode123-v5/review/index.html`（Windows：`%USERPROFILE%\tmp\vrc-old-web-map\episode123-v5\review\`） |
| V5 参考片 | `~/tmp/vrc-old-web-map/episode123-v5/review/episode123-v5-review.mp4`（1600×900，25 fps，135.04 秒） |
| V5 定稿帧与运行截图 | `~/tmp/vrc-old-web-map/episode123-v5/frames/`、`.../runtime/` |
| V5 报告 | `~/tmp/vrc-old-web-map/episode123-v5/reports/`（远端同名目录） |
| 制作状态 | `data/episode123-v5-production.json` |
| 构建脚本 | `unity/AgentScripts/OWEpisodeV5*.cs`；运行时 `unity/Assets/OldWebMap/Scripts/OWV5*.cs`；着色器 `unity/Assets/OldWebMap/Shaders/OWV5*.shader` |
| 工具 | `tools/*_episode123_v5*.py` |
