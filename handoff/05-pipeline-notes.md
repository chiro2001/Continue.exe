# 05 工程管线与踩坑记录

本文写给接手代理，记录 V5 实际跑通的流程与容易出错的地方。

## 环境

- **远端主机：** SSH 别名 `gpu5080`（经 `192.168.101.5` 跳板）。远端工程在 `D:/Projects/VRChatProjects2/vrc-old-web-map`，远端临时目录在 `C:\Users\Chiro\tmp\vrc-old-web-map\`。
- **驱动编辑器：** `python3 tools/unity_remote.py <pipeline 命令>`，已锁定本工程的 `--project-path`。辅助脚本 `~/tmp/vrc-old-web-map/episode123-v5/run.sh <File.cs> <Class.Entry>` 会打印结果或错误摘要。
- **本机 Python：** 用 `~/miniforge3/bin/python`，带 PIL、numpy、soundfile，没有 scipy。系统 `python3` 没有 PIL。
- **zsh 拆词：** Bash 工具实际跑在 zsh 下，`for e in "a b"; do $R $e; done` 里的 `$e` 不会按空格拆开。要么逐条调用，要么用数组。

## V5 构建顺序（已验证）

```
OWEpisodeV5Build.CreateScene          # 需在已保存、未修改的 V4 场景上运行；会备份 V4 并记录哈希
OWEpisodeV5Build.PrepareImport        # 暂停自动刷新
python3 tools/sync_episode123_v5.py   # 一次性复制构建脚本、数据、着色器、运行时代码和导出文件
OWEpisodeV5Build.ResumeImport         # 先 Refresh，再建文件夹（顺序不能反，见下）
unity command recompile_status        # 轮询，直到 completed
OWEpisodeV5Build.RepairPrograms       # 为 OWV5* 新建 UdonSharp 程序资产并编译
OWEpisodeV5Atmosphere.Build
OWEpisodeV5Signal.BuildPhosphor ; OWEpisodeV5Signal.BuildTracker
OWEpisodeV5Identity.BuildCracktro ; OWEpisodeV5Identity.BuildGreetz
OWEpisodeV5Build.ApplyLayout          # 必须在 BuildCracktro 之后（BuildCracktro 会把缩放重置为 1）
OWEpisodeV5Foley.Import ; OWEpisodeV5Foley.Build
OWEpisodeV5Cast.Apply
OWEpisodeV5Build.ApplyText
各模块 Validate                        # Atmosphere / Signal / Identity / Foley / Cast
OWEpisodeV5Build.FinalizeAndSave
OWEpisodeV5Build.TextAndSpaceChecks ; OWEpisodeV5Build.KeylineVisibilityChecks
editor_play  →  等约 25 秒  →  OWEpisodeV5Validation.InteractionChecks  →  OWEpisodeV5Validation.LiveValidation  →  editor_stop
OWEpisodeV5Build.CaptureAll ; OWEpisodeV5Build.StartMovie  →  轮询 MovieStatus（约 15 分钟）
~/miniforge3/bin/python tools/render_episode123_v5_audio.py      # 135.04 秒参考混音
python3 tools/encode_episode123_v5_movie.py                       # 远端 ffmpeg 编码并取回
OWEpisodeV5Build.SdkBuildOnly  →  轮询远端 reports/sdk-build.json（约 3—5 分钟）
OWEpisodeV5Build.InspectBuiltBundle ; OWEpisodeV5Build.ReopenCheck
~/miniforge3/bin/python tools/package_episode123_v5_review.py
```

## 踩过的坑

- **`run_script` 的命令行 30 秒超时：** 运行超过 30 秒的入口（LiveValidation、SdkBuildOnly 等），命令行会报 `timed out after 30000ms`，但任务会在 Unity 里继续执行。`--timeout_ms` 改变不了这个 30 秒。正确做法是轮询远端报告文件：`live-progress.json`、`sdk-build.json`、`movie-progress.json`。
- **SDK 构建期间编辑器无响应：** `editor_status` 可能不返回。等 `sdk-build.json` 出现即可。
- **文件夹创建顺序：** 先 `AssetDatabase.Refresh`，再 `AssetDatabase.CreateFolder`。如果文件夹已经由 scp 建在磁盘上、但还没被导入，直接 CreateFolder 可能会建出带编号后缀的重复文件夹。
- **复制即重编译：** 往 `Assets/` 复制 `.cs` 或 `.shader` 会触发导入和重编译，所以要成批复制。`AgentScripts/` 在 Assets 之外，可以随时复制，不触发编译。只改数据和构建脚本时，用单独的 scp，不要跑整套同步。
- **TMP 动态字体：** 域重载之后，`TryAddCharacters` 可能因为无法重新打开字体（`LoadFontFace`）而把所有字符都报为缺失，即使字形已经在字体里。`ApplyText` 的处理是：只补缺失的字形；确实需要新增又失败时，重建字体资产并重新指派。
- **共享资源：** V5 场景仍引用大量 Revision3/Revision4 资源，比如关键句底板材质和 Hub 字体。需要改动时，复制到新的 Revision 目录再在新场景里重新指派，绝不能改旧资源。
- **采样驱动：**
  - V4 模块由 `OWV4Presentation.LateUpdate` 统一驱动（`autoSample=false`）。
  - V5 模块自己用 `director.Elapsed()` 采样（`autoSample=true`）。
  - 离线 `Pose` 在 `OWEpisodeV5Build` 里按顺序调用：先 V4 模块，再用反射调用所有 `OWV5*` 的 `Seek/Sample`。
  - 新模块若依赖别的模块当帧的结果（比如窗口位置），要像氛围模块那样，从数据和公式自己算，不要读 Transform。
- **校验之间互相污染：** 校验入口会把场景摆到任意时刻。依赖场景姿态的校验必须先 `journey.Sample(0)`、`hub.Sample(0)`，拟音校验就因此失败过一次。
- **截图：** `CaptureSome` 的参数写法是 `--args '["label",[t1,t2]]'`。批量截图偶尔出现过异常帧（见 01 #2），单独重拍可以复核。
- **荧光着色器：** 材质缺少的新属性会使用 shader 默认值。`_NearFadeStart/End` 就是这样后加的，不需要重建材质，但调参写在 `OWEpisodeV5Signal.BuildPhosphor` 里，以便重跑。
- **133.12 秒硬编码：** `OWEpisodeDirector.cs`、`OWEpisodeMusic.cs`、`OWV4Montage.cs`、`OWV4Oscilloscope.cs`、`OWV5Atmosphere.cs`、`OWV5Greetz.cs`、`OWV5Tracker.cs` 等运行时脚本里有 133.12 的默认值或常量。做全片时，改成序列化字段，并在全片场景里赋值为 256，不要改变旧场景的行为。
- **示波器波形图集：** 现有图集 3995 帧 @30fps，只覆盖前 133 秒；全片需要重新生成。源分轨见 `~/tmp/vrc-old-web-map/episode123/audio/FC14_Mono_0*.wav`，四条都是完整的 256 秒。
- **子代理越界：** 人物子代理曾在本机 `~/tmp/vrc-old-web-map/episode123-v5/reports/` 写下离线模拟结果（`cast-*.json`）。主代理从远端取报告时会覆盖同名文件。看到报告先核对来源，以远端实际运行的报告为准。

## 验收页与工件

- `tools/package_episode123_v5_review.py` 要求 12 份报告全部 `success: true`，才会生成 `review/index.html`：参考片、V4/V5 同刻对照、定稿帧、运行截图和报告链接。
- Windows 侧的同名目录已经同步。
- 新版本可以复制这个脚本改路径。对照图通过 `nearest_v4()` 取最近时刻的旧帧；文件名不是时间的帧（`waiting`/`finished`）要跳过。
