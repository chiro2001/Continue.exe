# 全片 V4：历史表情与表情包素材

日期：2026-10-18。当前清单可直接供 V4 场景绑定。

本轮导出 **29 个不同表情图**，另有阿狸、VRChat+功能图和早期文字颜文字三个脉络的辅助史料。主清单的每个 `display_path` 都指向一张独立 PNG；重复动图帧没有拆成多个表情。

联系表：[全部 29 个表情](../art/export/fullfilm-v4-emotes/contact-sheet.png)、[QQ 2003 核心集](../art/export/fullfilm-v4-emotes/contact-sheet-qq2003.png)、[背景与年代辅助图](../art/export/fullfilm-v4-emotes/contact-sheet-context.png)。

完整来源、下载与导出 SHA-256、宽高/aspect、动画帧数、裁区、背景处理、年代证据、不确定性和推荐时间均在 [JSON 清单](../data/fullfilm-v4-historical-emotes.json)。原始文件保存在 `art/reference/fullfilm-v4-emotes/`，独立 PNG 保存在 `art/export/fullfilm-v4-emotes/`。可以运行 `python tools/prepare_fullfilm_v4_emotes.py` 从这些本地源文件重新导出，不需要再次联网。

| 素材家族 | 数量 | 年代与来源证据 | 推荐文化段 |
| --- | ---: | --- | --- |
| QQ 经典黄脸 | 20 | 社区档案标为 QQ 2003 版，附 96 项命名映射；原 GIF 为 20×20 动图 | 工具/汉化/BT，主起点 20.48 |
| 洋葱头大头贴 | 3 | 搜狐记载系列约于 2006 年创作；实际裁切源图日期为 2007-10-10，原图 alt 为“大头贴”，只裁了三个有辨识度的小格 | 工具/汉化/BT，主起点 51.2 |
| 百度贴吧彩色滑稽 | 1 | 百度词条说贴吧滑稽自 2012 年起广泛使用；本文件是较晚的彩色配图，不冒充 2012 原始图 | 工具/汉化/BT，主起点 51.2 |
| Rage Comic | 3 | Openclipart 的 Rage Guy、Forever Alone、Trollface 向量图均来自 2011 年前后；这些是同期矢量化版本，不是 2008 年最早的原始上传 | 工具/汉化/BT，主起点 51.2 |
| 兔斯基 | 1 | 公开资源页日期为 2011-07-30，注明其官网为 `mytuzki.net`；图片是 8×5 预览格之一，表情包首次发行日不明 | 工具/汉化/BT，主起点 51.2 |
| Twemoji | 1 | 官方 Twemoji v2.0.0 发布树中的 72×72 PNG | 过载/社交反应，133.12–186.88 |

**按来源做了限制。** QQ 档案仓库声明 GPL-3.0，但这不足以独立证明腾讯底层美术资产可再发布，故清单将其标为历史参考、需确认使用权。搜狐这张洋葱头来源图是角色大头贴拼图，不把它称作 QQ/MSN 原始表情包。百度的 2012 年证据只说明滑稽表情的流行时间，当前 36×36 蓝色配图的精确客户端版本未确认。Rage 图是 2011 年向量化材料，可表现早期 Rage Comic 传播期，但不冒充最初的 4chan 原稿。兔斯基单格来自第三方页面存档，原作者授权状态未核实。Twemoji 按 CC BY 4.0 保留署名要求。

**另外保留的年代辅助图不计入 29 个表情。** [阿狸 QQ 输入法主题预览](../art/export/fullfilm-v4-emotes/ali-2009-qqpinyin-theme-preview.png)对应官方皮肤页标注的 2009-12-11 上传日期，但它是完整皮肤预览，不是单独表情包。两张 VRChat+ 辅助图来自官方贴纸功能页；开发者更新于 2024-09-05 介绍 Stickers and Sharing，故只推荐放在 235.52–249.6 的后期交接段，不放进 2017/2018 年 VRC 初期段。VRChat 动图只取一帧做功能示例，其余 154 帧没有作为不同表情。CMU 保存的 Scott Fahlman 原始 BBS 记录日期为 1982-09-19，清单另转排 `:-)` 与 `:-(` 两个文字样式；它们明确标为本地字形重排，不声称是下载到的历史图片。

所有源图均保留原始像素与颜色。20×20 QQ GIF 只输出首个完整帧，20 个不同命名表情逐一核对导出哈希；Openclipart SVG 以 `rsvg-convert` 栅格化；大头贴、兔斯基、贴吧图片不去白底、不透明化，只记录精确裁区。已有透明通道的源图保留原 alpha。

年代依据来源包括：[QQ 表情档案及映射](https://github.com/YiJio/emoji-chinese)、[搜狐洋葱头官方网站存档页](https://dm.sohu.com/s2007/onion/index.shtml)、[百度“滑稽”词条](https://bkso.baidu.com/item/%E6%BB%91%E7%A8%BD/19219706?fromModule=lemma_inlink)、[兔斯基预览存档页](https://www.rjzxw.com/sucai/1251.html)、[Openclipart Rage Guy](https://openclipart.org/detail/142225/rage-guy)、[Openclipart Forever Alone](https://openclipart.org/detail/166561/forever-alone)、[Openclipart Trollface](https://openclipart.org/detail/146953/troll-face-problem)、[Twemoji v2.0.0](https://github.com/twitter/twemoji/releases/tag/v2.0.0)、[阿狸 QQ 输入法皮肤页](https://skin.qq.pinyin.cn/skin_detail?skin_id=1337258054)、[VRChat+贴纸功能页](https://help.vrchat.com/hc/en-us/articles/34198079589267-New-VRC-Feature-Stickers-Sharing)和 [CMU 原始 smiley 记录](https://www.cs.cmu.edu/~sef/Orig-Smiley.htm)。
