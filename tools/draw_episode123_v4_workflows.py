#!/usr/bin/env python3
"""Author distinct, source-led operation screens. Provenance lives in metadata, while the depicted screen keeps period-appropriate user language."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,hashlib,math
ROOT=Path(__file__).resolve().parent.parent
OUT=ROOT/'art/export/episode123-v4-workflows';OUT.mkdir(parents=True,exist_ok=True)
FONT=Path.home()/'tmp/vrc-old-web-map/episode123/fonts/simsun.ttc'
MONO='/usr/share/fonts/noto/NotoSansMono-Regular.ttf'
def f(n=14):return ImageFont.truetype(str(FONT),n)
def m(n=13):return ImageFont.truetype(MONO,n)
# Each state changes the operation, information and output, not just a progress number.
# Key, domain, visual family, category, scene range, four distinct operations and contents.
WORK=[
('dial','接通网络','terminal','desktop',[0,10.24],[('拨号连接','ATZ|OK|ATDT 163|CONNECT 33600|正在验证用户名和密码'),('分配地址','PPP negotiation|TCP/IP installed|远程计算机已连接|发送: 136 字节   接收: 284 字节'),('测试连接','C:\\> ping 202.96.128.86|Reply: bytes=32 time=184ms TTL=242|Packets: Sent=4 Received=4 Lost=0'),('第一次响应','C:\\> dir A:\\|README.TXT   1,024|HOME.HTM   8,192|1 个目录   2 个文件|C:\\> edit home.htm')]),
('explorer','我的电脑','folder','desktop',[4,16],[('打开软盘','A:\\|README.TXT|主页素材|音乐|我的第一个网页'),('整理目录','C:\\MYWEB\\|index.htm|images\\|guestbook.htm|links.htm'),('复制文件','从 A:\\网页素材 复制到 C:\\MYWEB|index.htm|mail.gif|button.gif|正在复制网页需要的文件'),('查看属性','index.htm 属性|类型: HTML 文档|大小: 8.00 KB|打开方式: Internet Explorer|存档: 是')]),
('portable','解压即用','folder','tools',[10.24,20.48],[('压缩包内容','tools.zip - 压缩文件管理器|editor.exe|readme.txt|license.txt|plugins\\'),('先看说明','README.TXT - 记事本|无需安装，解压到独立目录。|保留作者说明与许可。|设置保存在程序目录。'),('选择工具','工具设置|文本编码: GB2312|制表符: 4|换行: CRLF|自动保存: 开启'),('致谢','About / Credits|Programming / Artwork / Music|感谢维护者与翻译者。|作者信息随文件一起保留。')]),
('winamp','播放列表','player','tools',[10.24,30.72],[('加入音乐','WINAMP|OPEN FILE|demo.mod|track01.xm|tune.fc14'),('编辑列表','PLAYLIST EDITOR|01. Intro|02. Pattern loop|03. Four voices|04. End'),('均衡器','GRAPHIC EQUALIZER|PREAMP|60 170 310 600 1K 3K 6K 12K|音乐从很小的文件里响起'),('保存列表','SAVE PLAYLIST|Filename: night.m3u|M3U playlist|Relative paths|与朋友交换播放列表')]),
('homepage','个人主页','web','web',[16,30.72],[('写 HTML','<html>|<title>我的小站</title>|<body bgcolor="#000080">|<h1>欢迎光临</h1>|<a href="guestbook.htm">留言簿</a>'),('浏览预览','我的小站|欢迎光临|本站最后更新: 2001-06-16|文章 · 音乐 · 留言簿|你是第 000128 位访客'),('上传目录','FTP 文件传输|index.htm  →  /public_html/|images/ → /public_html/images/|传输完成|226 Transfer complete'),('收到留言','留言簿|访客: 夜行人|这个页面很可爱，链接可以交换吗？|站长回复: 已经加上了！|返回首页')]),
('paint','一像素一像素','paint','creation',[20.48,40.96],[('画图','画图 - 未命名|铅笔工具|16×16 图标|调色板: 16 色|缩放: 800%'),('编辑纹理','texture.bmp|32×32 Tile|重复平铺检查|修正接缝|背景像素与前景像素分层'),('另存为','保存为|文件名: home.bmp|24 位位图|保存源文件|导出网页用 GIF'),('看见结果','index.htm - 预览|新的小房子图标|站点导航已有自己的样子|原始文件留在本地|下一个图标继续画')]),
('manga','漫画汉化','comic','translation',[30.72,40.96],[('整理原稿','原稿 03 / 12|扫描与裁切|修正倾斜与黑边|保持台词和画面位置|第 03 页'),('翻译台词','台词表|001 今日はここに。 → 今天就在这里。|002 手伝うよ。 → 我来帮你。|003 また明日。 → 明天见。'),('清字嵌字','图层|对白文字|气泡清理|原稿备份|校对: 断句与标点'),('合并发布','成品检查|页序正确|没有漏字|保留翻译/校对/嵌字署名|导出 012 张图片')]),
('game','游戏汉化','hex','translation',[30.72,43.52],[('抽取脚本','script_01.txt|0001 Start Game|0002 Continue|0003 Options|0004 Exit'),('文本对照','原文                  译文|Start Game            开始游戏|Continue              继续|Options               设置|Exit                  退出'),('字库与测试','font.tbl|开 始 游 戏 继 续|检查字宽与溢出|修正缺字 □|测试菜单与存档'),('打包修正','Patch notes|修正第 3 章漏译|新增字体回退|校对: A / 翻译: B / 测试: C|请保留说明文件')]),
('subtitles','字幕时间轴','timeline','fansub',[40.96,51.2],[('听译','01 00:00:02.120 → 00:00:04.600|今天也一起做一点。|02 00:00:05.000 → 00:00:07.440|这句我来校对。|音频与原文对照'),('对轴','起始: 0:00:02.120|结束: 0:00:04.600|入点对齐辅音|出点留出阅读时间|检查相邻字幕间隔'),('校对','检查列表|专有名词统一|不要遮挡画面|每行字数与停留时间|错字修正后再次预览'),('导出字幕','Export subtitles|UTF-8|ASS Advanced SubStation Alpha|movie.zh-cn.ass|翻译 / 时间轴 / 校对 / 特效')]),
('typeset','卡拉与特效','timeline','fansub',[40.96,56.32],[('逐音节','{\\k24}一{\\k16}起{\\k32}做{\\k40}下{\\k24}去|音节边界|重听这个词|先保证内容准确'),('设定样式','样式管理器|Default / Song / Sign|字体: 宋体|描边: 2|主色: 白色  次色: 金色'),('运动预览','视觉排版|\\pos(320,420)|\\fad(120,240)|匹配标牌透视|不要盖住人物表情'),('完整检查','播放预览|台词与歌词分层|特效不抢对白|片尾保留成员署名|发布前最后一遍')]),
('torrent','BitTorrent','transfer','distribution',[51.2,64],[('制作种子','Create Torrent|文件: night.mkv|Piece size: 256 KB|保留作品说明|生成 .torrent'),('下载区块','Pieces|绿色: 已完成|蓝色: 正在接收|白色: 等待|区块来自不同的参与者'),('校验','Hash check|piece 001 OK|piece 002 OK|piece 003 OK|文件完整性验证通过'),('继续做种','Seeding|Uploaded: 128 MB|Ratio: 1.24|连接: 12 个节点|收到了，也继续传出去')]),
('emule','电驴的共享目录','transfer','distribution',[51.2,66.56],[('搜索文件','搜索|档案 / 自制作品 / 字幕|类型: 文档|可用源|先核对文件名与大小'),('排队等待','传输|排队: 182|可用源: 24|完成块: 18 / 64|预计剩余时间不断变化'),('共享目录','共享文件|原创教程.pdf|字幕样例.ass|站点备份.zip|散列与文件大小'),('找回缺块','恢复下载|重新搜索来源|最后一个区块有了回应|散列校验通过|文件完整')]),
('release','发布与署名','web','distribution',[51.2,71.68],[('发布页','作品发布|标题: 原创短片与制作记录|说明: 修正上一版字幕|文件列表|翻译 / 校对 / 时间轴 / 维护'),('NFO','RELEASE NOTES|ARTWORK : LOCAL TEAM|TRANSLATION : A+B|TIMING : C|THANKS TO EVERYONE'),('补档','补档说明|原链接失效|已补充镜像与校验值|保留原发布信息|感谢报告问题的朋友'),('回应','发布页回复|1楼: 收到了，谢谢！|2楼: 第三句有个错字|楼主: 已更新到 v2|下载后请保留说明')]),
('bbs','论坛的互助','web','chinese-web',[61.44,81.92],[('求助帖','制作交流区|主题: 贴图为什么变成粉色？|我试了几个 Shader，仍然不对。|附件: 编辑器截图|等待回复'),('回帖','Re: 贴图为什么变成粉色？|先确认项目的渲染管线。|再检查材质引用和控制台。|可以先做一个最小场景。|附件: 检查清单'),('贴出结果','Re: 问题解决了|找到缺失的 Shader 了。|重新指定后正常。|把解决方法写在这里，方便后来的人。'),('精华整理','本周精华|贴图导入常见问题|骨骼与手势入门|世界光照检查表|失效链接补档')]),
('webring','友情链接','web','web',[16,35.84],[('交换链接','友情链接|夜航小站|像素仓库|译者日记|个人主页互助圈'),('收集书签','收藏夹|个人主页|字体与绘画|音乐与 Tracker|世界制作教程'),('网页横幅','88×31 链接按钮|请使用本地副本|不要盗链|小小图片也能指向一个人'),('下一站','Web Ring|<< 上一站     随机一站 >>|本站没有广告|欢迎留下你的地址|链接一个新的房间')]),
('qq','QQ 的另一端','im','chinese-web',[61.44,81.92],[('上线','我的好友|在线好友 (3)|同学|网友|制作群|离线好友'),('发消息','消息记录|我: 还在吗？|朋友: 在，刚刚下好。|我: 有个地方想请你看一下。|朋友: 发过来吧。'),('传文件','发送文件|model_preview.png|接收方已同意|传输完成|打开文件夹'),('群里有人回应','制作交流群|A: 这里可以这样改|B: 我补了一张示意图|C: 我来测试|文件已保存到本地')]),
('msn','MSN','im','chinese-web',[64,81.92],[('联系人','MSN Messenger|Available|Friends (4)|Away (2)|Offline (8)'),('对话','A conversation|我: 晚上有空吗|朋友: 有，正好在画贴图|我: 一起看看这个场景|朋友: 好 :)'),('表情与涂鸦','手写消息|不只是文字|小涂鸦与表情|一个人的笔迹|跨过屏幕来到这里'),('保存记录','Save Conversation|conversation.txt|聊天记录|留下解决问题的方法|也留下当时的语气')]),
('mail','邮件列表','mail','web',[10.24,51.2],[('写信','To: list@example.invalid|Subject: 制作进展|附件: preview.png|这次修好了手部姿势。|期待大家的反馈。'),('收件箱','Inbox|Re: 制作进展|Re: 字幕修正|[list] 本周聚会|[list] 发布新版本'),('附件','Attachments|source.zip|readme.txt|checksum.txt|源文件与说明一起发出'),('邮件归档','Mailing list archive|按主题浏览|按日期浏览|制作问题与回复|多年以后仍然可以检索')]),
('rss','订阅与保存','web','web',[61.44,81.92],[('加入订阅','RSS Reader|订阅地址: /feed.xml|制作日志|开源项目更新|字幕发布'),('列表更新','All items|新的制作教程|更新日志 v1.2|译者的工作日记|订阅内容来自各处'),('阅读原文','文章预览|一个材质问题的解决过程|作者保留了失败的尝试|旁边还有其他人的补充|点击查看原文'),('离线保存','Archive|文章正文|原始链接|发布时间|保存为本地 HTML')]),
('tianya','天涯社区','web','chinese-web',[61.44,74.24],[('版块','天涯社区|天涯杂谈   贴图专区|煮酒论史   旅游休闲|版主公告|欢迎回到天涯。'),('主题列表','帖子列表|深夜，谁还在线？|写给十年后的自己|求助：以前收藏的网址打不开了|看过的帖子又有人回复'),('楼中回应','只看楼主|楼主: 这段记忆还有人记得吗|回帖: 我存过一份|回帖: 链接在这里|更多回复'),('收藏与归档','收藏夹|一个长帖的许多页|有人整理目录|有人补充图片|有人把它留了下来')]),
('xiaonei','校内网','profile','chinese-web',[64,76.8],[('个人主页','校内网|最近状态|同学 / 相册 / 日志|今天在机房待到很晚|欢迎回来。'),('相册','相册: 毕业之前|教室|宿舍|一起做过的东西|上传 12 张照片'),('班级','我的班级|同班同学|班级留言板|下周聚会时间|每个人的近况'),('邀请','好友请求|来自同一所学校|一起维护班级相册|通过申请|多了一条连接')]),
('renren','人人网','profile','chinese-web',[66.56,79.36],[('新鲜事','人人网|好友新鲜事|上传了一张新照片|分享了一篇日志|欢迎回来。'),('分享日志','分享|我整理的制作教程|这条评论下面还有补充|朋友的朋友也看到了|返回新鲜事'),('评论','评论列表|A: 我也遇到了|B: 这个步骤有新版|C: 已经更新，谢谢！|继续回复'),('活动','共同活动|周末一起做个小场景|参与 12 人|相册与讨论|发布新的进度')]),
('weibo','微博','profile','chinese-web',[69.12,81.92],[('发布','新浪微博|有什么新鲜事想告诉大家？|今天做完了第一个模型。|还可以输入 123 字|'),('转发','转发|这个教程很清楚|原作者: 制作过程记录|带着原始链接|转发 12  评论 8'),('话题','搜索话题|#制作日记#|来自不同人的小成果|有人刚刚开始|有人愿意解释'),('私信回应','私信|谢谢你的教程。|我按着做出来了。|这里还有个小问题……|下一次交流开始')]),
('tieba','百度贴吧','web','chinese-web',[64,81.92],[('搜索进吧','百度贴吧|搜索一个共同的兴趣|世界制作吧|主题帖 / 精品区|欢迎回来。'),('楼层','主题: 从零做一个小房间|1楼: 先建一个方块|2楼: 我来补充灯光|3楼: 收藏慢慢看|下一页'),('ASCII','顶帖与文字画|+--------------------+|  一起做点什么吧|+--------------------+|帖子又被顶上来了'),('整理索引','精品区|入门教程索引|素材授权说明|失效链接反馈|版务与更新记录')]),
('blog','博客','web','chinese-web',[61.44,79.36],[('新建文章','写日志|标题: 一个小场景的诞生|标签: 制作 / 学习|插入图片|保存草稿'),('长文','制作日志|从一个简单方块开始|试了三种光照|最后还是用了最朴素的方案|附上参数和失败截图'),('评论','读者留言|试过了，很有用。|你这个地方能否再说详细些？|作者回复: 补了一张图。|下次更新见'),('博客卷','友情链接与归档|2007 年 6 月|2007 年 5 月|朋友们的制作日志|下一篇仍在写')]),
('danmaku','弹幕共同观看','player','chinese-web',[69.12,81.92],[('开始播放','早期弹幕播放器|载入视频|弹幕列表|缓冲中|0:00 / 1:30'),('发送弹幕','发送弹幕|字体: 小号|颜色: 白色|位置: 滚动|这段旋律一响就想起来了'),('同屏回应','弹幕列表|前方高能|这段我记得！|有人在一起看|字幕组辛苦了'),('保存时间','播放结束|视频还会被下一位观众打开|弹幕按时间重新出现|不同时间的人|在同一秒钟回应')]),
('flash','Flash 与创作','timeline','creation',[20.48,40.96],[('关键帧','动画时间轴|图层: 背景 / 人物 / 文字|关键帧 01 12 24 36|元件库|第一次让画面动起来'),('元件','Library|小房子|角色行走|按钮|循环音乐|每个元件可以重复使用'),('预览','Test Movie|播放 / 停止|循环播放|时间轴在继续|画面和声音对齐'),('发布','Publish Settings|HTML|SWF|保留源工程 FLA|下一次继续修改')]),
('opensource','开源与维护','hex','creation',[92.16,102.4],[('问题单','Issue #128|标题: 新版导入失败|复现步骤|预期结果|实际日志|附件截图'),('补丁','diff --git a/import.cs b/import.cs|- LoadTexture(path);|+ if (File.Exists(path))|+     LoadTexture(path);|修正缺失资源判断'),('代码评审','Review|这处需要兼容旧版本|补充了一条边界检查|感谢测试与说明|Changes approved'),('发行说明','Release v1.2|修复导入|保留旧版本兼容|新增翻译|维护者与贡献者名单')]),
('world','世界制作','editor','creation',[20.48,30.72],[('搭建','Hierarchy|WorldRoot|  Floor|  Window|  Desk|  Light|从人尺度开始'),('UV 与贴图','Inspector|Mesh Renderer|Albedo: wall.png|Tiling: 2 × 2|把像素贴进立体空间'),('光照','Lighting|Baked GI|Lightmap size: 512|检查接缝和阴影|一盏灯让房间有人气'),('测试','Build & Test|检查出生点|检查碰撞|检查音频|进入自己做的地方')]),
('avatar','Avatar 制作','editor','vrc',[23.04,35.84],[('骨骼','Humanoid Rig|Head / Spine / Hips|LeftArm / RightArm|LeftLeg / RightLeg|同一套动作可以换身体'),('表情','Blend Shapes|Smile|Blink|Mouth_A|表情让对话有了语气'),('材质','Avatar Materials|脸部与衣服分层|贴图 / 颜色 / 阴影|同一个身体也能有不同样子|保留原作者署名'),('手势','Gesture Preview|Idle / Point / Offer|检查双手分离|检查手腕朝向|保存手势预览')]),
('vrc17','VRChat 2017','vrc','vrc',[81.92,87.04],[('Worlds','VRCHAT / WORLDS|PUBLIC WORLDS|THE HUB|NEW WORLDS|COMMUNITY EVENTS'),('Avatars','VRCHAT / AVATARS|SELECT AVATAR|PUBLIC|FAVORITES|换个身体再回来'),('Social','VRCHAT / SOCIAL|FRIENDS ONLINE|JOIN WORLD|INVITE|同一个空间里见面'),('Travel Map','THE HUB / TRAVEL MAP|AVATAR HALL|MIRROR|EVENTS|WORLD PORTALS')]),
('vrc19','VRChat 2019','vrc','vrc',[87.04,92.16],[('Home','VRCHAT HOME / 2019|AVATAR MIRROR|PERSONAL SPACE|WORLD PORTAL|先在这里整理自己'),('Platforms','CROSS-PLATFORM|PC + QUEST|共同支持的世界|可进入的入口发生变化|2019 官方材料参考'),('Campfire','THE HUB / CAMPFIRE|圆形草坪|原木座位|世界入口|人们围坐下来'),('Gather','FRIENDS IN THIS WORLD|同一世界的不同身体|邀请 / 加入 / 交谈|空间里有人回应|共同在场')]),
('virus','出错与恢复','dialog','security',[92.16,102.4],[('异常','系统提示|程序遇到问题，需要关闭。|保存尚未提交的文件。|错误详情|不要把错误当作结局'),('扫描','安全检查|可疑文件已隔离|保留日志|检查备份|已完成安全检查'),('求助','求助记录|刚刚的文件打不开了。|朋友: 先别覆盖原文件。|我这里还有一份备份。|一起检查'),('恢复','恢复完成|源文件完好|重新保存到新目录|补上校验和与说明|继续制作')]),
('3q','入口与选择','dialog','conditions',[102.4,112.64],[('公开信','2010 年 11 月 / 3Q|软件之间的冲突进入用户桌面|请选择要继续使用的程序|你的联系人和聊天记录仍在这里|留意用户被迫面对的选择'),('冲突提示','兼容性提示|一个入口要求另一个退出|通讯录仍在这里|对话被打断|桌面变成选择题'),('切换工具','更换程序|导出联系人|保存聊天记录|重新通知朋友|便利也带来迁移成本'),('联系人还在','消息窗口|换个地方还能找到你吗？|可以，把地址发给我。|先保存这份名单。|连接依赖具体的人')]),
('eac','VRChat 条件改变','vrc','conditions',[110.08,122.88],[('安全更新','VRCHAT / 2022-07|Security Update|Easy Anti-Cheat|客户端需要更新|重新启动后生效'),('既有工具','访问与工具|社区曾经自行补齐一些能力|字幕 / 辅助 / 界面功能|安全与使用需求同时存在|遇到问题，请查看帮助'),('设置变化','新的设置入口|可访问性|音频与字幕|用户界面|变化会改变日常动作'),('重新进入','JOIN WORLD|更新客户端|检查兼容|重新找到朋友|入口还在，条件变了')]),
('maintain','继续可用','hex','maintenance',[122.88,133.12],[('链接失效','HTTP/1.1 404 Not Found|旧书签仍然在|图片不再加载|搜索引擎还有标题|正文已经消失'),('依赖变化','Build log|Missing dependency|API changed|Shader unavailable|旧工程需要重新维护'),('有人维护','Issue response|我还有源文件。|可以一起整理。|先把能运行的版本留住。|还有工作正在继续'),('灯还亮着','maintenance.log|已保存本地副本|已整理旧目录|已修好失效的链接|下一次从这里继续')])
]

def txt(d,xy,s,n=14,c='#111',mono=False):
    # Bilevel glyph masks preserve the pixel-rendered Windows text character.
    font=m(n) if mono else f(n);box=d.textbbox((0,0),str(s),font=font)
    if not s:return
    mask=Image.new('L',(max(1,box[2]+2),max(1,box[3]+2)));md=ImageDraw.Draw(mask);md.text((0,0),str(s),font=font,fill=255)
    mask=mask.point(lambda p:255 if p>90 else 0)
    d._image.paste(Image.new('RGB',mask.size,c),tuple(map(int,xy)),mask)
def bevel(d,b,fill='#c0c0c0',inset=False):
    d.rectangle(b,fill=fill);a,z=('#808080','#ffffff') if inset else ('#ffffff','#808080');x,y,r,t=b
    d.line([(x,t),(x,y),(r,y)],fill=a,width=1);d.line([(r,y),(r,t),(x,t)],fill=z,width=1)
def chrome(title,xp=False,browser=False):
    im=Image.new('RGB',(800,540),'#c0c0c0');d=ImageDraw.Draw(im);bevel(d,(0,0,799,539))
    for y in range(3,28):
        p=(y-3)/25;d.line((3,y,796,y),fill=(int(6+p*17),int(38+p*56),int(142+p*70)) if xp else (0,0,128))
    txt(d,(27,7),title,16,'white');d.rectangle((8,8,20,21),fill='#f2d67d');d.rectangle((9,9,19,12),fill='#2582a0')
    for i,ch in enumerate(['_','□','×']):
        x=736+i*19;bevel(d,(x,7,x+16,23));txt(d,(x+3,8),ch,12)
    txt(d,(9,35),'文件(F)  编辑(E)  查看(V)  工具(T)  帮助(H)',13)
    d.line((5,57,794,57),fill='#808080');d.line((5,58,794,58),fill='white')
    top=65
    if browser:
        for x,t in [(12,'← 后退'),(95,'→'),(139,'停止'),(198,'刷新'),(257,'主页'),(327,'搜索'),(397,'收藏')]:
            bevel(d,(x,65,x+71,93));txt(d,(x+5,72),t,13)
        txt(d,(12,108),'地址(D)',12);bevel(d,(68,102,758,124),'white',True);txt(d,(75,107),'http://'+title.split(' - ')[0]+'/',12,mono=True);top=135
    bevel(d,(6,top,793,513),'white',True);bevel(d,(5,519,794,535),'#c0c0c0',True)
    txt(d,(12,521),'就绪',11,'#555555');txt(d,(654,521),'完成',11,'#333333')
    return im,d,top
def lines(d,items,x=215,y=110,n=18,gap=42,c='#171717'):
    for i,t in enumerate(items):txt(d,(x,y+i*gap),t,n,c)
def comic(d,top,stage):
    d.rectangle((18,top+12,390,503),fill='#e9e8df');d.rectangle((32,top+25,374,255),fill='white',outline='black',width=3)
    d.rectangle((32,270,195,489),fill='white',outline='black',width=3);d.rectangle((208,270,374,489),fill='white',outline='black',width=3)
    for x,y,r in [(130,155,39),(294,389,34),(111,402,29)]:
        d.ellipse((x-r,y-r-45,x+r,y-r+31),fill='#f5f3e8',outline='black',width=3)
        d.polygon([(x-r-12,y-r-37),(x-r+3,y-r-73),(x-r+24,y-r-46)],fill='#444');d.polygon([(x+r-15,y-r-48),(x+r+7,y-r-73),(x+r+12,y-r-22)],fill='#444')
        d.line((x-25,y-32,x-10,y-30),fill='black',width=3);d.line((x+10,y-30,x+25,y-32),fill='black',width=3)
        d.polygon([(x-36,y-5),(x+36,y-5),(x+50,y+75),(x-48,y+75)],fill='#555',outline='black')
    d.ellipse((192,top+40,360,top+131),fill='white',outline='black',width=2)
    txt(d,(208,top+68),'我来帮你。' if stage>1 else '手伝うよ。',18)
    for x in range(35,370,12):d.line((x,250,x+45,225),fill='#aaa')
def draw(work,stage):
    key,name,family,cat,span,states=work;action,body=states[stage];items=body.split('|')
    im,d,top=chrome(name+' - '+action,xp=family in ['im','timeline','editor','vrc'],browser=family in ['web','profile'])
    if family=='terminal':
        d.rectangle((8,top+2,791,510),fill='#080b0b');txt(d,(22,top+17),'Microsoft(R) Windows 98  [Version 4.10.1998]',14,'#c7c7c7',True)
        lines(d,items,22,top+65,17,44,'#b8e7bd');txt(d,(22,474),'C:\\> _',17,'#c8c8c8',True)
    elif family=='comic':
        comic(d,top,stage);txt(d,(415,top+20),action,24,'#000080');lines(d,items,415,top+77,17,44)
        for i,t in enumerate(['图层 1  原稿','图层 2  清字','图层 3  台词','图层 4  校对']):
            bevel(d,(420,350+i*30,771,378+i*30),'#d5d7db');txt(d,(431,356+i*30),t,13)
    elif family=='paint':
        d.rectangle((15,top+10,71,452),fill='#c0c0c0')
        for i in range(16):bevel(d,(19+(i%2)*25,top+16+(i//2)*31,42+(i%2)*25,top+43+(i//2)*31));txt(d,(24+(i%2)*25,top+21+(i//2)*31),['P','L','O','T','+','/','[]','*'][i%8],12)
        d.rectangle((91,top+12,500,466),fill='#f3f1e6')
        for x in range(96,497,16):d.line((x,top+20,x,458),fill='#d7d7d7')
        for y in range(top+20,462,16):d.line((95,y,498,y),fill='#d7d7d7')
        # A hand-authored 16px house motif, shown as an editable pixel object.
        for yy in range(14):
            for xx in range(16):
                roof=yy<6 and abs(xx-7.5)<yy+1;wall=6<=yy<13 and 3<=xx<=12;door=9<=yy<13 and 7<=xx<=9
                if roof or wall:d.rectangle((154+xx*16,top+50+yy*16,168+xx*16,top+64+yy*16),fill='#8b4155' if roof else '#967247' if door else '#dfbd70')
        lines(d,items,530,top+30,16,47)
        for i,c in enumerate(['#000','#808080','#800000','#808000','#008000','#008080','#000080','#800080','#fff','#ddd','#f00','#ff0','#0f0','#0ff','#00f','#f0f']):bevel(d,(95+i*25,475,117+i*25,497),c)
    elif key=='winamp':
        d.rectangle((10,top+4,790,510),fill='#171921')
        d.rectangle((26,top+19,453,top+184),fill='#464953',outline='#b6bac0',width=3)
        txt(d,(40,top+28),'WINAMP',19,'#c7c9b0',True);d.rectangle((40,top+62,228,top+133),fill='#101c18')
        txt(d,(52,top+78),'00:0'+str(stage+1),29,'#a0f798',True)
        for j in range(13):d.rectangle((252+j*13,top+130-(j*11+stage*7)%54,260+j*13,top+133),fill='#9fe36c')
        for j,t in enumerate(['|◀','▶','Ⅱ','■','▶|','↑']):bevel(d,(43+j*61,top+148,95+j*61,top+173),'#737985');txt(d,(56+j*61,top+152),t,13,'#d5dacb')
        txt(d,(482,top+25),action,24,'#d3d8bd');lines(d,items,482,top+83,16,30,'#c8dfb8')
        d.rectangle((26,top+206,453,top+390),fill='#292e35',outline='#b3b8bc',width=3)
        txt(d,(40,top+215),'GRAPHIC EQUALIZER',14,'#c7c9b0',True)
        for j in range(10):
            x=52+j*39;d.line((x,top+250,x,top+346),fill='#111',width=5);bevel(d,(x-9,top+277+(j%3)*12,x+9,top+287+(j%3)*12),'#a4a795');txt(d,(x-11,top+361),['60','170','310','600','1K','3K','6K','12K','14K','16K'][j],10,'#b8c5ac',True)
        d.rectangle((474,top+245,774,top+390),fill='#0a150b',outline='#6d7268');lines(d,['01. Intro','02. Four voices','03. Pattern loop','04. End'],488,top+263,14,28,'#9cdd70')
    elif family in ['timeline','player']:
        dark=family=='player';d.rectangle((15,top+10,492,top+213),fill='#151b27');d.rectangle((33,top+30,473,top+192),outline='#57738b')
        for i in range(30):
            x=34+i*14;v=math.sin(i*2.3+stage)*math.cos(i*.23)*65;d.line((x,top+100-v,x,top+100+v),fill='#9dcc61',width=3)
        txt(d,(39,top+164),'00:00:02.120     →     00:00:04.600',12,'white',True)
        lines(d,items,514,top+15,15,36)
        for row in range(7):
            y=top+230+row*26;d.rectangle((17,y,782,y+25),fill='#cdd9ec' if row==stage+1 else '#f4f4f4',outline='#afb5c0')
            txt(d,(24,y+5),f'{row+1:02d}    0:00:{row*3+1:02d}.120     0:00:{row*3+3:02d}.600',12,mono=True);txt(d,(380,y+5),['Default','Sign','Song'][row%3]+'   '+['今天也一起做一点。','这句我来校对。','谢谢，你的修改收到了。'][row%3],12)
        d.line((160+stage*92,top+10,160+stage*92,top+213),fill='#e29d24',width=2)
    elif family=='folder':
        for j,t in enumerate(['后退','前进','向上','复制','粘贴','删除']):bevel(d,(14+j*80,top+8,88+j*80,top+41));txt(d,(27+j*80,top+18),t,13)
        bevel(d,(15,top+53,782,top+80),'white',True);txt(d,(26,top+61),items[0],13)
        d.rectangle((15,top+88,188,503),fill='#fff',outline='#888');lines(d,['桌面','  我的电脑','    3.5 软盘(A:)','    本地磁盘(C:)','      MYWEB','      TOOLS','  网上邻居','  回收站'],26,top+105,13,28)
        d.rectangle((197,top+89,782,top+114),fill='#c0c0c0');txt(d,(205,top+96),'名称                         大小      类型       修改时间',12)
        for j,t in enumerate(items[1:]+['readme.txt','license.txt','backup','images']):
            y=top+130+j*34
            if y>484:break
            d.rectangle((208,y,223,y+17),fill='#fff5cc',outline='#777');d.rectangle((209,y+2,222,y+5),fill='#d5b457');txt(d,(235,y+2),t[:44],14);txt(d,(556,y+2),str((j+1)*4)+' KB',12);txt(d,(649,y+2),'文件',12)
        if stage==2:
            bevel(d,(278,top+192,704,top+342));txt(d,(294,top+210),'正在复制文件……',17);bevel(d,(298,top+254,681,top+280),'#eee',True)
            for j in range(17):d.rectangle((302+j*19,top+258,317+j*19,top+276),fill='#000080')
            txt(d,(298,top+302),'从 A:\\  到 C:\\MYWEB',13)
    elif family=='transfer':
        d.rectangle((16,top+9,158,505),fill='#edf3fb');lines(d,['全部任务','正在下载','已完成','共享文件','服务器','统计'],29,top+26,14,38)
        txt(d,(181,top+16),action,23,'#000080');lines(d,items,183,top+70,17,34)
        txt(d,(184,top+265),'已完成区块',13)
        for yy in range(5):
            for xx in range(27):
                done=(xx*17+yy*9+stage*13)%31;d.rectangle((185+xx*21,top+290+yy*14,204+xx*21,top+301+yy*14),fill='#5c9ed5' if done>7 else '#53ad75' if done<4 else '#ddd')
        txt(d,(184,top+384),'文件名                 大小       状态     来源',12)
        for i,t in enumerate(['原创短片.mkv','字幕与说明.zip','制作笔记.pdf']):txt(d,(184,top+411+i*21),t+'          '+['128 MB    正在接收    24','4 MB      已完成      12','2 MB      做种中      8'][i],12)
    elif family in ['editor','hex']:
        d.rectangle((16,top+10,177,505),fill='#e6e7e8');lines(d,['Project','  Assets','    Scripts','    Materials','    Textures','  Scene','Console'],27,top+25,13,26)
        d.rectangle((190,top+10,577,358),fill='#272e36');
        if family=='editor':
            for q in range(9):d.line((200,340-q*18,565,340-q*18),fill='#45505b')
            for q in range(9):d.line((385,top+65,200+q*44,355),fill='#45505b')
            d.polygon([(302,top+109),(400,top+76),(475,top+128),(375,top+163)],fill='#9faab8',outline='#dbe4ef');d.polygon([(302,top+109),(375,top+163),(375,top+248),(302,top+194)],fill='#526878',outline='#dbe4ef');d.polygon([(375,top+163),(475,top+128),(475,top+212),(375,top+248)],fill='#7d90a1',outline='#dbe4ef')
            d.line((375,top+162,375,top+55),fill='#6bd46b',width=3);d.line((375,top+162,516,top+188),fill='#df6060',width=3)
        else:lines(d,items,201,top+30,14,38,'#c9e0b8')
        lines(d,['Inspector',action,'Position  0  0  0','Scale     1  1  1','Apply / Revert'],595,top+22,13,37)
        d.rectangle((191,371,780,504),fill='#eee');lines(d,items[:4] if family=='editor' else ['Console','No errors','修改已保存','可以继续测试'],202,380,14,26)
    elif family=='vrc':
        d.rectangle((8,top+2,791,511),fill='#11242c');d.rectangle((22,top+14,778,top+58),fill='#26424d');txt(d,(38,top+24),items[0],25,'#f4f6f4')
        for i in range(3):
            x=31+i*250;d.rectangle((x,top+83,x+233,top+265),fill=['#4b5966','#65544f','#44645d'][i]);d.ellipse((x+71,top+105,x+165,top+197),outline='#58d9e8',width=5);txt(d,(x+22,top+232),items[min(i+1,len(items)-1)],17,'white')
        lines(d,items[1:],40,top+303,20,35,'#9fe2df');bevel(d,(570,448,753,490),'#356a75');txt(d,(614,462),'JOIN / SELECT',16,'white',True)
    elif family in ['im','mail']:
        d.rectangle((16,top+11,180,506),fill='#edf5fa');lines(d,['在线联系人','  夜行人','  小站维护者','  译者 A','  时间轴 B','离线联系人'],27,top+30,14,35)
        txt(d,(204,top+20),action,24,'#164a80');lines(d,items,206,top+80,18,46)
        bevel(d,(201,397,779,465),'#fff',True);txt(d,(215,417),'输入消息……',15,'#666');bevel(d,(669,473,778,500));txt(d,(706,478),'发送',14)
    elif family=='dialog':
        d.rectangle((8,top+1,792,511),fill='#008080');bevel(d,(118,top+46,682,top+348));d.rectangle((123,top+51,677,top+81),fill='#000080');txt(d,(136,top+58),action,16,'white')
        d.ellipse((142,top+111,200,top+169),fill='#e8ce49',outline='black');txt(d,(164,top+118),'!',31)
        lines(d,items,222,top+107,17,36)
        for x,t in [(378,'查看详情'),(534,'确定')]:bevel(d,(x,top+296,x+124,top+330));txt(d,(x+27,top+303),t,14)
    else:
        brand={'tianya':'#657d9a','xiaonei':'#42668c','renren':'#345e88','weibo':'#e7e5d8','tieba':'#edf3fb','blog':'#6c4b65'}.get(key,'#153969')
        d.rectangle((14,top+8,785,top+61),fill=brand);txt(d,(28,top+20),name,28,'white' if key not in ['weibo','tieba'] else '#b33229');txt(d,(240,top+28),'首页   发现   日志   相册   留言',15,'white' if key not in ['weibo','tieba'] else '#1d4f8a')
        d.rectangle((20,top+76,174,501),fill='#edf1f6');lines(d,['我的主页','最新消息','主题分类','友情链接','收藏与归档','帮助'],32,top+93,14,37)
        txt(d,(198,top+83),action,24,'#164779');lines(d,items,201,top+139,18,40)
        for y in [top+119,top+356,top+382]:d.line((195,y,771,y),fill='#aebcd0')
        txt(d,(203,top+365),'回复 12  |  浏览 128  |  收藏  |  返回目录',12,'#446b91')
        txt(d,(203,top+401),'这个页面的下一次更新，来自另一端的人。',14,'#555')
    return im
def main():
    assets=[]
    for w in WORK:
        for state in range(4):
            im=draw(w,state);id=f'workflow-{w[0]}-{state+1}';p=OUT/(id+'.png');im.save(p)
            assets.append(dict(id=id,path=str(p.relative_to(ROOT)),width=800,height=540,title=w[1]+' · '+w[5][state][0],category=w[3],kind='original-operation-reconstruction',workflow=w[0],state=state,preferred_range=w[4],source_url='spec/36-episode123-v4-production.md',date_basis='Original illustration, historical style reference; not original-era screen capture',content=w[5][state][1].split('|'),sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
    (ROOT/'data/episode123-v4-workflows.json').write_text(json.dumps({'version':4,'count':len(assets),'workflows':len(WORK),'assets':assets,'counting_rule':'Distinct operation + changed information/output. No recolour/crop variants. Original reconstruction, labelled in provenance rather than in the performed UI.'},ensure_ascii=False,indent=2))
    cols=4;thumb=320;h=216;sheet=Image.new('RGB',(cols*thumb,9*h),'#17191d')
    for i,w in enumerate(WORK):
        im=draw(w,i%4);im.thumbnail((thumb,h),Image.Resampling.NEAREST);sheet.paste(im,((i%cols)*thumb,(i//cols)*h))
    tmp=Path.home()/'tmp/vrc-old-web-map/episode123-v4';tmp.mkdir(parents=True,exist_ok=True);sheet.save(tmp/'workflow-contact.jpg',quality=92)
    print(len(assets),'distinct authored operation screens; contact:',tmp/'workflow-contact.jpg')
if __name__=='__main__':main()
