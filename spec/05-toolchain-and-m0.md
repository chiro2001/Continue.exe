# 05 工具链与 M0 音源实证

## 需要先解决的事实问题（已实证，见文末）

参考片用的是 Quazar of Sanxion 1996 年 1 月的《Funky Stars》（XM 模块）。用户要的是「Keil 经典注册机音乐」。
实证结论：**两者不是同一首**（详见文末「实证结论」与 `audio/SOURCE.md`）。

```text
Keil 注册机（EDGE keygen_new2032.exe）
  └─ RT_RCDATA / TUNE  9,902 B  →  Future Composer 1.4 (FC14), 4 通道, 4:16.000, 循环

《Funky Stars》 Hybrid song 2:20 (XM)
  └─ 10 通道, 2:20.296, The Mod Archive 66187（候补/引用，不是注册机内曲目）
```

## 渲染与卡点

```text
keygen_new2032.exe
  └─ PE 资源解包 ──►  audio/keil-keygen-2008/tune.fc14      (原始模块，唯一事实源)
        ├─ FC14 解析 ──►  audio/keil-keygen-2008/tracks.csv (4 通道音符事件 + 绝对秒)
        ├─ 行号导出  ──►  audio/beats.json                  (小节/行号 → 秒，待生成)
        └─ libopenmpt（可选）──►  ~/tmp/.../fc14-renders/*.wav (试听/对拍，不入库)
```

不要用别人压好的 MP3/转录轨：会有音高与时长漂移，卡点会整体错位。

## 可复用的既有工具链

上一支片子（`/home/chiro/projects/vibe-video/WorldExcuteMeVideo-New`）里已经有这套流程的一半，直接复用思路与脚本：

| 能力 | 位置 |
| --- | --- |
| 逐帧渲染与分片并行 | `tools/render.py`、`tools/render_shards.py` |
| 音频主控审计 | `tools/audit_audio_master.py` |
| TTML 歌词解析与导出 | `tools/ttml_lyrics.py`（含 `test_ttml_lyrics.py`） |
| 4K 两阶段编码 | `tools/hevc_crf18_remote.py` |
| spec 文档约定 | `spec/00-format.md`、`spec/01-timeline.md` 等 |

本项目与其不同的一点：最终输出是 **VRChat 世界里的实时演出**，不是 MP4。所以渲染脚本只用于两类工作——生成贴图/缩略图素材，以及为对拍导出一版「参考影片」用于检查节奏。

## 署名与版权

- 片尾与世界的说明牌必须署名：音乐来源（2008 年 EDGE Keil 注册机内嵌 FC14 模块；报告记作曲 Rhino of Torment，原曲出自 Cytax《I.C.E. 2》磁盘杂志，曲名待查档）、画面模板作者（EDGE Cracking Group）。
- 若引用《Funky Stars》，单独标注 Quazar of Sanxion 与 FastTracker II（Triton），不与注册机原曲混写。
- 片内不出现注册机本体，不提供下载，不引导破解。
- 旧网页素材以本地重建为主；使用存档或截图时在片尾说明来源。

## 临时文件位置

- Linux 侧：`~/tmp/vrc-old-web-map/`
- Windows 侧：`%USERPROFILE%\tmp\vrc-old-web-map\`

注册机本体、渲染出的 WAV、贴图序列、对拍影片都放这里，不进仓库；
`audio/keil-keygen-2008/`（模块 + 音轨 CSV + 摘要，约 100 KB）与 `audio/SOURCE.md` 进仓库，
后续的 `beats.json` 也放 `audio/`。

## 实证结论（2026-10-15）

### 音乐：注册机内曲目为 FC14，一个循环 4:16，4 条音轨（已三重互证）

- 原程序 `keygen_new2032.exe`（EDGE，509,575 B，SHA-256 `216e3426…`）只有一首音乐资源
  `RT_RCDATA / TUNE` = **Future Composer 1.4（FC14）**，9,902 B，MD5 `08fc472649363a74f40c4a58df73174d`。
- 逻辑循环长度 **4:16.000**：100 段序列 × 32 行，每行 80 ms（速度 4，PAL 50 Hz），播完循环回开头。
  当前临时 libopenmpt WAV 文件为 256.100 秒，包含下一循环起点；逻辑循环与完整文件长度分别记录。
  4 条通道各自独立的 pattern 序列（0–40），即 4 条音轨。具体可视化与测量见 [spec/14](14-fc14-music-visual-score.md)。
- 解包方式：本仓库 `pe_extract_res.py` 解析 PE 资源表（只读），结果与两个第三方副本逐字节一致。
- 署名：反向工程报告记作曲 Rhino of Torment；移植项目注记原曲出自 Cytax 的 I.C.E. 2 磁盘杂志（曲名待查档）。

### 《Funky Stars》（XM）是另一首，不复用为「注册机原曲」

- `Hybrid song 2:20`，Quazar of Sanxion，1996-01，FastTracker 2 XM，2:20.296，10 通道，
  The Mod Archive 66187 与镜像两份 MD5 一致 = `4e7887bcd4495e8ad2147a17057fb94e`。
- 它属于「注册机音乐」题材合集里最出圈的一首，但不在这个注册机里；是否引用由创意决定，
  引用时单独署名，不与注册机原曲混为一谈。

### 可视化输入（音轨数据）

`audio/keil-keygen-2008/` 三个文件：

| 文件 | 内容 |
| --- | --- |
| `tune.fc14` | 从注册机解出的原始模块（原始程序输出，非录音） |
| `tracks.csv` | 4 通道逐音符事件：`channel,order,row,time_s,pattern,raw,note,instrument,volume_slide,pitch_slide`，共 2,953 条 |
| `summary.json` | 模块头部、时长、每通道 pattern 序列与校验值 |

WAV 渲染（试听/对拍用）只在 `~/tmp/vrc-old-web-map/keygen-music/fc14-renders/` 生成，不入库。

### 3D 画面绘制方法（EDGE 注册机）

取证对象：原程序 `keygen_new2032.exe` 的反向工程报告（函数地址表）+ 从原程序提取的
`render_data.h`（512 B 纹理 LUT、滚动字幕文本）+ ElvisBlue 归档的 EDGE 模板逆向复刻（MIT，2023-09）
与真机截图三方对照。效果为**纯 CPU 软件渲染**，不使用 DirectX/OpenGL：

1. 帧缓冲 360×200、32bpp；每帧清 0 → 绘制 → `StretchDIBits(SRCCOPY)` 贴进对话框（原程序 `RenderThreadProc @0x801F20`；模板 Main.asm:296 同构）。
2. 星场 `RenderStarfield @0x801940`（模板 `DrawStar`，Main.asm:437）：3 组各 2800 颗星，启动时 `rand()` 铺开；相机位置由多组不同频率正弦叠加驱动，越界按 2800 单位回绕；投影到 360×200，亮度随深度衰减。
3. 3D 主字 `Render3DScene @0x801BB0`（模板 `Draw_MainText`，Main.asm:981）：点阵字表（`*` 为实心格，列距 9px）；每个实心格画一个 7×7 纹理方块，位置叠加 (行, 列, 时间) 的正弦偏移形成 3D 摆动/透视；强度取 `0xAA − 偏移量`。
4. 方块光栅器 `DrawPixelBlock @0x801830`（模板 `@dumped__008016c0`，Main.asm:856）：软件纹理映射四边形，定点 UV 步进（512 定标、0.5 步长），带 360×200 视口裁剪；纹理取 512 B 内嵌 LUT（`texture_lut`），四次查表拼 32 位 ARGB（蓝白金属渐变/网格感）。
5. 滚动字幕 `RenderScrollText @0x801E50`（模板 `Draw_ScrollText`，Main.asm:660）+ 固定行 `ScreenText`（Keil 版为 `Keil Embedded Workbench`）。
6. 音频线程独立：`FC14_Init @0x803C30` / `FC14_Tick @0x804240` / `FC14_RenderAudio @0x804E30`，44100 Hz 单声道；与画面无关，可单独提取（见上）。
7. 复刻路径：Unity/VRChat 里用点云/粒子 + 深度亮度 shader 复刻星场；点阵字用 instancing quad 做正弦位移与深度阴影（或烘成位图再 shader 形变），4 条音轨驱动 4 组视觉参数；LUT 保持像素阶梯感；滚动字幕用 TMP + UV 滚动。参数可 1:1 对齐，不需要注册机本体。
