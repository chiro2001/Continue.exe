# 全片 V3：VRChat Avatar 资产归属素材

资料核对日：2026-10-17。已整理 6 个可作内部参考的公开来源，包含 7 张真实浏览器截图。逐项日期、来源 URL、核验范围、权利状态、截图路径、SHA-256 和推荐上下文见 [素材 JSON](../data/fullfilm-v3-avatar-provenance.json)。

## 可用的内部参考

| 来源 | 内容日期 | 本轮截图 | 可支持的叙事用途 |
| --- | --- | --- | --- |
| VRChat 官方 2024-07-25 开发者更新 | 2024-07-25 18:41 UTC | [截图](../art/reference/fullfilm-v3-avatar-provenance/01-2024-07-official-anti-ripping-update.png) | 平台说明反盗模保护与逾 1,900 个账号封禁。是平台自己的披露，不是独立统计。 |
| VRChat 官方 2024-08-08 开发者更新 | 2024-08-08 | [截图](../art/reference/fullfilm-v3-avatar-provenance/02-2024-08-official-ripping-definition.png) | 官方定义其所称的 ripping，并报告约 2,400 个进一步处理的账号；不要与 7 月数字直接相加。 |
| VRChat 当前服务条款第 8、13.2 节 | 页面标示生效日 2026-02-09 | [作者权利与许可](../art/reference/fullfilm-v3-avatar-provenance/03-terms-user-content-rights-and-licenses.png)、[未获许可的复制与分发](../art/reference/fullfilm-v3-avatar-provenance/04-terms-copying-without-permission.png) | 用户保留发布内容权利；公开内容对其他用户的许可受条款和平台功能约束；第 13.2(c) 禁止未经许可复制或分发他人内容。只能作为当前平台条款介绍，不能冒充旧年代条款或具体案件判决。 |
| VRChat 官方 Copyright Policy | 页面未标发文/修订日；2026-10-17 核对 | [截图](../art/reference/fullfilm-v3-avatar-provenance/05-copyright-policy-takedown-process.png) | 展示版权下架通知渠道及其身份披露风险。页面说明通知会启动法律流程，不证明某个投诉已成功。 |
| VRChat 官方 2018.4.4 更新说明 | 2018-12-20 | [Public Avatar cloning 与隐私设置](../art/reference/fullfilm-v3-avatar-provenance/06-2018-12-public-avatar-cloning-patch-notes.png) | 官方说明站内可克隆 Public Avatar、穿戴者的 Allow Avatar Cloning 设置，以及创作者可把不愿被克隆的作品设为 Private；还可打开 Avatar 作者的社交页。用于区分获准的站内分享与未经许可导出/再分发。 |
| VRChat Feedback 上的 ABOM 社区提案 | 2026-09-22 08:30 UTC | [匿名化提案截屏](../art/reference/fullfilm-v3-avatar-provenance/07-community-abom-proposal-anonymized.png) | 一位社区用户提出保存组件来源、作者、版本、依赖和许可的可选 Avatar Bill of Materials。它只是提案，没有证据表明 VRChat 已采纳。 |

页面文字和截图版权未发现可自由再发行许可。这里的“可用”指可作本地核验和创作参考，**不代表已经取得公开成片、展映或传播许可**。若成片采用真实页面截图，依 [spec/05-toolchain-and-m0.md](05-toolchain-and-m0.md) 补来源署名，并先确认可公开展示的权利依据；如无法确认，改用有来源说明的原创重建画面。

## 待核与不可用项

- **待核：2026-03-16 用户反馈。** 帖子可以证明有人在该日提交了投诉，不能证明发帖者陈述的受害范围或技术机制。本轮没有保存可展示截图。若后续需要呈现，只能标成“单一用户反馈”，截标题和日期并匿名化，不呈现机制主张。
- **不可作为数量或事实依据：2026-06-24 用户反馈。** 标题中的“数万”未独立核实，帖子带有外部链接；本轮没有截图、下载或复述链接内容。
- **未定位：小圈子未成年人事件。** 沿用 [spec/11-vrchat-community-history.md](11-vrchat-community-history.md) 与既有资料中的结论，没有找到可核验原帖，因此不纳入盗模素材，也不补造关系人、日期或行为细节。

上述两条反馈记录与具体状态已在 JSON 的 `review_queue` 中，发帖者姓名、昵称和投票者均未写入索引。

## 截图方法与叙事边界

本轮用 Selenium WebDriver 4.49.0 控制 Chromium 152.0.7977.75，`--headless=new`、`--mute-audio`，直接加载来源网页后截取真实浏览器视口；请求窗口为 1600×1400，实际视口为 1600×1257、DPR 1。没有播放音频、AI 生成或重绘页面文字。ABOM 截图通过 CSS 隐去账号名并模糊小头像，再裁掉投票者侧栏。各图捕获时刻、裁切坐标、哈希和完整命令脚本见 `~/tmp/vrc-old-web-map/fullfilm-v3/provenance/`。

叙事上可将“同意开放的克隆”放在分享与共同创作段，将平台条款、反盗模公告和 ABOM 提案放在署名消失、创作者失去控制的段落。所有官方数字都用“平台公告称”；ABOM 用“社区提案”；未核实投诉不画成已经查明的案件。不要把公开头像权限解释成源文件可任意下载，也不要让多个不同日期的页面看起来像同一时间发生。
