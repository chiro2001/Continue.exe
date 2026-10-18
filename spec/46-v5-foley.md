# V5 拟音：年代声音层（第五声部）

本模块对应 [42](42-episode123-v5-look-and-throughline.md) 的第 8 项。V4 只有 5 个程序音，多数年代与动作没有声音身份；V5 用 numpy 原创合成 29 个短音，按 80 ms 行网格排成 137 个事件，叠在 FC14 四轨之上。运行时直接复用 `OWV4Foley`（`PlayOneShot`，起音时取发声点位置），不改 V3/V4 脚本、资源和四条音乐声源。

## 原则

- 全部由程序合成：没有录音、人声，也不模仿 Windows、VRChat、QQ、MSN 或其他系统音。拨号握手用标准 DTMF 双音、2100 Hz 应答音与随机拼接的音/噪声段，不是任何真实协议音的复刻。
- 音乐是主体。拟音总量低于音乐，事件挑动作落点，同一时刻不堆瞬态；旅行期间只有气流，不播其他事件。
- 音高跟随曲子：FC14 四轨的音级能量约 46 % 在 F，铃音用 F 大三和弦（C6→F6、F5→A5），低脉冲用 F3；气流、营火等宽带声没有稳定音高。
- 固定种子，重跑逐字节一致。每个 WAV 为 48 kHz 单声道 PCM_16，去直流，首尾淡入淡出，峰值归一到 −3 dBFS；场景里的电平由各片段 `AudioSource.volume`（数据中的 `source_peak`）决定。

## 片段（`art/export/episode123-v5/foley/`）

| 片段 | 时长/s | 内容 |
| --- | --- | --- |
| `crt_power_on` | 0.82 | 消磁低频闷响（150→46 Hz 滑落 + 50/100 Hz 衰减）与 15.625 kHz 行频啸叫 |
| `modem_handshake` | 3.20 | 6 个 DTMF 数字、2100 Hz 应答音、渐密的音/噪声乱码段、载波嘶声尾；限制在电话带宽 |
| `hdd_seek_a/b` | 0.34 | 磁头寻道的短点击串，两种节奏 |
| `keyboard_mech_a/b/c` | 0.17 | 机械键盘：开关脆响、触底、抬键，三种变化 |
| `keyboard_laptop_a/b/c` | 0.12 | 笔记本键盘：低而柔的短键程，三种变化 |
| `mouse_click_old` | 0.14 | 旧鼠标按下与释放 |
| `bt_piece_a/b/c` | 0.07 | 分片到达的小滴答（F6、A6、C7），按分片号轮换，同刻到达的分片不同音 |
| `bt_complete` | 0.62 | 两音柔和铃声 C6→F6 |
| `forum_post` | 0.36 | 纸页/印章落下的闷响 |
| `subtitle_snap` | 0.075 | 字幕确认的两段短滴答 |
| `journey_air_long/short` | 2.60/2.10 | 宽带气流渐强渐弱，峰值对准旅行中点（1.52 s / 1.20 s 处） |
| `campfire_crackle_a/b` | 7.2 | 营火低频底噪加噼啪，两端 1.28 s 等功率淡变，可无缝重叠 |
| `gate_close` / `gate_open` | 1.00/1.30 | 金属闩撞击；滑动加轻闩 |
| `verify_ok` / `verify_wait` | 0.60/0.45 | 柔和两音 F5→A5；低 F3 脉冲 |
| `save_write` | 1.52 | 磁盘写入的快速点击串，末尾落定 |
| `msg_pop` | 0.26 | 原创的柔和双泡声（三Q时期 IM 语境） |
| `notification` / `page_flip` | 0.42/0.32 | 取代 V4 同名音：A5 单音；翻纸 |

## 事件（`data/episode123-v5-foley.json`）

所有时间都是 0.08 s 的整数倍。V4 的 30 个事件（含重复的一条）在数据里逐时刻登记去留：保留 13 个（点击、翻页、通知，点击换成 `mouse_click_old`），`connection` 换成握手，其余因与新事件重合、落在旅行内或被新序列取代而删除。

| 时间/s | 事件 |
| --- | --- |
| 0.08 / 1.28 | `crt_power_on`；`modem_handshake`（取代 V4 `connection`） |
| 10.24—20.48 | 11.52 选中、15.36 与 15.52 两次点击、15.68/15.92/16.24 读盘、16.72—18.00 机械键盘敲 README、18.88 保存点击、18.96 写盘 |
| 25—38 | 三段机械键盘短敲；V4 保留的翻页与通知 |
| 40.96—51.20 | 16 个 `bt_piece` 在分片到达时刻；47.44 下载完成铃；47.52 校验读盘；8 个 `bt_complete` 在上传到达时刻（48.64—51.04） |
| 51.20—61.44 | 笔记本键盘起；51.84、56.00 社区页面落下与 59.52 回帖时的 `forum_post` |
| 61.44—71.68 | 8 个 `subtitle_snap` 在 Aegisub 逐行确认时刻（62.32 起每 1.28 s）、每行前的笔记本键盘敲击、69.04 播放按钮点击、70.96 导出通知 |
| 79.12 / 86.16 / 102.16 | 三次 `journey_air`，各比旅行早 0.24 s；其中 86.16 用短版 |
| 88.80 / 94.72 | `campfire_crackle_a`、`campfire_crackle_b`，重叠 1.28 s，101.92 结束，不进入 102.40 的旅行 |
| 105.60 / 107.52 / 110.08 | 三个 `msg_pop`：三Q两张焦点图落下，与 A 闸“无法同时运行”弹框 |
| 108.96—122.88 | 闸门序列：`gate_close` 108.96/112.64/117.76，`gate_open` 115.20/118.40/120.40，`verify_wait` 113.92/116.48，`verify_ok` 120.32/122.88 |
| 123.92—128.00 | 档案桌笔记本键盘、125.44 文档滑入的翻页、126.48 `save_write`（正好在 128.00 结束）、128.00 已保存的 `verify_ok`；131.20 保留 V4 的点击 |

## 发声点

`OWV4Foley` 在起音时取 `emitters[i].position`，之后声源停在该点。0 号是内容点（运行时读 `contentWorldPosition` = (0, 2.1, 5.6)），1 号沿用 V4 键盘接触点；V5 新增：

| 编号 | 名称 | 位置 |
| --- | --- | --- |
| 2 | journey-air | 物理空间 (0, 2.0, −1.2)，在观众前上方，不进 worldFrame |
| 3 | campfire | worldFrame 内，2019 Hub 篝火处（区域 2），到达后物理位置约 (0, 0.96, 4.6) |
| 4/5/6 | gate-a/b/c | worldFrame 内，区域 3 三道闸中心上方 1.1 m，到达后物理位置 (0,1.1,6.0)、(0,1.1,10.2)、(4.8,1.1,6.0) |
| 7 | archive-desk | worldFrame 内，维护桌上方，到达后物理位置约 (4.65, 0.8, 4.9) |

worldFrame 内的发声点由构建器按场景中的篝火灯、闸门和维护根物体定位；场景处于与保存状态不一致的姿态时退回数据里的坐标。所有这类事件都在对应旅行结束之后，所以起音位置就是物理位置。

## 电平与参考混音

`tools/render_episode123_v5_audio.py` 以观众 (0, 1.35, −2.8) 为固定听者：音乐沿用 V4 的位置模型（`--scene-report` 默认指向 V4 保存场景验证，通过时沿用 V4 保存场景的声源轨迹，V5 不移动音乐），拟音按发声点做等功率声像和距离衰减（6 m 内不衰减，远于此按 1/距离，对应 V4 参考混音里内容点 0.73 的相对平衡）。这不是头显 HRTF，也不模拟 VRC 空间音频的增益曲线。

最近一次本地渲染（135.04 s、48 kHz 立体声，`~/tmp/vrc-old-web-map/episode123-v5/review/review-audio.wav`，另有只含拟音的 `review-foley-only.wav`）：音乐峰值 −6.05 dBFS、RMS −18.16 dBFS；拟音峰值 −24.42 dBFS，比音乐峰值低 18.4 dB（要求 ≥ 14 dB）；逐事件对照音乐局部峰值，最小余量 15.0 dB。

| 组 | 事件数 | 峰值 dBFS | 全片 RMS dBFS | 低于音乐峰值 dB |
| --- | --- | --- | --- | --- |
| air | 3 | −24.4 | −54.0 | 18.4 |
| keys | 56 | −25.9 | −63.9 | 19.8 |
| bt | 25 | −26.7 | −59.4 | 20.6 |
| gates | 11 | −27.7 | −59.1 | 21.7 |
| opening | 2 | −28.5 | −57.6 | 22.4 |
| disk | 6 | −28.6 | −66.4 | 22.6 |
| fire | 2 | −28.8 | −50.7 | 22.8 |
| forum | 3 | −29.1 | −71.1 | 23.0 |
| ui | 29 | −30.2 | −60.6 | 24.1 |

## 构建入口

1. `python tools/make_episode123_v5_foley.py` 生成 WAV 与数据；本地 `python tools/render_episode123_v5_audio.py` 渲染参考混音并检查余量。
2. 把 WAV 放到远端 `Assets/OldWebMap/Episode123/Revision5/Foley/Audio/`，数据放到 `AgentScripts/data/`。
3. Pipeline `OWEpisodeV5Foley.Import`：强制单声道、预载、非后台加载；短片 PCM，≥ 1 s 的片段 Vorbis 质量 0.75（载入时解压）。
4. `OWEpisodeV5Foley.Build`：在 `OWEpisode123/V4 physical sound sources/V5 foley` 下为每个片段建一个 `AudioSource`，附 `VRCSpatialAudioSource`（设置取自 V4 的 `Original mouse_click` 源，找不到时用 VRChat 自动添加的默认值）；写入 `OWV4Foley` 的 `sources`、`eventTimes`、`eventClips`、`eventEmitters`、`emitters`。可重复运行，不保存场景。
5. `OWEpisodeV5Foley.Validate`：核对网格、排序、索引、旅行内无事件（气流除外）、气流提前 0.24 s、营火限定窗口与发声点、机械/笔记本键盘分界、`crt_power_on` 0.08 与 `modem_handshake` 1.28、源与片段为持久资产且在 worldFrame 之外、四条 FC14 声源未动；写 `reports/foley-validate.json`（含实际发声点位置，渲染脚本可读取）。

## 局限

- 未做听感确认，也没有头显实测；电平与空间位置都是离线参考，尤其 VRC 空间音频默认 +10 dB 增益与衰减曲线没有模拟，需要进场景后用 `master_scale` 或各片段 `source_peak` 校准。
- V4 的 5 个旧声源留在场景中但不再被引用。
