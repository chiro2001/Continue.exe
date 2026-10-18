# 59 全片 V2：实体制作台与可展开的同一世界

日期：2026-10-17。本文只规定并交付静态 Blender 模型、UV 与界面槽。动画、时钟、Unity 材质和场景持久化由主代理完成。

## 制作立意与这轮修正

197.12 秒后的再兴不再依靠几个人物循环打字与手势证明“在创作”。观众先看见本 MV 实际用过的 AI、素材编辑、Blender、Unity 制作痕迹，再看见同一份三维作品从编辑器屏幕内进入现实尺度。人物仅保留 VRC 社区场景中的必要存在；设备、素材的相互关系承担后段表达。

一个主制作台配两个侧层框，不复制成三台绿黑板。主屏放完整真实软件截图；侧层框放同一操作中可辨认的层级、素材或对话，时间轴低于主屏并有自己的薄实体背板。界面栏位必须来自实际截图裁切，不用生成式图片和虚构 Unity/Blender 文字。中性色与 UV 检测格只用来校对材质槽。

制作台具有显示器正反面壳、细倒角边框、VESA 板和四个固定点、散热槽、HDMI/USB 口形、铰链、支柱、加重脚、机械键盘键帽、触控板和触控笔、桌下线槽及少量走线。细节集中在近景会看见的边沿和接合处，避免用大量无意义散落物遮挡制作过程。

## 同一网格的世界

`fullfilm_v2_scene_block` 是可扩展作品的唯一几何源。Blender 实截打开这份 `.blend`，Unity 实截导入同目录 `.fbx`，微缩模型与展开后的世界实例化同一个资产，不另制作星形门户或不相关成果。

场景是原创的开放式小型社区场所：环形木平台、四段有靠背的环形坐席、一座穿过平台的小桥、真正具有内侧面和厚度的拱门、树形支撑的折叠遮棚、四盏路径灯和小型内容面板。它回应旧 Hub 的木构、公共坐席、中央树与入口记忆，但不声称是官方地图复刻。门洞、桥栏与树棚提供前后遮挡，缩小时有明确轮廓，放大后仍能从门洞走向内侧。

模型根在平台中心、地面高度附近；平台名义直径 7.8 m。统一根 `FFV2_SceneBlock` 可供整体缩放和旋转，内部语义组 `PlazaFoundation / RingSeating / Gateway / BridgeWalkway / BranchCanopy / CommunityPanel / PathLight_00…03` 保留。建议制作阶段先以 0.10—0.16 倍展示约 0.78—1.25 m 宽的微缩世界，再按主代理的实际空间与窗口关系扩展。0.02—12 倍只说明几何可统一变换；极小尺寸下的细栏杆和文字不作为必读内容，展开后的真实行走、遮挡和头显阅读由目标 Unity 验证。

## 交付与对象接口

Linux 事实源目录 `art/models/fullfilm-v2/`，Windows 同份产物在 `C:/Users/Chiro/tmp/vrc-old-web-map/fullfilm-v2/modeling/`。三套各提供 `.blend / .fbx / .glb`、对象清单和实际 Blender 离线预览图。

| 资产 | 网格数 | 三角形 | 用途 |
| --- | ---: | ---: | --- |
| `fullfilm_v2_workbench` | 51 | 19,538 | 1 主制作台、2 侧层框、主屏、时间轴、3 张独立实体缩略图 |
| `fullfilm_v2_scene_block` | 80 | 17,502 | 从微缩模型展开的同一世界 |
| `fullfilm_v2_hub_detail_kit` | 26 | 3,218 | 旧 Hub 的可选高收益结构补件 |

每个对象的父节点、局部 pivot、尺寸、旋转、顶点与三角形数、材质槽、UV 层都记录于 `data/fullfilm-v2-model-assets.json`。Blender 源采用米制、Z 向上、+Y 是屏幕正面；FBX 导出为 Y-up / -Z-forward，glTF 使用 Y-up。目标 Unity 必须以导入后的真实前向和尺寸核对；需要转向时调整资产实例根，不修改屏幕 UV 以补偿错误朝向。

主制作台根 `FFV2_Workbench`，主要可调节点：

| 节点 | pivot / 用法 |
| --- | --- |
| `DisplayRig_Main` | 主屏中心，Blender 局部 `(0, -0.10, 1.36)`；屏幕与边框联动锚点 |
| `LeftHierarchy`、`RightAssets` | 各自面板中心；实体框、背板、界面面可一起微移 |
| `Timeline` | 主显示器子节点，低于主屏；有独立厚度和主屏不同的材质槽 |
| `ThumbnailCard_00…02` | 三张独立薄背板，中心 pivot，可分别排列与替换图 |
| `Stylus_PivotNib` | 笔尖位置 pivot；静态源无动画 |
| `Keyboard`、`PenTablet`、`MonitorStand` | 各部件局部中心或底部锚点；无需拆开固定网格 |

所有静态几何都带 UV。主屏和各界面面使用准确 `ScreenshotUV`：0—1 完整矩形，无套用图集、透明假界面或文字烘焙；普通材质面使用 `UVMap` 或 `SurfaceUV`。相机和照明只保存在 `.blend` 用于源模型预览，不包含在 FBX/GLB 内。没有动画、骨骼、运行时代码、Udon、效果 Shader 或碰撞体。

## 真实软件截图槽

| 网格 | 材质槽 | 尺寸与贴图要求 |
| --- | --- | --- |
| `UI_Main_16x9` | `M_UI_MainScreenshot_SLOT` | 1.28×0.72 m；16:9 实截，1920×1080 或 2048×1152 |
| `UI_LeftHierarchy` | `M_UI_LeftHierarchy_SLOT` | 0.44×0.70 m；真实栏位裁切，勿拉伸完整截图 |
| `UI_RightAssets` | `M_UI_RightAssets_SLOT` | 0.46×0.70 m；真实栏位裁切 |
| `UI_Timeline` | `M_UI_Timeline_SLOT` | 1.20×0.14 m；真实时间轴裁切 |
| `UI_Thumbnail_00…02` | `M_UI_Thumbnail_SLOT` | 0.256×0.144 m；独立16:9正面，必要时可在 Unity 中分别创建材质 |
| `UI_WorldNotice`、`UI_HubSign` | `M_UI_Thumbnail_SLOT` | 世界内内容面板及 Hub 标牌；由主代理赋原创说明内容 |

当前导出不绑定真实截图。`art/export/fullfilm-v2-ui/uv-test-16x9-1920x1080.png` 明确写有 UV TEST，仅供验证方向、比例和边缘，不计为真实软件画面。主代理可为同一主屏按制作进展切换 AI/素材/Blender/Unity 实截，不必每个画面另造一台显示器。

## 旧 Hub 的可执行细化清单

优先保留既有 `OWEpisodeV4HubCast` 的年代空间与已完成主体，少量替换真正影响轮廓、厚度与人的停留方式的资源。具体历史布局仍以既有已核图证为准，以下原创补件不覆盖年代事实。

1. **入口接地与门洞厚度**：既有 Portal 表面不动，选 1—2 个最靠近主视线的入口，套用 `HubEntrance` 拱形结构、独立门槛与顶部分类牌。入口从平面变成具有内侧壁和石脚的体积；按原门洞宽高缩放根，面板贴图和传送功能保留既有逻辑。优先级最高。
2. **木柱接合**：2019 Gallery 靠近画面的两根柱可使用 `HubTimberPostDetail` 的石柱鞋、顶托、斜撑和金属箍。不要批量替换全环；远处现有柱足够，主视线边缘有 1—2 组清晰接合即可。
3. **坐席**：将一处近景纯圆柱木头座替换为 `HubConversationBench`，有四块座板、两根石支脚和双层靠背。保留篝火环与其他原木，增加“可以坐下来”的识别，而不是统一成新式长凳。
4. **牌面留白**：`UI_HubSign` 只提供平面与实体槽，不把所有入口都堆成文字墙。历史分类牌应复用已有文案；新世界面板应明确原创，不假称来自旧 Hub。
5. **低优先级**：树叶材质、更多随机石头、桌面零碎件、额外人形动作不在这轮补件内。已有旧 Hub 树与篝火等主体不由本模型工作替换。

补件套装根只是便于预览的横向摆放，主代理应分别实例化 `HubEntrance / HubCategorySign / HubConversationBench / HubTimberPostDetail` 到正确既有位置。不要把整个横排套装直接放进演出。

## 生成与验证记录

实际在 Windows `D:/Software/Blender 4.2/blender.exe`（4.2.0）以 factory-startup / background 执行 `tools/build_fullfilm_v2_blender.py`。源工程和导出均在新建目录，没有操作前台窗口、默认文件、Unity 编辑器或 Unity 序列化资产。

入口示例：

```powershell
& 'D:\Software\Blender 4.2\blender.exe' --factory-startup --background --python 'C:\Users\Chiro\tmp\vrc-old-web-map\fullfilm-v2\modeling\build_fullfilm_v2_blender.py' -- --output 'C:\Users\Chiro\tmp\vrc-old-web-map\fullfilm-v2\modeling' --render
```

三套 `*_preview.png` 是实际 Blender 几何离线渲染，已人工查看工作台和场景块。FBX 与 GLB 另在独立空场景重新导入，报告 `art/models/fullfilm-v2/export-reimport-validation.json`。Unity 导入、截图贴图绑定、目标场景实看及头显由主代理完成；本文不以 Blender 预览替代这些验证。
