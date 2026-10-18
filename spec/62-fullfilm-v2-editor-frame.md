# 62 全片 V2：可进入的立体编辑器窗口框

日期：2026-10-17。只交付独立静态 Blender 模型与接口。主代理负责真实截图、Unity 导入材质、运动与实看。

## 几何与立意

编辑器窗口在末段扩大时，主视口撤去图像即可露出同一份真实三维世界。这个框提供可辨认的标题栏、工具栏、侧栏与时间轴外形，但没有假 Unity 按钮、伪造编辑器文字或生成式截图。

外框宽4.0 m、高2.6 m、最大前后厚度0.27 m。主开口精确3.2×1.8 m（16:9），其中心就是模型根原点。中央没有网格、玻璃面或透明替身；内侧壁、前唇、后壳和边沿均有实体厚度，经过观众时仍可见侧面。原生界面依靠真实截图贴片，不能把中性色预览当作已贴好的编辑器。

分层外壳采用深灰材质槽、细倒角、内陷面板、细轨槽、少量结构螺钉与背部接口凹槽。没有装饰性假按钮。底部时间轴背盒有三层细轨槽，正面由真实时间轴截图统一覆盖；侧栏与顶部可按各自根节点独立移开。

## 交付目录

Linux：`art/models/fullfilm-v2/editor-frame/`。

Windows：`C:/Users/Chiro/tmp/vrc-old-web-map/fullfilm-v2/editor-frame/`。

- `fullfilm_v2_editor_frame.blend`：Blender 4.2 静态源，含独立预览灯光/相机。
- `fullfilm_v2_editor_frame.fbx`、`.glb`：导入模型，不含预览相机与灯光。
- `fullfilm_v2_editor_frame_preview.png`：实际 Blender 离线渲染，已目视检查。
- `build_editor_frame.py`：同套静态几何的后台生成入口，不操作 Unity。
- `editor-frame-validation.json`：FBX/GLB 重导入、UV与开口检查。
- `fbx-metadata.json`：直接解析 FBX 二进制得到的单位、轴向与根变换，不是根据文件后缀猜测。

完整对象清单、局部 pivot、尺寸、面数、UV槽及 SHA-256 在 `data/fullfilm-v2-editor-frame.json`。共53个网格、4762个三角形。没有动画、骨骼、Udon、Shader 或碰撞体。

## 语义层级与开口

根 `FFV2_EditorFrame` 位于主开口中心。Blender 使用米制数值、Z向上、+Y是屏幕正面。

| 根或组 | Blender局部pivot | 用途 |
| --- | --- | --- |
| `Frame_Perimeter` | `(0,0,0)` | 外框、后沿结构 |
| `Viewport_InnerReveal` | `(0,0,0)` | 具有真实深度的主开口内框 |
| `TopToolbar_Rig` | `(0,0,1.135)` | 顶部标题与工具栏整体 |
| `LeftHierarchy_Rig` | `(-1.80,0,0)` | 左侧薄层，中心pivot |
| `RightInspector_Rig` | `(1.80,0,0)` | 右侧薄层，中心pivot |
| `BottomTimeline_Rig` | `(0,0,-1.105)` | 底时间轴盒与正面贴片 |
| `Frame_AssemblyDetails` | `(0,0,0)` | 固定点与背面接口槽 |
| `Viewport_16x9_Anchor` | `(0,0,0)` | 无网格的开口锚点 |

锚点四个无网格子节点为 `Viewport_TopLeft / TopRight / BottomLeft / BottomRight`，其位置是X=±1.6、Z=±0.9、Y=0.09。开口可将主代理的真实视口图或同一世界模型与之对齐；本交付不另放中心预览面，避免留下挡住几何的隐藏遮罩。

## 截图贴片接口

五个正面均为单面网格，Blender法线+Y，`ScreenshotUV`完整0—1。材质只保留中性颜色，不绑定图片。

| 网格 | 上级 | 局部位置 | 宽×高（m） | 材质槽 |
| --- | --- | --- | --- | --- |
| `UI_TopTitle` | `TopToolbar_Rig` | `(0,.070,.105)` | 3.80×.058 | `M_EF_UI_Title_SLOT` |
| `UI_TopToolbar` | `TopToolbar_Rig` | `(0,.082,-.030)` | 3.18×.150 | `M_EF_UI_Toolbar_SLOT` |
| `UI_LeftHierarchy` | `LeftHierarchy_Rig` | `(0,.069,0)` | .265×2.16 | `M_EF_UI_Hierarchy_SLOT` |
| `UI_RightInspector` | `RightInspector_Rig` | `(0,.069,0)` | .265×2.16 | `M_EF_UI_Inspector_SLOT` |
| `UI_BottomTimeline` | `BottomTimeline_Rig` | `(0,.088,0)` | 3.18×.267 | `M_EF_UI_Timeline_SLOT` |

主代理按真实截图的栏位裁切，保留原软件图标与按钮位置。窄侧栏槽可以只显示层级/属性的一部分，不要把完整Unity截图挤到侧栏中。界面面片与外壳分离，可以在Unity中分别赋材质或遮去；不得用重绘假按钮代替真实软件细节。

## FBX单位与轴向：本批及早先三套资产的实测修正

主代理在目标Unity发现桌板变为百分之一大小、Z仍向上。为此直接解析了 `fullfilm_v2_workbench.fbx` 和本框FBX；两者约定完全相同：

```text
FBX version: 7400
GlobalSettings.UnitScaleFactor = 1.0
UpAxis = 1, UpAxisSign = +1
FrontAxis = 2, FrontAxisSign = +1
FFV2_* root Lcl Rotation = (-90.000009, 0, 0)
FFV2_* root Lcl Scaling = (100, 100, 100)
```

子网格与子节点仍使用原来的米制数值与Blender局部轴。FBX文件的厘米单位与导出根的100倍缩放共同表达正确物理尺寸，根的−90°表达Z-up到Y-up的转换。根转换是交付接口的一部分。此前仅凭导出参数写“Y-up”不足以说明Unity实例根应如何保留，这里用原始元数据补齐。

**优先接口**：导入设 `globalScale=1`、`useFileScale=true`、`bakeAxisConversion=false`、`preserveHierarchy=true`，通过一个独立动画父节点实例化模型，保留FBX实例转换根实际读到的原始缩放和旋转。动画只改外包父节点，不在导入根上把局部缩放覆盖为作品尺度、或把局部旋转重设为identity。动画关闭、父节点scale=1时应量得外宽4.0、高2.6，开口3.2×1.8。

若目标Unity或既有集成已经剥除/归一化这一个转换根，主代理也可采用其已实测的兼容接口：`ModelImporter.globalScale=100`，内容层X旋转−90°，再用独立父节点承接作品缩放。**这两个接口互斥，只能保留一套100倍与−90°转换**；否则根保留后会再放大100倍或再旋转一次。实际采用哪种以目标Unity的根Transform及Renderer bounds为准，不仅看Inspector的Scale Factor。

与本批相同约定的早先文件：`fullfilm_v2_workbench / scene_block / hub_detail_kit`。校准桌板在作品父节点scale2.4时应宽5.808 m、深2.232 m、厚0.1344 m；若读到0.05808/0.02232/0.001344，则还缺100倍转换。此次不重导出或覆盖旧模型。

其他导入建议：不导入动画/相机/灯，导入已有法线，网格压缩先关闭，材质由主代理通过Pipeline创建并绑定。不要在未测量的情况下同时开启新的轴向烘焙和上述手动补偿。JSON中将这两种接口与适用条件分别记录，便于后续脚本明确选择。

## 实际验证与限制

在Windows `D:/Software/Blender 4.2/blender.exe`（4.2.0）后台factory-startup执行，未使用前台窗口或Unity编辑器。

FBX、GLB分别在空场景重导入，均53网格/4762三角形，所有网格有UV，五个UI矩形保留0—1完整UV，没有动画数据、相机或灯光。两份导入的边界约为Blender `(-2,-.154,-1.3)…(2,.116,1.3)`，对应4×.27×2.6 m。

对开口X取−1.59/0/1.59、Z取−.89/0/.89，从前向后发射九条射线，两种导入共18条均无阻挡，证明中心与内边附近没有意外填充面。它不能代替目标Unity的材质正反面、截图朝向和实际演出遮挡检查；这些由主代理统一验证。
