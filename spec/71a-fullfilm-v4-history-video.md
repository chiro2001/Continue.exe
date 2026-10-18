# Fullfilm V4：VRChat 历史视频素材

这组素材从用户提供的《6分钟看完VRChat历史》视频中筛出，供 V4 时间线挑选真实历史画面。交付包括 40 张 PNG、8 段无声 H.264 MP4、帧与短片 contact sheet，以及逐项记录来源时间、类别、年代证据、建议 MV 区间、裁区、匿名化范围、文件尺寸和 SHA-256 的 [JSON 清单](../data/fullfilm-v4-history-video.json)。

## 来源与下载记录

视频 BV 号为 `BV1brH769EtM`，标题《6分钟看完VRChat历史》，B 站页面 API 登记发布时间为 2026-10-04 14:10:43 UTC，时长 385 秒、标注分辨率 1920×1080。用户提供的下载服务返回 HTTP 200，将请求重定向到 B 站 CDN 视频流；实际下载流为 1280×720、H.264、名义 30 fps、时长 384.383878 秒，带 AAC 原声。API 页面字段和下载响应分别记录在 [页面 API 元数据](../art/reference/fullfilm-v4-video/source-page-api.json) 与临时目录 `~/tmp/vrc-old-web-map/fullfilm-v4/history-video/source-response.json`。

完整下载源仅存于 `~/tmp/vrc-old-web-map/fullfilm-v4/history-video/source-full.mp4`，大小 51,167,482 字节，SHA-256 为 `6f156db2b0687169492d448d3f98d03658cdc8368ead9993c494ebb6a34a79be`。仓库只保存挑选并处理后的交付文件。

## 画面处理

输入帧尺寸为 1280×720。统一裁区为源图坐标 `[260, 0, 1252, 558)`，得到 992×558 的精确 16:9 区域，再等比缩放到 1280×720。裁区去掉左上 B 站水印及右下角教育用途字样，不增加黑边，也不做补画、生成式填充或界面重绘。逐项 crop bounds 记录在 JSON。

对能读出真实玩家用户名的少量画面，仅在用户名文字区域做像素化，保留名牌外框、图标和周围历史 UI。涉及匿名化的帧会在清单中记录源图坐标与处理方法。短片删除全部音轨；画面统一输出为 1280×720、H.264 MP4，长度 2–6 秒。

## 年代证据

片内直接出现的年份卡时间码为：2014（00:00:01）、2015（00:00:21）、2016（00:01:04）、2017（00:01:28）、2018（00:01:54）、2020（00:03:04）、2021（00:03:47）、2022（00:05:34）。其他画面按所在章节上下文给中等置信度，或标为未知/待核；例如 Avatar 3.0、New Action Menu、Regions、IK 2.0 只记录版本或功能名，不据此猜拍摄年份。Unity 2019 LTS 仅证明画面提到了该版本。

## 短片清单

| ID | 源时间码 | 时长 | 画面 | 建议 MV 区间 |
|---|---|---:|---|---|
| C01 | 01:54–01:58 | 4 秒 | 2018 年份卡转 Unity 编辑器 | VRC 79.36–86.4 |
| C02 | 03:19–03:22 | 3 秒 | Introducing Udon 功能卡 | VRC 86.4–102.4 |
| C03 | 03:30–03:36 | 6 秒 | Avatar 3.0、头像控制页、New Action Menu | VRC 86.4–102.4；具体年份待核 |
| C04 | 04:22–04:26 | 4 秒 | Regions 卡转创建实例地区选择界面 | 年份待核，可作 UI 变化过渡 |
| C05 | 04:34–04:38 | 4 秒 | Engine Upgrade / Unity 2019 LTS 卡 | VRC 86.4–102.4 |
| C06 | 04:45–04:47 | 2 秒 | Spookality 2021 卡 | VRC 86.4–102.4 |
| C07 | 05:34–05:38 | 4 秒 | 2022 年份卡转 OSC for Avatars | VRC 102.4 之后 |
| C08 | 05:56–06:02 | 6 秒 | IK 2.0 跟踪功能卡 | VRC 102.4 之后；具体年份待核 |

## 文件位置

- 原始 PNG 帧：`art/export/fullfilm-v4-video/frames/`
- 无声 MP4：`art/export/fullfilm-v4-video/clips/`
- 40 帧总览：[contact sheet](../art/export/fullfilm-v4-video/contact-sheet.png)
- 8 段短片总览：[clip contact sheet](../art/export/fullfilm-v4-video/clip-contact-sheet.png)
- 处理脚本：[prepare_fullfilm_v4_history_video.py](../tools/prepare_fullfilm_v4_history_video.py)

指定用于阅读与绑定的 F 编号保持为 F01、F09、F12、F14、F15、F16、F19、F20、F24、F25、F26、F28、F29、F30、F31；其中 F14 采用 01:44 单个绿色名牌特写并像素化用户名，按名牌 UI 画面使用，不标成 2018。其他 F 编号在清单中标明内容，可按需要替换。
