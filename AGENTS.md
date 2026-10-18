# 项目协作说明

- 始终使用简体中文回复用户，除非用户要求其他语言。
- 可独立拆分的资料搜集、批量整理、重复检查和实现子任务可委派子代理。2026-10-18 起用户指定后续子代理统一使用 `gpt-6-luna`，推理强度 `max`（即 `gpt-6-luna[max]`，取代此前的模型约定）。主代理负责统筹、整合与结果检查；并行任务明确文件或职责归属，避免多个代理同时操作同一个 Unity 编辑器。
- 实现代理交付模型、贴图、静态构建资源与清楚的导入接口；主代理统一操作目标Unity编辑器、集成和实看验证。并行任务明确文件或职责归属，避免多人操作同一个Unity编辑器或抢占Windows截图窗口。
- 临时文件统一放在 `~/tmp/vrc-old-web-map/`，按任务建立子目录；Windows 侧放 `%USERPROFILE%\tmp\vrc-old-web-map\`。风格草图、对照图和临时预览也按此约定存放，用户可以直接查看。
- 本仓库（Linux 侧 `/home/chiro/projects/vrc-old-web-map`）是**创意与规格的事实源**：`spec/` 写立意、时间轴与实现方案，`data/` 放机器可读的歌词、引文、时间轴与地图节点数据。改创意先改这里。
- 对外世界名固定为 **Continue.exe**；发布简介以 `data/world-publication.json` 为准。仓库名和 Windows 工程路径仍保留 `vrc-old-web-map` 作为内部标识。
- Unity 工程在 Windows 主机 `192.168.101.5` 的 `D:\Projects\VRChatProjects2\vrc-old-web-map`，Unity 2022.3.22f1 + VRChat SDK3 Worlds + UdonSharp。远端工程只做**实现载体**，不另立创意文档。
- 进行 Unity 开发时必须阅读并遵循 [UNITY-GUIDE.md](UNITY-GUIDE.md)；操作 Unity 时使用 [.agents/skills/unity-pipeline/SKILL.md](.agents/skills/unity-pipeline/SKILL.md)。
- 通过 `python tools/unity_remote.py <Pipeline 命令及参数>` 驱动远端编辑器：SSH → PowerShell → `unity` CLI，脚本默认已锁定本项目的 `--project-path`。多个编辑器实例同时运行时必须显式指定目标，避免误操作别的工程。
- Unity 序列化内容（场景、Prefab、材质、ScriptableObject、AnimationClip、Timeline、导入设置）一律走 Pipeline；`.cs`、`.md`、`.json`、`.shader` 等纯文本可直接编辑，禁止手改 `.unity`/`.prefab`/`.mat`/`.asset`/`.meta`。
- 批量操作先攒齐再统一执行与验证，避免逐文件往返触发重复编译。做视觉改动时按批次截图留证。
- 改动收尾：停止 Play Mode、等编译结束、保存有意义的场景改动，再向用户汇报验证结果；不要保存或丢弃用户手上的临时改动。
- Linux 侧仓库使用 Git，远端为私有仓库 `chiro2001/vrc-old-web-map`，默认分支 `main`。Windows Unity 工程目前仍没有 Git；现有 `tools/sync_episode123*.py` / `sync_fullfilm.py` 只做脚本与数据文件同步，不能提供提交、分支、回滚或冲突合并。双端 Git 同步脚本尚未移植，不要假设 Windows 工程存在对应分支。
- 音乐与素材署名要求见 [spec/05-toolchain-and-m0.md](spec/05-toolchain-and-m0.md)：片内不出现注册机本体，不提供下载，不引导破解；涉及真实侵权事件只用公开报道，不点名受害者。
