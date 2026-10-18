# 06 施工单

状态：本单对应旧 180 秒运行骨架。新版剧情、片尾和美术分别见 [15](15-integrated-show-outline.md)、[17](17-art-production-plan.md)，需在素材与声画试制后整体重排本单；下文卡点、坐标和收尾不作为 4:16 版已选方案。

新场地拓扑、灰盒候选尺寸与批次见 [19 三维场景规划](19-spatial-scene-plan.md) 和 `data/spatial-scene-plan.json`；该数据是讨论规格，尚未替换本单所用的 `data/stage.json`。

本文件把创意规格翻译成 Unity 侧可以直接执行的数值与步骤。数据源是 `data/stage.json`（空间与道具）、`data/labels.json`（上屏文字）、`data/timeline.json`（段落与事件）、`data/lyrics.json`、`data/quotes.json`。

先跑一次自检：`python3 tools/validate_spec.py`。它检查分段连续性、事件可解析、歌词归属、连线对端、相机覆盖与预算一致性，当前状态为 OK。

## 一、坐标系与尺度

- 单位：米。+Z 是前进方向，X 左右，Y 向上。
- 地面格栅 2 m 一格，走廊半宽 3 m；玩家出生点 `(0, 0.05, -6)`，眼高 1.6 m。
- 全片沿 Z 轴展开 150 m：冷启动 −8..2，黄金段 4..36，网盘峡 40..64，开源荒原 68..90，VRChat 城 94..116，希望之河 120..138，片尾 140..150。
- 每个分区是一个可独立开关的根节点，段落切换只改分区可见性与环境参数，不移动几何体。

## 二、节点与连线

- 22 个节点，21 条连线，全部列在 `data/stage.json` 的 `nodes`。
- 节点名片尺寸 1.6 m × 0.9 m，牌面中心高度 1.4 m，标题字号 0.13 m，正文 0.09 m，状态条 1.6 m × 0.08 m。
- 节点朝向：左右两侧的牌子朝走廊中心（`rotation_y` 90 或 −90），走廊中轴上的牌子朝来路（180）。
- 连线用 `LineRenderer`，宽度 0.06 m；失效时在中间断开 0.5 m（对应道具 `link-expire-01`）。
- 六态视觉参数从左到右分别是：在线的琥珀自发光、限速的青色闪烁、失效的灰度与 broken image、归档的降饱和灰点、被导出的框选高亮、复活的青绿 `#3DFFB0`。

## 三、道具与特效（17 个）

`data/stage.json` 的 `props` 逐个给了种类、位置与参数，按种类实现：

| 种类 | 实现要点 |
| --- | --- |
| `text_wall` | 逐字符打字机，速度由 `chars_per_second` 控制；`c-prompt` 带光标闪烁 |
| `directory_tree` | 12 行缩进文本，行高 0.22 m，展开动画 1.5 s |
| `scanline_row` | 8 张 0.9 m 缩略图，5 s 内自上而下扫描成相 |
| `progress_bar` | 读满后回退，循环 4 次，速度固定 100 KB/s 的视觉隐喻 |
| `broken_link` | 线段断开 + 端点抖动 |
| `popup_wall` | 120 个实例合并成单网格，透明度 0.85，禁止逐个 GameObject |
| `date_banner` | 巨大日期字 0.4 m，"最后提交：四年前" |
| `particle_flood` | 400 粒子沿 −Z 向镜头涌来，寿命 4 s |
| `despawn_effect` | 1.5 s 溶解，从边缘向内 |
| `marquee_select` | 1.2 m 见方选框，框选后复制到 `paste_target` |
| `blackout_wave` | 沿 +Z 以 12 m/s 席卷全图，3 s 内把自发光压到 0 |
| `case_wall` | 24 块案例砖，0.6 m/s 横向滚动，颜色 `#3DFFB0` |
| `greetz_roll` | 7 行彩色滚动名单，0.9 m/s |
| `map_collapse` | 8 m 半径内所有节点向磁盘图标收拢，3 s |
| `visitor_counter` | 8 位翻页数字，从 42 滚到 0，4 s |

## 四、相机路径

12 个关键帧，见 `data/stage.json` 的 `camera_path`，覆盖全部 7 个段落。演出模式用这段路径做轨道运动；自由模式不启用。

要点：黄金段贴地 1.9 m 平视；网盘峡被弹窗墙挤到 1.7 m；VRChat 城在 137 s 抬到 4.5 m 回望整片熄灭；片尾降到 2.8 m 俯视磁盘图标与归零的计数器。缓动只用 `linear` / `ease-in` / `ease-out` / `ease-in-out` 四种，避免风格漂移。

## 五、上屏文字

全部文字在 `data/labels.json`，不要在场景里手打：

- `ui.boot` 是 CMOS 自检六行与 `Starting Continue.exe…`；
- `ui.plates` 是入口铭牌 `Best viewed in 800×600`、`UNDER CONSTRUCTION` 与片尾署名四行；
- `node_labels` 是 22 张名片卡的标题与元信息，含六态文案覆盖；
- 引文正文不在这里，卡片按 `quote_ref` 去 `data/quotes.json` 取。

字体风险：中文点阵字体需要先生成 TextMeshPro Font Asset，并与拉丁数字保持等宽。建议用等宽中文点阵字体（如 Noto Sans SC 的子集）加 ASCII 像素字体两套，数字与年份走 ASCII 那套。

## 六、施工顺序

1. `unity command set_autotick --enable true`，确认编辑器在后台也继续 tick。
2. 写 `AgentScripts/Build.cs`（放在 `Assets/` 外），用 `run_script` 的 builder 模式批量建：分区根节点 → 地面格栅 → 节点与连线 → 道具 → 相机轨道。
3. 改名/改字段这类迭代也用 `run_script`，只做 `recompile` 的时候才碰 `.cs` 的编译期改动。
4. 每完成一个分区，用 `capture_game_view` 在同一机位截一张图留档，对比 `spec/01-timeline.md` 的画面描述。
5. 全部建完后跑一次性能检查（面数、材质数、纹理内存、音频源），再进入段落联调。

## 七、段落联调检查表

| 段落 | 看什么 | 通过判据 |
| --- | --- | --- |
| cold-boot | 六行自检逐字出现、第一枚光点上线 | 打字速度与 18 字/秒一致，光点在 12.5 s 亮 |
| act1-golden | 目录树、缩略图扫描、名片卡、greetz 第一次切换 | 40 s 切 greetz，节点全亮，亮度是全片上限 |
| act2a-netdisk | 断线、进度条卡住、弹窗墙合拢 | 58 s 卡在 100 KB/s，70 s 弹窗遮住视线 |
| act2b-opensource | 墓碑日期、issue 洪水、引文卡 | 96 s 洪水过脚，110 s 出现 curl 引文 |
| act2c-vrchat | 摘除、框选复制、整片熄灭 | 128 s 完成粘贴，140 s 全图自发光归零 |
| act3-hope | 复亮点换色、案例墙 | 148 s 第一枚重亮，颜色为 `#3DFFB0` |
| outro | 合拢、计数器归零、`C:\>_` | 176 s 计数到 0，178.5 s 出现提示符 |

## 八、不要做的事

- 不要为了效果在运行时新建 GameObject；道具与节点一律预先在场景里做好，运行时只改参数。
- 不要逐个同步 120 个弹窗或 400 个粒子；只同步段落号与节点状态位。
- 不要把中文字符串写进代码；全部从 `labels.json` 走导入脚本进资产，改文案不重新编译。
- 不要手改 `.meta`、`.unity`、`.prefab`、`.mat` 等序列化文件；一律走 Pipeline。
