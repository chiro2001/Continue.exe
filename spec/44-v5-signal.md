# 44 V5 信号层：荧光示波器与 tracker 谱面侧栏

承接 [42](42-episode123-v5-look-and-throughline.md) 第 3、4 项。V4 的 `OWV4Oscilloscope` 不改，仍驱动四条 PCM 波形的位置、材质参数与能量；本模块只换材质并新增两块物理谱面。新资源在 `Assets/OldWebMap/Episode123/Revision5/Signal/{Materials,Meshes,Textures}`，不改 Revision3/Revision4 资源。

## 荧光示波器

着色器 `OldWebMap/V5/Phosphor Oscilloscope`（`OWV5Phosphor.shader`）接受 V4 运行时每帧写入的全部属性（`_WaveAtlas,_Channel,_Color,_Row,_Length,_Amplitude,_Width,_Bend,_Curl,_Twist,_Timeline,_Energy,_Constrain,_Gain`），含义不变；`_Constrain` 的缺口/溶解、立体宏与 V3 相同。

- **余辉**：四个 pass。pass 0 画当前行；pass 1–3 画前 1/2/3 个图集帧。V4 写 `_Row = 1-(frame+.5)/atlasHeight`，更早的帧 V 更大，故余辉行 = `min(_Row + k·_RowStep, 1-.5/H)`（`_RowStep = 1/图集高度`，到第 0 帧为止）。每个余辉亮度乘 `_Persistence^k`（0.5），宽度乘 `1+0.2k`，热芯白化减半。
- **束斑**：沿 PCM 曲线用相邻列算屏幕法线，在裁剪空间偏移，粗细在陡峭斜率处不变薄；总半宽 = V3 的 1.6 倍（`_HaloWidth`），核心高斯 `_CoreK=7`，核心向白色混合 `_HotCore=0.55`，外围宽光晕。
- **束速亮度**：`rsqrt(1 + _SlewDim·斜率²)`，下限 `_SlewFloor`（0.03 / 0.35）。方波平台更亮、竖边变暗但仍可见。
- **混合与色调**：默认 `Blend One OneMinusSrcColor`（软加色，`_DstBlend=1` 即纯加色），输出经 `1-exp(-x)` 软限幅，余辉叠加和亮背景都不过曝。
- **四色**：CH1 P31 绿 (0.35,1.0,0.45)，CH2 青 (0.3,0.85,1.0)，CH3 琥珀 (1.0,0.7,0.25)，CH4 品红粉 (1.0,0.4,0.85)。
- 全部位移仍来自图集里的真实 PCM，没有合成波形。V4 带状网格每列一个采样（512 列），已满足图集分辨率，`BuildPhosphor` 仅在列数不足时才建更高分辨率网格。

`OWEpisodeV5Signal.BuildPhosphor()` 新建 4 个材质 `V5 Phosphor CH01..04`（贴图与当前数值取自 V4 材质，V4 材质不动），赋给 `osc.waveMaterials` 与 16 个 `regionalRenderers`（序号 `region*4+ch` 用通道 `ch`），并同步代理。

## tracker 谱面侧栏

两块物理面板在 `OWEpisode123/Revision4Performance/Revision5 Tracker` 下，不在 worldFrame 内，与音源一样固定。左板 CH1|CH2、右板 CH3|CH4（与 V4 历史窗声部左右一致）。中心 (∓5.0, 1.52, 6.2)，1.7 × 3.0 m，沿水平面朝向参考眼 (0,1.35,−2.8)（偏航约 29°）。从参考眼（FOV 52°，16:9）投影：NDC x ∓0.52…0.77、y −0.32…0.40，完全在画内；不碰焦点矩形（NDC |x|<0.28）；顶边低于弹幕带下缘 0.015 NDC；四角在开场房间体积内（墙 ±8.5、远墙 z=14.5），与房间道具（盆栽 (5.8,4.4)、书架 z=8.9）无交叠。

### 内容与音符命名

行内容 `RR|NNN II|NNN II`：RR 为序列内行号十六进制 `00`–`1F`，NNN 为音符名，II 为乐器号十六进制，空行 `--- --`。每 32 行（2.56 s，一个序列）有一条分隔线。全部 3200 行（256 s）都在图集里。

音符名来自 `tracks.csv` 的 `note` 字段（模块 pattern 字节 + 序列音符移调，已用 `tune.fc14` 独立解码核对：`note = raw + transpose`，乐器 = `info&0x3F + 乐器移调 + 1`）。名称 = `["C-","C#","D-","D#","E-","F-","F#","G-","G#","A-","A#","B-"][n%12]` + `n//12`，与 OpenMPT `Load_fc` 的显示（`NOTE_MIN + n`，八度从 0 起）和 ProTracker 周期标注（周期 856 = C-1）一致；实际音符 n = 0…46，最常见 F-2(29)、C-1(12)、F-0(5)、F-1(17)，是 F 小调的低音与五度。不确定处：绝对音高还取决于乐器波形长度，八度标号是 tracker 惯例，不是音乐会音高。效果列：`volume_slide` 与 `pitch_slide` 在全部 2953 个事件里都是 0，模块里也没有 pattern break（0x49）或 portamento 标志，所以没有效果列。FC14 空行不是静音，旧音保持到下一个事件。

### 图集

`art/export/episode123-v5/signal/tracker-atlas.png`，2048×808 RGBA32，线性数据，点过滤、无 mip、无压缩。一条条带 = 16 字符 × 8 px（128×8），每个图集行 16 条。条带号 = 行号>>1；偶数行 R=左板、G=右板，奇数行 B=左板、A=右板；条带 1600 为页眉（R、G），1601 为十六进制字体（R）。覆盖值就是亮度类别（音符 255、空 78、分隔 110、行号 140/200/255 表示普通/每 4 行/每 8 行）。字体为 5×7 点阵加粗一像素放进 8×8 格，字形在 `tools/prepare_episode123_v5_tracker.py` 内。机器可读数据 `data/episode123-v5-tracker.json`：四声部事件表 `[行,音符,乐器]`、片内 note-on 秒表、抽查行（含条带 CRC32）与图集 SHA256。

### 着色器与运行时

`OldWebMap/V5/Tracker Pattern`（`OWV5Tracker.shader`）：面板像素 144×254（字体像素），页眉 44 px（CHn 标签、十六进制序列号、每声部 12 段电平条），其下 21 行、每行 10 px；当前行居中，蓝色高亮条；`_RowPosition` 连续滚动；已播音符按声部色余辉衰减；`_Flash` 点亮高亮条下对应声部列；空格更暗；背景半透明（α≈0.55）加扫描线与慢带，时间只用 `_Timeline`。

运行时 `OWV5Tracker`（UdonSharp，时间的纯函数）：`_RowPosition = t/0.08`；`_Flash` = 各声部最近一次 note-on 起 `exp(-age/0.14)`（二分查烘焙的 CSV 时间）；`_Levels` = 最近 4 帧 30 Hz RMS 的峰值（权重 0.7 递减）；`_Opacity` 包络：0–30.72 s 1.0（前 0.4 s 淡入），30.72–79.36 0.28，79.36–92.16 1.0，92.16–122.88 0.28，122.88–133.12 0.6，最后 0.8 s 淡出，各段过渡自分界点起 1.28 s（smoothstep）。

## 构建与验证

顺序：把 PNG 复制到 `Revision5/Signal/Textures/`、JSON 复制到 `AgentScripts/data/` → 编译（`OWV5Tracker` 的 Udon 程序资产由 `RepairPrograms` 建）→ `OWEpisodeV5Signal.BuildPhosphor()` → `BuildTracker()` → `Validate()`（报告 `reports/signal-validate.json`）。

本地自检：`tools/prepare_episode123_v5_tracker.py` 生成后自动解码图集的全部 6400 条条带并与 CSV 逐项比对，同时与 FC14 模块独立解码核对。示波器与谱面的 CPU 近似预览在 `~/tmp/vrc-old-web-map/episode123-v5/signal/`。着色器、Udon 编译与真实双眼观感仍需主代理在 Unity 中确认。
