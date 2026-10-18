# V3 用户图像接收、QvPen 与 FC14 尾段核验

记录日期：2026-10-17。素材接收与核对完成；Unity 导入和场景集成由主代理通过 Pipeline 处理。本记录不改用户原图、音轨、场景或材质。

## 用户图像

8 张 PNG 从 Windows `C:/Users/Chiro/tmp/vrc-old-web-map/fullfilm-v2/user-images/` 取回，并原字节复制到 [`art/source/fullfilm-v3-user-images/`](../art/source/fullfilm-v3-user-images/)。全部为 1672×941 RGB，远端 SHA-256 与本地副本相符；没有改写、压缩或裁切原图。文件逐张评审和像素坐标裁切建议见 [`data/fullfilm-v3-user-images.json`](../data/fullfilm-v3-user-images.json)，临时总览为 `~/tmp/vrc-old-web-map/fullfilm-v3/intake/contact-sheet.jpg`。

| Prompt 项 | 已收到画面 | 接收评审 |
| --- | --- | --- |
| v2-04 | 星空下原创漫画日文/中文对照 | 两格、人物动作、背景和对白泡位置与05连贯；日文“おはよう！/あの星を見に行こう。”及中文“早上好！/去看看那颗星吧。”清楚。建议按同一画格左右配对裁切。 |
| v2-05 | 同漫画无字清稿/中文嵌字 | 无字左栏与中文右栏保持相同构图和泡位，可将无字图作为底层，仅显现中文。裁切时排除顶部状态标签和中缝。 |
| v2-06 | 青色像素星六格动画板 | 3列×2行，星点贯穿六态，序列从星点、窗口构造到像素展开，再回到星点。每格左上编号需在动画帧中遮去；格线易分。 |
| v2-07 | 月下古桥原创游戏 CG 翻译对照 | 左右构图、人物与灯光一致，文字可读；是原创插画，不是真实软件或游戏截图。 |
| v2-08 | 夜航邮局原创主页 | 标题、菜单和更新03:14可读；整幅可用，也可单独裁菜单与中央插画。 |
| v2-09 | 反应贴纸 | 实际为4列×3行，12条文字可读。卡片有彩色底且无透明通道，按卡片矩形裁切，不按透明贴纸处理。 |
| v2-10 | 创作者桌面静物 | 漫画稿、译稿、键鼠、游戏手柄、纸模型和VR头显均可见；显示器内容是风景插画，不是软件UI。物件互相遮挡，优先整幅使用。 |
| v2-11 | 水上圆亭概念画 | 圆平台、拱、棚、弧形座席与桥等气氛相近，但桥的方向、拱门与棚顶位置、座席分段已偏离真实模型预览。只作情绪/美术概念，不作结构证据。 |

8 个文件的 SHA-256 均不同；04/05 是刻意成对的同源漫画，其他图没有精确重复。11 的真实结构参照仍是 `art/models/fullfilm-v2/fullfilm_v2_scene_block_preview.png`。

## QvPen 本地版本与官方后备

本地旧世界工程在 `D:/Projects/VRChatProjects/JustForSleep/Assets/QvPen/` 和 `D:/Projects/VRChatProjects/Stars/Assets/QvPen/` 各有一份完整 QvPen 3.3.6。两者项目使用 Unity 2022.3、VRChat SDK Base/Worlds 3.10.4、TextMesh Pro 2.1.6；与当前工程 Unity 2022.3、SDK 3.10.5 和 TMP 2.1.6 的版本差异很小。JustForSleep 版本包含木笔与笔架变体，故本轮按主代理决定优先使用此目录的木架版。

完整目录已按字节复制到 Windows `C:/Users/Chiro/tmp/vrc-old-web-map/fullfilm-v3/intake/local-qvpen/Assets/QvPen/` 与 Linux `/home/chiro/tmp/vrc-old-web-map/fullfilm-v3/intake/local-qvpen/Assets/QvPen/`。共217个文件、9,261,346字节，逐文件 SHA-256 和长度都匹配，原有 `.meta` 保留。Windows 副本的核对表为 `.../local-qvpen/source-copy-manifest.json`，Linux 副本的核对表为 `~/tmp/vrc-old-web-map/fullfilm-v3/intake/local-qvpen/source-copy-manifest.json`。主代理已通过 Unity Pipeline 导入这份全目录，报告116个GUID无冲突、重新编译无错误；Prefab已通过Pipeline实例化并使用低笔台；实际ClientSim手部输入拿笔、146点笔画、原版清除与重播丢笔/隐藏墨迹检查通过。该测试为单客户端，不宣称远端多人同步通过。这份临时副本供Pipeline检查，不是项目创意资源；旧世界工程未打开或改写。

可用 Prefab 与 GUID：

| Prefab | GUID |
| --- | --- |
| `Assets/QvPen/QvPen.prefab` | `2674f268860583949b53b63df24489ea` |
| `Assets/QvPen/QvPen(grad).prefab` | `5bca603d49548cc4d9a3329a36716749` |
| `Assets/QvPen/QvPen_WOOD.prefab` | `bc94d81522e742e4b8fe409d02b3acb7` |
| `Assets/QvPen/QvPen_WOOD_PenStand.prefab` | `f4351ff05524c30418f4c4896b489928` |
| `Assets/QvPen/Prefab/PenManager.prefab` | `c055c7dd62f8a8044bf2a4f8666fa699` |
| `Assets/QvPen/Prefab/EraserManager.prefab` | `d68f67d369d908d429c189ca9fe652a7` |

木架 Prefab 引用的两个 FBX、两份木材质及六张 BaseMap/Normal/MaskMap 贴图都在 `Assets/QvPen/` 内；材质使用 Unity 内建 Standard shader，没有 lilToon 依赖。Prefabs 的外部脚本和序列化程序 GUID 在该目录及 JustForSleep 的902个 `Assets/*.meta` 中找不到，完整 GUID 清单、已查范围和未确认项见 [`data/fullfilm-v3-local-assets.json`](../data/fullfilm-v3-local-assets.json)。这些引用可能由 VRChat/Udon 包或生成的 Udon 程序提供；本轮没有扫描 Library/PackageCache，所以不补造其物理文件路径，主代理导入后需由 Pipeline 确认解析。

木架 Prefab 在旧工程中的根 pose 是 `(-8.019948, 1.4180231, -0.34)`、单位旋转、单位缩放，这是原场景坐标。新场景应移动/旋转整个 Prefab 根节点到笔架目标点，保留内部笔尖、模型和按钮的相对位姿。主 `QvPen.prefab` 根 pose 为 `(0, 0.8, 0)`、单位旋转和缩放。旧目录没有 README、LICENSE 或 license 字段；遵守作者 BOOTH 条款与官方 VPM 获取方式，不转发或再分发包文件。

官方后备已只读下载到 `~/tmp/vrc-old-web-map/fullfilm-v3/intake/vendor/net.ureishi.qvpen-3.3.15.zip`，未导入 Unity。官方 VPM repo 索引列出稳定版 `net.ureishi.qvpen` 3.3.15，Unity 2022.3，`com.vrchat.worlds ^3.5.0`；当前 SDK 满足该依赖。索引给出发行 URL，没有 SHA 字段；本地包 SHA-256 为 `220df71a5fb5540ac7c9d75a7f40b57d69aa6aa432db3ddbc7563e0d60606537`。包内路径是 `Packages/net.ureishi.qvpen/QvPen.prefab`。旧版木架变体与新版功能包分开保留，不混拷 compiled/Udon 资产。

QvPen 还在 `GiantSleepWorld/Assets/Ichigo Lab/ARI_QVPen_usa/` 找到艺术模型包，目录内没有 C# 脚本；它不是 QvPen runtime。Downloads 中名为 `3500.unitypackage` 的小包只含 `Assets/NotoSansSC SDF.asset`，并非 QvPen。更广的用户目录和项目扫描范围及 lilToon 路径/Shader 线索详见本地资产 JSON。

## lilToon 线索

当前工程可用包目录为 `D:/Projects/VRChatProjects2/vrc-old-web-map/LocalPackages/jp.lilxyzw.liltoon/`，项目 manifest 指向 `file:../LocalPackages/jp.lilxyzw.liltoon`；版本2.3.4，包内 LICENSE 为 MIT。确认主 shader 名 `lilToon`，并有 `Hidden/lilToonCutout`、`Hidden/lilToonOutline`、`Hidden/lilToonTransparent` 等变体；本轮没有修改材质。

## FC14 末段与单次演出截止

谱面与音符证据以 `audio/keil-keygen-2008/summary.json`、`audio/keil-keygen-2008/tracks.csv` 为准。模块 100 个序列段，每段32行、每行80ms，总长256.000秒。order98 是250.88–253.44秒，四通道 pattern 均为0、无新触发，但允许此前声音继续衰减；order99 是253.44–256.00秒，通道2和4切到 pattern29，并在row 0、1、2、3、4、6、8、11、14、18、22、26触发，最后起音在255.52秒。开头order0/1的pattern为 ch1 `[0,0]`、ch2 `[26,30]`、ch3 `[16,0]`、ch4 `[0,16]`；末段没有复制这组开头谱面。

`~/tmp/vrc-old-web-map/keygen-music/fc14-renders/master.wav` 和四条 `ch01…ch04.wav` 临时渲染长256.100秒，`rows.json` 在256.001333秒记录下一循环order0/row0。多出的4800帧属于跨256秒边界的录制余量，不是额外写进FC14的100ms乐句。库内 `episode123/audio/FC14_Mono_01…04.wav` 每条正好12288000帧、48kHz，即256.000秒。样本对比显示末5.12秒和开头并非相同波形；这不能单独说明order99没有音乐回接功能，因为order99仍可能承担过门。

按主代理的单次演出决策，performanceEnd/duration 设为253.44秒：保留order98的250.88–253.44秒延音，253.28–253.44秒对四轨统一淡出，结束在order99触发前。原 FC14 模块和四条256秒 source stem 不裁剪、不改写。精确256秒 clip 排除了捕获额外的100ms，但若运行时继续循环仍会回到order0；本次performanceEnd用于单次演出。

## 用户补充的“笔4件”目录

2026-10-17只读核对 `E:/BaiduNetdiskDownload/地图基本功能合集-初音的VRC小店/笔4件`，共27个文件、75,897,366字节。这里是本体与三套外观：

- `OVPen本体`：实际包内版本3.3.6、Unity2022.3、Worlds依赖`^3.5.0`；另有按文件名标注3.2.9、3.2.14的旧版压缩包。3.3.6的15个C#脚本与本轮采用的旧世界源逐字节相同；差异的14个重叠文件均为生成的SerializedUdonPrograms。
- `4553516木头笔/QvPen_Wood_v1/QvPen_Wood_v1.unitypackage`：12个源文件（两Prefab、两FBX、两材质、六贴图）与已采用源完全相同，包含同GUID木架Prefab；无需因本目录另行重导。
- `5781637兔兔画笔/ARI_QVPen_usa/ARI_QVPen_usa.unitypackage`：10个Prefab，普通与mini、蝴蝶结与简洁等变体；没有本体脚本。随包说明标注适配QvPen3.3.5并依赖lilToon，未在当前3.3.6工程中实际导入验证。
- `5920768圆滑风格笔/QvPenShortV2(1)/QvPenShortV2.unitypackage`：独立笔与笔架两个Prefab，没有本体脚本；另有V1.1压缩包。V2包含与现有库同路径、但内容不同的`Assets/QvPen/Texture/qvpen_button.png`及`.xcf`，换外观时须明确隔离或保留共享UI资源，不能直接整体覆盖。

清单与SHA见`data/fullfilm-v3-pen-four-pieces.json`；包内逐资产目录见 `~/tmp/vrc-old-web-map/fullfilm-v3/intake/pen-four-pieces/packages.json`，三种外观对照为同目录`pen-styles.jpg`。本次仅检查、复制到任务tmp与记录，没有操作Unity编辑器或修改V3场景。
