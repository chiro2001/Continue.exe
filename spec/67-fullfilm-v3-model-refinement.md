# 67 全片 V3：可辨认用途的世界与低制作台

日期：2026-10-17。此批交付独立 Blender 静态源、FBX/GLB、UV、标牌 PNG 和碰撞尺寸，不操作 Unity。V2 源与导出均未覆盖。主代理统一绑定真实截图、用户作品、lilToon 材质与碰撞，并负责目标编辑器实看。

## 世界保持同一份作品

`FFV3_SceneBlock` 保留 V2 的 7.8 米平台、四段环形座席、贯穿桥、厚拱门与树形折棚；微缩作品和展开后的可进入世界必须使用这一份资产。用途通过结构细化和位置关系表达：拱门是社区入口，中央桥是通路，环席是交谈区，树棚提供遮荫，左侧告示区用于资料与来源，另一侧双幅展示架用于共享漫画/翻译及游戏/本地化作品。

这轮增加的都是能解释使用和承重的结构：桥下横向承梁、落地承座、端柱、两道扶栏和挡脚板；四段长凳改为三条有真实缝隙的座板，加入下靠背与端扶手；拱脚分缝带、门槛、顶部接合；树棚的承杆收在棚面下，补边条、放射支撑与根箍；告示架有石脚、斜撑和雨帽，作品架有展示轨与独立说明槽。中央桥两侧的缓坡连接高桥面与低环面，避免桥栏中段留口却无上下路径。

不添加虚构软件按钮、假作者名、密集散落物。新牌面仅说明公共用途；两张作品图由主代理绑定用户提供成图。

## 制作台尺寸与导入根

V2 桌板源顶高 0.795 米。V3 将整套设备先按 0.625 等比缩小，重新制作矮桌腿和支架，再单独降低屏幕与侧栏；截图网格与 `ScreenshotUV` 仍沿用原名。源桌板宽 1.5125 米、深 0.58125 米、厚 0.035 米。

本轮固定集成契约：**工作台外动画父节点 scale=2.4、世界 Y=0**。此前 V2 外节点 Y=-0.8 的补偿必须随新台一起改为 0。

| 部位 | 源 Z（米） | 演出高度（米） |
| --- | ---: | ---: |
| 桌板顶 | 0.25 | 0.60 |
| 主屏中心 | 0.47 | 1.128 |
| 主屏底 | 0.245 | 0.588 |
| 前裙时间轴中心 | 0.22 | 0.528 |
| 低位直立牌中心 | 0.345 | 0.828 |

演出桌板宽 3.63 米、深 1.395 米，主屏为 1.92×1.08 米。时间轴移至桌前沿立面，低位直立牌提供另一处近看内容。0.7、1.1、1.5 米相机眼高的离线视点已分别输出；0.7 米视点中主屏和时间轴完整露出。中性色 UI 只能验证网格遮挡，真实软件文字大小和头显阅读仍需 Unity 实看。

根为 `FFV3_Workbench / FFV3_SceneBlock`，源原点均 `(0,0,0)`，Blender 使用米、Z-up、+Y 面向观众。所有既有截图槽保留；新增 `UI_LowReadingCard`、入口/归档/作品牌也使用完整 0—1 UV。

FBX 二进制实测：7400、UnitScaleFactor=1、UpAxis=1、FrontAxis=2；两根 `Lcl Scaling=(100,100,100)`、`Lcl Rotation=(-90.000009,0,0)`。沿用 V2 优先接口：Unity `globalScale=1 / useFileScale=true / bakeAxisConversion=false / preserveHierarchy=true`，保留转换根的 100 和 -90°，动画仅改外父节点。不得把外父节点作品尺度覆盖到 FBX 转换根。无需对本批再叠加手工 100 倍或 -90° 补偿。

## 地板与座面碰撞接口

`data/fullfilm-v3-model-assets.json` 的 `collision_contract` 是碰撞尺寸事实源，提供源 Blender 坐标和已转换的 Unity 米制盒坐标。

| 表面 | 源顶 Z（米） | scale=1.6、根 Y=-0.5484 后世界顶 Y |
| --- | ---: | ---: |
| 中央桥 | 0.3115 | -0.05 |
| 环形平台木面 | 0.015 | -0.5244 |
| 四段座面 | 0.435 | 0.1476 |

主代理约定桥面与原观众地面 -0.05 米接平，故 `endRootY=-0.05-0.3115×1.6=-0.5484`。低环面比桥面低 0.4744 米；两条中央侧坡连接两者。座面相对低环面高 0.672 米，此值来自既有座席按世界演出统一放大，应按实际 avatar 体验调整是否需要坐姿 Station；本批只提供几何与站立/座面碰撞。

- `Plaza_UpperDeck` 和 `Bridge_AccessRamp_L / R` 可在实际网格对象上添加非凸静态 MeshCollider。
- 中央桥盒源中心 `(0,0.145,0.269)`、尺寸 `(1.46,6.28,0.085)`；Unity 米制中心 `(0,0.269,-0.145)`、尺寸 `(1.46,0.085,6.28)`。
- 每段座席拆为六个切线方向的薄盒，共 24 个；JSON 列出位置、尺寸和旋转，覆盖三条座板间的小缝隙。
- 米制盒应放在**外世界动画父节点下的 identity 碰撞组**，继承其 1.6 倍，不能把这些米制参数直接塞到 scale=100 的 FBX 根下。轴转换是 `(x,y,z)→(x,z,-y)`，盒尺寸是 `(x,z,y)`，源 Z 旋转对应 Unity Y 旋转。
- 仅最终可进入段启用地板与座面碰撞。栏杆、拱门、棚、显示器和其他演出几何无碰撞。

## 标牌与材质绑定

`art/export/fullfilm-v3-model-labels/` 使用现有安装字体 Noto Sans CJK SC Regular，以 Pillow 排版输出不透明 sRGB PNG。米灰底、深绿字、细边线和少量左标记回应实体指示牌，未生成图片或伪造编辑器 UI。

| PNG | 网格 | 正文 | 分辨率 |
| --- | --- | --- | --- |
| `community-sign.png` | `UI_CommunitySign` | 作品 / 留言 / 存档 | 1950×300 |
| `archive-label.png` | `UI_ArchiveLabel` | 资料与来源 | 1680×240 |
| `shared-work-credit-00.png` | `UI_SharedWorkCredit_00` | 漫画与翻译 | 2048×256 |
| `shared-work-credit-01.png` | `UI_SharedWorkCredit_01` | 游戏与本地化 | 2048×256 |

这些网格沿用 `M_UI_Thumbnail_SLOT`，绑定时要为每个网格分配独立材质，不能全局替换共享槽。`UI_SharedWork_00 / 01` 继续留给用户成图；`UI_WorldNotice` 留给主代理真实来源内容。清单、正文、长宽比和 SHA-256 写入模型 manifest 的 `semantic_labels`。

适合统一绑定项目现有 lilToon 的槽：`M_Walnut / M_Endgrain / M_CreamConcrete / M_Sandstone / M_Terracotta / M_CanopyLeaf / M_CanopySage / M_JadeAccent`。金属件可单独保留较低调的硬面材质。真实截图槽和标牌以可读性为先，避免将统一轮廓线或强阴影套到图文面上。材质创建与绑定均由主代理通过 Pipeline 完成。

## 交付与实际验证

模型目录：`art/models/fullfilm-v3/`；生成入口：`tools/build_fullfilm_v3_models.py`；完整对象清单与接口：`data/fullfilm-v3-model-assets.json`。Windows 对应目录：`C:/Users/Chiro/tmp/vrc-old-web-map/fullfilm-v3/modeling/`。

| 资产 | 网格 | 三角形 | 文件 |
| --- | ---: | ---: | --- |
| `fullfilm_v3_workbench` | 59 | 17,512 | `.blend / .fbx / .glb / *_objects.json / *_preview.png` |
| `fullfilm_v3_scene_block` | 214 | 33,522 | `.blend / .fbx / .glb / *_objects.json / *_preview.png` |

实际使用 Windows `D:/Software/Blender 4.2/blender.exe`（4.2.0）后台 factory-startup 生成、保存、离线渲染，再分别对两套 FBX/GLB 在空场景重导入。`export-reimport-validation.json` 四次均通过：网格数与三角形数一致，每个网格有 UV，所有 UI 面 UV 精确覆盖 0—1，无相机、灯光或动画数据；另直接解析两份 FBX 原始根元数据。

离线实看图包括模型全景、0.7/1.1/1.5 米工作台视点、桥上行走视点与归档侧视点，参数记录在 `offline-review-views.json`，重现入口为 `render-review.py`。所有预览均为 Blender 后台独立渲染，未操作 Unity 或抢占前台截图窗口。此验证不能代替目标 Unity 的根变换、材质反面、真实截图文字和最终碰撞行走检查。
