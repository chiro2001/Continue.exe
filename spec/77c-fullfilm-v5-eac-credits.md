# V5 EAC 画面与借物表资料

核查日期：2026-10-18。机器可读来源、署名、图片哈希与布局位于 [`data/fullfilm-v5-eac-credits.json`](../data/fullfilm-v5-eac-credits.json)。

## 当前 EAC 画面

目标机器实际安装的 Steam public 分支为 BuildID **25841461**，Steam `appmanifest_438100.acf` 记录本机于 **2026-10-11** 更新；SteamDB public manifest 列表同样记录 BuildID 25841461。项目没有运行客户端或 EAC 安装程序。

已找到一张近当前的真实启动错误窗口截图：VRChat Ask Forum 用户 **Chazv** 于 **2026-09-24** 发布的 “Launch Error” 贴图。截图为 800×450 原图，含 EAC 蓝熊与 VRChat 启动画面，SHA-256 为 `7bd092917c7cb651d23c9e3f90366f5b8f595844d265de4fbd6c64e4dd67426a`，无裁切、无修图，保存在 [`vrchat-eac-launch-error-2026-09-24.png`](../art/reference/fullfilm-v5/eac/vrchat-eac-launch-error-2026-09-24.png)。来源页未说明客户端 BuildID，因此只能称作 **2026-09-24 用户实拍启动报错状态**；它比本机 2026-10-11 的当前安装早 17 天。

当前安装包另外提供两份可核对的原始资源：[`vrchat-current-splashscreen.png`](../art/reference/fullfilm-v5/eac/vrchat-current-splashscreen.png) 是 `EasyAntiCheat/SplashScreen.png` 原文件，800×450，SHA-256 `ece4da9441acac56df4bde01605bfb3349c573b5e0dad01bde941a33162d1613`，只含 VRChat 标志与人物图，不含小蓝熊；[`eac-current-setup-icon.png`](../art/reference/fullfilm-v5/eac/eac-current-setup-icon.png) 是当前 `EasyAntiCheat_EOS_Setup.exe` 的关联图标资源，只读提取为 32×32 PNG，SHA-256 `b86181e50ab143d907589151299003cb5507880b6ae40d64f446ce85416fd841`。它们是安装包资源，不是运行中窗口截图。

建议把 9 月 24 日截图标明日期，放作 2026 年当下访问条件的后续或独立段落。若它与 112.64—122.88 秒的 2022 EAC 事件并置，画面上必须保留 2026 日期。2022-07-25 VRChat 公告的标题图和 Secure Instances 示意图是历史公告素材，不是 EAC 启动画面。不要把当前 splash 原图和图标拼成伪造的运行截图。

来源：

- [Ask VRChat Forum：Launch Error](https://ask.vrchat.com/t/launch-error-vrchat/48969)，作者 Chazv，2026-09-24。
- [SteamDB：VRChat public depot manifests](https://steamdb.info/depot/438101/manifests/)，与目标机器本地 public BuildID 交叉核对。
- [VRChat：Security Update](https://hello.vrchat.com/blog/vrchat-security-update)，2022-07-25；仅作历史公告，不作启动窗口图。

## 借物表

借物表独立于片尾标题，由主代理在世界中放置两块实体牌，PNG 不做轮播。两张图均为 2048×1152、深底浅字、四列“分类 / 项目或作品 / 作者 / 来源”：

- [第 1 页：场景、人物与工具](../art/export/fullfilm-v5/credits/credits-01-tools-and-world.png)
- [第 2 页：音乐、历史影像与来源](../art/export/fullfilm-v5/credits/credits-02-music-and-media.png)

署名以已核实的作品名和账号为准：Kipfel（もち山金魚，模型制作 かめ山，BOOTH 5813187）、Nemesis（こまいぬ通り，角色设计与模型 こぬい，BOOTH 5986971）、Room -01- 部分家具（どんぐりのお店／どんぐりちゃん，BOOTH 6370315）、lilToon 2.3.4（lilxyzw／lilLab，BOOTH 3087170，MIT）、QvPen 3.3.6（ureishi／Package Shop @aivrc，BOOTH 1555789）、QvPen Wood v1（#たくあん屋さん，BOOTH 4553516）、Noto Sans CJK SC（Noto 项目，OFL 1.1）、UdonSharp（MerlinVR 与 Contributors，MIT）。QvPen 页面列出的协作者已放在第一页底部。Room -01- 行说明实际使用的书架、书、桌与灯具，不声称整套场景被使用。

2026-10-18 补全 Kipfel 的衣装与发型署名：**眠猫せーらー【キプフェル対応髪型＋衣装】**，作者／店铺 **ねこまんま屋**，BOOTH 商品 **8379856**。经现用演员的材质 `Nemuneko_Sailor`、`Hair_kip` 和原工程 `Assets/衣服/眠眠/Prefab` 下的衣装／发型预制件交叉核对；不是仅凭外观推测。第一页在 Kipfel 行之后增加“衣装 / 发型”行，显示作者与完整 BOOTH URL。原有两块借物表的布局、显隐时机保留。

模型与系统来源页： [Kipfel](https://booth.pm/ja/items/5813187)、[眠猫せーらー：Kipfel 衣装与发型](https://booth.pm/ja/items/8379856)、[Nemesis](https://booth.pm/ja/items/5986971)、[Room -01-](https://booth.pm/ja/items/6370315)、[lilToon](https://github.com/lilxyzw/lilToon)、[QvPen](https://booth.pm/ja/items/1555789)、[QvPen Wood v1](https://booth.pm/ja/items/4553516)、[UdonSharp](https://github.com/vrchat-community/UdonSharp)、[Noto CJK](https://github.com/notofonts/noto-cjk)。

本片原创场景、模型、图稿与 OWFilmV4/OWV5 Shader 署名为 **Chiro（Unity、Blender、ChatGPT 辅助）**。音乐行按项目资料记录 Rhino of Torment、Cytax／I.C.E. 2 与 EDGE Cracking Group；不添加未经查明的曲名。《6分钟看完VRChat历史》作者为 **一个一个hs**，Bilibili BV1brH769EtM，2026-10-04；EAC 启动报错截图单独署名 Chazv 与 2026-09-24 来源。

表格不显示仓库路径或内部数据文件名；逐项来源 URL、时间线、作者、许可依据与素材校验值留在 [`data/fullfilm-v5-emotes.json`](../data/fullfilm-v5-emotes.json) 与 EAC/署名总清单中。历史表情按四个有来源的阶段署名：phpBB 3.0.8（8 枚，官方发布 2010-11-20，项目 phpBB Project，使用 51.2—71.68 秒）；JB51 目录中的贴吧泡泡预览（14 格，目录更新 2014-09-11，使用 71.68—86.4 秒）；Tieba Android 11.6.8.2 快照（8 枚，提取者 microlong666，README 标注 2020-07-13，使用 86.4—112.64 秒）；百度贴吧 Android 22.12.1.0 默认预设（32 枚，应用更新 2026-09-24，使用 112.64—186.88 秒及 235.52—249.6 秒）。2014-09-11 是目录页更新时间，不是图包创作日期；原包作者和原始首发未确认，展示只署目录预览来源。2020 版按原文件编号标注，不推测表情名称。2026 图像取自当前贴吧 APK 默认 preset。授权限制保留在资料记录，不作为观众面板上的待核注释。
