# V4 原生操作：按新年代重新编排

本模块只新增 V4 脚本、数据和资源，不修改 V3，也不覆盖 V4 Montage。主规格与 11 条用户意见见 `spec/36-episode123-v4-production.md`。V3 的字体、文字行框、深度排序及真实音符检查都保留。

## 三段操作及年代

| 时间 | 原生动作 | 结果 |
| --- | --- | --- |
| 10.24—20.48 | Windows 98 目录、文件图块、README、记事本有独立深度；选中、拖拽、双击打开 | 15.36 与 15.56 两次点击，第二次才展开；正文 18.305 秒写完，18.88 秒显示已打开 |
| 40.96—51.20 | 四节点提供 16 个分片，音符控制实际传递；区块逐个抵达后合拢为文件 | 47.36 秒成片并校验，48.00 秒开始继续上传，51.05 秒最后一包抵达 |
| 61.44—71.68 | Aegisub 3.2 翻译助手真实分区、实际四轨音频、原生时间轴与译文输入 | 64.64 秒仍有“回耒了”，64.88 秒后改“回来了”；第六行入点实际前移 80ms；70.94 秒后保存 ASS |

三组均是 region 0，位于第一次整体旅行之前。它们全部挂在 `journey.worldFrame/Revision4Operations`，只写局部坐标；旅程移动它们时不会补偿摄像机位置。三组仅在各自半开区间出现，互不挤进其他时期。

## 来源与重建口径

Windows 98 使用既有 `windows98-se-explorer` 的真实虚拟机截图，截屏拍摄日未确认。目录、标题、工具栏、状态栏和纸张图标均来自同一完整原图。README 与记事本工作文档是本片制作的操作内容，不冒充历史文件。可见正文采用自然说明：使用说明、感谢分享、保留作者署名、遇到问题欢迎回来交流；不显示“本项目原创”“原图拆层”等审核口吻。

字幕不继续使用 V3 中标题明确为 v2.2 的主界面截图。V4 改用既有 `aegisub-translation-assistant-doc`：官方文档 `/img/3.2/Translation_Assistant.png`，383×546，真实 Original、Translation、Keys、Actions 四区和标题拆层。3.2 作为 2014 版本家族进入本段，图像本身拍摄日仍记未知，不称为 2014 当年截图。右侧音频和时间轴为该工具工作方式的原生场景复刻，译文为本片创作；这些性质登记在 JSON，不烘焙到画面。

以上图片都引用 V3 的只读纹理路径。没有新图片，也不将裁切、分区和预制副本计为新素材。模块不改图片 importer、源图文件或 V3 材质。

## 可见文字

只保留界面本身、文档内容、翻译、校对与下载/做种状态。README 标题为 `README.TXT — 记事本`，菜单为文件、编辑、搜索、帮助。BT 使用“下载中”“下载完成 · 校验通过”“做种中 · 已上传 N 个分片”和每个节点已提供/收到的数量；文件名为“我们的短片.mkv”。字幕显示实际英文、中文、当前行、确认结果、当前起止时间与 `movie.zh-cn.ass` 保存结果。

删除来源性质脚注、“原创演示”“Win98 原图拆层”“真实起音触发传递”“听译→对轴→校对 1/8”等用户不需要的制作说明。自然 README 的作者署名提醒属于文档内容，保留。

## 鼠标命中真实内容

指针网格的实际尖端在 `(-0.3,0.5)`，运行器按尖端计算位置，按下缩放也不会使点击点漂移。

README 先点真实纸张图标所在的工作图块，指针与图块平面共面，仅向前留 6mm 防深度冲突；拖动时指针随文件平面一起旋转。打开后移到记事本真实标题/正文平面，不另造按钮。点击环只做短暂反馈，不写深度。

Aegisub 输入时指针跟随当前文字插入点；69.04 秒指向真实 3.2 原图中的 Play Video 按钮，源像素 `(300,374)`。该像素属于裁切 `(23,309,333,181)`，UV 直接由源像素推导，随着 Actions 面片的真实局部变换求位置，没有虚拟按钮。

源坐标目检图为临时验证资料 `~/tmp/vrc-old-web-map/episode123-v4/operations/aegisub32-real-click-proof.png`，不进入片内。

## BT 重新选择原音符

`data/episode123-v4-operations.json` 保存 16 次收片与 8 次上传的绝对时间、声部、原音符数组索引、节点、行程和抵达时刻。它们从 40.96—51.20 秒的原实际音符重新挑选，不是将 V3 时间表减去常数后称作对齐。

收片起点包含 0.16/0.32/0.48/0.64 秒等不等间隔，最末分片在 46.72 秒由真实起音出发，0.64 秒后抵达。四节点轮换，每个恰好提供 4 片；节点身份与触发声部单独记录，避免该段没有新起音的 CH02 被伪造为鼓点。

上传为实际起音 48.00、48.32、48.64、48.96、49.44、49.76、50.08、50.40 秒；四节点各接收两次。Builder 会逐个读取 `music.notes[channel][note_index]`，不符即失败。运行时按绝对时间直接重建分片状态和路径，倒退、迟到与重复 Seek 不依赖上次发包历史。

## 字体与渲染不回退

独立字体位于 Revision4/Operations/Fonts，通过现有源 TTF 新建 4096×4096 动态单图集，材质和图集均为子资产。不复制已填充的多 atlas TMP 资产，也不修改来源字体。

所有 Text 的行框至少为 `fontSize × (face.lineHeight / pointSize) × 0.1 × 1.2`，避免 Ellipsis 在高度不足时清空整行。验证通过 `ForceMeshUpdate(true,true)` 和实际 `characterInfo.isVisible` 检查，不只看 `.text`。

复用只读的 `OldWebMap/V3/Operation Crop` shader，但全部材质独立存入 Revision4：实体、纸面、原图与框为 2020/写深度；选择、点击环与渐显文件为 3010/不写深度；字体为 3020。零 alpha 裁掉，保留 LEqual，不使用 ZTest Always。实际实体与透明材质数量在报告中列出，不硬套 V3 的计数来假称通过。

四条小波形复用只读 V3 PCM 图集与 ribbon Mesh，V4 有自己的材质与行采样参数。没有改变真实 AudioSource 或主波形的位置。

## 构建与验证

新增：

- `unity/Assets/OldWebMap/Scripts/OWV4Operations.cs`
- `unity/AgentScripts/OWEpisodeV4Operations.cs`
- `data/episode123-v4-operations.json`

主代理先批量同步编译，再将数据放到 `AgentScripts/data/episode123-v4-operations.json`，通过 Pipeline 调用 `OWEpisodeV4Operations.Build()`。要求已有 V4 Journey.worldFrame、真实音乐导演与共享 V3 波形资源。V4 score 可用于补充图像索引；本模块的数据已包含所需来源路径和尺寸，不要求 V4 score 先存在。

根为 `worldFrame/Revision4Operations`，资源为 Revision4/Operations/{Materials,Meshes,Fonts}。重复构建只重建该根，独立材质与网格复用。Build CopyProxy、SaveAssets、MarkSceneDirty，由主代理最终保存场景。

运行器保留独立 LateUpdate 入口与公开 Sample/Seek；集成 V4 中设 `autoSample=false`，由 `OWV4Presentation` 按统一时钟顺序采样。编辑器聚合入口为 `OWEpisodeV4Operations.Sample(float)`。`Validate()` 核：

- 新时钟的分组独占、16 块完成与继续上传。
- 全部实际音符索引和绝对时间，四节点确实参与。
- README 满文、八行校对/保存、三行字幕及时间字段的真实可见网格。
- 指针尖端与真实图内按钮命中。
- 第六行可见时码确实从 67.840 前移至 67.760，而非只写“80ms”状态句。
- 反向 Seek 的相同包路径、共同 worldFrame 平移后局部姿态不变。
- 深度队列、零透明度裁剪、字体后绘制、片内无审核/来源口吻。

验证报告在 Windows 用户临时目录 `episode123-v4/operations/`。主代理已通过 Pipeline 完成构建、实际时段截图及 `Validate()`；BT 结尾额外在 51.12 秒断言 16 个收片、8 个上传全部完成，避开 51.20 秒退场后的缓存计数。全片 Udon 与实机验收的最终状态统一记录在 `spec/36-episode123-v4-production.md`。
