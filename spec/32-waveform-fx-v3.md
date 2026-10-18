# 前三段第三版：真实四通道波形、音源、粒子与本地视图效果

本模块取代柱状频谱。四条可悬浮、弯曲、扭转和伸缩的示波器轨迹在常态前方互相编织；81.92—92.16 秒才展开为四声部环绕。真实声部的 AudioSource 与对应波形中心严格共用同一世界坐标，波形的沿线形变直接来自该声部真实 WAV。模块不移动玩家、头或摄像机。

事实源为本文件、`data/episode123-v3-waveforms.json`、`tools/prepare_episode123_v3_waveforms.py` 与 Linux 纯文本源码。生成 PNG 为可再生实现资料，保存在 `~/tmp/vrc-old-web-map/episode123-v3/wave-fx/`，不在远端另立创意文档。

## 真实数据与触发

输入为四条 `FC14_Mono_01..04.wav`，48 kHz、单声道、16 位 PCM、256 秒。只取本片 0—133.12 秒用于示波器；音源继续使用已有完整 WAV 和 Music 的共同 DSP 调度。

- 时间每秒 30 帧，共 3,995 行有效数据，图集 512×4096 RGBA32；R/G/B/A 分别装 CH01/02/03/04。alpha 是第四条声部的数据，禁止预乘、透明处理或丢弃。
- 每行显示真实 64 ms 窗口，六个原始采样做一次精确均值积分成为一个显示列，没有 FFT 柱或合成正弦替身。
- 每帧向前最多检查 1,600 个原始采样，寻找合格上升过零点；过零后 96 个采样内的正峰需达到局部峰值的 18%，避免细噪声造成不稳定触发。
- 使用示波器常见的 AC 耦合：触发探测及显示窗口去各自常量均值，真实 40 ms RMS 也去常量均值。现有音轨的整段 DC 校正会让无声片段保留恒定数字偏移，AC 耦合使这种片段显示真实听感对应的平线和零能量。没有逐帧幅度归一化，四声部的相对幅度保持原样。
- 编码 `round(signal×127)+128`；解码 `(texture×255−128)/127`。PNG 第一行对应 `v=1−(frame+0.5)/4096`。选择整行，不跨时间行插值，避免异相波形互相抵消。
- 四声部能量共用同一 98 百分位 RMS 参考值，只供发光与粒子强度，不改变轨迹的真实振幅关系。

生产命令：

```sh
python tools/prepare_episode123_v3_waveforms.py
```

输出 `waveform_rgba.png`、`metadata.json`、`waveform-contact.png` 到任务临时目录；机器可读 metadata 同步写入 `data/episode123-v3-waveforms.json`。metadata 记录原 WAV 与图集 SHA256，可追溯再生产。

## 空间与节奏

常态中心默认 `(0, 2.55, 5.8)`，轨迹长度约 5.8—7 米，四条声部保持约 0.13 米的纵深与高度错位，低频缓慢漂浮使它们看似编织成同一条信号。每条使用自己的真实声部、色彩、相位、弯曲与扭转。32 秒以后扩大漂浮与立体变形；约束段逐步变短、出现有限缺口。

81.92→84.48 秒使用 smoothstep 展开；84.48→89.60 秒维持环绕；89.60→92.16 秒合回。默认环绕中心 `(0,1.65,-1.4)`、半径 4.2 米、角速度 0.46 rad/s，四声部相差 90°。每条在展开后缩到约 2.8 米并朝环绕切向转动。声部位置直接设为同名轨迹根位置，空间化从 0.55 增至 1，Doppler 为零，不因运动造成音高变化。

`OWEpisodeMusic.externalVoicePositionDriver` 默认 false，仅 V3 Builder 启用；旧版的声部位置行为继续由原采样器驱动。外部开关只包住旧位置写入，调度、音量、音符和节奏逻辑照旧。

## 真正的 ParticleSystem

场景中持久化五套 ParticleSystem：四条声部各自一套信号火花，另有一套方形数据尘埃。所有系统非循环、不自动播放、不自动随机种子、`SimulationSpace.Local`；波形火花跟随对应波形锚点运动，粒子不会留在头部旁边或在迟到者处突然飞回旧世界位置。

声部发射曲线来自实际 AC RMS，并将现有真实音符表的每个起点烘成短脉冲：精确起点前 1 ms、起点、起点后 25/60/120/200/320 ms 均有曲线节点，普通起音发射积分约 3 粒、高潮约 7 粒，实际能量为零时不发。ParticleSystem 的 rateOverTime 使用 0—1 归一化时间，所有秒数除以 main.duration；节点显式使用线性切线。Unity 2022 真实验证确认 SetBursts 会截到 8 条，因此不用 Burst，全部 burstCount=0。声部粒径 0.045—0.1 米，最长寿命 1.75 秒、每套上限 220；尘埃归一化曲线为 12—28 粒/秒，最长 5.2 秒、上限 180。合计上限 1,060 个 billboard，使用独立自写加色特效 shader，不是把普通 cube 叫粒子。

运行时暂停系统的自动推进，用 30 Hz 绝对时间格采样。正常播放只推进新增格；倒退、首次采样和大于 0.35 秒跳跃时从零以固定模拟步重建，覆盖迟到与重播。`Seek(time)` 总会从零重建，供截图及确定性检查；长时间直接 seek 会有一次性的模拟成本，不会每帧重放整个片段。显式验证会比较相同绝对时间的粒子数量、位置、速度、随机种子和剩余寿命。

## 本地摄像机 shader

采用持久化的全屏四边形与 PC GrabPass shader，实际处理已经渲染的场景颜色：按段落变化的轻微 RGB 色散、扫描线、周边压暗、冷暖变化与去饱和；高潮有极轻径向折射，111.36 与 121.60 秒附近有短暂的丢行。没有常开强闪烁，也没有改变摄像机的投影、FOV、旋转或位置。

面片跟随本地 `TrackingDataType.Head`，只用于将剔除包围盒置于眼前及标记本地玩家的主要视图。shader 直接输出每眼独立 clip-space 全屏顶点，使用 `UNITY_VERTEX_OUTPUT_STEREO`、`UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX` 和屏幕空间纹理宏；艺术计算先在单眼 UV 空间完成，再回到打包采样坐标，采样夹在本眼范围，避免串眼。Multipass、double-wide 和 single-pass-instanced 都保留对应路径。

shader 同时检查 `_VRChatMirrorMode`、`_VRChatCameraMode`、相机到本地头部的距离（默认 ≤0.24 米）和视向余弦（≥0.78）。镜子、手持摄影机及旁观远端相机不显示效果。本地玩家自由转头和移动，效果层跟随视野；它并非锁头系统。

这是 PC 场景的 GrabPass 实现，尚未承诺 Quest/移动 GPU 路径。Shader 宏齐全和桌面预览不能代替真实 VR 双眼验证；最终需要主代理在实际 VRChat 渲染条件中检查双眼、镜面和摄影机排除。按本项目协作规范，本子任务没有操作远端 Unity。

## 施工接口与持久化

纯文本文件：

- `unity/Assets/OldWebMap/Scripts/OWV3Oscilloscope.cs`
- `unity/Assets/OldWebMap/Scripts/OWV3CameraFx.cs`
- `unity/Assets/OldWebMap/Shaders/OWV3Oscilloscope.shader`
- `unity/Assets/OldWebMap/Shaders/OWV3Particle.shader`
- `unity/Assets/OldWebMap/Shaders/OWV3CameraFx.shader`
- `unity/AgentScripts/OWEpisodeV3AudioFx.cs`

先把 PNG 复制至 `Assets/OldWebMap/Episode123/Revision3/Data/waveform_rgba.png`，metadata 复制至 `AgentScripts/data/episode123-v3-waveforms.json`，已有音频 metadata 复制至 `AgentScripts/data/episode123-audio.json`。复制外部资源后统一由 Pipeline 刷新导入；严禁自行写 `.meta` 或材质/网格 asset。

主代理统一编译后在已停止的 `OWM_Episode123_v3.unity` 调用 `OWEpisodeV3AudioFx.Build()`。该方法批量创建并持久化图集导入设置、一个波形 ribbon Mesh、一个屏幕 Mesh、七个 Material、五套粒子以及两个 UdonSharp 代理；全部放在 `OWEpisode123/Revision3AudioFx`，资产放 `Revision3/{Meshes,Materials,Data}`。最后 CopyProxyToUdon 并 MarkSceneDirty，由主 V3 Builder 统一保存场景。重复调用只重建本模块根节点，不碰其他 V3 模块。

运行时两个类均自行 `LateUpdate → director.Elapsed()`，主导演无需增加调用。离线调用各实例的 `Sample(time)`；需要严格从零粒子采样时调用 `osc.Seek(time)`。聚合入口 `OWEpisodeV3AudioFx.Sample(time)` 自动执行严格 seek。

## 验证入口与风险

`OWEpisodeV3AudioFx.Validate()` 应由 Pipeline 调用，报告写至 Windows 用户目录 `tmp/vrc-old-web-map/episode123-v3/wave-fx/audio-fx-validation.json`。它覆盖 12 个时间点，并断言：

- 四声部音源与各自波形中心位置误差 <0.1 mm；非高潮时间展开值为零。
- 同时间重复绝对 Seek 的粒子完整状态一致，且所有系统固定种子、Local 空间。
- Mesh 与 Material 是持久资产，五套 ParticleSystem 实际存在；三支 shader 无编译 Error。

额外入口 `OWEpisodeV3AudioFx.ParticleDensity()` 会在 6.4、43.52、86.4、112 秒报告每套实际粒子计数、模拟时间、归一化发射率、曲线节点范围和具体音符的起音前/峰值/衰减采样，并断言正常段与高潮的数量下限。离线寿命积分预估依次约为 `[7,27,8,8,48]`、`[19,12,28,36,63]`、`[41,30,20,71,96]`、`[16,36,12,0,29]`，这是诊断参考，不能代替 Unity 的实际计数与可见性检查。

必须补充的真实 Unity 检查：UdonSharp 的 `ParticleSystem.Simulate(float,bool,bool,bool)`、`Pause(bool)` 与材质参数调用可编译；四通道纹理导入后仍为 512×4096、线性、无 mip、无压缩且 alpha 保留；0、43.52、84.48、92.16、112、122 秒截图；真实 VRChat 双眼与镜子/摄影机排除。shader 错误会使 Build/Validate 失败，不应把纯文本静态检查报告成引擎验证通过。

可调参数集中在已持久化组件：`fusedCenter`、`orbitCenter`、`orbitRadius`、`masterGain`、`masterIntensity`；波形 Material 的 `_Width`、相机材质 `_ViewRadius`；粒子的寿命、数量、RMS 发射曲线保存在各 ParticleSystem 上。时间与图集维度必须跟 metadata 一致，修改时先改事实源并重跑生成器。

## 主集成实看调整

常态波形中心调整为 `(0,4.1,8.2)`，为主图保留阅读空间；高潮环绕不变。基础/高潮色散已减弱，片头三维点阵靠近，后续退到画面上缘。原人物材质保持，V3 使用独立材质副本限制过曝和全身发光。粒子数量以修正后的 Unity `ParticleDensity` 为验收，旧版“组件存在/重播一致”不足以证明密度正确。

最终环绕中心已对齐实际出生点为 `(0,1.65,-2.8)`。修正后Unity 86.4秒实际粒子计数34/31/23/57/94，四通道曲线6494/5266/5242/6517键，归一化0—1，无Burst截断；连续推进与绝对Seek在86.4秒得到同样计数，模拟时钟偏差0.00249秒。
