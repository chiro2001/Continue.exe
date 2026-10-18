# 第三版多层蒙太奇与原生金属点阵

本模块为四通道波形之外的前方活动层：16—20 个持久化的原生窗口槽位，按具体声部音符编排多个窗口同时进入、移动、滚动、传递、操作并给出结果。画面不再依赖 V2 的四张图轮换。图片来源、具体事件时间与坐标由 `data/episode123-v3-score.json` 负责；本模块将其变成可采样、可寻址、可重播的场景内容。

## 数据合同

主代理生产如下 JSON，复制到远端 `AgentScripts/data/episode123-v3-score.json`：

```json
{
  "schema": "oldwebmap.v3.montage.1",
  "duration": 133.12,
  "slots": 20,
  "images": [
    {
      "id": "bbs_topic",
      "path": "Assets/OldWebMap/Episode123/Revision3/Images/bbs_topic.png",
      "width": 1280,
      "height": 960,
      "title": "论坛主题页 · 年代与来源",
      "kind": "historical",
      "category": "bbs"
    }
  ],
  "events": [
    {
      "slot": 0,
      "image": "bbs_topic",
      "start": 10.24,
      "end": 12.8,
      "from": [-2.0, 2.1, 6.8],
      "to": [-0.8, 2.4, 5.8],
      "width": 2.4,
      "height": 1.8,
      "motion": "scroll",
      "channel": 2,
      "label": "打开主题，寻找回复",
      "result": "已收到：可以正常使用"
    }
  ],
  "scenes": [
    {"start": 0, "end": 30.72, "title": "接入", "caption": "窗口与操作的共同语境"}
  ]
}
```

图像 id 为字符串，事件 image 对应同名 id。图片 width/height 为来源像素信息，事件 width/height 为实际米数。from/to 为世界坐标。事件按 slot、start 排序，同一槽位不得重叠；多个槽位的事件可以并行。所有 start 应由上层生成器对齐 `episode123-audio.json` 指定声部的具体音符，不由运行器生成均匀替代节拍。

motion 支持 `float`、`slide`、`tilt`、`depth`、`scroll`、`stamp`、`transfer`，也支持对应整数 0—6；channel 为 0—3，-1 表示无声部。result 可省略，默认使用 `label · 完成`，但正式制作应给出与操作对应的具体结果。图片 kind/category 保留在事实源，窗口标题使用 title；来源信息应放进 title 或场景 caption，避免由运动掩盖图片性质。

前方关注区建议 x±5 米、y0.4—4.8 米、z3.2—11 米，在主要演出段同时保持 4—10 个可辨认活动窗口。20 个槽位允许前后事件复用，不要求同时把 20 个窗口塞进正前方。主代理负责事件密度与主次，运行器不会静默删掉多余事件。

## 运行设计

`OWV3Montage` 自行 `LateUpdate → director.Elapsed()`，公开 `Sample(float)` 与严格寻址的 `Seek(float)`。主导演无需加调用。

每个槽位有独立的 first/count 范围和当前事件游标。普通播放只向前跨过该槽位已经发生的事件；首次采样、倒退或跳跃超过 0.5 秒时，对每个槽位二分查找。始终是 20 个槽位的固定循环，运行时不扫描千条事件，也不创建任何 GameObject、Mesh、Material 或 Texture。

仅事件变化时设置真实图片、标题、尺寸和按钮位置；活动窗口的位置、旋转、平滑进入/退出及指针按 30 Hz 更新。逐帧的音符游标也只前进；倒退时对各声部二分查找。窗口边缘和金属点阵的强调来自实际声部音符，不采用均匀拍替身。

每个窗口持久化：图片 Quad、带真实厚度的背板、蓝白标题栏、原生凸起指针、操作目标、来源标题与操作结果文字。每槽一个持久 Material，通过真实纹理引用切换图片，不做运行时材质实例化。图片维持内容本身，模拟滚动只在 motion=scroll 时裁取并推进阅读位置。光标从标题区域移到目标，抵达后把 label 改为具体 result，并用小幅按钮缩放表达落点反馈。

弹幕使用 12 个持久 TMP 对象，内容由事件 label/result 产生，以不小于 0.52 秒间隔挑选实际事件起点；每条在前方五条高度通道中穿过 3.6 秒。播放使用单向游标，seek 使用二分加最近 12 条重建，不扫描整个历史。

## 原生三维蓝白金属点阵

最终合并 Mesh 包含 512 个球形点、6,144 个顶点和 10,240 个三角形：经纬球面、白色赤道/经线、两条倾斜交叉轨道，以及具有前后厚度的 FC14 点阵文字。球面点有真实三维体积和法线，使用自写金属 shader 的蓝白漫反射、锐利高光与边缘光；没有用截图假装点阵或把大量独立 GameObject 留在场景中。6,144 顶点已由最终 Unity Build 报告确认。

点阵按三个轴旋转，实际四声部的最近音符起点控制轻微高光与尺度强调。主集成常态中心 `(0,3.6,9.8)`，片头靠近展开，随后退到上缘；高潮中心 `(-4.1,4.1,10.5)`，让给前方共同活动；出现时间为 0—30.72、81.92—92.16、128—133.12 秒。中心、高潮中心与尺度是组件公开字段，可在 Pipeline 中调整。

所有新 shader 都有实例/立体眼初始化宏，金属高光视线取当前眼的相机位置。窗口和原生物体作为真正世界几何，自然提供双眼视差；它们不跟随或限制玩家头姿。

## 持久化与集成

源码为 `OWV3Montage.cs`，构建入口 `OWEpisodeV3Montage.Build()`，离线聚合采样入口 `OWEpisodeV3Montage.Sample(float)`。新增 shader 为 `OWV3Window.shader` 与 `OWV3MetalMatrix.shader`。

主代理通过 Pipeline 在已停止的 `OWM_Episode123_v3.unity` 调用 Build。构建根为 `OWEpisode123/Revision3Montage`，重复构建只重建该根。Mesh、Material 与独立中文字库保存到 `Revision3/{Meshes,Materials,Fonts}`，字体从已有场景字体复制并补齐 score 和反馈字符。图片导入统一在线性尺寸上限 2048、sRGB、mip、双线性、Clamp 与高质量压缩下进行。

Builder 完成后 CopyProxyToUdon、SaveAssets、MarkSceneDirty，由主 V3 Builder 最终统一保存场景。Builder 不主动清理 V2：主代理应将 `director.montage=null`，并停用旧四图、focusWord、imageLabel、greeting、logoRoot 与 starLayers。否则旧 Montage 的 Sample 会重新打开原先图层。

## 验证与边界

`OWEpisodeV3Montage.Validate()` 用 15 个参考时间检查二分采样的活动窗口数是否与事件事实源一致；再以 0.1 秒步进全片，验证普通游标与独立事件计数一致，并断言稳态不执行二分。对 43.52 秒进行跨段反复 seek，检查可见窗口位置与缩放一致。报告同时列出持久化槽位、材质、原生 Mesh 顶点与 shader 编译消息。

报告位于 Windows 用户目录 `tmp/vrc-old-web-map/episode123-v3/montage/montage-validation.json`。本子任务只写纯文本，并在 Linux 对游标/二分的边界、空槽位和倒退逻辑做独立数值检查；真实 C#/Udon/Shader 编译、实际图片导入和视觉截图由主代理统一执行。不能据此声称引擎或 VR 画面验证已经通过。

建议最终截图检查 0、10.24、30.72、43.52、64、84.48、102.4、115.2、128 秒，确认同时发生的活动有主次、标题和结果可读、真实图片与原生指针层有明确区别、点阵从侧面有体积，且不会挡住关键人物和操作接触点。

## 主集成最终口径

实际素材355项、703个窗口事件（693个来自真实音符起点），触发间隔覆盖0.08—2.56秒的多种取值；切图不等于拍子。完整素材与使用位置见score与material-audit。点阵新增原生FC14立体字与片头近距展开，后退到上缘；实际网格规模以Unity最终报告为准。旧系统与原创操作图使用Point+mipmap，其他来源图按Bilinear+mipmap导入。独立Operations在10.24—20.48、40.96—61.44占中央，同期整图退到两侧与后方。
