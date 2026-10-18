#!/usr/bin/env python3
"""Author the full-film overload UI atlases. No network, audio, or Unity operations.

These are original interfaces expressing general conditions, not replicas of a
specific outage. Three workflow images are recalled from the existing V4 art.
"""
from __future__ import annotations

import argparse
import json
import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageOps

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "art/export/fullfilm/overload"
DATA = ROOT / "data/fullfilm-overload.json"
PREVIEW = Path.home() / "tmp/vrc-old-web-map/fullfilm-overload"
W, H = 1280, 720
FONT_PATH = "/usr/share/fonts/noto-cjk/NotoSansCJK-Regular.ttc"
BOLD_PATH = "/usr/share/fonts/noto-cjk/NotoSansCJK-Bold.ttc"
MONO_PATH = "/usr/share/fonts/liberation/LiberationMono-Regular.ttf"


def face_index(path):
    for i in range(10):
        try:
            f = ImageFont.truetype(path, 20, index=i)
        except OSError:
            break
        if " SC" in " ".join(f.getname()):
            return i
    return 0


def font(size, bold=False, mono=False):
    p = MONO_PATH if mono else BOLD_PATH if bold else FONT_PATH
    return ImageFont.truetype(p, size, index=0 if mono else face_index(p))


def text(d, xy, s, size=24, fill="#253d4c", bold=False, mono=False):
    d.text(xy, s, font=font(size, bold, mono), fill=fill)


def gradient(im, box, top, bot):
    x0, y0, x1, y1 = map(int, box)
    d = ImageDraw.Draw(im)
    a = tuple(int(top[i:i+2], 16) for i in (1, 3, 5))
    b = tuple(int(bot[i:i+2], 16) for i in (1, 3, 5))
    for y in range(y0, y1):
        u = (y-y0)/max(1, y1-y0-1)
        d.line((x0, y, x1, y), fill=tuple(round(a[k]*(1-u)+b[k]*u) for k in range(3)))


def bevel(d, box, fill="#d8dee0", raised=True, width=2):
    x0, y0, x1, y1 = box
    d.rectangle(box, fill=fill)
    a, b = ("#f9fcfc", "#647b8a") if raised else ("#647b8a", "#ffffff")
    for k in range(width):
        d.line((x0+k, y1-k, x0+k, y0+k, x1-k, y0+k), fill=a)
        d.line((x1-k, y0+k, x1-k, y1-k, x0+k, y1-k), fill=b)


def button(d, box, label, selected=False):
    bevel(d, box, "#c7e3ef" if selected else "#e2e6e6", not selected)
    x0, y0, x1, y1 = box
    f = font(23, bold=selected)
    bb = d.textbbox((0, 0), label, font=f)
    d.text(((x0+x1-bb[2])/2, (y0+y1-25)/2-3), label, font=f, fill="#173c54")


def base(title, warning=False):
    im = Image.new("RGBA", (W, H), "#ced6d8")
    d = ImageDraw.Draw(im)
    bevel(d, (0, 0, W-1, H-1), "#d8dfe0", width=3)
    gradient(im, (5, 5, W-5, 49), "#8d302d" if warning else "#203e69", "#cb6755" if warning else "#709ab1")
    d = ImageDraw.Draw(im)
    d.rectangle((18, 17, 35, 35), fill="#deeceb", outline="#99cbd9", width=2)
    text(d, (48, 6), title, 27, "#ffffff", True)
    for x, label in ((1150, "_"), (1189, "□"), (1228, "×")):
        button(d, (x, 13, x+30, 41), label)
    d.rectangle((7, 51, W-8, 91), fill="#e9edeb")
    text(d, (23, 55), "文件    编辑    查看    工具    帮助", 22)
    d.line((8, 93, W-8, 93), fill="#879ca4")
    bevel(d, (10, 676, W-11, 709), "#c9d2d4", False, 1)
    text(d, (25, 678), "就绪", 19, "#435d69")
    text(d, (1050, 678), "UTF-8    ONLINE", 17, "#435d69", mono=True)
    return im


def inset(d, box, fill="#f5f6f1"):
    bevel(d, box, fill, False, 2)


def fit(im, path, box):
    src = Image.open(ROOT / path).convert("RGB")
    x0, y0, x1, y1 = map(int, box)
    src = ImageOps.contain(src, (x1-x0, y1-y0), Image.Resampling.LANCZOS)
    im.paste(src, (x0+(x1-x0-src.width)//2, y0+(y1-y0-src.height)//2))


RECALLS = [
    "art/export/episode123-v4-workflows/workflow-manga-3.png",
    "art/export/episode123-v4-workflows/workflow-subtitles-3.png",
    "art/export/episode123-v4-workflows/workflow-avatar-3.png",
]


def source_frame():
    im = base("共享文件夹  /  副本 000128")
    d = ImageDraw.Draw(im)
    text(d, (31, 106), "相同的作品，更多的副本", 42, bold=True)
    text(d, (34, 166), "作品内容", 23)
    inset(d, (28, 206, 790, 592), "#fffdf2")
    fit(im, RECALLS[0], (35, 211, 782, 586))
    d = ImageDraw.Draw(im)
    inset(d, (810, 206, 1250, 390))
    fit(im, RECALLS[1], (816, 212, 1244, 384))
    inset(d, (810, 407, 1250, 591))
    fit(im, RECALLS[2], (816, 413, 1244, 585))
    d = ImageDraw.Draw(im)
    text(d, (39, 619), "署名", 25, bold=True)
    d.line((140, 656, 1002, 656), fill="#81949b", width=2)
    text(d, (1057, 622), "副本", 24, "#627880")
    return im


def search_frame():
    im = base("搜索  /  那份可编辑的原文件", True)
    d = ImageDraw.Draw(im)
    inset(d, (26, 111, 1086, 166), "#fffdfa")
    text(d, (45, 115), "那份可编辑的原文件", 30, "#203a49")
    button(d, (1100, 111, 1251, 166), "查找", True)
    text(d, (32, 188), "找到 12,800 个相似结果", 24)
    for i, y in enumerate((240, 355, 470)):
        inset(d, (29, y, 1250, y+99), "#f8f8f1" if i != 1 else "#eaf2f2")
        d.rectangle((46, y+14, 154, y+85), fill="#9bafb3")
        d.polygon([(49,y+78),(80,y+39),(103,y+62),(124,y+29),(151,y+78)], fill="#c4d3cf")
        text(d, (177, y+6), "完整版 · 高清 · 一键获取", 29, "#244e83", True)
        text(d, (179, y+49), "内容相同    来源：网络    复制时间不同", 23, "#5b6c70")
        text(d, (1109, y+33), "打开", 24, "#a05545")
    bevel(d, (298, 590, 986, 660), "#eadbd0", True)
    text(d, (331, 600), "正在查找原始页面…", 35, "#783d35", True)
    return im


def maintenance_frame(fixed=False):
    im = base("维护队列  /  链接检查")
    d = ImageDraw.Draw(im)
    text(d, (30, 108), "有人正在修好其中一个问题", 40, bold=True)
    inset(d, (28, 186, 790, 654), "#f3f5f1")
    d.rectangle((33, 192, 785, 231), fill="#c5d5da")
    text(d, (46, 194), "项目                 状态                 操作", 23)
    rows = [("字幕工程 / ep.01", "路径缺失"), ("贴图资源 / texture", "等待检查"), ("旧教程 / readme", "等待检查"), ("世界工程 / world", "等待检查"), ("附件索引 / archive", "等待检查")]
    for i, (a, b) in enumerate(rows):
        y = 245+i*68
        if i % 2 == 0:
            d.rectangle((35,y-3,782,y+58), fill="#e3e9e5")
        text(d, (47,y+5), a, 24)
        text(d, (453,y+5), "已修复 ✓" if fixed and i == 0 else b, 24, "#337968" if fixed and i == 0 else "#86604b")
        text(d, (658,y+5), "检查", 22)
    inset(d, (811, 186, 1250, 654), "#e5e8dd")
    text(d, (833, 207), "缺失文件", 28, bold=True)
    text(d, (834, 268), "ep01_source.ass", 24, mono=True)
    text(d, (834, 323), "已找到保存的副本" if fixed else "重新指定文件的位置", 25, "#286b5b" if fixed else "#546b72")
    button(d, (836, 388, 1222, 451), "预览已恢复 ✓" if fixed else "重新连接文件", fixed)
    text(d, (836, 501), "完成 1 项" if fixed else "等待操作", 34, "#286b5b" if fixed else "#546b72", True)
    text(d, (836, 563), "队列仍在继续", 27, "#61757b")
    return im


def broken(d, box):
    x0,y0,x1,y1=box
    inset(d, box, "#e2e6df")
    cx,cy=(x0+x1)//2,(y0+y1)//2
    d.rectangle((cx-29,cy-24,cx+29,cy+24), outline="#829194", width=3)
    d.line((cx-27,cy+18,cx-10,cy-3,cx+3,cy+8,cx+25,cy-15), fill="#829194", width=3)
    d.line((cx-15,cy-32,cx+3,cy-8,cx-8,cy+8,cx+13,cy+33), fill="#e2e6df", width=10)
    d.line((cx-15,cy-32,cx+3,cy-8,cx-8,cy+8,cx+13,cy+33), fill="#977469", width=3)


def failure_frame(which):
    titles = ["内容播放器  /  已保存的文件", "社区存档  /  一篇旧帖", "浏览器  /  重新连接", "世界工程  /  版本检查"]
    anchors = ["文件还在，播放器没有响应", "正文还在，图片没有回来", "熟悉的入口，暂时无法访问", "工程还在，需要重新适配"]
    im = base(titles[which])
    d = ImageDraw.Draw(im)
    text(d, (31, 108), anchors[which], 43, bold=True)
    if which == 0:
        inset(d, (30, 187, 1248, 594), "#26343c")
        d.rounded_rectangle((356,269,923,477), radius=5, fill="#d9dcd7", outline="#8f9b9a", width=3)
        d.ellipse((392,311,457,376), outline="#987347", width=5)
        text(d, (416,310), "!", 41, "#987347", True)
        text(d, (482,300), "此内容暂时无法播放", 32, bold=True)
        text(d, (482,362), "需要的运行环境不可用", 25)
        d.rectangle((47,558,1229,579), fill="#10252d")
        d.rectangle((47,558,396,579), fill="#5a9da9")
        text(d, (41,615), "文件：memory.swf       已保存       位置可找到", 27)
    elif which == 1:
        inset(d, (30,190,1248,657), "#f5f4e9")
        text(d, (49,203), "楼主：xxxx     回复 128     收藏", 24, "#49606b")
        text(d, (49,254), "过程记在这里，图片里是当时的做法。", 31)
        for x in (49,442,835):
            broken(d, (x,319,x+363,506))
        text(d, (49,541), "文字仍然可以阅读。", 29)
        text(d, (49,596), "附件：image_01.jpg    image_02.jpg    image_03.jpg", 23, "#687d84")
    elif which == 2:
        inset(d, (30,187,1248,657), "#f2f4ee")
        inset(d, (50,208,1228,259), "#ffffff")
        text(d, (67,214), "https://community.example/", 27, mono=True)
        text(d, (121,328), "连接中断", 52, "#647985", True)
        text(d, (125,414), "请求没有得到回应。请稍后再试。", 33)
        button(d, (126,509,417,573), "重新加载")
        text(d, (125,606), "保留书签，等待重新连接。", 26, "#6d7c80")
    else:
        inset(d, (30,187,1248,657), "#dce3df")
        inset(d, (49,210,392,636), "#f0f2e9")
        text(d, (66,225), "Project", 28, bold=True, mono=True)
        for i,s in enumerate(("▸ Assets", "  ▸ Scenes", "  ▸ Models", "  ▸ Textures", "  ▸ Scripts", "  ▸ Archive")):
            text(d, (67,285+i*48), s, 25, mono=True)
        text(d, (428,215), "资源已找到 ✓", 33, "#427563", True)
        text(d, (428,278), "运行环境已经改变", 34, "#8e6647", True)
        for i,s in enumerate(("重新导入资源", "检查脚本接口", "更新材质与设置")):
            y=355+i*67
            d.rectangle((431,y+8,455,y+32), outline="#7a8c8d", width=2)
            text(d,(475,y),s,28)
        button(d,(428,571,952,628),"开始逐项检查")
    return im


def loading_frame():
    im=base("社区  /  熟悉的页面")
    d=ImageDraw.Draw(im)
    text(d,(31,108),"布局回来了，回应还没有回来",42,bold=True)
    inset(d,(29,189,1250,654),"#f0f2eb")
    for row,y in enumerate((214,354,494)):
        broken(d,(51,y,276,y+116))
        text(d,(310,y+3),"加载中…",34,"#789096",True)
        text(d,(312,y+62),"正在等待图片和附件",25,"#8a9b9a")
        text(d,(1075,y+38),"已阅",25,"#778c91")
    return im


def tile_frame(i):
    im=Image.new("RGBA",(512,320),(0,0,0,0))
    d=ImageDraw.Draw(im)
    if i < 3:
        bevel(d,(0,0,511,319),"#d8dedc")
        gradient(im,(3,3,509,30),"#45637a","#8aa4aa")
        fit(im,RECALLS[i],(8,37,504,273))
        d=ImageDraw.Draw(im)
        text(d,(12,279),"署名：",20,"#5b7177")
        d.line((87,309,476,309),fill="#94a3a5")
    elif i == 3:
        d.rectangle((2,95,509,223),fill="#f2edcf",outline="#a69975",width=3)
        text(d,(29,108),"翻译 · 校对 · 嵌字",36,"#5d6655",True)
        text(d,(30,161),"xxxx / xxxx / xxxx",29,"#7f806b",mono=True)
    elif i in (4,5,6,7):
        bevel(d,(0,0,511,319),"#e2e5de")
        gradient(im,(3,3,509,38),"#9c5e50" if i<6 else "#52777d","#c29a80" if i<6 else "#87a4a6")
        text(d,(15,4),["相似结果","相关推荐","维护队列","旧版社区"][i-4],22,"#ffffff",True)
        if i<6:
            for n,y in enumerate((60,139,218)):
                d.rectangle((14,y,118,y+65),fill="#aabcb7")
                d.polygon([(16,y+60),(38,y+26),(63,y+46),(89,y+13),(115,y+60)],fill="#d2dcd1")
                text(d,(135,y),"完整版 · 高清",24,"#355777",True)
                text(d,(136,y+36),"来源：网络   立即查看",18,"#6c7c7b")
        else:
            for y in (60,131,202):
                d.rectangle((15,y,496,y+57),fill="#eef0e7")
                text(d,(26,y+10),"等待检查…" if i==6 else "加载中…",26,"#80918d")
    elif i in (8,9,13,14,15):
        bevel(d,(2,68,509,252),"#d7ded8")
        s={8:"已转  已转  已转",9:"求资源  求资源",13:"沙发  顶  已阅",14:"链接在评论区",15:"原地址失效"}[i]
        text(d,(22,98),s,35,"#697a76",True)
        text(d,(23,169),"xxxx   xxxx   xxxx",23,"#91a09a",mono=True)
    elif i==10:
        # Arrow exactly follows the same down-right orientation in every copy.
        points=[(177,56),(177,245),(221,204),(259,278),(296,257),(255,186),(318,185)]
        d.polygon([(x+9,y+9) for x,y in points],fill=(10,21,26,130))
        d.polygon(points,fill="#f3f7e7",outline="#273e49",width=7)
    elif i==11:
        for k in range(12):
            a=k*math.tau/12
            c=int(70+185*k/11)
            x,y=256+76*math.sin(a),160-76*math.cos(a)
            d.ellipse((x-12,y-12,x+12,y+12),fill=(c,c,c,255))
    elif i==12:
        d.ellipse((151,55,361,265),outline=(236,178,124,230),width=9)
        d.ellipse((177,81,335,239),outline=(240,226,194,180),width=3)
    return im


def generate():
    OUT.mkdir(parents=True,exist_ok=True)
    PREVIEW.mkdir(parents=True,exist_ok=True)
    frames=[source_frame(),search_frame(),maintenance_frame(),maintenance_frame(True)]
    frames += [failure_frame(i) for i in range(4)]
    frames += [loading_frame()]
    atlas=Image.new("RGBA",(W*2,H*5),(0,0,0,0))
    for i,im in enumerate(frames):
        atlas.paste(im,((i%2)*W,(i//2)*H))
        im.convert("RGB").save(PREVIEW/f"focus-{i:02d}.jpg",quality=93)
    atlas.save(OUT/"overload-focus-atlas.png",optimize=True)
    tiles=Image.new("RGBA",(2048,1280),(0,0,0,0))
    for i in range(16):
        tiles.paste(tile_frame(i),((i%4)*512,(i//4)*320))
    tiles.save(OUT/"overload-field-atlas.png",optimize=True)
    # Contact sheet is a review artifact, never a Unity asset.
    sheet=Image.new("RGB",(1280,1080),"#15212b")
    for i,im in enumerate(frames):
        sheet.paste(im.convert("RGB").resize((426,240)),((i%3)*426,(i//3)*360))
        text(ImageDraw.Draw(sheet),((i%3)*426+12,(i//3)*360+249),f"{i:02d}  "+["来源脱落","发现困难","维护中","一次修复","播放器失效","图床缺失","入口中断","重新适配","布局返回"][i],23,"#e0e4db")
    sheet.save(PREVIEW/"contact.jpg",quality=94)
    data={
        "schema":"oldwebmap.fullfilm.overload.v1",
        "duration":256,"start":133.12,"end":186.88,"row_seconds":0.08,
        "scene":"Assets/Scenes/OWM_FullFilm_v1.unity",
        "asset_root":"Assets/OldWebMap/FullFilm/Revision1/Overload",
        "parent":"FullFilm Continuation","root":"FullFilm Overload","reference_eye":[0,1.35,-2.8],
        "assets":[
            {"file":"overload-focus-atlas.png","width":2560,"height":3600,"columns":2,"rows":5,"cell_width":1280,"cell_height":720,"kind":"original-explanatory-interface","fact_claims":[]},
            {"file":"overload-field-atlas.png","width":2048,"height":1280,"columns":4,"rows":4,"cell_width":512,"cell_height":320,"kind":"original-explanatory-interface","fact_claims":[]},
        ],
        "recalls":[{"path":p,"kind":"existing-authored-workflow","credit":"前半已制作工作流画面；保留既有来源索引","operation":"composite-recall"} for p in RECALLS],
        "schedule":[
            {"scene":14,"start":133.12,"end":143.36,"focus":0,"anchor":"相同的作品，更多的副本","action":"署名标签脱落；副本继续推进","minimum_hold":8.8},
            {"scene":15,"start":143.36,"end":153.60,"focus":1,"anchor":"正在查找原始页面…","action":"相似结果和复制光标逼近；中央阅读锥保留","minimum_hold":8.8},
            {"scene":16,"start":153.60,"end":168.96,"focus":2,"anchor":"有人正在修好其中一个问题","action":"159.36一项修复完成，队列继续；163.84—166.40冻结暗下；166.40熟悉版式返回","minimum_hold":4.0},
            {"scene":17,"start":168.96,"end":173.44,"focus":4,"anchor":"文件还在，播放器没有响应","kind":"general-condition","minimum_hold":3.68},
            {"scene":17,"start":173.44,"end":177.92,"focus":5,"anchor":"正文还在，图片没有回来","kind":"general-condition","minimum_hold":3.68},
            {"scene":17,"start":177.92,"end":182.40,"focus":6,"anchor":"熟悉的入口，暂时无法访问","kind":"general-condition","minimum_hold":3.68},
            {"scene":17,"start":182.40,"end":186.88,"focus":7,"anchor":"工程还在，需要重新适配","kind":"general-condition","minimum_hold":3.68},
        ],
        "failure_interface_note":"原创示意，仅表达一般体验；不声称复刻Flash、天涯或任何具体SDK事件。community.example为保留示例域名。",
        "music_note":"本模块不以空note-on行推断静音，不改变音轨、声源或全局照明。",
        "comfort":{"central_clear_half_angle_degrees":20,"nearest_field_z":4.5,"head_locked_overlay":False,"full_view_wall":False},
        "implementation":{"merged_field_cards_per_group":144,"merged_cursors_per_group":32,"runtime_allocations":False,"auto_sample":False},
    }
    DATA.write_text(json.dumps(data,ensure_ascii=False,indent=2)+"\n")
    return data


def check():
    data=json.loads(DATA.read_text())
    assert data["start"]==133.12 and data["end"]==186.88
    for a in data["assets"]:
        assert Image.open(OUT/a["file"]).size==(a["width"],a["height"])
        assert not a["fact_claims"]
    for s in data["schedule"]:
        assert abs(s["start"]/.08-round(s["start"]/.08))<1e-5
        assert abs(s["end"]/.08-round(s["end"]/.08))<1e-5
        assert s["minimum_hold"]>=2.4
    assert all((ROOT/r["path"]).exists() for r in data["recalls"])
    assert all(data["schedule"][i]["end"]==data["schedule"][i+1]["start"] for i in range(6))
    print(json.dumps({"success":True,"focus_frames":9,"field_tiles":16,"readable_anchors":7,"minimum_hold":3.68,"assets":data["assets"],"preview":str(PREVIEW/"contact.jpg")},ensure_ascii=False,indent=2))


if __name__=="__main__":
    parser=argparse.ArgumentParser()
    parser.add_argument("--check",action="store_true",help="Check existing outputs without rewriting them")
    args=parser.parse_args()
    if not args.check:
        generate()
    check()
