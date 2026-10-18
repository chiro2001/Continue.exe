# 音频素材来源与实证（M0）

记录日期：2026-10-15（第二版，修正第一版的曲目判断）。上游依据：`spec/05-toolchain-and-m0.md`。

## 结论：Keil 注册机音乐 = FC14，4:16，4 条音轨

EDGE Cracking Group 的 Keil 注册机 `keygen_new2032.exe` 只内嵌一首曲子：
资源 `RT_RCDATA / TUNE`，格式为 Amiga **Future Composer 1.4（FC14）**，
完整播放长度 **4:16.000（256.0 秒）**，4 个通道（即 4 条音轨），播放到结尾后回到序列表开头**循环**。

第一版把《Funky Stars》(XM) 当成注册机音乐，是错的：那是另一首常被写进「注册机音乐」
合集的曲子（Quazar of Sanxion，1996-01，2:20.296，10 通道），**不在**这个注册机里。
两首都能用，但用途要分开记录（见文末「候补曲目」）。

## 主素材：`audio/keil-keygen-2008/tune.fc14`

由本仓库流程从原程序静态解包得到，未运行目标程序：

| 项 | 值 |
| --- | --- |
| 格式 | Future Composer 1.4（`FC14`，Amiga，PAL 50 Hz，4 通道） |
| 长度 | 4:16.000（100 段序列 × 32 行 × 80 ms/行） |
| 大小 | 9,902 字节 |
| MD5 | `08fc472649363a74f40c4a58df73174d` |
| SHA-256 | `2e901c5869a1bfa486baa0d521af03a691e416d7641ec2f243b04f736b91b0b6` |
| 循环 | 是（序列表播完回到开头；注册机开着就一直放） |
| 音轨 | 4 条通道，各自独立的 pattern 序列（pattern 0–40） |

宿主程序（只做取证，不进仓库、不随片发布）：

| 项 | 值 |
| --- | --- |
| 文件 | `keygen_new2032.exe`（EDGE Cracking Group，PE32，509,575 字节） |
| SHA-256 | `216e34266fb0fa44a596c1624ecef633d67e6cb9f522c44c7ee36d0a14da7806` |
| 内嵌资源 | `RT_RCDATA / TUNE`（语言 2070），9,902 字节 |
| 解包方式 | PE 资源目录解析（脚本 `pe_extract_res.py`，只读） |

三重互证（数值完全一致）：本仓库解包结果、`FASTSHIFT/keygen_re` 从同一 exe 提取的
`rcdata_tune.bin`、`RigoLigoRLC/FutureComposerSTM32` 内嵌字节串（该副本尾部多 2 个 `0x00`）。

## 音轨数据（可视化输入）

`tracks.csv`：逐通道逐音符事件，字段为
`channel,order,row,time_s,pattern,raw,note,instrument,volume_slide,pitch_slide`。

| 通道 | 音符事件数 | 使用 pattern 数 | 首/末事件 |
| --- | --- | --- | --- |
| 1 | 963 | 8 | 5.12 s / 245.60 s |
| 2 | 487 | 13 | 0.40 s / 255.52 s |
| 3 | 656 | 13 | 0.00 s / 250.72 s |
| 4 | 847 | 13 | 2.56 s / 255.52 s |

合计 2,953 个音符事件。`summary.json` 记录头部解析结果、4 条通道的 pattern 序列
（每段 32 行，`order` 0–99）与哈希。

时间基准：`time_s` 由 `Σ(speed × 1/50 秒)` 累加，行间隔 80 ms（速度 4），逻辑循环为 4:16.000。
2026-10-15 补充检查：当前临时 WAV 文件实际长 256.100 秒，播放位置日志在约 256.001333 秒记录
下一循环开头，不能把整个文件当作精确的一轮。多数行位置轮询记录约晚 1.333 ms，属于观测粒度，
不是所有起音必须平移的依据。卡点用逻辑行表，正式母带与输出延迟另做对拍，见 `spec/14`。

工作目录另有（未入库）：`fc14/tracks.json`（含完整事件对象）、
`fc14-renders/master.wav` 与 `ch01…ch04.wav`（48 kHz/32-bit float 分轨渲染，仅供试听对拍）。

本轮另根据事件和分轨信号建立 `data/music-visual-plan.json` 与 `spec/14-fc14-music-visual-score.md`。
测得临时渲染存在直流偏移；空行也可能有持续声，不能用原始 RMS 或新音符数直接判断静音与情绪。
这是离线测量与演出建议，不代表已经完成最终混音、听感评审或客户端同步验证。

## 署名与版权

- 音乐：Future Composer 1.4 模块；反向工程报告记为作曲 **Rhino of Torment**，
  移植项目注记原曲出自 **Cytax 的 I.C.E. 2 磁盘杂志**（曲名待进一步查档确认）。
- 注册机程序本体与模板作者：EDGE Cracking Group。片内/世界内不出现注册机本体，
  不提供下载、不引导破解。
- 使用素材时在片尾与世界说明牌署名，并注明「取自 2008 年 EDGE Keil 注册机内嵌模块」。

## 候补曲目（不在本注册机内，是否引用另行决定）

| 曲目 | 格式 | 长度 | 通道 | 来源 |
| --- | --- | --- | --- | --- |
| Funky Stars（模块内标题 `Hybrid song 2:20`） | FastTracker 2 XM | 2:20.296 | 10 | The Mod Archive 66187，MD5 `4e7887bcd4495e8ad2147a17057fb94e` |
