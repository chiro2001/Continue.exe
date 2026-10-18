#!/usr/bin/env python3
"""Draw original, brand-free working interfaces for the 186.88–256 s ensemble.

These are contemporary in-world work screens, never source evidence. All visible
content is drawn here; the one memory image is an already rendered V5 scene.
"""
from pathlib import Path
import json
import math
import shutil
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "art/export/fullfilm/renewal"
PREVIEW = Path.home() / "tmp/vrc-old-web-map/fullfilm-renewal"
FONT = "/usr/share/fonts/noto-cjk/NotoSansCJK-Regular.ttc"
BOLD = "/usr/share/fonts/noto-cjk/NotoSansCJK-Bold.ttc"
MONO = "/usr/share/fonts/noto/NotoSansMono-Regular.ttf"
W, H = 1536, 960
BG = (16, 25, 41)
INK = (229, 237, 240)
MUTED = (135, 159, 177)
MINT = (112, 227, 197)
GOLD = (239, 191, 123)
CORAL = (246, 135, 129)
BLUE = (130, 186, 243)


def font(size, bold=False, mono=False):
    return ImageFont.truetype(MONO if mono else BOLD if bold else FONT, size, index=2 if not mono else 0)


def text(d, p, s, n=28, fill=INK, bold=False, mono=False):
    d.text(p, s, font=font(n, bold, mono), fill=fill)


def rr(d, box, fill, radius=18, outline=None, width=2):
    d.rounded_rectangle(box, radius, fill, outline, width)


def shell(title, sub, accent=MINT):
    im = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(im)
    for y in range(H):
        c = tuple(int(BG[i] + (4, 7, 12)[i] * (1 - y / H)) for i in range(3))
        d.line((0, y, W, y), fill=c)
    rr(d, (12, 12, W - 12, H - 12), None, 28, (74, 98, 119), 3)
    rr(d, (34, 32, 99, 97), (36, 63, 73), 16)
    d.line((51, 66, 63, 78, 84, 51), fill=accent, width=6)
    text(d, (123, 33), title, 35, INK, True)
    text(d, (123, 80), sub, 20, MUTED)
    for x, c in [(1417, MINT), (1450, GOLD), (1483, CORAL)]:
        d.ellipse((x, 52, x + 13, 65), fill=c)
    d.line((34, 129, W - 34, 129), fill=(56, 76, 98), width=2)
    return im, d


def chip(d, box, s, color=MINT, n=22):
    rr(d, box, tuple(int(x * .16 + 15) for x in color), 12, tuple(int(x * .48) for x in color))
    text(d, (box[0] + 15, box[1] + 8), s, n, color)


def check(d, x, y, color=MINT):
    d.line((x, y + 9, x + 10, y + 19, x + 29, y - 6), fill=color, width=5)


def island(d, box, variant=0):
    """Original dimensional picture also used by the physical layered preview."""
    x, y, r, b = box
    cx, cy = (x+r)/2, y+(b-y)*.65
    sx, sy = (r-x)/610, (b-y)/460
    def p(a, z): return (cx+a*sx, cy+z*sy)
    d.polygon([p(-265,-40),p(0,-139),p(267,-40),p(0,99)], fill=(57,116,126))
    d.polygon([p(-265,-40),p(0,99),p(0,151),p(-265,10)], fill=(28,57,75))
    d.polygon([p(267,-40),p(0,99),p(0,151),p(267,10)], fill=(20,42,64))
    for i in range(-3,4):
        d.line([p(-220+i*27,-19+i*10),p(50+i*27,75-i*10)], fill=(76,142,149), width=2)
    # A small shelter, seats and a suspended paper-star sculpture.
    for a in (-115, -8):
        d.line([p(a,-55),p(a,-189)], fill=GOLD, width=max(3,int(10*sx)))
    d.polygon([p(-153,-173),p(-64,-235),p(26,-174),p(-64,-142)], fill=(106,181,174))
    d.polygon([p(-153,-173),p(-64,-142),p(-64,-125),p(-153,-156)], fill=(44,89,104))
    for a in (100,178):
        d.line([p(a,-2),p(a,-48)], fill=(176,149,119), width=max(3,int(8*sx)))
    d.line([p(76,-43),p(199,-43)], fill=(222,176,125), width=max(5,int(18*sx)))
    star = [p(98 + math.cos(i*math.pi/5)* (38 if i%2==0 else 16), -143+math.sin(i*math.pi/5)*(38 if i%2==0 else 16)) for i in range(10)]
    d.polygon(star, fill=GOLD)
    d.line([p(98,-182),p(98,-228)], fill=(139,176,186), width=2)
    for a,z,rad in [(-195,-58,12),(198,-29,14),(25,40,9)]:
        px,py=p(a,z); d.ellipse((px-rad*sx,py-rad*sy,px+rad*sx,py+rad*sy),fill=MINT)
    if variant:
        for a,z in [(43,-5),(-72,28)]:
            px,py=p(a,z); d.ellipse((px-7,py-29,px+7,py-14),fill=INK);d.line((px,py-14,px,py+6),fill=BLUE,width=7)


def tool(state):
    titles = ["先让一件小事，运行起来", "哪里不对？把问题留下来", "有人回应，一起验证", "可用的方法，也留在这里"]
    im,d=shell("层间 / 一个整理图层的小工具", titles[state])
    rr(d,(34,154,960,806),(22,38,56),20,(56,81,101))
    text(d,(62,176),"空间预览",28,INK,True)
    chip(d,(748,173,932,216),["v0.1","检查中","v0.2","v0.3"][state],GOLD)
    for i in range(9):
        yy=290+i*50; d.line((72,yy,923,yy),fill=(33,56,73))
    for i in range(11):
        xx=92+i*78;d.line((xx,254,xx,765),fill=(33,56,73))
    # Distinct, visibly separated layer cards. Error places the horizon in front.
    for i,(col,lab) in enumerate([(BLUE,"远景"),(GOLD,"桥面"),(MINT,"前景")]):
        order = [1,2,0][i] if state==1 else i
        xx,yy=128+order*120,260+order*89
        rr(d,(xx,yy,xx+490,yy+260),tuple(int(v*.18+15) for v in col),15,col,3)
        text(d,(xx+22,yy+16),lab,22,col)
        if i==0:
            d.ellipse((xx+334,yy+45,xx+387,yy+98),fill=GOLD)
            d.polygon([(xx+25,yy+205),(xx+160,yy+93),(xx+270,yy+197),(xx+388,yy+109),(xx+467,yy+217)],fill=(54,98,132))
        elif i==1:
            d.arc((xx+85,yy+57,xx+370,yy+288),180,354,fill=GOLD,width=22)
            d.line((xx+45,yy+215,xx+440,yy+215),fill=GOLD,width=8)
        else:
            for a in range(5):
                d.line((xx+64+a*90,yy+220,xx+74+a*90,yy+156-a%2*45),fill=MINT,width=8)
    if state==1:
        chip(d,(250,714,806,770),"前景被压住了，顺序是不是反了？",CORAL,25)
    elif state>=2:
        check(d,853,737);text(d,(638,718),"近 / 中 / 远",24,MINT)
    rr(d,(987,154,1502,806),(24,34,50),20,(61,77,98))
    text(d,(1014,177),"把保存的说明读懂",27,INK,True)
    text(d,(1014,233),"想让三个图层能前后调整。",25)
    chip(d,(1013,283,1474,329),"AI 辅助 · 先解释，再动手改",BLUE,22)
    lines = ["layers = [far, mid, near]", "for layer in layers:", "  layer.depth = order * 0.25", "  preview.update(layer)"]
    for i,s in enumerate(lines): text(d,(1017,373+i*44),s,21,BLUE if i%2==0 else MUTED,mono=True)
    d.line((1015,575,1473,575),fill=(60,77,97),width=2)
    for i,s in enumerate(["保留原说明中的作者", "改过的地方写进 README", "用自己的图层重新测试"]):
        check(d,1020,623+i*51);text(d,(1064,605+i*51),s,23)
    rr(d,(34,831,1501,922),(30,52,62),16)
    text(d,(62,852),["读说明 → 运行 → 检查","问题已写清楚，等待一个回应","第三层改到最前面，再试一次","README   原作者 xxxx  /  修改 xxxx  /  测试 xxxx"][state],28,MINT if state==3 else INK)
    return im


def subtitles(state):
    im,d=shell("让这句话，变得读得懂",["初译不是终点", "一边对照，一边改", "校对和时间轴，都有人接住"][state],GOLD)
    rr(d,(34,154,1502,470),(21,40,57),20,(57,79,98))
    d.rectangle((58,180,755,442),fill=(22,49,67))
    island(d,(84,204,730,423),1)
    text(d,(808,188),"原文",23,MUTED)
    text(d,(808,237),"Leave a light for the next one.",28,INK,mono=True)
    text(d,(808,309),"初译（AI 辅助）",22,BLUE)
    text(d,(808,353),"为下一个人留下灯光。",29,BLUE)
    rr(d,(34,494,1502,794),(26,37,54),18,(57,79,98))
    for i,(a,b,c) in enumerate([("01","00:12.04","我们在桥的另一头等你。"),("02","00:14.20","给后来的人，留一盏灯。"),("03","00:17.12","做完以后，也把方法留下。")]):
        yy=524+i*79
        if i==1: rr(d,(54,yy-5,1476,yy+64),(42,68,77),9)
        text(d,(75,yy),a,26,MUTED,mono=True);text(d,(161,yy),b,26,BLUE,mono=True)
        text(d,(434,yy),c if state else ("为下一个人留下灯光。" if i==1 else c),29,GOLD if i==1 else INK)
        if state and i<=state:check(d,1414,yy+20)
    text(d,(61,833),["还要听语气，还要检查停留的时间。","“给后来的人”——这句更像我们说的话。","初译（AI 辅助） xxxx   ·   校对 xxxx   ·   时间轴 xxxx"][state],27,GOLD)
    for i in range(84):
        x=61+i*17; amp=10+int(15*abs(math.sin(i*.72))*abs(math.sin(i*.21)))
        d.line((x,917-amp,x,917+amp),fill=MINT if i<60 else (69,91,107),width=5)
    return im


def world(state):
    im,d=shell("星桥 / 我们做的一个小世界",["从自己的草图开始", "模型、材质、相聚的位置", "试走过，试坐过，再邀请别人"][state],MINT)
    rr(d,(34,154,1044,850),(21,40,57),20,(57,79,98))
    island(d,(87,203,998,826),state>0)
    if state==0:
        for x in range(100,1010,70): d.line((x,199,x,812),fill=(54,75,89),width=1)
    rr(d,(1069,154,1502,850),(27,38,53),20,(58,79,98))
    text(d,(1098,185),"自己的东西",27,INK,True)
    for i,(s,c) in enumerate([("原创草图",GOLD),("简单模型",MINT),("星形配饰",BLUE),("碰撞与站位",MUTED),("邀请同伴试走",CORAL)]):
        yy=267+i*88;check(d,1102,yy+23,c);text(d,(1148,yy),s,27,c)
    chip(d,(1097,744,1473,810),["草稿 v0.1","一起试走 v0.2","已发布 v0.3"][state],MINT,25)
    text(d,(63,884),"原画 xxxx   ·   建模 xxxx   ·   世界 xxxx   ·   试玩 xxxx",27,MINT)
    return im


def share(state):
    # A square-ish post is cropped inside a genuinely raised, portrait frame.
    im=Image.new("RGB",(960,1280),BG); d=ImageDraw.Draw(im)
    rr(d,(18,18,942,1262),None,28,(74,98,119),3)
    d.ellipse((55,62,133,140),fill=(66,108,122));text(d,(159,62),"xxxx",36,INK,True)
    text(d,(159,113),"把问题说清楚，等一个回应",22,MUTED)
    text(d,(55,193),"图层能动了，",43,INK,True);text(d,(55,252),"可第三层总是被遮住。",43,INK,True)
    text(d,(55,338),"I can move the layers.",27,BLUE,mono=True)
    text(d,(55,383),"Why is the near layer hidden?",27,BLUE,mono=True)
    rr(d,(55,460,905,817),(23,43,60),18,(62,96,110))
    for i,c in enumerate([BLUE,GOLD,MINT]):
        rr(d,(95+i*120,491+i*68,630+i*120,631+i*68),tuple(int(v*.22) for v in c),12,c,3)
    chip(d,(78,831,882,889),"源文件 + 复现步骤 + 试过的修改",GOLD,28)
    d.line((55,938,905,938),fill=(60,83,101),width=2)
    text(d,(55,966),"xxxx 回复",29,MINT,True)
    text(d,(55,1021),"第三层顺序反了，我也遇到过。",32,INK)
    text(d,(55,1073),"把它放到最前面，再一起试试。",32,INK)
    if state:
        chip(d,(55,1161,905,1220),"已复现  ✓     修改后通过",MINT,28)
    return im


def newcomer():
    im,d=shell("我的第一个版本 v0.1", "自己改了一点，也把做法写下来",GOLD)
    rr(d,(34,154,987,798),(21,42,58),20,(60,89,104));island(d,(63,170,952,771),1)
    rr(d,(1014,154,1502,798),(29,40,55),20,(60,82,101))
    text(d,(1043,188),"这一次，我来试试",29,INK,True)
    for i,s in enumerate(["换了一种星星颜色", "调整了座位间距", "给入口加了一个提示", "请保留原作者与说明"]):
        text(d,(1043,279+i*91),s,25,GOLD if i==3 else INK)
    chip(d,(1043,680,1472,746),"发布  ✓",MINT,30)
    text(d,(62,845),"原作者 xxxx    /    新贡献者 xxxx    /    帮助与试玩 xxxx",28,MINT)
    text(d,(62,894),"“学会了，我也想试试。”",26,MUTED)
    return im


def layer(kind):
    im=Image.new("RGBA",(768,512),(0,0,0,0));d=ImageDraw.Draw(im)
    if kind==0:
        d.ellipse((539,43,630,134),fill=GOLD)
        d.polygon([(0,450),(172,126),(360,404),(576,195),(768,425),(768,512),(0,512)],fill=(64,111,152,255))
    if kind==1:
        d.arc((101,164,682,740),183,358,fill=(239,191,123,255),width=43)
        d.line((33,430,737,430),fill=(239,191,123,255),width=13)
        for x in range(125,680,95):d.line((x,409,x,479),fill=(181,137,99,255),width=10)
    if kind==2:
        for i in range(11):
            x=17+i*76;d.line((x,508,x+16,389-i%3*43),fill=(112,227,197,255),width=16)
        d.ellipse((18,448,746,565),fill=(34,90,93,255))
    return im


def main():
    OUT.mkdir(parents=True,exist_ok=True);PREVIEW.mkdir(parents=True,exist_ok=True)
    made=[]
    for kind,fn,count in [("tool",tool,4),("subtitles",subtitles,3),("world",world,3),("share",share,2)]:
        for i in range(count):
            name=f"{kind}-{i}.png";fn(i).save(OUT/name);made.append(name)
    newcomer().save(OUT/"newcomer.png");made.append("newcomer.png")
    for i in range(3):layer(i).save(OUT/f"layer-{i}.png");made.append(f"layer-{i}.png")
    source=Path.home()/"tmp/vrc-old-web-map/episode123-v5/frames/v5_129.28.png"
    if not source.exists():source=Path.home()/"tmp/vrc-old-web-map/v5-review-20261017/movie-129.28.jpg"
    if source.exists():
        with Image.open(source) as im:im.convert("RGB").resize((1280,720),Image.Resampling.LANCZOS).save(OUT/"memory-v5-archive.png")
        made.append("memory-v5-archive.png")
    else:raise SystemExit("V5 archive memory frame missing; never substitute an invented historical screenshot")
    thumbs=[]
    for name in made:
        if name.startswith("layer-"):continue
        im=Image.open(OUT/name).convert("RGB");im.thumbnail((480,300))
        tile=Image.new("RGB",(500,340),(9,15,26));tile.paste(im,((500-im.width)//2,0));ImageDraw.Draw(tile).text((10,308),name,font=font(18),fill=INK);thumbs.append(tile)
    sheet=Image.new("RGB",(1500,340*math.ceil(len(thumbs)/3)),(9,15,26))
    for i,tile in enumerate(thumbs):sheet.paste(tile,((i%3)*500,(i//3)*340))
    sheet.save(PREVIEW/"interfaces-contact.jpg",quality=91)
    (PREVIEW/"art-manifest.json").write_text(json.dumps({"images":made,"memory_source":str(source),"original_interfaces":True,"audio_played":False},ensure_ascii=False,indent=2)+"\n")
    print(json.dumps({"images":len(made),"output":str(OUT),"preview":str(PREVIEW/"interfaces-contact.jpg")},ensure_ascii=False))


if __name__=="__main__":main()
