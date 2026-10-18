# 全片 V5：树棚与 Room01 书架材质恢复

日期：2026-10-18。模型源和材质恢复资产独立生成，不操作 Unity Editor，不改写 V2/V3/V4 模型、Room01 FBX 或 `.blend`。主代理负责 V5 场景导入、实例替换、材质绑定及最终视角验证。

## 最终世界的树棚

原 V3 最终世界模型中的目标是 `BranchCanopy`，Blender 局部 pivot 为 `(0.95, -0.35, 0)`；`Canopy_Planter` 是它的同级对象。原方案用一根竖杆、从同一高度放射的枝条和扇形棚面，俯视像伞，近看难以分辨承重和分枝关系。

V5 只替换最终世界 `BranchCanopy` 下的静态几何。新独立模型根 `BranchCanopy_Refined` 位于原 pivot 的局部原点，边界约 `3.60 × 3.74 × 2.77 m`，Z 范围 `0.288–3.06 m`。现有花盆顶约 `0.32 m`，新主干从盆内起始，接近原屋棚高度与占地。保留既有花盆、广场、桥、座位、地板和碰撞，以及 QvPen、屏幕和编辑器锚点。

新轮廓是树形木构遮棚：收分的主干与四条短根脚支在旧花盆内；三根粗主枝在高处分成六根渐细副枝；六条屋面肋、三道环向横带和檐边承接六片带轻微冠瓣起伏的双面布面。叉接处有小型箍件，木、布、缝边和铜色连接件各有独立材质。主干、Y 形分叉和连续冠面共同说明它是带树形支撑的遮荫结构；不增加场景剧情。

模型由 Blender 4.2 后台生成，不含动画、Collider、相机或灯光。FBX、GLB 分别在新空场景重新导入核对，均为 75 个网格、16,128 个三角形、UV 齐全，无相机、灯光或动画。Blender local 用米、Z-up、`+Y` 朝前；模型 pivot 为 `(0,0,0)`，集成到原 `BranchCanopy` pivot 后不要再额外手动转轴或缩放。

## 书架贴图回归与精准恢复

V4 `OWFilmV4Build.IsolateMaterials()` 对场景内所有材质只用 `AssetDatabase.AssetPathToGUID(GetAssetPath(material))` 生成一个 `Inherited-{guid}.mat`。Room01 FBX 内多个 embedded material 是同一个 FBX 资产路径，虽材质 sub-asset 不同，GUID 相同；第一个创建的材质副本随后被所有这些引用共用。当前 V4 实测四个 `Living_Shelf` 的唯一 slot 0 都指向同一个 Revision4 材质，Standard Shader 的主贴图错误为 `book4.png`。十个复数 `Books` 实例的 slot 1、slot 2 也都落在这份 `book4.png` 材质；slot 0 的纸张槽独立。单数 `Book` 的 `book.mat` 和 `paper.mat` 来自独立外部 `.mat`，当前未见同类污染。

追加检查的 47 个 Renderer 报告确认另有 20 个 furniture slot 受同一 GUID 碰撞影响：`Desk_Lamp[1]` ×10、`Wooden_Table[0]` ×8、`LowTable_wood[0]` ×1、`Sofa[1]` ×1。V4 HubCast 构建记录与源 FBX 对照表明，四组污染槽对应两个 embedded material：

| Unity 网格槽 | 源 FBX 材质 | 精确源材质数据 |
| --- | --- | --- |
| `Desk_Lamp[1]`, `LowTable_wood[0]` | `wood.004` | Base Color `(0.800457835, 0.301460505, 0.072322369)`、无 Albedo 图；Metallic `0`、Specular IOR Level `0`；Wood032 NormalGL 与 Roughness，均为 2048²，Repeat UV |
| `Wooden_Table[0]`, `Sofa[1]` | `wood.001` | Wood021 Color、NormalGL、Roughness，均为 1024²；Metallic `0`、Specular IOR Level `0.5`；Repeat UV |

源 Normal Map 节点都使用 Tangent 空间、强度 `1.0`。wood.001 的 Base Color 由 Wood021 Color 图驱动；wood.004 使用无纹理的源 Base Color。Unity V5 材质由这些节点值和原始 packed images 恢复：wood.001 使用 Standard Metallic setup，wood.004 使用 Unity 内置 `Standard (Specular setup)` Shader，匹配源 Specular IOR Level `0`。绑定只改上表列出的 20 个 mesh/slot，不替换相邻 `Desk_Lamp` 白罩体、灯泡槽或 Sofa 的外部布料材质。

只读导入 Room01 源 FBX 后确认了准确映射：

| 网格 / 材质槽 | 源图 | Unity 原资源对应 | UV / 恢复方式 |
| --- | --- | --- | --- |
| `Living_Shelf`, slot 0 `shelf` | `Wood017_2K-JPG_Color.jpg`、`NormalGL.jpg`、`Roughness.jpg` | FBX 内嵌图片 | 活跃 UV `UVマップ` 范围 U `-2.303858–1.989196`、V `-1.003602–1.996399`；材质贴图必须 Repeat |
| `Books`, slot 0 `paper.001` | 无图 | 纸材质 | 保留原材质 |
| `Books`, slot 1 `book.008` | FBX 内嵌 `4.png` | `Assets/Room01/texture/book4.png` | UV 约 0–1；绑定 V5 自有 BookCover04 材质 |
| `Books`, slot 2 `book.009` | FBX 内嵌 `1.png` | `Assets/Room01/texture/book5.png` | UV 约 0–1；绑定 V5 自有 BookCover05 材质 |

两张书封面恢复图的 SHA-256 与 Windows Room01 原 `book4.png`、`book5.png` 完全相同。书架原始 Color、OpenGL Normal、Roughness 图从 FBX 打包图像原字节提取；另将 `1 - Roughness` 写入 Unity Standard Metallic/Smoothness 贴图的 Alpha。没有覆盖任何 Room01 源资产。

Editor 入口只创建 `Assets/OldWebMap/FullFilm/Revision5/ModelAssets/{Materials,Textures,Meshes}` 下的 V5 自有材质与资源。`RestoreShelfAndBooksMaterials()` 绑定所有已确认受影响的 `Living_Shelf[0]`、10 个复数 Books 的 `[1]=book4` 和 `[2]=book5`，保留 `[0]` 纸张槽且不触碰单数 `Book`。`RestoreRoom01WoodMaterials()` 只改上述 20 个家具槽，其他外部材质继续引用原资源。`PrepareCanopyImport()` 仅导入和配置独立 FBX；最终场景实例替换由主代理在 Pipeline 中完成。

## 资产与重建

- 构建入口：[`tools/build_fullfilm_v5_models.py`](../tools/build_fullfilm_v5_models.py)
- 静态模型与检查清单：[`art/models/fullfilm-v5/`](../art/models/fullfilm-v5/)
- 恢复贴图：[`art/export/fullfilm-v5/model-textures/`](../art/export/fullfilm-v5/model-textures/)
- 对象、材质槽、UV、SHA-256 和导入核对数据：[`data/fullfilm-v5-model-assets.json`](../data/fullfilm-v5-model-assets.json)
- Room01 家具材质源节点、原始 slot 映射、SHA-256 与恢复方式：[`data/fullfilm-v5-room01-material-recovery.json`](../data/fullfilm-v5-room01-material-recovery.json)
- Unity Editor 静态资源入口：[`unity/AgentScripts/OWFilmV5ModelBuild.cs`](../unity/AgentScripts/OWFilmV5ModelBuild.cs)

复现命令（须在已有 Room01 资源的 Windows 工程中运行）：

```powershell
blender --factory-startup --background --python tools/build_fullfilm_v5_models.py -- `
  --output art/models/fullfilm-v5 `
  --texture-output art/export/fullfilm-v5/model-textures `
  --shelf-source-fbx Assets/Room01/Scene/model/Room01.fbx `
  --manifest data/fullfilm-v5-model-assets.json --render
```

V5 Unity 场景整合、导入转换、原地画笔交互、矮 Avatar 实际遮挡、碰撞和播放过程中的最终视觉仍由主代理通过目标 Unity Pipeline 验证；本批离线预览不替代这些检查。
