# 20 特效参考：新一批资产的可复用技术与我们的着色器

盘点与拆解日期：2026-10-16。机器：GamePC（`192.168.101.5`，ssh 别名 `gpu5080`）。
编号说明：本稿原拟为 spec/17，与同日加入的 [17 美术制作计划](17-art-production-plan.md) 冲突，改挂 spec/20。

同日复核见 [22 新资产与 Shader 复核](22-new-assets-review.md)：确认家具重复包的 SHA-256，补充 Modern 包的网格/材质/贴图检查，并发现自写消融逻辑及立体适配待处理项。本文保留原拆解和前次编译记录；当前演出用途按 22 与全片串联稿执行，旧分区用途不自动恢复。

本文件记录 2026-10-16 新下载的一批资产里**与特效渲染有关**的部分：它们用了什么技术、
参数是什么、我们据此写了哪几个着色器，以及授权边界。

配套工件：

- 我们自己的着色器：[unity/Assets/OldWebMap/Shaders/](../unity/Assets/OldWebMap/Shaders/)
  （已导入远端 Unity 工程 `Assets/OldWebMap/Shaders/` 并确认编译通过）
- 抽取工具：[tools/extract_unitypackage_code.py](../tools/extract_unitypackage_code.py)
- 机器可读清单：[data/gamepc-assets.json](../data/gamepc-assets.json) 的 `vfxBatch20261016`

## 一、这批新资产

| 目录 | 体积 | 内容 | 与特效的关系 |
| --- | --- | --- | --- |
| `E:\BaiduNetdiskDownload\LH80\Vrchat插件 立体音乐魔法 ma导入\` | 16 MB | CooGee Works《立体音乐魔法》MA 导入版 | ★★★ 自带 2 个着色器 + 4 组粒子 + 噪声/渐变遮罩贴图 |
| `E:\BaiduNetdiskDownload\JJJ963\Field_of_stars_by_Floppiii_1.0.rar` | 12 MB | Floppiii《Field of Stars》星空特效 | ★★★ 自带 3 个着色器（含 MIT 屏幕空间 Ubershader）+ 8 组粒子 |
| `E:\BaiduNetdiskDownload\8540189【MA対応・武器召喚ギミック】ファントムワルツ\` | 13 MB | orinigi《Phantomwaltz》武器召唤 | ★★ 24 段 FX 动画 + 8 个音效 + 刀身网格，无自定义着色器 |
| `E:\BaiduNetdiskDownload\k787\Modern_Asset_Pack.zip` | 513 MB | Modern Asset Pack（FBX + blend + unitypackage + 贴图） | ★ 家具/装饰/植物材质，属环境资产 |
| `E:\BaiduNetdiskDownload\vrchat地图素材 家具合集 超多类型的家具\vrchat地图素材 家具合集 超多类型的家具\` | 939 MB | 与 10-15 已盘过的 `Furniture_Asset_Pack_2022_VRS.zip` SHA-256 一致 | 已核实重复 |

拆解方式：把 `.unitypackage` 当 gzip tar 解开，读每个资产目录里的 `pathname` 与 `asset`；
**没有运行任何可执行文件，也没有把资产导入 Unity**（我们的着色器是另写的，见第三节）。

## 二、从原资产里读到的做法

### 2.1 《立体音乐魔法》：加色中心光晕 + 噪声溶解

**`Add_CenterGlow.shader`**（Amplify Shader Editor 生成）做得是「能量核心」：

| 做法 | 参数 |
| --- | --- |
| 主贴图 UV 被一张法线流场推动 | `_DistortionSpeedXYPowerZ`、`_Flow` |
| 主贴图 × 噪声 × 顶点色 × 颜色 | `_SpeedMainTexUVNoiseZW`、`_Color` |
| 阈值式中心光晕：`mask × clamp(mask − (1−flow.z))` | `_Usecenterglow` 开关 |
| 输出 | `Emission = 2`，`Blend One One`，`ZWrite Off`，可选 `OneMinusSrcAlpha` |
| 软粒子 | `_InvFade` + 相机深度纹理 |

**`DissolveSphereField.shader`** 是完整的「溶解出现」：

| 环节 | 做法 | 参数 |
| --- | --- | --- |
| 体积感 | 菲涅尔 `pow(1 − dot(N,V), exp)` | `_MaskFresnelExp = 4` |
| 与场景相交软化 | 深度淡出 `clamp(1 − |sceneZ − partZ| / dist)` | `_DepthFadeDistance = 0.25` |
| 溶解噪声 | **三向投影（triplanar）**采样 + 随时间滚动 + 用第二张噪声扰动 | `_NoisetexTiling`、`_NoiseScrollSpeed = 0.25`、`_NoiseDistortionPower = 0.5` |
| 出现波前 | 物体局部 Y 归一化 + `_MaskAppearProgress` − 噪声，再查 ramp 贴图取本体与边缘 | `_MaskAppearLocalY`、`_MaskAppearRamp`（G=本体、R=边缘） |
| 输出 | `Emission = 颜色 × 功率 × clamp(不透明度 − 调整)`，`Alpha = 不透明度` | `_EmissionPower = 4` |

粒子部分（`Music Circle Particle.prefab`，4 个系统）：

| 系统 | 生命周期 | 初速 | 尺寸 | 发射率 | 上限 |
| --- | --- | --- | --- | --- | --- |
| Sparks | 1 s | 0 | 0.02 | 50/s | 1000 |
| Notes | 3.75 s | 0 | 1.2 | 0（脚本/动画触发） | 1000 |
| Music Circle Particle | 3.75 s | 0 | 1.2 | 0 | 1000 |
| Smoke | 1 s | 0 | 0.8 | 50/s | 1000 |

噪声与渐变遮罩贴图值得学：`NoiseClassic01`/`NoiseSmooth02`（颗粒）、
`RampMask03/10`、`RampComMask01`（溶解波前的软硬边）。
我们后面用自制噪声（Perlin/白噪声平铺 PNG）替代，避免分发他人贴图。

### 2.2 《Field of Stars》：三层粒子 + 屏幕故障

| 着色器 | 做法 | 备注 |
| --- | --- | --- |
| `AnimeParticlesV1.1.shader` | **三层贴图**各自滚动 + 旋转 + 各自扰动；每层可选「保留彩色 / 用亮度」；三层相加后乘遮罩（遮罩也能被第二张贴图扰动）；输出 = 三层和 × Additive × 遮罩 × 顶点色 α | 与我们写的 `OWAnimeParticle` 对应 |
| `Particles Additive Intensify.shader` | 最简加色：`tex × 顶点色 × _Glow`，`_Glow` 上限 100000 | 低消耗方案 |
| `ScreenSpace Ubershader 2.9` | **MIT 许可**（© 2019 Leviant）的屏幕空间大合集：故障、屏幕放大、模糊、色差、像素化、海报化、扫描线、HSV、抖动等 | 故障部分：按方块/扫描线分层哈希 → 周期强度（`glitchMul = pow(abs(sin(t·π/period)), 1/activeTime − 1)`）→ 位移/灰度/色偏/马赛克 |

粒子部分（`Field of Stars by Floppiii.prefab`，8 个系统，节选）：

| 系统 | 生命周期 | 尺寸 | 发射率 | 上限 | 说明 |
| --- | --- | --- | --- | --- | --- |
| Animator Field of Stars | 1 s | 15 | 600/s | 10 | 大尺寸、极短寿命 |
| Middle | 2 s | 0.05 | 20/s | 5555 | 星尘本体 |
| Screen FX | 1 s | 10 | 50/s | 1000 | 屏幕层 |
| Ring | 2 s | 0.5 | 25/s | 5555 | 环形 |
| Sound Field of Stars | 1 s | 8 | 200/s | 1000 | 音效触发 |
| flashes | 1 s | 90 | 5/s | 10 | 闪光 |

### 2.3 《Phantomwaltz》：动画编排参考

没有自定义着色器，价值在**时间轴与音效对点**：24 段 FX 动画
（`sword_L/R`、`rain_on/off/neutral/shocker`、`lunar_neutral/lunargram`、
`headbringer`、`twinbreak`、`roundtrip`、`reversetrip` 等）、8 段 WAV
（`PW_double_enable/disable`、`PW_headbringer`、`PW_lunargram`、`PW_rainshocker`、
`PW_roundtrip`、`PW_roundtripreverse`、`PW_twinbreak`）、刀身与斩击 FBX、
36 张贴图（slash/tear/flare/moya/noise/caust 系列）。
若要给演员做「召唤/收起」类动作，可以照它的动画命名与音画对齐方式来排。

## 三、我们自己的四个着色器（已编译通过）

位置：`unity/Assets/OldWebMap/Shaders/`（远端工程同名目录已导入）。
前次编辑器验证记录：`list_shaders` 显示四个都 `isSupported: true`，控制台 0 错误 0 警告。本次静态复核未重测；这些结果不代表头显双眼、目标平台或运行性能已验证。

| 文件 | 着色器名 | 对应的原做法 | 主要参数 |
| --- | --- | --- | --- |
| `OWAnimeParticle.shader` | `OldWebMap/Anime Particle (3 Layer)` | 三层粒子 + 扰流 + 遮罩 | `_Tex1..3`、`_Tex*Scroll`、`_Tex*Rotate`、`_Dist*`（法线贴图）、`_Mask`、`_Emission`、`_SrcBlend/_DstBlend`、`_Cull` |
| `OWNoiseDissolve.shader` | `OldWebMap/Noise Dissolve` | 三向投影噪声 + 菲涅尔 + 深度淡出 + 进度波前 | `_Progress` 原约定 −1 全显 → 1 全消，当前实现方向与端点不符，见 22；其余为 `_EdgeSoftness`、`_EdgeColor`、`_FresnelExp`、`_NoiseTiling/_NoiseScroll`、`_DEPTHFADE_ON`、`_HeightScale/_HeightOffset` |
| `OWAdditiveGlow.shader` | `OldWebMap/Additive Center Glow` | 流场扰动 + 噪声 + 中心光晕阈值 | `_MainScroll`、`_FlowPower`、`_UseCenterGlow`、`_CenterThreshold`、`_Emission` |
| `OWCrtGlitch.shader` | `OldWebMap/CRT Glitch` | 分块/分行哈希故障 + 扫描线 + 老屏 | `_BlockSize`、`_BlockAmount`、`_LineAmount`、`_Shift`、`_Chromatic`、`_PosterizeSteps`、`_ScanlineCount`、`_PeriodTime`、`_ActiveTime` |

与我们演出计划的对应关系：

| 着色器 | 用在哪 |
| --- | --- |
| `OWAnimeParticle` | 音符粒子与星场（[spec/14](14-fc14-music-visual-score.md) 的第 1 层「局部细节」）、案例墙与节点上的光点 |
| `OWNoiseDissolve` | 施工单里的 `despawn_effect`；节点六态中的「失效 / 归档 / 被摘除」；VRChat 城整片熄灭的柔和版本 |
| `OWAdditiveGlow` | 节点点亮、`scanline_row` 扫描成相时扫过的高光、希望之河的重亮点 |
| `OWCrtGlitch` | 失效链接的花屏、`marquee_select` 复制粘贴时的数据损坏、画面里的旧网页屏幕 |

实现细节（都写进了文件头注释）：

- 三层粒子用宏展开而不是把 `sampler2D` 传进函数，避免部分平台对 sampler 作参数的兼容问题。
- 溶解用 `COMPUTE_EYEDEPTH` + `LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(...))` 做软边；
  深度纹理需要相机开启 `depthTextureMode`，VRChat 世界默认不一定有，所以开关默认关闭。
- 故障效果用自写哈希（`OWMHash11`/`OWMHash21`），没有搬 MIT Ubershader 的代码，只借鉴分层思路。
- 四个着色器都支持 `_Emission` 与混合模式参数，方便按「PC 优先 / Quest 兼容」两档调。

## 四、授权与边界

- 《立体音乐魔法》（CooGee Works）、《Field of Stars》（Floppiii）、《Phantomwaltz》（orinigi）、
  `Modern Asset Pack` 都是**用户自购素材**，可以在本项目里使用与改造；
  但多数日系素材的利用规约**不允许再分发原始文件与源码**，因此：
  - 原资产文件只留在 GamePC 与 `~/tmp/`，不进仓库；
  - 仓库里只放我们自写的着色器与说明；
  - 若要直接用原资产（例如原粒子预制体），按规约在世界里使用，不随仓库外发。
- `ScreenSpace Ubershader 2.9` 是 MIT 许可（© 2019 Leviant，`github.com/Leviant/ScreenSpace_Ubershader`），
  可以合法复用，但需保留版权与许可声明；我们的 `OWCrtGlitch` 目前是重写版，未包含其代码。
- 片内不出现注册机本体、不提供下载、不引导破解（与前几节一致）。

## 五、后续可做

1. 用 `OWAnimeParticle` + `OWAdditiveGlow` 做一段「音符粒子随四通道跳动」的验证场景，
   参数按 [data/music-visual-plan.json](../data/music-visual-plan.json) 的声部分配来驱动。
2. 把 `OWNoiseDissolve` 接到 `OWStageBeacon` 的节点状态上，验证六态里的「失效」「被摘除」两态。
3. 需要原资产里的贴图（噪声、ramp、slash）时，先自制等价的 PNG 再进仓库，避免分发问题。
